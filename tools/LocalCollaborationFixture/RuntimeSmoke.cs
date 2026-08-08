using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManagement.API.Hubs;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.DTOs.Common;
using TaskManagement.Infrastructure.Data;

namespace LocalCollaborationFixture;

internal sealed class RuntimeSmoke
{
    private readonly LocalApiFactory _factory;
    private readonly FixtureIdentity _identity;
    private readonly string _tokenA;
    private readonly string _tokenB;
    private readonly string _tokenC;

    public RuntimeSmoke(LocalApiFactory factory, FixtureIdentity identity)
    {
        _factory = factory;
        _identity = identity;
        _tokenA = factory.CreateToken(identity.UserAId);
        _tokenB = factory.CreateToken(identity.UserBId);
        _tokenC = factory.CreateToken(identity.UserCId);
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using var clientA = CreateClient(_tokenA);
        using var clientB = CreateClient(_tokenB);
        using var clientC = CreateClient(_tokenC);

        await CheckChannelRestAsync(clientA, clientB, clientC, cancellationToken);
        await CheckDirectRestAsync(clientA, clientB, clientC, cancellationToken);
        await CheckSignalRAsync(clientA, clientB, cancellationToken);
        await CheckMentionsAsync(clientA, clientB, clientC, cancellationToken);
        await CheckAttachmentsAsync(clientA, clientB, clientC, cancellationToken);
        await AssertDatabaseShapeAsync(cancellationToken);
    }

    private async Task CheckMentionsAsync(
        HttpClient clientA,
        HttpClient clientB,
        HttpClient clientC,
        CancellationToken cancellationToken)
    {
        await using var connectionB = CreateConnection(new TokenSlot(_tokenB));
        await using var connectionC = CreateConnection(new TokenSlot(_tokenC));
        var eventsB = new EventProbe<CollaborationMentionCreatedEventDto>();
        var eventsC = new EventProbe<CollaborationMentionCreatedEventDto>();
        connectionB.On<CollaborationMentionCreatedEventDto>(ChatRealtimeEvents.CollaborationMentionCreated, eventsB.Record);
        connectionC.On<CollaborationMentionCreatedEventDto>(ChatRealtimeEvents.CollaborationMentionCreated, eventsC.Record);
        await connectionB.StartAsync(cancellationToken);
        await connectionC.StartAsync(cancellationToken);

        var members = await GetDataAsync<List<ChannelMemberSuggestionDto>>(
            clientA.GetAsync($"/api/channels/{_identity.ChannelAId:D}/members?query={_identity.Prefix}&limit=20", cancellationToken),
            cancellationToken);
        Require(members.Select(item => item.UserId).ToHashSet().SetEquals([_identity.UserAId, _identity.UserBId]),
            "Mention member discovery leaked an outsider or omitted an active Channel member.");

        var tokenB = $"@{_identity.Prefix}-USER_B";
        var content = $"{tokenB} kiểm tra mention Unicode";
        var mention = new { userId = _identity.UserBId, startIndex = 0, length = tokenB.Length };
        var message = await PostDataAsync<ChannelMessageDto>(
            clientA,
            $"/api/channels/{_identity.ChannelAId:D}/messages",
            new { content, mentions = new[] { mention, mention } },
            cancellationToken);
        await WaitForAsync(
            () => eventsB.Count(item => item.MessageId == message.MessageId) == 1,
            "USER_B did not receive exactly one private mention event.",
            cancellationToken);
        Require(eventsC.Count(item => item.MessageId == message.MessageId) == 0,
            "USER_C received another user's private mention event.");
        Require(message.Mentions?.Single().UserId == _identity.UserBId,
            "Mention response lost persisted internal UserId metadata.");

        var history = await GetChannelHistoryAsync(clientB, 1, 50, cancellationToken);
        Require(history.Items.Single(item => item.MessageId == message.MessageId).Mentions?.Single().DisplayText == tokenB,
            "Mention metadata did not survive history reload.");
        using var freshB = CreateClient(_tokenB);
        var notificationsB = await GetDataAsync<List<FixtureNotificationDto>>(
            freshB.GetAsync("/api/notifications", cancellationToken), cancellationToken);
        Require(notificationsB.Count(item =>
                item.NotificationType == "collaboration_channel_mention" &&
                item.ChannelMessageId == message.MessageId &&
                item.CollaborationChannelId == _identity.ChannelAId) == 1,
            "USER_B reload did not return exactly one persisted mention notification.");

        var tokenC = $"@{_identity.Prefix}-USER_C";
        await RequireStatusAsync(
            await clientA.PostAsJsonAsync(
                $"/api/channels/{_identity.ChannelAId:D}/messages",
                new
                {
                    content = tokenC,
                    mentions = new[] { new { userId = _identity.UserCId, startIndex = 0, length = tokenC.Length } }
                },
                cancellationToken),
            HttpStatusCode.Forbidden,
            "USER_C forged mention");
        var notificationsC = await GetDataAsync<List<FixtureNotificationDto>>(
            clientC.GetAsync("/api/notifications", cancellationToken), cancellationToken);
        Require(notificationsC.All(item => item.NotificationType != "collaboration_channel_mention"),
            "USER_C received a mention notification despite being outside CHANNEL_A.");

        var tokenA = $"@{_identity.Prefix}-USER_A";
        var self = await PostDataAsync<ChannelMessageDto>(
            clientA,
            $"/api/channels/{_identity.ChannelAId:D}/messages",
            new
            {
                content = tokenA,
                mentions = new[] { new { userId = _identity.UserAId, startIndex = 0, length = tokenA.Length } }
            },
            cancellationToken);
        Require(self.Mentions?.Count == 0, "Self mention was not ignored by policy.");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userB = await context.Users.IgnoreQueryFilters().SingleAsync(item => item.Id == _identity.UserBId, cancellationToken);
            userB.IsActive = false;
            await context.SaveChangesAsync(cancellationToken);
            await RequireStatusAsync(
                await clientA.PostAsJsonAsync(
                    $"/api/channels/{_identity.ChannelAId:D}/messages",
                    new { content, mentions = new[] { mention } },
                    cancellationToken),
                HttpStatusCode.Forbidden,
                "inactive member mention");
            userB.IsActive = true;
            await context.SaveChangesAsync(cancellationToken);
        }

