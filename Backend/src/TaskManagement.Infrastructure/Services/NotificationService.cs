using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ISignalRClientNotifier _clientNotifier;

        public NotificationService(ApplicationDbContext context, ISignalRClientNotifier clientNotifier)
        {
            _context = context;
            _clientNotifier = clientNotifier;
        }

        public async Task SendNotificationAsync(Guid userId, string title, string content, string type, string? linkUrl = null, Guid? triggeredByUserId = null, Guid? relatedSiteAccountLinkRequestId = null)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Content = content,
                NotificationType = type,
                LinkUrl = linkUrl,
                TriggeredByUserId = triggeredByUserId,
                RelatedSiteAccountLinkRequestId = relatedSiteAccountLinkRequestId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            // Push real-time notification
            await _clientNotifier.SendNotificationAsync(userId, notification);
        }

        public async Task<bool> SendNotificationOnceAsync(Guid userId, string title, string content, string type, string dedupeKey, string? linkUrl = null, Guid? triggeredByUserId = null)
        {
            if (string.IsNullOrWhiteSpace(dedupeKey)) throw new ArgumentException("Notification dedupe key is required.", nameof(dedupeKey));
            if (await _context.Notifications.AnyAsync(x => x.DedupeKey == dedupeKey)) return false;

            var notification = new Notification
            {
                Id = Guid.NewGuid(), UserId = userId, Title = title, Content = content,
                NotificationType = type, LinkUrl = linkUrl, DedupeKey = dedupeKey,
                TriggeredByUserId = triggeredByUserId, CreatedAt = DateTime.UtcNow, IsRead = false
            };
            _context.Notifications.Add(notification);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent webhook/order transition already persisted this notification.
                _context.Entry(notification).State = EntityState.Detached;
                return false;
            }

            await _clientNotifier.SendNotificationAsync(userId, notification);
            return true;
        }

        public async Task SendNotificationToTeamAsync(Guid teamId, string title, string content, string type, string? linkUrl = null, Guid? triggeredByUserId = null)
        {
            var memberIds = await _context.DepartmentMembers
                .Where(dm => dm.DepartmentId == teamId)
                .Select(dm => dm.UserId)
                .ToListAsync();

            var notifications = memberIds.Select(userId => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Content = content,
                NotificationType = type,
                LinkUrl = linkUrl,
                TriggeredByUserId = triggeredByUserId,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            foreach (var notification in notifications)
            {
                await _clientNotifier.SendNotificationAsync(notification.UserId, notification);
            }
        }

        public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
        {
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }
    }
}
