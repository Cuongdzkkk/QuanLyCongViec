using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.AI;
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
        context.AiPricingPlans.Add(new AiPricingPlan
        {
            Id = Guid.NewGuid(), Code = "free", Name = "Free", IncludedAiCredits = 100,
            MonthlyPriceVnd = 0, IsPublished = true, PricingStatus = "Published",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
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
    [InlineData("Bạn là ai?")]
    [InlineData("Bạn có thể làm được những gì trong dự án?")]
    [InlineData("Bạn có chức năng gì?")]
    public async Task CapabilityQuestions_ReturnGroundedVietnameseDescription(string message)
    {
        await using var context = CreateContext();
        var service = CreateService(context, new RecordingResponseHandler(ShouldNotBeCalledResponse()));

        var result = await service.ContextChatAsync(Guid.NewGuid(), new AiContextChatRequestDto { Message = message });

        result.Answer.Should().Contain("Tạo công việc");
        result.Answer.Should().Contain("Cập nhật trạng thái công việc");
        result.Answer.Should().Contain("Tóm tắt dự án");
        result.Answer.Should().Contain("xác nhận");
    }

    [Fact]
    public async Task CapabilityPrompt_UsesCanonicalRegistryAndSeparatesCapabilityKinds()
    {
        await using var context = CreateContextWithUser(out var userId);
        var handler = new RecordingResponseHandler(SuccessResponse());
        var service = CreateService(context, handler);

        await service.ContextChatAsync(userId, new AiContextChatRequestDto
        {
            Route = "/dashboard",
            PageContext = new AiContextPageDto { PageType = "dashboard", CurrentView = "Dashboard" },
            Message = "Hãy mô tả chính xác capability của SprintA AI."
        });

        foreach (var action in AiActionCatalog.Definitions)
        {
            handler.RequestBody.Should().Contain(action.Key);
        }

        handler.RequestBody.Should().Contain("READ");
        handler.RequestBody.Should().Contain("ANALYZE");
        handler.RequestBody.Should().Contain("WRITE");
        handler.RequestBody.Should().Contain("capability");
        handler.RequestBody.Should().NotContain("unsupported_action");
    }

    [Fact]
    public async Task CapabilityCatalog_ContainsOnlyExecutableHandlers()
    {
        AiActionCatalog.Definitions.Should().HaveCount(27);
        AiActionCatalog.Definitions.Select(action => action.Key)
            .Should().NotContain("unsupported_action");
        AiActionCatalog.Definitions
            .Where(action => action.Value.CapabilityKind == AiCapabilityKind.Write)
            .Should().AllSatisfy(action => action.Value.RequiresConfirmation.Should().BeTrue());
        AiActionCatalog.Definitions
            .Where(action => action.Value.CapabilityKind != AiCapabilityKind.Write)
            .Should().AllSatisfy(action => action.Value.RequiresConfirmation.Should().BeFalse());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingProviderUsage_FallbackEstimateIncludesSystemInstruction(bool includesZeroUsage)
    {
        await using var context = CreateContextWithUser(out var userId);
        var handler = new RecordingResponseHandler(ResponseWithoutUsage(includesZeroUsage));
        var service = CreateService(context, handler);
        const string message = "AI đang hoạt động bình thường, bạn có thể làm gì?";
        const string output = "{\"answer\":\"ok\",\"suggestions\":[],\"warnings\":[],\"actions\":[]}";

        await service.ContextChatAsync(userId, new AiContextChatRequestDto { Message = message });

        var usage = await context.AITokenUsages.SingleAsync();
        var promptOnlyEstimate = Math.Max(1, ($"User message: {message}".Length + output.Length) / 4);
        usage.TokensUsed.Should().BeGreaterThan(promptOnlyEstimate);
        usage.TokensUsed.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ProductionShapedDashboardMetadata_DoesNotForceGenericMessageIntoContext()
    {
        await using var context = CreateContextWithUser(out var userId);
        var handler = new RecordingResponseHandler(SuccessResponse());
        var service = CreateService(context, handler);

        await service.ContextChatAsync(userId, new AiContextChatRequestDto
        {
            Route = "/dashboard",
            WorkspaceId = Guid.NewGuid(),
            PageContext = new AiContextPageDto
            {
                PageType = "dashboard",
                CurrentView = "Dashboard",
                VisibleTaskIds = [Guid.NewGuid(), Guid.NewGuid()],
                VisibleStatuses = ["Todo", "Done"]
            },
            Message = "Viết cho tôi một lời chúc sinh nhật ngắn."
        });

        handler.CallCount.Should().Be(1);
        handler.RequestBody.Should().NotContain("Route: /dashboard");
        handler.RequestBody.Should().NotContain("Visible task ids");
        handler.RequestBody.Should().NotContain("Accessible project");
        handler.RequestBody.Should().NotContain("Task count in context");
    }

    [Theory]
    [InlineData("Tóm tắt dashboard hiện tại")]
    [InlineData("Rủi ro nào cần xử lý trước?")]
    [InlineData("Gợi ý ưu tiên hôm nay")]
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

    [Theory]
    [InlineData("Rủi ro nào cần xử lý trước?")]
    [InlineData("Tóm tắt dashboard hiện tại")]
    public async Task DashboardVisibleTasks_AreHydratedAndPermissionFiltered(string message)
    {
        await using var context = CreateContextWithUser(out var userId);
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var secretProjectId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var secretStatusId = Guid.NewGuid();
        var taskAId = Guid.NewGuid();
        var taskBId = Guid.NewGuid();
        var secretTaskId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.Projects.AddRange(
            new Project
            {
                Id = projectId,
                Name = "Dashboard Project",
                Identifier = "DASH",
                WorkspaceId = workspaceId,
                CreatorId = userId,
                CreatedAt = now,
                UpdatedAt = now,
                Status = true
            },
            new Project
            {
                Id = secretProjectId,
                Name = "SECRET PROJECT",
                Identifier = "SECRET",
                WorkspaceId = workspaceId,
                CreatorId = Guid.NewGuid(),
                CreatedAt = now,
                UpdatedAt = now,
                Status = true
            });
        context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = userId,
            Status = true,
            JoinedAt = now
        });
        context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = workspaceId,
            UserId = userId,
            IsActive = true,
            JoinedAt = now
        });
        context.TaskStatuses.AddRange(
            new DomainTaskStatus { Id = statusId, ProjectId = projectId, Name = "In Progress" },
            new DomainTaskStatus { Id = secretStatusId, ProjectId = secretProjectId, Name = "Secret" });
        context.WorkTasks.AddRange(
            new WorkTask
            {
                Id = taskAId,
                ProjectId = projectId,
                WorkspaceId = workspaceId,
                TaskStatusId = statusId,
                TaskTypeId = Guid.NewGuid(),
                ReporterId = userId,
                Title = "Accessible task A",
                Priority = 1,
                DueDate = now.Date.AddDays(1),
                CreatedAt = now,
                UpdatedAt = now
            },
            new WorkTask
            {
                Id = taskBId,
                ProjectId = projectId,
                WorkspaceId = workspaceId,
                TaskStatusId = statusId,
                TaskTypeId = Guid.NewGuid(),
                ReporterId = userId,
                Title = "Accessible task B",
                Priority = 2,
                DueDate = now.Date.AddDays(2),
                CreatedAt = now,
                UpdatedAt = now
            },
            new WorkTask
            {
                Id = secretTaskId,
                ProjectId = secretProjectId,
                WorkspaceId = workspaceId,
                TaskStatusId = secretStatusId,
                TaskTypeId = Guid.NewGuid(),
                ReporterId = Guid.NewGuid(),
                Title = "SECRET task must not leak",
                Priority = 1,
                DueDate = now.Date,
                CreatedAt = now,
                UpdatedAt = now
            });
        await context.SaveChangesAsync();

        var handler = new RecordingResponseHandler(SuccessResponse());
        var service = CreateService(context, handler);
        await service.ContextChatAsync(userId, new AiContextChatRequestDto
        {
            Route = "/dashboard",
            WorkspaceId = workspaceId,
            PageContext = new AiContextPageDto
            {
                PageType = "dashboard",
                CurrentView = "Dashboard",
                VisibleTaskIds = [taskAId, taskBId, secretTaskId],
                VisibleStatuses = ["In Progress"]
            },
            Message = message
        });

        handler.RequestBody.Should().Contain("Visible task context (trusted server data):");
        handler.RequestBody.Should().Contain("Accessible task A");
        handler.RequestBody.Should().Contain("Accessible task B");
        handler.RequestBody.Should().Contain("Dashboard Project");
        handler.RequestBody.Should().NotContain("SECRET task must not leak");
        handler.RequestBody.Split("Visible task context (trusted server data):", StringSplitOptions.None).Length.Should().Be(2);
        handler.RequestBody.Split("- [", StringSplitOptions.None).Length.Should().BeLessThanOrEqualTo(21);
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
    public async Task ExplicitProjectId_PreservesContextForGenericLookingMessage()
    {
        await using var context = CreateContextWithUser(out var userId);
        var projectId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "PROJECT EXPLICIT",
            Identifier = "EXP",
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
        await context.SaveChangesAsync();

        var handler = new RecordingResponseHandler(SuccessResponse());
        var service = CreateService(context, handler);
        await service.ContextChatAsync(userId, new AiContextChatRequestDto
        {
            ProjectId = projectId,
            Message = "Viết lời chúc sinh nhật"
        });

        handler.RequestBody.Should().Contain("Project: PROJECT EXPLICIT");
    }

    [Fact]
    public async Task SelectedText_PreservesContextForGenericLookingMessage()
    {
        await using var context = CreateContextWithUser(out var userId);
        var handler = new RecordingResponseHandler(SuccessResponse());
        var service = CreateService(context, handler);

        await service.ContextChatAsync(userId, new AiContextChatRequestDto
        {
            SelectedText = "Selected SprintA task context",
            Message = "Dịch câu này sang tiếng Anh"
        });

        handler.RequestBody.Should().Contain("Selected text (untrusted): Selected SprintA task context");
    }

    [Theory]
    [InlineData("Tóm tắt PROJECT-25")]
    [InlineData("Tóm tắt P25")]
    public async Task NamedProjectOutsideCompactCatalog_IsResolvedFromPermissionFilteredDirectory(string message)
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
            Message = message
        });

        handler.RequestBody.Should().Contain("Project: PROJECT-25");
        handler.RequestBody.Should().NotContain("PROJECT-24");
        handler.RequestBody.Should().NotContain("SECRET-PROJECT");
        handler.RequestBody.Should().NotContain("Accessible project catalog");
    }

    [Theory]
    [InlineData("Đặt priority task ABC thành P1")]
    [InlineData("Chuyển trạng thái task ABC sang Done")]
    [InlineData("Cập nhật hạn task ABC sang ngày mai")]
    public async Task MutationVerbAndTarget_AddWriteActionPolicy(string message)
    {
        await using var context = CreateContextWithUser(out var userId);
        var handler = new RecordingResponseHandler(SuccessResponse());
        var service = CreateService(context, handler);

        await service.ContextChatAsync(userId, new AiContextChatRequestDto
        {
            Route = "/dashboard",
            PageContext = new AiContextPageDto { PageType = "dashboard", CurrentView = "Dashboard" },
            Message = message
        });

        handler.RequestBody.Should().Contain("Write whitelist:");
        handler.RequestBody.Should().Contain("requiresConfirmation=true");
    }

    [Fact]
    public async Task ReadOnlyPrioritySummary_DoesNotAddWriteActionPolicy()
    {
        await using var context = CreateContextWithUser(out var userId);
        var handler = new RecordingResponseHandler(SuccessResponse());
        var service = CreateService(context, handler);

        await service.ContextChatAsync(userId, new AiContextChatRequestDto
        {
            Route = "/dashboard",
            PageContext = new AiContextPageDto { PageType = "dashboard", CurrentView = "Dashboard" },
            Message = "Tóm tắt priority của project"
        });

        handler.RequestBody.Should().NotContain("Write whitelist:");
        handler.RequestBody.Should().NotContain("requiresConfirmation=true");
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
        context.AiPricingPlans.Add(new AiPricingPlan
        {
            Id = Guid.NewGuid(), Code = "free", Name = "Free", IncludedAiCredits = 100,
            MonthlyPriceVnd = 0, IsPublished = true, PricingStatus = "Published",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
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

    private static HttpResponseMessage ResponseWithoutUsage(bool includesZeroUsage) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            $"{{\"choices\":[{{\"message\":{{\"content\":\"{{\\\"answer\\\":\\\"ok\\\",\\\"suggestions\\\":[],\\\"warnings\\\":[],\\\"actions\\\":[]}}\"}}}}]" +
            (includesZeroUsage ? ",\"usage\":{\"total_tokens\":0}" : string.Empty) + "}",
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
