using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Infrastructure.Services;

internal interface IDeepgramWebSocketFactory
{
    Task<WebSocket> ConnectAsync(Uri endpoint, string apiKey, CancellationToken cancellationToken);
}

internal sealed class ClientDeepgramWebSocketFactory : IDeepgramWebSocketFactory
{
    public async Task<WebSocket> ConnectAsync(Uri endpoint, string apiKey, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        try
        {
            socket.Options.SetRequestHeader("Authorization", $"Token {apiKey}");
            await socket.ConnectAsync(endpoint, cancellationToken);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

public sealed class DeepgramCallTranscriptionProvider : ICallStreamingTranscriptionProvider, IAsyncDisposable
{
    private readonly CallTranscriptionOptions _options;
    private readonly ICallTranscriptionUsageSink _usageSink;
    private readonly ILogger<DeepgramCallTranscriptionProvider> _logger;
    private readonly IDeepgramWebSocketFactory _socketFactory;
    private readonly ConcurrentDictionary<SessionKey, SessionEntry> _sessions = new();

    public DeepgramCallTranscriptionProvider(
        CallTranscriptionOptions options,
        ICallTranscriptionUsageSink usageSink,
        ILogger<DeepgramCallTranscriptionProvider> logger)
        : this(options, usageSink, logger, new ClientDeepgramWebSocketFactory())
    {
    }

    internal DeepgramCallTranscriptionProvider(
        CallTranscriptionOptions options,
        ICallTranscriptionUsageSink usageSink,
        ILogger<DeepgramCallTranscriptionProvider> logger,
        IDeepgramWebSocketFactory socketFactory)
    {
        _options = options;
        _usageSink = usageSink;
        _logger = logger;
        _socketFactory = socketFactory;
    }

    public bool IsConfigured => _options.IsConfigured;
    public string ProviderName => "Deepgram";
    public IReadOnlyList<string> SupportedLanguages => _options.SupportedLanguages;
    public string DefaultLanguage => _options.Language;

    public Task<CallTranscriptionResult?> TranscribeAsync(
        CallAudioChunk chunk,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Deepgram uses the streaming transcription contract.");

    public async Task SubmitAsync(
        CallAudioChunk chunk,
        Func<CallAudioChunk, CallTranscriptionResult, Task> onResult,
        Func<bool> canContinue,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) throw new CallTranscriptionProviderUnavailableException();
        if (!canContinue()) return;

        var key = new SessionKey(chunk.RoomId, chunk.CallSessionId, chunk.SpeakerUserId, chunk.ConsentGeneration);
        var (entry, session) = await GetOrCreateSessionAsync(key, chunk, onResult, canContinue, cancellationToken);
        try
        {
            await session.SendAsync(chunk.AudioBytes, chunk, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A submit token belongs to this send only. It must not cancel the long-lived receive loop.
            throw;
        }
        catch when (canContinue())
        {
            await RemoveAndDisposeAsync(key, entry, "send failure");
            (_, session) = await GetOrCreateSessionAsync(key, chunk, onResult, canContinue, cancellationToken);
            await session.SendAsync(chunk.AudioBytes, chunk, cancellationToken);
        }
    }

    public async Task StopAsync(
        string roomId,
        Guid callSessionId,
        Guid speakerUserId,
        long consentGeneration,
        CancellationToken cancellationToken = default)
    {
        var key = new SessionKey(roomId, callSessionId, speakerUserId, consentGeneration);
        if (_sessions.TryRemove(key, out var entry)) await DisposeEntryAsync(key, entry, "explicit stream stop");
    }

    public async Task StopRoomAsync(string roomId, CancellationToken cancellationToken = default)
    {
        var sessions = _sessions.Where(item => string.Equals(item.Key.RoomId, roomId, StringComparison.Ordinal)).ToArray();
        foreach (var item in sessions)
        {
            if (_sessions.TryRemove(item.Key, out var entry)) await DisposeEntryAsync(item.Key, entry, "room transcription stopped");
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var item in _sessions.ToArray())
        {
            if (_sessions.TryRemove(item.Key, out var entry)) await DisposeEntryAsync(item.Key, entry, "provider shutdown");
        }
    }

    public static DeepgramTranscript? ParseTranscript(string json) =>
        JsonSerializer.Deserialize<DeepgramTranscript>(json, JsonOptions.Default);

    private async Task<(SessionEntry Entry, DeepgramSession Session)> GetOrCreateSessionAsync(
        SessionKey key,
        CallAudioChunk chunk,
        Func<CallAudioChunk, CallTranscriptionResult, Task> onResult,
        Func<bool> canContinue,
        CancellationToken cancellationToken)
    {
        var candidate = new SessionEntry(() => DeepgramSession.ConnectAsync(
            _socketFactory,
            _options,
            _usageSink,
            _logger,
            chunk,
            onResult,
            canContinue,
            cancellationToken));
        var entry = _sessions.GetOrAdd(key, candidate);
        if (ReferenceEquals(entry, candidate))
        {
            _logger.LogInformation(
                "[CAPTION_PROVIDER] event=SESSION_INSERTED roomId={RoomId} callSessionId={CallSessionId} speakerUserId={SpeakerUserId} consentGeneration={ConsentGeneration}",
                key.RoomId,
                key.CallSessionId,
                key.SpeakerUserId,
                key.ConsentGeneration);
        }

        try
        {
            var session = await entry.Session;
            session.SetCallbacks(onResult, canContinue);
            return (entry, session);
        }
        catch
        {
            _sessions.TryRemove(new KeyValuePair<SessionKey, SessionEntry>(key, entry));
            throw;
        }
    }

    private async Task RemoveAndDisposeAsync(SessionKey key, SessionEntry entry, string reason)
    {
        if (_sessions.TryRemove(new KeyValuePair<SessionKey, SessionEntry>(key, entry)))
            await DisposeEntryAsync(key, entry, reason);
    }

    private async Task DisposeEntryAsync(SessionKey key, SessionEntry entry, string reason)
    {
        _logger.LogInformation(
            "[CAPTION_PROVIDER] event=SESSION_REMOVED roomId={RoomId} callSessionId={CallSessionId} speakerUserId={SpeakerUserId} consentGeneration={ConsentGeneration} reason={Reason}",
            key.RoomId,
            key.CallSessionId,
            key.SpeakerUserId,
            key.ConsentGeneration,
            reason);
        await entry.DisposeAsync(reason);
    }

    private readonly record struct SessionKey(string RoomId, Guid CallSessionId, Guid SpeakerUserId, long ConsentGeneration);

    private sealed class SessionEntry(Func<Task<DeepgramSession>> factory)
    {
        private readonly Lazy<Task<DeepgramSession>> _session = new(factory, LazyThreadSafetyMode.ExecutionAndPublication);

        public Task<DeepgramSession> Session => _session.Value;

        public async Task DisposeAsync(string reason)
        {
            if (!_session.IsValueCreated) return;
            DeepgramSession session;
            try
            {
                session = await _session.Value;
            }
            catch
            {
                // A failed connection has no live session to dispose.
                return;
            }
            await session.DisposeAsync(reason);
        }
    }

    private sealed class DeepgramSession : IAsyncDisposable
    {
        private readonly WebSocket _socket;
        private readonly CallTranscriptionOptions _options;
        private readonly ICallTranscriptionUsageSink _usageSink;
        private readonly ILogger<DeepgramCallTranscriptionProvider> _logger;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private readonly DateTimeOffset _streamStartedAt;
        private readonly Guid _sessionId;
        private readonly Guid _speakerId;
        private readonly string _speakerName;
        private readonly string _roomId;
        private SessionCallbacks _callbacks;
        private readonly List<string> _finalSegments = [];
        private DateTimeOffset _lastAudioEndedAt;
        private CallAudioChunk _latestSource;
        private Task? _receiveTask;

        private DeepgramSession(
            WebSocket socket,
            CallTranscriptionOptions options,
            ICallTranscriptionUsageSink usageSink,
            ILogger<DeepgramCallTranscriptionProvider> logger,
            CallAudioChunk firstChunk,
            Func<CallAudioChunk, CallTranscriptionResult, Task> onResult,
            Func<bool> canContinue)
        {
            _socket = socket;
            _options = options;
            _usageSink = usageSink;
            _logger = logger;
            _callbacks = new SessionCallbacks(onResult, canContinue);
            _streamStartedAt = firstChunk.StartedAt;
            _lastAudioEndedAt = firstChunk.EndedAt;
            _sessionId = firstChunk.CallSessionId;
            _speakerId = firstChunk.SpeakerUserId;
            _speakerName = firstChunk.SpeakerDisplayName;
            _roomId = firstChunk.RoomId;
            _latestSource = firstChunk with { AudioBytes = [] };
        }

        public static async Task<DeepgramSession> ConnectAsync(
            IDeepgramWebSocketFactory socketFactory,
            CallTranscriptionOptions options,
            ICallTranscriptionUsageSink usageSink,
            ILogger<DeepgramCallTranscriptionProvider> logger,
            CallAudioChunk firstChunk,
            Func<CallAudioChunk, CallTranscriptionResult, Task> onResult,
            Func<bool> canContinue,
            CancellationToken cancellationToken)
        {
            var language = options.SupportedLanguages.Contains(firstChunk.Language, StringComparer.OrdinalIgnoreCase)
                ? firstChunk.Language.ToLowerInvariant()
                : options.Language;
            var query = $"?model={Uri.EscapeDataString(options.Model)}&language={Uri.EscapeDataString(language)}&encoding=linear16&sample_rate={options.SampleRate}&channels=1&interim_results=true&punctuate=true&endpointing={options.EndpointingMilliseconds}";
            var socket = await socketFactory.ConnectAsync(
                new Uri(options.Deepgram.Endpoint + query),
                options.Deepgram.ApiKey!,
                cancellationToken);
            var session = new DeepgramSession(socket, options, usageSink, logger, firstChunk, onResult, canContinue);
            logger.LogInformation(
                "[CAPTION_PROVIDER] event=SESSION_CREATED roomId={RoomId} callSessionId={CallSessionId} speakerUserId={SpeakerUserId} consentGeneration={ConsentGeneration}",
                firstChunk.RoomId,
                firstChunk.CallSessionId,
                firstChunk.SpeakerUserId,
                firstChunk.ConsentGeneration);
            session._receiveTask = session.ReceiveLoopAsync(session._lifetime.Token);
            logger.LogInformation(
                "[CAPTION_PROVIDER] event=WS_OPEN roomId={RoomId} callSessionId={CallSessionId} speakerUserId={SpeakerUserId} consentGeneration={ConsentGeneration} receiveLoopStarted=YES",
                firstChunk.RoomId,
                firstChunk.CallSessionId,
                firstChunk.SpeakerUserId,
                firstChunk.ConsentGeneration);
            return session;
        }

        public void SetCallbacks(
            Func<CallAudioChunk, CallTranscriptionResult, Task> onResult,
            Func<bool> canContinue) =>
            Volatile.Write(ref _callbacks, new SessionCallbacks(onResult, canContinue));

        public async Task SendAsync(byte[] audioBytes, CallAudioChunk source, CancellationToken cancellationToken)
        {
            if (audioBytes.Length == 0 || !Volatile.Read(ref _callbacks).CanContinue()) return;
            _latestSource = source with { AudioBytes = [] };
            _lastAudioEndedAt = source.EndedAt;
            await _sendGate.WaitAsync(cancellationToken);
            try
            {
                if (_socket.State != WebSocketState.Open) throw new WebSocketException("Deepgram stream is not open.");
                await _socket.SendAsync(new ArraySegment<byte>(audioBytes), WebSocketMessageType.Binary, true, cancellationToken);
            }
            finally { _sendGate.Release(); }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[32 * 1024];
            var closeLogged = false;
            try
            {
                while (!cancellationToken.IsCancellationRequested && _socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    using var message = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            LogClosed(result.CloseStatus, result.CloseStatusDescription);
                            closeLogged = true;
                            return;
                        }
                        message.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    _logger.LogInformation(
                        "[CAPTION_PROVIDER] event=RECEIVE callSessionId={CallSessionId} messageType={MessageType} payloadLength={PayloadLength}",
                        _sessionId,
                        result.MessageType,
                        message.Length);
                    var parsed = ParseTranscript(Encoding.UTF8.GetString(message.GetBuffer(), 0, checked((int)message.Length)));

                    if (parsed is null || parsed.Channel?.Alternatives is not { Count: > 0 })
                    {
                        _logger.LogInformation(
                            "[CAPTION_PROVIDER] event=EMPTY_RESULT callSessionId={CallSessionId} messageType={MessageType} payloadLength={PayloadLength}",
                            _sessionId,
                            result.MessageType,
                            message.Length);
                        continue;
                    }
                    var text = parsed.Channel.Alternatives[0].Transcript?.Trim();
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        _logger.LogInformation(
                            "[CAPTION_PROVIDER] event=EMPTY_RESULT callSessionId={CallSessionId} messageType={MessageType} payloadLength={PayloadLength}",
                            _sessionId,
                            result.MessageType,
                            message.Length);
                        continue;
                    }
                    var isFinal = parsed.IsFinal;
                    var isUtteranceFinal = parsed.SpeechFinal || parsed.FromFinalize;
                    if (isFinal) _finalSegments.Add(text);

                    var displayText = isUtteranceFinal || isFinal
                        ? string.Join(" ", _finalSegments).Trim()
                        : string.Join(" ", _finalSegments.Append(text)).Trim();
                    if (isUtteranceFinal) _finalSegments.Clear();
                    _logger.LogInformation(
                        "[CAPTION_PROVIDER] event=RESULT callSessionId={CallSessionId} isFinal={IsFinal} speechFinal={SpeechFinal} textLength={TextLength} confidence={Confidence} language={Language}",
                        _sessionId,
                        isFinal,
                        isUtteranceFinal,
                        text.Length,
                        parsed.Channel.Alternatives[0].Confidence,
                        _latestSource.Language);
                    var resultValue = new CallTranscriptionResult(
                        displayText,
                        _streamStartedAt,
                        _lastAudioEndedAt < _streamStartedAt ? _streamStartedAt : _lastAudioEndedAt,
                        parsed.Channel.Alternatives[0].Confidence,
                        isUtteranceFinal,
                        isUtteranceFinal,
                        "Deepgram",
                        parsed.Duration);
                    await Volatile.Read(ref _callbacks).OnResult(_latestSource, resultValue);
                    if (isUtteranceFinal && parsed.Duration is > 0)
                    {
                        await _usageSink.RecordAsync(
                            new CallTranscriptionUsage("Deepgram", _sessionId, _speakerId, parsed.Duration.Value, _options.Model),
                            cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                LogClosed(_socket.CloseStatus, "receive loop canceled");
                closeLogged = true;
            }
            catch (Exception exception)
            {
                LogError(exception);
                throw;
            }
            finally
            {
                if (!closeLogged)
                {
                    LogClosed(_socket.CloseStatus, _socket.CloseStatusDescription);
                }
            }
        }

        private void LogClosed(WebSocketCloseStatus? closeStatus, string? closeDescription)
        {
            _logger.LogInformation(
                "[CAPTION_PROVIDER] event=CLOSED callSessionId={CallSessionId} closeStatus={CloseStatus} safeCloseDescription={SafeCloseDescription}",
                _sessionId,
                closeStatus?.ToString() ?? "none",
                SafeCloseDescription(closeDescription));
        }

        private void LogError(Exception exception)
        {
            _logger.LogError(
                "[CAPTION_PROVIDER] event=ERROR callSessionId={CallSessionId} exceptionType={ExceptionType} safeMessage={SafeMessage}",
                _sessionId,
                exception.GetType().FullName,
                SafeMessage(exception));
        }

        private static string SafeCloseDescription(string? value) =>
            SafeMessage(value ?? "");

        private static string SafeMessage(Exception exception) => SafeMessage(exception.Message);

        private static string SafeMessage(string value)
        {
            var message = value.Replace("\r", " ").Replace("\n", " ");
            var accessTokenIndex = message.IndexOf("access_token=", StringComparison.OrdinalIgnoreCase);
            if (accessTokenIndex >= 0)
            {
                var end = message.IndexOfAny(['&', ' ', '"'], accessTokenIndex);
                message = message[..accessTokenIndex] + "access_token=[redacted]" + (end >= 0 ? message[end..] : string.Empty);
            }
            return message.Length <= 256 ? message : message[..256];
        }

        public ValueTask DisposeAsync() => DisposeAsync("session disposed");

        public async ValueTask DisposeAsync(string reason)
        {
            _logger.LogInformation(
                "[CAPTION_PROVIDER] event=LIFETIME_CANCEL callSessionId={CallSessionId} reason={Reason}",
                _sessionId,
                reason);
            _lifetime.Cancel();
            try
            {
                await _sendGate.WaitAsync(TimeSpan.FromMilliseconds(200));
                if (_socket.State == WebSocketState.Open)
                {
                    var finalize = Encoding.UTF8.GetBytes("{\"type\":\"Finalize\"}");
                    await _socket.SendAsync(new ArraySegment<byte>(finalize), WebSocketMessageType.Text, true, CancellationToken.None);
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "call ended", CancellationToken.None);
                }
                _sendGate.Release();
            }
            catch { }
            if (_receiveTask is not null)
            {
                try { await _receiveTask.WaitAsync(TimeSpan.FromSeconds(1)); } catch { }
            }
            _socket.Dispose();
            _sendGate.Dispose();
            _lifetime.Dispose();
        }

        private sealed record SessionCallbacks(
            Func<CallAudioChunk, CallTranscriptionResult, Task> OnResult,
            Func<bool> CanContinue);
    }

    private static class JsonOptions
    {
        public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web);
    }
}

public sealed class DeepgramTranscript
{
    [JsonPropertyName("is_final")] public bool IsFinal { get; set; }
    [JsonPropertyName("speech_final")] public bool SpeechFinal { get; set; }
    [JsonPropertyName("from_finalize")] public bool FromFinalize { get; set; }
    [JsonPropertyName("duration")] public double? Duration { get; set; }
    [JsonPropertyName("channel")] public DeepgramChannel? Channel { get; set; }
}

public sealed class DeepgramChannel
{
    [JsonPropertyName("alternatives")] public List<DeepgramAlternative> Alternatives { get; set; } = [];
}

public sealed class DeepgramAlternative
{
    [JsonPropertyName("transcript")] public string? Transcript { get; set; }
    [JsonPropertyName("confidence")] public double? Confidence { get; set; }
}
