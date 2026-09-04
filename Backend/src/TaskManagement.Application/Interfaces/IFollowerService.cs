using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TaskManagement.Application.Interfaces
{
    public interface IFollowerService
    {
        Task<IEnumerable<object>> GetAllFollowedAsync(Guid userId, Guid workspaceId);
        Task<IEnumerable<object>> GetFollowersAsync(Guid actorUserId, Guid workspaceId, string entityType, Guid entityId);
        Task<IEnumerable<object>> AddFollowersAsync(Guid actorUserId, Guid workspaceId, string entityType, Guid entityId, IEnumerable<Guid> userIds);
        Task<object> ToggleFollowAsync(Guid userId, Guid workspaceId, string entityType, Guid entityId);
    }
}
