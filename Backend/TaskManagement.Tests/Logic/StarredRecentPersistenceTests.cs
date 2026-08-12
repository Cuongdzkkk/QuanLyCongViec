using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.DTOs.StarredRecent;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;
using TaskStatus = TaskManagement.Domain.Entities.TaskStatus;

namespace TaskManagement.Tests.Logic;

public sealed class StarredRecentPersistenceTests
{
    [Fact]
    public async Task StarProject_IsIdempotentCanonicalAndPersistentAcrossContexts()
    {
        await using var fixture = await StarRecentFixture.CreateAsync();
        await using (var firstContext = fixture.CreateContext())
        {
            var first = await fixture.StarredService(firstContext).StarAsync(
                fixture.UserAId, fixture.WorkspaceId, "project", fixture.ProjectAId);
            var retry = await fixture.StarredService(firstContext).StarAsync(
                fixture.UserAId, fixture.WorkspaceId, "Project", fixture.ProjectAId);

            first.Status.Should().Be("starred");
            retry.Status.Should().Be("starred");
        }

        await using var reloadedContext = fixture.CreateContext();
        var page = await fixture.StarredService(reloadedContext).GetAllAsync(
            fixture.UserAId, fixture.WorkspaceId, 1, 20);

        page.Items.Should().ContainSingle();
        page.Items[0].ItemType.Should().Be(StarredItemTypes.Project);
        page.Items[0].Url.Should().Be($"/home/projects/{fixture.ProjectAId}");
        (await reloadedContext.StarredItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task TaskAlias_IsStoredAsCanonicalWorkTask()
    {
        await using var fixture = await StarRecentFixture.CreateAsync();
        await using var context = fixture.CreateContext();

        var result = await fixture.StarredService(context).StarAsync(
            fixture.UserAId, fixture.WorkspaceId, "task", fixture.TaskAId);

        result.Item!.ItemType.Should().Be(StarredItemTypes.WorkTask);
        (await context.StarredItems.SingleAsync()).ItemType
            .Should().Be(StarredItemTypes.WorkTask);
        result.Item.ProjectId.Should().Be(fixture.ProjectAId);
        result.Item.Url.Should().Contain($"task={fixture.TaskAId}");
    }

    [Fact]
    public async Task StarredItems_AreIsolatedAndUnstarDoesNotAffectAnotherUser()
    {
        await using var fixture = await StarRecentFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        var service = fixture.StarredService(context);
        await service.StarAsync(
            fixture.UserAId, fixture.WorkspaceId, "Project", fixture.ProjectAId);
        await service.StarAsync(
            fixture.UserBId, fixture.WorkspaceId, "Project", fixture.ProjectAId);

        await service.UnstarAsync(
            fixture.UserAId, fixture.WorkspaceId, "Project", fixture.ProjectAId);
        await service.UnstarAsync(
            fixture.UserAId, fixture.WorkspaceId, "Project", fixture.ProjectAId);

        (await service.GetAllAsync(fixture.UserAId, fixture.WorkspaceId, 1, 20))
            .Items.Should().BeEmpty();
        (await service.GetAllAsync(fixture.UserBId, fixture.WorkspaceId, 1, 20))
            .Items.Should().ContainSingle();
    }

    [Fact]
    public async Task OutsiderCannotStarAndInvalidTypeIsRejected()
    {
        await using var fixture = await StarRecentFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        var service = fixture.StarredService(context);

        var outsider = () => service.StarAsync(
            fixture.OutsiderId, fixture.WorkspaceId, "Project", fixture.ProjectAId);
        var invalid = () => service.StarAsync(
            fixture.UserAId, fixture.WorkspaceId, "Cycle", fixture.ProjectAId);

        await outsider.Should().ThrowAsync<UnauthorizedAccessException>();
        await invalid.Should().ThrowAsync<ArgumentException>();
        (await context.StarredItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task StarredList_FiltersDeletedOrphansAndPaginatesDeterministically()
    {
        await using var fixture = await StarRecentFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        var start = DateTime.UtcNow.AddHours(-1);
        context.StarredItems.AddRange(
            fixture.Star(fixture.UserAId, StarredItemTypes.Project, fixture.ProjectAId, start.AddMinutes(1)),
            fixture.Star(fixture.UserAId, StarredItemTypes.Project, fixture.ProjectBId, start.AddMinutes(2)),
            fixture.Star(fixture.UserAId, StarredItemTypes.WorkTask, fixture.TaskAId, start.AddMinutes(3)),
            fixture.Star(fixture.UserAId, StarredItemTypes.WorkTask, fixture.TaskBId, start.AddMinutes(4)),
            fixture.Star(fixture.UserAId, StarredItemTypes.WorkTask, fixture.DeletedTaskId, start.AddMinutes(5)),
            fixture.Star(fixture.UserAId, StarredItemTypes.Project, Guid.NewGuid(), start.AddMinutes(6)));
        await context.SaveChangesAsync();
        var service = fixture.StarredService(context);

        var first = await service.GetAllAsync(fixture.UserAId, fixture.WorkspaceId, 1, 2);
        var second = await service.GetAllAsync(fixture.UserAId, fixture.WorkspaceId, 2, 2);

        first.TotalCount.Should().Be(4);
        first.Items.Select(item => item.ItemId)
            .Should().NotIntersectWith(second.Items.Select(item => item.ItemId));
        first.Items.Concat(second.Items).Select(item => item.ItemId)
            .Should().Equal(
                fixture.TaskBId,
                fixture.TaskAId,
                fixture.ProjectBId,
                fixture.ProjectAId);
    }

    [Fact]
    public async Task ConcurrentStarRequests_CreateOneRecord()
    {
        await using var fixture = await StarRecentFixture.CreateAsync();
        await using var contextA = fixture.CreateContext();
        await using var contextB = fixture.CreateContext();

        await Task.WhenAll(
            fixture.StarredService(contextA).StarAsync(
                fixture.UserAId, fixture.WorkspaceId, "Project", fixture.ProjectAId),
            fixture.StarredService(contextB).StarAsync(
                fixture.UserAId, fixture.WorkspaceId, "project", fixture.ProjectAId));

        await using var verification = fixture.CreateContext();
        (await verification.StarredItems.CountAsync(item =>
            item.UserId == fixture.UserAId &&
            item.ItemId == fixture.ProjectAId)).Should().Be(1);
    }

    [Fact]
    public async Task StarredController_UsesJwtIdentityOnly()
    {
        var userAId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var service = new Mock<IStarredItemService>();
        service.Setup(item => item.StarAsync(
                userAId, workspaceId, "Project", projectId))
            .ReturnsAsync(new StarredItemMutationDto { Status = "starred" });
        var controller = new StarredItemsController(service.Object)
        {
            ControllerContext = ControllerContextFor(userAId)
        };

        var result = await controller.Star(workspaceId, new StarredItemRequestDto
        {
            ItemType = "Project",
            ItemId = projectId
        });

        result.Should().BeOfType<OkObjectResult>();
        service.Verify(item => item.StarAsync(
            userAId, workspaceId, "Project", projectId), Times.Once);
    }

    [Fact]
    public async Task RecordView_IsExplicitDeduplicatedAndPersistent()
    {
        await using var fixture = await StarRecentFixture.CreateAsync();
        DateTime firstViewedAt;
        await using (var firstContext = fixture.CreateContext())
        {
            var service = fixture.RecentService(firstContext);
            var first = await service.RecordAsync(
                fixture.UserAId, "project", fixture.ProjectAId);
            firstViewedAt = first.ViewedAt;
            await Task.Delay(5);
            var revisited = await service.RecordAsync(
                fixture.UserAId, "Project", fixture.ProjectAId);

            revisited.Id.Should().Be(first.Id);
            revisited.ViewedAt.Should().BeAfter(firstViewedAt);
        }

        await using var reloaded = fixture.CreateContext();
        var page = await fixture.RecentService(reloaded).GetAllAsync(
            fixture.UserAId, 1, 20);
        page.Items.Should().ContainSingle();
        page.Items[0].Title.Should().Be("Project A");
        (await reloaded.RecentViews.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RecentViewController_UsesJwtIdentityAndIgnoresClientMetadata()
    {
        var userAId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var viewedAt = DateTime.UtcNow;
        var service = new Mock<IRecentViewService>();
        service.Setup(item => item.RecordAsync(userAId, "Project", projectId))
            .ReturnsAsync(new RecentViewDto
            {
                Id = Guid.NewGuid(),
                EntityType = "Project",
                EntityId = projectId,
                Title = "Server title",
                ViewedAt = viewedAt
            });
        var controller = new RecentViewsController(service.Object)
        {
            ControllerContext = ControllerContextFor(userAId)
        };

        var result = await controller.Upsert(new RecentViewRequestDto
        {
            EntityType = "Project",
            EntityId = projectId,
            Title = "Spoofed title",
            Url = "/spoofed"
        });

        result.Should().BeOfType<OkObjectResult>();
        service.Verify(item => item.RecordAsync(userAId, "Project", projectId), Times.Once);
    }

    [Fact]
    public async Task ReadingAListDoesNotRecordAView()
    {
        await using var fixture = await StarRecentFixture.CreateAsync();
        await using var context = fixture.CreateContext();

        var page = await fixture.RecentService(context).GetAllAsync(
            fixture.UserAId, 1, 20);

        page.Items.Should().BeEmpty();
        (await context.RecentViews.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RecentViews_AreIsolatedAndOutsiderCannotRecord()
    {
        await using var fixture = await StarRecentFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        var service = fixture.RecentService(context);
        await service.RecordAsync(fixture.UserAId, "Project", fixture.ProjectAId);
        await service.RecordAsync(fixture.UserBId, "Project", fixture.ProjectAId);

        (await service.GetAllAsync(fixture.UserAId, 1, 20))
            .Items.Should().ContainSingle();
        (await service.GetAllAsync(fixture.UserBId, 1, 20))
            .Items.Should().ContainSingle();
        var outsider = () => service.RecordAsync(
            fixture.OutsiderId, "Project", fixture.ProjectAId);
        await outsider.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RecentList_FiltersDeletedEntitiesAndReturnsStableMetadata()
    {
        await using var fixture = await StarRecentFixture.CreateAsync();
        await using var context = fixture.CreateContext();
        var start = DateTime.UtcNow.AddHours(-1);
        context.RecentViews.AddRange(
            fixture.View(fixture.UserAId, StarredItemTypes.Project, fixture.ProjectAId, start.AddMinutes(1)),
            fixture.View(fixture.UserAId, StarredItemTypes.Project, fixture.ProjectBId, start.AddMinutes(2)),
            fixture.View(fixture.UserAId, StarredItemTypes.WorkTask, fixture.TaskAId, start.AddMinutes(3)),
            fixture.View(fixture.UserAId, StarredItemTypes.WorkTask, fixture.TaskBId, start.AddMinutes(4)),
            fixture.View(fixture.UserAId, StarredItemTypes.WorkTask, fixture.DeletedTaskId, start.AddMinutes(5)));
        await context.SaveChangesAsync();
        var service = fixture.RecentService(context);

        var first = await service.GetAllAsync(fixture.UserAId, 1, 2);
        var second = await service.GetAllAsync(fixture.UserAId, 2, 2);

        first.TotalCount.Should().Be(4);
        first.Items.Select(item => item.EntityId)
            .Should().NotIntersectWith(second.Items.Select(item => item.EntityId));
        first.Items.Concat(second.Items).Select(item => item.EntityId)
            .Should().Equal(
                fixture.TaskBId,
                fixture.TaskAId,
                fixture.ProjectBId,
                fixture.ProjectAId);
        first.Items[0].EntityType.Should().Be(StarredItemTypes.WorkTask);
        first.Items[0].ProjectId.Should().Be(fixture.ProjectAId);
        first.Items[0].Url.Should().Contain($"task={fixture.TaskBId}");
    }

    [Fact]
    public async Task ConcurrentViewRequests_CreateOneRecord()
    {
        await using var fixture = await StarRecentFixture.CreateAsync();
        await using var contextA = fixture.CreateContext();
        await using var contextB = fixture.CreateContext();

        await Task.WhenAll(
            fixture.RecentService(contextA).RecordAsync(
                fixture.UserAId, "Task", fixture.TaskAId),
            fixture.RecentService(contextB).RecordAsync(
                fixture.UserAId, "WorkTask", fixture.TaskAId));

        await using var verification = fixture.CreateContext();
        (await verification.RecentViews.CountAsync(item =>
            item.UserId == fixture.UserAId &&
            item.EntityId == fixture.TaskAId)).Should().Be(1);
    }

    [Fact]
    public void ModelHasDatabaseUniqueAndOrderingIndexes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new ApplicationDbContext(options);

        var starredIndexes = context.Model.FindEntityType(typeof(StarredItem))!.GetIndexes();
        var recentIndexes = context.Model.FindEntityType(typeof(RecentView))!.GetIndexes();

        starredIndexes.Should().Contain(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { "UserId", "WorkspaceId", "ItemType", "ItemId" }));
        recentIndexes.Should().Contain(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { "UserId", "EntityType", "EntityId" }));
        starredIndexes.Should().Contain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { "UserId", "WorkspaceId", "CreatedAt", "Id" }));
        recentIndexes.Should().Contain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { "UserId", "ViewedAt", "Id" }));
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task SqlServerEnforcesUniqueKeysAndCanonicalTypeConstraints()
    {
        var connectionString = SqlServerTestConfiguration.ConnectionString("SprintAStarRecent01Integration");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        try
        {
            await using (var setup = new ApplicationDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                setup.Users.Add(new User
                {
                    Id = userId,
                    Email = $"{userId:N}@example.test",
                    FullName = "SQL Integration User",
                    PasswordHash = "unused",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                setup.Workspaces.Add(new Workspace
                {
                    Id = workspaceId,
                    Name = "SQL Integration Workspace",
                    Slug = $"sql-{workspaceId:N}",
                    OwnerId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                setup.StarredItems.Add(new StarredItem
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    WorkspaceId = workspaceId,
                    ItemType = StarredItemTypes.Project,
                    ItemId = entityId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                setup.RecentViews.Add(new RecentView
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    EntityType = StarredItemTypes.Project,
                    EntityId = entityId,
                    Title = "Project",
                    ViewedAt = DateTime.UtcNow
                });
                await setup.SaveChangesAsync();
            }

            await using (var duplicateStar = new ApplicationDbContext(options))
            {
                duplicateStar.StarredItems.Add(new StarredItem
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    WorkspaceId = workspaceId,
                    ItemType = StarredItemTypes.Project,
                    ItemId = entityId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                var save = () => duplicateStar.SaveChangesAsync();
                await save.Should().ThrowAsync<DbUpdateException>();
            }

            await using (var duplicateView = new ApplicationDbContext(options))
            {
                duplicateView.RecentViews.Add(new RecentView
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    EntityType = StarredItemTypes.Project,
                    EntityId = entityId,
                    Title = "Project",
                    ViewedAt = DateTime.UtcNow
                });
                var save = () => duplicateView.SaveChangesAsync();
                await save.Should().ThrowAsync<DbUpdateException>();
            }

            await using (var invalidType = new ApplicationDbContext(options))
            {
                invalidType.RecentViews.Add(new RecentView
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    EntityType = "Cycle",
                    EntityId = Guid.NewGuid(),
                    Title = "Invalid",
                    ViewedAt = DateTime.UtcNow
                });
                var save = () => invalidType.SaveChangesAsync();
                await save.Should().ThrowAsync<DbUpdateException>();
            }
        }
        finally
        {
            try
            {
                await using var cleanup = new ApplicationDbContext(options);
                await cleanup.RecentViews
                    .IgnoreQueryFilters()
                    .Where(item => item.UserId == userId)
                    .ExecuteDeleteAsync();
                await cleanup.StarredItems
                    .IgnoreQueryFilters()
                    .Where(item => item.UserId == userId)
                    .ExecuteDeleteAsync();
                await cleanup.Workspaces
                    .IgnoreQueryFilters()
                    .Where(item => item.Id == workspaceId)
                    .ExecuteDeleteAsync();
                await cleanup.Users
                    .IgnoreQueryFilters()
                    .Where(item => item.Id == userId)
                    .ExecuteDeleteAsync();
            }
            catch
            {
                // Cleanup is best-effort and never drops the integration database.
            }
        }
    }

    private static ControllerContext ControllerContextFor(Guid userId)
    {
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    "TestAuth"))
            }
        };
    }

    private sealed class StarRecentFixture : IAsyncDisposable
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        private StarRecentFixture(DbContextOptions<ApplicationDbContext> options)
        {
            _options = options;
        }

        public Guid UserAId { get; } = Guid.NewGuid();
        public Guid UserBId { get; } = Guid.NewGuid();
        public Guid OutsiderId { get; } = Guid.NewGuid();
        public Guid WorkspaceId { get; } = Guid.NewGuid();
        public Guid ProjectAId { get; } = Guid.NewGuid();
        public Guid ProjectBId { get; } = Guid.NewGuid();
        public Guid TaskAId { get; } = Guid.NewGuid();
        public Guid TaskBId { get; } = Guid.NewGuid();
        public Guid DeletedTaskId { get; } = Guid.NewGuid();

        public static async Task<StarRecentFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var fixture = new StarRecentFixture(options);
            await fixture.SeedAsync();
            return fixture;
        }

        public ApplicationDbContext CreateContext()
        {
            return new ApplicationDbContext(_options);
        }

        public IStarredItemService StarredService(ApplicationDbContext context)
        {
            var resolver = new PersonalEntityReferenceResolver(context);
            return new StarredItemService(context, resolver);
        }

        public IRecentViewService RecentService(ApplicationDbContext context)
        {
            var resolver = new PersonalEntityReferenceResolver(context);
            return new RecentViewService(context, resolver);
        }

        public StarredItem Star(
            Guid userId,
            string itemType,
            Guid itemId,
            DateTime createdAt)
        {
            return new StarredItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                WorkspaceId = WorkspaceId,
                ItemType = itemType,
                ItemId = itemId,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
        }

        public RecentView View(
            Guid userId,
            string entityType,
            Guid entityId,
            DateTime viewedAt)
        {
            return new RecentView
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EntityType = entityType,
                EntityId = entityId,
                Title = "Client supplied title",
                Url = "/client-supplied",
                ViewedAt = viewedAt
            };
        }

