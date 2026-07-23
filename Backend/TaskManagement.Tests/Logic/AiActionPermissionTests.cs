using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic
{
    public class AiActionPermissionTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Guid _workspaceId = Guid.NewGuid();
        private readonly Guid _projectId = Guid.NewGuid();
        private readonly Guid _restrictedUserId = Guid.NewGuid();
        private readonly Guid _pmUserId = Guid.NewGuid();

        public AiActionPermissionTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);
            Seed();
        }

        [Fact]
        public async Task ExecuteCreateCycle_RestrictedProjectMember_Returns403AndDoesNotMutate()
        {
            var controller = CreateController(_restrictedUserId);
            var request = CreateCycleRequest("restricted-create-cycle");

            var result = await controller.PreviewAction(request);

            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
            (await _context.Sprints.CountAsync()).Should().Be(0);
            (await _context.AiActionExecutions.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task ExecuteCreateCycle_ProjectManager_Returns200AndMutates()
        {
            var controller = CreateController(_pmUserId);
            var request = CreateCycleRequest("pm-create-cycle");

            var preview = await controller.PreviewAction(request);
            preview.Should().BeOfType<OkObjectResult>();
            var actionId = await _context.AiActionExecutions.Select(action => action.Id).SingleAsync();
            var result = await controller.ConfirmAction(actionId);

            result.Should().BeOfType<OkObjectResult>();
            var sprint = await _context.Sprints.SingleAsync();
            sprint.ProjectId.Should().Be(_projectId);
            sprint.Name.Should().Be("AI Cycle");
            (await _context.SystemAuditLogs.CountAsync(log =>
                log.Action == "AI_ACTION_EXECUTE" &&
                log.Status == "Success")).Should().Be(1);

            var replay = await controller.ConfirmAction(actionId);
            replay.Should().BeOfType<OkObjectResult>();
            (await _context.Sprints.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task DirectExecute_AndCancelledPreview_CreateNoEntity()
        {
            var controller = CreateController(_pmUserId);
            var request = CreateCycleRequest("server-confirm-required");

            (await controller.ExecuteAction(request)).Should().BeOfType<BadRequestObjectResult>();
            (await _context.Sprints.CountAsync()).Should().Be(0);

            (await controller.PreviewAction(request)).Should().BeOfType<OkObjectResult>();
            var actionId = await _context.AiActionExecutions.Select(action => action.Id).SingleAsync();
            (await controller.CancelAction(actionId)).Should().BeOfType<OkObjectResult>();
            (await controller.ConfirmAction(actionId)).Should().BeOfType<ConflictObjectResult>();
            (await _context.Sprints.CountAsync()).Should().Be(0);

            var reloadedController = CreateController(_pmUserId);
            (await reloadedController.GetAction(actionId)).Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task ExecuteUpdatePriority_ClosedSprint_IsRejectedWithoutMutation()
        {
            var sprintId = Guid.NewGuid();
            var taskId = Guid.NewGuid();
            var statusId = Guid.NewGuid();
            var typeId = Guid.NewGuid();
            _context.Sprints.Add(new Sprint
            {
                Id = sprintId,
                ProjectId = _projectId,
                Name = "Closed",
                Status = true,
                StartDate = DateTime.UtcNow.AddDays(-10),
                EndDate = DateTime.UtcNow.AddDays(-1)
            });
            _context.TaskStatuses.Add(new TaskManagement.Domain.Entities.TaskStatus
            {
                Id = statusId,
                ProjectId = _projectId,
                Name = "To Do"
            });
            _context.TaskTypes.Add(new TaskType
            {
                Id = typeId,
                ProjectId = _projectId,
                Name = "Task"
            });
            _context.WorkTasks.Add(new WorkTask
            {
                Id = taskId,
                WorkspaceId = _workspaceId,
                ProjectId = _projectId,
                SprintId = sprintId,
                TaskStatusId = statusId,
                TaskTypeId = typeId,
                ReporterId = _pmUserId,
                Title = "Locked task",
                Priority = 3,
                SequenceId = "PRJ-1",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            var controller = CreateController(_pmUserId);

            var request = new AiExecuteActionRequestDto
            {
                Type = "update_task_priority",
                IdempotencyKey = "closed-sprint-priority",
                ProjectId = _projectId,
                Payload = new Dictionary<string, object?>
                {
                    ["taskId"] = taskId.ToString(),
                    ["priority"] = 1
                },
                WorkspaceId = _workspaceId
            };
            var preview = await controller.PreviewAction(request);
            preview.Should().BeOfType<OkObjectResult>();
            var actionId = await _context.AiActionExecutions.Select(action => action.Id).SingleAsync();
            var result = await controller.ConfirmAction(actionId);

            result.Should().BeOfType<BadRequestObjectResult>();
            (await _context.WorkTasks.SingleAsync(task => task.Id == taskId)).Priority.Should().Be(3);
        }

        private AiController CreateController(Guid userId)
        {
            var controller = new AiController(
                Mock.Of<IAiService>(),
                Mock.Of<IAiAttachmentService>(),
                Mock.Of<IWorkTaskService>(),
                Mock.Of<IProjectService>(),
                Mock.Of<IGoalService>(),
                _context,
                new ResourceAuthorizationService(_context));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                    }, "TestAuth"))
                }
            };

            return controller;
        }

        private AiExecuteActionRequestDto CreateCycleRequest(string idempotencyKey)
        {
            return new AiExecuteActionRequestDto
            {
                Type = "create_cycle",
                IdempotencyKey = idempotencyKey,
                ProjectId = _projectId,
                WorkspaceId = _workspaceId,
                Payload = new Dictionary<string, object?>
                {
                    ["projectId"] = _projectId.ToString(),
                    ["name"] = "AI Cycle",
                    ["startDate"] = "2026-07-12",
                    ["endDate"] = "2026-07-15"
                }
            };
        }

        private void Seed()
        {
            _context.Workspaces.Add(new Workspace
            {
                Id = _workspaceId,
                Name = "Security Workspace",
                Slug = "security-workspace",
                OwnerId = _pmUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            _context.Users.AddRange(
                new User
                {
                    Id = _restrictedUserId,
                    Email = "restricted@example.com",
                    FullName = "Restricted User",
                    PasswordHash = "unused",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new User
                {
                    Id = _pmUserId,
                    Email = "pm@example.com",
                    FullName = "PM User",
                    PasswordHash = "unused",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });

            _context.WorkspaceMembers.AddRange(
                new WorkspaceMember
                {
                    WorkspaceId = _workspaceId,
                    UserId = _restrictedUserId,
                    WorkspaceRole = "MEMBER",
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow
                },
                new WorkspaceMember
                {
                    WorkspaceId = _workspaceId,
                    UserId = _pmUserId,
                    WorkspaceRole = "MEMBER",
                    IsActive = true,
                    JoinedAt = DateTime.UtcNow
                });

            _context.Projects.Add(new Project
            {
                Id = _projectId,
                WorkspaceId = _workspaceId,
                Name = "Security Project",
                Identifier = "SEC",
                CreatorId = _pmUserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            _context.ProjectMembers.AddRange(
                new ProjectMember
                {
                    ProjectId = _projectId,
                    UserId = _restrictedUserId,
                    ProjectRole = "Developer",
                    Status = true,
                    JoinedAt = DateTime.UtcNow
                },
                new ProjectMember
                {
                    ProjectId = _projectId,
                    UserId = _pmUserId,
                    ProjectRole = "PM",
                    Status = true,
                    JoinedAt = DateTime.UtcNow
                });

            _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
