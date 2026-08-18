using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class EmailServiceTests
{
    [Fact]
    public async Task SendInviteEmailAsync_PostsResendRequestWithRecipientSenderAndInvitationLink()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "email-test-id" })
            };
        });
        var service = CreateService(handler);

        await service.SendInviteEmailAsync(
            "invitee@example.com",
            "Invitee",
            "Owner",
            "SprintA",
            "Project Alpha",
            "https://app.example.com/accept-invite?token=abc123",
            "Welcome aboard");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().Be("https://api.resend.com/emails");
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization.Parameter.Should().Be("test-api-key");

        var body = JsonDocument.Parse(await capturedRequest.Content!.ReadAsStringAsync()).RootElement;
        body.GetProperty("from").GetString().Should().Be("SprintA <noreply@example.com>");
        body.GetProperty("to")[0].GetString().Should().Be("invitee@example.com");
        body.GetProperty("subject").GetString().Should().Contain("Project Alpha");
        body.GetProperty("html").GetString().Should().Contain("accept-invite?token=abc123");
        body.GetProperty("html").GetString().Should().Contain("Welcome aboard");
    }

    [Fact]
    public async Task SendInviteEmailAsync_ThrowsWhenResendRejectsRequest()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("invalid sender")
        });
        var service = CreateService(handler);

        var act = () => service.SendInviteEmailAsync(
            "invitee@example.com",
            "Invitee",
            "Owner",
            "SprintA",
            "Project Alpha",
            "https://app.example.com/accept-invite?token=abc123",
            null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Resend returned*");
    }

    private static EmailService CreateService(HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resend:ApiKey"] = "test-api-key",
                ["Resend:FromEmail"] = "noreply@example.com"
            })
            .Build();

        return new EmailService(configuration, new HttpClient(handler));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(_handler(request));
    }
}
