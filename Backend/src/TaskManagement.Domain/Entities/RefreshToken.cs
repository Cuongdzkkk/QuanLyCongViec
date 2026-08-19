using System;

namespace TaskManagement.Domain.Entities
{
    public class RefreshToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;
        public Guid? ProjectInvitationId { get; set; }
        public ProjectInvitation? ProjectInvitation { get; set; }
        public string Token { get; set; } = string.Empty;
        public string? DeviceId { get; set; }
        public string? UserAgent { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiryTime { get; set; }
        public bool IsRevoked { get; set; } = false;
    }
}
