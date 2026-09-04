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
            if (!TryGetUserId(out var userId)) return Unauthorized();
            try
            {
                var result = await _followerService.GetAllFollowedAsync(userId, workspaceId);
                return Ok(ApiResponse<object>.Success(result));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("entity")]
        public async Task<IActionResult> GetEntityFollowers(Guid workspaceId, [FromQuery] string entityType, [FromQuery] Guid entityId)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            try
            {
                var result = await _followerService.GetFollowersAsync(userId, workspaceId, entityType, entityId);
                return Ok(ApiResponse<object>.Success(result));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost("entity")]
        public async Task<IActionResult> AddEntityFollowers(Guid workspaceId, [FromQuery] string entityType, [FromQuery] Guid entityId, [FromBody] AddFollowersRequest request)
        {
            if (!TryGetUserId(out var actorUserId)) return Unauthorized();
            try
            {
                var result = await _followerService.AddFollowersAsync(actorUserId, workspaceId, entityType, entityId, request.UserIds);
                if (_hub != null)
                {
                    foreach (var userId in request.UserIds.Distinct())
                        await _hub.PublishUserEntityChangedAsync(userId, "Follower", "updated", entityId, new { entityType, entityId });
                }
                return Ok(ApiResponse<object>.Success(result));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> ToggleFollow(Guid workspaceId, [FromQuery] string entityType, [FromQuery] Guid entityId)
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            try
            {
                var result = await _followerService.ToggleFollowAsync(userId, workspaceId, entityType, entityId);
                if (_hub != null)
                    await _hub.PublishUserEntityChangedAsync(userId, "Follower", "updated", entityId, new { workspaceId, entityType, entityId, result });
                return Ok(ApiResponse<object>.Success(result));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        private bool TryGetUserId(out Guid userId)
        {
            return Guid.TryParse(
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                out userId) && userId != Guid.Empty;
        }
    }
}
