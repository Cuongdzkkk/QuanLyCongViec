using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TaskManagement.API.Controllers;

namespace TaskManagement.Tests.Logic;

public sealed class WebRtcControllerTests
{
    [Fact]
    public void IceEndpointRequiresAuthentication()
    {
        typeof(WebRtcController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Should().NotBeEmpty();
    }

    [Fact]
    public void StunOnlyConfigurationReturnsSafeResponse()
    {
        var controller = CreateController(new Dictionary<string, string?>
        {
            ["WebRtc:IceServers:0:Urls:0"] = "stun:stun.example.test:3478"
        });

        var response = controller.GetIceServers().Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(response.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Contain("stun:stun.example.test:3478");
        json.Should().NotContain("shared-secret");
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("iceServers")[0].GetProperty("credential").ValueKind
            .Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void TurnResponseContainsTemporaryCredentialAndExpiryOnly()
    {
        var userId = Guid.NewGuid();
        const string secret = "local-disposable-turn-secret";
        var controller = CreateController(new Dictionary<string, string?>
        {
            ["WebRtc:IceServers:0:Urls:0"] = "turn:turn.example.test:3478?transport=udp",
            ["WebRtc:IceServers:0:SharedSecret"] = secret,
            ["WebRtc:IceServers:0:CredentialTtlSeconds"] = "900"
        }, userId);

        var response = controller.GetIceServers().Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(response.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        json.Should().Contain("turn:turn.example.test:3478");
        json.Should().Contain("username").And.Contain("credential").And.Contain("expiresAt");
        json.Should().NotContain(secret);
        json.Should().NotContain("static-auth-secret");
    }

    [Fact]
    public void TurnWithoutSecretIsNotReturned()
    {
        var controller = CreateController(new Dictionary<string, string?>
        {
            ["WebRtc:IceServers:0:Urls:0"] = "turn:turn.example.test:3478?transport=tcp"
        });

        var response = controller.GetIceServers().Should().BeOfType<OkObjectResult>().Subject;
        JsonSerializer.Serialize(response.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            .Should().Be("{\"iceServers\":[]}");
    }

    private static WebRtcController CreateController(
        IDictionary<string, string?> values,
        Guid? userId = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var controller = new WebRtcController(configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, (userId ?? Guid.NewGuid()).ToString())], "test"))
                }
            }
        };
        return controller;
    }
}
