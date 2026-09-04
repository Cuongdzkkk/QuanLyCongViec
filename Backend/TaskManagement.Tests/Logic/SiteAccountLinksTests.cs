using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Tests.Logic;

public sealed class SiteAccountLinksTests
{
    [Fact]
    public async Task RequestLink_PersistsPendingRequestAndSendsWebNotificationAndEmail()
    {
        await using var context = CreateContext();
        var requester = NewUser("requester@example.com", "Requester");
        var target = NewUser("owner@example.com", "Owner");
        context.Users.AddRange(requester, target);
        await context.SaveChangesAsync();
        var notifications = new Mock<INotificationService>();
        var email = new Mock<IEmailService>();
        var controller = CreateController(context, requester.Id, notifications.Object, email.Object);

        var result = await controller.RequestLink(new SiteAccountLinkRequestDto { Email = "OWNER@example.com" });

        result.Should().BeOfType<OkObjectResult>();
        var link = await context.SiteAccountLinkRequests.SingleAsync();
        link.RequesterUserId.Should().Be(requester.Id);
        link.TargetUserId.Should().Be(target.Id);
        link.Status.Should().Be("Pending");
        notifications.Verify(service => service.SendNotificationAsync(
            target.Id,
            It.IsAny<string>(),
            It.IsAny<string>(),
            "SITE_ACCOUNT_LINK_REQUEST",
            "/home/notifications",
            requester.Id,
            link.Id), Times.Once);
        email.Verify(service => service.SendInviteEmailAsync(
            target.Email,
            target.FullName,
            requester.FullName,
            "SprintA",
            null,
            It.Is<string>(url => url.EndsWith("/home/notifications", StringComparison.Ordinal)),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task AcceptLink_MakesTheOwnerAccountVisibleButDoesNotGrantChildWorkspaceAccess()
    {
        await using var context = CreateContext();
        var requester = NewUser("requester@example.com", "Requester");
        var target = NewUser("owner@example.com", "Owner");
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Private child site",
            Slug = "private-child-site",
            OwnerId = target.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var link = new SiteAccountLinkRequest
        {
            Id = Guid.NewGuid(),
            RequesterUserId = requester.Id,
            TargetUserId = target.Id,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        context.Users.AddRange(requester, target);
        context.Workspaces.Add(workspace);
        context.SiteAccountLinkRequests.Add(link);
        await context.SaveChangesAsync();
        var controller = CreateController(context, target.Id, Mock.Of<INotificationService>(), Mock.Of<IEmailService>());

        var result = await controller.Accept(link.Id);

        result.Should().BeOfType<OkObjectResult>();
        (await context.SiteAccountLinkRequests.SingleAsync()).Status.Should().Be("Accepted");
        (await context.WorkspaceMembers.CountAsync(member => member.UserId == requester.Id)).Should().Be(0);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static SiteAccountLinksController CreateController(
        ApplicationDbContext context,
        Guid userId,
        INotificationService notifications,
        IEmailService email)
    {
        var controller = new SiteAccountLinksController(
            context,
            notifications,
            email,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Frontend:BaseUrl"] = "http://localhost:5173"
                })
                .Build());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    "TestAuth"))
            }
        };
        return controller;
    }

    private static User NewUser(string email, string fullName) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        FullName = fullName,
        PasswordHash = "unused",
        IsActive = true
    };
}
