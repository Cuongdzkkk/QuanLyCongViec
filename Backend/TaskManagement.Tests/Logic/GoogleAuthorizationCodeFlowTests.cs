using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TaskManagement.API.Controllers;
using TaskManagement.Application.DTOs.Auth;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class GoogleAuthorizationCodeFlowTests
{
    private const string Origin = "https://app.example.test";

    [Fact]
    public void Start_StoresOneTimeStateAndReturnsNoProviderSecret()
    {
        var stateStore = new CapturingStateStore();
        var controller = CreateController(
            Configuration(),
            Mock.Of<IAuthService>(),
            Mock.Of<IGoogleAuthorizationCodeExchange>(),
            stateStore);

        var result = controller.StartGoogleAuthorizationCodeLogin();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = JsonSerializer.Serialize(ok.Value);
        payload.Should().Contain("state");
        payload.Should().NotContain("server-client-secret");
        stateStore.StoredState.Should().NotBeNullOrWhiteSpace();
        controller.Response.Headers.SetCookie.ToString().Should().Contain("sprinta_google_login_state=");
    }

    [Fact]
    public async Task ConfiguredCode_UsesServerExchangeAndExistingGoogleLoginPipeline()
    {
        var auth = new Mock<IAuthService>();
        var userId = Guid.NewGuid();
        auth.Setup(item => item.GoogleLoginAsync(It.IsAny<GoogleLoginRequestDto>()))
            .ReturnsAsync((new AuthResponseDto
            {
                AccessToken = "existing-access-token",
                Id = userId,
                Email = "google@example.test",
                FullName = "Google User"
            }, "existing-refresh-token"));
        var exchange = new Mock<IGoogleAuthorizationCodeExchange>();
        exchange.Setup(item => item.ExchangeAsync(
                "authorization-code",
                "client-id.apps.googleusercontent.com",
                "server-client-secret",
                Origin,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("verified-id-token");
        var stateStore = new CapturingStateStore { ConsumeResult = true };
        var controller = CreateController(Configuration(), auth.Object, exchange.Object, stateStore);
        controller.HttpContext.Request.Headers.Cookie = $"sprinta_google_login_state={stateStore.State}";

        var result = await controller.GoogleAuthorizationCodeLogin(new GoogleAuthorizationCodeLoginRequestDto
        {
            Code = "authorization-code",
            State = stateStore.State
        });

        result.Should().BeOfType<OkObjectResult>();
        auth.Verify(item => item.GoogleLoginAsync(It.Is<GoogleLoginRequestDto>(request =>
            request.Credential == "verified-id-token")), Times.Once);
        exchange.VerifyAll();
        controller.Response.Headers.SetCookie.ToString().Should().Contain("refreshToken=");
    }

    [Fact]
    public async Task CodeLogin_RejectsMissingOrMismatchedStateBeforeProviderExchange()
    {
        var exchange = new Mock<IGoogleAuthorizationCodeExchange>(MockBehavior.Strict);
        var stateStore = new CapturingStateStore { ConsumeResult = true };
        var controller = CreateController(Configuration(), Mock.Of<IAuthService>(), exchange.Object, stateStore);
        controller.HttpContext.Request.Headers.Cookie = "sprinta_google_login_state=other-state";

        var result = await controller.GoogleAuthorizationCodeLogin(new GoogleAuthorizationCodeLoginRequestDto
        {
            Code = "authorization-code",
            State = stateStore.State
        });

        result.Should().BeOfType<UnauthorizedObjectResult>();
        exchange.VerifyNoOtherCalls();
        stateStore.ConsumeCalled.Should().BeFalse();
    }

    [Fact]
    public async Task CodeExchange_SendsSecretOnlyToGoogleTokenEndpointAndReturnsIdToken()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id_token\":\"verified-id-token\"}", System.Text.Encoding.UTF8, "application/json")
        });
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("GoogleAuth")).Returns(new HttpClient(handler));
        var service = new GoogleAuthorizationCodeExchange(factory.Object);

        var idToken = await service.ExchangeAsync("authorization-code", "client-id", "server-client-secret", Origin);

        idToken.Should().Be("verified-id-token");
        var requestBody = await handler.LastRequest!.Content!.ReadAsStringAsync();
        requestBody.Should().Contain("code=authorization-code");
        requestBody.Should().Contain("client_secret=server-client-secret");
        requestBody.Should().NotContain("id_token=server-client-secret");
    }

    private static AuthController CreateController(
        IConfiguration configuration,
        IAuthService auth,
        IGoogleAuthorizationCodeExchange exchange,
        IGoogleLoginOAuthStateStore stateStore)
    {
        var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Origin = Origin;
        httpContext.Request.Headers["X-Requested-With"] = "XmlHttpRequest";
        return new AuthController(
            auth,
            Mock.Of<IOtpService>(),
            Mock.Of<IEmailService>(),
            context,
            configuration,
            exchange,
            stateStore)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Google:Enabled"] = "true",
            ["Google:ClientId"] = "client-id.apps.googleusercontent.com",
            ["Google:ClientSecret"] = "server-client-secret",
            ["Google:RedirectUri"] = Origin,
            ["Frontend:BaseUrl"] = Origin,
            ["Cors:AllowedOrigins:0"] = Origin
        })
        .Build();

    private sealed class CapturingStateStore : IGoogleLoginOAuthStateStore
    {
        public string State { get; } = "state-from-server";
        public string? StoredState { get; private set; }
        public bool ConsumeResult { get; set; }
        public bool ConsumeCalled { get; private set; }

        public void Store(string state, DateTime expiresAt) => StoredState = state;

        public bool TryConsume(string state)
        {
            ConsumeCalled = true;
            return ConsumeResult && state == State;
        }
    }

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
