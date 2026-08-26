using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Infrastructure.Data;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Security.Claims;
using TaskManagement.Application.Interfaces;
using TaskManagement.Application.Common;

namespace TaskManagement.API.Controllers
{
    [ApiController]
    [Route("api/security")]
    [Authorize]
    public class SecurityController : ControllerBase
    {
        private static readonly string[] AdminAccessRoles =
        {
            "superadmin",
            "admin",
            "system admin",
            "organization admin",
            "accessadmin",
            "access admin"
        };

        private readonly ApplicationDbContext _context;
        private readonly IResourceAuthorizationService _authorization;

        public SecurityController(
            ApplicationDbContext context,
            IResourceAuthorizationService authorization)
        {
            _context = context;
            _authorization = authorization;
        }

        public class IpWhitelistEntry
        {
            public string Ip { get; set; } = string.Empty;
            public string? Note { get; set; }
            public string? AddedBy { get; set; }
            public string? Date { get; set; }
        }

        [HttpGet("current-ip")]
        public async Task<IActionResult> GetCurrentIp()
        {
            if (!await CurrentUserHasAdminAccessAsync())
            {
                return Forbid();
            }

            return Ok(new
            {
                ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
        }

        [HttpGet("ip-whitelist")]
        public async Task<IActionResult> GetIpWhitelist()
        {
            if (!await CurrentUserHasAdminAccessAsync())
            {
                return Forbid();
            }

            var config = await _context.TenantConfigs.FirstOrDefaultAsync();
            if (config == null) return NotFound(new { statusCode = 404, message = "Global configuration not found." });

            var result = new { isEnabled = false, ips = new List<IpWhitelistEntry>() };
            
            if (!string.IsNullOrEmpty(config.IpWhitelist))
            {
                try
                {
                   // We actually store List<IpWhitelistEntry> into TenantConfig.IpWhitelist now
                   var ips = JsonSerializer.Deserialize<List<IpWhitelistEntry>>(config.IpWhitelist);
                   if (ips != null)
                   {
                       result = new 
                       {
                           isEnabled = ips.Count > 0, // Simplified: if list has items, it's enabled
                           ips = ips
                       };
                   }
                } 
                catch 
                {
                   // Fallback for old string array
                   try 
                   {
                        var oldIps = JsonSerializer.Deserialize<List<string>>(config.IpWhitelist);
                        if (oldIps != null)
                        {
                            result = new {
                                isEnabled = oldIps.Count > 0,
                                ips = oldIps.Select(ip => new IpWhitelistEntry { Ip = ip, Note = "Migrated from old system", AddedBy = "System", Date = System.DateTime.UtcNow.ToString("dd/MM/yyyy") }).ToList()
                            };
                        }
                   } catch {}
                }
            }

            return Ok(new { statusCode = 200, data = result });
        }

        public class UpdateIpWhitelistRequest
        {
            public bool IsEnabled { get; set; }
            public List<IpWhitelistEntry>? Ips { get; set; }
        }

        [HttpPut("ip-whitelist")]
        public async Task<IActionResult> UpdateIpWhitelist([FromBody] UpdateIpWhitelistRequest request)
        {
            if (!await CurrentUserHasAdminAccessAsync())
            {
                return Forbid();
            }

            var config = await _context.TenantConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new Domain.Entities.TenantConfig { Id = System.Guid.NewGuid(), OrganizationName = "Default" };
                _context.TenantConfigs.Add(config);
            }

            var finalIps = request.IsEnabled && request.Ips != null ? request.Ips : new List<IpWhitelistEntry>();
            config.IpWhitelist = JsonSerializer.Serialize(finalIps);

            await _context.SaveChangesAsync();
            return Ok(new { statusCode = 200, message = "IP Whitelist updated successfully.", data = new { isEnabled = request.IsEnabled, ips = finalIps } });
        }

        [HttpGet("accessible-projects")]
        public async Task<IActionResult> GetAccessibleProjects()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { statusCode = 401, message = "Unauthorized." });
            }

            var accessibleProjectIds = await _authorization.GetAccessibleProjectIdsAsync(userId);
            var query = _context.Projects
                .Where(project => accessibleProjectIds.Contains(project.Id));

            var projects = await query
                .OrderBy(project => project.Name)
                .Select(project => new
                {
                    id = project.Id,
                    name = project.Name,
                    identifier = project.Identifier,
                    networkType = project.NetworkType,
                    status = project.Status ? "Active" : "Inactive"
                })
                .ToListAsync();

            return Ok(new
            {
                statusCode = 200,
                data = new
                {
                    canAccessAllProjects = !ProjectAccessPolicy.RestrictionsEnabled,
                    items = projects
                }
            });
        }

        private async Task<bool> CurrentUserHasAdminAccessAsync()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return false;
            }

            return await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId && user.IsActive && !user.IsDeleted)
                .SelectMany(user => user.UserRoles.Select(ur => ur.Role.Name))
                .AnyAsync(role => AdminAccessRoles.Contains(role.Trim().ToLowerInvariant()));
        }
    }
}
