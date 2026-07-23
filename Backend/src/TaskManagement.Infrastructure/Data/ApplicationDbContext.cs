using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaskManagement.Domain.Entities;
using TaskStatus = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor? _httpContextAccessor;
        private readonly TaskManagement.Application.Interfaces.IAuditLogQueue? _auditLogQueue;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContextAccessor = null,
            TaskManagement.Application.Interfaces.IAuditLogQueue? auditLogQueue = null) : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
            _auditLogQueue = auditLogQueue;
        }

        // Group 1: System & Access
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<SystemAuditLog> SystemAuditLogs { get; set; }
        public DbSet<TenantConfig> TenantConfigs { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        // Group 2: Organization
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<DepartmentMember> DepartmentMembers { get; set; }

        // Group 3: Core Work Management
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectTemplate> ProjectTemplates { get; set; }
        public DbSet<ProjectMember> ProjectMembers { get; set; }
        public DbSet<ProjectDepartmentRole> ProjectDepartmentRoles { get; set; }
        public DbSet<Sprint> Sprints { get; set; }
        public DbSet<TaskType> TaskTypes { get; set; }
        public DbSet<TaskManagement.Domain.Entities.TaskStatus> TaskStatuses { get; set; }
        public DbSet<WorkTask> WorkTasks { get; set; }
        public DbSet<TaskContingencyPlan> TaskContingencyPlans { get; set; }
        public DbSet<ContingencyPlan> ContingencyPlans { get; set; }
        public DbSet<ContingencyPlanTask> ContingencyPlanTasks { get; set; }
        public DbSet<TaskAssignment> TaskAssignments { get; set; }
        public DbSet<TaskDependency> TaskDependencies { get; set; }

        // Group 4: Collaboration & Tracking
        public DbSet<Comment> Comments { get; set; }
        public DbSet<CommentMention> CommentMentions { get; set; }
        public DbSet<CommentAttachment> CommentAttachments { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SiteAuditLog> SiteAuditLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<NotificationPreference> NotificationPreferences { get; set; }
        public DbSet<RecentView> RecentViews { get; set; }

        // Group 5: Gamification & Recognition
        public DbSet<PerformanceReview> PerformanceReviews { get; set; }
        public DbSet<UserWallet> UserWallets { get; set; }
        public DbSet<PointTransaction> PointTransactions { get; set; }
        public DbSet<Kudo> Kudos { get; set; }
        public DbSet<KudoReaction> KudoReactions { get; set; }

        // Group 6: AI Integration
        public DbSet<AIPromptTemplate> AIPromptTemplates { get; set; }
        public DbSet<AITokenUsage> AITokenUsages { get; set; }
        public DbSet<AiPricingPlan> AiPricingPlans { get; set; }
        public DbSet<AiCreditRule> AiCreditRules { get; set; }
        public DbSet<AiUsageLedger> AiUsageLedgerEntries { get; set; }
        public DbSet<AIFeedback> AIFeedbacks { get; set; }
        public DbSet<AITrainingDataset> AITrainingDatasets { get; set; }
        public DbSet<TaskVectorEmbedding> TaskVectorEmbeddings { get; set; }
        public DbSet<AiConversation> AiConversations { get; set; }
        public DbSet<AiActionExecution> AiActionExecutions { get; set; }
        public DbSet<AiAttachment> AiAttachments { get; set; }
        public DbSet<AiAttachmentChunk> AiAttachmentChunks { get; set; }
        public DbSet<IntegrationAccount> IntegrationAccounts { get; set; }
        public DbSet<InboxItem> InboxItems { get; set; }
        public DbSet<SyncHistory> SyncHistories { get; set; }

        // Group 6: Time Tracking
        public DbSet<TimeLog> TimeLogs { get; set; }

        // Group 7: Workspace & Plane-Inspired Entities
        public DbSet<Workspace> Workspaces { get; set; }
        public DbSet<WorkspaceMember> WorkspaceMembers { get; set; }
        public DbSet<Label> Labels { get; set; }
        public DbSet<IssueLabel> IssueLabels { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<IssueModule> IssueModules { get; set; }
        public DbSet<Intake> Intakes { get; set; }
        public DbSet<Page> Pages { get; set; }
        public DbSet<StickyNote> StickyNotes { get; set; }
        public DbSet<TaskDraft> TaskDrafts { get; set; }
        public DbSet<ProjectView> ProjectViews { get; set; }
        public DbSet<TaskSubscriber> TaskSubscribers { get; set; }

        // Group 8: Goals & Strategy
        public DbSet<Goal> Goals { get; set; }
        public DbSet<GoalUpdate> GoalUpdates { get; set; }
        public DbSet<GoalLesson> GoalLessons { get; set; }
        public DbSet<GoalRisk> GoalRisks { get; set; }
        public DbSet<GoalDecision> GoalDecisions { get; set; }
        public DbSet<TeamGoal> TeamGoals { get; set; }

        // Group 9: Links & Favorites
        public DbSet<StarredItem> StarredItems { get; set; }
        public DbSet<ProjectLink> ProjectLinks { get; set; }
        public DbSet<ProjectUpdate> ProjectUpdates { get; set; }
        public DbSet<ProjectLesson> ProjectLessons { get; set; }
        public DbSet<ProjectRisk> ProjectRisks { get; set; }
        public DbSet<ProjectDecision> ProjectDecisions { get; set; }
        public DbSet<EntityFollower> EntityFollowers { get; set; }

        public DbSet<DirectMessage> DirectMessages { get; set; }
        public DbSet<ChannelMessage> ChannelMessages { get; set; }

        public DbSet<CustomFieldDefinition> CustomFieldDefinitions { get; set; }
        public DbSet<CustomFieldValue> CustomFieldValues { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =============================================
            // 0. Global Query Filters (Soft Delete) - Spec 5.1
            // =============================================
            modelBuilder.Entity<Department>().HasQueryFilter(d => !d.IsDeleted);
            modelBuilder.Entity<Project>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<WorkTask>().HasQueryFilter(wt => !wt.IsDeleted);
            modelBuilder.Entity<Workspace>().HasQueryFilter(w => !w.IsDeleted);
            modelBuilder.Entity<CustomFieldDefinition>().HasQueryFilter(cfd => !cfd.IsDeleted);
            modelBuilder.Entity<StickyNote>().HasQueryFilter(note => !note.IsDeleted);

            // =============================================
            // 0.5 Department - Project Relationship
            // =============================================
            modelBuilder.Entity<Project>()
                .HasOne(p => p.Department)
                .WithMany(d => d.Projects)
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            // =============================================
            // 1. Composite Keys & Special PKs
            // =============================================
            modelBuilder.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
            modelBuilder.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
            modelBuilder.Entity<DepartmentMember>().HasKey(x => new { x.DepartmentId, x.UserId });
            modelBuilder.Entity<ProjectMember>().HasKey(x => new { x.ProjectId, x.UserId });
            modelBuilder.Entity<TaskAssignment>().HasKey(x => new { x.WorkTaskId, x.UserId });

            modelBuilder.Entity<ContingencyPlan>()
                .HasOne(cp => cp.WorkTask)
                .WithMany(wt => wt.ContingencyPlans)
                .HasForeignKey(cp => cp.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContingencyPlanTask>()
                .HasOne(cpt => cpt.ContingencyPlan)
                .WithMany(cp => cp.ContingencyPlanTasks)
                .HasForeignKey(cpt => cpt.ContingencyPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContingencyPlanTask>()
                .HasOne(cpt => cpt.WorkTask)
                .WithMany()
                .HasForeignKey(cpt => cpt.WorkTaskId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ContingencyPlanTask>()
                .HasOne(cpt => cpt.Assignee)
                .WithMany()
                .HasForeignKey(cpt => cpt.AssigneeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ContingencyPlanTask>()
                .HasOne(cpt => cpt.ActivatedBy)
                .WithMany()
                .HasForeignKey(cpt => cpt.ActivatedById)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<TaskDependency>().HasKey(x => new { x.PredecessorTaskId, x.SuccessorTaskId });
            modelBuilder.Entity<UserWallet>().HasKey(x => x.UserId);
            modelBuilder.Entity<TaskVectorEmbedding>().HasKey(x => x.WorkTaskId);

            // New Composite Keys
            modelBuilder.Entity<WorkspaceMember>().HasKey(x => new { x.WorkspaceId, x.UserId });
            modelBuilder.Entity<IssueLabel>().HasKey(x => new { x.WorkTaskId, x.LabelId });
            modelBuilder.Entity<IssueModule>().HasKey(x => new { x.WorkTaskId, x.ModuleId });
            modelBuilder.Entity<TaskSubscriber>().HasKey(x => new { x.WorkTaskId, x.UserId });

            // =============================================
            // 2. Unique Constraints
            // =============================================
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<Permission>().HasIndex(p => p.Code).IsUnique();

            // Workspace Indexes
            modelBuilder.Entity<Workspace>().HasIndex(w => w.Slug).IsUnique();
            modelBuilder.Entity<Project>().HasIndex(p => new { p.WorkspaceId, p.Identifier }).IsUnique();

            // Group 3 Core Management Indexes
            modelBuilder.Entity<WorkTask>().HasIndex(wt => new { wt.ProjectId, wt.IsDeleted });
            modelBuilder.Entity<WorkTask>().HasIndex(wt => wt.ReporterId);
            modelBuilder.Entity<WorkTask>().HasIndex(wt => wt.AssignedUserId);
            modelBuilder.Entity<WorkTask>().HasIndex(wt => new { wt.WorkspaceId, wt.ProjectId });
            modelBuilder.Entity<WorkTask>().HasIndex(wt => wt.SortOrder);
            modelBuilder.Entity<TaskContingencyPlan>().HasIndex(plan => new { plan.WorkTaskId, plan.Status });
            modelBuilder.Entity<TaskContingencyPlan>().HasIndex(plan => plan.SupportPersonId);
            modelBuilder.Entity<ProjectMember>().HasIndex(pm => pm.UserId);
            modelBuilder.Entity<TaskDraft>().HasIndex(td => new { td.UserId, td.UpdatedAt });
            modelBuilder.Entity<TaskDraft>().HasIndex(td => new { td.UserId, td.ProjectId, td.UpdatedAt });
            modelBuilder.Entity<AiConversation>().HasIndex(conversation => new { conversation.UserId, conversation.WorkspaceId, conversation.UpdatedAt });
            modelBuilder.Entity<AiConversation>().Property(conversation => conversation.Title).HasMaxLength(180);
            modelBuilder.Entity<AiConversation>().Property(conversation => conversation.MessagesJson).HasColumnType("nvarchar(max)");
            modelBuilder.Entity<AiAttachment>(entity =>
            {
                entity.Property(item => item.FileName).HasMaxLength(255).IsRequired();
                entity.Property(item => item.StoredFileName).HasMaxLength(100).IsRequired();
                entity.Property(item => item.MimeType).HasMaxLength(150).IsRequired();
                entity.Property(item => item.Extension).HasMaxLength(20).IsRequired();
                entity.Property(item => item.Kind).HasMaxLength(20).IsRequired();
                entity.Property(item => item.Sha256).HasMaxLength(64).IsRequired();
                entity.Property(item => item.Status).HasMaxLength(30).IsRequired();
                entity.Property(item => item.ErrorMessage).HasMaxLength(500);
                entity.HasIndex(item => new { item.UserId, item.WorkspaceId, item.ConversationId, item.Sha256 });
                entity.HasIndex(item => new { item.ConversationId, item.CreatedAt });
                entity.HasOne<AiConversation>()
                    .WithMany()
                    .HasForeignKey(item => item.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<AiAttachmentChunk>(entity =>
            {
                entity.Property(item => item.Locator).HasMaxLength(180).IsRequired();
                entity.Property(item => item.Content).HasMaxLength(2000).IsRequired();
                entity.HasIndex(item => new { item.AttachmentId, item.ChunkIndex }).IsUnique();
                entity.HasOne(item => item.Attachment)
                    .WithMany(item => item.Chunks)
                    .HasForeignKey(item => item.AttachmentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<IntegrationAccount>().HasIndex(ia => new { ia.UserId, ia.Provider }).IsUnique();
            modelBuilder.Entity<InboxItem>().HasIndex(ii => new { ii.UserId, ii.Provider, ii.ExternalId }).IsUnique();
            modelBuilder.Entity<InboxItem>().HasIndex(ii => new { ii.UserId, ii.Source, ii.CreatedAt });
            modelBuilder.Entity<InboxItem>().HasIndex(ii => new { ii.UserId, ii.IsRead });
            modelBuilder.Entity<SyncHistory>().HasIndex(sh => new { sh.UserId, sh.Provider, sh.StartedAt });

            // =============================================
            // 3. Relationships - Group 1: System & Access
            // =============================================
            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            // =============================================
            // 4. Relationships - Group 2: Organization
            // =============================================
            modelBuilder.Entity<Department>()
                .HasOne(d => d.Manager)
                .WithMany(u => u.ManagedDepartments)
                .HasForeignKey(d => d.ManagerId)
                .OnDelete(DeleteBehavior.SetNull);

            // Department Hierarchy
            modelBuilder.Entity<Department>()
                .HasOne(d => d.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(d => d.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DepartmentMember>()
                .HasOne(dm => dm.Department)
                .WithMany(d => d.DepartmentMembers)
                .HasForeignKey(dm => dm.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DepartmentMember>()
                .HasOne(dm => dm.User)
                .WithMany(u => u.DepartmentMemberships)
                .HasForeignKey(dm => dm.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // =============================================
            // 5. Relationships - Group 3: Core Work Management
            // =============================================
            modelBuilder.Entity<Project>()
                .HasOne(p => p.Creator)
                .WithMany(u => u.CreatedProjects)
                .HasForeignKey(p => p.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectMember>()
                .HasOne(pm => pm.Project)
                .WithMany(p => p.ProjectMembers)
                .HasForeignKey(pm => pm.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectMember>()
                .HasOne(pm => pm.User)
                .WithMany(u => u.ProjectMemberships)
                .HasForeignKey(pm => pm.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Sprint>()
                .HasOne(s => s.Project)
                .WithMany(p => p.Sprints)
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskType>()
                .HasOne(tt => tt.Project)
                .WithMany(p => p.TaskTypes)
                .HasForeignKey(tt => tt.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskStatus>()
                .HasOne(ts => ts.Project)
                .WithMany(p => p.TaskStatuses)
                .HasForeignKey(ts => ts.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WorkTask>()
                .HasOne(wt => wt.Project)
                .WithMany(p => p.WorkTasks)
                .HasForeignKey(wt => wt.ProjectId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<WorkTask>()
                .HasOne(wt => wt.Sprint)
                .WithMany(s => s.WorkTasks)
                .HasForeignKey(wt => wt.SprintId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<WorkTask>()
                .HasOne(wt => wt.ParentTask)
                .WithMany(wt => wt.ChildTasks)
                .HasForeignKey(wt => wt.ParentTaskId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WorkTask>()
                .HasOne(wt => wt.TaskType)
                .WithMany(tt => tt.WorkTasks)
                .HasForeignKey(wt => wt.TaskTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WorkTask>()
                .HasOne(wt => wt.TaskStatus)
                .WithMany(ts => ts.WorkTasks)
                .HasForeignKey(wt => wt.TaskStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<WorkTask>()
                .HasOne(wt => wt.Reporter)
                .WithMany(u => u.ReportedTasks)
                .HasForeignKey(wt => wt.ReporterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TaskAssignment>()
                .HasOne(ta => ta.WorkTask)
                .WithMany(wt => wt.TaskAssignments)
                .HasForeignKey(ta => ta.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskAssignment>()
                .HasOne(ta => ta.User)
                .WithMany(u => u.TaskAssignments)
                .HasForeignKey(ta => ta.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TaskAssignment>()
                .HasOne(ta => ta.BlockedByUser)
                .WithMany()
                .HasForeignKey(ta => ta.BlockedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TaskAssignment>()
                .HasOne(ta => ta.RemovedByUser)
                .WithMany()
                .HasForeignKey(ta => ta.RemovedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TaskAssignment>()
                .Property(ta => ta.ContributionWeight)
                .HasDefaultValue(1.0);

            modelBuilder.Entity<TaskDependency>()
                .HasOne(td => td.PredecessorTask)
                .WithMany(wt => wt.SuccessorDependencies)
                .HasForeignKey(td => td.PredecessorTaskId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TaskDependency>()
                .HasOne(td => td.SuccessorTask)
                .WithMany(wt => wt.PredecessorDependencies)
                .HasForeignKey(td => td.SuccessorTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            // =============================================
            // 6. Relationships - Group 4: Collaboration & Tracking
            // =============================================
            modelBuilder.Entity<Comment>()
                .HasIndex(c => new { c.EntityType, c.EntityId });

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.ParentComment)
                .WithMany(pc => pc.ChildComments)
                .HasForeignKey(c => c.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Attachment>()
                .HasOne(a => a.WorkTask)
                .WithMany(wt => wt.Attachments)
                .HasForeignKey(a => a.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Attachment>()
                .HasOne(a => a.User)
                .WithMany(u => u.Attachments)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AuditLog>()
                .HasOne(al => al.WorkTask)
                .WithMany(wt => wt.AuditLogs)
                .HasForeignKey(al => al.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AuditLog>()
                .HasOne(al => al.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // CommentAttachment -> Comment, User
            modelBuilder.Entity<CommentAttachment>()
                .HasOne(ca => ca.Comment)
                .WithMany(c => c.CommentAttachments)
                .HasForeignKey(ca => ca.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CommentAttachment>()
                .HasOne(ca => ca.UploadedByUser)
                .WithMany()
                .HasForeignKey(ca => ca.UploadedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.TriggeredByUser)
                .WithMany()
                .HasForeignKey(n => n.TriggeredByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SystemAuditLog>()
                .HasOne(sal => sal.User)
                .WithMany(u => u.SystemAuditLogs)
                .HasForeignKey(sal => sal.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<IntegrationAccount>()
                .HasOne(ia => ia.User)
                .WithMany()
                .HasForeignKey(ia => ia.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InboxItem>()
                .HasOne(ii => ii.User)
                .WithMany()
                .HasForeignKey(ii => ii.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InboxItem>()
                .HasOne(ii => ii.IntegrationAccount)
                .WithMany(ia => ia.InboxItems)
                .HasForeignKey(ii => ii.IntegrationAccountId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<InboxItem>()
                .HasOne(ii => ii.CreatedTask)
                .WithMany()
                .HasForeignKey(ii => ii.CreatedTaskId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SyncHistory>()
                .HasOne(sh => sh.User)
                .WithMany()
                .HasForeignKey(sh => sh.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SyncHistory>()
                .HasOne(sh => sh.IntegrationAccount)
                .WithMany(ia => ia.SyncHistories)
                .HasForeignKey(sh => sh.IntegrationAccountId)
                .OnDelete(DeleteBehavior.NoAction);

            // =============================================
            // 7. Relationships - Group 5: Gamification
            // =============================================
            modelBuilder.Entity<PerformanceReview>()
                .HasOne(pr => pr.Reviewer)
                .WithMany(u => u.ReviewsGiven)
                .HasForeignKey(pr => pr.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PerformanceReview>()
                .HasOne(pr => pr.Reviewee)
                .WithMany(u => u.ReviewsReceived)
                .HasForeignKey(pr => pr.RevieweeId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserWallet>()
                .HasOne(uw => uw.User)
                .WithOne(u => u.Wallet)
                .HasForeignKey<UserWallet>(uw => uw.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PointTransaction>()
                .HasOne(pt => pt.UserWallet)
                .WithMany(uw => uw.PointTransactions)
                .HasForeignKey(pt => pt.UserWalletUserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<PointTransaction>()
                .HasOne(pt => pt.WorkTask)
                .WithMany()
                .HasForeignKey(pt => pt.WorkTaskId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PointTransaction>()
                .HasIndex(pt => new { pt.UserWalletUserId, pt.WorkTaskId, pt.TransactionType });

            modelBuilder.Entity<PointTransaction>()
                .Property(pt => pt.IdempotencyKey)
                .HasMaxLength(200);

            modelBuilder.Entity<PointTransaction>()
                .Property(pt => pt.RewardRuleVersion)
                .HasMaxLength(40);

            modelBuilder.Entity<PointTransaction>()
                .HasIndex(pt => pt.IdempotencyKey)
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");

            modelBuilder.Entity<PointTransaction>()
                .HasIndex(pt => pt.ReversalOfTransactionId)
                .IsUnique()
                .HasFilter("[ReversalOfTransactionId] IS NOT NULL");

            modelBuilder.Entity<PointTransaction>()
                .HasOne(pt => pt.ReversalOfTransaction)
                .WithMany()
                .HasForeignKey(pt => pt.ReversalOfTransactionId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<CommentMention>()
                .HasOne(cm => cm.Comment)
                .WithMany()
                .HasForeignKey(cm => cm.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SiteAuditLog>()
                .HasOne(sal => sal.User)
                .WithMany()
                .HasForeignKey(sal => sal.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SiteAuditLog>()
                .HasIndex(sal => new { sal.EntityType, sal.EntityId });

            modelBuilder.Entity<RecentView>()
                .HasOne(rv => rv.User)
                .WithMany()
                .HasForeignKey(rv => rv.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RecentView>().Property(rv => rv.EntityType).HasMaxLength(64);
            modelBuilder.Entity<RecentView>().Property(rv => rv.Title).HasMaxLength(512);
            modelBuilder.Entity<RecentView>().Property(rv => rv.Subtitle).HasMaxLength(512);
            modelBuilder.Entity<RecentView>().Property(rv => rv.Url).HasMaxLength(1024);
            modelBuilder.Entity<RecentView>().Property(rv => rv.Icon).HasMaxLength(128);

            modelBuilder.Entity<RecentView>()
                .HasIndex(rv => new { rv.UserId, rv.EntityType, rv.EntityId })
                .IsUnique();

            modelBuilder.Entity<KudoReaction>()
                .HasOne(kr => kr.Kudo)
                .WithMany()
                .HasForeignKey(kr => kr.KudoId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<KudoReaction>()
                .HasOne(kr => kr.User)
                .WithMany()
                .HasForeignKey(kr => kr.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // =============================================
            // 8. Relationships - Group 6: AI Integration
            // =============================================
            modelBuilder.Entity<AITokenUsage>()
                .HasOne(tu => tu.User)
                .WithMany(u => u.AITokenUsages)
                .HasForeignKey(tu => tu.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AiPricingPlan>().HasIndex(x => x.Code).IsUnique();
            modelBuilder.Entity<AiPricingPlan>().Property(x => x.Code).HasMaxLength(64);
            modelBuilder.Entity<AiPricingPlan>().Property(x => x.Name).HasMaxLength(128);
            modelBuilder.Entity<AiPricingPlan>().Property(x => x.PricingStatus).HasMaxLength(64);
            modelBuilder.Entity<AiCreditRule>().HasIndex(x => x.ActionType).IsUnique();
            modelBuilder.Entity<AiCreditRule>().Property(x => x.ActionType).HasMaxLength(128);
            modelBuilder.Entity<AiUsageLedger>().HasIndex(x => new { x.WorkspaceId, x.OccurredAt });
            modelBuilder.Entity<AiUsageLedger>().HasIndex(x => new { x.UserId, x.OccurredAt });
            modelBuilder.Entity<AiUsageLedger>().HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            modelBuilder.Entity<AiUsageLedger>()
                .HasOne(x => x.User).WithMany(x => x.AiUsageLedgerEntries).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AiUsageLedger>()
                .HasOne(x => x.Workspace).WithMany().HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<AiUsageLedger>()
                .HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<AiActionExecution>().HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
            modelBuilder.Entity<AiActionExecution>().HasIndex(x => new { x.UserId, x.State, x.UpdatedAt });
            modelBuilder.Entity<AiActionExecution>().Property(x => x.ActionType).HasMaxLength(128);
            modelBuilder.Entity<AiActionExecution>().Property(x => x.IdempotencyKey).HasMaxLength(200);
            modelBuilder.Entity<AiActionExecution>().Property(x => x.PayloadHash).HasMaxLength(64);
            modelBuilder.Entity<AiActionExecution>().Property(x => x.State).HasMaxLength(32);
            modelBuilder.Entity<AiActionExecution>().Property(x => x.ErrorCode).HasMaxLength(64);
            modelBuilder.Entity<AiActionExecution>().Property(x => x.RowVersion).IsRowVersion();
            modelBuilder.Entity<NotificationPreference>().HasIndex(x => new { x.UserId, x.Category }).IsUnique();
            modelBuilder.Entity<NotificationPreference>()
                .HasOne(x => x.User).WithMany(x => x.NotificationPreferences).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AIFeedback>()
                .HasOne(f => f.User)
                .WithMany(u => u.AIFeedbacks)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AITrainingDataset>()
                .HasOne(td => td.Creator)
                .WithMany(u => u.AITrainingDatasets)
                .HasForeignKey(td => td.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TaskVectorEmbedding>()
                .HasOne(tve => tve.WorkTask)
                .WithOne(wt => wt.TaskVectorEmbedding)
                .HasForeignKey<TaskVectorEmbedding>(tve => tve.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskContingencyPlan>()
                .HasOne(plan => plan.WorkTask)
                .WithMany(task => task.TaskContingencyPlans)
                .HasForeignKey(plan => plan.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskContingencyPlan>()
                .HasOne(plan => plan.SupportPerson)
                .WithMany()
                .HasForeignKey(plan => plan.SupportPersonId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TaskContingencyPlan>()
                .HasOne(plan => plan.CreatedBy)
                .WithMany()
                .HasForeignKey(plan => plan.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<TaskContingencyPlan>()
                .HasOne(plan => plan.UpdatedBy)
                .WithMany()
                .HasForeignKey(plan => plan.UpdatedById)
                .OnDelete(DeleteBehavior.NoAction);

            // =============================================
            // 9. Relationships - Time Tracking
            // =============================================
            modelBuilder.Entity<TimeLog>()
                .HasOne(tl => tl.WorkTask)
                .WithMany(wt => wt.TimeLogs)
                .HasForeignKey(tl => tl.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TimeLog>()
                .HasOne(tl => tl.User)
                .WithMany(u => u.TimeLogs)
                .HasForeignKey(tl => tl.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // =============================================
            // 10. Workspace & Plane-Inspired Relationships
            // =============================================

            // Workspace -> Owner (User)
            modelBuilder.Entity<Workspace>()
                .HasOne(w => w.Owner)
                .WithMany(u => u.OwnedWorkspaces)
                .HasForeignKey(w => w.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // WorkspaceMember -> Workspace, User
            modelBuilder.Entity<WorkspaceMember>()
                .HasOne(wm => wm.Workspace)
                .WithMany(w => w.Members)
                .HasForeignKey(wm => wm.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<WorkspaceMember>()
                .HasOne(wm => wm.User)
                .WithMany(u => u.WorkspaceMemberships)
                .HasForeignKey(wm => wm.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Project -> Workspace
            modelBuilder.Entity<Project>()
                .HasOne(p => p.Workspace)
                .WithMany(w => w.Projects)
                .HasForeignKey(p => p.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Label -> Workspace, Project (optional)
            modelBuilder.Entity<Label>()
                .HasOne(l => l.Workspace)
                .WithMany()
                .HasForeignKey(l => l.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Label>()
                .HasOne(l => l.Project)
                .WithMany(p => p.Labels)
                .HasForeignKey(l => l.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // IssueLabel -> WorkTask, Label
            modelBuilder.Entity<IssueLabel>()
                .HasOne(il => il.WorkTask)
                .WithMany(wt => wt.IssueLabels)
                .HasForeignKey(il => il.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IssueLabel>()
                .HasOne(il => il.Label)
                .WithMany(l => l.IssueLabels)
                .HasForeignKey(il => il.LabelId)
                .OnDelete(DeleteBehavior.Restrict);

            // Module -> Project
            modelBuilder.Entity<Module>()
                .HasOne(m => m.Project)
                .WithMany(p => p.Modules)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Module>()
                .HasOne(m => m.Lead)
                .WithMany()
                .HasForeignKey(m => m.LeadId)
                .OnDelete(DeleteBehavior.SetNull);

            // IssueModule -> WorkTask, Module
            modelBuilder.Entity<IssueModule>()
                .HasOne(im => im.WorkTask)
                .WithMany(wt => wt.IssueModules)
                .HasForeignKey(im => im.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IssueModule>()
                .HasOne(im => im.Module)
                .WithMany(m => m.IssueModules)
                .HasForeignKey(im => im.ModuleId)
                .OnDelete(DeleteBehavior.Restrict);

            // TaskSubscriber -> WorkTask, User
            modelBuilder.Entity<TaskSubscriber>()
                .HasOne(ts => ts.WorkTask)
                .WithMany(wt => wt.Subscribers)
                .HasForeignKey(ts => ts.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskSubscriber>()
                .HasOne(ts => ts.User)
                .WithMany()
                .HasForeignKey(ts => ts.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskDraft>()
                .HasOne(td => td.User)
                .WithMany()
                .HasForeignKey(td => td.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Intake -> Project, Users
            modelBuilder.Entity<Intake>()
                .HasOne(i => i.Project)
                .WithMany(p => p.Intakes)
                .HasForeignKey(i => i.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Intake>()
                .HasOne(i => i.SubmittedBy)
                .WithMany()
                .HasForeignKey(i => i.SubmittedById)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Intake>()
                .HasOne(i => i.ReviewedBy)
                .WithMany()
                .HasForeignKey(i => i.ReviewedById)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Intake>()
                .HasOne(i => i.CreatedIssue)
                .WithMany()
                .HasForeignKey(i => i.CreatedIssueId)
                .OnDelete(DeleteBehavior.SetNull);

            // Page -> Project, Users
            modelBuilder.Entity<Page>()
                .HasOne(pg => pg.Project)
                .WithMany(p => p.Pages)
                .HasForeignKey(pg => pg.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Page>()
                .HasOne(pg => pg.CreatedBy)
                .WithMany()
                .HasForeignKey(pg => pg.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Page>()
                .HasOne(pg => pg.UpdatedBy)
                .WithMany()
                .HasForeignKey(pg => pg.UpdatedById)
                .OnDelete(DeleteBehavior.NoAction);

            // =============================================
            // 11. Goals & Strategy Relationships
            // =============================================
            modelBuilder.Entity<Goal>()
                .HasOne(g => g.Owner)
                .WithMany()
                .HasForeignKey(g => g.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Goal>()
                .HasOne(g => g.Department)
                .WithMany()
                .HasForeignKey(g => g.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Goal>()
                .HasOne(g => g.Workspace)
                .WithMany()
                .HasForeignKey(g => g.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Goal>()
                .HasOne(g => g.ParentGoal)
                .WithMany(g => g.SubGoals)
                .HasForeignKey(g => g.ParentGoalId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<GoalUpdate>()
                .HasOne(gu => gu.Goal)
                .WithMany(g => g.Updates)
                .HasForeignKey(gu => gu.GoalId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TeamGoal>()
                .HasOne(tg => tg.Department)
                .WithMany(d => d.TeamGoals)
                .HasForeignKey(tg => tg.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TeamGoal>()
                .HasOne(tg => tg.Goal)
                .WithMany(g => g.TeamGoals)
                .HasForeignKey(tg => tg.GoalId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TeamGoal>()
                .HasOne(tg => tg.CreatedByUser)
                .WithMany()
                .HasForeignKey(tg => tg.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeamGoal>()
                .HasIndex(tg => new { tg.DepartmentId, tg.GoalId })
                .IsUnique();

            modelBuilder.Entity<GoalLesson>()
                .HasOne(gl => gl.Goal)
                .WithMany(g => g.Lessons)
                .HasForeignKey(gl => gl.GoalId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GoalRisk>()
                .HasOne(gr => gr.Goal)
                .WithMany(g => g.Risks)
                .HasForeignKey(gr => gr.GoalId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<GoalDecision>()
                .HasOne(gd => gd.Goal)
                .WithMany(g => g.Decisions)
                .HasForeignKey(gd => gd.GoalId)
                .OnDelete(DeleteBehavior.Cascade);

            // =============================================
            // 12. Links & Favorites Relationships
            // =============================================
            modelBuilder.Entity<StarredItem>()
                .HasOne(si => si.User)
                .WithMany()
                .HasForeignKey(si => si.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StarredItem>()
                .HasOne(si => si.Workspace)
                .WithMany()
                .HasForeignKey(si => si.WorkspaceId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StarredItem>()
                .Property(si => si.ItemType)
                .IsRequired()
                .HasMaxLength(64);

            modelBuilder.Entity<StarredItem>()
                .HasIndex(si => new { si.UserId, si.WorkspaceId, si.ItemType, si.ItemId })
                .IsUnique();

            modelBuilder.Entity<StarredItem>()
                .HasIndex(si => si.UserId);

            modelBuilder.Entity<StarredItem>()
                .ToTable("StarredItems", table => table.HasCheckConstraint(
                    "CK_StarredItems_ItemType",
                    "[ItemType] IN ('Goal', 'Project', 'Team', 'User')"));

            modelBuilder.Entity<ProjectLink>()
                .HasOne(pl => pl.Project)
                .WithMany()
                .HasForeignKey(pl => pl.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectLink>()
                .HasOne(pl => pl.Creator)
                .WithMany()
                .HasForeignKey(pl => pl.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StickyNote>(entity =>
            {
                entity.Property(note => note.Title).HasMaxLength(180).IsRequired();
                entity.Property(note => note.Content).HasMaxLength(10000).IsRequired();
                entity.Property(note => note.Color).HasMaxLength(20).IsRequired();
                entity.Property(note => note.SourceRoute).HasMaxLength(500);
                entity.HasIndex(note => new { note.UserId, note.IsDeleted, note.IsPinned, note.UpdatedAt });
                entity.HasIndex(note => new { note.UserId, note.IsFloating });
                entity.HasIndex(note => note.WorkspaceId);
                entity.HasIndex(note => note.ProjectId);
                entity.HasIndex(note => note.WorkTaskId);
                entity.HasIndex(note => note.GoalId);
            });

            // =============================================
            // 11. Applying custom configurations
            // =============================================
            modelBuilder.ApplyConfiguration(new Configurations.ProjectTemplateConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.ProjectDepartmentRoleConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.TenantConfigConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.RefreshTokenConfiguration());


           modelBuilder.Entity<DirectMessage>()
                .HasOne(dm => dm.Sender)
                .WithMany()
                .HasForeignKey(dm => dm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DirectMessage>()
                .HasOne(dm => dm.Receiver)
                .WithMany()
                .HasForeignKey(dm => dm.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChannelMessage>()
                .HasOne(cm => cm.Sender)
                .WithMany()
                .HasForeignKey(cm => cm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ChannelMessage>()
                .HasOne(cm => cm.Channel)
                .WithMany()
                .HasForeignKey(cm => cm.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);
            // Custom Fields Configurations
            modelBuilder.Entity<CustomFieldDefinition>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasOne(x => x.Project)
                    .WithMany(p => p.CustomFieldDefinitions)
                    .HasForeignKey(x => x.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => new { x.ProjectId, x.Key })
                    .IsUnique()
                    .HasFilter("[IsDeleted] = 0");
            });

            modelBuilder.Entity<CustomFieldValue>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasOne(x => x.WorkTask)
                    .WithMany(t => t.CustomFieldValues)
                    .HasForeignKey(x => x.WorkTaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.FieldDefinition)
                    .WithMany(fd => fd.CustomFieldValues)
                    .HasForeignKey(x => x.FieldDefinitionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(x => new { x.WorkTaskId, x.FieldDefinitionId })
                    .IsUnique();
            });

        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // 1. Timezone Handling: Strictly save dates as UTC
            var dateTimeProperties = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                .SelectMany(e => e.Properties)
                .Where(p => p.Metadata.ClrType == typeof(DateTime) || p.Metadata.ClrType == typeof(DateTime?));

            foreach (var prop in dateTimeProperties)
            {
                if (prop.CurrentValue is DateTime dt && dt.Kind != DateTimeKind.Utc)
                {
                    var propertyName = prop.Metadata.Name;
                    if (propertyName is "PlannedStartDate" or "PlannedEndDate" or "DueDate" or "StartDate" or "EndDate")
                    {
                        prop.CurrentValue = DateTime.SpecifyKind(dt.Date, DateTimeKind.Utc);
                    }
                    else
                    {
                        prop.CurrentValue = dt.ToUniversalTime();
                    }
                }
            }

            // 2. Automatic Data Roll-up logic preparation
            var timeLogEntries = ChangeTracker.Entries<TimeLog>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                .ToList();
            var changedTaskEntries = ChangeTracker.Entries<WorkTask>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                .ToList();
            var assignmentEntries = ChangeTracker.Entries<TaskAssignment>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                .ToList();

            // Save the logs first
            
            // =============================================
            // 2.5 Audit Logging (Module 6)
            // =============================================
            var pendingLogsActions = new List<Action<List<AuditLog>>>();
            var pendingLogs = new List<AuditLog>();

            try
            {
                if (_auditLogQueue != null)
                {
                    var userIdClaim = _httpContextAccessor?.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (Guid.TryParse(userIdClaim, out Guid parsedUserId) && parsedUserId != Guid.Empty)
                    {
                        var blacklist = new[] { "PasswordHash", "SecretKey", "RefreshToken", "MatKhau" };
                        var taskEntries = ChangeTracker.Entries<WorkTask>()
                            .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added)
                            .ToList();

                        foreach (var entry in taskEntries)
                        {
                            var changedFields = new List<string>();
                            var oldValuesDict = new Dictionary<string, string?>();
                            var newValuesDict = new Dictionary<string, string?>();

                            foreach (var prop in entry.Properties.Where(p => p.IsModified || entry.State == EntityState.Added))
                            {
                                var propName = prop.Metadata.Name;
                                if (propName == "UpdatedAt" || propName == "CreatedAt" || propName == "RowVersion" || propName == "Id" || propName == "SortOrder" || propName == "SequenceId" || propName == "CreatorId" || propName == "WorkspaceId") 
                                    continue;

                                string? oldVal = null;
                                if (entry.State != EntityState.Added)
                                {
                                    try { oldVal = entry.OriginalValues[propName]?.ToString(); } catch { }
                                }
                                
                                string? newVal = null;
                                try { newVal = entry.CurrentValues[propName]?.ToString(); } catch { }

                                // Sensitive Data Masking
                                if (Array.Exists(blacklist, b => b == propName))
                                {
                                    oldVal = oldVal != null ? "******" : null;
                                    newVal = newVal != null ? "******" : null;
                                }

                                changedFields.Add(propName);
                                if (oldVal != null) oldValuesDict[propName] = oldVal;
                                if (newVal != null) newValuesDict[propName] = newVal;
                            }

                            if (changedFields.Count > 0)
                            {
                                pendingLogsActions.Add(list => list.Add(new AuditLog
                                {
                                    Id = Guid.NewGuid(),
                                    WorkTaskId = entry.Entity.Id, 
                                    UserId = parsedUserId,
                                    FieldChanged = string.Join(", ", changedFields),
                                    OldValue = JsonSerializer.Serialize(oldValuesDict),
                                    NewValue = JsonSerializer.Serialize(newValuesDict),
                                    CreatedAt = DateTime.UtcNow
                                }));
                            }
                        }

                        var commentEntries = ChangeTracker.Entries<Comment>()
                            .Where(e => e.State == EntityState.Added)
                            .ToList();

                        foreach (var entry in commentEntries)
                        {
                            pendingLogsActions.Add(list => list.Add(new AuditLog
                            {
                                Id = Guid.NewGuid(),
                                WorkTaskId = entry.Entity.EntityId,
                                UserId = parsedUserId,
                                FieldChanged = "ADD_COMMENT",
                                OldValue = "{}",
                                NewValue = "{\"Comment\": \"added\"}",
                                CreatedAt = DateTime.UtcNow
                            }));
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently ignore audit log generation failures to keep the core transaction 100% stable
            }

            var result = await base.SaveChangesAsync(cancellationToken);

            // Execute Audit Log actions and enqueue
            if (pendingLogsActions.Count > 0 && _auditLogQueue != null)
            {
                try
                {
                    foreach (var action in pendingLogsActions) action(pendingLogs);
                    foreach (var log in pendingLogs)
                    {
                        await _auditLogQueue.EnqueueAsync(log, cancellationToken);
                    }
                }
                catch (Exception)
                {
                    // Fail gracefully
                }
            }

            // 3. Automatic Data Roll-up execution (Update Tasks, Assignments, and Parent Tasks)
            var affectedTaskIds = new HashSet<Guid>(
                timeLogEntries.Select(e => e.Entity.WorkTaskId)
                    .Concat(assignmentEntries.Select(e => e.Entity.WorkTaskId))
                    .Concat(changedTaskEntries.Where(e => e.State != EntityState.Deleted).Select(e => e.Entity.Id))
            );

            var affectedParentIds = changedTaskEntries
                .Select(e => e.Entity.ParentTaskId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();

            foreach (var parentId in affectedParentIds)
            {
                affectedTaskIds.Add(parentId);
            }

            if (affectedTaskIds.Any())
            {
                var tasks = await WorkTasks
                    .Where(t => affectedTaskIds.Contains(t.Id))
                    .ToListAsync(cancellationToken);

                var assignmentTaskIds = tasks.Select(t => t.Id).ToList();
                if (assignmentTaskIds.Any())
                {
                    var assignments = await TaskAssignments
                        .Where(ta => assignmentTaskIds.Contains(ta.WorkTaskId))
                        .ToListAsync(cancellationToken);

                    foreach (var assignment in assignments)
                    {
                        assignment.TotalActualHours = await TimeLogs
                            .Where(tl => tl.WorkTaskId == assignment.WorkTaskId && tl.UserId == assignment.UserId)
                            .SumAsync(tl => tl.Hours, cancellationToken);
                    }
                }

                await base.SaveChangesAsync(cancellationToken);

                foreach (var task in tasks)
                {
                    var hasChildren = await WorkTasks.AnyAsync(t => t.ParentTaskId == task.Id && !t.IsDeleted, cancellationToken);
                    if (!hasChildren)
                    {
                        var assignmentEstimateTotal = await TaskAssignments
                            .Where(ta => ta.WorkTaskId == task.Id && ta.Status)
                            .SumAsync(ta => (double?)ta.EstimatedHours, cancellationToken) ?? 0;

                        if (assignmentEstimateTotal > 0)
                        {
                            task.TotalEstimatedHours = Math.Round(assignmentEstimateTotal, 1);
                        }
                    }

                    task.TotalActualHours = await TimeLogs
                        .Where(tl => tl.WorkTaskId == task.Id)
                        .SumAsync(tl => tl.Hours, cancellationToken);
                }
                await base.SaveChangesAsync(cancellationToken);

                var parentIds = tasks
                    .Where(t => t.ParentTaskId != null)
                    .Select(t => t.ParentTaskId!.Value)
                    .Concat(affectedParentIds)
                    .Distinct()
                    .ToList();

                while (parentIds.Any())
                {
                    var parents = await WorkTasks
                        .Where(t => parentIds.Contains(t.Id))
                        .ToListAsync(cancellationToken);

                    foreach (var parent in parents)
                    {
                        parent.TotalActualHours = await WorkTasks
                            .Where(t => t.ParentTaskId == parent.Id && !t.IsDeleted)
                            .SumAsync(t => (double?)t.TotalActualHours, cancellationToken) ?? 0;
                        parent.TotalEstimatedHours = await WorkTasks
                            .Where(t => t.ParentTaskId == parent.Id && !t.IsDeleted)
                            .SumAsync(t => (double?)t.TotalEstimatedHours, cancellationToken) ?? 0;
                    }
                    await base.SaveChangesAsync(cancellationToken);

                    parentIds = parents
                        .Where(t => t.ParentTaskId != null)
                        .Select(t => t.ParentTaskId!.Value)
                        .Distinct()
                        .ToList();
                }
            }

            return result;
        }
    }
}
