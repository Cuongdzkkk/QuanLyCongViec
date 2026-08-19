using TaskManagement.Application.DTOs.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using TaskManagement.Application.DTOs.Project;
using TaskManagement.Application.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Infrastructure.Services
{
    public class ProjectService : IProjectService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly string[] FullAccessRoles =
        {
            "superadmin",
            "admin",
            "system admin",
            "organization admin",
            "accessadmin",
            "access admin"
        };

        public ProjectService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        private static string BuildProjectUiConfig(string? existingConfig, string? cover, string? icon)
        {
            var config = new Dictionary<string, string?>();

            if (!string.IsNullOrWhiteSpace(existingConfig))
            {
                config["navigationConfig"] = existingConfig;
            }

            if (!string.IsNullOrWhiteSpace(cover))
            {
                config["cover"] = cover;
            }

            if (!string.IsNullOrWhiteSpace(icon))
            {
                config["icon"] = icon;
            }

            return JsonSerializer.Serialize(config);
        }

        private static void ApplyProjectUiConfig(List<ProjectResponseDto> projects)
        {
            foreach (var project in projects)
            {
                ApplyProjectUiConfig(project);
            }
        }

        private static void ApplyProjectUiConfig(List<ProjectDiscoveryDto> projects)
        {
            foreach (var project in projects)
            {
                ApplyProjectUiConfig(project);
            }
        }

        private static void ApplyProjectUiConfig(ProjectResponseDto project)
        {
            var config = ParseProjectUiConfig(project.Cover);
            project.Cover = config.Cover;
            project.Icon = config.Icon;
        }

        private static void ApplyProjectUiConfig(ProjectDiscoveryDto project)
        {
            var config = ParseProjectUiConfig(project.Cover);
            project.Cover = config.Cover;
            project.Icon = config.Icon;
        }

        private static (string? Cover, string? Icon) ParseProjectUiConfig(string? rawConfig)
        {
            if (string.IsNullOrWhiteSpace(rawConfig))
            {
                return (null, null);
            }

            try
            {
                using var document = JsonDocument.Parse(rawConfig);
                var root = document.RootElement;
                var cover = root.TryGetProperty("cover", out var coverElement) ? coverElement.GetString() : null;
                var icon = root.TryGetProperty("icon", out var iconElement) ? iconElement.GetString() : null;
                return (cover, icon);
            }
            catch (JsonException)
            {
                return (null, null);
            }
        }

        /// <summary>
        /// 5.2 Chá»‘ng N+1: DĂ¹ng .Include().Select() gom data trong 1 cĂ¢u SQL
        /// </summary>
        public async Task<List<ProjectResponseDto>> GetAllAsync()
        {
            var userIdString = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                return new List<ProjectResponseDto>();
            }

            var query = _context.Projects
                .AsNoTracking()
                .Include(p => p.Creator)
                .Include(p => p.Department)
                .Where(p => !p.IsDeleted);

            if (ProjectAccessPolicy.RestrictionsEnabled)
            {
                query = query.Where(p => p.ProjectMembers.Any(pm => pm.UserId == userId && pm.Status));
            }

            var projects = await query
                .Select(p => new ProjectResponseDto
                {
                    Id = p.Id,
                    WorkspaceId = p.WorkspaceId,
                    Name = p.Name,
                    Key = p.Identifier,
                    Description = p.Description,
                    Why = p.Why,
                    SuccessCriteria = p.SuccessCriteria,
                    TrackedLinkUrl = p.TrackedLinkUrl,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    Status = p.Status,
                    LatestUpdateStatus = p.Updates.OrderByDescending(update => update.CreatedAt).Select(update => update.NewStatus ?? update.Status).FirstOrDefault(),
                    CreatorName = p.Creator != null ? p.Creator.FullName : string.Empty,
                    CreatorAvatarUrl = p.Creator != null ? p.Creator.AvatarUrl : null,
                    DepartmentId = p.DepartmentId,
                    DepartmentName = p.Department != null ? p.Department.Name : null,
                    ActiveMemberCount = p.ProjectMembers.Count(m => m.Status == true),
                    NetworkType = p.NetworkType,
                    LeadUserId = p.ProjectMembers
                        .Where(pm => pm.Status && (pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" || pm.ProjectRole == "PM" || pm.ProjectRole == "PROJECT_MANAGER"))
                        .OrderBy(pm => pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" ? 0 : 1)
                        .Select(pm => (Guid?)pm.UserId)
                        .FirstOrDefault(),
                    LeadName = p.ProjectMembers
                        .Where(pm => pm.Status && (pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" || pm.ProjectRole == "PM" || pm.ProjectRole == "PROJECT_MANAGER"))
                        .OrderBy(pm => pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" ? 0 : 1)
                        .Select(pm => pm.User != null ? pm.User.FullName : string.Empty)
                        .FirstOrDefault(),
                    LeadAvatarUrl = p.ProjectMembers
                        .Where(pm => pm.Status && (pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" || pm.ProjectRole == "PM" || pm.ProjectRole == "PROJECT_MANAGER"))
                        .OrderBy(pm => pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" ? 0 : 1)
                        .Select(pm => pm.User != null ? pm.User.AvatarUrl : null)
                        .FirstOrDefault(),
                    Cover = p.NavigationConfig,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();

            ApplyProjectUiConfig(projects);
            return projects;
        }

        /// <summary>
        /// Returns ALL active projects with a flag indicating whether the current user is a member.
        /// Used by Dashboard/ManageSpaces to show "Tham gia" for non-member projects.
        /// </summary>
        public async Task<List<ProjectDiscoveryDto>> GetAllForDiscoveryAsync()
        {
            var userIdString = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out var userId))
            {
                return new List<ProjectDiscoveryDto>();
            }

            var normalizedRoles = await _context.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.Name.Trim().ToLower())
                .ToListAsync();

            var canAccessAllProjects = ProjectAccessPolicy.IsUnrestricted ||
                normalizedRoles.Any(role => FullAccessRoles.Contains(role));

            var query = _context.Projects
                .AsNoTracking()
                .Include(p => p.Creator)
                .Include(p => p.Department)
                .Where(p => !p.IsDeleted && !p.IsArchived && p.Status);

            if (!canAccessAllProjects)
            {
                var assignedProjectIds = await _context.ProjectMembers
                    .AsNoTracking()
                    .Where(pm => pm.UserId == userId && pm.Status)
                    .Select(pm => pm.ProjectId)
                    .ToListAsync();

                query = query.Where(p => assignedProjectIds.Contains(p.Id));
            }

            var projects = await query
                .Select(p => new ProjectDiscoveryDto
                {
                    Id = p.Id,
                    WorkspaceId = p.WorkspaceId,
                    Name = p.Name,
                    Key = p.Identifier,
                    Description = p.Description,
                    Why = p.Why,
                    SuccessCriteria = p.SuccessCriteria,
                    TrackedLinkUrl = p.TrackedLinkUrl,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    Status = p.Status,
                    LatestUpdateStatus = p.Updates.OrderByDescending(update => update.CreatedAt).Select(update => update.NewStatus ?? update.Status).FirstOrDefault(),
                    CreatorName = p.Creator != null ? p.Creator.FullName : string.Empty,
                    CreatorAvatarUrl = p.Creator != null ? p.Creator.AvatarUrl : null,
                    DepartmentId = p.DepartmentId,
                    DepartmentName = p.Department != null ? p.Department.Name : null,
                    ActiveMemberCount = p.ProjectMembers.Count(m => m.Status == true),
                    NetworkType = p.NetworkType,
                    LeadUserId = p.ProjectMembers
                        .Where(pm => pm.Status && (pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" || pm.ProjectRole == "PM" || pm.ProjectRole == "PROJECT_MANAGER"))
                        .OrderBy(pm => pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" ? 0 : 1)
                        .Select(pm => (Guid?)pm.UserId)
                        .FirstOrDefault(),
                    LeadName = p.ProjectMembers
                        .Where(pm => pm.Status && (pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" || pm.ProjectRole == "PM" || pm.ProjectRole == "PROJECT_MANAGER"))
                        .OrderBy(pm => pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" ? 0 : 1)
                        .Select(pm => pm.User != null ? pm.User.FullName : string.Empty)
                        .FirstOrDefault(),
                    LeadAvatarUrl = p.ProjectMembers
                        .Where(pm => pm.Status && (pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" || pm.ProjectRole == "PM" || pm.ProjectRole == "PROJECT_MANAGER"))
                        .OrderBy(pm => pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" ? 0 : 1)
                        .Select(pm => pm.User != null ? pm.User.AvatarUrl : null)
                        .FirstOrDefault(),
                    Cover = p.NavigationConfig,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    IsMember = canAccessAllProjects || p.ProjectMembers.Any(pm => pm.UserId == userId && pm.Status),
                    MyRole = p.ProjectMembers
                        .Where(pm => pm.UserId == userId && pm.Status)
                        .Select(pm => pm.ProjectRole)
                        .FirstOrDefault()
                })
                .ToListAsync();

            ApplyProjectUiConfig(projects);
            return projects;
        }

        public async Task<List<ProjectDiscoveryDto>> GetArchivedAsync()
        {
            var userIdString = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out var userId))
            {
                return new List<ProjectDiscoveryDto>();
            }

            var normalizedRoles = await _context.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.Name.Trim().ToLower())
                .ToListAsync();

            var canAccessAllProjects = ProjectAccessPolicy.IsUnrestricted ||
                normalizedRoles.Any(role => FullAccessRoles.Contains(role));

            var query = _context.Projects
                .AsNoTracking()
                .Include(p => p.Creator)
                .Include(p => p.Department)
                .Where(p => !p.IsDeleted && p.IsArchived);

            if (!canAccessAllProjects)
            {
                var assignedProjectIds = await _context.ProjectMembers
                    .AsNoTracking()
                    .Where(pm => pm.UserId == userId && pm.Status)
                    .Select(pm => pm.ProjectId)
                    .ToListAsync();

                query = query.Where(p => assignedProjectIds.Contains(p.Id));
            }

            var projects = await query
                .Select(p => new ProjectDiscoveryDto
                {
                    Id = p.Id,
                    WorkspaceId = p.WorkspaceId,
                    Name = p.Name,
                    Key = p.Identifier,
                    Description = p.Description,
                    Why = p.Why,
                    SuccessCriteria = p.SuccessCriteria,
                    TrackedLinkUrl = p.TrackedLinkUrl,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    Status = p.Status,
                    LatestUpdateStatus = p.Updates.OrderByDescending(update => update.CreatedAt).Select(update => update.NewStatus ?? update.Status).FirstOrDefault(),
                    CreatorName = p.Creator != null ? p.Creator.FullName : string.Empty,
                    CreatorAvatarUrl = p.Creator != null ? p.Creator.AvatarUrl : null,
                    DepartmentId = p.DepartmentId,
                    DepartmentName = p.Department != null ? p.Department.Name : null,
                    ActiveMemberCount = p.ProjectMembers.Count(m => m.Status),
                    NetworkType = p.NetworkType,
                    LeadUserId = p.ProjectMembers
                        .Where(pm => pm.Status && (pm.ProjectRole == "PROJECT_LEAD" || pm.ProjectRole == "PROJECT_MANAGER"))
                        .OrderBy(pm => pm.ProjectRole == "PROJECT_LEAD" ? 0 : 1)
                        .Select(pm => (Guid?)pm.UserId)
                        .FirstOrDefault(),
                    LeadName = p.ProjectMembers
                        .Where(pm => pm.Status && (pm.ProjectRole == "PROJECT_LEAD" || pm.ProjectRole == "PROJECT_MANAGER"))
                        .OrderBy(pm => pm.ProjectRole == "PROJECT_LEAD" ? 0 : 1)
                        .Select(pm => pm.User != null ? pm.User.FullName : string.Empty)
                        .FirstOrDefault(),
                    Cover = p.NavigationConfig,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    IsMember = canAccessAllProjects || p.ProjectMembers.Any(pm => pm.UserId == userId && pm.Status),
                    MyRole = p.ProjectMembers
                        .Where(pm => pm.UserId == userId && pm.Status)
                        .Select(pm => pm.ProjectRole)
                        .FirstOrDefault()
                })
                .ToListAsync();

            ApplyProjectUiConfig(projects);
            return projects;
        }


        public async Task<ProjectResponseDto?> GetByIdAsync(Guid id)
        {
            var project = await _context.Projects
                .Include(p => p.Creator)
                .Include(p => p.Department)
                .Where(p => p.Id == id)
                .Select(p => new ProjectResponseDto
                {
                    Id = p.Id,
                    WorkspaceId = p.WorkspaceId,
                    Name = p.Name,
                    Key = p.Identifier,
                    Description = p.Description,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    Status = p.Status,
                    LatestUpdateStatus = p.Updates.OrderByDescending(update => update.CreatedAt).Select(update => update.NewStatus ?? update.Status).FirstOrDefault(),
                    CreatorName = p.Creator != null ? p.Creator.FullName : string.Empty,
                    CreatorAvatarUrl = p.Creator != null ? p.Creator.AvatarUrl : null,
                    DepartmentId = p.DepartmentId,
                    DepartmentName = p.Department != null ? p.Department.Name : null,
                    ActiveMemberCount = p.ProjectMembers.Count(m => m.Status == true),
                    NetworkType = p.NetworkType,
                    LeadUserId = p.ProjectMembers
                        .Where(pm => pm.Status && (pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" || pm.ProjectRole == "PM" || pm.ProjectRole == "PROJECT_MANAGER"))
                        .OrderBy(pm => pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" ? 0 : 1)
                        .Select(pm => (Guid?)pm.UserId)
                        .FirstOrDefault(),
                    LeadName = p.ProjectMembers
                        .Where(pm => pm.Status && (pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" || pm.ProjectRole == "PM" || pm.ProjectRole == "PROJECT_MANAGER"))
                        .OrderBy(pm => pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" ? 0 : 1)
                        .Select(pm => pm.User != null ? pm.User.FullName : string.Empty)
                        .FirstOrDefault(),
                    LeadAvatarUrl = p.ProjectMembers
                        .Where(pm => pm.Status && (pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" || pm.ProjectRole == "PM" || pm.ProjectRole == "PROJECT_MANAGER"))
                        .OrderBy(pm => pm.ProjectRole == "Project Lead" || pm.ProjectRole == "PROJECT_LEAD" ? 0 : 1)
                        .Select(pm => pm.User != null ? pm.User.AvatarUrl : null)
                        .FirstOrDefault(),
                    Cover = p.NavigationConfig,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (project != null)
            {
                ApplyProjectUiConfig(project);
            }

            return project;
        }

        public async Task<ProjectResponseDto> CreateAsync(Guid creatorId, CreateProjectDto dto)
        {
            // Validate DepartmentId náº¿u cĂ³
            if (dto.DepartmentId.HasValue)
            {
                var deptExists = await _context.Departments.AnyAsync(d => d.Id == dto.DepartmentId.Value);
                if (!deptExists)
                    throw new ArgumentException("PhĂ²ng ban khĂ´ng tá»“n táº¡i.");
            }

            Guid workspaceId;
            if (dto.WorkspaceId.HasValue && dto.WorkspaceId.Value != Guid.Empty)
            {
                var requestedMembership = await _context.WorkspaceMembers
                    .FirstOrDefaultAsync(wm =>
                        wm.WorkspaceId == dto.WorkspaceId.Value &&
                        wm.UserId == creatorId &&
                        wm.IsActive);

                if (requestedMembership == null)
                    throw new ArgumentException("Bạn không có quyền tạo dự án trong workspace này.");

                workspaceId = requestedMembership.WorkspaceId;
            }
            else
            {
                var workspaceMembership = await _context.WorkspaceMembers
                    .FirstOrDefaultAsync(wm => wm.UserId == creatorId && wm.IsActive);

                if (workspaceMembership != null)
                {
                    workspaceId = workspaceMembership.WorkspaceId;
                }
                else
                {
                    // Auto-create a default workspace for the user if none exists
                    var defaultWorkspace = new Workspace
                    {
                        Id = Guid.NewGuid(),
                        Slug = "workspace-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                        Name = "Default Workspace",
                        OwnerId = creatorId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Workspaces.Add(defaultWorkspace);
                    _context.WorkspaceMembers.Add(new WorkspaceMember
                    {
                        WorkspaceId = defaultWorkspace.Id,
                        UserId = creatorId,
                        WorkspaceRole = "OWNER",
                        JoinedAt = DateTime.UtcNow,
                        IsActive = true
                    });
                    workspaceId = defaultWorkspace.Id;
                }
            }

            // Generate Identifier: láº¥y 3-4 kĂ½ tá»± Ä‘áº§u viáº¿t hoa tá»« tĂªn project
            string identifier = string.IsNullOrWhiteSpace(dto.Key) ? GenerateIdentifier(dto.Name) : NormalizeIdentifier(dto.Key);
            // Ensure unique within workspace
            int suffix = 1;
            string originalIdentifier = identifier;
            while (await _context.Projects.AnyAsync(p => p.WorkspaceId == workspaceId && p.Identifier == identifier))
            {
                identifier = originalIdentifier + suffix;
                suffix++;
            }

            string? templateType = null;
            string? navConfig = null;
            if (dto.ProjectTemplateId.HasValue)
            {
                var template = await _context.ProjectTemplates.FirstOrDefaultAsync(t => t.Id == dto.ProjectTemplateId.Value);
                if (template != null)
                {
                    templateType = template.TemplateCode;
                    navConfig = template.DefaultNavigationConfig;
                }
            }

            var networkType = string.Equals(dto.NetworkType, "Private", StringComparison.OrdinalIgnoreCase)
                ? "Private"
                : "Public";

            Guid? leadUserId = dto.LeadUserId;
            if (leadUserId.HasValue)
            {
                var leadIsWorkspaceMember = await _context.WorkspaceMembers
                    .AnyAsync(wm => wm.WorkspaceId == workspaceId && wm.UserId == leadUserId.Value && wm.IsActive);

                if (!leadIsWorkspaceMember)
                    throw new ArgumentException("Lead pháº£i lĂ  thĂ nh viĂªn Ä‘ang hoáº¡t Ä‘á»™ng trong workspace.");
            }

            navConfig = BuildProjectUiConfig(navConfig, dto.Cover, dto.Icon);

            var project = new Project
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description,
                Identifier = identifier,
                WorkspaceId = workspaceId,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Status = true,
                CreatorId = creatorId,
                DepartmentId = dto.DepartmentId,
                ProjectTemplateId = dto.ProjectTemplateId,
                TemplateType = templateType,
                NavigationConfig = navConfig,
                NetworkType = networkType,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Projects.Add(project);

            // Seed default Task Statuses for the new project
            _context.TaskStatuses.AddRange(
                new TaskManagement.Domain.Entities.TaskStatus { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "TO DO", Position = 1 },
                new TaskManagement.Domain.Entities.TaskStatus { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "IN PROGRESS", Position = 2 },
                new TaskManagement.Domain.Entities.TaskStatus { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "DONE", Position = 3 }
            );

            // Tá»± Ä‘á»™ng sinh TaskType dá»±a theo Template
            if (templateType == "IT_SERVICE")
            {
                _context.TaskTypes.AddRange(
                    new TaskManagement.Domain.Entities.TaskType { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Ticket lá»—i", ColorCode = "#FF0000" },
                    new TaskManagement.Domain.Entities.TaskType { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "YĂªu cáº§u thiáº¿t bá»‹", ColorCode = "#00FF00" }
                );
            }
            else if (templateType == "SOFTWARE_DEV")
            {
                _context.TaskTypes.AddRange(
                    new TaskManagement.Domain.Entities.TaskType { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Bug", ColorCode = "#FF0000" },
                    new TaskManagement.Domain.Entities.TaskType { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Feature", ColorCode = "#0000FF" }
                );
            }
            else
            {
                _context.TaskTypes.Add(
                    new TaskManagement.Domain.Entities.TaskType { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Task", ColorCode = "#3b82f6" }
                );
            }

            // Add the creator as the PM
            var projectMember = new TaskManagement.Domain.Entities.ProjectMember
            {
                ProjectId = project.Id,
                UserId = creatorId,
                ProjectRole = "PM",
                JoinedAt = DateTime.UtcNow,
                Status = true
            };
            _context.ProjectMembers.Add(projectMember);

            if (leadUserId.HasValue && leadUserId.Value != creatorId)
            {
                _context.ProjectMembers.Add(new TaskManagement.Domain.Entities.ProjectMember
                {
                    ProjectId = project.Id,
                    UserId = leadUserId.Value,
                    ProjectRole = "Project Lead",
                    JoinedAt = DateTime.UtcNow,
                    Status = true
                });
            }
            else if (leadUserId.HasValue)
            {
                projectMember.ProjectRole = "Project Lead";
            }
            
            // Add System Audit Log
            var auditLog = new TaskManagement.Domain.Entities.SystemAuditLog
            {
                Id = Guid.NewGuid(),
                UserId = creatorId,
                Action = "CREATE_PROJECT",
                Resource = $"Project: {project.Name}",
                Status = "Success",
                Details = $"User {creatorId} created project {project.Name}",
                CreatedAt = DateTime.UtcNow
            };
            _context.SystemAuditLogs.Add(auditLog);

            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                EntityId = project.Id,
                EntityType = "Project",
                Action = "Create",
                NewValue = project.Name,
                UserId = creatorId,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return (await GetByIdAsync(project.Id))!;
        }

        public async Task<ProjectResponseDto> UpdateAsync(Guid id, UpdateProjectDto dto)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
                throw new ArgumentException("Dá»± Ă¡n khĂ´ng tá»“n táº¡i.");

            // Validate DepartmentId náº¿u cĂ³
            if (dto.DepartmentId.HasValue)
            {
                var deptExists = await _context.Departments.AnyAsync(d => d.Id == dto.DepartmentId.Value);
                if (!deptExists)
                    throw new ArgumentException("PhĂ²ng ban khĂ´ng tá»“n táº¡i.");
            }

            project.Name = dto.Name;
            var normalizedIdentifier = dto.Identifier.Trim().ToUpperInvariant();
            var identifierExists = await _context.Projects.AnyAsync(item =>
                item.Id != project.Id &&
                item.WorkspaceId == project.WorkspaceId &&
                !item.IsDeleted &&
                item.Identifier.ToUpper() == normalizedIdentifier);
            if (identifierExists)
            {
                throw new ArgumentException("Mã dự án đã tồn tại trong workspace.");
            }
            project.Identifier = normalizedIdentifier;
            project.Description = dto.Description;
            project.Why = dto.Why;
            project.SuccessCriteria = dto.SuccessCriteria;
            project.TrackedLinkUrl = dto.TrackedLinkUrl;
            project.StartDate = dto.StartDate;
            project.EndDate = dto.EndDate;
            project.DepartmentId = dto.DepartmentId;
            project.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return (await GetByIdAsync(project.Id))!;
        }

        /// <summary>
        /// 5.1 Archive: Set Status = false (áº©n khá»i danh sĂ¡ch active, váº«n xem Ä‘Æ°á»£c lá»‹ch sá»­)
        /// </summary>
        public async Task ArchiveAsync(Guid id)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
                throw new ArgumentException("Dá»± Ă¡n khĂ´ng tá»“n táº¡i.");

            project.IsArchived = true;
            project.Status = false;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task RestoreAsync(Guid id)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
                throw new ArgumentException("Dá»± Ă¡n khĂ´ng tá»“n táº¡i.");

            project.IsArchived = false;
            project.Status = true;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 5.1 Soft Delete: Set IsDeleted = true, Global Query Filter sáº½ tá»± áº©n
        /// </summary>
        public async Task SoftDeleteAsync(Guid id)
        {
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
                throw new ArgumentException("Dá»± Ă¡n khĂ´ng tá»“n táº¡i.");

            project.IsDeleted = true;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<List<ProjectMemberResponseDto>> GetMembersAsync(Guid projectId)
        {
            return await _context.ProjectMembers
                .AsNoTracking()
                .Include(pm => pm.User)
                .Where(pm => pm.ProjectId == projectId && pm.Status)
                .Select(pm => new ProjectMemberResponseDto
                {
                    UserId = pm.UserId,
                    FullName = pm.User.FullName ?? pm.User.Email,
                    Email = pm.User.Email,
                    ProjectRole = pm.ProjectRole,
                    JoinedAt = pm.JoinedAt
                })
                .ToListAsync();
        }

        /// <summary>
        /// Generate a short identifier from project name (e.g., "My Project" â†’ "MP")
        /// </summary>
        private static string GenerateIdentifier(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "PRJ";

            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1)
            {
                // Single word: take first 3 chars
                return name.Substring(0, Math.Min(3, name.Length)).ToUpper();
            }

            // Multiple words: take first letter of each word (max 4)
            var initials = string.Concat(words.Take(4).Select(w => char.ToUpper(w[0])));
            return initials;
        }

        private static string NormalizeIdentifier(string key)
        {
            var normalized = new string(key
                .Trim()
                .ToUpperInvariant()
                .Where(char.IsLetterOrDigit)
                .Take(8)
                .ToArray());

            return string.IsNullOrWhiteSpace(normalized) ? "PRJ" : normalized;
        }

        public async Task<List<ProjectDiscoveryDto>> GetDeletedAsync()
        {
            var userIdString = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out var userId))
            {
                return new List<ProjectDiscoveryDto>();
            }

            var normalizedRoles = await _context.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.Name.Trim().ToLower())
                .ToListAsync();

            var canAccessAllProjects = ProjectAccessPolicy.IsUnrestricted ||
                normalizedRoles.Any(role => FullAccessRoles.Contains(role));

            var query = _context.Projects
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(p => p.Creator)
                .Include(p => p.Department)
                .Where(p => p.IsDeleted);

            if (!canAccessAllProjects)
            {
                var assignedProjectIds = await _context.ProjectMembers
                    .AsNoTracking()
                    .Where(pm => pm.UserId == userId && pm.Status)
                    .Select(pm => pm.ProjectId)
                    .ToListAsync();

                query = query.Where(p => assignedProjectIds.Contains(p.Id));
            }

            var projects = await query
                .Select(p => new ProjectDiscoveryDto
                {
                    Id = p.Id,
                    WorkspaceId = p.WorkspaceId,
                    Name = p.Name,
                    Key = p.Identifier,
                    Description = p.Description,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    Status = p.Status,
                    LatestUpdateStatus = p.Updates.OrderByDescending(update => update.CreatedAt).Select(update => update.NewStatus ?? update.Status).FirstOrDefault(),
                    CreatorName = p.Creator != null ? p.Creator.FullName : string.Empty,
                    DepartmentId = p.DepartmentId,
                    DepartmentName = p.Department != null ? p.Department.Name : null,
                    ActiveMemberCount = p.ProjectMembers.Count(m => m.Status),
                    NetworkType = p.NetworkType,
                    LeadUserId = p.ProjectMembers
                        .Where(pm => pm.Status && (pm.ProjectRole == "PROJECT_LEAD" || pm.ProjectRole == "PROJECT_MANAGER"))
                        .OrderBy(pm => pm.ProjectRole == "PROJECT_LEAD" ? 0 : 1)
                        .Select(pm => (Guid?)pm.UserId)
                        .FirstOrDefault(),
                    LeadName = p.ProjectMembers
                        .Where(pm => pm.Status && (pm.ProjectRole == "PROJECT_LEAD" || pm.ProjectRole == "PROJECT_MANAGER"))
                        .OrderBy(pm => pm.ProjectRole == "PROJECT_LEAD" ? 0 : 1)
                        .Select(pm => pm.User != null ? pm.User.FullName : string.Empty)
                        .FirstOrDefault(),
                    Cover = p.NavigationConfig,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    IsMember = canAccessAllProjects || p.ProjectMembers.Any(pm => pm.UserId == userId && pm.Status),
                    MyRole = p.ProjectMembers
                        .Where(pm => pm.UserId == userId && pm.Status)
                        .Select(pm => pm.ProjectRole)
                        .FirstOrDefault()
                })
                .ToListAsync();

            ApplyProjectUiConfig(projects);
            return projects;
        }

        public async Task RestoreDeletedAsync(Guid id)
        {
            var project = await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
                throw new ArgumentException("Dá»± Ă¡n khĂ´ng tá»“n táº¡i.");

            project.IsDeleted = false;
            project.Status = true;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task PermanentDeleteAsync(Guid id)
        {
            var project = await _context.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
                throw new ArgumentException("Dá»± Ă¡n khĂ´ng tá»“n táº¡i.");

            var tasks = await _context.WorkTasks.IgnoreQueryFilters().Where(wt => wt.ProjectId == id).ToListAsync();
            var taskIds = tasks.Select(t => t.Id).ToList();

            if (taskIds.Any())
            {
                var assignments = await _context.TaskAssignments.Where(ta => taskIds.Contains(ta.WorkTaskId)).ToListAsync();
                _context.TaskAssignments.RemoveRange(assignments);

                var dependencies = await _context.TaskDependencies.Where(td => taskIds.Contains(td.PredecessorTaskId) || taskIds.Contains(td.PredecessorTaskId)).ToListAsync();
                _context.TaskDependencies.RemoveRange(dependencies);

                var comments = await _context.Comments.Where(c => c.EntityType == "WorkTask" && taskIds.Contains(c.EntityId)).ToListAsync();
                _context.Comments.RemoveRange(comments);

                var attachments = await _context.Attachments.Where(a => taskIds.Contains(a.WorkTaskId)).ToListAsync();
                _context.Attachments.RemoveRange(attachments);

                var auditLogs = await _context.AuditLogs.Where(al => taskIds.Contains(al.WorkTaskId)).ToListAsync();
                _context.AuditLogs.RemoveRange(auditLogs);

                var issueLabels = await _context.IssueLabels.Where(il => taskIds.Contains(il.WorkTaskId)).ToListAsync();
                _context.IssueLabels.RemoveRange(issueLabels);

                var issueModules = await _context.IssueModules.Where(im => taskIds.Contains(im.WorkTaskId)).ToListAsync();
                _context.IssueModules.RemoveRange(issueModules);

                var subscribers = await _context.TaskSubscribers.Where(ts => taskIds.Contains(ts.WorkTaskId)).ToListAsync();
                _context.TaskSubscribers.RemoveRange(subscribers);

                var timeLogs = await _context.TimeLogs.Where(tl => taskIds.Contains(tl.WorkTaskId)).ToListAsync();
                _context.TimeLogs.RemoveRange(timeLogs);

                var embeddings = await _context.TaskVectorEmbeddings.Where(tve => taskIds.Contains(tve.WorkTaskId)).ToListAsync();
                _context.TaskVectorEmbeddings.RemoveRange(embeddings);

                var drafts = await _context.TaskDrafts.Where(td => td.ProjectId == id).ToListAsync();
                _context.TaskDrafts.RemoveRange(drafts);

                _context.WorkTasks.RemoveRange(tasks);
            }

            var members = await _context.ProjectMembers.Where(pm => pm.ProjectId == id).ToListAsync();
            _context.ProjectMembers.RemoveRange(members);

            var sprints = await _context.Sprints.Where(s => s.ProjectId == id).ToListAsync();
            _context.Sprints.RemoveRange(sprints);

            var taskStatuses = await _context.TaskStatuses.Where(ts => ts.ProjectId == id).ToListAsync();
            _context.TaskStatuses.RemoveRange(taskStatuses);

            var taskTypes = await _context.TaskTypes.Where(tt => tt.ProjectId == id).ToListAsync();
            _context.TaskTypes.RemoveRange(taskTypes);

            var modules = await _context.Modules.Where(m => m.ProjectId == id).ToListAsync();
            _context.Modules.RemoveRange(modules);

            var views = await _context.ProjectViews.Where(pv => pv.ProjectId == id).ToListAsync();
            _context.ProjectViews.RemoveRange(views);

            var pages = await _context.Pages.Where(pg => pg.ProjectId == id).ToListAsync();
            _context.Pages.RemoveRange(pages);

            var intakes = await _context.Intakes.Where(i => i.ProjectId == id).ToListAsync();
            _context.Intakes.RemoveRange(intakes);

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();
        }
    
        public async Task<IEnumerable<TabItemDto>> GetLessonsAsync(Guid id)
        {
            var items = await _context.ProjectLessons
                .Include(x => x.Creator)
                .Where(x => x.ProjectId == id)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return items.Select(x => new TabItemDto
            {
                Id = x.Id,
                Text = x.Text,
                CreatorId = x.CreatorId,
                CreatorName = x.Creator?.FullName ?? x.Creator?.Email ?? "Unknown",
                CreatedAt = x.CreatedAt
            });
        }

        public async Task<TabItemDto> AddLessonAsync(Guid id, CreateTabItemDto dto, Guid userId)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null) throw new Exception("Project not found");

            var item = new TaskManagement.Domain.Entities.ProjectLesson
            {
                ProjectId = id,
                Text = dto.Text,
                CreatorId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProjectLessons.Add(item);
            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                EntityId = id,
                EntityType = "Project",
                Action = "AddLesson",
                NewValue = item.Text,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var creator = await _context.Users.FindAsync(userId);

            return new TabItemDto
            {
                Id = item.Id,
                Text = item.Text,
                CreatorId = item.CreatorId,
                CreatorName = creator?.FullName ?? creator?.Email ?? "Unknown",
                CreatedAt = item.CreatedAt
            };
        }

        public async Task<IEnumerable<TabItemDto>> GetRisksAsync(Guid id)
        {
            var items = await _context.ProjectRisks
                .Include(x => x.Creator)
                .Where(x => x.ProjectId == id)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return items.Select(x => new TabItemDto
            {
                Id = x.Id,
                Text = x.Text,
                Title = x.Severity,
                CreatorId = x.CreatorId,
                CreatorName = x.Creator?.FullName ?? x.Creator?.Email ?? "Unknown",
                CreatedAt = x.CreatedAt
            });
        }

        public async Task<TabItemDto> AddRiskAsync(Guid id, CreateTabItemDto dto, Guid userId)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null) throw new Exception("Project not found");

            var item = new TaskManagement.Domain.Entities.ProjectRisk
            {
                ProjectId = id,
                Text = dto.Text,
                Severity = dto.Severity ?? "Medium",
                CreatorId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProjectRisks.Add(item);
            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                EntityId = id,
                EntityType = "Project",
                Action = "AddRisk",
                NewValue = item.Text,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var creator = await _context.Users.FindAsync(userId);

            return new TabItemDto
            {
                Id = item.Id,
                Text = item.Text,
                Title = item.Severity,
                CreatorId = item.CreatorId,
                CreatorName = creator?.FullName ?? creator?.Email ?? "Unknown",
                CreatedAt = item.CreatedAt
            };
        }

        public async Task<IEnumerable<TabItemDto>> GetDecisionsAsync(Guid id)
        {
            var items = await _context.ProjectDecisions
                .Include(x => x.Creator)
                .Where(x => x.ProjectId == id)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return items.Select(x => new TabItemDto
            {
                Id = x.Id,
                Text = x.Text,
                CreatorId = x.CreatorId,
                CreatorName = x.Creator?.FullName ?? x.Creator?.Email ?? "Unknown",
                CreatedAt = x.CreatedAt
            });
        }

        public async Task<TabItemDto> AddDecisionAsync(Guid id, CreateTabItemDto dto, Guid userId)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null) throw new Exception("Project not found");

            var item = new TaskManagement.Domain.Entities.ProjectDecision
            {
                ProjectId = id,
                Text = dto.Text,
                CreatorId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProjectDecisions.Add(item);
            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                EntityId = id,
                EntityType = "Project",
                Action = "AddDecision",
                NewValue = item.Text,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var creator = await _context.Users.FindAsync(userId);

            return new TabItemDto
            {
                Id = item.Id,
                Text = item.Text,
                CreatorId = item.CreatorId,
                CreatorName = creator?.FullName ?? creator?.Email ?? "Unknown",
                CreatedAt = item.CreatedAt
            };
        }

        public async Task<IEnumerable<TabItemDto>> GetUpdatesAsync(Guid id)
        {
            var items = await _context.ProjectUpdates
                .Include(x => x.Creator)
                .Where(x => x.ProjectId == id)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return items.Select(x => new TabItemDto
            {
                Id = x.Id,
                Text = x.Content,
                Title = x.Status,
                CreatorId = x.CreatorId,
                CreatorName = x.Creator?.FullName ?? x.Creator?.Email ?? "Unknown",
                CreatorEmail = x.Creator?.Email,
                CreatorAvatarUrl = x.Creator?.AvatarUrl,
                OldStatus = x.OldStatus,
                NewStatus = x.NewStatus,
                PreviousStatus = x.OldStatus,
                CreatedAt = x.CreatedAt
            });
        }

        public async Task<TabItemDto> AddUpdateAsync(Guid id, CreateTabItemDto dto, Guid userId)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project == null) throw new Exception("Project not found");

            var latestStatus = await _context.ProjectUpdates
                .Where(x => x.ProjectId == id)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.NewStatus ?? x.Status)
                .FirstOrDefaultAsync();
            var oldStatus = latestStatus ?? "Pending";
            var requestedStatus = string.IsNullOrWhiteSpace(dto.Status) ? dto.Title : dto.Status;
            var newStatus = string.IsNullOrWhiteSpace(requestedStatus) ? oldStatus : requestedStatus;

            var item = new TaskManagement.Domain.Entities.ProjectUpdate
            {
                ProjectId = id,
                Content = dto.Text,
                Status = newStatus,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                CreatorId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProjectUpdates.Add(item);
            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                EntityId = project.Id,
                EntityType = "Project",
                Action = "AddUpdate",
                NewValue = item.Content,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            if (!string.Equals(oldStatus, newStatus, StringComparison.Ordinal))
            {
                _context.SiteAuditLogs.Add(new SiteAuditLog
                {
                    EntityId = project.Id,
                    EntityType = "Project",
                    Action = "StatusChanged",
                    OldValue = oldStatus,
                    NewValue = newStatus,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var creator = await _context.Users.FindAsync(userId);

            return new TabItemDto
            {
                Id = item.Id,
                Text = item.Content,
                Title = item.Status,
                CreatorId = item.CreatorId,
                CreatorName = creator?.FullName ?? creator?.Email ?? "Unknown",
                CreatorEmail = creator?.Email,
                CreatorAvatarUrl = creator?.AvatarUrl,
                OldStatus = item.OldStatus,
                NewStatus = item.NewStatus,
                PreviousStatus = item.OldStatus,
                CreatedAt = item.CreatedAt
            };
        }
}
}

