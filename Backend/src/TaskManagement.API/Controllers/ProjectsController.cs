using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaskManagement.API.Filters;
using TaskManagement.API.Security;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.DTOs.Project;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/projects")]
    [Authorize]
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectService _projectService;
        private readonly ApplicationDbContext _context;
        private readonly IDataProtector _integrationSecretProtector;

        public ProjectsController(
            IProjectService projectService,
            ApplicationDbContext context,
            IDataProtectionProvider dataProtectionProvider)
        {
            _projectService = projectService;
            _context = context;
            _integrationSecretProtector = dataProtectionProvider.CreateProtector("SprintA.ProjectIntegrationSecrets.v1");
        }

        public sealed class ProjectIntegrationSetting
        {
            public string Provider { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public bool Enabled { get; set; }
            public string? Endpoint { get; set; }
            public string? ProjectKey { get; set; }
            public string? Secret { get; set; }
            public string? Notes { get; set; }
            public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        }

        public sealed class UpdateProjectIntegrationsRequest
        {
            public List<ProjectIntegrationSetting> Items { get; set; } = new();
        }

        public sealed class UpdateProjectExecutionRulesRequest
        {
            public ProjectExecutionRulesDto Rules { get; set; } = new();
        }

        public sealed class UpdateProjectCoverRequest
        {
            public string? CoverUrl { get; set; }
            public string? CoverAltText { get; set; }
            public string? Icon { get; set; }
        }

        private static readonly string[] HiddenProjectRoleOptionNames =
        {
            "superadmin",
            "system_admin",
            "system admin",
            "organization_admin",
            "organization admin",
            "accessadmin",
            "access admin",
            "member",
            "guest",
            "stakeholder",
            "user"
        };

        private static Dictionary<string, object?> ParseNavigationConfig(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new Dictionary<string, object?>();
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(raw) ?? new Dictionary<string, object?>();
            }
            catch (JsonException)
            {
                return new Dictionary<string, object?>();
            }
        }

        private static bool ReadFavoriteFlag(string? raw)
        {
            var config = ParseNavigationConfig(raw);
            if (!config.TryGetValue("favorite", out var value) || value == null) return false;

            return value switch
            {
                bool boolValue => boolValue,
                JsonElement json when json.ValueKind == JsonValueKind.True => true,
                JsonElement json when json.ValueKind == JsonValueKind.False => false,
                JsonElement json when json.ValueKind == JsonValueKind.String && bool.TryParse(json.GetString(), out var parsed) => parsed,
                string text when bool.TryParse(text, out var parsed) => parsed,
                _ => false
            };
        }

        private static string MergeFavoriteIntoNavigationConfig(string? raw, bool favorite)
        {
            var config = ParseNavigationConfig(raw);
            config["favorite"] = favorite;
            return JsonSerializer.Serialize(config);
        }

        private static ProjectExecutionRulesDto ReadExecutionRules(string? raw)
        {
            var config = ParseNavigationConfig(raw);
            if (!config.TryGetValue("executionRules", out var value) || value == null)
            {
                return ProjectExecutionRuleHelper.NormalizeExecutionRules(null);
            }

            try
            {
                if (value is JsonElement json)
                {
                    return ProjectExecutionRuleHelper.NormalizeExecutionRules(
                        json.Deserialize<ProjectExecutionRulesDto>());
                }

                if (value is ProjectExecutionRulesDto typed)
                {
                    return ProjectExecutionRuleHelper.NormalizeExecutionRules(typed);
                }

                return ProjectExecutionRuleHelper.NormalizeExecutionRules(
                    JsonSerializer.Deserialize<ProjectExecutionRulesDto>(JsonSerializer.Serialize(value)));
            }
            catch
            {
                return ProjectExecutionRuleHelper.NormalizeExecutionRules(null);
            }
        }

        private static string MergeExecutionRulesIntoNavigationConfig(string? raw, ProjectExecutionRulesDto rules)
        {
            var config = ParseNavigationConfig(raw);
            config["executionRules"] = ProjectExecutionRuleHelper.NormalizeExecutionRules(rules);
            return JsonSerializer.Serialize(config);
        }

        private static string MergeProjectVisualsIntoNavigationConfig(
            string? raw,
            string? coverUrl,
            string? coverAltText,
            string? icon,
            bool removeCover = false)
        {
            var config = ParseNavigationConfig(raw);
            if (removeCover)
            {
                config.Remove("cover");
                config.Remove("coverUrl");
                config.Remove("coverAltText");
            }
            else if (!string.IsNullOrWhiteSpace(coverUrl))
            {
                config["cover"] = coverUrl;
                config["coverAltText"] = coverAltText;
            }

            if (!string.IsNullOrWhiteSpace(icon))
            {
                config["icon"] = icon;
            }

            return JsonSerializer.Serialize(config);
        }

        private async Task<List<object>> BuildProjectRoleOptionsAsync()
        {
            var preferredOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["PM"] = 0,
                ["PO"] = 1,
                ["SM"] = 2,
                ["Project Lead"] = 3,
                ["Admin"] = 4,
                ["Developer"] = 5,
                ["QA"] = 6,
                ["Accountant"] = 7
            };

            var roleOptions = await _context.Roles
                .AsNoTracking()
                .Select(role => new
                {
                    role.Name,
                    role.Description
                })
                .ToListAsync();

            return roleOptions
                .Where(role => !HiddenProjectRoleOptionNames.Contains(ProjectExecutionRuleHelper.NormalizeProjectRole(role.Name)))
                .OrderBy(role => preferredOrder.TryGetValue(role.Name, out var rank) ? rank : int.MaxValue)
                .ThenBy(role => role.Name)
                .Select(role => (object)new
                {
                    value = role.Name,
                    label = role.Name,
                    description = role.Description
                })
                .ToList();
        }

        /// <summary>
        /// 5.2 Lấy danh sách dự án - chống N+1 Query Problem
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var projects = await _projectService.GetAllAsync();
            var favoriteMap = await _context.Projects
                .AsNoTracking()
                .Where(project => projects.Select(p => p.Id).Contains(project.Id))
                .ToDictionaryAsync(project => project.Id, project => ReadFavoriteFlag(project.NavigationConfig));
            var coverAltTextMap = await _context.Projects
                .AsNoTracking()
                .Where(project => projects.Select(p => p.Id).Contains(project.Id))
                .ToDictionaryAsync(project => project.Id, project => project.CoverAltText);

            return Ok(ApiResponse<object>.Success(projects.Select(project => new
            {
                project.Id,
                project.WorkspaceId,
                project.Name,
                project.Key,
                project.Description,
                project.StartDate,
                project.EndDate,
                project.Status,
                project.CreatorName,
                project.DepartmentId,
                project.DepartmentName,
                project.ActiveMemberCount,
                project.NetworkType,
                project.Cover,
                CoverAltText = coverAltTextMap.GetValueOrDefault(project.Id),
                project.Icon,
                project.LeadUserId,
                project.LeadName,
                project.CreatedAt,
                project.UpdatedAt,
                IsFavorite = favoriteMap.TryGetValue(project.Id, out var favorite) && favorite
            }).ToList()));
        }

        /// <summary>
        /// Returns ALL active projects with IsMember flag per current user.
        /// Dashboard uses this to show "Tham gia" (Join) for non-member projects.
        /// </summary>
        [HttpGet("discovery")]
        public async Task<IActionResult> GetAllForDiscovery()
        {
            var projects = await _projectService.GetAllForDiscoveryAsync();
            var favoriteMap = await _context.Projects
                .AsNoTracking()
                .Where(project => projects.Select(p => p.Id).Contains(project.Id))
                .ToDictionaryAsync(project => project.Id, project => ReadFavoriteFlag(project.NavigationConfig));
            var coverAltTextMap = await _context.Projects
                .AsNoTracking()
                .Where(project => projects.Select(p => p.Id).Contains(project.Id))
                .ToDictionaryAsync(project => project.Id, project => project.CoverAltText);

            return Ok(ApiResponse<object>.Success(projects.Select(project => new
            {
                project.Id,
                project.WorkspaceId,
                project.Name,
                project.Key,
                project.Description,
                project.StartDate,
                project.EndDate,
                project.Status,
                project.CreatorName,
                project.DepartmentId,
                project.DepartmentName,
                project.ActiveMemberCount,
                project.NetworkType,
                project.Cover,
                CoverAltText = coverAltTextMap.GetValueOrDefault(project.Id),
                project.Icon,
                project.LeadUserId,
                project.LeadName,
                project.CreatedAt,
                project.UpdatedAt,
                project.IsMember,
                project.MyRole,
                IsFavorite = favoriteMap.TryGetValue(project.Id, out var favorite) && favorite
            }).ToList()));
        }

        [HttpGet("archived")]
        public async Task<IActionResult> GetArchived()
        {
            try
            {
                var projects = await _projectService.GetArchivedAsync();
                var favoriteMap = await _context.Projects
                    .AsNoTracking()
                    .Where(project => projects.Select(p => p.Id).Contains(project.Id))
                    .ToDictionaryAsync(project => project.Id, project => ReadFavoriteFlag(project.NavigationConfig));
                var coverAltTextMap = await _context.Projects
                    .AsNoTracking()
                    .Where(project => projects.Select(p => p.Id).Contains(project.Id))
                    .ToDictionaryAsync(project => project.Id, project => project.CoverAltText);

                return Ok(ApiResponse<object>.Success(projects.Select(project => new
                {
                    project.Id,
                    project.WorkspaceId,
                    project.Name,
                    project.Key,
                    project.Description,
                    project.StartDate,
                    project.EndDate,
                    project.Status,
                    project.CreatorName,
                    project.DepartmentId,
                    project.DepartmentName,
                    project.ActiveMemberCount,
                    project.NetworkType,
                    project.Cover,
                    CoverAltText = coverAltTextMap.GetValueOrDefault(project.Id),
                    project.Icon,
                    project.LeadUserId,
                    project.LeadName,
                    project.CreatedAt,
                    project.UpdatedAt,
                    project.IsMember,
                    project.MyRole,
                    IsFavorite = favoriteMap.TryGetValue(project.Id, out var favorite) && favorite
                }).ToList()));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = ex.Message });
            }
        }

        [HttpGet("{id:guid}")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var project = await _projectService.GetByIdAsync(id);
            if (project == null)
                return NotFound(ApiResponse<object>.Error("Dự án không tồn tại.", 404));

            var rawProject = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            return Ok(ApiResponse<object>.Success(new
            {
                project.Id,
                project.WorkspaceId,
                project.Name,
                project.Key,
                project.Description,
                project.StartDate,
                project.EndDate,
                project.Status,
                project.CreatorName,
                project.DepartmentId,
                project.DepartmentName,
                project.ActiveMemberCount,
                project.NetworkType,
                project.Cover,
                CoverAltText = rawProject?.CoverAltText,
                project.Icon,
                project.LeadUserId,
                project.LeadName,
                project.CreatedAt,
                project.UpdatedAt,
                IsFavorite = ReadFavoriteFlag(rawProject?.NavigationConfig)
            }));
        }

        [HttpGet("{projectId:guid}/settings")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,Admin")]
        public async Task<IActionResult> GetSettingsOverview(Guid projectId)
        {
            var project = await _projectService.GetByIdAsync(projectId);
            if (project == null)
                return NotFound(ApiResponse<object>.Error("Project not found.", 404));

            var members = await _projectService.GetMembersAsync(projectId);
            var rawProject = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId);
            var roleOptions = await BuildProjectRoleOptionsAsync();

            return Ok(ApiResponse<object>.Success(new
            {
                project = new
                {
                    project.Id,
                    project.WorkspaceId,
                    project.Name,
                    project.Key,
                    project.Description,
                    project.StartDate,
                    project.EndDate,
                    project.Status,
                    project.CreatorName,
                    project.DepartmentId,
                    project.DepartmentName,
                    project.ActiveMemberCount,
                    project.NetworkType,
                    project.Cover,
                    CoverAltText = rawProject?.CoverAltText,
                    project.Icon,
                    project.LeadUserId,
                    project.LeadName,
                    project.CreatedAt,
                    project.UpdatedAt,
                    IsFavorite = ReadFavoriteFlag(rawProject?.NavigationConfig),
                    IsArchived = rawProject?.IsArchived ?? false,
                    ExecutionRules = ReadExecutionRules(rawProject?.NavigationConfig)
                },
                members,
                roleOptions
            }));
        }

        [HttpGet("{projectId:guid}/execution-rules")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,Admin")]
        public async Task<IActionResult> GetExecutionRules(Guid projectId)
        {
            var project = await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == projectId && !item.IsDeleted);

            if (project == null)
            {
                return NotFound(ApiResponse<object>.Error("Project not found.", 404));
            }

            return Ok(ApiResponse<ProjectExecutionRulesDto>.Success(
                ReadExecutionRules(project.NavigationConfig)));
        }

        [HttpGet("{projectId:guid}/role-options")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,Admin")]
        public async Task<IActionResult> GetProjectRoleOptions(Guid projectId)
        {
            var projectExists = await _context.Projects
                .AsNoTracking()
                .AnyAsync(item => item.Id == projectId && !item.IsDeleted);

            if (!projectExists)
            {
                return NotFound(ApiResponse<object>.Error("Project not found.", 404));
            }

            return Ok(ApiResponse<object>.Success(await BuildProjectRoleOptionsAsync()));
        }

        [HttpPut("{projectId:guid}/execution-rules")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,Admin")]
        public async Task<IActionResult> UpdateExecutionRules(Guid projectId, [FromBody] UpdateProjectExecutionRulesRequest request)
        {
            var project = await _context.Projects
                .FirstOrDefaultAsync(item => item.Id == projectId && !item.IsDeleted);

            if (project == null)
            {
                return NotFound(ApiResponse<object>.Error("Project not found.", 404));
            }

            var normalizedRules = ProjectExecutionRuleHelper.NormalizeExecutionRules(request?.Rules);
            project.NavigationConfig = MergeExecutionRulesIntoNavigationConfig(project.NavigationConfig, normalizedRules);
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<ProjectExecutionRulesDto>.Success(normalizedRules, "Project execution rules updated."));
        }

        [HttpGet("{projectId:guid}/integrations")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,Admin")]
        public async Task<IActionResult> GetProjectIntegrations(Guid projectId)
        {
            var projectExists = await _context.Projects
                .AsNoTracking()
                .AnyAsync(project => project.Id == projectId && !project.IsDeleted);

            if (!projectExists)
            {
                return NotFound(ApiResponse<object>.Error("Project not found.", 404));
            }

            var group = GetProjectIntegrationGroup(projectId);
            var settings = await _context.SystemSettings
                .AsNoTracking()
                .Where(setting => setting.SettingGroup == group)
                .ToListAsync();

            var configuredItems = settings
                .Select(setting => DeserializeProjectIntegration(setting.Value))
                .Where(setting => setting != null)
                .Cast<ProjectIntegrationSetting>()
                .ToDictionary(
                    setting => setting.Provider.Trim().ToLowerInvariant(),
                    setting => setting);

            var items = GetDefaultProjectIntegrations()
                .Select(defaultItem =>
                {
                    if (!configuredItems.TryGetValue(defaultItem.Provider, out var existing))
                    {
                        return defaultItem;
                    }

                    return new ProjectIntegrationSetting
                    {
                        Provider = defaultItem.Provider,
                        DisplayName = existing.DisplayName,
                        Enabled = existing.Enabled,
                        Endpoint = existing.Endpoint,
                        ProjectKey = existing.ProjectKey,
                        Secret = null,
                        Notes = existing.Notes,
                        UpdatedAt = existing.UpdatedAt
                    };
                })
                .OrderBy(item => item.DisplayName)
                .ToList();

            return Ok(ApiResponse<object>.Success(items));
        }

        [HttpPut("{projectId:guid}/integrations")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,Admin")]
        public async Task<IActionResult> UpdateProjectIntegrations(Guid projectId, [FromBody] UpdateProjectIntegrationsRequest request)
        {
            var projectExists = await _context.Projects
                .AnyAsync(project => project.Id == projectId && !project.IsDeleted);

            if (!projectExists)
            {
                return NotFound(ApiResponse<object>.Error("Project not found.", 404));
            }

            var requestedItems = (request.Items ?? new List<ProjectIntegrationSetting>())
                .Select(NormalizeProjectIntegration)
                .GroupBy(item => item.Provider)
                .Select(group => group.Last())
                .ToList();

            if (requestedItems.Count == 0)
            {
                return BadRequest(ApiResponse<object>.Error("At least one integration item is required."));
            }

            var validProviders = GetDefaultProjectIntegrations()
                .Select(item => item.Provider)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (requestedItems.Any(item => !validProviders.Contains(item.Provider)))
            {
                return BadRequest(ApiResponse<object>.Error("One or more integration providers are invalid."));
            }

            var group = GetProjectIntegrationGroup(projectId);
            var existingSettings = await _context.SystemSettings
                .Where(setting => setting.SettingGroup == group)
                .ToListAsync();

            foreach (var item in requestedItems)
            {
                var existing = existingSettings.FirstOrDefault(setting => setting.Key == item.Provider);
                var existingItem = existing == null ? null : DeserializeProjectIntegration(existing.Value);
                if (existing == null)
                {
                    existing = new TaskManagement.Domain.Entities.SystemSetting
                    {
                        Id = Guid.NewGuid(),
                        SettingGroup = group,
                        Key = item.Provider,
                        Description = $"Project integration settings for {item.DisplayName}"
                    };
                    _context.SystemSettings.Add(existing);
                }

                var storedItem = new ProjectIntegrationSetting
                {
                    Provider = item.Provider,
                    DisplayName = item.DisplayName,
                    Enabled = item.Enabled,
                    Endpoint = item.Endpoint,
                    ProjectKey = item.ProjectKey,
                    Secret = ResolveStoredIntegrationSecret(item, existingItem),
                    Notes = item.Notes,
                    UpdatedAt = item.UpdatedAt
                };

                existing.Value = JsonSerializer.Serialize(storedItem);
                existing.Description = $"Project integration settings for {item.DisplayName}";
                existing.LastModifiedAt = DateTime.UtcNow;
            }

            var requestedProviders = requestedItems
                .Select(item => item.Provider)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var staleSettings = existingSettings
                .Where(setting => !requestedProviders.Contains(setting.Key))
                .ToList();

            if (staleSettings.Count > 0)
            {
                _context.SystemSettings.RemoveRange(staleSettings);
            }

            await _context.SaveChangesAsync();

            var responseItems = requestedItems.Select(SanitizeProjectIntegration).ToList();
            return Ok(ApiResponse<object>.Success(responseItems, "Project integrations updated successfully."));
        }

        [HttpPut("{id:guid}/favorite")]
        [ProjectAuthorize(ResourcePermissionCodes.ProjectWrite)]
        public async Task<IActionResult> UpdateFavorite(Guid id, [FromBody] JsonElement payload)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var userId))
                return Unauthorized(ApiResponse<object>.Error("Unauthorized.", 401));

            var project = await _context.Projects
                .Include(p => p.ProjectMembers)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (project == null)
                return NotFound(ApiResponse<object>.Error("Project not found.", 404));

            var favorite = true;
            if (payload.ValueKind == JsonValueKind.Object &&
                payload.TryGetProperty("favorite", out var favoriteProperty) &&
                (favoriteProperty.ValueKind == JsonValueKind.True || favoriteProperty.ValueKind == JsonValueKind.False))
            {
                favorite = favoriteProperty.GetBoolean();
            }

            project.NavigationConfig = MergeFavoriteIntoNavigationConfig(project.NavigationConfig, favorite);
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(new { id = project.Id, favorite }));
        }

        [HttpPost("{id:guid}/cover")]
        [ProjectAuthorize(ResourcePermissionCodes.ProjectWrite)]
        [RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> UpdateCover(
            Guid id,
            [FromForm] IFormFile? file,
            [FromForm] string? coverUrl,
            [FromForm] string? coverAltText,
            [FromForm] string? icon)
        {
            return await UpdateProjectCoverInternal(id, file, coverUrl, coverAltText, icon);
        }

        [HttpPut("{id:guid}/cover")]
        [ProjectAuthorize(ResourcePermissionCodes.ProjectWrite)]
        public async Task<IActionResult> UpdateCoverMetadata(Guid id, [FromBody] UpdateProjectCoverRequest request)
        {
            return await UpdateProjectCoverInternal(id, null, request.CoverUrl, request.CoverAltText, request.Icon);
        }

        private async Task<IActionResult> UpdateProjectCoverInternal(
            Guid id,
            IFormFile? file,
            string? coverUrl,
            string? coverAltText,
            string? icon)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var userId))
                return Unauthorized(ApiResponse<object>.Error("Unauthorized.", 401));

            var project = await _context.Projects
                .Include(item => item.ProjectMembers)
                .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted);

            if (project == null)
                return NotFound(ApiResponse<object>.Error("Project not found.", 404));

            var nextCoverUrl = TrimOrNull(coverUrl);
            if (file != null && file.Length > 0)
            {
                var validation = ValidateCoverUpload(file);
                if (validation != null) return validation;
                ValidatedUpload validated;
                try { validated = await UploadSecurity.ReadPublicImageAsync(file); }
                catch (InvalidDataException exception) { return BadRequest(ApiResponse<object>.Error(exception.Message)); }

                var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "project-covers");
                Directory.CreateDirectory(uploadsRoot);
                var fileName = $"{Guid.NewGuid():N}{validated.Extension}";
                var filePath = UploadSecurity.ResolveUnderRoot(uploadsRoot, fileName);
                await System.IO.File.WriteAllBytesAsync(filePath, validated.Bytes);

                nextCoverUrl = $"/uploads/project-covers/{fileName}";
            }

            if (string.IsNullOrWhiteSpace(nextCoverUrl) && string.IsNullOrWhiteSpace(icon))
            {
                return BadRequest(ApiResponse<object>.Error("Provide a cover file, coverUrl, or icon."));
            }

            if (!string.IsNullOrWhiteSpace(nextCoverUrl))
            {
                project.CoverUrl = nextCoverUrl;
                project.CoverAltText = TrimOrNull(coverAltText) ?? $"Cover for {project.Name}";
            }

            project.NavigationConfig = MergeProjectVisualsIntoNavigationConfig(
                project.NavigationConfig,
                nextCoverUrl,
                project.CoverAltText,
                TrimOrNull(icon));
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(new
            {
                project.Id,
                project.CoverUrl,
                project.CoverAltText,
                icon = TrimOrNull(icon),
                project.NavigationConfig
            }));
        }

        [HttpDelete("{id:guid}/cover")]
        [ProjectAuthorize(ResourcePermissionCodes.ProjectWrite)]
        public async Task<IActionResult> DeleteCover(Guid id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var userId))
                return Unauthorized(ApiResponse<object>.Error("Unauthorized.", 401));

            var project = await _context.Projects
                .Include(item => item.ProjectMembers)
                .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted);

            if (project == null)
                return NotFound(ApiResponse<object>.Error("Project not found.", 404));

            project.CoverUrl = null;
            project.CoverAltText = null;
            project.NavigationConfig = MergeProjectVisualsIntoNavigationConfig(
                project.NavigationConfig,
                null,
                null,
                null,
                removeCover: true);
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(new
            {
                project.Id,
                project.CoverUrl,
                project.CoverAltText,
                project.NavigationConfig
            }));
        }

        private IActionResult? ValidateCoverUpload(IFormFile file)
        {
            if (file.Length > 5 * 1024 * 1024)
            {
                return BadRequest(ApiResponse<object>.Error("Project cover must be 5MB or smaller."));
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };
            if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<object>.Error("Project cover must be PNG, JPG, JPEG, or WEBP."));
            }

            var contentType = (file.ContentType ?? string.Empty).Split(';', 2)[0].Trim();
            var allowedContentTypes = new[] { "image/png", "image/jpeg", "image/webp" };
            if (!allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<object>.Error("Invalid project cover content type."));
            }

            return null;
        }

        private static string? TrimOrNull(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        [HttpPost]
        // [RequirePermission("projects.dashboard.create")]
        public async Task<IActionResult> Create([FromBody] CreateProjectDto dto)
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid creatorId))
                    return Unauthorized(ApiResponse<object>.Error("Unauthorized.", 401));

                var result = await _projectService.CreateAsync(creatorId, dto);
                return CreatedAtAction(nameof(GetById), new { id = result.Id },
                    ApiResponse<ProjectResponseDto>.Created(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        public class CreateCommentRequest {
            public Guid WorkTaskId { get; set; }
            public string Content { get; set; } = string.Empty;
            public Guid? ParentCommentId { get; set; }
        }

        [HttpGet("{id:guid}/comments")]
        public async Task<IActionResult> GetProjectComments(Guid id, [FromServices] ApplicationDbContext context)
        {
            var projectExists = await context.Projects.AnyAsync(p => p.Id == id && !p.IsDeleted);
            if (!projectExists)
                return NotFound(ApiResponse<object>.Error("Project not found.", 404));

            var comments = await context.Comments
                .AsNoTracking()
                .Include(c => c.User)
                .Include(c => c.CommentAttachments)
                .Where(c => c.EntityType == "Project" && c.EntityId == id && !c.IsDeleted)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new
                {
                    c.Id,
                    c.EntityId,
                    c.EntityType,
                    c.Content,
                    c.ParentCommentId,
                    c.CreatedAt,
                    c.UpdatedAt,
                    UserId = c.UserId,
                    FullName = c.User.FullName ?? c.User.Email,
                    AvatarUrl = c.User.AvatarUrl,
                    Attachments = c.CommentAttachments.Select(a => new
                    {
                        a.Id,
                        a.FileName,
                        FileUrl = $"/api/private-attachments/comments/{a.Id}",
                        a.ContentType,
                        a.FileSize,
                        a.CreatedAt
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new { statusCode = 200, message = "Success", data = comments });
        }

        [HttpPost("{id:guid}/Comments")]
        public async Task<IActionResult> CreateComment(Guid id, [FromBody] CreateCommentRequest request, [FromServices] TaskManagement.Infrastructure.Data.ApplicationDbContext context)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
                return Unauthorized(ApiResponse<object>.Error("Unauthorized.", 401));

            // Vá lỗi IDOR: Đảm bảo WorkTaskId này thuộc về đúng projectId trên tham số URL
            var taskNode = await context.WorkTasks.FirstOrDefaultAsync(wt => wt.Id == request.WorkTaskId && !wt.IsDeleted);
            if (taskNode == null || taskNode.ProjectId != id)
            {
                return StatusCode(403, ApiResponse<object>.Error("Forbidden: Việc cần làm không tồn tại hoặc không thuộc dự án này."));
            }

            var comment = new TaskManagement.Domain.Entities.Comment
            {
                Id = Guid.NewGuid(),
                EntityId = request.WorkTaskId,
                EntityType = "WorkTask",
                Content = request.Content,
                ParentCommentId = request.ParentCommentId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Comments.Add(comment);
            await context.SaveChangesAsync();

            var user = await context.Users.FindAsync(userId);
            return Ok(new { statusCode = 200, message = "Success", data = new {
                comment.Id,
                comment.Content,
                comment.ParentCommentId,
                comment.CreatedAt,
                UserId = userId,
                FullName = user?.FullName ?? user?.Email,
                Avatar = user?.FullName != null ? user.FullName.Substring(0, 1) : "U"
            }});
        }

        [HttpPut("{projectId:guid}")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,Admin")]
        [RequirePermission("projects.dashboard.edit")]
        public async Task<IActionResult> Update(Guid projectId, [FromBody] UpdateProjectDto dto)
        {
            try
            {
                var result = await _projectService.UpdateAsync(projectId, dto);
                return Ok(ApiResponse<ProjectResponseDto>.Success(result, "Cập nhật thành công."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        /// <summary>
        /// 5.1 Archive: Vô hiệu hóa dự án
        /// </summary>
        [HttpPut("{projectId:guid}/archive")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,Admin")]
        [RequirePermission("projects.dashboard.delete")]
        public async Task<IActionResult> Archive(Guid projectId)
        {
            try
            {
                await _projectService.ArchiveAsync(projectId);
                return Ok(ApiResponse<object>.Success(null!, "Dự án đã được vô hiệu hóa."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpPut("{projectId:guid}/restore")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,Admin")]
        public async Task<IActionResult> Restore(Guid projectId)
        {
            try
            {
                await _projectService.RestoreAsync(projectId);
                return Ok(ApiResponse<object>.Success(null!, "Dự án đã được khôi phục."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpGet("deleted")]
        public async Task<IActionResult> GetDeleted()
        {
            try
            {
                var projects = await _projectService.GetDeletedAsync();
                var favoriteMap = await _context.Projects
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(project => projects.Select(p => p.Id).Contains(project.Id))
                    .ToDictionaryAsync(project => project.Id, project => ReadFavoriteFlag(project.NavigationConfig));

                return Ok(ApiResponse<object>.Success(projects.Select(project => new
                {
                    project.Id,
                    project.WorkspaceId,
                    project.Name,
                    project.Key,
                    project.Description,
                    project.StartDate,
                    project.EndDate,
                    project.Status,
                    project.CreatorName,
                    project.DepartmentId,
                    project.DepartmentName,
                    project.ActiveMemberCount,
                    project.NetworkType,
                    project.Cover,
                    project.Icon,
                    project.LeadUserId,
                    project.LeadName,
                    project.CreatedAt,
                    project.UpdatedAt,
                    project.IsMember,
                    project.MyRole,
                    IsFavorite = favoriteMap.TryGetValue(project.Id, out var favorite) && favorite
                }).ToList()));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = ex.Message });
            }
        }

        [HttpPut("{projectId:guid}/restore-deleted")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,Admin")]
        public async Task<IActionResult> RestoreDeleted(Guid projectId)
        {
            try
            {
                await _projectService.RestoreDeletedAsync(projectId);
                return Ok(ApiResponse<object>.Success(null!, "Dự án đã được khôi phục."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpDelete("{projectId:guid}/permanent")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,Admin")]
        public async Task<IActionResult> PermanentDelete(Guid projectId)
        {
            try
            {
                await _projectService.PermanentDeleteAsync(projectId);
                return Ok(ApiResponse<object>.Success(null!, "Dự án đã được xóa vĩnh viễn."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        /// <summary>
        /// 5.1 Soft Delete
        /// </summary>
        [HttpDelete("{projectId:guid}")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,Admin")]
        public async Task<IActionResult> SoftDelete(Guid projectId)
        {
            try
            {
                await _projectService.SoftDeleteAsync(projectId);
                return Ok(ApiResponse<object>.Success(null!, "Dự án đã được xóa."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Error(ex.Message));
            }
        }

        [HttpGet("{id:guid}/members")]
        [ProjectAuthorize("")]
        public async Task<IActionResult> GetMembers(Guid id)
        {
            try
            {
                var members = await _projectService.GetMembersAsync(id);
                return Ok(ApiResponse<List<ProjectMemberResponseDto>>.Success(members));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = ex.Message });
            }
        }

        [HttpGet("{id:guid}/work-items")]
        public async Task<IActionResult> GetProjectWorkItems(Guid id, [FromQuery] string? search)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var userId))
                return Unauthorized(ApiResponse<object>.Error("Unauthorized.", 401));

            var isMember = await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == id && pm.UserId == userId && pm.Status);
            if (!isMember)
                return StatusCode(403, ApiResponse<object>.Error("Forbidden.", 403));

            var query = _context.WorkTasks
                .AsNoTracking()
                .Include(wt => wt.TaskStatus)
                .Where(wt => wt.ProjectId == id && !wt.IsDeleted && !wt.IsArchived);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim().ToLower();
                query = query.Where(wt => wt.Title.ToLower().Contains(keyword)
                    || (wt.Description != null && wt.Description.ToLower().Contains(keyword)));
            }

            var tasks = await query
                .OrderByDescending(wt => wt.UpdatedAt)
                .Take(50)
                .Select(wt => new
                {
                    wt.Id,
                    wt.ProjectId,
                    wt.Title,
                    wt.SequenceId,
                    wt.Priority,
                    wt.UpdatedAt,
                    wt.CreatedAt,
                    StatusName = wt.TaskStatus.Name
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(tasks));
        }

        [HttpGet("/api/worktasks")]
        public async Task<IActionResult> SearchWorkTasks([FromQuery] string? search)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out var userId))
                return Unauthorized(ApiResponse<object>.Error("Unauthorized.", 401));

            var userProjectIds = await _context.ProjectMembers
                .AsNoTracking()
                .Where(pm => pm.UserId == userId && pm.Status)
                .Select(pm => pm.ProjectId)
                .ToListAsync();

            var query = _context.WorkTasks
                .AsNoTracking()
                .Include(wt => wt.Project)
                .Include(wt => wt.TaskStatus)
                .Where(wt => userProjectIds.Contains(wt.ProjectId) && !wt.IsDeleted && !wt.IsArchived);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim().ToLower();
                query = query.Where(wt => wt.Title.ToLower().Contains(keyword)
                    || (wt.Description != null && wt.Description.ToLower().Contains(keyword)));
            }

            var results = await query
                .OrderByDescending(wt => wt.UpdatedAt)
                .Take(20)
                .Select(wt => new
                {
                    wt.Id,
                    wt.ProjectId,
                    ProjectName = wt.Project.Name,
                    wt.Title,
                    wt.SequenceId,
                    wt.Priority,
                    wt.UpdatedAt,
                    StatusName = wt.TaskStatus.Name
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(results));
        }

        [HttpGet("{id}/lessons")]
        public async Task<IActionResult> GetLessons(Guid id)
        {
            var items = await _projectService.GetLessonsAsync(id);
            return Ok(ApiResponse<object>.Success(items));
        }

        [HttpPost("{id}/lessons")]
        public async Task<IActionResult> AddLesson(Guid id, [FromBody] CreateTabItemDto dto)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var item = await _projectService.AddLessonAsync(id, dto, userId);
            return StatusCode(201, ApiResponse<object>.Created(item));
        }

        [HttpGet("{id}/risks")]
        public async Task<IActionResult> GetRisks(Guid id)
        {
            var items = await _projectService.GetRisksAsync(id);
            return Ok(ApiResponse<object>.Success(items));
        }

        [HttpPost("{id}/risks")]
        public async Task<IActionResult> AddRisk(Guid id, [FromBody] CreateTabItemDto dto)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var item = await _projectService.AddRiskAsync(id, dto, userId);
            return StatusCode(201, ApiResponse<object>.Created(item));
        }

        [HttpGet("{id}/decisions")]
        public async Task<IActionResult> GetDecisions(Guid id)
        {
            var items = await _projectService.GetDecisionsAsync(id);
            return Ok(ApiResponse<object>.Success(items));
        }

        [HttpPost("{id}/decisions")]
        public async Task<IActionResult> AddDecision(Guid id, [FromBody] CreateTabItemDto dto)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var item = await _projectService.AddDecisionAsync(id, dto, userId);
            return StatusCode(201, ApiResponse<object>.Created(item));
        }

        [HttpGet("{id}/updates")]
        public async Task<IActionResult> GetUpdates(Guid id)
        {
            var items = await _projectService.GetUpdatesAsync(id);
            return Ok(ApiResponse<object>.Success(items));
        }

        [HttpPost("{id}/updates")]
        public async Task<IActionResult> AddUpdate(Guid id, [FromBody] CreateTabItemDto dto)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var item = await _projectService.AddUpdateAsync(id, dto, userId);
            return StatusCode(201, ApiResponse<object>.Created(item));
        }

        private static string GetProjectIntegrationGroup(Guid projectId) => $"ProjectIntegrations:{projectId:D}";

        private static List<ProjectIntegrationSetting> GetDefaultProjectIntegrations()
        {
            return new List<ProjectIntegrationSetting>
            {
                new()
                {
                    Provider = "github",
                    DisplayName = "GitHub",
                    Notes = "Repository sync, issue links, and PR references."
                },
                new()
                {
                    Provider = "jira",
                    DisplayName = "Jira",
                    Notes = "Two-way issue references and planning alignment."
                },
                new()
                {
                    Provider = "slack",
                    DisplayName = "Slack",
                    Notes = "Project alerts, sprint updates, and completion digests."
                }
            };
        }

        private static ProjectIntegrationSetting? DeserializeProjectIntegration(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                return NormalizeProjectIntegration(JsonSerializer.Deserialize<ProjectIntegrationSetting>(raw));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static ProjectIntegrationSetting NormalizeProjectIntegration(ProjectIntegrationSetting? item)
        {
            var provider = item?.Provider?.Trim().ToLowerInvariant() ?? string.Empty;
            var defaults = GetDefaultProjectIntegrations()
                .FirstOrDefault(defaultItem => defaultItem.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));

            return new ProjectIntegrationSetting
            {
                Provider = defaults?.Provider ?? provider,
                DisplayName = string.IsNullOrWhiteSpace(item?.DisplayName) ? defaults?.DisplayName ?? provider : item!.DisplayName.Trim(),
                Enabled = item?.Enabled ?? false,
                Endpoint = string.IsNullOrWhiteSpace(item?.Endpoint) ? null : item.Endpoint.Trim(),
                ProjectKey = string.IsNullOrWhiteSpace(item?.ProjectKey) ? null : item.ProjectKey.Trim(),
                Secret = string.IsNullOrWhiteSpace(item?.Secret) ? null : item.Secret.Trim(),
                Notes = string.IsNullOrWhiteSpace(item?.Notes) ? defaults?.Notes : item.Notes.Trim(),
                UpdatedAt = item?.UpdatedAt == default ? DateTime.UtcNow : item?.UpdatedAt ?? DateTime.UtcNow
            };
        }

        private string? ResolveStoredIntegrationSecret(
            ProjectIntegrationSetting requested,
            ProjectIntegrationSetting? existing)
        {
            if (!requested.Enabled)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(requested.Secret))
            {
                return _integrationSecretProtector.Protect(requested.Secret);
            }

            if (string.IsNullOrWhiteSpace(existing?.Secret))
            {
                return null;
            }

            try
            {
                _integrationSecretProtector.Unprotect(existing.Secret);
                return existing.Secret;
            }
            catch
            {
                return _integrationSecretProtector.Protect(existing.Secret);
            }
        }

        private static ProjectIntegrationSetting SanitizeProjectIntegration(ProjectIntegrationSetting item) => new()
        {
            Provider = item.Provider,
            DisplayName = item.DisplayName,
            Enabled = item.Enabled,
            Endpoint = item.Endpoint,
            ProjectKey = item.ProjectKey,
            Secret = null,
            Notes = item.Notes,
            UpdatedAt = item.UpdatedAt
        };
    }
}
