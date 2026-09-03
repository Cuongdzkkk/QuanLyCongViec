using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.API.Hubs;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class CallCaptionSignalRJsonBindingTests
{
    [Fact]
    public void SubmitCallAudioChunk_keeps_the_original_eight_argument_contract()
    {
        var parameters = typeof(CallHub)
            .GetMethod(nameof(CallHub.SubmitCallAudioChunk))!
            .GetParameters();

        parameters.Length.Should().Be(8);
        parameters[0].ParameterType.Should().Be(typeof(string));
        parameters[1].ParameterType.Should().Be(typeof(string));
        parameters[2].ParameterType.Should().Be(typeof(long));
        parameters[3].ParameterType.Should().Be(typeof(string));
        parameters[4].ParameterType.Should().Be(typeof(byte[]));
        parameters[5].ParameterType.Should().Be(typeof(DateTimeOffset));
        parameters[6].ParameterType.Should().Be(typeof(DateTimeOffset));
        parameters[7].ParameterType.Should().Be(typeof(string));
        parameters[7].IsOptional.Should().BeTrue();
        parameters[7].DefaultValue.Should().BeNull();
    }

    [Fact]
    public async Task BrowserJsonProtocolBase64BindsToByteArrayAndNumericArrayDoesNot()
    {
        await using var factory = new CaptionSignalRApplicationFactory();
        var userId = Guid.NewGuid();
        await SeedUserAsync(factory, userId);

        using var client = factory.CreateClient();
        var token = CreateToken(factory, userId);
        var connection = await StartRawJsonConnectionAsync(client, token);
        var projectId = Guid.NewGuid();
        var roomId = $"project:{projectId:N}:voice:general";
        var registry = factory.Services.GetRequiredService<ICallRoomRegistry>();
        registry.Join(new CallRoomParticipant(
            roomId, connection.ConnectionId, userId, "Caption test user", null, true, false, false));
        var requested = registry.RequestAiTranscription(roomId, connection.ConnectionId);
        var active = registry.RespondToAiConsent(
            roomId,
            connection.ConnectionId,
            requested.CallSessionId,
            requested.ConsentGeneration,
            true);
        active.State.Should().Be(CallAiStates.Active);

        var original = Enumerable.Range(0, 8000)
            .Select(index => (byte)((index * 73 + 129) & 0xff))
            .ToArray();
        original.Should().Contain(byte.MaxValue);
        var browserBase64 = Convert.ToBase64String(original);
        var startedAt = "2026-08-29T05:00:00.000Z";
        var endedAt = "2026-08-29T05:00:00.250Z";

        var base64Frame = BuildInvocation(
            "base64-1",
            roomId,
            requested.CallSessionId,
            requested.ConsentGeneration,
            browserBase64,
            startedAt,
            endedAt);
        using (var base64Json = JsonDocument.Parse(base64Frame.TrimEnd('\u001e')))
        {
            var arguments = base64Json.RootElement.GetProperty("arguments");
            arguments.GetArrayLength().Should().Be(8);
            arguments[4].ValueKind.Should().Be(JsonValueKind.String);
            arguments[7].ValueKind.Should().Be(JsonValueKind.String);
        }

        await connection.SendAsync(base64Frame);
        var base64Completion = await connection.ReceiveAsync();
        base64Completion.Should().Contain("\"type\":3");
        base64Completion.Should().Contain("\"invocationId\":\"base64-1\"");

        var provider = factory.Services.GetRequiredService<RecordingCaptionProvider>();
        provider.Calls.Should().Be(1);
        provider.LastBytes.Should().NotBeNull();
        provider.LastBytes!.Length.Should().Be(original.Length);
        provider.LastBytes.Should().Equal(original);

        var numericFrame = BuildInvocation(
            "numeric-1",
            roomId,
            requested.CallSessionId,
            requested.ConsentGeneration,
            original.Select(value => (int)value).ToArray(),
            startedAt,
            endedAt);
        using (var numericJson = JsonDocument.Parse(numericFrame.TrimEnd('\u001e')))
        {
            numericJson.RootElement.GetProperty("arguments")[4].ValueKind
                .Should().Be(JsonValueKind.Array);
        }

        await connection.SendAsync(numericFrame);
        var numericCompletion = await connection.ReceiveAsync();
        numericCompletion.Should().Contain("\"type\":3");
        numericCompletion.Should().Contain("\"invocationId\":\"numeric-1\"");
        numericCompletion.Should().Contain("\"error\"");
        provider.Calls.Should().Be(1, "numeric JSON binding must fail before CallHub handler execution");

        var oldInvocationBytes = Encoding.UTF8.GetByteCount(numericFrame);
        var newInvocationBytes = Encoding.UTF8.GetByteCount(base64Frame);
        newInvocationBytes.Should().BeLessThan(oldInvocationBytes);

        Console.WriteLine("OLD_TRANSPORT_SHAPE=JSON arguments[4] numeric array");
        Console.WriteLine("NEW_TRANSPORT_SHAPE=JSON arguments[4] Base64 string");
        Console.WriteLine("OLD_NUMERIC_ARRAY_BINDS_TO_BYTE_ARRAY=NO");
        Console.WriteLine("BASE64_STRING_BINDS_TO_BYTE_ARRAY=YES");
        Console.WriteLine($"ORIGINAL_BYTES={original.Length}");
        Console.WriteLine($"DECODED_BYTES={provider.LastBytes.Length}");
        Console.WriteLine("BYTE_EQUALITY=PASS");
        Console.WriteLine($"OLD_INVOCATION_BYTES={oldInvocationBytes}");
        Console.WriteLine($"NEW_INVOCATION_BYTES={newInvocationBytes}");
    }

    private static string BuildInvocation(
        string invocationId,
        string roomId,
        Guid callSessionId,
        long consentGeneration,
        object audioPayload,
        string startedAt,
        string endedAt) =>
        JsonSerializer.Serialize(new
        {
            type = 1,
            invocationId,
            target = "SubmitCallAudioChunk",
            arguments = new object?[]
            {
                roomId,
                callSessionId.ToString("D"),
                consentGeneration,
                "audio/linear16;rate=16000;channels=1",
                audioPayload,
                startedAt,
                endedAt,
                "vi"
            }
        }) + '\u001e';

    private static async Task<RawJsonConnection> StartRawJsonConnectionAsync(HttpClient client, string token)
    {
        using var negotiate = await client.PostAsync(
            $"{CallHub.Route}/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(token)}",
            content: null);
        negotiate.StatusCode.Should().Be(HttpStatusCode.OK);
        using var negotiateJson = JsonDocument.Parse(await negotiate.Content.ReadAsStringAsync());
        var connectionId = negotiateJson.RootElement.GetProperty("connectionId").GetString();
        var connectionToken = negotiateJson.RootElement.GetProperty("connectionToken").GetString();
        connectionId.Should().NotBeNullOrWhiteSpace();
        connectionToken.Should().NotBeNullOrWhiteSpace();

        var url = $"{CallHub.Route}?id={Uri.EscapeDataString(connectionToken!)}&access_token={Uri.EscapeDataString(token)}";
        using var initialPoll = await client.GetAsync(url);
        initialPoll.StatusCode.Should().Be(HttpStatusCode.OK);

        var connection = new RawJsonConnection(client, url, connectionId!);
        await connection.SendAsync("{\"protocol\":\"json\",\"version\":1}\u001e");
        var handshake = await connection.ReceiveAsync();
        handshake.Should().Contain("{}");
        return connection;
    }

    private static async Task SeedUserAsync(CaptionSignalRApplicationFactory factory, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Users.Add(new User
        {
            Id = userId,
            Email = $"{userId:N}@caption.test",
            FullName = "Caption test user",
            PasswordHash = "test-only",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static string CreateToken(CaptionSignalRApplicationFactory factory, Guid userId)
    {
        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var credentials = new SigningCredentials(
            options.TokenValidationParameters.IssuerSigningKey!,
            SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(10);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: options.TokenValidationParameters.ValidIssuer,
            audience: options.TokenValidationParameters.ValidAudience,
            claims: [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires,
            signingCredentials: credentials));
    }

    private sealed class RawJsonConnection(HttpClient client, string url, string connectionId)
    {
        public string ConnectionId { get; } = connectionId;

        public async Task SendAsync(string frame)
        {
            using var content = new StringContent(frame, Encoding.UTF8, "text/plain");
            using var response = await client.PostAsync(url, content);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        public async Task<string> ReceiveAsync()
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await response.Content.ReadAsStringAsync();
        }
    }
}

public sealed class RecordingCaptionProvider : ICallTranscriptionProvider
{
    public int Calls { get; private set; }
    public byte[]? LastBytes { get; private set; }
    public bool IsConfigured => true;
    public string ProviderName => "CaptionTest";
    public IReadOnlyList<string> SupportedLanguages { get; } = ["vi", "en"];
    public string DefaultLanguage => "vi";

    public Task<CallTranscriptionResult?> TranscribeAsync(
        CallAudioChunk chunk,
        CancellationToken cancellationToken = default)
    {
        Calls++;
        LastBytes = chunk.AudioBytes.ToArray();
        return Task.FromResult<CallTranscriptionResult?>(null);
    }
}

public sealed class CaptionSignalRApplicationFactory : WebApplicationFactory<Program>
{
    public const string JwtIssuer = "SprintA-Caption-Tests";
    public const string JwtAudience = "SprintA-Caption-Client";
    private readonly string databaseName = $"caption-signalr-{Guid.NewGuid():N}";
    private readonly string jwtSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private readonly RecordingCaptionProvider provider = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = jwtSecret,
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Security:RequireHttpsMetadata"] = "false",
                ["Database:Provider"] = "InMemory",
                ["Database:InMemoryName"] = databaseName,
                ["Features:AIEnabled"] = "false",
                ["Google:Enabled"] = "false",
                ["OpenApi:Enabled"] = "false",
                ["DataProtection:KeysPath"] = Path.Combine(
                    Path.GetTempPath(),
                    $"sprinta-caption-keys-{Guid.NewGuid():N}")
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
            services.RemoveAll<ICallTranscriptionProvider>();
            services.AddSingleton(provider);
            services.AddSingleton<ICallTranscriptionProvider>(provider);
        });
    }
}
