using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Infrastructure.Data
{
    public static class DataSeeder
    {
        public static async Task SeedMockDataAsync(ApplicationDbContext context)
        {
            var now = DateTime.UtcNow;
            var invalidStarredItems = await context.StarredItems
                .Where(item => !StarredItemTypes.Allowed.Contains(item.ItemType))
                .ToListAsync();
            if (invalidStarredItems.Count > 0)
            {
                context.StarredItems.RemoveRange(invalidStarredItems);
                await context.SaveChangesAsync();
            }

            var preferredOwnerId = Guid.Parse("11111111-0000-0000-0000-000000000001");
            const string demoWorkspaceSlug = "cybwf";
            const string demoProjectIdentifier = "CYBWF";
            const string demoProjectName = "Demo Plane Project";
            var standardRoles = new[]
            {
                new { Name = "Admin", Description = "System Administrator", IsProtected = true },
                new { Name = "PM", Description = "Project Manager", IsProtected = true },
                new { Name = "Project Lead", Description = "Project lead access", IsProtected = false },
                new { Name = "PO", Description = "Product Owner", IsProtected = false },
                new { Name = "Developer", Description = "Developer access", IsProtected = false },
                new { Name = "QA", Description = "Quality Assurance", IsProtected = false },
                new { Name = "Accountant", Description = "Accounting access", IsProtected = false }
            };

            foreach (var roleSeed in standardRoles)
            {
                var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == roleSeed.Name);
                if (role == null)
                {
                    context.Roles.Add(new Role
                    {
                        Id = Guid.NewGuid(),
                        Name = roleSeed.Name,
                        Description = roleSeed.Description,
                        IsProtected = roleSeed.IsProtected
                    });
                }
                else if (role.IsProtected != roleSeed.IsProtected)
                {
                    role.IsProtected = roleSeed.IsProtected;
                }
            }

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync();
            }

            await SeedPermissionsAsync(context);

            var owner = await context.Users.FirstOrDefaultAsync(u => u.Id == preferredOwnerId)
                ?? await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@example.com");

            if (owner == null)
            {
                owner = new User
                {
                    Id = preferredOwnerId,
                    FullName = "Admin (Seeded)",
                    Email = "admin@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                context.Users.Add(owner);
                await context.SaveChangesAsync();
            }

            var testUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
            if (testUser == null)
            {
                testUser = new User
                {
                    Id = Guid.NewGuid(),
                    FullName = "Test User",
                    Email = "test@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"),
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                context.Users.Add(testUser);
                await context.SaveChangesAsync();
            }

            var devAdmin = await context.Users.FirstOrDefaultAsync(u => u.Email == "dev@sprinta.local");
            if (devAdmin == null)
            {
                devAdmin = new User
                {
                    Id = Guid.NewGuid(),
                    FullName = "Dev Admin",
                    Email = "dev@sprinta.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("dev123"),
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                context.Users.Add(devAdmin);
                await context.SaveChangesAsync();

                var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
                if (adminRole != null)
                {
                    context.UserRoles.Add(new UserRole
                    {
                        UserId = devAdmin.Id,
                        RoleId = adminRole.Id
                    });
                    await context.SaveChangesAsync();
                }
            }

            var workspace = await context.Workspaces
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.Slug == demoWorkspaceSlug);
            if (workspace == null)
            {
                workspace = new Workspace
                {
                    Id = Guid.NewGuid(),
                    Name = "Cybwf Workspace",
                    Slug = demoWorkspaceSlug,
                    OwnerId = owner.Id,
                    Timezone = "Asia/Ho_Chi_Minh",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                context.Workspaces.Add(workspace);
                await context.SaveChangesAsync();
            }

            await EnsureWorkspaceMemberAsync(context, workspace.Id, owner.Id, "OWNER", now);
            await EnsureWorkspaceMemberAsync(context, workspace.Id, testUser.Id, "MEMBER", now);
            await EnsureWorkspaceMemberAsync(context, workspace.Id, devAdmin.Id, "OWNER", now);

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync();
            }

            var project = await context.Projects
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p =>
                    p.WorkspaceId == workspace.Id
                    && (p.Identifier == demoProjectIdentifier || p.Name == demoProjectName));
            if (project == null)
            {
                project = new Project
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspace.Id,
                    CreatorId = owner.Id,
                    Identifier = demoProjectIdentifier,
                    IssueSequence = 10,
                    NetworkType = "Public",
                    Name = demoProjectName,
                    Description = "A sample project to test the Plane-like UI",
                    Status = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                context.Projects.Add(project);
                await context.SaveChangesAsync();
            }
            else if (project.IsDeleted)
            {
                return;
            }
            else if (project.CreatorId == Guid.Empty)
            {
                project.CreatorId = owner.Id;
                project.UpdatedAt = now;
                await context.SaveChangesAsync();
            }

            await EnsureProjectMemberAsync(context, project.Id, owner.Id, "PM", now);
            await EnsureProjectMemberAsync(context, project.Id, testUser.Id, "PM", now);
            await EnsureProjectMemberAsync(context, project.Id, devAdmin.Id, "Admin", now);

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync();
            }

            var statusBacklog = await EnsureTaskStatusAsync(context, project.Id, "BACKLOG", 0, "#64748b");
            var statusTodo = await EnsureTaskStatusAsync(context, project.Id, "TO DO", 1, "#3b82f6");
            var statusProgress = await EnsureTaskStatusAsync(context, project.Id, "IN PROGRESS", 2, "#f59e0b");
            var statusReview = await EnsureTaskStatusAsync(context, project.Id, "IN REVIEW", 3, "#8b5cf6");
            var statusDone = await EnsureTaskStatusAsync(context, project.Id, "DONE", 4, "#10b981");

            var typeTask = await EnsureTaskTypeAsync(context, project.Id, "Task", "#3b82f6");
            var typeBug = await EnsureTaskTypeAsync(context, project.Id, "Bug", "#ef4444");

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync();
            }

            await EnsureTaskAsync(
                context,
                project.Id,
                workspace.Id,
                typeTask.Id,
                statusDone.Id,
                owner.Id,
                "CYBWF-1",
                "Nghien cuu kien truc Plane",
                "Phan tich schema database",
                3,
                10000,
                now);

            await EnsureTaskAsync(
                context,
                project.Id,
                workspace.Id,
                typeTask.Id,
                statusProgress.Id,
                owner.Id,
                "CYBWF-2",
                "Thiet ke giao dien Dark Mode",
                "#0D0D0D background",
                2,
                20000,
                now);

            await EnsureTaskAsync(
                context,
                project.Id,
                workspace.Id,
                typeBug.Id,
                statusReview.Id,
                owner.Id,
                "CYBWF-3",
                "Viet API Kanban Reorder",
                "Testing Lexorank",
                1,
                30000,
                now);

            await EnsureTaskAsync(
                context,
                project.Id,
                workspace.Id,
                typeTask.Id,
                statusTodo.Id,
                owner.Id,
                "CYBWF-4",
                "Len ke hoach Sprint 1",
                "Chuan bi backlog",
                4,
                40000,
                now);

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync();
            }

            await SeedTerraDemoDataAsync(context, workspace, project, owner, now);
            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync();
            }
        }

        private static async Task SeedTerraDemoDataAsync(
            ApplicationDbContext context,
            Workspace workspace,
            Project project,
            User owner,
            DateTime now)
        {
            // Covers are local metadata only; this seed intentionally does not hotlink external assets.
            if (string.IsNullOrWhiteSpace(project.CoverUrl))
            {
                project.CoverUrl = "/uploads/demo/project-covers/cybwf-gradient.svg";
                project.CoverAltText = "Demo SprintA project cover";
                project.UpdatedAt = now;
            }

            var plans = new[]
            {
                new { Code = "free", Name = "Free", Credits = 100, Users = (int?)3 },
                new { Code = "team", Name = "Team", Credits = 0, Users = (int?)null },
                new { Code = "business", Name = "Business", Credits = 0, Users = (int?)null }
            };
            foreach (var item in plans)
            {
                var plan = await context.AiPricingPlans.SingleOrDefaultAsync(x => x.Code == item.Code);
                if (plan == null)
                {
                    context.AiPricingPlans.Add(new AiPricingPlan
                    {
                        Id = Guid.NewGuid(), Code = item.Code, Name = item.Name,
                        IncludedAiCredits = item.Credits, IncludedUsers = item.Users,
                        MonthlyPriceVnd = null, PricingStatus = "PendingConfirmation",
                        CreatedAt = now, UpdatedAt = now
                    });
                }
            }

            const string disclaimer = "Mức sử dụng là ước tính và có thể thay đổi theo độ dài nội dung.";
            foreach (var action in new[] { "summarize_project", "create_project", "create_task", "create_cycle", "create_goal", "list_overdue_tasks" })
            {
                var rule = await context.AiCreditRules.SingleOrDefaultAsync(x => x.ActionType == action);
                if (rule == null)
                {
                    context.AiCreditRules.Add(new AiCreditRule
                    {
                        Id = Guid.NewGuid(), ActionType = action, EstimatedCredits = 1,
                        Disclaimer = disclaimer, CreatedAt = now, UpdatedAt = now
                    });
                }
            }

            var demoTask = await context.WorkTasks.SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.SequenceId == "CYBWF-4");
            if (demoTask != null)
            {
                const string contingencyRisk = "Key delivery owner becomes unavailable before the sprint planning deadline.";
                if (!await context.TaskContingencyPlans.AnyAsync(x => x.WorkTaskId == demoTask.Id && x.Risk == contingencyRisk))
                {
                    context.TaskContingencyPlans.Add(new TaskContingencyPlan
                    {
                        Id = Guid.NewGuid(), WorkTaskId = demoTask.Id, Risk = contingencyRisk,
                        Cause = "Unplanned leave or competing production incident.",
                        ResponsePlan = "Transfer the prepared backlog and planning notes to the support owner.",
                        SupportPersonId = owner.Id, ReplacementDeadline = now.AddDays(2),
                        ImpactLevel = "High", TriggerCondition = "Owner is unavailable for one business day.",
                        Status = "Open", CreatedAt = now, UpdatedAt = now,
                        CreatedById = owner.Id, UpdatedById = owner.Id
                    });
                }
            }

            const string usageKey = "demo-ai-usage-cybwf-2026-07";
            if (!await context.AiUsageLedgerEntries.AnyAsync(x => x.IdempotencyKey == usageKey))
            {
                context.AiUsageLedgerEntries.Add(new AiUsageLedger
                {
                    Id = Guid.NewGuid(), WorkspaceId = workspace.Id, UserId = owner.Id, ProjectId = project.Id,
                    ActionType = "summarize_project", CreditsConsumed = 1, ProviderTokens = 0,
                    IdempotencyKey = usageKey, OccurredAt = now
                });
            }

            foreach (var category in new[] { "Task", "Project", "Workspace", "AI", "Deadline", "Mention", "Comment", "Invitation", "Permission", "Report", "Document", "DailyFocus", "Calendar", "Cycle" })
            {
                if (!await context.NotificationPreferences.AnyAsync(x => x.UserId == owner.Id && x.Category == category))
                {
                    context.NotificationPreferences.Add(new NotificationPreference
                    {
                        Id = Guid.NewGuid(), UserId = owner.Id, Category = category,
                        InAppEnabled = true, EmailEnabled = false, Priority = "Normal",
                        CreatedAt = now, UpdatedAt = now
                    });
                }
            }
        }

        private static async Task EnsureWorkspaceMemberAsync(
            ApplicationDbContext context, Guid workspaceId, Guid userId, string role, DateTime now)
        {
            var tracked = context.WorkspaceMembers.Local
                .FirstOrDefault(x => x.WorkspaceId == workspaceId && x.UserId == userId);
            if (tracked != null || await context.WorkspaceMembers.AnyAsync(x => x.WorkspaceId == workspaceId && x.UserId == userId))
            {
                return;
            }

            context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = workspaceId, UserId = userId, WorkspaceRole = role,
                JoinedAt = now, IsActive = true
            });
        }

        private static async Task EnsureProjectMemberAsync(
            ApplicationDbContext context, Guid projectId, Guid userId, string role, DateTime now)
        {
            var tracked = context.ProjectMembers.Local
                .FirstOrDefault(x => x.ProjectId == projectId && x.UserId == userId);
            if (tracked != null || await context.ProjectMembers.AnyAsync(x => x.ProjectId == projectId && x.UserId == userId))
            {
                return;
            }

            context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId, UserId = userId, ProjectRole = role,
                JoinedAt = now, Status = true
            });
        }

        private static async Task<TaskManagement.Domain.Entities.TaskStatus> EnsureTaskStatusAsync(
            ApplicationDbContext context,
            Guid projectId,
            string name,
            int position,
            string colorCode)
        {
            var status = await context.TaskStatuses.FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Name == name);
            if (status != null)
            {
                if (status.Position != position || string.IsNullOrWhiteSpace(status.ColorCode))
                {
                    status.Position = position;
                    status.ColorCode = colorCode;
                }

                return status;
            }

            status = new TaskManagement.Domain.Entities.TaskStatus
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = name,
                Position = position,
                ColorCode = colorCode
            };

            context.TaskStatuses.Add(status);
            return status;
        }

        private static async Task<TaskType> EnsureTaskTypeAsync(
            ApplicationDbContext context,
            Guid projectId,
            string name,
            string colorCode)
        {
            var taskType = await context.TaskTypes.FirstOrDefaultAsync(t => t.ProjectId == projectId && t.Name == name);
            if (taskType != null)
            {
                if (string.IsNullOrWhiteSpace(taskType.ColorCode))
                {
                    taskType.ColorCode = colorCode;
                }

                return taskType;
            }

            taskType = new TaskType
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = name,
                ColorCode = colorCode
            };

            context.TaskTypes.Add(taskType);
            return taskType;
        }

        private static async Task EnsureTaskAsync(
            ApplicationDbContext context,
            Guid projectId,
            Guid workspaceId,
            Guid taskTypeId,
            Guid taskStatusId,
            Guid reporterId,
            string sequenceId,
            string title,
            string description,
            int priority,
            double sortOrder,
            DateTime now)
        {
            var existingTask = await context.WorkTasks.FirstOrDefaultAsync(t => t.ProjectId == projectId && t.SequenceId == sequenceId);
            if (existingTask != null)
            {
                return;
            }

            context.WorkTasks.Add(new WorkTask
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                WorkspaceId = workspaceId,
                Title = title,
                Description = description,
                Priority = priority,
                TaskTypeId = taskTypeId,
                TaskStatusId = taskStatusId,
                ReporterId = reporterId,
                SortOrder = sortOrder,
                SequenceId = sequenceId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        private static async Task SeedPermissionsAsync(ApplicationDbContext context)
        {
            var permissions = new[]
            {
                new { Module = "admin.roles", Code = "admin.roles.view", Description = "View admin roles" },
                new { Module = "admin.roles", Code = "admin.roles.can_view", Description = "View admin roles" },
                new { Module = "admin.roles", Code = "admin.roles.create", Description = "Create admin roles" },
                new { Module = "admin.roles", Code = "admin.roles.edit", Description = "Edit admin roles" },
                new { Module = "admin.roles", Code = "admin.roles.delete", Description = "Delete admin roles" },
                new { Module = "admin.roles", Code = "admin.roles.manage_permissions", Description = "Manage permissions admin roles" },

                new { Module = "admin.security", Code = "admin.security.view", Description = "View admin security" },
                new { Module = "admin.security", Code = "admin.security.manage_2fa", Description = "Manage 2fa admin security" },
                new { Module = "admin.security", Code = "admin.security.manage_ip_whitelist", Description = "Manage ip whitelist admin security" },

                new { Module = "admin.settings", Code = "admin.settings.view", Description = "View admin settings" },
                new { Module = "admin.settings", Code = "admin.settings.edit", Description = "Edit admin settings" },

                new { Module = "admin.users", Code = "admin.users.view", Description = "View admin users" },
                new { Module = "admin.users", Code = "admin.users.invite", Description = "Invite admin users" },
                new { Module = "admin.users", Code = "admin.users.edit", Description = "Edit admin users" },
                new { Module = "admin.users", Code = "admin.users.suspend", Description = "Suspend admin users" },
                new { Module = "admin.users", Code = "admin.users.delete", Description = "Delete admin users" },

                new { Module = "ai_assistant", Code = "ai_assistant.view", Description = "View ai_assistant" },
                new { Module = "ai_assistant", Code = "ai_assistant.use", Description = "Use ai_assistant" },

                new { Module = "analytics", Code = "analytics.view", Description = "View analytics" },
                new { Module = "analytics", Code = "analytics.export", Description = "Export analytics" },

                new { Module = "archives", Code = "archives.view", Description = "View archives" },
                new { Module = "archives", Code = "archives.restore", Description = "Restore archives" },
                new { Module = "archives", Code = "archives.delete", Description = "Delete archives" },

                new { Module = "audit_log", Code = "audit_log.view", Description = "View audit_log" },
                new { Module = "audit_log", Code = "audit_log.export", Description = "Export audit_log" },

                new { Module = "chat", Code = "chat.view", Description = "View chat" },
                new { Module = "chat", Code = "chat.send", Description = "Send chat" },
                new { Module = "chat", Code = "chat.edit", Description = "Edit chat" },
                new { Module = "chat", Code = "chat.delete", Description = "Delete chat" },
                new { Module = "chat", Code = "chat.manage_channel", Description = "Manage channel chat" },

                new { Module = "checkin", Code = "checkin.view", Description = "View checkin" },
                new { Module = "checkin", Code = "checkin.submit", Description = "Submit checkin" },
                new { Module = "checkin", Code = "checkin.review", Description = "Review checkin" },

                new { Module = "dashboard", Code = "dashboard.view", Description = "View dashboard" },

                new { Module = "drafts", Code = "drafts.view", Description = "View drafts" },
                new { Module = "drafts", Code = "drafts.create", Description = "Create drafts" },
                new { Module = "drafts", Code = "drafts.edit", Description = "Edit drafts" },
                new { Module = "drafts", Code = "drafts.delete", Description = "Delete drafts" },

                new { Module = "feed", Code = "feed.view", Description = "View feed" },
                new { Module = "feed", Code = "feed.post", Description = "Post feed" },
                new { Module = "feed", Code = "feed.comment", Description = "Comment feed" },
                new { Module = "feed", Code = "feed.like", Description = "Like feed" },

                new { Module = "goals", Code = "goals.view", Description = "View goals" },
                new { Module = "goals", Code = "goals.create", Description = "Create goals" },
                new { Module = "goals", Code = "goals.edit", Description = "Edit goals" },
                new { Module = "goals", Code = "goals.delete", Description = "Delete goals" },
                new { Module = "goals", Code = "goals.update_progress", Description = "Update progress goals" },
                new { Module = "goals", Code = "goals.manage_metrics", Description = "Manage metrics goals" },

                new { Module = "integrations", Code = "integrations.view", Description = "View integrations" },
                new { Module = "integrations", Code = "integrations.manage", Description = "Manage integrations" },

                new { Module = "notifications", Code = "notifications.view", Description = "View notifications" },
                new { Module = "notifications", Code = "notifications.manage", Description = "Manage notifications" },

                new { Module = "people", Code = "people.view", Description = "View people" },
                new { Module = "people", Code = "people.manage", Description = "Manage people" },

                new { Module = "priority", Code = "priority.view", Description = "View priority" },
                new { Module = "priority", Code = "priority.manage", Description = "Manage priority" },

                new { Module = "profile", Code = "profile.view", Description = "View profile" },
                new { Module = "profile", Code = "profile.edit", Description = "Edit profile" },

                new { Module = "projects", Code = "projects.view", Description = "View projects" },
                new { Module = "projects", Code = "projects.create", Description = "Create projects" },
                new { Module = "projects", Code = "projects.edit", Description = "Edit projects" },
                new { Module = "projects", Code = "projects.delete", Description = "Delete projects" },
                new { Module = "projects", Code = "projects.archive", Description = "Archive projects" },
                new { Module = "projects", Code = "projects.restore", Description = "Restore projects" },
                new { Module = "projects", Code = "projects.export", Description = "Export projects" },
                new { Module = "projects", Code = "projects.import", Description = "Import projects" },
                new { Module = "projects", Code = "projects.manage_settings", Description = "Manage settings projects" },

                new { Module = "rewards", Code = "rewards.view", Description = "View rewards" },
                new { Module = "rewards", Code = "rewards.manage", Description = "Manage rewards" },

                new { Module = "space.cycles", Code = "space.cycles.view", Description = "View space cycles" },
                new { Module = "space.cycles", Code = "space.cycles.create", Description = "Create space cycles" },
                new { Module = "space.cycles", Code = "space.cycles.edit", Description = "Edit space cycles" },
                new { Module = "space.cycles", Code = "space.cycles.delete", Description = "Delete space cycles" },

                new { Module = "space.dashboard", Code = "space.dashboard.view", Description = "View space dashboard" },
                new { Module = "space.dashboard", Code = "space.dashboard.edit", Description = "Edit space dashboard" },

                new { Module = "space.intakes", Code = "space.intakes.view", Description = "View space intakes" },
                new { Module = "space.intakes", Code = "space.intakes.manage", Description = "Manage space intakes" },

                new { Module = "space.members", Code = "space.members.view", Description = "View space members" },
                new { Module = "space.members", Code = "space.members.invite", Description = "Invite space members" },
                new { Module = "space.members", Code = "space.members.remove", Description = "Remove space members" },
                new { Module = "space.members", Code = "space.members.manage_roles", Description = "Manage roles space members" },

                new { Module = "space.modules", Code = "space.modules.view", Description = "View space modules" },
                new { Module = "space.modules", Code = "space.modules.manage", Description = "Manage space modules" },

                new { Module = "space.pages", Code = "space.pages.view", Description = "View space pages" },
                new { Module = "space.pages", Code = "space.pages.create", Description = "Create space pages" },
                new { Module = "space.pages", Code = "space.pages.edit", Description = "Edit space pages" },
                new { Module = "space.pages", Code = "space.pages.delete", Description = "Delete space pages" },

                new { Module = "space.reports", Code = "space.reports.view", Description = "View space reports" },
                new { Module = "space.reports", Code = "space.reports.create", Description = "Create space reports" },
                new { Module = "space.reports", Code = "space.reports.export", Description = "Export space reports" },

                new { Module = "space.settings", Code = "space.settings.view", Description = "View space settings" },
                new { Module = "space.settings", Code = "space.settings.edit", Description = "Edit space settings" },

                new { Module = "space.timeline", Code = "space.timeline.view", Description = "View space timeline" },
                new { Module = "space.timeline", Code = "space.timeline.edit", Description = "Edit space timeline" },

                new { Module = "space.views", Code = "space.views.view", Description = "View space views" },
                new { Module = "space.views", Code = "space.views.create", Description = "Create space views" },
                new { Module = "space.views", Code = "space.views.edit", Description = "Edit space views" },
                new { Module = "space.views", Code = "space.views.delete", Description = "Delete space views" },

                new { Module = "space.work_items", Code = "space.work_items.view", Description = "View space work_items" },
                new { Module = "space.work_items", Code = "space.work_items.create", Description = "Create space work_items" },
                new { Module = "space.work_items", Code = "space.work_items.edit", Description = "Edit space work_items" },
                new { Module = "space.work_items", Code = "space.work_items.delete", Description = "Delete space work_items" },
                new { Module = "space.work_items", Code = "space.work_items.assign", Description = "Assign space work_items" },
                new { Module = "space.work_items", Code = "space.work_items.change_status", Description = "Change status space work_items" },
                new { Module = "space.work_items", Code = "space.work_items.comment", Description = "Comment space work_items" },
                new { Module = "space.work_items", Code = "space.work_items.attachment", Description = "Attachment space work_items" },

                new { Module = "spaces", Code = "spaces.view", Description = "View spaces" },
                new { Module = "spaces", Code = "spaces.create", Description = "Create spaces" },
                new { Module = "spaces", Code = "spaces.edit", Description = "Edit spaces" },
                new { Module = "spaces", Code = "spaces.delete", Description = "Delete spaces" },
                new { Module = "spaces", Code = "spaces.archive", Description = "Archive spaces" },
                new { Module = "spaces", Code = "spaces.restore", Description = "Restore spaces" },
                new { Module = "spaces", Code = "spaces.manage_settings", Description = "Manage settings spaces" },

                new { Module = "starred", Code = "starred.view", Description = "View starred" },
                new { Module = "starred", Code = "starred.manage", Description = "Manage starred" },

                new { Module = "stickies", Code = "stickies.view", Description = "View stickies" },
                new { Module = "stickies", Code = "stickies.create", Description = "Create stickies" },
                new { Module = "stickies", Code = "stickies.edit", Description = "Edit stickies" },
                new { Module = "stickies", Code = "stickies.delete", Description = "Delete stickies" },

                new { Module = "system_status", Code = "system_status.view", Description = "View system_status" },

                new { Module = "teams", Code = "teams.view", Description = "View teams" },
                new { Module = "teams", Code = "teams.create", Description = "Create teams" },
                new { Module = "teams", Code = "teams.edit", Description = "Edit teams" },
                new { Module = "teams", Code = "teams.delete", Description = "Delete teams" },
                new { Module = "teams", Code = "teams.manage_members", Description = "Manage members teams" },
                new { Module = "teams.dashboard", Code = "teams.dashboard.can_view", Description = "View teams dashboard" },
                new { Module = "teams.dashboard", Code = "teams.dashboard.create", Description = "Create teams dashboard" },
                new { Module = "teams.dashboard", Code = "teams.dashboard.edit", Description = "Edit teams dashboard" },
                new { Module = "teams.dashboard", Code = "teams.dashboard.delete", Description = "Delete teams dashboard" },

                new { Module = "views", Code = "views.view", Description = "View views" },
                new { Module = "views", Code = "views.create", Description = "Create views" },
                new { Module = "views", Code = "views.edit", Description = "Edit views" },
                new { Module = "views", Code = "views.delete", Description = "Delete views" },

                new { Module = "your_work", Code = "your_work.view", Description = "View your_work" },

            };

            foreach (var perm in permissions)
            {
                var existing = await context.Permissions.FirstOrDefaultAsync(p => p.Code == perm.Code);
                if (existing == null)
                {
                    context.Permissions.Add(new Permission
                    {
                        Id = Guid.NewGuid(),
                        Module = perm.Module,
                        Code = perm.Code,
                        Description = perm.Description
                    });
                }
                else if (existing.Description != perm.Description || existing.Module != perm.Module)
                {
                    existing.Description = perm.Description;
                    existing.Module = perm.Module;
                }
            }
            await context.SaveChangesAsync();

            // Assign all permissions to Admin role
            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            if (adminRole != null)
            {
                var allPermissions = await context.Permissions.ToListAsync();
                var existingRolePerms = await context.RolePermissions
                    .Where(rp => rp.RoleId == adminRole.Id)
                    .Select(rp => rp.PermissionId)
                    .ToListAsync();

                foreach (var perm in allPermissions)
                {
                    if (!existingRolePerms.Contains(perm.Id))
                    {
                        context.RolePermissions.Add(new RolePermission
                        {
                            RoleId = adminRole.Id,
                            PermissionId = perm.Id
                        });
                    }
                }
                await context.SaveChangesAsync();
            }
        }
    }
}
