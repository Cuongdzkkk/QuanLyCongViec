using System.Text.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.DTOs.Enterprise;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Tests.Logic;

public sealed class EnterpriseLeadFlowTests
{
    [Fact]
    public void AdminUpdateRequest_DeserializesStringStatus()
    {
        var request = JsonSerializer.Deserialize<UpdateEnterpriseLeadRequest>(
            "{\"status\":\"Contacted\"}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        request!.Status.Should().Be(EnterpriseLeadStatus.Contacted);
    }

    [Fact]
    public async Task ValidAnonymousLead_IsPersistedWithCanonicalEmail()
    {
        await using var context = CreateContext();
        var controller = CreatePublicController(context);

        var result = await controller.Create(new CreateEnterpriseLeadRequest
        {
            ContactName = "  Nguyen Van A ", WorkEmail = " SALES@Example.COM ", Company = " Acme ", TeamSize = "1–10"
        }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var lead = await context.EnterpriseLeads.SingleAsync();
        lead.ContactName.Should().Be("Nguyen Van A");
        lead.WorkEmail.Should().Be("sales@example.com");
        lead.Status.Should().Be(EnterpriseLeadStatus.New);
    }

    [Fact]
    public async Task NotificationFailure_DoesNotRollbackSavedLead()
    {
        await using var context = CreateContext();
        var admin = new User { Id = Guid.NewGuid(), Email = "admin@example.com", FullName = "Admin", PasswordHash = "x", IsActive = true };
        var role = new Role { Id = Guid.NewGuid(), Name = "Admin" };
        context.Users.Add(admin);
        context.Roles.Add(role);
        context.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = role.Id, User = admin, Role = role });
        await context.SaveChangesAsync();

        var notifications = new Mock<INotificationService>();
        notifications.Setup(service => service.SendNotificationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>()))
            .ThrowsAsync(new InvalidOperationException("notification unavailable"));
        var controller = CreatePublicController(context, notifications.Object);

        await controller.Create(new CreateEnterpriseLeadRequest
        {
            ContactName = "Sales Contact", WorkEmail = "sales@example.com", Company = "Acme", TeamSize = "11–50"
        }, CancellationToken.None);

        (await context.EnterpriseLeads.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("bad@")]
    public async Task InvalidEmail_IsRejected(string email)
    {
        await using var context = CreateContext();
        var controller = CreatePublicController(context);
        var request = new CreateEnterpriseLeadRequest { ContactName = "A", WorkEmail = email, Company = "Acme", TeamSize = "1–10" };
        controller.ModelState.AddModelError(nameof(request.WorkEmail), "Invalid email");

        var result = await controller.Create(request, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>();
        (await context.EnterpriseLeads.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AdminCanSearchFilterAndUpdateStatusWithAudit()
    {
        await using var context = CreateContext();
        var lead = new EnterpriseLead
        {
            Id = Guid.NewGuid(), ContactName = "A", WorkEmail = "a@example.com", Company = "Acme", TeamSize = "51-200",
            Status = EnterpriseLeadStatus.New, Source = "public-website", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.EnterpriseLeads.Add(lead);
        await context.SaveChangesAsync();
        var actorId = Guid.NewGuid();
        var controller = new AdminEnterpriseLeadsController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, actorId.ToString()) })) }
            }
        };

        var list = await controller.List("Acme", EnterpriseLeadStatus.New, cancellationToken: CancellationToken.None);
        list.Should().BeOfType<OkObjectResult>();
        var update = await controller.Update(lead.Id, new UpdateEnterpriseLeadRequest { Status = EnterpriseLeadStatus.Contacted, InternalNote = "Call next week" }, CancellationToken.None);

        update.Should().BeOfType<OkObjectResult>();
        (await context.EnterpriseLeads.FindAsync(lead.Id))!.Status.Should().Be(EnterpriseLeadStatus.Contacted);
        (await context.SystemAuditLogs.AnyAsync(log => log.Action == "EnterpriseLeadStatusChanged" && log.UserId == actorId)).Should().BeTrue();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static EnterpriseLeadsController CreatePublicController(ApplicationDbContext context, INotificationService? notifications = null)
    {
        return new EnterpriseLeadsController(context, notifications ?? Mock.Of<INotificationService>(), NullLogger<EnterpriseLeadsController>.Instance);
    }
}
