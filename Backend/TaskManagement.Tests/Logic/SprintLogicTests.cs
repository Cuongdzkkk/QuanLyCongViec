using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TaskManagement.Application.DTOs.Sprint;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;
using Xunit;

namespace TaskManagement.Tests.Logic
{
    /// <summary>
    /// SPRINT LOGIC TESTS - Test thực tế vào SprintService
    /// Dùng In-Memory Database để giả lập SQL Server
    /// </summary>
    public class SprintLogicTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly SprintService _sprintService;

        public SprintLogicTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            _context = new ApplicationDbContext(options);
            _sprintService = new SprintService(_context);
        }

        private async Task<Guid> SeedProjectAsync(string name = "Test Project")
        {
            var projectId = Guid.NewGuid();
            _context.Projects.Add(new Project
            {
                Id = projectId,
                Name = name,
                CreatedAt = DateTime.UtcNow,
                WorkspaceId = Guid.NewGuid()
            });
            await _context.SaveChangesAsync();
            return projectId;
        }

        private async Task<Guid> SeedSprintAsync(Guid projectId, bool isActive = false,
            DateTime? start = null, DateTime? end = null, string name = "Sprint 1")
        {
            var sprintId = Guid.NewGuid();
            _context.Sprints.Add(new Sprint
            {
                Id = sprintId,
                ProjectId = projectId,
                Name = name,
                StartDate = start ?? DateTime.UtcNow.AddDays(-1),
                EndDate = end ?? DateTime.UtcNow.AddDays(13),
                Status = isActive,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return sprintId;
        }

        // =========================================================
        // TC_SPRINT_001: [PASS] Tạo Sprint với dữ liệu hợp lệ
        // =========================================================
        [Fact]
        public async Task CreateAsync_ValidData_ReturnsNewSprint()
        {
            // Arrange
            var projectId = await SeedProjectAsync();
            var dto = new CreateSprintDto
            {
                Name = "Sprint Alpha",
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(14)
            };

            // Act
            var result = await _sprintService.CreateAsync(projectId, dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Sprint Alpha", result.Name);
            Assert.Equal(projectId, result.ProjectId);
        }

        // =========================================================
        // TC_SPRINT_002: [PASS] Ngày kết thúc phải sau ngày bắt đầu
        // =========================================================
        [Fact]
        public async Task CreateAsync_EndDateBeforeStartDate_ThrowsArgumentException()
        {
            // Arrange
            var projectId = await SeedProjectAsync();
            var dto = new CreateSprintDto
            {
                Name = "Sprint Sai Ngay",
                StartDate = DateTime.UtcNow.AddDays(10), // Start sau End -> INVALID
                EndDate = DateTime.UtcNow.AddDays(1)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _sprintService.CreateAsync(projectId, dto));

            Assert.Contains("Ngày kết thúc phải sau ngày bắt đầu", ex.Message);
        }

        // =========================================================
        // TC_SPRINT_003: [PASS] Chặn khi Project không tồn tại
        // =========================================================
        [Fact]
        public async Task CreateAsync_NonExistentProject_ThrowsArgumentException()
        {
            // Arrange
            var fakeProjectId = Guid.NewGuid(); // Project giả mạo
            var dto = new CreateSprintDto
            {
                Name = "Ghost Sprint",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(14)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _sprintService.CreateAsync(fakeProjectId, dto));

            Assert.Contains("không tồn tại", ex.Message);
        }

        // =========================================================
        // TC_SPRINT_004: [PASS] Sprint Overlap Guard - Không cho Start khi đã có Sprint Active
        // =========================================================
        [Fact]
        public async Task StartAsync_WhenAnotherSprintActive_ThrowsInvalidOperationException()
        {
            // Arrange
            var projectId = await SeedProjectAsync();

            // Sprint 1 đang Active
            await SeedSprintAsync(projectId, isActive: true, name: "Sprint 1 Đang Chạy");

            // Sprint 2 mới muốn Start
            var sprint2Id = await SeedSprintAsync(projectId, isActive: false, name: "Sprint 2 Muốn Start");

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sprintService.StartAsync(projectId, sprint2Id));

            Assert.Contains("Phải đóng Sprint hiện tại trước", ex.Message);
        }

        // =========================================================
        // TC_SPRINT_005: [PASS] Start Sprint bình thường khi không có Sprint Active
        // =========================================================
        [Fact]
        public async Task StartAsync_WhenNoActiveSprint_Succeeds()
        {
            // Arrange
            var projectId = await SeedProjectAsync();
            var sprintId = await SeedSprintAsync(projectId, isActive: false);

            // Act
            var result = await _sprintService.StartAsync(projectId, sprintId);

            // Assert
            Assert.NotNull(result);
            // Verify in DB
            var sprint = await _context.Sprints.FindAsync(sprintId);
            Assert.True(sprint!.Status); // Status = true = Active
        }

        // =========================================================
        // TC_SPRINT_006: [PASS] Start Sprint đã chạy -> Lỗi
        // =========================================================
        [Fact]
        public async Task StartAsync_SprintAlreadyActive_ThrowsInvalidOperationException()
        {
            // Arrange
            var projectId = await SeedProjectAsync();
            var sprintId = await SeedSprintAsync(projectId, isActive: true);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sprintService.StartAsync(projectId, sprintId));

            Assert.Contains("đã đang chạy", ex.Message);
        }

        // =========================================================
        // TC_SPRINT_007: [PASS] Close Sprint thành công + Task chưa xong chuyển sang Backlog
        // =========================================================
        [Fact]
        public async Task CloseAsync_WithUnfinishedTasks_MovesTasksToBacklog()
        {
            // Arrange
            var projectId = await SeedProjectAsync();
            var workspaceId = (await _context.Projects.FindAsync(projectId))!.WorkspaceId;
            var sprintId = await SeedSprintAsync(projectId, isActive: true);
            var actorUserId = Guid.NewGuid();

            // Seed TaskStatus "In Progress" (không phải Done)
            var inProgressStatus = new TaskManagement.Domain.Entities.TaskStatus
            {
                Id = Guid.NewGuid(), ProjectId = projectId, Name = "In Progress"
            };
            _context.TaskStatuses.Add(inProgressStatus);

            // Seed Task chưa xong
            var taskId = Guid.NewGuid();
            _context.WorkTasks.Add(new WorkTask
            {
                Id = taskId,
                ProjectId = projectId,
                WorkspaceId = workspaceId,
                SprintId = sprintId,
                TaskStatusId = inProgressStatus.Id,
                Title = "Task Còn Dang Dở",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SequenceId = "TC-001",
                ReporterId = actorUserId
            });
            await _context.SaveChangesAsync();

            var closeDto = new CloseSprintDto { TargetSprintId = null }; // Về Backlog

            // Act
            await _sprintService.CloseAsync(sprintId, closeDto, actorUserId);

            // Assert
            var sprint = await _context.Sprints.FindAsync(sprintId);
            Assert.False(sprint!.Status); // Sprint đã đóng

            var task = await _context.WorkTasks.FindAsync(taskId);
            Assert.Null(task!.SprintId); // Task đã về Backlog (null)
        }

        // =========================================================
        // TC_SPRINT_008: [PASS] Close Sprint đã đóng -> Lỗi
        // =========================================================
        [Fact]
        public async Task CloseAsync_SprintAlreadyClosed_ThrowsInvalidOperationException()
        {
            // Arrange
            var projectId = await SeedProjectAsync();
            var sprintId = await SeedSprintAsync(projectId, isActive: false); // Đã đóng
            var dto = new CloseSprintDto();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _sprintService.CloseAsync(sprintId, dto, Guid.NewGuid()));

            Assert.Contains("đã đóng", ex.Message);
        }

        // =========================================================
        // TC_SPRINT_009: [PASS] Close Sprint không tồn tại -> Lỗi
        // =========================================================
        [Fact]
        public async Task CloseAsync_NonExistentSprint_ThrowsArgumentException()
        {
            // Arrange
            var fakeSprint = Guid.NewGuid();
            var dto = new CloseSprintDto();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _sprintService.CloseAsync(fakeSprint, dto, Guid.NewGuid()));

            Assert.Contains("không tồn tại", ex.Message);
        }

        // =========================================================
        // TC_SPRINT_010: [PASS] Close Sprint chuyển Task sang Sprint khác
        // =========================================================
        [Fact]
        public async Task CloseAsync_WithTargetSprint_MovesTasksToTargetSprint()
        {
            // Arrange
            var projectId = await SeedProjectAsync();
            var workspaceId = (await _context.Projects.FindAsync(projectId))!.WorkspaceId;
            var sprint1Id = await SeedSprintAsync(projectId, isActive: true, name: "Sprint 1");
            var sprint2Id = await SeedSprintAsync(projectId, isActive: false, name: "Sprint 2");
            var actorUserId = Guid.NewGuid();

            var inProgressStatus = new TaskManagement.Domain.Entities.TaskStatus
            {
                Id = Guid.NewGuid(), ProjectId = projectId, Name = "In Progress"
            };
            _context.TaskStatuses.Add(inProgressStatus);

            var taskId = Guid.NewGuid();
            _context.WorkTasks.Add(new WorkTask
            {
                Id = taskId, ProjectId = projectId, WorkspaceId = workspaceId, SprintId = sprint1Id,
                TaskStatusId = inProgressStatus.Id, Title = "Task Cần Chuyển",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                SequenceId = "TC-002", ReporterId = actorUserId
            });
            await _context.SaveChangesAsync();

            var closeDto = new CloseSprintDto { TargetSprintId = sprint2Id };

            // Act
            await _sprintService.CloseAsync(sprint1Id, closeDto, actorUserId);

            // Assert
            var task = await _context.WorkTasks.FindAsync(taskId);
            Assert.Equal(sprint2Id, task!.SprintId); // Task đã chuyển sang Sprint 2
        }

        // =========================================================
        // TC_SPRINT_011: [PASS] Update Sprint với dữ liệu hợp lệ
        // =========================================================
        [Fact]
        public async Task UpdateAsync_ValidData_UpdatesSprint()
        {
            // Arrange
            var projectId = await SeedProjectAsync();
            var sprintId = await SeedSprintAsync(projectId);

            var dto = new UpdateSprintDto
            {
                Name = "Sprint Đã Đổi Tên",
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(20)
            };

            // Act
            var result = await _sprintService.UpdateAsync(projectId, sprintId, dto);

            // Assert
            Assert.Equal("Sprint Đã Đổi Tên", result.Name);
        }

        // =========================================================
        // TC_SPRINT_012: [PASS] Update Sprint với ngày không hợp lệ -> Lỗi
        // =========================================================
        [Fact]
        public async Task UpdateAsync_InvalidDateRange_ThrowsArgumentException()
        {
            // Arrange
            var projectId = await SeedProjectAsync();
            var sprintId = await SeedSprintAsync(projectId);

            var dto = new UpdateSprintDto
            {
                Name = "Sprint Lỗi Ngày",
                StartDate = DateTime.UtcNow.AddDays(20), // Start > End
                EndDate = DateTime.UtcNow.AddDays(1)
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _sprintService.UpdateAsync(projectId, sprintId, dto));
        }

        // =========================================================
        // TC_SPRINT_013: [PASS] Burndown Chart - Sprint không có Task thì trả Empty
        // =========================================================
        [Fact]
        public async Task GetBurndownChartAsync_SprintWithNoTasks_ReturnsDataPointsWithZeroPoints()
        {
            // Arrange
            var projectId = await SeedProjectAsync();
            var sprintId = await SeedSprintAsync(projectId,
                start: DateTime.UtcNow.AddDays(-7),
                end: DateTime.UtcNow.AddDays(7));

            // Act
            var result = await _sprintService.GetBurndownChartAsync(sprintId);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result); // Phải có data points
            Assert.All(result, point => Assert.Equal(0, point.IdealPoints));
        }

        // =========================================================
        // TC_SPRINT_014: [PASS] Burndown Chart - Sprint không tồn tại -> Lỗi
        // =========================================================
        [Fact]
        public async Task GetBurndownChartAsync_NonExistentSprint_ThrowsArgumentException()
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _sprintService.GetBurndownChartAsync(Guid.NewGuid()));

            Assert.Contains("không tồn tại", ex.Message);
        }

        // =========================================================
        // TC_SPRINT_015: [PASS] GetById Sprint đúng ProjectId
        // =========================================================
        [Fact]
        public async Task GetByIdAsync_WrongProjectId_ReturnsNullOrMismatch()
        {
            // Arrange
            var projectId = await SeedProjectAsync();
            var sprintId = await SeedSprintAsync(projectId);
            var wrongProjectId = Guid.NewGuid();

            // Act
            var result = await _sprintService.GetByIdAsync(sprintId);

            // Assert - Sprint tìm thấy nhưng ProjectId không khớp
            Assert.NotNull(result);
            Assert.NotEqual(wrongProjectId, result.ProjectId); // ProjectId phải là projectId gốc
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