        private async Task SeedAsync()
        {
            await using var context = CreateContext();
            var now = DateTime.UtcNow;
            var statusId = Guid.NewGuid();
            var taskTypeId = Guid.NewGuid();
            context.Users.AddRange(
                User(UserAId, "a@example.test", "User A"),
                User(UserBId, "b@example.test", "User B"),
                User(OutsiderId, "outsider@example.test", "Outsider"));
            context.Workspaces.Add(new Workspace
            {
                Id = WorkspaceId,
                Name = "Workspace",
                Slug = "star-recent-tests",
                OwnerId = UserAId,
                CreatedAt = now,
                UpdatedAt = now
            });
            context.WorkspaceMembers.AddRange(
                WorkspaceMember(UserAId),
                WorkspaceMember(UserBId),
                WorkspaceMember(OutsiderId));
            context.Projects.AddRange(
                Project(ProjectAId, "Project A"),
                Project(ProjectBId, "Project B"));
            context.ProjectMembers.AddRange(
                ProjectMember(ProjectAId, UserAId),
                ProjectMember(ProjectAId, UserBId),
                ProjectMember(ProjectBId, UserAId));
            context.TaskStatuses.Add(new TaskStatus
            {
                Id = statusId,
                ProjectId = ProjectAId,
                Name = "To Do"
            });
            context.TaskTypes.Add(new TaskType
            {
                Id = taskTypeId,
                ProjectId = ProjectAId,
                Name = "Task"
            });
            context.WorkTasks.AddRange(
                WorkTask(TaskAId, "Task A", statusId, taskTypeId),
                WorkTask(TaskBId, "Task B", statusId, taskTypeId),
                WorkTask(DeletedTaskId, "Deleted Task", statusId, taskTypeId, deleted: true));
            await context.SaveChangesAsync();
        }

