using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.API.Hubs;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Tests.Logic;

public sealed class ProjectMemberControllerTests
{
    [Fact]
    public async Task RemoveMember_UnknownMembershipReturnsNotFoundWithoutInternalDetails()
    {
        var service = new Mock<IProjectMemberService>();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        service.Setup(item => item.RemoveMemberAsync(projectId, userId, actorId, null))
            .ThrowsAsync(new KeyNotFoundException("Member does not exist or has already left the project."));

        var controller = CreateController(service.Object, actorId);
        var result = await controller.RemoveMember(projectId, userId);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        notFound.Value.Should().NotBeNull();
        notFound.Value!.ToString().Should().NotContain("SqlServer");
    }

    [Fact]
    public async Task RemoveMember_UnexpectedFailureReturnsSafeServerError()
    {
        var service = new Mock<IProjectMemberService>();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        service.Setup(item => item.RemoveMemberAsync(projectId, userId, actorId, null))
            .ThrowsAsync(new InvalidOperationException("SqlServerRetryingExecutionStrategy internal detail"));

        var controller = CreateController(service.Object, actorId);
        var result = await controller.RemoveMember(projectId, userId);

        var serverError = result.Should().BeOfType<ObjectResult>().Subject;
        serverError.StatusCode.Should().Be(500);
        var payload = JsonSerializer.Serialize(serverError.Value);
        payload.Should().Contain("Unable to remove the project member.");
        payload.Should().NotContain("SqlServerRetryingExecutionStrategy");
    }

    private static ProjectMembersController CreateController(
        IProjectMemberService service,
        Guid actorId)
    {
        var controller = new ProjectMembersController(
            service,
            new Mock<IHubContext<KanbanHub>>().Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, actorId.ToString())],
                        "Test"))
                }
            }
        };
        return controller;
    }
}
