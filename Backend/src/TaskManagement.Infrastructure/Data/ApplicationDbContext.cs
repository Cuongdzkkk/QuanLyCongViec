using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskStatus = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        { }

        // CỤM 1: QUẢN TRỊ & PHÂN QUYỀN
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }

        // CỤM 2: TỔ CHỨC & NHÂN SỰ
        public DbSet<Department> Departments { get; set; }
        public DbSet<DepartmentMember> DepartmentMembers { get; set; }

        // CỤM 3: LÕI QUẢN LÝ CÔNG VIỆC
        public DbSet<Project> Projects { get; set; }
        public DbSet<Sprint> Sprints { get; set; }
        public DbSet<TaskType> TaskTypes { get; set; }
        public DbSet<TaskStatus> TaskStatuses { get; set; }
        public DbSet<WorkTask> WorkTasks { get; set; }
        public DbSet<TaskAssignment> TaskAssignments { get; set; }
        public DbSet<TaskDependency> TaskDependencies { get; set; }

        // CỤM 4: TƯƠNG TÁC & TRUY VẾT
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        // CỤM 5: HIỆU SUẤT & GAMIFICATION
        public DbSet<PerformanceReview> PerformanceReviews { get; set; }
        public DbSet<UserWallet> UserWallets { get; set; }
        public DbSet<PointTransaction> PointTransactions { get; set; }

        // CỤM 6: TRÍ TUỆ NHÂN TẠO
        public DbSet<AIPromptTemplate> AIPromptTemplates { get; set; }
        public DbSet<AITokenUsage> AITokenUsages { get; set; }
        public DbSet<AIFeedback> AIFeedbacks { get; set; }
        public DbSet<AITrainingDataset> AITrainingDatasets { get; set; }
        public DbSet<TaskVectorEmbedding> TaskVectorEmbeddings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Composite Keys
            modelBuilder.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
            modelBuilder.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
            modelBuilder.Entity<DepartmentMember>().HasKey(x => new { x.DepartmentId, x.UserId });
            modelBuilder.Entity<TaskAssignment>().HasKey(x => new { x.WorkTaskId, x.UserId });
            modelBuilder.Entity<TaskDependency>().HasKey(x => new { x.PredecessorTaskId, x.SuccessorTaskId });

            // 2. Primary Keys configured conventionally
            modelBuilder.Entity<UserWallet>().HasKey(x => x.UserId);
            modelBuilder.Entity<TaskVectorEmbedding>().HasKey(x => x.WorkTaskId);

            // 3. Unique constraints
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<Permission>().HasIndex(p => p.Code).IsUnique();

            // 4. Relationships
            // Cụm 1
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

            // Cụm 2
            modelBuilder.Entity<Department>()
                .HasOne(d => d.Manager)
                .WithMany(u => u.ManagedDepartments)
                .HasForeignKey(d => d.ManagerId)
                .OnDelete(DeleteBehavior.SetNull);

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

            // Cụm 3
            modelBuilder.Entity<Project>()
                .HasOne(p => p.Owner)
                .WithMany(u => u.OwnedProjects)
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

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

            // Cụm 4
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.WorkTask)
                .WithMany(wt => wt.Comments)
                .HasForeignKey(c => c.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);

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
                .HasOne(al => al.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(al => al.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cụm 5
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
                .HasOne(pt => pt.Wallet)
                .WithMany(uw => uw.PointTransactions)
                .HasForeignKey(pt => pt.WalletId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cụm 6
            modelBuilder.Entity<AITokenUsage>()
                .HasOne(tu => tu.User)
                .WithMany(u => u.AITokenUsages)
                .HasForeignKey(tu => tu.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AIFeedback>()
                .HasOne(f => f.User)
                .WithMany(u => u.AIFeedbacks)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TaskVectorEmbedding>()
                .HasOne(tve => tve.WorkTask)
                .WithOne(wt => wt.TaskVectorEmbedding)
                .HasForeignKey<TaskVectorEmbedding>(tve => tve.WorkTaskId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}