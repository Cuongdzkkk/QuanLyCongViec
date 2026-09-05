using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.AI;
using TaskManagement.Application.DTOs.Project;
using TaskManagement.Application.DTOs.WorkTask;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class AiP0AReadActionTests
{
    private static readonly IReadOnlyDictionary<string, AiCapabilityKind> P0AActions =
        new Dictionary<string, AiCapabilityKind>(StringComparer.Ordinal)
        {
            ["get_task_details"] = AiCapabilityKind.Read,
            ["search_tasks"] = AiCapabilityKind.Read,
            ["list_task_comments"] = AiCapabilityKind.Read,
            ["list_task_dependencies"] = AiCapabilityKind.Read,
            ["list_project_labels"] = AiCapabilityKind.Read,
            ["list_task_custom_fields"] = AiCapabilityKind.Read,
            ["list_project_members"] = AiCapabilityKind.Read,
            ["get_goal_details"] = AiCapabilityKind.Read,
            ["get_personal_work_summary"] = AiCapabilityKind.Analyze,
            ["get_planning_summary"] = AiCapabilityKind.Analyze
        };

    [Fact]
    public void P0A_CatalogContainsExactlyTenNewReadOnlyActions()
    {
        AiActionCatalog.Definitions.Should().HaveCount(37);
        AiActionCatalog.Definitions.Keys.Should().OnlyHaveUniqueItems();

        foreach (var expected in P0AActions)
        {
            var definition = AiActionCatalog.Definitions.Should().ContainKey(expected.Key).WhoseValue;
            definition.CapabilityKind.Should().Be(expected.Value);
            definition.RequiresConfirmation.Should().BeFalse();
            definition.DirectExecution.Should().BeTrue();
        }

        AiActionCatalog.Definitions
            .Where(action => !P0AActions.ContainsKey(action.Key))
            .Should().AllSatisfy(action => action.Value.DirectExecution.Should().BeFalse());
    }

    [Fact]
    public void CatalogActions_AllHaveConventionMatchedHandlers()
    {
        var handlerNames = typeof(AiController)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(method => method.Name.StartsWith("Execute", StringComparison.Ordinal) &&
                             method.Name.EndsWith("Async", StringComparison.Ordinal))
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var actionId in AiActionCatalog.Definitions.Keys)
        {
            handlerNames.Should().Contain(ToHandlerName(actionId), $"{actionId} must have a real dispatcher handler");
        }
    }

    public static IEnumerable<object[]> AllP0AActions() => P0AActions.Keys.Select(action => new object[] { action });

    public static IEnumerable<object[]> ProjectScopedP0AActions() => P0AActions.Keys
        .Where(action => action is not "get_goal_details" and not "get_personal_work_summary")
        .Select(action => new object[] { action });

    [Theory]
    [MemberData(nameof(AllP0AActions))]
    public async Task P0A_AuthorizedUser_ReturnsDataWithoutMutation(string actionType)
    {
        await using var fixture = new ReadActionFixture();
        var controller = fixture.CreateController(fixture.AuthorizedUserId);

        var result = await controller.ExecuteAction(fixture.Request(actionType));

        result.Should().BeOfType<OkObjectResult>();
        (await fixture.Context.AiActionExecutions.CountAsync()).Should().Be(0);
        (await fixture.Context.SystemAuditLogs.CountAsync()).Should().Be(0);
        fixture.Context.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(AllP0AActions))]
    public async Task P0A_PreviewRoute_IsRejectedWithoutCreatingPendingState(string actionType)
    {
        await using var fixture = new ReadActionFixture();
        var controller = fixture.CreateController(fixture.AuthorizedUserId);

        var result = await controller.PreviewAction(fixture.Request(actionType));

        result.Should().BeOfType<BadRequestObjectResult>();
        (await fixture.Context.AiActionExecutions.CountAsync()).Should().Be(0);
        (await fixture.Context.SystemAuditLogs.CountAsync()).Should().Be(0);
        fixture.Context.ChangeTracker.HasChanges().Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(AllP0AActions))]
    public async Task P0A_UserWithoutWorkspaceAccess_IsRejected(string actionType)
    {
        await using var fixture = new ReadActionFixture();
        var controller = fixture.CreateController(fixture.OutsiderUserId);

        var result = await controller.ExecuteAction(fixture.Request(actionType));

        AssertForbidden(result);
    }

    [Theory]
    [MemberData(nameof(AllP0AActions))]
    public async Task P0A_CrossWorkspaceTarget_IsRejected(string actionType)
    {
        await using var fixture = new ReadActionFixture();
        var controller = fixture.CreateController(fixture.AuthorizedUserId);

        var result = await controller.ExecuteAction(fixture.CrossWorkspaceRequest(actionType));

        AssertForbidden(result);
    }

    [Theory]
    [MemberData(nameof(ProjectScopedP0AActions))]
    public async Task P0A_CrossProjectTarget_IsRejected(string actionType)
    {
        await using var fixture = new ReadActionFixture();
        var controller = fixture.CreateController(fixture.AuthorizedUserId);

        var result = await controller.ExecuteAction(fixture.CrossProjectRequest(actionType));

        AssertForbidden(result);
    }

    [Fact]
    public async Task SearchTasks_IsProjectScopedAndCappedAtTwentyFive()
    {
        await using var fixture = new ReadActionFixture(searchResultCount: 40, includeCrossProjectSearchResult: true);
        var controller = fixture.CreateController(fixture.AuthorizedUserId);

        var result = await controller.ExecuteAction(fixture.Request("search_tasks", new Dictionary<string, object?>
        {
            ["projectId"] = fixture.ProjectId,
            ["query"] = "task",
            ["maxResults"] = 100
        }));

        var json = SerializeOkResult(result);
        json.Should().NotContain("CROSS-PROJECT-SECRET");
        CountArrayItems(json, "entity").Should().BeLessThanOrEqualTo(25);
    }

    [Fact]
    public async Task PersonalWorkSummary_UsesOnlyAuthenticatedUserServiceScope()
    {
        await using var fixture = new ReadActionFixture();
        var controller = fixture.CreateController(fixture.AuthorizedUserId);

        var result = await controller.ExecuteAction(fixture.Request("get_personal_work_summary"));

        SerializeOkResult(result).Should().Contain("assigned");
        fixture.WorkTaskService.Verify(service => service.GetPersonalWorkSummaryAsync(fixture.AuthorizedUserId), Times.Once);
        fixture.WorkTaskService.Verify(service => service.GetPersonalWorkSummaryAsync(It.Is<Guid>(id => id != fixture.AuthorizedUserId)), Times.Never);
    }

    [Fact]
    public async Task TaskDependencies_DoNotRevealPrivateRelatedTasksInTheSameProject()
    {
        await using var fixture = new ReadActionFixture();
        var controller = fixture.CreateController(fixture.AuthorizedUserId);

        var result = await controller.ExecuteAction(fixture.Request("list_task_dependencies"));

        SerializeOkResult(result).Should().NotContain("PRIVATE-RELATED-TASK");
    }

    [Fact]
    public async Task ExistingReadAction_PreservesItsPreviewTransportContract()
    {
        await using var fixture = new ReadActionFixture();
        var controller = fixture.CreateController(fixture.AuthorizedUserId);
        var request = new AiExecuteActionRequestDto
        {
            Type = "summarize_dashboard",
            WorkspaceId = fixture.WorkspaceId
        };

        (await controller.ExecuteAction(request)).Should().BeOfType<BadRequestObjectResult>();
        (await controller.PreviewAction(request)).Should().BeOfType<OkObjectResult>();
        (await fixture.Context.AiActionExecutions.CountAsync()).Should().Be(1);
    }

    private static void AssertForbidden(IActionResult result)
    {
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    private static string SerializeOkResult(IActionResult result)
    {
        var value = result.Should().BeOfType<OkObjectResult>().Subject.Value;
        return JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static int CountArrayItems(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return FindProperty(document.RootElement, propertyName).GetArrayLength();
    }

    private static JsonElement FindProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(propertyName)) return property.Value;
                var nested = FindProperty(property.Value, propertyName);
                if (nested.ValueKind != JsonValueKind.Undefined) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindProperty(item, propertyName);
                if (nested.ValueKind != JsonValueKind.Undefined) return nested;
            }
        }

        return default;
    }

    private static string ToHandlerName(string actionId) =>
        $"Execute{string.Concat(actionId.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]))}Async";

    private sealed class ReadActionFixture : IAsyncDisposable
    {
        public Guid AuthorizedUserId { get; } = Guid.NewGuid();
        public Guid OutsiderUserId { get; } = Guid.NewGuid();
        public Guid WorkspaceId { get; } = Guid.NewGuid();
        public Guid OtherWorkspaceId { get; } = Guid.NewGuid();
        public Guid ProjectId { get; } = Guid.NewGuid();
        public Guid OtherProjectId { get; } = Guid.NewGuid();
        public Guid SameWorkspaceProjectId { get; } = Guid.NewGuid();
        public Guid TaskId { get; } = Guid.NewGuid();
        public Guid OtherTaskId { get; } = Guid.NewGuid();
        public Guid SameWorkspaceTaskId { get; } = Guid.NewGuid();
        public Guid PrivateRelatedTaskId { get; } = Guid.NewGuid();
        public Guid GoalId { get; } = Guid.NewGuid();
        public Guid OtherGoalId { get; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; }
        public Mock<IWorkTaskService> WorkTaskService { get; } = new();
        private Mock<IProjectService> ProjectService { get; } = new();
        private Mock<IGoalService> GoalService { get; } = new();

        public ReadActionFixture(int searchResultCount = 3, bool includeCrossProjectSearchResult = false)
        {
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Seed();

            var visibleTasks = Enumerable.Range(0, searchResultCount)
                .Select(index => Task(ProjectId, WorkspaceId, index == 0 ? TaskId : Guid.NewGuid(), $"Task {index}"))
                .ToList();
            if (includeCrossProjectSearchResult)
            {
                visibleTasks.Add(Task(OtherProjectId, OtherWorkspaceId, OtherTaskId, "CROSS-PROJECT-SECRET"));
            }

            WorkTaskService.Setup(service => service.GetByProjectAsync(ProjectId, AuthorizedUserId))
                .ReturnsAsync(visibleTasks.Where(task => task.ProjectId == ProjectId).ToList());
            WorkTaskService.Setup(service => service.SearchTasksAsync(
                    AuthorizedUserId,
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<int?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<string?>()))
                .ReturnsAsync(visibleTasks);
            WorkTaskService.Setup(service => service.GetPersonalWorkSummaryAsync(AuthorizedUserId))
                .ReturnsAsync(new PersonalWorkSummaryDto { Assigned = 2, Created = 1, Overdue = 1 });
            ProjectService.Setup(service => service.GetByIdAsync(ProjectId))
                .ReturnsAsync(new ProjectResponseDto { Id = ProjectId, WorkspaceId = WorkspaceId, Name = "Project A" });
            ProjectService.Setup(service => service.GetMembersAsync(ProjectId))
                .ReturnsAsync(new List<ProjectMemberResponseDto>
                {
                    new() { UserId = AuthorizedUserId, Email = "member@example.test", FullName = "Member", ProjectRole = "Developer" }
                });
            GoalService.Setup(service => service.GetByIdAsync(GoalId))
                .ReturnsAsync(new { id = GoalId, workspaceId = WorkspaceId, title = "Goal A", progress = 25 });
        }

        public AiController CreateController(Guid userId)
        {
            var controller = new AiController(
                Mock.Of<IAiService>(),
                Mock.Of<IAiCreditUsageService>(),
                Mock.Of<IAiAttachmentService>(),
                WorkTaskService.Object,
                ProjectService.Object,
                GoalService.Object,
                Context,
                new ResourceAuthorizationService(Context));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "TestAuth"))
                }
            };
            return controller;
        }

        public AiExecuteActionRequestDto Request(string actionType, Dictionary<string, object?>? payload = null) =>
            new()
            {
                Type = actionType,
                WorkspaceId = WorkspaceId,
                ProjectId = IsProjectScoped(actionType) ? ProjectId : null,
                Payload = payload ?? Payload(actionType, ProjectId, TaskId, GoalId)
            };

        public AiExecuteActionRequestDto CrossWorkspaceRequest(string actionType)
        {
            if (actionType == "get_personal_work_summary")
            {
                return new AiExecuteActionRequestDto { Type = actionType, WorkspaceId = OtherWorkspaceId };
            }

            return new AiExecuteActionRequestDto
            {
                Type = actionType,
                WorkspaceId = WorkspaceId,
                ProjectId = IsProjectScoped(actionType) ? OtherProjectId : null,
                Payload = Payload(actionType, OtherProjectId, OtherTaskId, OtherGoalId)
            };
        }

        public AiExecuteActionRequestDto CrossProjectRequest(string actionType) => new()
        {
            Type = actionType,
            WorkspaceId = WorkspaceId,
            ProjectId = SameWorkspaceProjectId,
            Payload = Payload(actionType, SameWorkspaceProjectId, SameWorkspaceTaskId, GoalId)
        };

        private void Seed()
        {
            var now = DateTime.UtcNow;
            Context.Users.AddRange(
                User(AuthorizedUserId, "authorized@example.test"),
                User(OutsiderUserId, "outsider@example.test"));
            Context.Workspaces.AddRange(
                Workspace(WorkspaceId, AuthorizedUserId, "workspace-a"),
                Workspace(OtherWorkspaceId, OutsiderUserId, "workspace-b"));
            Context.WorkspaceMembers.Add(new WorkspaceMember
            {
                WorkspaceId = WorkspaceId,
                UserId = AuthorizedUserId,
                WorkspaceRole = "MEMBER",
                IsActive = true,
                JoinedAt = now
            });
            Context.Projects.AddRange(
                Project(ProjectId, WorkspaceId, AuthorizedUserId, "Project A"),
                Project(OtherProjectId, OtherWorkspaceId, OutsiderUserId, "Project B"),
                Project(SameWorkspaceProjectId, WorkspaceId, OutsiderUserId, "Project C", "Private"));
            Context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = ProjectId,
                UserId = AuthorizedUserId,
                ProjectRole = "Developer",
                Status = true,
                JoinedAt = now
            });
            Context.WorkTasks.AddRange(
                WorkTask(TaskId, ProjectId, WorkspaceId, AuthorizedUserId, "Task A"),
                WorkTask(PrivateRelatedTaskId, ProjectId, WorkspaceId, OutsiderUserId, "PRIVATE-RELATED-TASK"),
                WorkTask(OtherTaskId, OtherProjectId, OtherWorkspaceId, OutsiderUserId, "Task B"),
                WorkTask(SameWorkspaceTaskId, SameWorkspaceProjectId, WorkspaceId, OutsiderUserId, "Task C"));
            Context.TaskDependencies.Add(new TaskDependency
            {
                PredecessorTaskId = TaskId,
                SuccessorTaskId = PrivateRelatedTaskId,
                DependencyType = 0
            });
            Context.Goals.AddRange(
                new Goal { Id = GoalId, WorkspaceId = WorkspaceId, OwnerId = AuthorizedUserId, Title = "Goal A" },
                new Goal { Id = OtherGoalId, WorkspaceId = OtherWorkspaceId, OwnerId = OutsiderUserId, Title = "Goal B" });
            Context.SaveChanges();
        }

        private static Dictionary<string, object?> Payload(string actionType, Guid projectId, Guid taskId, Guid goalId)
        {
            if (actionType == "get_goal_details") return new() { ["goalId"] = goalId };
            if (actionType == "get_personal_work_summary") return new();
            if (actionType is "list_project_labels" or "list_project_members" or "get_planning_summary" or "search_tasks")
                return new() { ["projectId"] = projectId, ["maxResults"] = 25 };
            return new() { ["projectId"] = projectId, ["taskId"] = taskId };
        }

        private static bool IsProjectScoped(string actionType) =>
            actionType is not "get_goal_details" and not "get_personal_work_summary";

        private static User User(Guid id, string email) => new()
        {
            Id = id,
            Email = email,
            FullName = email,
            PasswordHash = "unused",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        private static Workspace Workspace(Guid id, Guid ownerId, string slug) => new()
        {
            Id = id,
            OwnerId = ownerId,
            Name = slug,
            Slug = slug,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        private static Project Project(Guid id, Guid workspaceId, Guid creatorId, string name, string networkType = "Public") => new()
        {
            Id = id,
            WorkspaceId = workspaceId,
            CreatorId = creatorId,
            Name = name,
            Identifier = name.Replace(" ", string.Empty).ToUpperInvariant(),
            NetworkType = networkType,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        private static WorkTask WorkTask(Guid id, Guid projectId, Guid workspaceId, Guid reporterId, string title) => new()
        {
            Id = id,
            ProjectId = projectId,
            WorkspaceId = workspaceId,
            ReporterId = reporterId,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        private static WorkTaskResponseDto Task(Guid projectId, Guid workspaceId, Guid id, string title) => new()
        {
            Id = id,
            ProjectId = projectId,
            WorkspaceId = workspaceId,
            Title = title,
            StatusName = "Todo",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        public async ValueTask DisposeAsync() => await Context.DisposeAsync();
    }
}
