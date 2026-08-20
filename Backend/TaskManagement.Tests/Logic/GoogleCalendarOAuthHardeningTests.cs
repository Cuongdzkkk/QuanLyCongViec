using System.Net;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class GoogleCalendarOAuthHardeningTests
{
    [Fact]
    public void OAuthStateStore_ConsumesOnlyOnceAndBindsUserAndProvider()
    {
        var store = new OAuthStateStore();
        var nonce = "nonce-a";
        var userA = Guid.NewGuid();
        store.Store(nonce, userA, "google-calendar", "verifier", DateTime.UtcNow.AddMinutes(5));

        store.TryConsume(nonce, Guid.NewGuid(), "google-calendar", out _).Should().BeFalse();
        store.TryConsume(nonce, userA, "gmail", out _).Should().BeFalse();
        store.TryConsume(nonce, userA, "google-calendar", out var verifier).Should().BeTrue();
        verifier.Should().Be("verifier");
        store.TryConsume(nonce, userA, "google-calendar", out _).Should().BeFalse();
    }

    [Fact]
    public void OAuthStateStore_RejectsExpiredState()
    {
        var store = new OAuthStateStore();
        var userId = Guid.NewGuid();
        store.Store("expired", userId, "google-calendar", "verifier", DateTime.UtcNow.AddSeconds(-1));

        store.TryConsume("expired", userId, "google-calendar", out _).Should().BeFalse();
    }

    [Fact]
    public void AuthorizationUrl_ContainsPkceChallengeWithoutCredentialsInBrowserState()
    {
        var verifier = GoogleCalendarIntegrationService.CreateCodeVerifier();
        var challenge = GoogleCalendarIntegrationService.CreateCodeChallenge(verifier);
        var service = new GoogleCalendarIntegrationService(Mock.Of<IHttpClientFactory>());

        var url = service.BuildAuthorizationUrl("client-id", "https://api.example.test/callback", "protected-state", challenge);

        url.Should().Contain("code_challenge=");
        url.Should().Contain("code_challenge_method=S256");
        url.Should().Contain("state=protected-state");
        url.Should().NotContain("client-secret");
    }

    [Fact]
    public async Task RefreshResponseWithoutRefreshToken_PreservesNullAndMapsSafePayload()
    {
        var handler = new StubHandler(_ => JsonResponse("{\"access_token\":\"new-access\",\"expires_in\":3600}"));
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("GoogleCalendar")).Returns(new HttpClient(handler));
        var service = new GoogleCalendarIntegrationService(factory.Object);

        var result = await service.RefreshAccessTokenAsync("client", "secret", "refresh");

        result.AccessToken.Should().Be("new-access");
        result.RefreshToken.Should().BeNull();
        (await handler.LastRequest!.Content!.ReadAsStringAsync()).Should().Contain("refresh_token=refresh");
    }

    [Fact]
    public async Task InvalidGrant_IsMappedToReconnectRequiredWithoutProviderBody()
    {
        var handler = new StubHandler(_ => JsonResponse("{\"error\":\"invalid_grant\",\"error_description\":\"secret provider detail\"}", HttpStatusCode.BadRequest));
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("GoogleCalendar")).Returns(new HttpClient(handler));
        var service = new GoogleCalendarIntegrationService(factory.Object);

        var action = () => service.RefreshAccessTokenAsync("client", "secret", "refresh");

        var exception = await action.Should().ThrowAsync<GoogleProviderException>();
        exception.Which.ReconnectRequired.Should().BeTrue();
        exception.Which.Message.Should().NotContain("secret provider detail");
    }

    [Fact]
    public void ConnectGoogleCalendar_RejectsDisabledConfiguration()
    {
        var userId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IntegrationOAuth:GoogleCalendar:Enabled"] = "false",
                ["IntegrationOAuth:GoogleCalendar:ClientId"] = "client-id",
                ["IntegrationOAuth:GoogleCalendar:ClientSecret"] = "server-secret",
                ["IntegrationOAuth:GoogleCalendar:RedirectUri"] = "https://api.example.test/callback"
            })
            .Build();
        var controller = CreateController(configuration, userId);

        var result = controller.ConnectGoogleCalendar();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void TokenDecryption_DoesNotAcceptPlaintextFallback()
    {
        var controller = CreateController(new ConfigurationBuilder().Build(), Guid.NewGuid());
        var decrypt = typeof(IntegrationsController).GetMethod("DecryptToken", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        var action = () => decrypt.Invoke(controller, new object?[] { "legacy-plaintext-token" });

        action.Should().Throw<TargetInvocationException>()
            .Which.InnerException.Should().BeOfType<CryptographicException>();
    }

    [Fact]
    public async Task GoogleCalendarCallback_RejectsDisabledConfigurationBeforeExchangeOrPersistence()
    {
        var userId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IntegrationOAuth:GoogleCalendar:Enabled"] = "false",
                ["IntegrationOAuth:GoogleCalendar:ClientId"] = "client-id",
                ["IntegrationOAuth:GoogleCalendar:ClientSecret"] = "server-secret",
                ["IntegrationOAuth:GoogleCalendar:RedirectUri"] = "https://api.example.test/callback"
            })
            .Build();
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        context.Users.Add(new User { Id = userId, Email = "oauth@example.test", IsActive = true });
        await context.SaveChangesAsync();

        var dataProtection = new EphemeralDataProtectionProvider();
        var provider = new Mock<IGoogleCalendarIntegrationService>();
        var store = new OAuthStateStore();
        var controller = new IntegrationsController(
            context,
            configuration,
            Mock.Of<IHttpClientFactory>(),
            dataProtection,
            provider.Object,
            store);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request = { Headers = { Cookie = "sprinta_google_oauth_nonce=nonce-disabled" } },
                User = new ClaimsPrincipal(new ClaimsIdentity("test"))
            }
        };

        var expiresAt = DateTime.UtcNow.AddMinutes(5);
        store.Store("nonce-disabled", userId, "google-calendar", "verifier", expiresAt);
        var state = dataProtection.CreateProtector("SprintA.IntegrationOAuthState.v1").Protect(JsonSerializer.Serialize(new
        {
            userId,
            provider = "google-calendar",
            nonce = "nonce-disabled",
            createdAt = DateTime.UtcNow,
            expiresAt
        }));

        var result = await controller.GoogleCalendarCallback("authorization-code", state, null, null);

        result.Should().BeOfType<RedirectResult>();
        ((RedirectResult)result).Url.Should().Contain("connected=error");
        provider.Verify(service => service.ExchangeCodeAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        context.IntegrationAccounts.Should().BeEmpty();
    }

    private static IntegrationsController CreateController(IConfiguration configuration, Guid userId)
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        var controller = new IntegrationsController(
            context,
            configuration,
            Mock.Of<IHttpClientFactory>(),
            new EphemeralDataProtectionProvider(),
            Mock.Of<IGoogleCalendarIntegrationService>(),
            new OAuthStateStore());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"))
            }
        };
        return controller;
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }
}
