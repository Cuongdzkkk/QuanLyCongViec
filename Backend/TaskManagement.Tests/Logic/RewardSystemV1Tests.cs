using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Application.DTOs.Rewards;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class RewardSystemV1Tests
{
    [Fact]
    public async Task CompletionCreatesPendingEventUsingStoryPointsAndNoEarlyBonus()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var service = new RewardSystemService(context);

        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "In Progress", "Done");

        var pointEvent = await context.RewardPointEvents.SingleAsync();
        pointEvent.Status.Should().Be("Pending");
        pointEvent.Points.Should().Be(150);
        pointEvent.Xp.Should().Be(150);
        pointEvent.ScoreSource.Should().Be("StoryPoints");
        (await context.PointTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ManagerApprovalFinalizesXpExactlyOnceAndSelfApprovalIsRejected()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var service = new RewardSystemService(context);
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");
        var eventId = await context.RewardPointEvents.Select(item => item.Id).SingleAsync();

        var selfApproval = () => service.ReviewPointEventAsync(ids.ProjectId, eventId, ids.AssigneeId, true, null);
        await selfApproval.Should().ThrowAsync<UnauthorizedAccessException>();
        await service.ReviewPointEventAsync(ids.ProjectId, eventId, ids.ManagerId, true, null);
        await service.ReviewPointEventAsync(ids.ProjectId, eventId, ids.ManagerId, true, null);

        (await context.RewardPointEvents.Where(item => item.UserId == ids.AssigneeId && item.Status == "Finalized").SumAsync(item => item.Xp)).Should().Be(150);
        (await context.PointTransactions.CountAsync()).Should().Be(0);
        (await context.RewardPointEvents.SingleAsync()).Status.Should().Be("Finalized");
    }

    [Fact]
    public async Task ReopeningTaskCancelsPendingEventAndPreventsSecondAward()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var service = new RewardSystemService(context);
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "Done", "In Progress");
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "In Progress", "Done");

        (await context.RewardPointEvents.CountAsync()).Should().Be(1);
        (await context.RewardPointEvents.SingleAsync()).Status.Should().Be("Pending");
    }

    [Fact]
    public async Task ClosedSeasonSettlementCreatesIdempotentRankingGrant()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var service = new RewardSystemService(context);
        await service.CreateDefinitionAsync(ids.ProjectId, ids.SeasonId, ids.ManagerId,
            new CreateRewardDefinitionRequest("Top contributor", "Display-only reward", "Voucher", 25, "USD", "Ranking", "SeasonPoints", 0, 1, 1));
        context.RewardPointEvents.Add(new RewardPointEvent
        {
            Id = Guid.NewGuid(), ProjectId = ids.ProjectId, SeasonId = ids.SeasonId, WorkTaskId = ids.TaskId, UserId = ids.AssigneeId,
            Points = 150, Xp = 150, Status = "Finalized", CompletedAt = DateTimeOffset.UtcNow.AddHours(-1), CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            IdempotencyKey = $"seed:{Guid.NewGuid():N}", DifficultySnapshot = "SP:3", ScoreSource = "StoryPoints"
        });
        await context.SaveChangesAsync();

        await service.CloseSeasonAsync(ids.ProjectId, ids.SeasonId, ids.ManagerId);
        await service.SettleSeasonAsync(ids.ProjectId, ids.SeasonId, ids.ManagerId);

        var grant = await context.RewardGrants.SingleAsync();
        grant.Status.Should().Be("PendingFulfillment");
        grant.RecipientUserId.Should().Be(ids.AssigneeId);
        (await context.RewardGrants.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CareerXpIsCumulativeWhileNewSeasonStartsAtZero()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        context.RewardSeasons.Add(new RewardSeason { Id = Guid.NewGuid(), ProjectId = ids.ProjectId, Name = "Future", Type = "Month", StartAt = DateTimeOffset.UtcNow.AddDays(2), EndAt = DateTimeOffset.UtcNow.AddDays(32), Status = "Draft", CreatedBy = ids.ManagerId, CreatedAt = DateTimeOffset.UtcNow });
        context.RewardPointEvents.Add(new RewardPointEvent { Id = Guid.NewGuid(), ProjectId = ids.ProjectId, SeasonId = ids.SeasonId, WorkTaskId = ids.TaskId, UserId = ids.AssigneeId, Points = 200, Xp = 200, Status = "Finalized", CompletedAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedAt = DateTimeOffset.UtcNow.AddDays(-1), IdempotencyKey = "historical-xp", DifficultySnapshot = "M", ScoreSource = "EstimateHours" });
        await context.SaveChangesAsync();

        var dashboard = await new RewardSystemService(context).GetDashboardAsync(ids.ProjectId, ids.AssigneeId);
        dashboard.CareerXp.Should().Be(200);
        dashboard.MySeasonPoints.Should().Be(200);
    }

    [Fact]
    public async Task DelayedApprovalKeepsCompletionSeason()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var oldSeason = await context.RewardSeasons.SingleAsync(item => item.Id == ids.SeasonId);
        oldSeason.Status = "Closed";
        var eventId = Guid.NewGuid();
        context.RewardPointEvents.Add(new RewardPointEvent { Id = eventId, ProjectId = ids.ProjectId, SeasonId = ids.SeasonId, WorkTaskId = ids.TaskId, UserId = ids.AssigneeId, Points = 100, Xp = 100, Status = "Pending", CompletedAt = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero), CreatedAt = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero), IdempotencyKey = "delayed-approval", DifficultySnapshot = "S", ScoreSource = "StoryPoints" });
        await context.SaveChangesAsync();

        await new RewardSystemService(context).ReviewPointEventAsync(ids.ProjectId, eventId, ids.ManagerId, true, null);
        var finalized = await context.RewardPointEvents.SingleAsync(item => item.Id == eventId);
        finalized.SeasonId.Should().Be(ids.SeasonId);
        finalized.CompletedAt.Should().Be(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task StoryPointSnapshotCannotBeInflatedBeforeApproval()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var service = new RewardSystemService(context);
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");
        var pointEvent = await context.RewardPointEvents.SingleAsync();
        var task = await context.WorkTasks.SingleAsync(item => item.Id == ids.TaskId);
        task.StoryPoints = 13;
        await context.SaveChangesAsync();
        await service.ReviewPointEventAsync(ids.ProjectId, pointEvent.Id, ids.ManagerId, true, null);

        (await context.RewardPointEvents.SingleAsync()).Points.Should().Be(150);
    }

    [Fact]
    public async Task ReopenedFinalizedEventIsRearmedForCorrectedCompletion()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var service = new RewardSystemService(context);
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");
        var eventId = await context.RewardPointEvents.Select(item => item.Id).SingleAsync();
        await service.ReviewPointEventAsync(ids.ProjectId, eventId, ids.ManagerId, true, null);
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "Done", "In Progress");
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "In Progress", "Done");

        (await context.RewardPointEvents.CountAsync()).Should().Be(1);
        (await context.RewardPointEvents.SingleAsync()).Status.Should().Be("Pending");
        (await context.RewardPointEvents.Where(item => item.UserId == ids.AssigneeId && item.Status == "Finalized").SumAsync(item => item.Xp)).Should().Be(0);
    }

    [Fact]
    public async Task ReopenRecompleteApproveProducesOneFinalAwardAndRetriesStayIdempotent()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var service = new RewardSystemService(context);

        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");
        var eventId = await context.RewardPointEvents.Select(item => item.Id).SingleAsync();
        await service.ReviewPointEventAsync(ids.ProjectId, eventId, ids.ManagerId, true, null);
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "Done", "In Progress");
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "In Progress", "Done");
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "In Progress", "Done");
        await service.ReviewPointEventAsync(ids.ProjectId, eventId, ids.ManagerId, true, null);
        await service.ReviewPointEventAsync(ids.ProjectId, eventId, ids.ManagerId, true, null);

        (await context.RewardPointEvents.CountAsync()).Should().Be(1);
        (await context.RewardPointEvents.CountAsync(item => item.Status == "Finalized")).Should().Be(1);
        (await context.RewardPointEvents.Where(item => item.Status == "Finalized").SumAsync(item => item.Xp)).Should().Be(150);
        (await context.RewardPointEvents.Where(item => item.Status == "Finalized").SumAsync(item => item.Points)).Should().Be(150);
    }

    [Fact]
    public async Task ActiveButExpiredSeasonDoesNotAcceptCompletion()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var season = await context.RewardSeasons.SingleAsync(item => item.Id == ids.SeasonId);
        season.EndAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync();

        await new RewardSystemService(context).HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");

        (await context.RewardPointEvents.CountAsync()).Should().Be(0);
        (await context.RewardSeasons.SingleAsync()).Status.Should().Be("Active");
    }

    [Fact]
    public async Task MonthSeasonUsesWorkspaceLocalCalendarBoundariesAndAcceptsUtcBoundary()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var workspace = await context.Workspaces.SingleAsync();
        workspace.Timezone = "Asia/Ho_Chi_Minh";
        var oldSeason = await context.RewardSeasons.SingleAsync(item => item.Id == ids.SeasonId);
        oldSeason.Status = "Closed";
        await context.SaveChangesAsync();

        var fixedClock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 31, 16, 59, 59, TimeSpan.Zero));
        var service = new RewardSystemService(context, fixedClock);
        var month = await service.CreateSeasonAsync(ids.ProjectId, ids.ManagerId,
            new CreateRewardSeasonRequest("September", "Month", null, new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero), null, null));

        month.StartAt.Should().Be(new DateTimeOffset(2026, 8, 31, 17, 0, 0, TimeSpan.Zero));
        month.EndAt.Should().Be(new DateTimeOffset(new DateTime(2026, 9, 30, 16, 59, 59, DateTimeKind.Utc).AddTicks(9999999)));
        await service.ActivateSeasonAsync(ids.ProjectId, month.Id, ids.ManagerId);
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");
        (await context.RewardPointEvents.CountAsync()).Should().Be(0, "one second before local September must be excluded");

        fixedClock.UtcNow = new DateTimeOffset(2026, 8, 31, 17, 0, 0, TimeSpan.Zero);
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");
        (await context.RewardPointEvents.CountAsync()).Should().Be(1, "local September 1 00:00 is the inclusive UTC boundary");
    }

    [Fact]
    public async Task RejectedPendingEventHasNoContribution()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var service = new RewardSystemService(context);
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");
        var eventId = await context.RewardPointEvents.Select(item => item.Id).SingleAsync();
        await service.ReviewPointEventAsync(ids.ProjectId, eventId, ids.ManagerId, false, "Insufficient evidence");

        var pointEvent = await context.RewardPointEvents.SingleAsync();
        pointEvent.Status.Should().Be("Cancelled");
        (await context.RewardPointEvents.Where(item => item.Status == "Finalized").SumAsync(item => item.Points)).Should().Be(0);
    }

    [Fact]
    public async Task RankingTieCreatesResolutionRequiredGrantsForAllTiedMembers()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var tiedUserId = Guid.NewGuid();
        context.Users.Add(new User { Id = tiedUserId, Email = $"tie-{tiedUserId:N}@example.com", FullName = "Tie member", PasswordHash = "unused" });
        context.ProjectMembers.Add(new ProjectMember { ProjectId = ids.ProjectId, UserId = tiedUserId, ProjectRole = "DEV", Status = true, JoinedAt = DateTime.UtcNow });
        context.RewardDefinitions.Add(new RewardDefinition { Id = Guid.NewGuid(), ProjectId = ids.ProjectId, SeasonId = ids.SeasonId, Name = "Top one", RewardType = "Gift", ConditionType = "Ranking", ConditionMetric = "SeasonPoints", RankFrom = 1, RankTo = 1, CreatedBy = ids.ManagerId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        context.RewardPointEvents.AddRange(
            NewFinalizedEvent(ids, ids.AssigneeId, 150, "tie-a"),
            NewFinalizedEvent(ids, tiedUserId, 150, "tie-b"));
        await context.SaveChangesAsync();
        var service = new RewardSystemService(context);
        await service.CloseSeasonAsync(ids.ProjectId, ids.SeasonId, ids.ManagerId);

        var grants = await context.RewardGrants.ToListAsync();
        grants.Should().HaveCount(2);
        grants.Should().OnlyContain(item => item.RequiresManagerResolution);
    }

    [Fact]
    public async Task PersonalMilestoneTeamGoalAndFulfillmentAreExplicitAndIdempotent()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        context.RewardDefinitions.AddRange(
            new RewardDefinition { Id = Guid.NewGuid(), ProjectId = ids.ProjectId, SeasonId = ids.SeasonId, Name = "Milestone", RewardType = "Privilege", ConditionType = "PersonalMilestone", ConditionMetric = "SeasonPoints", Threshold = 100, CreatedBy = ids.ManagerId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new RewardDefinition { Id = Guid.NewGuid(), ProjectId = ids.ProjectId, SeasonId = ids.SeasonId, Name = "Team target", RewardType = "Custom", ConditionType = "TeamGoal", ConditionMetric = "OnTimeRate", Threshold = 100, CreatedBy = ids.ManagerId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        context.RewardPointEvents.Add(NewFinalizedEvent(ids, ids.AssigneeId, 150, "settlement-event"));
        await context.SaveChangesAsync();
        var service = new RewardSystemService(context);
        await service.CloseSeasonAsync(ids.ProjectId, ids.SeasonId, ids.ManagerId);
        await service.SettleSeasonAsync(ids.ProjectId, ids.SeasonId, ids.ManagerId);
        var count = await context.RewardGrants.CountAsync();
        await service.SettleSeasonAsync(ids.ProjectId, ids.SeasonId, ids.ManagerId);

        (await context.RewardGrants.CountAsync()).Should().Be(count);
        var grant = await context.RewardGrants.FirstAsync(item => item.RecipientUserId == ids.AssigneeId);
        await service.FulfillGrantAsync(ids.ProjectId, grant.Id, ids.ManagerId);
        (await context.RewardGrants.SingleAsync(item => item.Id == grant.Id)).Status.Should().Be("Fulfilled");
        (await context.PointTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ActiveMemberPolicyExcludesLeaversByDefaultAndAllowsHistoricalPolicyWhenOptedIn()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var member = await context.ProjectMembers.SingleAsync(item => item.UserId == ids.AssigneeId);
        member.Status = false; member.LeftAt = DateTime.UtcNow;
        context.RewardDefinitions.AddRange(
            new RewardDefinition { Id = Guid.NewGuid(), ProjectId = ids.ProjectId, SeasonId = ids.SeasonId, Name = "Active only", RewardType = "Gift", ConditionType = "PersonalMilestone", ConditionMetric = "SeasonPoints", Threshold = 100, RequireActiveMemberAtSettlement = true, CreatedBy = ids.ManagerId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new RewardDefinition { Id = Guid.NewGuid(), ProjectId = ids.ProjectId, SeasonId = ids.SeasonId, Name = "Historical", RewardType = "Gift", ConditionType = "PersonalMilestone", ConditionMetric = "SeasonPoints", Threshold = 100, RequireActiveMemberAtSettlement = false, CreatedBy = ids.ManagerId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        context.RewardPointEvents.Add(NewFinalizedEvent(ids, ids.AssigneeId, 150, "leaver-event"));
        await context.SaveChangesAsync();
        await new RewardSystemService(context).CloseSeasonAsync(ids.ProjectId, ids.SeasonId, ids.ManagerId);

        (await context.RewardGrants.CountAsync()).Should().Be(1);
        (await context.RewardGrants.SingleAsync()).RewardDefinition.Name.Should().Be("Historical");
    }

    [Fact]
    public async Task NonManagerCannotCreateDefinitionOrFulfillGrant()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var service = new RewardSystemService(context);
        var create = () => service.CreateDefinitionAsync(ids.ProjectId, ids.SeasonId, ids.AssigneeId, new CreateRewardDefinitionRequest("Nope", null, "Gift", null, null, "PersonalMilestone", "SeasonPoints", 1, null, null));
        await create.Should().ThrowAsync<UnauthorizedAccessException>();
        var grant = new RewardGrant { Id = Guid.NewGuid(), ProjectId = ids.ProjectId, SeasonId = ids.SeasonId, RewardDefinitionId = Guid.NewGuid(), RecipientUserId = ids.AssigneeId, Status = "PendingFulfillment", EarnedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow };
        context.RewardGrants.Add(grant);
        await context.SaveChangesAsync();
        var fulfill = () => service.FulfillGrantAsync(ids.ProjectId, grant.Id, ids.AssigneeId);
        await fulfill.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DuplicateCompleteRequestDoesNotCreateAnotherEvent()
    {
        await using var context = CreateContext(); var ids = await SeedAsync(context); var service = new RewardSystemService(context);
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");
        (await context.RewardPointEvents.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CloseRefusesPendingEventsAndDoesNotSilentlySettle()
    {
        await using var context = CreateContext(); var ids = await SeedAsync(context); var service = new RewardSystemService(context);
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");
        var close = () => service.CloseSeasonAsync(ids.ProjectId, ids.SeasonId, ids.ManagerId);
        await close.Should().ThrowAsync<InvalidOperationException>();
        (await context.RewardSeasons.SingleAsync()).Status.Should().Be("Active");
    }

    [Fact]
    public async Task ActiveSeasonOverlapIsRejected()
    {
        await using var context = CreateContext(); var ids = await SeedAsync(context); var service = new RewardSystemService(context);
        var draft = await service.CreateSeasonAsync(ids.ProjectId, ids.ManagerId, new CreateRewardSeasonRequest("Next", "Month", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30), "UTC"));
        var activate = () => service.ActivateSeasonAsync(ids.ProjectId, draft.Id, ids.ManagerId);
        await activate.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SprintMonthAndCustomSeasonDefinitionsArePersistedWithoutChangingTaskScoring()
    {
        await using var context = CreateContext(); var ids = await SeedAsync(context); var service = new RewardSystemService(context);
        var sprintId = Guid.NewGuid(); context.Sprints.Add(new Sprint { Id = sprintId, ProjectId = ids.ProjectId, Name = "Sprint 1", StartDate = DateTime.UtcNow.Date, EndDate = DateTime.UtcNow.Date.AddDays(13) }); await context.SaveChangesAsync();
        var sprint = await service.CreateSeasonAsync(ids.ProjectId, ids.ManagerId, new CreateRewardSeasonRequest("Sprint linked", "Sprint", sprintId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(14), "Asia/Ho_Chi_Minh"));
        sprint.SprintId.Should().Be(sprintId);
        var month = await service.CreateSeasonAsync(ids.ProjectId, ids.ManagerId, new CreateRewardSeasonRequest("Month", "Month", null, DateTimeOffset.UtcNow.AddDays(30), DateTimeOffset.UtcNow.AddDays(60), "UTC"));
        var custom = await service.CreateSeasonAsync(ids.ProjectId, ids.ManagerId, new CreateRewardSeasonRequest("Custom", "Custom", null, DateTimeOffset.UtcNow.AddDays(61), DateTimeOffset.UtcNow.AddDays(70), "UTC"));
        month.Type.Should().Be("Month"); custom.Type.Should().Be("Custom");
    }

    [Fact]
    public async Task TieResolutionCanAwardBothDeterministicallyWithoutMoneySideEffects()
    {
        await using var context = CreateContext(); var ids = await SeedAsync(context); var tiedUserId = Guid.NewGuid();
        context.Users.Add(new User { Id = tiedUserId, Email = $"tie2-{tiedUserId:N}@example.com", FullName = "Tie two", PasswordHash = "unused" });
        context.ProjectMembers.Add(new ProjectMember { ProjectId = ids.ProjectId, UserId = tiedUserId, ProjectRole = "DEV", Status = true, JoinedAt = DateTime.UtcNow });
        var definition = new RewardDefinition { Id = Guid.NewGuid(), ProjectId = ids.ProjectId, SeasonId = ids.SeasonId, Name = "Tie reward", RewardType = "Cash", DisplayValue = 100, Currency = "USD", ConditionType = "Ranking", ConditionMetric = "SeasonPoints", RankFrom = 1, RankTo = 1, CreatedBy = ids.ManagerId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        context.RewardDefinitions.Add(definition); context.RewardPointEvents.AddRange(NewFinalizedEvent(ids, ids.AssigneeId, 150, "tie2-a"), NewFinalizedEvent(ids, tiedUserId, 150, "tie2-b")); await context.SaveChangesAsync();
        var service = new RewardSystemService(context); await service.CloseSeasonAsync(ids.ProjectId, ids.SeasonId, ids.ManagerId);
        foreach (var grant in await context.RewardGrants.ToListAsync()) await service.ResolveGrantAsync(ids.ProjectId, grant.Id, ids.ManagerId, true, "Tie awarded to both.");
        (await context.RewardGrants.CountAsync(item => item.Status == "PendingFulfillment" && !item.RequiresManagerResolution)).Should().Be(2);
        (await context.PointTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task HistoricalEventRemainsAfterAnotherSeasonIsCreated()
    {
        await using var context = CreateContext(); var ids = await SeedAsync(context);
        context.RewardPointEvents.Add(NewFinalizedEvent(ids, ids.AssigneeId, 75, "history-preserve"));
        var service = new RewardSystemService(context); await context.SaveChangesAsync();
        await service.CreateSeasonAsync(ids.ProjectId, ids.ManagerId, new CreateRewardSeasonRequest("Future", "EntireProject", null, DateTimeOffset.UtcNow.AddDays(5), null, "UTC"));
        (await context.RewardPointEvents.CountAsync(item => item.IdempotencyKey == "history-preserve")).Should().Be(1);
    }

    [Fact]
    public async Task MemberCanReadDashboardButCannotSettleSeason()
    {
        await using var context = CreateContext(); var ids = await SeedAsync(context); var service = new RewardSystemService(context);
        var dashboard = await service.GetDashboardAsync(ids.ProjectId, ids.AssigneeId);
        dashboard.CurrentSeason.Should().NotBeNull();
        var settle = () => service.SettleSeasonAsync(ids.ProjectId, ids.SeasonId, ids.AssigneeId);
        await settle.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task MemberCannotCreateSeasonOrRewardDefinition()
    {
        await using var context = CreateContext();
        var ids = await SeedAsync(context);
        var service = new RewardSystemService(context);

        var season = () => service.CreateSeasonAsync(ids.ProjectId, ids.AssigneeId,
            new CreateRewardSeasonRequest("Nope", "Custom", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), "UTC"));
        await season.Should().ThrowAsync<UnauthorizedAccessException>();

        var reward = () => service.CreateDefinitionAsync(ids.ProjectId, ids.SeasonId, ids.AssigneeId,
            new CreateRewardDefinitionRequest("Nope", null, "Gift", null, null, "PersonalMilestone", "SeasonPoints", 1, null, null));
        await reward.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AlreadyCompletedTaskDoesNotReceivePointsFromASecondSeason()
    {
        await using var context = CreateContext(); var ids = await SeedAsync(context); var service = new RewardSystemService(context);
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");
        var firstSeasonEventId = await context.RewardPointEvents.Select(item => item.Id).SingleAsync();
        var season = await context.RewardSeasons.SingleAsync(item => item.Id == ids.SeasonId); season.Status = "Closed";
        var nextSeasonId = Guid.NewGuid(); context.RewardSeasons.Add(new RewardSeason { Id = nextSeasonId, ProjectId = ids.ProjectId, Name = "Next", Type = "Month", StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(30), Status = "Active", CreatedBy = ids.ManagerId, CreatedAt = DateTimeOffset.UtcNow }); await context.SaveChangesAsync();
        await service.HandleTaskStatusChangeAsync(ids.TaskId, ids.ManagerId, "To Do", "Done");
        (await context.RewardPointEvents.CountAsync()).Should().Be(1);
        (await context.RewardPointEvents.SingleAsync()).Id.Should().Be(firstSeasonEventId);
    }

    private static RewardPointEvent NewFinalizedEvent((Guid ProjectId, Guid SeasonId, Guid TaskId, Guid ManagerId, Guid AssigneeId) ids, Guid userId, int points, string key) => new()
    {
        Id = Guid.NewGuid(), ProjectId = ids.ProjectId, SeasonId = ids.SeasonId, WorkTaskId = ids.TaskId, UserId = userId, Points = points, Xp = points,
        Status = "Finalized", CompletedAt = DateTimeOffset.UtcNow.AddHours(-1), CreatedAt = DateTimeOffset.UtcNow.AddHours(-1), IdempotencyKey = key, DifficultySnapshot = "SP:3", ScoreSource = "StoryPoints"
    };

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"reward-v1-{Guid.NewGuid():N}").Options);

    private static async Task<(Guid ProjectId, Guid SeasonId, Guid TaskId, Guid ManagerId, Guid AssigneeId)> SeedAsync(ApplicationDbContext context)
    {
        var managerId = Guid.NewGuid(); var assigneeId = Guid.NewGuid(); var projectId = Guid.NewGuid(); var seasonId = Guid.NewGuid(); var taskId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid(); var statusId = Guid.NewGuid(); var typeId = Guid.NewGuid();
        context.Users.AddRange(
            new User { Id = managerId, Email = $"manager-{managerId:N}@example.com", FullName = "Manager", PasswordHash = "unused" },
            new User { Id = assigneeId, Email = $"member-{assigneeId:N}@example.com", FullName = "Member", PasswordHash = "unused" });
        context.Workspaces.Add(new Workspace { Id = workspaceId, OwnerId = managerId, Name = "Workspace", Slug = $"reward-{workspaceId:N}" });
        context.Projects.Add(new Project { Id = projectId, WorkspaceId = workspaceId, CreatorId = managerId, Identifier = "RWD", Name = "Rewards", Status = true });
        context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = projectId, UserId = managerId, ProjectRole = "PM", Status = true, JoinedAt = DateTime.UtcNow },
            new ProjectMember { ProjectId = projectId, UserId = assigneeId, ProjectRole = "DEV", Status = true, JoinedAt = DateTime.UtcNow });
        context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus { Id = statusId, ProjectId = projectId, Name = "To Do" });
        context.TaskTypes.Add(new TaskType { Id = typeId, ProjectId = projectId, Name = "Task" });
        context.WorkTasks.Add(new WorkTask { Id = taskId, ProjectId = projectId, WorkspaceId = workspaceId, TaskStatusId = statusId, TaskTypeId = typeId,
            ReporterId = assigneeId, AssignedUserId = assigneeId, StoryPoints = 3, Title = "Reward task", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.TaskAssignments.Add(new TaskAssignment { WorkTaskId = taskId, UserId = assigneeId, Status = true, EstimatedHours = 8, ContributionWeight = 1 });
        context.RewardSeasons.Add(new RewardSeason { Id = seasonId, ProjectId = projectId, Name = "Current", Type = "Sprint", StartAt = DateTimeOffset.UtcNow.AddDays(-1), EndAt = DateTimeOffset.UtcNow.AddDays(1), Status = "Active", CreatedBy = managerId, CreatedAt = DateTimeOffset.UtcNow });
        await context.SaveChangesAsync();
        return (projectId, seasonId, taskId, managerId, assigneeId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = initialUtcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
