using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using TaskManagement.Application.Interfaces;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/workspaces/{workspaceId}/projects/{projectId}/links")]
    [Authorize]
    public class ProjectLinksController : ControllerBase
    {
        private readonly IProjectLinkService _projectLinkService;
        private readonly IHubContext<KanbanHub>? _hub;

        public ProjectLinksController(IProjectLinkService projectLinkService, IHubContext<KanbanHub>? hub = null)
        {
            _projectLinkService = projectLinkService;
            _hub = hub;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(Guid workspaceId, Guid projectId)
        {
            var result = await _projectLinkService.GetAllLinksAsync(projectId);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Guid workspaceId, Guid projectId, [FromBody] object dto)
        {
            var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            try
            {
                var result = await _projectLinkService.CreateLinkAsync(userId, projectId, dto);
                if (_hub != null)
                    await _hub.PublishEntityChangedAsync(projectId, "ProjectLink", "created", projectId, result);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid workspaceId, Guid projectId, Guid id)
        {
            await _projectLinkService.DeleteLinkAsync(id);
            if (_hub != null)
                await _hub.PublishEntityChangedAsync(projectId, "ProjectLink", "deleted", id);
            return NoContent();
        }
    }
}
