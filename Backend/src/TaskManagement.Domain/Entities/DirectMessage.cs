using System;

namespace TaskManagement.Domain.Entities
{
    public class DirectMessage
    {
        public Guid Id { get; set; }
        public Guid? ConversationId { get; set; }
        public DirectConversation? Conversation { get; set; }
        public Guid SenderId { get; set; }
        public User? Sender { get; set; }
        public Guid ReceiverId { get; set; }
        public User? Receiver { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public string? AttachmentUrl { get; set; }
    }
}
