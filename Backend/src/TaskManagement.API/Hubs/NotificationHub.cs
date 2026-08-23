using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TaskManagement.API.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time notification push.
    /// Each user joins a personal group "user_{userId}" on connect.
    /// </summary>
    [Authorize]
    public class NotificationHub : Hub
    {
        public const string Route = "/notification-hub";

        private static string GetUserGroup(Guid userId) => $"user_{userId}";

        public override async Task OnConnectedAsync()
        {
            var claim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(claim, out var userId))
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId));
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Client calls this after connecting to subscribe to their personal notification channel
        /// </summary>
        public async Task JoinUserChannel()
        {
            var claim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(claim, out var userId))
                await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId));
        }

        public async Task LeaveUserChannel()
        {
            var claim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(claim, out var userId))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetUserGroup(userId));
        }
    }
}
