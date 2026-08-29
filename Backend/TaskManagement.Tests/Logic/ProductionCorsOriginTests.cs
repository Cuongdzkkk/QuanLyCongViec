using System.Net;
using FluentAssertions;

namespace TaskManagement.Tests.Logic;

public sealed class ProductionCorsOriginTests
{
    public static IEnumerable<object[]> OfficialOriginsAndPreflightRoutes()
    {
        var routes = new[]
        {
            (Path: "/hubs/call/negotiate", Method: "POST"),
            (Path: "/hubs/chat/negotiate", Method: "POST"),
            (Path: "/notification-hub/negotiate", Method: "POST"),
            (Path: "/kanban-hub/negotiate", Method: "POST"),
            (Path: "/api/webrtc/ice-servers", Method: "GET"),
            (Path: "/api/projects/00000000-0000-0000-0000-000000000001/voice-channels/general/calls/capabilities", Method: "GET")
        };

        foreach (var origin in new[] { "https://sprinta.id.vn", "https://www.sprinta.id.vn" })
        foreach (var route in routes)
            yield return new object[] { origin, route.Path, route.Method };
    }

    [Theory]
    [MemberData(nameof(OfficialOriginsAndPreflightRoutes))]
    public async Task Cors_Preflight_AllowsBothOfficialOrigins(string origin, string path, string requestedMethod)
    {
        await using var factory = new PaymentHttpApplicationFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", requestedMethod);
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Single().Should().Be(origin);
        response.Headers.GetValues("Access-Control-Allow-Credentials").Single().Should().Be("true");
        response.Headers.GetValues("Access-Control-Allow-Methods").Single().Should().Contain(requestedMethod);
        response.Headers.GetValues("Access-Control-Allow-Headers").Single().Should().Contain("authorization");
    }

    [Fact]
    public async Task Cors_Preflight_RejectsUnknownOrigin()
    {
        await using var factory = new PaymentHttpApplicationFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/hubs/call/negotiate");
        request.Headers.Add("Origin", "https://unknown.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }
}
