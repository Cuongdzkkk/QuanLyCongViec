using System;

namespace TaskManagement.Domain.Entities
{
    public class ChannelMessage
    {
        public Guid Id { get; set; }
        public Guid? LegacyDepartmentId { get; set; }
        public Department? LegacyDepartment { get; set; }
        public Guid? CollaborationChannelId { get; set; }
        public CollaborationChannel? CollaborationChannel { get; set; }
        public Guid SenderId { get; set; }
        public User? Sender { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public Guid? ReplyToMessageId { get; set; }
        public ChannelMessage? ReplyToMessage { get; set; }
        public ICollection<ChannelMessage> Replies { get; set; } = new List<ChannelMessage>();
        public string? AttachmentUrl { get; set; }
        public ICollection<CollaborationMessageAttachment> Attachments { get; set; } = new List<CollaborationMessageAttachment>();
        public ICollection<ChannelMessageMention> Mentions { get; set; } = new List<ChannelMessageMention>();
        public ICollection<CollaborationMessageReaction> Reactions { get; set; } = new List<CollaborationMessageReaction>();
        public CollaborationMessagePin? Pin { get; set; }
    }
}
