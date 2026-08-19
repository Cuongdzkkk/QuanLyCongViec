using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using TaskManagement.API.Hubs;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;

namespace TaskManagement.API.Services
{
    public class SignalRClientNotifier : ISignalRClientNotifier
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public SignalRClientNotifier(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendNotificationAsync(Guid userId, Notification notification)
        {
            await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", notification);
        }

        public async Task SendNotificationUpdatedAsync(Guid userId, Notification notification)
        {
            await _hubContext.Clients.Group($"user_{userId}").SendAsync("NotificationUpdated", notification);
        }
    }
}
