using System;
using System.Threading.Tasks;

namespace TaskManagement.Application.Interfaces
{
    public interface INotificationService
    {
        Task SendNotificationAsync(Guid userId, string title, string content, string type, string? linkUrl = null, Guid? triggeredByUserId = null);
        Task<bool> SendNotificationOnceAsync(Guid userId, string title, string content, string type, string dedupeKey, string? linkUrl = null, Guid? triggeredByUserId = null);
        Task SendNotificationToTeamAsync(Guid teamId, string title, string content, string type, string? linkUrl = null, Guid? triggeredByUserId = null);
        Task MarkAsReadAsync(Guid notificationId, Guid userId);
    }
}
