using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskManagement.Application.Common;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Data;

namespace TaskManagement.Tests.Logic;

public sealed class AiProviderHttpContractTests
{
    [Fact]
    public async Task RateLimitedProviderException_UsesPublic503Contract()
    {
        await using var factory = new AiProviderContractApplicationFactory(
            new AiProviderException(AiProviderErrorKind.RateLimited, retryAfterSeconds: 17));
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/ai/chat", new { message = "test" });
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Headers.GetValues("Retry-After").Single().Should().Be("17");
        body.RootElement.GetProperty("data").GetProperty("code").GetString()
            .Should().Be("AI_PROVIDER_RATE_LIMITED");
        body.RootElement.GetProperty("data").GetProperty("retryAfterSeconds").GetInt32()
            .Should().Be(17);
        body.RootElement.GetProperty("message").GetString()
            .Should().Be("Dịch vụ AI đang bận. Vui lòng thử lại sau.");
        body.RootElement.ToString().Should().NotContain("provider-secret");
    }

    [Fact]
    public async Task UnavailableProviderException_UsesPublic503Contract()
    {
        await using var factory = new AiProviderContractApplicationFactory(
            new AiProviderException(AiProviderErrorKind.Unavailable));
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/ai/chat", new { message = "test" });
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        body.RootElement.GetProperty("data").GetProperty("code").GetString()
            .Should().Be("AI_PROVIDER_UNAVAILABLE");
        body.RootElement.GetProperty("message").GetString()
            .Should().Be("Dịch vụ AI tạm thời không khả dụng. Vui lòng thử lại sau.");
        body.RootElement.ToString().Should().NotContain("provider-secret");
    }
}

public sealed class AiProviderContractApplicationFactory(AiProviderException providerException)
    : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"ai-provider-contract-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["Database:Provider"] = "InMemory",
                ["Database:InMemoryName"] = _databaseName,
                ["Features:AIEnabled"] = "false",
                ["Google:Enabled"] = "false",
                ["OpenApi:Enabled"] = "false",
                ["DataProtection:KeysPath"] = Path.Combine(
                    Path.GetTempPath(),
                    $"sprinta-ai-provider-keys-{Guid.NewGuid():N}")
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IAiService>();
            var aiService = new Moq.Mock<IAiService>();
            aiService
                .Setup(service => service.ChatAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<AiChatRequestDto>()))
                .ThrowsAsync(providerException);
            services.AddSingleton(aiService.Object);

            services.AddAuthentication(options => options.DefaultAuthenticateScheme = "Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        });
    }
}

internal sealed class TestAuthenticationHandler(
    Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
    Microsoft.Extensions.Logging.ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
