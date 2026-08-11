using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using System.Security.Claims;
using System.Text.Json;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DraftsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<KanbanHub>? _hub;
        private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public DraftsController(ApplicationDbContext context, IHubContext<KanbanHub>? hub = null)
        {
            _context = context;
            _hub = hub;
        }

        private static object? ConvertJsonElement(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number when value.TryGetInt32(out var intValue) => intValue,
                JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => value.EnumerateArray().Select(ConvertJsonElement).ToList(),
                JsonValueKind.Object => value.EnumerateObject()
                    .ToDictionary(prop => prop.Name, prop => ConvertJsonElement(prop.Value)),
                _ => value.ToString()
            };
        }

        private static Dictionary<string, object?> DeserializePayload(string? payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return new Dictionary<string, object?>();
            }

            try
            {
                var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson, PayloadSerializerOptions);
                if (payload == null)
                {
                    return new Dictionary<string, object?>();
                }

                return payload.ToDictionary(kv => kv.Key, kv => ConvertJsonElement(kv.Value));
            }
            catch
            {
                return new Dictionary<string, object?>();
            }
        }

        private static Dictionary<string, object?> BuildDraftResponse(TaskDraft draft)
        {
            var obj = new Dictionary<string, object?>
            {
                { "id", draft.Id },
                { "userId", draft.UserId },
                { "projectId", draft.ProjectId },
                { "title", draft.Title },
                { "description", draft.Description },
                { "createdAt", draft.CreatedAt },
                { "updatedAt", draft.UpdatedAt }
            };

            foreach (var kv in DeserializePayload(draft.PayloadJson))
            {
                if (!obj.ContainsKey(kv.Key))
                {
                    obj[kv.Key] = kv.Value;
                }
            }

            if (!obj.ContainsKey("statusName")) obj["statusName"] = "BACKLOG";
            if (!obj.ContainsKey("priority")) obj["priority"] = 3;

            return obj;
        }

        private static Dictionary<string, object?> BuildPayloadFromDto(DraftCreateUpdateDto dto)
        {
            var payload = new Dictionary<string, object?>
            {
                { "statusName", string.IsNullOrWhiteSpace(dto.StatusName) ? "BACKLOG" : dto.StatusName },
                { "priority", dto.Priority ?? 3 },
                { "assignee", dto.Assignee },
                { "label", dto.Label },
                { "startDate", dto.StartDate },
                { "dueDate", dto.DueDate },
                { "cycle", dto.Cycle },
                { "module", dto.Module }
            };

            if (dto.ProjectId.HasValue)
            {
                payload["projectId"] = dto.ProjectId.Value;
            }

            return payload;
        }

        private Guid GetUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
                throw new UnauthorizedAccessException("User not found in token");
            return Guid.Parse(userIdString);
        }

        [HttpGet]
        public async Task<IActionResult> GetDrafts([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? projectId = null)
        {
            var userId = GetUserId();
            var normalizedPage = page < 1 ? 1 : page;
            var normalizedPageSize = pageSize switch
            {
                < 1 => 20,
                > 100 => 100,
                _ => pageSize
            };

            var query = _context.TaskDrafts
                .AsNoTracking()
                .Where(d => d.UserId == userId);

            if (projectId.HasValue)
            {
                query = query.Where(d => d.ProjectId == projectId.Value);
            }

            var totalCount = await query.CountAsync();

            var drafts = await query
                .OrderByDescending(d => d.UpdatedAt)
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToListAsync();

            var result = drafts.Select(BuildDraftResponse).ToList();

            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);

            return Ok(new
            {
                data = result,
                pagination = new
                {
                    page = normalizedPage,
                    pageSize = normalizedPageSize,
                    totalCount,
                    totalPages,
                    hasPreviousPage = normalizedPage > 1,
                    hasNextPage = normalizedPage < totalPages
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateDraft([FromBody] DraftCreateUpdateDto dto)
        {
            var userId = GetUserId();

            var payloadObj = BuildPayloadFromDto(dto);

            var draft = new TaskDraft
            {
                UserId = userId,
                ProjectId = dto.ProjectId,
                Title = dto.Title,
                Description = dto.Description,
                PayloadJson = JsonSerializer.Serialize(payloadObj),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.TaskDrafts.Add(draft);
            await _context.SaveChangesAsync();
            await PublishDraftChangedAsync(userId, "created", draft);

            return Ok(new
            {
                data = BuildDraftResponse(draft)
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] DraftCreateUpdateDto dto)
        {
            var userId = GetUserId();
            var draft = await _context.TaskDrafts.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (draft == null)
                return NotFound(new { message = "Draft not found" });

            draft.Title = dto.Title ?? draft.Title;
            draft.Description = dto.Description ?? draft.Description;
            if (dto.ProjectId.HasValue)
            {
                draft.ProjectId = dto.ProjectId.Value;
            }

            var payloadObj = DeserializePayload(draft.PayloadJson);

            if (dto.StatusName != null) payloadObj["statusName"] = dto.StatusName;
            if (dto.Priority.HasValue) payloadObj["priority"] = dto.Priority.Value;
            if (dto.Assignee != null) payloadObj["assignee"] = dto.Assignee;
            if (dto.Label != null) payloadObj["label"] = dto.Label;
            if (dto.StartDate != null) payloadObj["startDate"] = dto.StartDate;
            if (dto.DueDate != null) payloadObj["dueDate"] = dto.DueDate;
            if (dto.Cycle != null) payloadObj["cycle"] = dto.Cycle;
            if (dto.Module != null) payloadObj["module"] = dto.Module;
            if (dto.ProjectId.HasValue) payloadObj["projectId"] = dto.ProjectId.Value;

            draft.PayloadJson = JsonSerializer.Serialize(payloadObj);
            draft.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await PublishDraftChangedAsync(userId, "updated", draft);

            return Ok(new
            {
                data = BuildDraftResponse(draft)
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDraft(Guid id)
        {
            var userId = GetUserId();
            var draft = await _context.TaskDrafts.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);
            
            if (draft == null)
            {
                return NotFound();
            }

            _context.TaskDrafts.Remove(draft);
            await _context.SaveChangesAsync();
            await PublishDraftChangedAsync(userId, "deleted", draft);

            return Ok(new { message = "Draft deleted" });
        }

        private Task PublishDraftChangedAsync(Guid userId, string action, TaskDraft draft)
        {
            return _hub == null
                ? Task.CompletedTask
                : _hub.PublishUserEntityChangedAsync(
                    userId,
                    "TaskDraft",
                    action,
                    draft.Id,
                    action == "deleted" ? null : BuildDraftResponse(draft));
        }

        public class DraftCreateUpdateDto
        {
            public string? Title { get; set; }
            public string? Description { get; set; }
            public string? StatusName { get; set; }
            public int? Priority { get; set; }
            public string? Assignee { get; set; }
            public string? Label { get; set; }
            public string? StartDate { get; set; }
            public string? DueDate { get; set; }
            public string? Cycle { get; set; }
            public string? Module { get; set; }
            public Guid? ProjectId { get; set; }
        }
    }
}
