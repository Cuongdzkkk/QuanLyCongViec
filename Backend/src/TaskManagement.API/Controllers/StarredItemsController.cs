using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaskManagement.Application.DTOs.StarredRecent;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/workspaces/{workspaceId}/[controller]")]
    [Authorize]
    public class StarredItemsController : ControllerBase
    {
        private readonly IStarredItemService _starredItemService;

        public StarredItemsController(IStarredItemService starredItemService)
        {
            _starredItemService = starredItemService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            Guid workspaceId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            try
            {
                var result = await _starredItemService.GetAllAsync(
                    userId,
                    workspaceId,
                    page,
                    pageSize);
                return Ok(new
                {
                    statusCode = 200,
                    message = "Success",
                    data = result.Items,
                    pagination = new
                    {
                        result.TotalCount,
                        result.Page,
                        result.PageSize
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    statusCode = 403,
                    message = ex.Message
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Star(
            Guid workspaceId,
            [FromBody] StarredItemRequestDto request)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
            return await ExecuteMutation(() => _starredItemService.StarAsync(
                userId,
                workspaceId,
                request.ItemType,
                request.ItemId));
        }

        [HttpDelete("{itemType}/{itemId:guid}")]
        public async Task<IActionResult> Unstar(
            Guid workspaceId,
            string itemType,
            Guid itemId)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
            return await ExecuteMutation(() => _starredItemService.UnstarAsync(
                userId,
                workspaceId,
                itemType,
                itemId));
        }

        // Compatibility endpoint for the existing frontend. New callers should use
        // the idempotent POST and DELETE endpoints above.
        [HttpPost("toggle")]
        public async Task<IActionResult> ToggleStar(Guid workspaceId, [FromQuery] string itemType, [FromQuery] Guid itemId)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
            return await ExecuteMutation(() => _starredItemService.ToggleStarAsync(
                userId,
                workspaceId,
                itemType,
                itemId));
        }

        private bool TryGetCurrentUserId(out Guid userId)
        {
            return Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId);
        }

        private async Task<IActionResult> ExecuteMutation(
            Func<Task<StarredItemMutationDto>> mutation)
        {
            try
            {
                var result = await mutation();
                return Ok(new
                {
                    statusCode = 200,
                    message = "Success",
                    data = result
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    statusCode = 403,
                    message = ex.Message
                });
            }
        }
    }
}
