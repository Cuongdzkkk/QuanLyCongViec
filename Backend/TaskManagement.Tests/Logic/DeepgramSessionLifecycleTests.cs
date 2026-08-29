using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class DeepgramSessionLifecycleTests
{
    [Fact]
    public async Task FiveSequentialChunks_ReuseSocketAndReceiveInterimAndFinalResults()
    {
        var socketFactory = new FakeDeepgramWebSocketFactory();
        await using var provider = CreateProvider(socketFactory);
        var sessionId = Guid.NewGuid();
        var speakerId = Guid.NewGuid();
        var callbackIndexes = new ConcurrentQueue<int>();
        var interim = new TaskCompletionSource<CallTranscriptionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var final = new TaskCompletionSource<CallTranscriptionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstCallbackCanContinue = true;

        for (var index = 1; index <= 5; index++)
        {
            var callbackIndex = index;
            await provider.SubmitAsync(
                Chunk(sessionId, speakerId, index),
                (_, result) =>
                {
                    callbackIndexes.Enqueue(callbackIndex);
                    if (result.IsFinal) final.TrySetResult(result);
                    else interim.TrySetResult(result);
                    return Task.CompletedTask;
                },
                index == 1 ? () => firstCallbackCanContinue : () => true);

            if (index == 1) firstCallbackCanContinue = false;
            if (index == 3)
            {
                socketFactory.Socket.QueueText(Transcript("đang thảo luận", isFinal: false, speechFinal: false));
                (await interim.Task.WaitAsync(TimeSpan.FromSeconds(2))).Text.Should().Be("đang thảo luận");
            }
            if (index == 4)
            {
                socketFactory.Socket.QueueText(Transcript("đã thống nhất", isFinal: true, speechFinal: true));
                (await final.Task.WaitAsync(TimeSpan.FromSeconds(2))).Text.Should().Be("đã thống nhất");
            }
        }

        socketFactory.ConnectionCount.Should().Be(1);
        socketFactory.Sockets.Should().ContainSingle();
        socketFactory.Socket.BinaryMessages.Should().HaveCount(5);
        socketFactory.Socket.ReceiveCancellationCount.Should().Be(0);
        socketFactory.Socket.State.Should().Be(WebSocketState.Open);
        callbackIndexes.Should().Equal(3, 4);

        await provider.StopAsync(RoomId, sessionId, speakerId, ConsentGeneration);

        socketFactory.Socket.ReceiveCancellationCount.Should().Be(1);
        socketFactory.Socket.State.Should().Be(WebSocketState.Closed);
    }

    [Fact]
    public async Task ConcurrentFirstChunks_CreateOneSocketForTheSessionKey()
    {
        var socketFactory = new FakeDeepgramWebSocketFactory(holdConnection: true);
        await using var provider = CreateProvider(socketFactory);
        var sessionId = Guid.NewGuid();
        var speakerId = Guid.NewGuid();

        var submissions = Enumerable.Range(1, 5)
            .Select(index => provider.SubmitAsync(
                Chunk(sessionId, speakerId, index),
                (_, _) => Task.CompletedTask,
                () => true))
            .ToArray();

        await socketFactory.ConnectionStarted.WaitAsync(TimeSpan.FromSeconds(2));
        socketFactory.ConnectionCount.Should().Be(1);
        socketFactory.ReleaseConnection();
        await Task.WhenAll(submissions);

        socketFactory.ConnectionCount.Should().Be(1);
        socketFactory.Sockets.Should().ContainSingle();
        socketFactory.Socket.BinaryMessages.Should().HaveCount(5);
        socketFactory.Socket.ReceiveCancellationCount.Should().Be(0);
    }

    [Fact]
    public async Task CanceledSubmit_DoesNotCancelLongLivedReceiveLoop()
    {
        var socketFactory = new FakeDeepgramWebSocketFactory();
        await using var provider = CreateProvider(socketFactory);
        var sessionId = Guid.NewGuid();
        var speakerId = Guid.NewGuid();
        await provider.SubmitAsync(Chunk(sessionId, speakerId, 1), (_, _) => Task.CompletedTask, () => true);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        var canceledSubmit = () => provider.SubmitAsync(
            Chunk(sessionId, speakerId, 2),
            (_, _) => Task.CompletedTask,
            () => true,
            canceled.Token);

        await canceledSubmit.Should().ThrowAsync<OperationCanceledException>();
        await provider.SubmitAsync(Chunk(sessionId, speakerId, 3), (_, _) => Task.CompletedTask, () => true);

        socketFactory.ConnectionCount.Should().Be(1);
        socketFactory.Socket.BinaryMessages.Should().HaveCount(2);
        socketFactory.Socket.ReceiveCancellationCount.Should().Be(0);
        socketFactory.Socket.State.Should().Be(WebSocketState.Open);
    }

    private static DeepgramCallTranscriptionProvider CreateProvider(IDeepgramWebSocketFactory socketFactory) =>
        new(
            new CallTranscriptionOptions
            {
                Enabled = true,
                Provider = "Deepgram",
                Language = "vi",
                SupportedLanguages = ["vi", "en"],
                SampleRate = 16000,
                Deepgram = new DeepgramCallTranscriptionOptions
                {
                    ApiKey = "test-only",
                    Endpoint = "wss://deepgram.test/v1/listen"
                }
            },
            new RecordingUsageSink(),
            NullLogger<DeepgramCallTranscriptionProvider>.Instance,
            socketFactory);

    private static CallAudioChunk Chunk(Guid sessionId, Guid speakerId, int index)
    {
        var startedAt = DateTimeOffset.Parse("2026-08-29T00:00:00Z").AddMilliseconds((index - 1) * 250);
        return new CallAudioChunk(
            sessionId,
            RoomId,
            speakerId,
            "Speaker",
            "audio/pcm;rate=16000",
            Enumerable.Range(0, 8000).Select(value => (byte)(value + index)).ToArray(),
            startedAt,
            startedAt.AddMilliseconds(250),
            ConsentGeneration,
            "connection-1",
            "vi");
    }

    private static string Transcript(string text, bool isFinal, bool speechFinal) =>
        $$"""
        {
          "is_final": {{isFinal.ToString().ToLowerInvariant()}},
          "speech_final": {{speechFinal.ToString().ToLowerInvariant()}},
          "duration": 0.25,
          "channel": { "alternatives": [{ "transcript": "{{text}}", "confidence": 0.95 }] }
        }
        """;

    private const string RoomId = "project:00000000-0000-0000-0000-000000000001:voice:general";
    private const long ConsentGeneration = 7;

    private sealed class RecordingUsageSink : ICallTranscriptionUsageSink
    {
        public Task RecordAsync(CallTranscriptionUsage usage, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

internal sealed class FakeDeepgramWebSocketFactory(bool holdConnection = false) : IDeepgramWebSocketFactory
{
    private readonly TaskCompletionSource _connectionStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _connectionReleased = CreateReleaseSource(holdConnection);
    private int _connectionCount;

    public ConcurrentQueue<FakeDeepgramWebSocket> Sockets { get; } = new();
    public int ConnectionCount => Volatile.Read(ref _connectionCount);
    public Task ConnectionStarted => _connectionStarted.Task;
    public FakeDeepgramWebSocket Socket => Sockets.Single();

    public async Task<WebSocket> ConnectAsync(Uri endpoint, string apiKey, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _connectionCount);
        var socket = new FakeDeepgramWebSocket();
        Sockets.Enqueue(socket);
        _connectionStarted.TrySetResult();
        await _connectionReleased.Task.WaitAsync(cancellationToken);
        socket.Open();
        return socket;
    }

    public void ReleaseConnection() => _connectionReleased.TrySetResult();

    private static TaskCompletionSource CreateReleaseSource(bool holdConnection)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!holdConnection) source.SetResult();
        return source;
    }
}

