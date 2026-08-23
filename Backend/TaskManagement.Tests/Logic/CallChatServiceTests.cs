using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class CallChatServiceTests
{
    [Fact]
    public async Task HistoryIsScopedToRoomAndCallSessionAndCreateReturnsClientId()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var userId = Guid.NewGuid();
        var roomId = "project:one:voice:general";
        var sessionId = Guid.NewGuid();

        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Users.Add(new User { Id = userId, Email = "caller@example.test", FullName = "Caller" });
            seed.CallChatMessages.Add(new CallChatMessage
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                CallSessionId = sessionId,
                SenderUserId = userId,
                Content = "same session",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            });
            seed.CallChatMessages.Add(new CallChatMessage
            {
                Id = Guid.NewGuid(),
                RoomId = roomId,
                CallSessionId = Guid.NewGuid(),
                SenderUserId = userId,
                Content = "other session",
                CreatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        await using var context = new ApplicationDbContext(options);
        var service = new CallChatService(context);
        var created = await service.CreateAsync(roomId, sessionId, userId, "Caller", "  canonical message  ", "client-1");
        var history = await service.GetHistoryAsync(roomId, sessionId, 100);

        created.Content.Should().Be("canonical message");
        created.ClientMessageId.Should().Be("client-1");
        history.Should().HaveCount(2);
        history.Should().OnlyContain(message => message.CallSessionId == sessionId);
    }
}
