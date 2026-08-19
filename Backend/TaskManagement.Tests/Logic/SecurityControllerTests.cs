using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.API.Controllers;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Tests.Logic;

public sealed class SecurityControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;

    public SecurityControllerTests()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(dbOptions);
    }

    [Fact]
    public async Task GetCurrentIp_AdminUser_ReturnsRemoteIpAddress()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = userId,
            Email = "admin-current-ip@example.com",
            FullName = "Current IP Admin",
            PasswordHash = "unused",
            IsActive = true,
            IsDeleted = false
        });
        _context.Roles.Add(new Role
        {
            Id = roleId,
            Name = "admin"
        });
        _context.UserRoles.Add(new UserRole
        {
            UserId = userId,
            RoleId = roleId
        });
        await _context.SaveChangesAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.42");
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()) },
                "Test"));
        var controller = new SecurityController(_context)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var result = await controller.GetCurrentIp();

        result.Should().BeOfType<OkObjectResult>();
        var payload = ((OkObjectResult)result).Value;
        payload.Should().NotBeNull();
        payload!.GetType().GetProperty("ipAddress")!.GetValue(payload)
            .Should().Be("198.51.100.42");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
