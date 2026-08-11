using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Application.Interfaces;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/workspaces/{workspaceId}/[controller]")]
    [Authorize]
    public class FollowersController : ControllerBase
    {
        private readonly IFollowerService _followerService;
        private readonly IHubContext<KanbanHub>? _hub;

        public sealed class AddFollowersRequest
        {
            public List<Guid> UserIds { get; set; } = new();
        }

        public FollowersController(IFollowerService followerService, IHubContext<KanbanHub>? hub = null)
        {
            _followerService = followerService;
            _hub = hub;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(Guid workspaceId)
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _followerService.GetAllFollowedAsync(userId);
            return Ok(ApiResponse<object>.Success(result));
        }

        [HttpGet("entity")]
        public async Task<IActionResult> GetEntityFollowers(Guid workspaceId, [FromQuery] string entityType, [FromQuery] Guid entityId)
        {
            var result = await _followerService.GetFollowersAsync(entityType, entityId);
            return Ok(ApiResponse<object>.Success(result));
        }

        [HttpPost("entity")]
        public async Task<IActionResult> AddEntityFollowers(Guid workspaceId, [FromQuery] string entityType, [FromQuery] Guid entityId, [FromBody] AddFollowersRequest request)
        {
            var actorUserId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _followerService.AddFollowersAsync(actorUserId, entityType, entityId, request.UserIds);
            if (_hub != null)
            {
                foreach (var userId in request.UserIds.Distinct())
                    await _hub.PublishUserEntityChangedAsync(userId, "Follower", "updated", entityId, new { entityType, entityId });
            }
            return Ok(ApiResponse<object>.Success(result));
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> ToggleFollow(Guid workspaceId, [FromQuery] string entityType, [FromQuery] Guid entityId)
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            var result = await _followerService.ToggleFollowAsync(userId, entityType, entityId);
            if (_hub != null)
                await _hub.PublishUserEntityChangedAsync(userId, "Follower", "updated", entityId, new { workspaceId, entityType, entityId, result });
            return Ok(ApiResponse<object>.Success(result));
        }
    }
}
