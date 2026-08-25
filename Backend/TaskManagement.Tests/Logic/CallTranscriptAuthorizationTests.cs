using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Tests.Logic;

public sealed class CallTranscriptAuthorizationTests
{
    [Fact]
    public async Task CrossProjectTranscriptReadIsDeniedByProjectAuthorization()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorization = new Mock<ICallRoomAuthorizationService>();
        authorization.Setup(item => item.AuthorizeVoiceRoomJoinAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var controller = new CallTranscriptsController(authorization.Object, new NoopTranscriptService())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"))
                }
            }
        };

        var result = await controller.Get(projectId, "general", Guid.NewGuid());

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ForbidResult>();
    }

    [Fact]
    public async Task CrossProjectMeetingAiReportReadIsDeniedByProjectAuthorization()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorization = new Mock<ICallRoomAuthorizationService>();
        authorization.Setup(item => item.AuthorizeVoiceRoomJoinAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());
        var meetingAi = new Mock<IMeetingAiAnalysisService>();
        var controller = new CallTranscriptsController(authorization.Object, new NoopTranscriptService(), meetingAi.Object)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"))
                }
            }
        };

        var result = await controller.GetAiReport(projectId, "general", Guid.NewGuid());

        result.Result.Should().BeOfType<Microsoft.AspNetCore.Mvc.ForbidResult>();
        meetingAi.Verify(item => item.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class NoopTranscriptService : ICallTranscriptService
    {
        public Task<CallTranscriptChunkDto?> AppendAsync(CallAudioChunk source, CallTranscriptionResult result, CancellationToken cancellationToken = default) => Task.FromResult<CallTranscriptChunkDto?>(null);
        public Task<IReadOnlyList<CallTranscriptChunkDto>> GetAsync(Guid projectId, string voiceChannelId, Guid callSessionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CallTranscriptChunkDto>>([]);
    }
}
