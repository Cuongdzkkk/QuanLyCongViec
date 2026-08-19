using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.Project;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Infrastructure.Services
{
    public class ProjectMemberService : IProjectMemberService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ICollaborationChannelService? _collaborationChannelService;

        public ProjectMemberService(
            ApplicationDbContext context,
            IEmailService emailService,
            IConfiguration configuration,
            ICollaborationChannelService? collaborationChannelService = null)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
            _collaborationChannelService = collaborationChannelService;
        }

        public async Task<ProjectInvitationOutcome> InviteMemberAsync(
            Guid projectId,
            ProjectMemberRequestDto request,
            string inviterName)
        {
            var persistence = _context.Database.IsRelational()
                ? await _context.Database.CreateExecutionStrategy()
                    .ExecuteAsync(() => PersistInvitationAsync(projectId, request))
                : await PersistInvitationAsync(projectId, request);

            if (persistence.Outcome != ProjectInvitationOutcome.InvitationCreated)
            {
                return persistence.Outcome;
            }

            await _emailService.SendInviteEmailAsync(
                persistence.NormalizedEmail!,
                persistence.InvitedUser!.FullName,
                inviterName,
                "SprintA",
                persistence.InvitedProject!.Name,
                BuildInviteUrl(persistence.RawInviteToken!),
                request.InviteMessage);

            return ProjectInvitationOutcome.InvitationCreated;
        }

        private async Task<InvitationPersistenceResult> PersistInvitationAsync(
            Guid projectId,
            ProjectMemberRequestDto request)
        {
            var normalizedEmail = EmailCanonicalizer.Normalize(request.Email);
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                throw new ArgumentException("Member email is required.");
            }

            IDbContextTransaction? transaction = null;
            if (_context.Database.IsRelational())
            {
                transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            }

            string? rawInviteToken = null;
            User? invitedUser = null;
            Project? invitedProject = null;

            try
            {
                if (_context.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var lockKey = Convert.ToHexString(SHA256.HashData(
                        Encoding.UTF8.GetBytes($"project-invite:{projectId:N}:{normalizedEmail}")));
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"EXEC sys.sp_getapplock @Resource={lockKey}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;");
                }

                invitedProject = await _context.Projects
                    .SingleOrDefaultAsync(project => project.Id == projectId && !project.IsDeleted);
                if (invitedProject == null)
                {
                    throw new ArgumentException("Project does not exist.");
                }

                var resolvedProjectRole = await ResolveProjectRoleAsync(request.Role);
                var now = DateTime.UtcNow;
                invitedUser = await _context.Users
                    .SingleOrDefaultAsync(user => user.Email == normalizedEmail);

                if (invitedUser?.IsDeleted == true)
                {
                    throw new ArgumentException("This email belongs to a deleted account.");
                }

                if (invitedUser == null)
                {
                    invitedUser = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = normalizedEmail,
                        FullName = BuildNameFromEmail(normalizedEmail),
                        PasswordHash = string.Empty,
                        IsActive = false,
                        IsDeleted = false,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _context.Users.Add(invitedUser);
                }
                else
                {
                    invitedUser.UpdatedAt = now;
                }

                var membership = await _context.ProjectMembers
                    .SingleOrDefaultAsync(member => member.ProjectId == projectId && member.UserId == invitedUser.Id);

                if (membership?.Status == true)
                {
                    if (transaction != null) await transaction.CommitAsync();
                    return new InvitationPersistenceResult(ProjectInvitationOutcome.AlreadyActiveMember, null, null, null, null);
                }

                if (membership is { Status: false, LeftAt: null })
                {
                    if (transaction != null) await transaction.CommitAsync();
                    return new InvitationPersistenceResult(ProjectInvitationOutcome.InvitationAlreadyPending, null, null, null, null);
                }

                if (membership == null)
                {
                    membership = new ProjectMember
                    {
                        ProjectId = projectId,
                        UserId = invitedUser.Id,
                        ProjectRole = resolvedProjectRole,
                        JoinedAt = now,
                        Status = false
                    };
                    _context.ProjectMembers.Add(membership);
                }
                else
                {
                    membership.ProjectRole = resolvedProjectRole;
                    membership.JoinedAt = now;
                    membership.LeftAt = null;
                    membership.Status = false;
                }

                var workspaceMembership = await _context.WorkspaceMembers.SingleOrDefaultAsync(member =>
                    member.WorkspaceId == invitedProject.WorkspaceId && member.UserId == invitedUser.Id);
                if (workspaceMembership == null)
                {
                    _context.WorkspaceMembers.Add(new WorkspaceMember
                    {
                        WorkspaceId = invitedProject.WorkspaceId,
                        UserId = invitedUser.Id,
                        WorkspaceRole = "MEMBER",
                        JoinedAt = now,
                        IsActive = false
                    });
                }

                var inviteDeviceId = BuildInviteDeviceId(projectId);
                var previousTokens = await _context.RefreshTokens
                    .Where(token => token.UserId == invitedUser.Id &&
                                    token.DeviceId == inviteDeviceId &&
                                    !token.IsRevoked)
                    .ToListAsync();
                foreach (var token in previousTokens)
                {
                    token.IsRevoked = true;
                }

                rawInviteToken = GenerateInviteToken();
                _context.RefreshTokens.Add(new RefreshToken
                {
                    Id = Guid.NewGuid(),
                    UserId = invitedUser.Id,
                    Token = HashToken(rawInviteToken),
                    DeviceId = inviteDeviceId,
                    ExpiryTime = now.AddDays(7),
                    IsRevoked = false
                });

                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                if (transaction != null) await transaction.RollbackAsync();
                throw new InvalidOperationException(
                    "A membership or invitation for this email already exists.", ex);
            }
            catch
            {
                if (transaction != null) await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }

            return new InvitationPersistenceResult(
                ProjectInvitationOutcome.InvitationCreated,
                normalizedEmail,
                invitedUser,
                invitedProject,
                rawInviteToken);
        }

        public async Task RemoveMemberAsync(
            Guid projectId,
            Guid userId,
            Guid removedBy,
            string? removalReason = null)
        {
            if (removedBy == Guid.Empty)
            {
                throw new UnauthorizedAccessException("Authenticated removal actor is required.");
            }

            IDbContextTransaction? transaction = null;
            if (_context.Database.IsRelational())
            {
                transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            }

            try
            {
                var member = await _context.ProjectMembers
                    .SingleOrDefaultAsync(item => item.ProjectId == projectId && item.UserId == userId && item.Status);
                if (member == null)
                {
                    throw new ArgumentException("Member does not exist or has already left the project.");
                }

                var now = DateTime.UtcNow;
                var reason = string.IsNullOrWhiteSpace(removalReason)
                    ? "Removed from project"
                    : removalReason.Trim();
                member.Status = false;
                member.LeftAt = now;

                var activeAssignments = await _context.TaskAssignments
                    .Include(assignment => assignment.WorkTask)
                    .Where(assignment => assignment.UserId == userId &&
                                         assignment.Status &&
                                         assignment.WorkTask.ProjectId == projectId)
                    .ToListAsync();

                foreach (var assignment in activeAssignments)
                {
                    assignment.Remove(removedBy, now, reason);

                    _context.AuditLogs.Add(new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        WorkTaskId = assignment.WorkTaskId,
                        UserId = removedBy,
                        FieldChanged = "ASSIGNEE_REMOVED_FROM_PROJECT",
                        OldValue = userId.ToString(),
                        NewValue = reason,
                        CreatedAt = now
                    });
                }

                var legacyPrimaryAssignments = await _context.WorkTasks
                    .Where(task => task.ProjectId == projectId && task.AssignedUserId == userId)
                    .ToListAsync();
                foreach (var task in legacyPrimaryAssignments)
                {
                    task.AssignedUserId = null;
                    task.UpdatedAt = now;
                }

                var inviteDeviceId = BuildInviteDeviceId(projectId);
                var pendingInviteTokens = await _context.RefreshTokens
                    .Where(token => token.UserId == userId &&
                                    token.DeviceId == inviteDeviceId &&
                                    !token.IsRevoked)
                    .ToListAsync();
                foreach (var token in pendingInviteTokens)
                {
                    token.IsRevoked = true;
                }

                if (activeAssignments.Count > 0)
                {
                    var manager = await _context.ProjectMembers
                        .FirstOrDefaultAsync(item => item.ProjectId == projectId &&
                                                     item.UserId != userId &&
                                                     item.Status &&
                                                     (item.ProjectRole == "PM" || item.ProjectRole == "PROJECT_MANAGER"));
                    if (manager != null)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            Id = Guid.NewGuid(),
                            UserId = manager.UserId,
                            Title = "Assignments require review",
                            Content = $"A member left the project. {activeAssignments.Count} active assignment(s) were deactivated and retained in history.",
                            CreatedAt = now,
                            IsRead = false
                        });
                    }
                }

                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();
            }
            catch
            {
                if (transaction != null) await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        }

        public async Task UpdateMemberRoleAsync(Guid projectId, Guid userId, string newRole)
        {
            var member = await _context.ProjectMembers
                .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.UserId == userId && item.Status);
            if (member == null)
            {
                throw new ArgumentException("Member does not exist in this project.");
            }

            member.ProjectRole = await ResolveProjectRoleAsync(newRole);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<ProjectMemberResponseDto>> GetProjectMembersAsync(Guid projectId)
        {
            return await _context.ProjectMembers
                .AsNoTracking()
                .Where(member => member.ProjectId == projectId && member.Status && !member.User.IsDeleted)
                .Select(member => new ProjectMemberResponseDto
                {
                    UserId = member.UserId,
                    Email = member.User.Email,
                    FullName = member.User.FullName,
                    ProjectRole = member.ProjectRole,
                    JoinedAt = member.JoinedAt
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<ProjectMemberCandidateResponseDto>> GetProjectMemberCandidatesAsync(
            Guid projectId,
            string? search,
            int page,
            int pageSize)
        {
            var projectWorkspaceId = await _context.Projects
                .AsNoTracking()
                .Where(project => project.Id == projectId && project.Status && !project.IsDeleted)
                .Select(project => (Guid?)project.WorkspaceId)
                .SingleOrDefaultAsync();

            if (!projectWorkspaceId.HasValue)
            {
                throw new ArgumentException("Project does not exist.");
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            var normalizedSearch = search?.Trim().ToLowerInvariant();

            var query = _context.Users
                .AsNoTracking()
                .Where(user => user.IsActive && !user.IsDeleted)
                .Where(user => _context.WorkspaceMembers.Any(workspaceMember =>
                    workspaceMember.WorkspaceId == projectWorkspaceId.Value &&
                    workspaceMember.UserId == user.Id &&
                    workspaceMember.IsActive))
                .Where(user => !_context.ProjectMembers.Any(projectMember =>
                    projectMember.ProjectId == projectId &&
                    projectMember.UserId == user.Id &&
                    projectMember.Status))
                .Where(user => !_context.ProjectMembers.Any(projectMember =>
                    projectMember.ProjectId == projectId &&
                    projectMember.UserId == user.Id &&
                    !projectMember.Status &&
                    projectMember.LeftAt == null));

            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                query = query.Where(user =>
                    user.FullName.ToLower().Contains(normalizedSearch) ||
                    user.Email.ToLower().Contains(normalizedSearch));
            }

            return await query
                .OrderBy(user => user.FullName)
                .ThenBy(user => user.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(user => new ProjectMemberCandidateResponseDto
                {
                    UserId = user.Id,
                    Email = user.Email,
                    FullName = user.FullName
                })
                .ToListAsync();
        }

        public async Task<ProjectMemberResponseDto> AddExistingMemberAsync(
            Guid projectId,
            AddExistingProjectMemberRequestDto request)
        {
            if (request.UserId == Guid.Empty)
            {
                throw new ArgumentException("User is required.");
            }

            IDbContextTransaction? transaction = null;
            if (_context.Database.IsRelational())
            {
                transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            }

            try
            {
                if (_context.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var lockKey = Convert.ToHexString(SHA256.HashData(
                        Encoding.UTF8.GetBytes($"project-member-add:{projectId:N}:{request.UserId:N}")));
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"EXEC sys.sp_getapplock @Resource={lockKey}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;");
                }

                var project = await _context.Projects
                    .SingleOrDefaultAsync(item => item.Id == projectId && item.Status && !item.IsDeleted);
                if (project == null)
                {
                    throw new ArgumentException("Project does not exist.");
                }

                var user = await _context.Users
                    .SingleOrDefaultAsync(item => item.Id == request.UserId);
                if (user == null)
                {
                    throw new ArgumentException("User does not exist.");
                }

                if (!user.IsActive || user.IsDeleted)
                {
                    throw new ArgumentException("User is not active.");
                }

                var workspaceMember = await _context.WorkspaceMembers
                    .SingleOrDefaultAsync(item =>
                        item.WorkspaceId == project.WorkspaceId &&
                        item.UserId == request.UserId);
                if (workspaceMember == null || !workspaceMember.IsActive)
                {
                    throw new ArgumentException("User is not an active member of this workspace.");
                }

                var membership = await _context.ProjectMembers
                    .SingleOrDefaultAsync(item =>
                        item.ProjectId == projectId &&
                        item.UserId == request.UserId);
                if (membership?.Status == true)
                {
                    throw new InvalidOperationException("User is already an active project member.");
                }

                if (membership is { Status: false, LeftAt: null })
                {
                    throw new InvalidOperationException("A pending invitation already exists for this user.");
                }

                var resolvedProjectRole = await ResolveProjectRoleAsync(request.Role);
                var now = DateTime.UtcNow;
                if (membership == null)
                {
                    membership = new ProjectMember
                    {
                        ProjectId = projectId,
                        UserId = request.UserId,
                        ProjectRole = resolvedProjectRole,
                        JoinedAt = now,
                        Status = true
                    };
                    _context.ProjectMembers.Add(membership);
                }
                else
                {
                    membership.ProjectRole = resolvedProjectRole;
                    membership.JoinedAt = now;
                    membership.LeftAt = null;
                    membership.Status = true;
                }

                if (_collaborationChannelService != null)
                {
                    await _collaborationChannelService.EnsureProjectMemberAccessAsync(
                        projectId,
                        request.UserId,
                        assumeActiveProjectMember: true);
                }

                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();

                return new ProjectMemberResponseDto
                {
                    UserId = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    ProjectRole = membership.ProjectRole,
                    JoinedAt = membership.JoinedAt
                };
            }
            catch (DbUpdateException ex)
            {
                if (transaction != null) await transaction.RollbackAsync();
                throw new InvalidOperationException("A project membership already exists for this user.", ex);
            }
            catch
            {
                if (transaction != null) await transaction.RollbackAsync();
                throw;
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        }

        private async Task<string> ResolveProjectRoleAsync(string? requestedRole)
        {
            var normalizedRequestedRole = ProjectExecutionRuleHelper.NormalizeProjectRole(requestedRole);
            if (string.IsNullOrWhiteSpace(normalizedRequestedRole))
            {
                normalizedRequestedRole = ProjectExecutionRuleHelper.NormalizeProjectRole("Developer");
            }

            var availableRoles = await _context.Roles
                .AsNoTracking()
                .Select(role => new
                {
                    role.Name,
                    Normalized = ProjectExecutionRuleHelper.NormalizeProjectRole(role.Name)
                })
                .ToListAsync();

            var exactMatch = availableRoles.FirstOrDefault(role => role.Normalized == normalizedRequestedRole);
            if (exactMatch != null) return exactMatch.Name;

            var aliasMatch = normalizedRequestedRole switch
            {
                "dev" => availableRoles.FirstOrDefault(role => role.Normalized == "developer"),
                "project_manager" => availableRoles.FirstOrDefault(role => role.Normalized == "pm"),
                "scrum_master" => availableRoles.FirstOrDefault(role => role.Normalized == "sm"),
                _ => null
            };
            if (aliasMatch != null) return aliasMatch.Name;

            throw new ArgumentException($"Project role '{requestedRole}' is invalid.");
        }

        private string BuildInviteUrl(string rawInviteToken)
        {
            var frontendBaseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
            return $"{frontendBaseUrl.TrimEnd('/')}/accept-invite?token={Uri.EscapeDataString(rawInviteToken)}";
        }

        private static string BuildInviteDeviceId(Guid projectId) => $"Invite:{projectId:N}";

        private static string GenerateInviteToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(48);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-", StringComparison.Ordinal)
                .Replace("/", "_", StringComparison.Ordinal)
                .TrimEnd('=');
        }

        private static string HashToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        private static string BuildNameFromEmail(string email)
        {
            var words = email.Split('@')[0]
                .Split(new[] { '.', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .Select(word => word.Length == 1
                    ? char.ToUpperInvariant(word[0]).ToString()
                    : char.ToUpperInvariant(word[0]) + word[1..]);
            return string.Join(' ', words);
        }

        private sealed record InvitationPersistenceResult(
            ProjectInvitationOutcome Outcome,
            string? NormalizedEmail,
            User? InvitedUser,
            Project? InvitedProject,
            string? RawInviteToken);
    }
}