internal sealed class FakeDeepgramWebSocket : WebSocket
{
    private readonly Channel<IncomingFrame> _incoming = Channel.CreateUnbounded<IncomingFrame>();
    private WebSocketState _state = WebSocketState.Connecting;
    private WebSocketCloseStatus? _closeStatus;
    private string? _closeStatusDescription;
    private int _receiveCancellationCount;

    public ConcurrentQueue<byte[]> BinaryMessages { get; } = new();
    public int ReceiveCancellationCount => Volatile.Read(ref _receiveCancellationCount);
    public override WebSocketCloseStatus? CloseStatus => _closeStatus;
    public override string? CloseStatusDescription => _closeStatusDescription;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;

    public void Open() => _state = WebSocketState.Open;

    public void QueueText(string payload) =>
        _incoming.Writer.TryWrite(new IncomingFrame(Encoding.UTF8.GetBytes(payload), WebSocketMessageType.Text));

    public override void Abort() => _state = WebSocketState.Aborted;

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        _closeStatus = closeStatus;
        _closeStatusDescription = statusDescription;
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) =>
        CloseAsync(closeStatus, statusDescription, cancellationToken);

    public override void Dispose() => _state = WebSocketState.Closed;

    public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            var frame = await _incoming.Reader.ReadAsync(cancellationToken);
            Buffer.BlockCopy(frame.Payload, 0, buffer.Array!, buffer.Offset, frame.Payload.Length);
            return new WebSocketReceiveResult(frame.Payload.Length, frame.MessageType, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Interlocked.Increment(ref _receiveCancellationCount);
            throw;
        }
    }

    public override Task SendAsync(
        ArraySegment<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_state != WebSocketState.Open) throw new WebSocketException("Socket is not open.");
        if (messageType == WebSocketMessageType.Binary) BinaryMessages.Enqueue(buffer.ToArray());
        return Task.CompletedTask;
    }

    private sealed record IncomingFrame(byte[] Payload, WebSocketMessageType MessageType);
}
