using System.Net;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.AI;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class GmailIntegrationSyncTests
{
    [Fact]
    public async Task SyncGmailPersistsFullBodyAndNormalizedAttachmentForCandidateAnalysis()
    {
        const string externalId = "gmail-item-1";
        const string subject = "[SprintA Attachment Test] Yêu cầu nằm trong file TXT";
        const string body = "Công việc chi tiết nằm trong file đính kèm, hãy đọc file để xử lý.";
        const string attachmentName = "sprinta_test_requirements.txt";
        const string attachmentText = "Task title:\nFix AI task creation confirmation flow\n\nPriority:\nHigh\n\nDue date:\n2026-09-07 17:00";
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/messages/" + externalId, StringComparison.Ordinal)
            ? JsonResponse($"{{\"id\":\"{externalId}\",\"snippet\":\"snippet-only\",\"payload\":{{\"mimeType\":\"multipart/mixed\",\"headers\":[{{\"name\":\"Subject\",\"value\":\"{subject}\"}},{{\"name\":\"From\",\"value\":\"sender@example.com\"}}],\"parts\":[{{\"mimeType\":\"text/plain\",\"filename\":\"\",\"body\":{{\"data\":\"{Encode(body)}\",\"size\":{Encoding.UTF8.GetByteCount(body)}}}}},{{\"mimeType\":\"text/plain\",\"filename\":\"{attachmentName}\",\"body\":{{\"data\":\"{Encode(attachmentText)}\",\"size\":{Encoding.UTF8.GetByteCount(attachmentText)}}}}}]}}}}")
            : JsonResponse($"{{\"messages\":[{{\"id\":\"{externalId}\"}}]}}"));

        var (controller, context) = CreateController(handler);
        var result = await controller.SyncGmail();

        result.Should().BeOfType<OkObjectResult>();
        var item = await context.InboxItems.SingleAsync();
        item.Content.Should().Contain("SOURCE: Gmail body\n" + body);
        item.Content.Should().Contain("SOURCE: gmail/attachment/" + attachmentName);
        item.Content.Should().Contain("TYPE: text/plain");

        var candidate = AiTaskCandidateParser.ExtractStructuredCandidate(item.Content!, "gmail", item.Id);
        candidate.Should().NotBeNull();
        candidate!.Title.Should().Be("Fix AI task creation confirmation flow");
        candidate.Priority.Should().Be(2);
        candidate.DueDate.Should().Be("2026-09-07T17:00:00");
        candidate.AttachmentFileName.Should().Be(attachmentName);
        candidate.Evidence.Should().Contain(evidence => evidence.AttachmentFileName == attachmentName && evidence.Type == "Extracted");
    }

    private static (IntegrationsController Controller, ApplicationDbContext Context) CreateController(HttpMessageHandler handler)
    {
        var userId = Guid.NewGuid();
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var protector = new EphemeralDataProtectionProvider();
        context.IntegrationAccounts.Add(new IntegrationAccount
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = "gmail",
            AccountEmail = "gmail@example.test",
            AccessToken = protector.CreateProtector("SprintA.IntegrationTokens").Protect("access-token"),
            IsActive = true,
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CreatedAt = DateTime.UtcNow
        });
        context.Users.Add(new User { Id = userId, Email = "gmail-user@example.test", IsActive = true });
        context.SaveChanges();

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        var controller = new IntegrationsController(
            context,
            new ConfigurationBuilder().Build(),
            factory.Object,
            protector,
            Mock.Of<IGoogleCalendarIntegrationService>(),
            new OAuthStateStore(),
            new AttachmentIngestionService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"))
                }
            }
        };
        return (controller, context);
    }

    private static string Encode(string value)
        => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(value));

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
