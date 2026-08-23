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

    private sealed class NoopTranscriptService : ICallTranscriptService
    {
        public Task<CallTranscriptChunkDto?> AppendAsync(CallAudioChunk source, CallTranscriptionResult result, CancellationToken cancellationToken = default) => Task.FromResult<CallTranscriptChunkDto?>(null);
        public Task<IReadOnlyList<CallTranscriptChunkDto>> GetAsync(Guid projectId, string voiceChannelId, Guid callSessionId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CallTranscriptChunkDto>>([]);
    }
}
