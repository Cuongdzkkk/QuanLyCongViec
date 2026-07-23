using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services
{
    public class GoalService : IGoalService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GoalService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        private Guid? CurrentUserId
        {
            get
            {
                var claim = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
                return Guid.TryParse(claim, out var id) && id != Guid.Empty ? id : null;
            }
        }

        private static JsonElement ToJsonElement(object dto)
        {
            return JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(dto));
        }

        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        private static string? GetString(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (TryGetProperty(element, name, out var value) && value.ValueKind != JsonValueKind.Null)
                {
                    return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
                }
            }

            return null;
        }

        private static Guid? GetGuid(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (TryGetProperty(element, name, out var value))
                {
                    if (value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parsed))
                    {
                        return parsed;
                    }

                    if (value.ValueKind == JsonValueKind.Null)
                    {
                        return null;
                    }
                }
            }

            return null;
        }

        private static int? GetInt(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (TryGetProperty(element, name, out var value))
                {
                    if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
                    {
                        return Math.Clamp(parsed, 0, 100);
                    }

                    if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out parsed))
                    {
                        return Math.Clamp(parsed, 0, 100);
                    }
                }
            }

            return null;
        }

        private static DateTime? GetDate(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (TryGetProperty(element, name, out var value))
                {
                    if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var parsed))
                    {
                        return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
                    }
                }
            }

            return null;
        }

        private async Task<Guid> ResolveWorkspaceIdAsync(Guid requestedWorkspaceId, Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Authenticated user context is required.");
            }

            if (requestedWorkspaceId == Guid.Empty)
            {
                throw new ArgumentException("An explicit workspace context is required.");
            }

            var hasActiveMembership = await _context.WorkspaceMembers
                .AsNoTracking()
                .AnyAsync(member => member.WorkspaceId == requestedWorkspaceId &&
                                    member.UserId == userId &&
                                    member.IsActive &&
                                    !member.Workspace.IsDeleted);

            if (!hasActiveMembership)
            {
                throw new UnauthorizedAccessException("Active membership in the selected workspace is required.");
            }

            return requestedWorkspaceId;
        }

        private async Task<object> MapGoalAsync(Goal goal, Guid? userId)
        {
            var isStarred = userId.HasValue && await _context.StarredItems.AnyAsync(s =>
                s.UserId == userId.Value &&
                s.WorkspaceId == goal.WorkspaceId &&
                s.ItemType == "Goal" &&
                s.ItemId == goal.Id);

            var isFollowing = userId.HasValue && await _context.EntityFollowers.AnyAsync(f =>
                f.UserId == userId.Value &&
                f.EntityType == "Goal" &&
                f.EntityId == goal.Id);

            return new
            {
                goal.Id,
                goal.WorkspaceId,
                goal.Title,
                goal.Description,
                goal.Status,
                goal.Progress,
                goal.IsArchived,
                goal.OwnerId,
                Owner = goal.Owner.FullName ?? goal.Owner.Email,
                OwnerName = goal.Owner.FullName ?? goal.Owner.Email,
                OwnerEmail = goal.Owner.Email,
                OwnerAvatarUrl = goal.Owner.AvatarUrl,
                OwnerColor = (string?)null,
                StartDate = goal.DueDate,
                DueDate = goal.DueDate,
                goal.DepartmentId,
                goal.ParentGoalId,
                goal.CreatedAt,
                goal.UpdatedAt,
                IsStarred = isStarred,
                IsFollowing = isFollowing
            };
        }

        private static object MapGoalUpdate(GoalUpdate update)
        {
            return new
            {
                update.Id,
                update.GoalId,
                update.Content,
                update.Status,
                update.OldStatus,
                update.NewStatus,
                PreviousStatus = update.OldStatus,
                update.OldProgress,
                update.NewProgress,
                PreviousProgress = update.OldProgress,
                Progress = update.NewProgress,
                update.UserId,
                UserName = update.User.FullName ?? update.User.Email,
                UserAvatar = update.User.AvatarUrl,
                update.CreatedAt
            };
        }

        private static object MapTabItem(GoalLesson item)
        {
            return new
            {
                item.Id,
                item.GoalId,
                Text = item.Text,
                Title = string.Empty,
                item.CreatorId,
                CreatorName = item.Creator.FullName ?? item.Creator.Email,
                CreatorAvatarUrl = item.Creator.AvatarUrl,
                item.CreatedAt
            };
        }

        private static object MapTabItem(GoalRisk item)
        {
            return new
            {
                item.Id,
                item.GoalId,
                Text = item.Text,
                Title = item.Severity,
                Severity = item.Severity,
                item.CreatorId,
                CreatorName = item.Creator.FullName ?? item.Creator.Email,
                CreatorAvatarUrl = item.Creator.AvatarUrl,
                item.CreatedAt
            };
        }

        private static object MapTabItem(GoalDecision item)
        {
            return new
            {
                item.Id,
                item.GoalId,
                Text = item.Text,
                Title = string.Empty,
                item.CreatorId,
                CreatorName = item.Creator.FullName ?? item.Creator.Email,
                CreatorAvatarUrl = item.Creator.AvatarUrl,
                item.CreatedAt
            };
        }

        public async Task<object> GetAllAsync(Guid workspaceId)
        {
            var userId = CurrentUserId;
            if (userId.HasValue)
            {
                workspaceId = await ResolveWorkspaceIdAsync(workspaceId, userId.Value);
            }

            var goals = await _context.Goals
                .AsNoTracking()
                .Include(g => g.Owner)
                .Where(g => g.WorkspaceId == workspaceId)
                .OrderByDescending(g => g.UpdatedAt)
                .ToListAsync();

            var result = new List<object>();
            foreach (var goal in goals)
            {
                result.Add(await MapGoalAsync(goal, userId));
            }

            return result;
        }

        public async Task<object?> GetByIdAsync(Guid id)
        {
            var userId = CurrentUserId;
            var goal = await _context.Goals
                .AsNoTracking()
                .Include(g => g.Owner)
                .Include(g => g.Updates).ThenInclude(u => u.User)
                .Include(g => g.Lessons).ThenInclude(l => l.Creator)
                .Include(g => g.Risks).ThenInclude(r => r.Creator)
                .Include(g => g.Decisions).ThenInclude(d => d.Creator)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (goal == null) return null;

            var baseGoal = await MapGoalAsync(goal, userId);
            var linkedProjects = await _context.ProjectLinks
                .AsNoTracking()
                .Include(link => link.Project)
                .Where(link => link.LinkedType == "Goal" && link.LinkedId == id && !link.Project.IsDeleted)
                .OrderByDescending(link => link.CreatedAt)
                .Select(link => new
                {
                    link.Project.Id,
                    link.Project.Name,
                    Title = link.Project.Name,
                    Icon = link.Project.NavigationConfig,
                    link.CreatedAt
                })
                .ToListAsync();

            return new
            {
                Goal = baseGoal,
                goal.Id,
                goal.WorkspaceId,
                goal.Title,
                goal.Description,
                goal.Status,
                goal.Progress,
                goal.IsArchived,
                goal.OwnerId,
                Owner = goal.Owner.FullName ?? goal.Owner.Email,
                OwnerName = goal.Owner.FullName ?? goal.Owner.Email,
                OwnerEmail = goal.Owner.Email,
                OwnerAvatarUrl = goal.Owner.AvatarUrl,
                StartDate = goal.DueDate,
                DueDate = goal.DueDate,
                goal.DepartmentId,
                goal.ParentGoalId,
                goal.CreatedAt,
                goal.UpdatedAt,
                IsStarred = userId.HasValue && await _context.StarredItems.AnyAsync(s => s.UserId == userId.Value && s.WorkspaceId == goal.WorkspaceId && s.ItemType == "Goal" && s.ItemId == goal.Id),
                IsFollowing = userId.HasValue && await _context.EntityFollowers.AnyAsync(f => f.UserId == userId.Value && f.EntityType == "Goal" && f.EntityId == goal.Id),
                Updates = goal.Updates.OrderByDescending(u => u.CreatedAt).Select(MapGoalUpdate).ToList(),
                Lessons = goal.Lessons.OrderByDescending(l => l.CreatedAt).Select(MapTabItem).ToList(),
                Risks = goal.Risks.OrderByDescending(r => r.CreatedAt).Select(MapTabItem).ToList(),
                Decisions = goal.Decisions.OrderByDescending(d => d.CreatedAt).Select(MapTabItem).ToList(),
                LinkedProjects = linkedProjects
            };
        }

        public async Task<object> CreateAsync(Guid creatorId, Guid workspaceId, object dto)
        {
            workspaceId = await ResolveWorkspaceIdAsync(workspaceId, creatorId);
            var data = ToJsonElement(dto);
            var ownerId = GetGuid(data, "ownerId", "OwnerId") ?? creatorId;
            var ownerExists = await _context.Users.AnyAsync(u => u.Id == ownerId && !u.IsDeleted);
            if (!ownerExists) ownerId = creatorId;

            var title = GetString(data, "title", "name", "Title", "Name");
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Goal title is required.");
            }

            var status = GetString(data, "status", "Status");
            if (string.IsNullOrWhiteSpace(status) ||
                string.Equals(status, "Dang cho cap nhat", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Đang chờ cập nhật", StringComparison.OrdinalIgnoreCase))
            {
                status = "Đang chờ xử lý";
            }

            var goal = new Goal
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                WorkspaceId = workspaceId,
                Title = title.Trim(),
                Description = GetString(data, "description", "Description"),
                Status = status,
                Progress = GetInt(data, "progress", "Progress") ?? 0,
                DueDate = GetDate(data, "date", "startDate", "dueDate", "Date", "StartDate", "DueDate"),
                DepartmentId = GetGuid(data, "departmentId", "DepartmentId"),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Goals.Add(goal);
            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                EntityId = goal.Id,
                EntityType = "Goal",
                Action = "Create",
                NewValue = goal.Title,
                UserId = creatorId,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var saved = await _context.Goals
                .AsNoTracking()
                .Include(g => g.Owner)
                .FirstAsync(g => g.Id == goal.Id);

            return await MapGoalAsync(saved, creatorId);
        }

        public async Task<object> UpdateAsync(Guid id, object dto)
        {
            var goal = await _context.Goals.FindAsync(id);
            if (goal == null)
            {
                throw new KeyNotFoundException($"Goal with ID {id} not found.");
            }

            var data = ToJsonElement(dto);
            var title = GetString(data, "title", "name", "Title", "Name");
            if (!string.IsNullOrWhiteSpace(title)) goal.Title = title.Trim();

            if (TryGetProperty(data, "description", out var description))
            {
                goal.Description = description.ValueKind == JsonValueKind.Null ? null : description.GetString();
            }

            var oldStatus = goal.Status;
            var oldProgress = goal.Progress;
            var status = GetString(data, "status", "Status");
            if (!string.IsNullOrWhiteSpace(status)) goal.Status = status;

            var progress = GetInt(data, "progress", "Progress");
            if (progress.HasValue) goal.Progress = progress.Value;

            var ownerId = GetGuid(data, "ownerId", "OwnerId");
            if (ownerId.HasValue && await _context.Users.AnyAsync(u => u.Id == ownerId.Value && !u.IsDeleted))
            {
                goal.OwnerId = ownerId.Value;
            }

            var date = GetDate(data, "date", "startDate", "dueDate", "Date", "StartDate", "DueDate");
            if (date.HasValue) goal.DueDate = date.Value;

            var departmentId = GetGuid(data, "departmentId", "DepartmentId");
            if (departmentId.HasValue || TryGetProperty(data, "departmentId", out _))
            {
                goal.DepartmentId = departmentId;
            }

            goal.UpdatedAt = DateTime.UtcNow;
            var currentUserId = CurrentUserId;
            if (currentUserId.HasValue && !string.Equals(oldStatus, goal.Status, StringComparison.Ordinal))
            {
                _context.SiteAuditLogs.Add(new SiteAuditLog
                {
                    EntityId = goal.Id,
                    EntityType = "Goal",
                    Action = "StatusChanged",
                    OldValue = oldStatus,
                    NewValue = goal.Status,
                    UserId = currentUserId.Value,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (currentUserId.HasValue && oldProgress != goal.Progress)
            {
                _context.SiteAuditLogs.Add(new SiteAuditLog
                {
                    EntityId = goal.Id,
                    EntityType = "Goal",
                    Action = "ProgressChanged",
                    OldValue = oldProgress.ToString(),
                    NewValue = goal.Progress.ToString(),
                    UserId = currentUserId.Value,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();

            var saved = await _context.Goals
                .AsNoTracking()
                .Include(g => g.Owner)
                .FirstAsync(g => g.Id == id);
            return await MapGoalAsync(saved, CurrentUserId);
        }

        public async Task ArchiveAsync(Guid id)
        {
            var goal = await _context.Goals.FindAsync(id);
            if (goal != null)
            {
                goal.IsArchived = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            var goal = await _context.Goals.FindAsync(id);
            if (goal != null)
            {
                _context.Goals.Remove(goal);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<object> GetUpdatesAsync(Guid goalId)
        {
            var items = await _context.GoalUpdates
                .AsNoTracking()
                .Include(u => u.User)
                .Where(u => u.GoalId == goalId)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return items.Select(MapGoalUpdate).ToList();
        }

        public async Task<object> GetLessonsAsync(Guid goalId)
        {
            var items = await _context.GoalLessons
                .AsNoTracking()
                .Include(l => l.Creator)
                .Where(l => l.GoalId == goalId)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            return items.Select(MapTabItem).ToList();
        }

        public async Task<object> GetRisksAsync(Guid goalId)
        {
            var items = await _context.GoalRisks
                .AsNoTracking()
                .Include(r => r.Creator)
                .Where(r => r.GoalId == goalId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return items.Select(MapTabItem).ToList();
        }

        public async Task<object> GetDecisionsAsync(Guid goalId)
        {
            var items = await _context.GoalDecisions
                .AsNoTracking()
                .Include(d => d.Creator)
                .Where(d => d.GoalId == goalId)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            return items.Select(MapTabItem).ToList();
        }

        public async Task<object> AddUpdateAsync(Guid goalId, Guid userId, object dto)
        {
            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == goalId);
            if (goal == null) throw new KeyNotFoundException($"Goal with ID {goalId} not found.");

            var data = ToJsonElement(dto);
            var oldStatus = goal.Status;
            var oldProgress = goal.Progress;
            var status = GetString(data, "status", "Status") ?? goal.Status;
            var progress = GetInt(data, "progress", "Progress");
            var newProgress = progress ?? goal.Progress;

            var update = new GoalUpdate
            {
                Id = Guid.NewGuid(),
                GoalId = goalId,
                UserId = userId,
                Content = GetString(data, "content", "text", "Content", "Text") ?? string.Empty,
                Status = status,
                OldStatus = oldStatus,
                NewStatus = status,
                OldProgress = oldProgress,
                NewProgress = newProgress,
                CreatedAt = DateTime.UtcNow
            };

            goal.Status = status;
            goal.Progress = newProgress;
            goal.UpdatedAt = DateTime.UtcNow;

            _context.GoalUpdates.Add(update);
            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                EntityId = goal.Id,
                EntityType = "Goal",
                Action = "AddUpdate",
                NewValue = update.Content,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            if (!string.Equals(oldStatus, status, StringComparison.Ordinal))
            {
                _context.SiteAuditLogs.Add(new SiteAuditLog
                {
                    EntityId = goal.Id,
                    EntityType = "Goal",
                    Action = "StatusChanged",
                    OldValue = oldStatus,
                    NewValue = status,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (oldProgress != newProgress)
            {
                _context.SiteAuditLogs.Add(new SiteAuditLog
                {
                    EntityId = goal.Id,
                    EntityType = "Goal",
                    Action = "ProgressChanged",
                    OldValue = oldProgress.ToString(),
                    NewValue = newProgress.ToString(),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();

            var saved = await _context.GoalUpdates
                .AsNoTracking()
                .Include(u => u.User)
                .FirstAsync(u => u.Id == update.Id);
            return MapGoalUpdate(saved);
        }

        public async Task<object> UpdateUpdateAsync(Guid goalId, Guid updateId, object dto)
        {
            var update = await _context.GoalUpdates.FirstOrDefaultAsync(u => u.Id == updateId && u.GoalId == goalId);
            if (update == null) throw new KeyNotFoundException($"Goal update with ID {updateId} not found.");

            var data = ToJsonElement(dto);
            var content = GetString(data, "content", "text", "Content", "Text");
            if (content != null) update.Content = content;

            var status = GetString(data, "status", "Status");
            if (!string.IsNullOrWhiteSpace(status)) update.Status = status;

            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == goalId);
            var progress = GetInt(data, "progress", "Progress");
            if (goal != null)
            {
                update.OldStatus ??= goal.Status;
                update.OldProgress ??= goal.Progress;
                if (!string.IsNullOrWhiteSpace(status)) goal.Status = status;
                if (progress.HasValue) goal.Progress = progress.Value;
                update.NewStatus = !string.IsNullOrWhiteSpace(status) ? status : update.Status;
                update.NewProgress = progress ?? update.NewProgress ?? goal.Progress;
                goal.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            var saved = await _context.GoalUpdates
                .AsNoTracking()
                .Include(u => u.User)
                .FirstAsync(u => u.Id == updateId);
            return MapGoalUpdate(saved);
        }

        public async Task DeleteUpdateAsync(Guid goalId, Guid updateId)
        {
            var update = await _context.GoalUpdates.FirstOrDefaultAsync(u => u.Id == updateId && u.GoalId == goalId);
            if (update == null) return;

            _context.GoalUpdates.Remove(update);
            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == goalId);
            if (goal != null) goal.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<object> AddLessonAsync(Guid goalId, Guid userId, object dto)
        {
            var data = ToJsonElement(dto);
            var lesson = new GoalLesson
            {
                Id = Guid.NewGuid(),
                GoalId = goalId,
                CreatorId = userId,
                Text = GetString(data, "text", "content", "Text", "Content") ?? "",
                CreatedAt = DateTime.UtcNow
            };
            _context.GoalLessons.Add(lesson);
            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                EntityId = goalId,
                EntityType = "Goal",
                Action = "AddLesson",
                NewValue = lesson.Text,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var saved = await _context.GoalLessons
                .AsNoTracking()
                .Include(l => l.Creator)
                .FirstAsync(l => l.Id == lesson.Id);
            return MapTabItem(saved);
        }

        public async Task<object> AddRiskAsync(Guid goalId, Guid userId, object dto)
        {
            var data = ToJsonElement(dto);
            var risk = new GoalRisk
            {
                Id = Guid.NewGuid(),
                GoalId = goalId,
                CreatorId = userId,
                Text = GetString(data, "text", "content", "Text", "Content") ?? "",
                Severity = GetString(data, "severity", "title", "Severity", "Title") ?? "Medium",
                CreatedAt = DateTime.UtcNow
            };
            _context.GoalRisks.Add(risk);
            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                EntityId = goalId,
                EntityType = "Goal",
                Action = "AddRisk",
                NewValue = risk.Text,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var saved = await _context.GoalRisks
                .AsNoTracking()
                .Include(r => r.Creator)
                .FirstAsync(r => r.Id == risk.Id);
            return MapTabItem(saved);
        }

        public async Task<object> AddDecisionAsync(Guid goalId, Guid userId, object dto)
        {
            var data = ToJsonElement(dto);
            var decision = new GoalDecision
            {
                Id = Guid.NewGuid(),
                GoalId = goalId,
                CreatorId = userId,
                Text = GetString(data, "text", "content", "Text", "Content") ?? "",
                CreatedAt = DateTime.UtcNow
            };
            _context.GoalDecisions.Add(decision);
            _context.SiteAuditLogs.Add(new SiteAuditLog
            {
                EntityId = goalId,
                EntityType = "Goal",
                Action = "AddDecision",
                NewValue = decision.Text,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var saved = await _context.GoalDecisions
                .AsNoTracking()
                .Include(d => d.Creator)
                .FirstAsync(d => d.Id == decision.Id);
            return MapTabItem(saved);
        }
    }
}
