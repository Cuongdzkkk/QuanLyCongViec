using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using TaskManagement.API.Filters;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api")]
    public class CustomFieldsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<KanbanHub> _hub;

        public CustomFieldsController(ApplicationDbContext context, IHubContext<KanbanHub> hub)
        {
            _context = context;
            _hub = hub;
        }

        // =========================================================================
        // 1. CUSTOM FIELD DEFINITIONS
        // =========================================================================

        [HttpGet("projects/{projectId}/custom-fields")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> GetProjectCustomFields(Guid projectId)
        {
            try
            {
                var fields = await _context.CustomFieldDefinitions
                    .Where(cfd => cfd.ProjectId == projectId && !cfd.IsDeleted)
                    .OrderBy(cfd => cfd.SortOrder)
                    .Select(cfd => new
                    {
                        cfd.Id,
                        cfd.ProjectId,
                        cfd.Name,
                        cfd.Key,
                        cfd.Type,
                        cfd.IsRequired,
                        cfd.OptionsJson,
                        cfd.IsVisible,
                        cfd.SortOrder,
                        cfd.CreatedAt,
                        cfd.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(new { statusCode = 200, message = "Success", data = fields });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost("projects/{projectId}/custom-fields")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,Admin")]
        public async Task<IActionResult> CreateProjectCustomField(Guid projectId, [FromBody] SaveCustomFieldRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new { statusCode = 400, message = "Tên trường tùy chỉnh không được để trống." });
                }

                var type = request.Type?.Trim();
                var validTypes = new[] { "Text", "Number", "Date", "Select", "Checkbox" };
                if (string.IsNullOrWhiteSpace(type) || !validTypes.Contains(type))
                {
                    return BadRequest(new { statusCode = 400, message = "Loại dữ liệu không hợp lệ. Chỉ chấp nhận: Text, Number, Date, Select, Checkbox." });
                }

                string? optionsJson = null;
                if (type == "Select")
                {
                    if (request.Options == null || !request.Options.Any(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        return BadRequest(new { statusCode = 400, message = "Loại dữ liệu Select yêu cầu danh sách tùy chọn (Options)." });
                    }
                    var cleanOptions = request.Options.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
                    optionsJson = JsonSerializer.Serialize(cleanOptions);
                }

                var key = GenerateSlugKey(request.Name);
                if (string.IsNullOrEmpty(key))
                {
                    return BadRequest(new { statusCode = 400, message = "Tên trường tùy chỉnh không hợp lệ để tạo key." });
                }

                var exists = await _context.CustomFieldDefinitions
                    .AnyAsync(cfd => cfd.ProjectId == projectId && cfd.Key == key && !cfd.IsDeleted);
                if (exists)
                {
                    return Conflict(new { statusCode = 409, message = $"Trường tùy chỉnh '{request.Name}' hoặc key tương đương đã tồn tại trong dự án." });
                }

                var maxSortOrder = await _context.CustomFieldDefinitions
                    .Where(cfd => cfd.ProjectId == projectId && !cfd.IsDeleted)
                    .Select(cfd => (int?)cfd.SortOrder)
                    .MaxAsync() ?? 0;

                var field = new CustomFieldDefinition
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    Name = request.Name.Trim(),
                    Key = key,
                    Type = type,
                    IsRequired = request.IsRequired,
                    OptionsJson = optionsJson,
                    IsVisible = request.IsVisible,
                    SortOrder = request.SortOrder >= 0 ? request.SortOrder : (maxSortOrder + 1),
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.CustomFieldDefinitions.Add(field);
                await _context.SaveChangesAsync();
                await _hub.PublishEntityChangedAsync(projectId, "custom-field", "upsert", field.Id, field);

                return CreatedAtAction(nameof(GetProjectCustomFields), new { projectId }, new
                {
                    statusCode = 201,
                    message = "Đã tạo trường tùy chỉnh thành công.",
                    data = field
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPut("projects/{projectId}/custom-fields/{fieldId}")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,Admin")]
        public async Task<IActionResult> UpdateProjectCustomField(Guid projectId, Guid fieldId, [FromBody] SaveCustomFieldRequest request)
        {
            try
            {
                var field = await _context.CustomFieldDefinitions
                    .FirstOrDefaultAsync(cfd => cfd.Id == fieldId && cfd.ProjectId == projectId && !cfd.IsDeleted);

                if (field == null)
                {
                    return NotFound(new { statusCode = 404, message = "Trường tùy chỉnh không tồn tại." });
                }

                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    var key = GenerateSlugKey(request.Name);
                    var exists = await _context.CustomFieldDefinitions
                        .AnyAsync(cfd => cfd.ProjectId == projectId && cfd.Id != fieldId && cfd.Key == key && !cfd.IsDeleted);
                    if (exists)
                    {
                        return Conflict(new { statusCode = 409, message = $"Tên trường tùy chỉnh mới trùng với một trường khác đã có." });
                    }
                    field.Name = request.Name.Trim();
                    field.Key = key;
                }

                if (field.Type == "Select")
                {
                    if (request.Options != null && request.Options.Any(x => !string.IsNullOrWhiteSpace(x)))
                    {
                        var cleanOptions = request.Options.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
                        field.OptionsJson = JsonSerializer.Serialize(cleanOptions);
                    }
                }

                field.IsRequired = request.IsRequired;
                field.IsVisible = request.IsVisible;
                if (request.SortOrder >= 0)
                {
                    field.SortOrder = request.SortOrder;
                }
                field.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await _hub.PublishEntityChangedAsync(projectId, "custom-field", "upsert", field.Id, field);

                return Ok(new { statusCode = 200, message = "Đã cập nhật trường tùy chỉnh.", data = field });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpDelete("projects/{projectId}/custom-fields/{fieldId}")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,Admin")]
        public async Task<IActionResult> DeleteProjectCustomField(Guid projectId, Guid fieldId)
        {
            try
            {
                var field = await _context.CustomFieldDefinitions
                    .FirstOrDefaultAsync(cfd => cfd.Id == fieldId && cfd.ProjectId == projectId && !cfd.IsDeleted);

                if (field == null)
                {
                    return NotFound(new { statusCode = 404, message = "Trường tùy chỉnh không tồn tại." });
                }

                // Xóa mềm để bảo toàn dữ liệu lịch sử
                field.IsDeleted = true;
                field.IsVisible = false;
                field.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await _hub.PublishEntityChangedAsync(projectId, "custom-field", "deleted", fieldId);

                return Ok(new { statusCode = 200, message = "Đã xóa trường tùy chỉnh thành công (dữ liệu cũ được bảo lưu)." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // =========================================================================
        // 2. CUSTOM FIELD VALUES
        // =========================================================================

        [HttpGet("worktasks/{taskId}/custom-field-values")]
        public async Task<IActionResult> GetTaskCustomFieldValues(Guid taskId)
        {
            try
            {
                var task = await _context.WorkTasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId && !t.IsDeleted);
                if (task == null)
                {
                    return NotFound(new { statusCode = 404, message = "Công việc không tồn tại." });
                }

                // Check authorization
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out Guid userId) || userId == Guid.Empty)
                {
                    return Unauthorized(new { statusCode = 401, message = "Unauthorized." });
                }

                var isMember = await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == task.ProjectId && pm.UserId == userId && pm.Status);
                if (!isMember)
                {
                    return StatusCode(403, new { statusCode = 403, message = "Forbidden. Bạn không thuộc dự án này." });
                }

                var values = await _context.CustomFieldValues
                    .Where(cfv => cfv.WorkTaskId == taskId && !cfv.FieldDefinition.IsDeleted)
                    .Select(cfv => new
                    {
                        cfv.FieldDefinitionId,
                        cfv.Value
                    })
                    .ToListAsync();

                return Ok(new { statusCode = 200, message = "Success", data = values });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPut("worktasks/{taskId}/custom-field-values")]
        public async Task<IActionResult> SaveTaskCustomFieldValues(Guid taskId, [FromBody] SaveCustomFieldValuesRequest request)
        {
            try
            {
                var task = await _context.WorkTasks.FirstOrDefaultAsync(t => t.Id == taskId && !t.IsDeleted);
                if (task == null)
                {
                    return NotFound(new { statusCode = 404, message = "Công việc không tồn tại." });
                }

                // Check authorization and write rights (not guest/stakeholder/viewer)
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(userIdString, out Guid userId) || userId == Guid.Empty)
                {
                    return Unauthorized(new { statusCode = 401, message = "Unauthorized." });
                }

                var member = await _context.ProjectMembers
                    .FirstOrDefaultAsync(pm => pm.ProjectId == task.ProjectId && pm.UserId == userId && pm.Status);
                if (member == null)
                {
                    return StatusCode(403, new { statusCode = 403, message = "Forbidden. Bạn không thuộc dự án này." });
                }

                var projectRole = member.ProjectRole.Trim().ToUpper();
                if (projectRole == "GUEST" || projectRole == "STAKEHOLDER" || projectRole == "VIEWER")
                {
                    return StatusCode(403, new { statusCode = 403, message = "Forbidden. Bạn không có quyền chỉnh sửa thuộc tính công việc." });
                }

                if (request.Values == null)
                {
                    return BadRequest(new { statusCode = 400, message = "Danh sách giá trị không được trống." });
                }

                // Validate toàn bộ trước khi ghi
                var definitionsMap = await _context.CustomFieldDefinitions
                    .Where(cfd => cfd.ProjectId == task.ProjectId && !cfd.IsDeleted)
                    .ToDictionaryAsync(cfd => cfd.Id);

                foreach (var item in request.Values)
                {
                    if (!definitionsMap.TryGetValue(item.FieldDefinitionId, out var def))
                    {
                        return BadRequest(new { statusCode = 400, message = $"Định nghĩa trường {item.FieldDefinitionId} không tồn tại hoặc đã bị xóa." });
                    }

                    var val = item.Value?.Trim();

                    // Check required
                    if (def.IsRequired && string.IsNullOrEmpty(val))
                    {
                        return BadRequest(new { statusCode = 400, message = $"Trường '{def.Name}' là bắt buộc, không được để trống." });
                    }

                    if (!string.IsNullOrEmpty(val))
                    {
                        // Check type format
                        if (def.Type == "Number" && !double.TryParse(val, out _))
                        {
                            return BadRequest(new { statusCode = 400, message = $"Giá trị '{val}' của trường '{def.Name}' phải là định dạng số." });
                        }

                        if (def.Type == "Date" && !DateTime.TryParse(val, out _))
                        {
                            return BadRequest(new { statusCode = 400, message = $"Giá trị '{val}' của trường '{def.Name}' phải là định dạng ngày tháng." });
                        }

                        if (def.Type == "Checkbox")
                        {
                            var lowerVal = val.ToLower();
                            if (lowerVal != "true" && lowerVal != "false")
                            {
                                return BadRequest(new { statusCode = 400, message = $"Giá trị của trường checkbox '{def.Name}' phải là true hoặc false." });
                            }
                        }

                        if (def.Type == "Select")
                        {
                            var options = new List<string>();
                            try
                            {
                                options = JsonSerializer.Deserialize<List<string>>(def.OptionsJson ?? "[]") ?? new List<string>();
                            }
                            catch { }

                            if (!options.Contains(val))
                            {
                                return BadRequest(new { statusCode = 400, message = $"Giá trị '{val}' không nằm trong danh sách lựa chọn của trường '{def.Name}'." });
                            }
                        }
                    }
                }

                // Thực hiện upsert
                var existingValues = await _context.CustomFieldValues
                    .Where(cfv => cfv.WorkTaskId == taskId)
                    .ToDictionaryAsync(cfv => cfv.FieldDefinitionId);

                foreach (var item in request.Values)
                {
                    var val = item.Value?.Trim();
                    if (existingValues.TryGetValue(item.FieldDefinitionId, out var existing))
                    {
                        existing.Value = val;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        var newValue = new CustomFieldValue
                        {
                            Id = Guid.NewGuid(),
                            WorkTaskId = taskId,
                            FieldDefinitionId = item.FieldDefinitionId,
                            Value = val,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.CustomFieldValues.Add(newValue);
                    }
                }

                await _context.SaveChangesAsync();
                await _hub.PublishEntityChangedAsync(
                    task.ProjectId,
                    "task-custom-field-values",
                    "upsert",
                    taskId,
                    new { taskId, values = request.Values });
                return Ok(new { statusCode = 200, message = "Đã lưu thông tin bổ sung thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // =========================================================================
        // PRIVATE HELPERS
        // =========================================================================

        private string GenerateSlugKey(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            string cleaned = name.Trim().ToLower();

            string[] signs = new string[] {
                "aaeoouiy", "áàảãạăắằẳẵặâấầẩẫậ", "éèẻẽẹêếềểễệ", "óòỏõọôốồổỗộơớờởỡợ", "úùủũụưứừửữự", "íìỉĩị", "ýỳỷỹỵ", "đ"
            };
            for (int i = 1; i < signs.Length; i++)
            {
                for (int j = 0; j < signs[i].Length; j++)
                {
                    cleaned = cleaned.Replace(signs[i][j].ToString(), signs[0][i - 1].ToString());
                }
            }
            cleaned = cleaned.Replace("đ", "d").Replace("Đ", "d");

            char[] arr = cleaned.ToCharArray();
            for (int i = 0; i < arr.Length; i++)
            {
                if (!char.IsLetterOrDigit(arr[i]))
                {
                    arr[i] = '_';
                }
            }
            cleaned = new string(arr);

            while (cleaned.Contains("__")) cleaned = cleaned.Replace("__", "_");
            return cleaned.Trim('_');
        }
    }

    public class SaveCustomFieldRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public List<string>? Options { get; set; }
        public bool IsVisible { get; set; } = true;
        public int SortOrder { get; set; }
    }

    public class SaveCustomFieldValuesRequest
    {
        public List<FieldValueInput> Values { get; set; } = new List<FieldValueInput>();
    }

    public class FieldValueInput
    {
        public Guid FieldDefinitionId { get; set; }
        public string? Value { get; set; }
    }
}
