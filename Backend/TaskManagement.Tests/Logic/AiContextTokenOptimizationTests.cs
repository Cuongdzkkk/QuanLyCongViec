using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.AI;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;
using TaskManagement.Application.Interfaces;
using DomainTaskStatus = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Tests.Logic;

public sealed class AiContextTokenOptimizationTests
{
    [Fact]
    public async Task Greeting_UsesLocalFastPathWithoutProviderOrUsage()
    {
        await using var context = CreateContext();
        var handler = new RecordingResponseHandler(ShouldNotBeCalledResponse());
        var service = CreateService(context, handler);

        var result = await service.ContextChatAsync(Guid.NewGuid(), new AiContextChatRequestDto
        {
            Message = "Xin chào"
        });

        result.Answer.Should().Contain("Xin chào");
        handler.CallCount.Should().Be(0);
        (await context.AITokenUsages.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GreetingWithOverdueTaskIntent_DoesNotUseGreetingFastPath()
    {
        await using var context = CreateContext();
        var handler = new RecordingResponseHandler(SuccessResponse());
        var service = CreateService(context, handler);

        var result = await service.ContextChatAsync(Guid.NewGuid(), new AiContextChatRequestDto
        {
            Message = "Xin chào, task nào đang quá hạn?"
        });

        result.Answer.Should().NotBe("Xin chào! Mình có thể hỗ trợ bạn với công việc, dự án và kế hoạch SprintA.");
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GenericAiIntent_UsesMinimalPromptAndCompletionCap()
    {
        await using var context = CreateContextWithUser(out var userId);
        var handler = new RecordingResponseHandler(SuccessResponse());
        var service = CreateService(context, handler);

        await service.ContextChatAsync(userId, new AiContextChatRequestDto
        {
            Message = "AI đang hoạt động bình thường, bạn có thể làm gì?"
        });

        handler.CallCount.Should().Be(1);
        handler.RequestBody.Should().NotContain("Accessible project catalog");
        handler.RequestBody.Should().NotContain("Task count in context");
        handler.RequestBody.Should().NotContain("create_task {projectId");
        handler.RequestBody.Should().Contain("\"max_completion_tokens\":800");
    }

    [Theory]
    [InlineData("Tóm tắt dashboard hiện tại")]
    [InlineData("Rủi ro nào cần xử lý trước?")]
    public async Task DashboardSurface_PreservesSprintAContext(string message)
    {
        await using var context = CreateContextWithUser(out var userId);
        var handler = new RecordingResponseHandler(SuccessResponse());
        var service = CreateService(context, handler);

        await service.ContextChatAsync(userId, new AiContextChatRequestDto
        {
            Route = "/dashboard",
            Message = message,
            PageContext = new AiContextPageDto
            {
                PageType = "dashboard",
                CurrentView = "overview"
            }
        });

        handler.CallCount.Should().Be(1);
        handler.RequestBody.Should().Contain("Route: /dashboard");
        handler.RequestBody.Should().Contain("Page type: dashboard; view: overview");
    }

    [Fact]
    public async Task ProjectContext_IsCompactedToTwentyTasks()
    {
        await using var context = CreateContextWithUser(out var userId);
        var projectId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "PROJECT WEB",
            Identifier = "WEB",
            WorkspaceId = workspaceId,
            CreatorId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Status = true
        });
        context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Status = true,
            JoinedAt = DateTime.UtcNow
        });
        context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });
        context.TaskStatuses.Add(new DomainTaskStatus { Id = statusId, ProjectId = projectId, Name = "Todo" });
        for (var index = 0; index < 25; index++)
        {
            context.WorkTasks.Add(new WorkTask
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                WorkspaceId = workspaceId,
                TaskStatusId = statusId,
                TaskTypeId = Guid.NewGuid(),
                ReporterId = userId,
                Title = $"task-{index}",
                Priority = 3,
                UpdatedAt = DateTime.UtcNow.AddMinutes(-index),
                CreatedAt = DateTime.UtcNow.AddMinutes(-index)
            });
        }
        await context.SaveChangesAsync();

        var handler = new RecordingResponseHandler(SuccessResponse());
        var service = CreateService(context, handler);
        await service.ContextChatAsync(userId, new AiContextChatRequestDto
        {
            ProjectId = projectId,
            Message = "Tóm tắt PROJECT WEB"
        });

        handler.RequestBody.Should().NotContain("Accessible project catalog");
        handler.RequestBody.Split("- id=", StringSplitOptions.None).Length.Should().BeLessThanOrEqualTo(21);
        handler.RequestBody.Should().Contain("Task count in context: 20");
    }

    [Fact]
    public async Task NamedProjectOutsideCompactCatalog_IsResolvedFromPermissionFilteredDirectory()
    {
        await using var context = CreateContextWithUser(out var userId);
        var workspaceId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        for (var index = 1; index <= 25; index++)
        {
            var projectId = Guid.NewGuid();
            context.Projects.Add(new Project
            {
                Id = projectId,
                Name = $"PROJECT-{index}",
                Identifier = $"P{index}",
                WorkspaceId = workspaceId,
                CreatorId = userId,
                CreatedAt = now,
                UpdatedAt = now.AddMinutes(-index),
                Status = true
            });
            context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Status = true,
                JoinedAt = now
            });
        }

        context.Projects.Add(new Project
        {
            Id = Guid.NewGuid(),
            Name = "SECRET-PROJECT",
            Identifier = "SECRET",
            WorkspaceId = workspaceId,
            CreatorId = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now,
            Status = true
        });
        context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            IsActive = true,
            JoinedAt = now
        });
        await context.SaveChangesAsync();

        var handler = new RecordingResponseHandler(SuccessResponse());
        var service = CreateService(context, handler);
        await service.ContextChatAsync(userId, new AiContextChatRequestDto
        {
            Message = "Tóm tắt PROJECT-25"
        });

        handler.RequestBody.Should().Contain("Project: PROJECT-25");
        handler.RequestBody.Should().NotContain("PROJECT-24");
        handler.RequestBody.Should().NotContain("SECRET-PROJECT");
        handler.RequestBody.Should().NotContain("Accessible project catalog");
    }

    [Fact]
    public async Task WriteAction_IsStillMarkedAsRequiringConfirmation()
    {
        await using var context = CreateContextWithUser(out var userId);
        var handler = new RecordingResponseHandler(ActionResponse());
        var service = CreateService(context, handler);

        var result = await service.ContextChatAsync(userId, new AiContextChatRequestDto
        {
            Message = "Tạo task cho tôi"
        });

        result.Actions.Should().ContainSingle().Which.RequiresConfirmation.Should().BeTrue();
    }

    private static GeminiAiService CreateService(ApplicationDbContext context, RecordingResponseHandler handler)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ZenMux:ApiKey"] = "test-api-key-not-a-secret",
            ["ZenMux:BaseUrl"] = "https://zenmux.test/api/v1",
            ["ZenMux:Model"] = "test-model"
        }).Build();
        return new GeminiAiService(
            context,
            new HttpClient(),
            new ZenMuxAiClient(new HttpClient(handler), configuration),
            Mock.Of<IWorkTaskService>(),
            new AiCreditUsageService(context),
            configuration);
    }

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ApplicationDbContext CreateContextWithUser(out Guid userId)
    {
        var context = CreateContext();
        userId = Guid.NewGuid();
        context.Users.Add(new User
        {
            Id = userId,
            Email = "context-test@example.com",
            FullName = "Context Test",
            PasswordHash = "unused",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.SaveChanges();
        return context;
    }

    private static HttpResponseMessage SuccessResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            "{\"choices\":[{\"message\":{\"content\":\"{\\\"answer\\\":\\\"ok\\\",\\\"suggestions\\\":[],\\\"warnings\\\":[],\\\"actions\\\":[]}\"}}],\"usage\":{\"total_tokens\":1}}",
            Encoding.UTF8,
            "application/json")
    };

    private static HttpResponseMessage ActionResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            "{\"choices\":[{\"message\":{\"content\":\"{\\\"answer\\\":\\\"ok\\\",\\\"suggestions\\\":[],\\\"warnings\\\":[],\\\"actions\\\":[{\\\"type\\\":\\\"create_task\\\",\\\"requiresConfirmation\\\":false}]}\"}}],\"usage\":{\"total_tokens\":1}}",
            Encoding.UTF8,
            "application/json")
    };

    private static HttpResponseMessage ShouldNotBeCalledResponse() => new(HttpStatusCode.InternalServerError)
    {
        Content = new StringContent("provider-secret", Encoding.UTF8, "text/plain")
    };

    private sealed class RecordingResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            RequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
