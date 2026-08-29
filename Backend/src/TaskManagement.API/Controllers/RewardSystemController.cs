using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.API.Filters;
using TaskManagement.Application.DTOs.Rewards;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/rewards")]
[ProjectAuthorize("")]
public sealed class RewardSystemController : ControllerBase
{
    private readonly IRewardSystemService _service;
    public RewardSystemController(IRewardSystemService service) => _service = service;

    [HttpGet("dashboard")]
    public Task<RewardDashboardDto> Dashboard(Guid projectId) => _service.GetDashboardAsync(projectId, CurrentUser());

    [HttpGet("seasons")]
    public Task<IReadOnlyList<RewardSeasonDto>> Seasons(Guid projectId) => _service.GetSeasonsAsync(projectId, CurrentUser());

    [HttpPost("seasons")]
    [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,PROJECT_ADMIN,Admin")]
    public Task<RewardSeasonDto> CreateSeason(Guid projectId, [FromBody] CreateRewardSeasonRequest request) => _service.CreateSeasonAsync(projectId, CurrentUser(), request);

    [HttpPost("seasons/{seasonId:guid}/activate")]
    [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,PROJECT_ADMIN,Admin")]
    public Task<RewardSeasonDto> ActivateSeason(Guid projectId, Guid seasonId) => _service.ActivateSeasonAsync(projectId, seasonId, CurrentUser());

    [HttpPost("seasons/{seasonId:guid}/close")]
    [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,PROJECT_ADMIN,Admin")]
    public Task<RewardSeasonDto> CloseSeason(Guid projectId, Guid seasonId) => _service.CloseSeasonAsync(projectId, seasonId, CurrentUser());

    [HttpPost("seasons/{seasonId:guid}/definitions")]
    [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,PROJECT_ADMIN,Admin")]
    public Task<RewardDefinitionDto> CreateDefinition(Guid projectId, Guid seasonId, [FromBody] CreateRewardDefinitionRequest request) => _service.CreateDefinitionAsync(projectId, seasonId, CurrentUser(), request);

    [HttpPost("events/{eventId:guid}/review")]
    [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,PROJECT_ADMIN,Admin")]
    public Task<RewardPointEventDto> ReviewEvent(Guid projectId, Guid eventId, [FromBody] ReviewRewardEventRequest request) => _service.ReviewPointEventAsync(projectId, eventId, CurrentUser(), request.Approve, request.Reason);

    [HttpPost("seasons/{seasonId:guid}/settle")]
    [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,PROJECT_ADMIN,Admin")]
    public async Task<IActionResult> Settle(Guid projectId, Guid seasonId)
    {
        await _service.SettleSeasonAsync(projectId, seasonId, CurrentUser());
        return Ok(new { statusCode = 200, message = "Reward season settled." });
    }

    [HttpPost("grants/{grantId:guid}/fulfill")]
    [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,PROJECT_ADMIN,Admin")]
    public Task<RewardGrantDto> Fulfill(Guid projectId, Guid grantId) => _service.FulfillGrantAsync(projectId, grantId, CurrentUser());

    [HttpPost("grants/{grantId:guid}/resolve")]
    [ProjectAuthorize("PROJECT_MANAGER,PROJECT_LEAD,PM,PO,SM,PROJECT_ADMIN,Admin")]
    public Task<RewardGrantDto> Resolve(Guid projectId, Guid grantId, [FromBody] ResolveRewardGrantRequest request) => _service.ResolveGrantAsync(projectId, grantId, CurrentUser(), request.Award, request.Note);

    private Guid CurrentUser()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException("Authentication is required.");
    }

    public sealed record ReviewRewardEventRequest(bool Approve, string? Reason);
    public sealed record ResolveRewardGrantRequest(bool Award, string? Note);
}
