using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.AI;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class AiChannelAnalysisServiceTests
{
    [Fact]
    public async Task AnalysisIsTextOnlyStructuredAndEvidenceIsServerValidated()
    {
        await using var context = CreateContext();
        var fixture = await SeedAsync(context);
        var validMessage = fixture.Messages[0];
        var handler = new RecordingHandler(JsonResponse($$"""
            {"summary":"Đã chốt hướng triển khai.","decisions":[{"text":"Dùng phương án A.","evidenceRefs":["M1","M999"],"evidenceMessageIds":["{{validMessage.Id}}"],"evidenceTimestamp":"not-a-timestamp"}],"actionItems":[{"text":"Chuẩn bị bản thử nghiệm.","assigneeCandidate":"Không tồn tại","deadlineCandidate":"Ngày mai","confidence":0.8,"evidenceRefs":["M1"]}],"openQuestions":[{"text":"Cần xác nhận ngân sách.","evidenceRefs":["M1"]}],"importantPoints":["Có rủi ro tiến độ."]}
            """));
        var service = CreateService(context, fixture.UserId, handler);

        var result = await service.AnalyzeAsync(fixture.UserId, fixture.ChannelId, new AiChannelAnalysisRequestDto
        {
            RequestId = "analysis-1"
        });

        result.Scope.Should().Be("text-channel");
        result.Decisions.Single().EvidenceMessageIds.Should().BeEquivalentTo([validMessage.Id]);
        result.ActionItems.Single().AssigneeCandidate.Should().BeNull();
        result.ActionItems.Single().DeadlineCandidate.Should().BeNull();
        result.QuestionAnswer.Should().BeNull();
        handler.LastBody.Should().Contain("untrusted-source");
        handler.LastBody.Should().NotContain("audioBytes");
        (await context.AITokenUsages.CountAsync()).Should().Be(1);
        (await context.AiCreditReservations.SingleAsync()).Status.Should().Be("Finalized");
    }

    [Fact]
    public async Task CrossChannelMessageScopeIsRejectedBeforeProviderCall()
    {
        await using var context = CreateContext();
        var fixture = await SeedAsync(context);
        var foreignMessage = new ChannelMessage
        {
            Id = Guid.NewGuid(),
            CollaborationChannelId = Guid.NewGuid(),
            SenderId = fixture.UserId,
            Content = "private",
            SentAt = DateTime.UtcNow
        };
        context.ChannelMessages.Add(foreignMessage);
        await context.SaveChangesAsync();
        var handler = new RecordingHandler(JsonResponse("{}"));
        var service = CreateService(context, fixture.UserId, handler);

        await service.Invoking(item => item.AnalyzeAsync(fixture.UserId, fixture.ChannelId, new AiChannelAnalysisRequestDto
        {
            RequestId = "cross-channel",
            MessageIds = [foreignMessage.Id]
        })).Should().ThrowAsync<ArgumentException>();

        handler.CallCount.Should().Be(0);
        (await context.AiCreditReservations.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UnsupportedQuestionDoesNotInventAnswerAndRetryKeyIsCached()
    {
        await using var context = CreateContext();
        var fixture = await SeedAsync(context);
        var handler = new RecordingHandler(JsonResponse("""
            {"summary":"","decisions":[],"actionItems":[],"openQuestions":[],"importantPoints":[],"questionAnswer":{"answer":"Không biết","unsupported":false,"evidenceRefs":[]}}
            """));
        var service = CreateService(context, fixture.UserId, handler);
        var request = new AiChannelAnalysisRequestDto { RequestId = "question-1", Question = "Mật khẩu production là gì?" };

        var first = await service.AnalyzeAsync(fixture.UserId, fixture.ChannelId, request);
        var second = await service.AnalyzeAsync(fixture.UserId, fixture.ChannelId, request);

        first.QuestionAnswer!.Unsupported.Should().BeTrue();
        first.QuestionAnswer.Answer.Should().Contain("Không đủ thông tin");
        second.QuestionAnswer!.Unsupported.Should().BeTrue();
        handler.CallCount.Should().Be(1);
        (await context.AiCreditReservations.CountAsync()).Should().Be(1);

        var postRestartHandler = new RecordingHandler(JsonResponse("{}"));
        var postRestartService = CreateService(context, fixture.UserId, postRestartHandler);
        await postRestartService.Invoking(item => item.AnalyzeAsync(fixture.UserId, fixture.ChannelId, request))
            .Should().ThrowAsync<AiChannelRequestAlreadyCompletedException>();
        postRestartHandler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ProviderFailureReleasesReservation()
    {
        await using var context = CreateContext();
        var fixture = await SeedAsync(context);
        var handler = new RecordingHandler("", HttpStatusCode.BadGateway);
        var service = CreateService(context, fixture.UserId, handler);

        await service.Invoking(item => item.AnalyzeAsync(fixture.UserId, fixture.ChannelId, new AiChannelAnalysisRequestDto
        {
            RequestId = "provider-failure"
        })).Should().ThrowAsync<AiProviderException>();

        (await context.AiCreditReservations.SingleAsync()).Status.Should().Be("Released");
        (await context.AITokenUsages.CountAsync()).Should().Be(0);

        var retryHandler = new RecordingHandler(JsonResponse("{}"));
        var retryService = CreateService(context, fixture.UserId, retryHandler);
        await retryService.AnalyzeAsync(fixture.UserId, fixture.ChannelId, new AiChannelAnalysisRequestDto
        {
            RequestId = "provider-failure"
        });
        retryHandler.CallCount.Should().Be(1);
        (await context.AiCreditReservations.SingleAsync()).Status.Should().Be("Finalized");
    }

    [Fact]
    public async Task EmptyProviderResponseIsUnavailableAndReleasesReservation()
    {
        await using var context = CreateContext();
        var fixture = await SeedAsync(context);
        var handler = new RecordingHandler(JsonResponse(string.Empty));
        var service = CreateService(context, fixture.UserId, handler);

        await service.Invoking(item => item.AnalyzeAsync(fixture.UserId, fixture.ChannelId, new AiChannelAnalysisRequestDto
        {
            RequestId = "empty-provider-response"
        })).Should().ThrowAsync<AiProviderException>();

        (await context.AiCreditReservations.SingleAsync()).Status.Should().Be("Released");
        (await context.AITokenUsages.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not-json")]
    public async Task MalformedOrWhitespaceProviderResponseIsUnavailableAndReleasesReservation(string content)
    {
        await using var context = CreateContext();
        var fixture = await SeedAsync(context);
        var handler = new RecordingHandler(JsonResponse(content));
        var service = CreateService(context, fixture.UserId, handler);

        await service.Invoking(item => item.AnalyzeAsync(fixture.UserId, fixture.ChannelId, new AiChannelAnalysisRequestDto
        {
            RequestId = "invalid-provider-response-" + content.Length
        })).Should().ThrowAsync<AiProviderException>();

        (await context.AiCreditReservations.SingleAsync()).Status.Should().Be("Released");
        (await context.AITokenUsages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ProviderRefusalIsUnavailableAndReleasesReservation()
    {
        await using var context = CreateContext();
        var fixture = await SeedAsync(context);
        var refusal = "{\"choices\":[{\"message\":{\"content\":\"\",\"refusal\":\"safety\"},\"finish_reason\":\"stop\"}]}";
        var service = CreateService(context, fixture.UserId, new RecordingHandler(refusal));

        await service.Invoking(item => item.AnalyzeAsync(fixture.UserId, fixture.ChannelId, new AiChannelAnalysisRequestDto
        {
            RequestId = "provider-refusal"
        })).Should().ThrowAsync<AiProviderException>();

        (await context.AiCreditReservations.SingleAsync()).Status.Should().Be("Released");
    }

    private static AiChannelAnalysisService CreateService(
        ApplicationDbContext context,
        Guid userId,
        RecordingHandler handler)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ZenMux:ApiKey"] = "test-key",
            ["ZenMux:BaseUrl"] = "https://test.local/api/v1",
            ["ZenMux:Model"] = "test-model"
        }).Build();
        var authorization = new Mock<IResourceAuthorizationService>();
        authorization.Setup(item => item.AuthorizeWorkspaceAsync(userId, It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(new ResourceAuthorizationResult(true));
        authorization.Setup(item => item.AuthorizeProjectAsync(userId, It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(new ResourceAuthorizationResult(true));
        return new AiChannelAnalysisService(
            context,
            authorization.Object,
            new ZenMuxAiClient(new HttpClient(handler), configuration),
            new AiCreditUsageService(context),
            configuration,
            new MemoryCache(new MemoryCacheOptions()));
    }

    private static async Task<Fixture> SeedAsync(ApplicationDbContext context)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "member@test.local", FullName = "Member One",
            PasswordHash = "test", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(), Name = "Workspace", Slug = "workspace", OwnerId = user.Id,
            Owner = user, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var project = new Project
        {
            Id = Guid.NewGuid(), Name = "Project", Identifier = "PRJ1", WorkspaceId = workspace.Id,
            Workspace = workspace, CreatorId = user.Id, Creator = user, Status = true,
            StartDate = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var channel = new CollaborationChannel
        {
            Id = Guid.NewGuid(), Name = "general", WorkspaceId = workspace.Id, Workspace = workspace,
            ProjectId = project.Id, Project = project, CreatedByUserId = user.Id, CreatedByUser = user,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.AddRange(user, workspace, project, channel, new CollaborationChannelMember
        {
            ChannelId = channel.Id, Channel = channel, UserId = user.Id, User = user,
            JoinedAt = DateTime.UtcNow, IsActive = true
        });
        var messages = new[]
        {
            new ChannelMessage { Id = Guid.NewGuid(), CollaborationChannelId = channel.Id, CollaborationChannel = channel, SenderId = user.Id, Sender = user, Content = "Đã chốt phương án A, chuẩn bị bản thử nghiệm.", SentAt = DateTime.UtcNow.AddMinutes(-2) },
            new ChannelMessage { Id = Guid.NewGuid(), CollaborationChannelId = channel.Id, CollaborationChannel = channel, SenderId = user.Id, Sender = user, Content = "Cần xác nhận ngân sách trước khi bắt đầu.", SentAt = DateTime.UtcNow.AddMinutes(-1) }
        };
        context.ChannelMessages.AddRange(messages);
        context.AiPricingPlans.Add(new AiPricingPlan
        {
            Id = Guid.NewGuid(), Code = "free", Name = "Free", IncludedAiCredits = 10,
            IsPublished = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return new Fixture(user.Id, channel.Id, messages);
    }

    private static ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static string JsonResponse(string json)
        => "{\"choices\":[{\"message\":{\"content\":" + JsonSerializer.Serialize(json) + "}}],\"usage\":{\"total_tokens\":120}}";

    private sealed record Fixture(Guid UserId, Guid ChannelId, IReadOnlyList<ChannelMessage> Messages);

    private sealed class RecordingHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastBody = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