        Console.WriteLine("PASS Mentions: UserId identity, member discovery, SQL reload, private SignalR, duplicate/self/C/inactive policy");
    }

    private async Task CheckAttachmentsAsync(
        HttpClient clientA,
        HttpClient clientB,
        HttpClient clientC,
        CancellationToken cancellationToken)
    {
        await using var connectionB = CreateConnection(new TokenSlot(_tokenB));
        var channelEvents = new EventProbe<ChannelMessageCreatedEventDto>();
        connectionB.On<ChannelMessageCreatedEventDto>(
            ChatRealtimeEvents.ChannelMessageCreated,
            channelEvents.Record);
        await connectionB.StartAsync(cancellationToken);
        await connectionB.InvokeAsync("JoinChannel", _identity.ChannelAId, cancellationToken);

        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };
        var pdf = "%PDF-1.7\nfixture"u8.ToArray();
        var attachmentMentionToken = $"@{_identity.Prefix}-USER_B";
        var channelMessage = await SendAttachmentMessageAsync<ChannelMessageDto>(
            clientA,
            $"/api/channels/{_identity.ChannelAId:D}/messages",
            $"{attachmentMentionToken} {_identity.Prefix}-channel-attachments",
            [(png, "../Ảnh kiểm thử.png", "image/svg+xml"), (pdf, "fixture.pdf", "text/html")],
            cancellationToken,
            [new ChannelMessageMentionRequestDto
            {
                UserId = _identity.UserBId,
                StartIndex = 0,
                Length = attachmentMentionToken.Length
            }]);
        var channelAttachments = channelMessage.Attachments
            ?? throw new InvalidOperationException("Channel response omitted attachment metadata.");
        Require(channelAttachments.Count == 2, "Channel response did not include two attachment metadata records.");
        Require(channelMessage.Mentions?.Single().UserId == _identity.UserBId,
            "Channel attachment message lost mention metadata.");
        Require(channelAttachments.All(item => !item.OriginalFileName.Contains("..", StringComparison.Ordinal)),
            "Channel attachment filename was not sanitized.");
        await WaitForAsync(
            () => channelEvents.Count(item => item.MessageId == channelMessage.MessageId && item.Attachments?.Count == 2) == 1,
            "Channel realtime event did not carry safe attachment metadata.",
            cancellationToken);

        var channelHistory = await GetChannelHistoryAsync(clientB, 1, 50, cancellationToken);
        var persistedChannel = channelHistory.Items.Single(item => item.MessageId == channelMessage.MessageId);
        var persistedAttachments = persistedChannel.Attachments
            ?? throw new InvalidOperationException("Channel reload omitted attachment metadata.");
        Require(persistedAttachments.Count == 2, "Channel reload lost attachment metadata.");
        foreach (var attachment in persistedAttachments)
        {
            using var download = await clientB.GetAsync(attachment.DownloadUrl, cancellationToken);
            Require(download.IsSuccessStatusCode, "USER_B could not download a Channel attachment.");
            Require(download.Content.Headers.ContentDisposition?.DispositionType == "attachment",
                "Channel download was not forced through Content-Disposition attachment.");
            await RequireStatusAsync(
                await clientC.GetAsync(attachment.DownloadUrl, cancellationToken),
                HttpStatusCode.NotFound,
                "USER_C Channel attachment enumeration");
        }
        using (var rangeRequest = new HttpRequestMessage(
                   HttpMethod.Get,
                   persistedAttachments[0].DownloadUrl))
        {
            rangeRequest.Headers.Range = new RangeHeaderValue(0, 3);
            using var rangeResponse = await clientB.SendAsync(rangeRequest, cancellationToken);
            Require(rangeResponse.StatusCode == HttpStatusCode.PartialContent,
                "Authorized attachment range request did not return 206 Partial Content.");
        }

        var directMessage = await SendAttachmentMessageAsync<DirectMessageDto>(
            clientA,
            $"/api/direct-conversations/{_identity.ConversationAbId:D}/messages",
            string.Empty,
            [("Nội dung Unicode"u8.ToArray(), "../../Tài liệu Việt Nam.txt", "application/x-msdownload")],
            cancellationToken);
        Require(directMessage.Attachments?.Single().OriginalFileName == "Tài liệu Việt Nam.txt",
            "DM path traversal filename was not reduced to a sanitized Unicode leaf name.");
        var directHistory = await GetDirectHistoryAsync(clientB, 1, 50, cancellationToken);
        var directAttachment = directHistory.Items.Single(item => item.MessageId == directMessage.MessageId).Attachments?.Single()
            ?? throw new InvalidOperationException("DM reload lost attachment metadata.");
        using (var download = await clientB.GetAsync(directAttachment.DownloadUrl, cancellationToken))
            Require(download.IsSuccessStatusCode, "USER_B could not download a DM attachment.");
        await RequireStatusAsync(
            await clientC.GetAsync(directAttachment.DownloadUrl, cancellationToken),
            HttpStatusCode.NotFound,
            "USER_C DM attachment enumeration");

        await RequireMultipartStatusAsync(clientA, $"/api/channels/{_identity.ChannelAId:D}/messages",
            Enumerable.Range(0, 6).Select(index => ("ok"u8.ToArray(), $"{index}.txt", "text/plain")).ToArray(),
            HttpStatusCode.BadRequest, "six attachments", cancellationToken);
        await RequireMultipartStatusAsync(clientA, $"/api/channels/{_identity.ChannelAId:D}/messages",
            [(new byte[10 * 1024 * 1024 + 1], "large.pdf", "application/pdf")],
            HttpStatusCode.BadRequest, "oversized attachment", cancellationToken);
        await RequireMultipartStatusAsync(clientA, $"/api/channels/{_identity.ChannelAId:D}/messages",
            [("<svg><script/></svg>"u8.ToArray(), "payload.svg", "image/svg+xml")],
            HttpStatusCode.BadRequest, "SVG attachment", cancellationToken);

        Console.WriteLine("PASS Attachments: Channel/DM SQL metadata, private download, realtime, Unicode/path sanitization, validation, C denial");
    }

    private async Task CheckChannelRestAsync(
        HttpClient clientA,
        HttpClient clientB,
        HttpClient clientC,
        CancellationToken cancellationToken)
    {
        var discovery = await GetDataAsync<CollaborationChannelPageDto>(
            clientA.GetAsync(
                $"/api/projects/{_identity.ProjectAId:D}/channels?page=1&pageSize=20",
                cancellationToken),
            cancellationToken);
        Require(discovery.Items.Count == 1, "USER_A discovery must return exactly CHANNEL_A.");
        Require(discovery.Items[0].ChannelId == _identity.ChannelAId, "USER_A did not discover CHANNEL_A.");

        var contents = new[]
        {
            $"{_identity.Prefix}-channel-one",
            $"{_identity.Prefix}-Xin chào Việt Nam – kiểm chứng Unicode",
            $"{_identity.Prefix}-channel-three"
        };
        var sentIds = new HashSet<Guid>();
        var sentOrder = new List<Guid>();
        foreach (var content in contents)
        {
            var sent = await SendChannelAsync(clientA, content, cancellationToken);
            Require(sent.Sender.UserId == _identity.UserAId, "Channel sender was not taken from USER_A JWT.");
            Require(sent.Content == content, "Channel content changed in transit.");
            sentIds.Add(sent.MessageId);
            sentOrder.Add(sent.MessageId);
        }

        var discoveryA = await GetProjectChannelsAsync(clientA, cancellationToken);
        var discoveryB = await GetProjectChannelsAsync(clientB, cancellationToken);
        Require(discoveryA.Items.Single().UnreadCount == 0, "USER_A counted own Channel messages as unread.");
        Require(discoveryB.Items.Single().UnreadCount == 3, "USER_B Channel unread count did not increase to three.");

        var page1 = await GetChannelHistoryAsync(clientB, 1, 2, cancellationToken);
        var page2 = await GetChannelHistoryAsync(clientB, 2, 2, cancellationToken);
        var pagedIds = page1.Items.Concat(page2.Items).Select(item => item.MessageId).ToList();
        Require(page1.TotalCount == 3 && page2.TotalCount == 3, "Channel total count is unstable across pages.");
        Require(pagedIds.Count == 3 && pagedIds.Distinct().Count() == 3, "Channel pagination lost or duplicated an ID.");
        Require(sentIds.SetEquals(pagedIds), "Channel pagination did not return the persisted fixture messages.");
        Require(page1.Items.Concat(page2.Items).Any(item => item.Content == contents[1]), "Unicode channel content did not round-trip.");

        using var reloadedB = CreateClient(_tokenB);
        var reloaded = await GetChannelHistoryAsync(reloadedB, 1, 20, cancellationToken);
        Require(sentIds.All(id => reloaded.Items.Any(item => item.MessageId == id)), "A new USER_B client did not see persisted channel history.");

        await RequireStatusAsync(
            await clientC.GetAsync($"/api/channels/{_identity.ChannelAId:D}/messages", cancellationToken),
            HttpStatusCode.NotFound,
            "USER_C channel read");
        await RequireStatusAsync(
            await clientC.PostAsJsonAsync(
                $"/api/channels/{_identity.ChannelAId:D}/messages",
                new { content = $"{_identity.Prefix}-forbidden-channel" },
                cancellationToken),
            HttpStatusCode.NotFound,
            "USER_C channel send");

        await RequireStatusAsync(
            await clientC.PostAsJsonAsync(
                $"/api/channels/{_identity.ChannelAId:D}/read",
                new { messageId = sentOrder[^1] },
                cancellationToken),
            HttpStatusCode.NotFound,
            "USER_C channel mark read");
        var channelRead = await MarkChannelReadAsync(clientB, sentOrder[^1], cancellationToken);
        Require(channelRead.UnreadCount == 0, "USER_B Channel unread count did not clear.");
        var repeatedChannelRead = await MarkChannelReadAsync(clientB, sentOrder[^1], cancellationToken);
        var regressedChannelRead = await MarkChannelReadAsync(clientB, sentOrder[0], cancellationToken);
        Require(repeatedChannelRead.LastReadMessageId == sentOrder[^1], "Repeated Channel mark read changed the cursor.");
        Require(regressedChannelRead.LastReadMessageId == sentOrder[^1], "Channel cursor moved backwards.");
        using var freshReadClientB = CreateClient(_tokenB);
        var freshDiscoveryB = await GetProjectChannelsAsync(freshReadClientB, cancellationToken);
        Require(freshDiscoveryB.Items.Single().UnreadCount == 0, "Fresh USER_B client lost persisted Channel read state.");

        Console.WriteLine("PASS Channel REST: unread, monotonic read cursor, persistence, C denial, pagination, Unicode");
    }

    private async Task CheckDirectRestAsync(
        HttpClient clientA,
        HttpClient clientB,
        HttpClient clientC,
        CancellationToken cancellationToken)
    {
        var fromA = await FindOrCreateAsync(clientA, _identity.UserBId, cancellationToken);
        var fromB = await FindOrCreateAsync(clientB, _identity.UserAId, cancellationToken);
        var retryA = await FindOrCreateAsync(clientA, _identity.UserBId, cancellationToken);
        Require(fromA.ConversationId == _identity.ConversationAbId, "USER_A did not receive fixture conversation AB.");
        Require(fromB.ConversationId == fromA.ConversationId, "Reverse DM lookup returned another conversation.");
        Require(retryA.ConversationId == fromA.ConversationId, "Repeated DM lookup created a duplicate.");

        var contents = new[]
        {
            $"{_identity.Prefix}-dm-one",
            $"{_identity.Prefix}-DM tiếng Việt – không biến dạng",
            $"{_identity.Prefix}-dm-three"
        };
        var sentIds = new HashSet<Guid>();
        var sentOrder = new List<Guid>();
        foreach (var content in contents)
        {
            var sent = await SendDirectAsync(clientA, content, cancellationToken);
            Require(sent.Sender.UserId == _identity.UserAId, "DM sender was not taken from USER_A JWT.");
            Require(sent.Content == content, "DM content changed in transit.");
            sentIds.Add(sent.MessageId);
            sentOrder.Add(sent.MessageId);
        }

        var listA = await GetDirectConversationsAsync(clientA, cancellationToken);
        var listB = await GetDirectConversationsAsync(clientB, cancellationToken);
        Require(listA.Items.Single().UnreadCount == 0, "USER_A counted own DM messages as unread.");
        Require(listB.Items.Single().UnreadCount == 3, "USER_B DM unread count did not increase to three.");

        var page1 = await GetDirectHistoryAsync(clientB, 1, 2, cancellationToken);
        var page2 = await GetDirectHistoryAsync(clientB, 2, 2, cancellationToken);
        var pagedIds = page1.Items.Concat(page2.Items).Select(item => item.MessageId).ToList();
        Require(page1.TotalCount == 3 && page2.TotalCount == 3, "DM total count is unstable across pages.");
        Require(pagedIds.Count == 3 && pagedIds.Distinct().Count() == 3, "DM pagination lost or duplicated an ID.");
        Require(sentIds.SetEquals(pagedIds), "DM pagination did not return the persisted fixture messages.");

        using var reloadedB = CreateClient(_tokenB);
        var reloaded = await GetDirectHistoryAsync(reloadedB, 1, 20, cancellationToken);
        Require(sentIds.All(id => reloaded.Items.Any(item => item.MessageId == id)), "A new USER_B client did not see persisted DM history.");

        await RequireStatusAsync(
            await clientC.GetAsync(
                $"/api/direct-conversations/{_identity.ConversationAbId:D}/messages",
                cancellationToken),
            HttpStatusCode.NotFound,
            "USER_C DM read");
        await RequireStatusAsync(
            await clientC.PostAsJsonAsync(
                $"/api/direct-conversations/{_identity.ConversationAbId:D}/messages",
                new { content = $"{_identity.Prefix}-forbidden-dm" },
                cancellationToken),
            HttpStatusCode.NotFound,
            "USER_C DM send");

        await RequireStatusAsync(
            await clientC.PostAsJsonAsync(
                $"/api/direct-conversations/{_identity.ConversationAbId:D}/read",
                new { messageId = sentOrder[^1] },
                cancellationToken),
            HttpStatusCode.NotFound,
            "USER_C DM mark read");
        var directRead = await MarkDirectReadAsync(clientB, sentOrder[^1], cancellationToken);
        Require(directRead.UnreadCount == 0, "USER_B DM unread count did not clear.");
        var repeatedDirectRead = await MarkDirectReadAsync(clientB, sentOrder[^1], cancellationToken);
        var regressedDirectRead = await MarkDirectReadAsync(clientB, sentOrder[0], cancellationToken);
        Require(repeatedDirectRead.LastReadMessageId == sentOrder[^1], "Repeated DM mark read changed the cursor.");
        Require(regressedDirectRead.LastReadMessageId == sentOrder[^1], "DM cursor moved backwards.");
        using var freshReadClientB = CreateClient(_tokenB);
        var freshListB = await GetDirectConversationsAsync(freshReadClientB, cancellationToken);
        Require(freshListB.Items.Single().UnreadCount == 0, "Fresh USER_B client lost persisted DM read state.");

        Console.WriteLine("PASS DM REST: unread, monotonic read cursor, persistence, C denial, no duplicate");
    }

    private async Task CheckSignalRAsync(
        HttpClient clientA,
        HttpClient clientB,
        CancellationToken cancellationToken)
    {
        var tokenSlotA = new TokenSlot(_tokenA);
        await using var connectionA = CreateConnection(tokenSlotA);
        await using var connectionB = CreateConnection(new TokenSlot(_tokenB));
        await using var connectionC = CreateConnection(new TokenSlot(_tokenC));
        var channelA = new EventProbe<ChannelMessageCreatedEventDto>();
        var channelB = new EventProbe<ChannelMessageCreatedEventDto>();
        var channelC = new EventProbe<ChannelMessageCreatedEventDto>();
        var directA = new EventProbe<DirectMessageCreatedEventDto>();
        var directB = new EventProbe<DirectMessageCreatedEventDto>();
        var directC = new EventProbe<DirectMessageCreatedEventDto>();
        var readA = new EventProbe<CollaborationReadStateDto>();
        var readB = new EventProbe<CollaborationReadStateDto>();
        var readC = new EventProbe<CollaborationReadStateDto>();
        connectionA.On<ChannelMessageCreatedEventDto>(ChatRealtimeEvents.ChannelMessageCreated, channelA.Record);
        connectionB.On<ChannelMessageCreatedEventDto>(ChatRealtimeEvents.ChannelMessageCreated, channelB.Record);
        connectionC.On<ChannelMessageCreatedEventDto>(ChatRealtimeEvents.ChannelMessageCreated, channelC.Record);
        connectionA.On<DirectMessageCreatedEventDto>(ChatRealtimeEvents.DirectMessageCreated, directA.Record);
        connectionB.On<DirectMessageCreatedEventDto>(ChatRealtimeEvents.DirectMessageCreated, directB.Record);
        connectionC.On<DirectMessageCreatedEventDto>(ChatRealtimeEvents.DirectMessageCreated, directC.Record);
        connectionA.On<CollaborationReadStateDto>(ChatRealtimeEvents.CollaborationReadStateChanged, readA.Record);
        connectionB.On<CollaborationReadStateDto>(ChatRealtimeEvents.CollaborationReadStateChanged, readB.Record);
        connectionC.On<CollaborationReadStateDto>(ChatRealtimeEvents.CollaborationReadStateChanged, readC.Record);

        await Task.WhenAll(
            connectionA.StartAsync(cancellationToken),
            connectionB.StartAsync(cancellationToken),
            connectionC.StartAsync(cancellationToken));
        await connectionA.InvokeAsync("JoinChannel", _identity.ChannelAId.ToString(), cancellationToken);
        await connectionB.InvokeAsync("JoinChannel", _identity.ChannelAId.ToString(), cancellationToken);
        await RequireHubDeniedAsync(
            () => connectionC.InvokeAsync("JoinChannel", _identity.ChannelAId.ToString(), cancellationToken),
            "CHANNEL_NOT_FOUND_OR_FORBIDDEN");

        var channelMessage = await SendChannelAsync(
            clientA,
            $"{_identity.Prefix}-signalr-channel",
            cancellationToken);
        await WaitForAsync(
            () => channelA.Count(item => item.MessageId == channelMessage.MessageId) == 1 &&
                channelB.Count(item => item.MessageId == channelMessage.MessageId) == 1 &&
                readB.Count(item =>
                    item.ResourceType == CollaborationReadResourceTypes.Channel &&
                    item.ResourceId == _identity.ChannelAId &&
                    item.UnreadCount == 1) == 1,
            "A/B Channel delivery or USER_B private unread update was incorrect.",
            cancellationToken);
        await Task.Delay(200, cancellationToken);
        Require(channelA.Count(item => item.MessageId == channelMessage.MessageId) == 1, "USER_A received a duplicate Channel event.");
        Require(channelB.Count(item => item.MessageId == channelMessage.MessageId) == 1, "USER_B received a duplicate Channel event.");
        Require(channelC.Count(item => item.MessageId == channelMessage.MessageId) == 0, "Channel event leaked to USER_C.");
        Require(readA.Count(item => item.ResourceId == _identity.ChannelAId) == 0, "Channel unread update leaked to sender USER_A.");
        Require(readC.Count(item => item.ResourceId == _identity.ChannelAId) == 0, "Channel unread update leaked to USER_C.");
        await MarkChannelReadAsync(clientB, channelMessage.MessageId, cancellationToken);
        await WaitForAsync(
            () => readB.Count(item =>
                item.ResourceType == CollaborationReadResourceTypes.Channel &&
                item.ResourceId == _identity.ChannelAId &&
                item.LastReadMessageId == channelMessage.MessageId &&
                item.UnreadCount == 0) == 1,
            "USER_B did not receive its private Channel read-state update.",
            cancellationToken);

        await connectionA.InvokeAsync("JoinDirectConversation", _identity.ConversationAbId.ToString(), cancellationToken);
        await connectionB.InvokeAsync("JoinDirectConversation", _identity.ConversationAbId.ToString(), cancellationToken);
        await RequireHubDeniedAsync(
            () => connectionC.InvokeAsync("JoinDirectConversation", _identity.ConversationAbId.ToString(), cancellationToken),
            "CONVERSATION_NOT_FOUND_OR_FORBIDDEN");

        var directMessage = await SendDirectAsync(
            clientA,
            $"{_identity.Prefix}-signalr-dm",
            cancellationToken);
        await WaitForAsync(
            () => directA.Count(item => item.MessageId == directMessage.MessageId) == 1 &&
                directB.Count(item => item.MessageId == directMessage.MessageId) == 1 &&
                readB.Count(item =>
                    item.ResourceType == CollaborationReadResourceTypes.DirectConversation &&
                    item.ResourceId == _identity.ConversationAbId &&
                    item.UnreadCount == 1) == 1,
            "A/B DM delivery or USER_B private unread update was incorrect.",
            cancellationToken);
        await Task.Delay(200, cancellationToken);
        Require(directA.Count(item => item.MessageId == directMessage.MessageId) == 1, "USER_A received a duplicate DM event.");
        Require(directB.Count(item => item.MessageId == directMessage.MessageId) == 1, "USER_B received a duplicate DM event.");
        Require(directC.Count(item => item.MessageId == directMessage.MessageId) == 0, "DM event leaked to USER_C.");
        Require(readA.Count(item => item.ResourceId == _identity.ConversationAbId) == 0, "DM unread update leaked to sender USER_A.");
        Require(readC.Count(item => item.ResourceId == _identity.ConversationAbId) == 0, "DM unread update leaked to USER_C.");
        await MarkDirectReadAsync(clientB, directMessage.MessageId, cancellationToken);
        await WaitForAsync(
            () => readB.Count(item =>
                item.ResourceType == CollaborationReadResourceTypes.DirectConversation &&
                item.ResourceId == _identity.ConversationAbId &&
                item.LastReadMessageId == directMessage.MessageId &&
                item.UnreadCount == 0) == 1,
            "USER_B did not receive its private DM read-state update.",
            cancellationToken);

        await connectionB.StopAsync(cancellationToken);
        await connectionB.StartAsync(cancellationToken);
        var beforeRejoin = await SendChannelAsync(
            clientA,
            $"{_identity.Prefix}-reconnect-before-rejoin",
            cancellationToken);
        await Task.Delay(350, cancellationToken);
        Require(channelB.Count(item => item.MessageId == beforeRejoin.MessageId) == 0, "Reconnect retained the old Channel group.");

        await connectionB.InvokeAsync("JoinChannel", _identity.ChannelAId.ToString(), cancellationToken);
        var afterRejoin = await SendChannelAsync(
            clientA,
            $"{_identity.Prefix}-reconnect-after-rejoin",
            cancellationToken);
        await WaitForAsync(
            () => channelB.Count(item => item.MessageId == afterRejoin.MessageId) == 1,
            "USER_B did not receive after explicit Channel rejoin.",
            cancellationToken);

        await connectionB.StopAsync(cancellationToken);
        await connectionB.StartAsync(cancellationToken);
        await connectionB.InvokeAsync("JoinDirectConversation", _identity.ConversationAbId.ToString(), cancellationToken);
        var oldGroupProbe = await SendChannelAsync(
            clientA,
            $"{_identity.Prefix}-old-channel-group-probe",
            cancellationToken);
        await Task.Delay(350, cancellationToken);
        Require(channelB.Count(item => item.MessageId == oldGroupProbe.MessageId) == 0, "Channel event leaked after reconnect into DM only.");
        var dmAfterReconnect = await SendDirectAsync(
            clientA,
            $"{_identity.Prefix}-dm-after-reconnect",
            cancellationToken);
        await WaitForAsync(
            () => directB.Count(item => item.MessageId == dmAfterReconnect.MessageId) == 1,
            "USER_B did not receive DM after reconnect and rejoin.",
            cancellationToken);

        await connectionA.StopAsync(cancellationToken);
        tokenSlotA.Value = _tokenC;
        await connectionA.StartAsync(cancellationToken);
        await RequireHubDeniedAsync(
            () => connectionA.InvokeAsync("JoinChannel", _identity.ChannelAId.ToString(), cancellationToken),
            "CHANNEL_NOT_FOUND_OR_FORBIDDEN");
        await RequireHubDeniedAsync(
            () => connectionA.InvokeAsync("JoinDirectConversation", _identity.ConversationAbId.ToString(), cancellationToken),
            "CONVERSATION_NOT_FOUND_OR_FORBIDDEN");
        var accountSwitchProbe = await SendDirectAsync(
            clientB,
            $"{_identity.Prefix}-account-switch-probe",
            cancellationToken);
        await Task.Delay(350, cancellationToken);
        Require(directA.Count(item => item.MessageId == accountSwitchProbe.MessageId) == 0, "USER_C inherited USER_A's DM group after token switch.");

        Console.WriteLine("PASS SignalR: delivery, private unread/read updates, C isolation, reconnect, account switch");
    }

    private async Task AssertDatabaseShapeAsync(CancellationToken cancellationToken)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pair = FixtureIdentity.Pair(_identity.UserAId, _identity.UserBId);
        var conversations = await context.DirectConversations.AsNoTracking()
            .CountAsync(item => item.UserLowId == pair.Low && item.UserHighId == pair.High, cancellationToken);
        var participants = await context.DirectConversationParticipants.AsNoTracking()
            .Where(item => item.ConversationId == _identity.ConversationAbId)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);
        var attachments = await context.CollaborationMessageAttachments.AsNoTracking()
            .Where(item =>
                (item.ChannelMessage != null && item.ChannelMessage.CollaborationChannelId == _identity.ChannelAId) ||
                (item.DirectMessage != null && item.DirectMessage.ConversationId == _identity.ConversationAbId))
            .ToListAsync(cancellationToken);
        var mentions = await context.ChannelMessageMentions.AsNoTracking()
            .Where(item => item.ChannelMessage.CollaborationChannelId == _identity.ChannelAId)
            .ToListAsync(cancellationToken);
        var mentionNotifications = await context.Notifications.AsNoTracking()
            .Where(item =>
                item.CollaborationChannelId == _identity.ChannelAId &&
                item.NotificationType == "collaboration_channel_mention")
            .ToListAsync(cancellationToken);
        Require(conversations == 1, "Database contains a duplicate AB conversation.");
        Require(participants.Count == 2 && participants.Contains(_identity.UserAId) && participants.Contains(_identity.UserBId),
            "Database conversation participants are not exactly USER_A and USER_B.");
        Require(attachments.Count == 3 && attachments.All(item =>
                (item.ChannelMessageId != null) != (item.DirectMessageId != null) &&
                item.UploadedByUserId == _identity.UserAId),
            "Attachment SQL metadata did not preserve one owner and JWT uploader identity.");
        Require(mentions.Count == 2 && mentions.All(item => item.MentionedUserId == _identity.UserBId),
            "Mention SQL metadata was duplicated or targeted outside USER_B.");
        Require(mentionNotifications.Count == 2 && mentionNotifications.All(item =>
                item.UserId == _identity.UserBId && item.TriggeredByUserId == _identity.UserAId),
            "Mention notifications were duplicated or escaped the intended actor/recipient pair.");
    }

    private HttpClient CreateClient(string token)
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private HubConnection CreateConnection(TokenSlot slot) =>
        new HubConnectionBuilder()
            .WithUrl(
                new Uri(_factory.Server.BaseAddress, ChatHub.Route),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.AccessTokenProvider = () => Task.FromResult<string?>(slot.Value);
                })
            .Build();

    private Task<ChannelMessageDto> SendChannelAsync(
        HttpClient client,
        string content,
        CancellationToken cancellationToken) =>
        PostDataAsync<ChannelMessageDto>(
            client,
            $"/api/channels/{_identity.ChannelAId:D}/messages",
            new { content },
            cancellationToken);

    private Task<DirectMessageDto> SendDirectAsync(
        HttpClient client,
        string content,
        CancellationToken cancellationToken) =>
        PostDataAsync<DirectMessageDto>(
            client,
            $"/api/direct-conversations/{_identity.ConversationAbId:D}/messages",
            new { content },
            cancellationToken);

    private Task<ChannelMessagePageDto> GetChannelHistoryAsync(
        HttpClient client,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        GetDataAsync<ChannelMessagePageDto>(
            client.GetAsync(
                $"/api/channels/{_identity.ChannelAId:D}/messages?page={page}&pageSize={pageSize}",
                cancellationToken),
            cancellationToken);

    private Task<DirectMessagePageDto> GetDirectHistoryAsync(
        HttpClient client,
        int page,
        int pageSize,
        CancellationToken cancellationToken) =>
        GetDataAsync<DirectMessagePageDto>(
            client.GetAsync(
                $"/api/direct-conversations/{_identity.ConversationAbId:D}/messages?page={page}&pageSize={pageSize}",
                cancellationToken),
            cancellationToken);

    private Task<DirectConversationDto> FindOrCreateAsync(
        HttpClient client,
        Guid participantUserId,
        CancellationToken cancellationToken) =>
        PostDataAsync<DirectConversationDto>(
            client,
            "/api/direct-conversations",
            new { participantUserId },
            cancellationToken);

    private Task<CollaborationChannelPageDto> GetProjectChannelsAsync(
        HttpClient client,
        CancellationToken cancellationToken) =>
        GetDataAsync<CollaborationChannelPageDto>(
            client.GetAsync(
                $"/api/projects/{_identity.ProjectAId:D}/channels?page=1&pageSize=20",
                cancellationToken),
            cancellationToken);

    private Task<DirectConversationPageDto> GetDirectConversationsAsync(
        HttpClient client,
        CancellationToken cancellationToken) =>
        GetDataAsync<DirectConversationPageDto>(
            client.GetAsync(
                "/api/direct-conversations?page=1&pageSize=20",
                cancellationToken),
            cancellationToken);

    private Task<CollaborationReadStateDto> MarkChannelReadAsync(
        HttpClient client,
        Guid messageId,
        CancellationToken cancellationToken) =>
        PostDataAsync<CollaborationReadStateDto>(
            client,
            $"/api/channels/{_identity.ChannelAId:D}/read",
            new { messageId },
            cancellationToken);

    private Task<CollaborationReadStateDto> MarkDirectReadAsync(
        HttpClient client,
        Guid messageId,
        CancellationToken cancellationToken) =>
        PostDataAsync<CollaborationReadStateDto>(
            client,
            $"/api/direct-conversations/{_identity.ConversationAbId:D}/read",
            new { messageId },
            cancellationToken);

    private static async Task<T> PostDataAsync<T>(
        HttpClient client,
        string path,
        object body,
        CancellationToken cancellationToken) where T : class
    {
        var response = await client.PostAsJsonAsync(path, body, cancellationToken);
        return await ReadDataAsync<T>(response, cancellationToken);
    }

    private static async Task<T> SendAttachmentMessageAsync<T>(
        HttpClient client,
        string path,
        string content,
        IReadOnlyList<(byte[] Bytes, string FileName, string ContentType)> files,
        CancellationToken cancellationToken,
        IReadOnlyList<ChannelMessageMentionRequestDto>? mentions = null) where T : class
    {
        using var form = CreateMultipart(content, files, mentions);
        using var response = await client.PostAsync(path, form, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Attachment send expected success but received {(int)response.StatusCode}: {body[..Math.Min(body.Length, 300)]}");
        }
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(
            cancellationToken: cancellationToken);
        return envelope?.Data ?? throw new InvalidOperationException("Attachment response data was empty.");
    }

    private static async Task RequireMultipartStatusAsync(
        HttpClient client,
        string path,
        IReadOnlyList<(byte[] Bytes, string FileName, string ContentType)> files,
        HttpStatusCode expected,
        string label,
        CancellationToken cancellationToken)
    {
        using var form = CreateMultipart("fixture validation", files);
        await RequireStatusAsync(await client.PostAsync(path, form, cancellationToken), expected, label);
    }

    private static MultipartFormDataContent CreateMultipart(
        string content,
        IReadOnlyList<(byte[] Bytes, string FileName, string ContentType)> files,
        IReadOnlyList<ChannelMessageMentionRequestDto>? mentions = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(content), "content");
        if (mentions != null)
        {
            for (var index = 0; index < mentions.Count; index++)
            {
                form.Add(new StringContent(mentions[index].UserId.ToString()), $"mentions[{index}].userId");
                form.Add(new StringContent(mentions[index].StartIndex.ToString()), $"mentions[{index}].startIndex");
                form.Add(new StringContent(mentions[index].Length.ToString()), $"mentions[{index}].length");
            }
        }
        foreach (var file in files)
        {
            var fileContent = new ByteArrayContent(file.Bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            form.Add(fileContent, "files", file.FileName);
        }
        return form;
    }

    private static async Task<T> GetDataAsync<T>(
        Task<HttpResponseMessage> responseTask,
        CancellationToken cancellationToken) where T : class =>
        await ReadDataAsync<T>(await responseTask, cancellationToken);

    private static async Task<T> ReadDataAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken) where T : class
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Expected success but received {(int)response.StatusCode}: {body[..Math.Min(body.Length, 300)]}");
            }
            var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(
                cancellationToken: cancellationToken);
            if (envelope?.Data == null)
                throw new InvalidOperationException("API response data was empty.");
            return envelope.Data;
        }
    }

    private static async Task RequireStatusAsync(
        HttpResponseMessage response,
        HttpStatusCode expected,
        string label)
    {
        using (response)
        {
            if (response.StatusCode != expected)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"{label}: expected {(int)expected}, received {(int)response.StatusCode}: {body[..Math.Min(body.Length, 300)]}");
            }
        }
    }

    private static async Task RequireHubDeniedAsync(Func<Task> action, string expectedCode)
    {
        try
        {
            await action();
        }
        catch (HubException exception) when (exception.Message.Contains(expectedCode, StringComparison.Ordinal))
        {
            return;
        }
        throw new InvalidOperationException($"Expected SignalR denial {expectedCode}.");
    }

    private static async Task WaitForAsync(
        Func<bool> condition,
        string message,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(7);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(50, cancellationToken);
        }
        throw new TimeoutException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TokenSlot(string value)
    {
        public string Value { get; set; } = value;
    }

    private sealed record FixtureNotificationDto(
        Guid Id,
        string NotificationType,
        Guid? CollaborationChannelId,
        Guid? ChannelMessageId,
        bool IsRead);

    private sealed class EventProbe<T>
    {
        private readonly ConcurrentQueue<T> _events = new();
        public void Record(T payload) => _events.Enqueue(payload);
        public int Count(Func<T, bool> predicate) => _events.Count(predicate);
    }
}