        private static User User(Guid id, string email, string name)
        {
            return new User
            {
                Id = id,
                Email = email,
                FullName = name,
                PasswordHash = "unused",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private WorkspaceMember WorkspaceMember(Guid userId)
        {
            return new WorkspaceMember
            {
                WorkspaceId = WorkspaceId,
                UserId = userId,
                WorkspaceRole = "MEMBER",
                IsActive = true,
                JoinedAt = DateTime.UtcNow
            };
        }

        private Project Project(Guid id, string name)
        {
            return new Project
            {
                Id = id,
                WorkspaceId = WorkspaceId,
                CreatorId = UserAId,
                Name = name,
                Identifier = name.EndsWith('A') ? "PRA" : "PRB",
                Status = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private static ProjectMember ProjectMember(Guid projectId, Guid userId)
        {
            return new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                ProjectRole = "Developer",
                Status = true,
                JoinedAt = DateTime.UtcNow
            };
        }

        private WorkTask WorkTask(
            Guid id,
            string title,
            Guid statusId,
            Guid taskTypeId,
            bool deleted = false)
        {
            return new WorkTask
            {
                Id = id,
                WorkspaceId = WorkspaceId,
                ProjectId = ProjectAId,
                TaskStatusId = statusId,
                TaskTypeId = taskTypeId,
                ReporterId = UserAId,
                Title = title,
                SequenceId = $"PRA-{id.ToString("N")[..4]}",
                IsDeleted = deleted,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
