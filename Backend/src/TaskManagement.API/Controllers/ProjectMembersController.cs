using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using TaskManagement.API.Filters;
using TaskManagement.API.Hubs;
using TaskManagement.API.Realtime;
using TaskManagement.Application.DTOs.Project;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/members")]
    public class ProjectMembersController : ControllerBase
    {
        private readonly IProjectMemberService _projectMemberService;
        private readonly IHubContext<KanbanHub> _hub;

        public ProjectMembersController(IProjectMemberService projectMemberService, IHubContext<KanbanHub> hub)
        {
            _projectMemberService = projectMemberService;
            _hub = hub;
        }

        [HttpGet]
        [ProjectAuthorize("")]
        public async Task<IActionResult> GetProjectMembers(Guid projectId)
        {
            try
            {
                var members = await _projectMemberService.GetProjectMembersAsync(projectId);
                return Ok(new { statusCode = 200, message = "Success", data = members });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Internal server error: " + ex.Message });
            }
        }

        [HttpGet("member-candidates")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,Admin")]
        public async Task<IActionResult> GetMemberCandidates(
            Guid projectId,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var candidates = await _projectMemberService.GetProjectMemberCandidatesAsync(projectId, search, page, pageSize);
                return Ok(new { statusCode = 200, message = "Success", data = candidates, page, pageSize });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Internal server error: " + ex.Message });
            }
        }

        [HttpPost("add-existing")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,Admin")]
        public async Task<IActionResult> AddExistingMember(
            Guid projectId,
            [FromBody] AddExistingProjectMemberRequestDto request)
        {
            try
            {
                var member = await _projectMemberService.AddExistingMemberAsync(projectId, request);
                await _hub.PublishEntityChangedAsync(projectId, "project-member", "created", member.UserId, new
                {
                    userId = member.UserId,
                    role = member.ProjectRole
                });
                return Ok(new { statusCode = 200, message = "Member added successfully.", data = member });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { statusCode = 409, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Internal server error: " + ex.Message });
            }
        }

        [HttpPost]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,Admin")]
        public async Task<IActionResult> InviteMember(Guid projectId, [FromBody] ProjectMemberRequestDto request)
        {
            try
            {
                var inviterName = User.FindFirstValue(ClaimTypes.Name)
                    ?? User.FindFirstValue(ClaimTypes.Email)
                    ?? "SprintA admin";

                var inviterId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedInviterId)
                    ? parsedInviterId
                    : (Guid?)null;
                var outcome = await _projectMemberService.InviteMemberAsync(projectId, request, inviterName, inviterId);
                var message = outcome switch
                {
                    ProjectInvitationOutcome.InvitationCreated => "Invitation email sent.",
                    ProjectInvitationOutcome.InvitationAlreadyPending => "An invitation is already pending for this member.",
                    _ => "This user is already an active project member."
                };
                await _hub.PublishEntityChangedAsync(projectId, "project-member", "reconcile", projectId, new
                {
                    outcome = outcome.ToString()
                });
                return Ok(new { statusCode = 200, message = "Success", data = message, outcome = outcome.ToString() });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { statusCode = 409, message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Internal server error: " + ex.Message });
            }
        }

        [HttpDelete("{userId}")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,Admin")]
        public async Task<IActionResult> RemoveMember(Guid projectId, Guid userId)
        {
            try
            {
                var actorValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(actorValue, out var removedBy) || removedBy == Guid.Empty)
                {
                    return Unauthorized(new { statusCode = 401, message = "Authenticated removal actor is required." });
                }

                await _projectMemberService.RemoveMemberAsync(projectId, userId, removedBy);
                await _hub.PublishEntityChangedAsync(projectId, "project-member", "deleted", userId, new { userId });
                return Ok(new { statusCode = 200, message = "Success", data = "Member access revoked; assignment history was preserved." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { statusCode = 404, message = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, new { statusCode = 403, message = "You are not allowed to remove this member." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { statusCode = 500, message = "Unable to remove the project member." });
            }
        }

        [HttpPut("{userId}/role")]
        [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,Admin")]
        public async Task<IActionResult> UpdateMemberRole(Guid projectId, Guid userId, [FromBody] UpdateRoleRequestDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Role))
                {
                    return BadRequest(new { statusCode = 400, message = "Role khong de trong." });
                }

                await _projectMemberService.UpdateMemberRoleAsync(projectId, userId, request.Role);
                await _hub.PublishEntityChangedAsync(projectId, "project-member", "role-updated", userId, new
                {
                    userId,
                    role = request.Role
                });
                return Ok(new { statusCode = 200, message = "Success", data = "Cap nhat role thanh cong." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { statusCode = 400, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { statusCode = 500, message = "Internal server error: " + ex.Message });
            }
        }
    }
}
