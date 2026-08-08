using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaskManagement.Application.DTOs.StarredRecent;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/recentviews")]
    [Authorize]
    public sealed class RecentViewsController : ControllerBase
    {
        private readonly IRecentViewService _recentViewService;

        public RecentViewsController(IRecentViewService recentViewService)
        {
            _recentViewService = recentViewService;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] int? limit = null)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
            if (limit.HasValue)
            {
                page = 1;
                pageSize = limit.Value;
            }

            try
            {
                var result = await _recentViewService.GetAllAsync(
                    userId,
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
        public async Task<IActionResult> Upsert([FromBody] RecentViewRequestDto request)
        {
            if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

            try
            {
                var result = await _recentViewService.RecordAsync(
                    userId,
                    request.EntityType,
                    request.EntityId);
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

        private bool TryGetCurrentUserId(out Guid userId)
        {
            return Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId);
        }
    }
}
