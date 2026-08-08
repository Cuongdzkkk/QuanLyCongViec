using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.API.Hubs;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class ChatRealtimeIntegrationTests
{
    [Fact]
    public async Task MissingAndExpiredJwtConnectionsAreRejected()
    {
        await using var factory = new ChatApplicationFactory();
        var userId = Guid.NewGuid();
        await SeedUserAsync(factory, userId, active: true);

        await using var missing = CreateConnection(factory, accessToken: null);
        await missing.Invoking(connection => connection.StartAsync())
            .Should().ThrowAsync<Exception>();

        await using var expired = CreateConnection(
            factory,
            CreateToken(factory, userId, DateTime.UtcNow.AddMinutes(-5)));
        await expired.Invoking(connection => connection.StartAsync())
            .Should().ThrowAsync<Exception>();

        using var client = factory.CreateClient();
        var validToken = CreateToken(factory, userId);
        var hubQueryToken = await client.PostAsync(
            $"{ChatHub.Route}/negotiate?negotiateVersion=1&access_token={validToken}",
            content: null);
        hubQueryToken.StatusCode.Should().Be(HttpStatusCode.OK);
        var apiQueryToken = await client.GetAsync(
            $"/api/users/me?access_token={validToken}");
        apiQueryToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChannelJoinAndEventsAreAuthorizedAndGroupIsolated()
    {
        await using var factory = new ChatApplicationFactory();
        ChannelSeed seed;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            seed = await ChannelSeed.InsertAsync(context);
        }

        await using var sender = CreateConnection(factory, CreateToken(factory, seed.UserAId));
        await using var member = CreateConnection(factory, CreateToken(factory, seed.UserBId));
        await using var outsider = CreateConnection(factory, CreateToken(factory, seed.OutsiderId));
        await sender.StartAsync();
        await member.StartAsync();
        await outsider.StartAsync();

        await sender.InvokeAsync("JoinChannel", seed.ChannelAId.ToString());
        await member.InvokeAsync("JoinChannel", seed.ChannelAId.ToString());
        await outsider.Invoking(connection =>
                connection.InvokeAsync("JoinChannel", seed.ChannelAId.ToString()))
            .Should().ThrowAsync<HubException>()
            .WithMessage("*CHANNEL_NOT_FOUND_OR_FORBIDDEN*");
        await sender.Invoking(connection =>
                connection.InvokeAsync("JoinChannel", seed.ChannelBId.ToString()))
            .Should().ThrowAsync<HubException>()
            .WithMessage("*CHANNEL_NOT_FOUND_OR_FORBIDDEN*");
        await sender.Invoking(connection =>
                connection.InvokeAsync("JoinChannel", "not-a-guid"))
            .Should().ThrowAsync<HubException>()
            .WithMessage("*INVALID_ID*");

        var senderEvents = new List<ChannelMessageCreatedEventDto>();
        var memberEvents = new List<ChannelMessageCreatedEventDto>();
        var outsiderEvents = new List<ChannelMessageCreatedEventDto>();
        var senderReceived = NewSignal<ChannelMessageCreatedEventDto>();
        var memberReceived = NewSignal<ChannelMessageCreatedEventDto>();
        sender.On<ChannelMessageCreatedEventDto>(
            ChatRealtimeEvents.ChannelMessageCreated,
            payload =>
            {
                senderEvents.Add(payload);
                senderReceived.TrySetResult(payload);
            });
        member.On<ChannelMessageCreatedEventDto>(
            ChatRealtimeEvents.ChannelMessageCreated,
            payload =>
            {
                memberEvents.Add(payload);
                memberReceived.TrySetResult(payload);
            });
        outsider.On<ChannelMessageCreatedEventDto>(
            ChatRealtimeEvents.ChannelMessageCreated,
            payload => outsiderEvents.Add(payload));

        using var client = CreateAuthenticatedClient(factory, seed.UserAId);
        var response = await client.PostAsJsonAsync(
            $"/api/channels/{seed.ChannelAId:D}/messages",
            new { content = "persist before channel event" });
        response.EnsureSuccessStatusCode();

        var senderEvent = await senderReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var memberEvent = await memberReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(250);

        senderEvent.Should().BeEquivalentTo(memberEvent);
        senderEvent.ChannelId.Should().Be(seed.ChannelAId);
        senderEvent.Sender.UserId.Should().Be(seed.UserAId);
        senderEvents.Should().ContainSingle();
        memberEvents.Should().ContainSingle();
        outsiderEvents.Should().BeEmpty();
        SecurityFields(senderEvent.Sender.GetType()).Should().BeEmpty();

        using var outsiderClient = CreateAuthenticatedClient(factory, seed.OutsiderId);
        (await outsiderClient.PostAsJsonAsync(
            $"/api/channels/{seed.ChannelAId:D}/messages",
            new { content = "forbidden channel send" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        await Task.Delay(150);
        senderEvents.Should().ContainSingle();
        memberEvents.Should().ContainSingle();

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyContext.ChannelMessages.SingleAsync())
            .Id.Should().Be(senderEvent.MessageId);
    }

    [Fact]
    public async Task DirectConversationJoinAndEventsAreParticipantAndGroupIsolated()
    {
        await using var factory = new ChatApplicationFactory();
        DirectSeed seed;
        Guid conversationAb;
        Guid conversationAc;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            seed = await DirectSeed.InsertAsync(context);
            var service = new DirectConversationService(context);
            conversationAb = (await service.FindOrCreateAsync(seed.UserAId, seed.UserBId))
                .ConversationId;
            conversationAc = (await service.FindOrCreateAsync(seed.UserAId, seed.UserCId))
                .ConversationId;
        }

        await using var userA = CreateConnection(factory, CreateToken(factory, seed.UserAId));
        await using var userB = CreateConnection(factory, CreateToken(factory, seed.UserBId));
        await using var userC = CreateConnection(factory, CreateToken(factory, seed.UserCId));
        await userA.StartAsync();
        await userB.StartAsync();
        await userC.StartAsync();

        await userA.InvokeAsync("JoinDirectConversation", conversationAb.ToString());
        await userB.InvokeAsync("JoinDirectConversation", conversationAb.ToString());
        await userC.InvokeAsync("JoinDirectConversation", conversationAc.ToString());
        await userC.Invoking(connection =>
                connection.InvokeAsync("JoinDirectConversation", conversationAb.ToString()))
            .Should().ThrowAsync<HubException>()
            .WithMessage("*CONVERSATION_NOT_FOUND_OR_FORBIDDEN*");

        var aEvents = new List<DirectMessageCreatedEventDto>();
        var bEvents = new List<DirectMessageCreatedEventDto>();
        var cEvents = new List<DirectMessageCreatedEventDto>();
        var aReceived = NewSignal<DirectMessageCreatedEventDto>();
        var bReceived = NewSignal<DirectMessageCreatedEventDto>();
        userA.On<DirectMessageCreatedEventDto>(
            ChatRealtimeEvents.DirectMessageCreated,
            payload =>
            {
                aEvents.Add(payload);
                aReceived.TrySetResult(payload);
            });
        userB.On<DirectMessageCreatedEventDto>(
            ChatRealtimeEvents.DirectMessageCreated,
            payload =>
            {
                bEvents.Add(payload);
                bReceived.TrySetResult(payload);
            });
        userC.On<DirectMessageCreatedEventDto>(
            ChatRealtimeEvents.DirectMessageCreated,
            payload => cEvents.Add(payload));

        using var client = CreateAuthenticatedClient(factory, seed.UserAId);
        var response = await client.PostAsJsonAsync(
            $"/api/direct-conversations/{conversationAb:D}/messages",
            new { content = "persist before dm event" });
        response.EnsureSuccessStatusCode();

        var eventA = await aReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var eventB = await bReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(250);

        eventA.Should().BeEquivalentTo(eventB);
        eventA.ConversationId.Should().Be(conversationAb);
        eventA.Sender.UserId.Should().Be(seed.UserAId);
        aEvents.Should().ContainSingle();
        bEvents.Should().ContainSingle();
        cEvents.Should().BeEmpty();
        SecurityFields(eventA.Sender.GetType()).Should().BeEmpty();

        using var forbiddenClient = CreateAuthenticatedClient(factory, seed.UserCId);
        (await forbiddenClient.PostAsJsonAsync(
            $"/api/direct-conversations/{conversationAb:D}/messages",
            new { content = "forbidden dm send" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        await Task.Delay(150);
        aEvents.Should().ContainSingle();
        bEvents.Should().ContainSingle();

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyContext.DirectMessages.SingleAsync())
            .Id.Should().Be(eventA.MessageId);
    }

    [Fact]
    public async Task InactiveUserCannotConnectAndReconnectMustJoinAgain()
    {
        await using var factory = new ChatApplicationFactory();
        ChannelSeed seed;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            seed = await ChannelSeed.InsertAsync(context);
        }

        await using var inactive = CreateConnection(
            factory,
            CreateToken(factory, seed.InactiveUserId));
        await inactive.Invoking(connection => connection.StartAsync())
            .Should().ThrowAsync<Exception>();

        await using var member = CreateConnection(factory, CreateToken(factory, seed.UserBId));
        var events = new List<ChannelMessageCreatedEventDto>();
        var rejoined = NewSignal<ChannelMessageCreatedEventDto>();
        member.On<ChannelMessageCreatedEventDto>(
            ChatRealtimeEvents.ChannelMessageCreated,
            payload =>
            {
                events.Add(payload);
                rejoined.TrySetResult(payload);
            });
        await member.StartAsync();
        await member.InvokeAsync("JoinChannel", seed.ChannelAId.ToString());
        await member.StopAsync();
        await member.StartAsync();

        using var client = CreateAuthenticatedClient(factory, seed.UserAId);
        (await client.PostAsJsonAsync(
            $"/api/channels/{seed.ChannelAId:D}/messages",
            new { content = "not delivered before rejoin" }))
            .EnsureSuccessStatusCode();
        await Task.Delay(250);
        events.Should().BeEmpty();

        await member.InvokeAsync("JoinChannel", seed.ChannelAId.ToString());
        (await client.PostAsJsonAsync(
            $"/api/channels/{seed.ChannelAId:D}/messages",
            new { content = "delivered after rejoin" }))
            .EnsureSuccessStatusCode();
        (await rejoined.Task.WaitAsync(TimeSpan.FromSeconds(5)))
            .Content.Should().Be("delivered after rejoin");
        events.Should().ContainSingle();
    }

    private static HubConnection CreateConnection(
        ChatApplicationFactory factory,
        string? accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, ChatHub.Route),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    if (accessToken != null)
                        options.AccessTokenProvider = () => Task.FromResult(accessToken)!;
                })
            .Build();

    private static HttpClient CreateAuthenticatedClient(
        ChatApplicationFactory factory,
        Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(factory, userId));
        return client;
    }

    private static async Task SeedUserAsync(
        ChatApplicationFactory factory,
        Guid userId,
        bool active)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Users.Add(new TaskManagement.Domain.Entities.User
        {
            Id = userId,
            Email = $"{userId:N}@sprinta.test",
            FullName = "Realtime User",
            PasswordHash = "test-only",
            IsActive = active,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static string CreateToken(
        ChatApplicationFactory factory,
        Guid userId,
        DateTime? expires = null)
    {
        var options = factory.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var validation = options.TokenValidationParameters;
        var credentials = new SigningCredentials(
            validation.IssuerSigningKey,
            SecurityAlgorithms.HmacSha256);
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(10);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: validation.ValidIssuer,
            audience: validation.ValidAudience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            ],
            notBefore: expiresAt.AddMinutes(-10),
            expires: expiresAt,
            signingCredentials: credentials));
    }

    private static TaskCompletionSource<T> NewSignal<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static IEnumerable<string> SecurityFields(Type type) =>
        type.GetProperties()
            .Select(property => property.Name)
            .Where(name => name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Provider", StringComparison.OrdinalIgnoreCase));
}

public sealed class ChatApplicationFactory : WebApplicationFactory<Program>
{
    public const string JwtIssuer = "SprintA-Realtime-Tests";
    public const string JwtAudience = "SprintA-Realtime-Client";
    private readonly string _databaseName = $"chat-realtime-{Guid.NewGuid():N}";
    private readonly string _jwtSecret =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = _jwtSecret,
                ["Jwt:Issuer"] = JwtIssuer,
                ["Jwt:Audience"] = JwtAudience,
                ["Security:RequireHttpsMetadata"] = "false",
                ["Database:Provider"] = "InMemory",
                ["Database:InMemoryName"] = _databaseName,
                ["Features:AIEnabled"] = "false",
                ["Google:Enabled"] = "false",
                ["OpenApi:Enabled"] = "false",
                ["DataProtection:KeysPath"] = Path.Combine(
                    Path.GetTempPath(),
                    $"sprinta-realtime-keys-{Guid.NewGuid():N}")
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
