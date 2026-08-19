using System;
using System.Threading.Tasks;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Application.Interfaces
{
    public interface ISignalRClientNotifier
    {
        Task SendNotificationAsync(Guid userId, Notification notification);
        Task SendNotificationUpdatedAsync(Guid userId, Notification notification);
    }
}
