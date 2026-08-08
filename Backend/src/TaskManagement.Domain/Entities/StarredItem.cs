using System;

namespace TaskManagement.Domain.Entities
{
    public static class StarredItemTypes
    {
        public const string Goal = "Goal";
        public const string Project = "Project";
        public const string Team = "Team";
        public const string User = "User";
        public const string WorkTask = "WorkTask";

        public static readonly string[] Allowed = [Goal, Project, Team, User, WorkTask];

        public static string? Normalize(string? value)
        {
            return value?.Trim().ToLowerInvariant() switch
            {
                "goal" => Goal,
                "project" => Project,
                "team" => Team,
                "user" => User,
                "task" or "work-task" or "work_task" or "worktask" => WorkTask,
                _ => null
            };
        }
    }

    public class StarredItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public Guid WorkspaceId { get; set; }
        public Workspace Workspace { get; set; } = null!;
        
        public string ItemType { get; set; } = string.Empty;
        public Guid ItemId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
