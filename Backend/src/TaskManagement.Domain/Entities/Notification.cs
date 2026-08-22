using System;

namespace TaskManagement.Domain.Entities
{
    /// <summary>
    /// Real-time notification entity with rich context for SignalR push
    /// </summary>
    public class Notification
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? LinkUrl { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; }

        /// <summary>Stable application-owned key used to make event notifications idempotent.</summary>
        public string? DedupeKey { get; set; }

        // Enhanced fields for rich notifications
        /// <summary>TASK_STATUS_CHANGED, TASK_ASSIGNED, COMMENT_ADDED, TASK_DUE_SOON, etc.</summary>
        public string NotificationType { get; set; } = "GENERAL";

        /// <summary>Related task ID for deep linking</summary>
        public Guid? RelatedTaskId { get; set; }

        /// <summary>Related project ID for context</summary>
        public Guid? RelatedProjectId { get; set; }

        public Guid? CollaborationChannelId { get; set; }
        public CollaborationChannel? CollaborationChannel { get; set; }
        public Guid? ChannelMessageId { get; set; }
        public ChannelMessage? ChannelMessage { get; set; }

        public Guid? RelatedInvitationId { get; set; }
        public ProjectInvitation? RelatedInvitation { get; set; }
        public string? ActionState { get; set; }

        /// <summary>Who triggered this notification</summary>
        public Guid? TriggeredByUserId { get; set; }
        public User? TriggeredByUser { get; set; }
    }
}
