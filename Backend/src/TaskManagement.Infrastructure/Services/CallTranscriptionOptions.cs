namespace TaskManagement.Infrastructure.Services;

public sealed class CallTranscriptionOptions
{
    public const string SectionName = "CallTranscription";

    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Deepgram";
    public string Language { get; set; } = "vi";
    public int SampleRate { get; set; } = 16000;
    public int EndpointingMilliseconds { get; set; } = 450;
    public string Model { get; set; } = "nova-3";
    public DeepgramCallTranscriptionOptions Deepgram { get; set; } = new();

    public bool IsConfigured =>
        Enabled &&
        string.Equals(Provider, "Deepgram", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Language, "vi", StringComparison.OrdinalIgnoreCase) &&
        SampleRate is >= 8000 and <= 48000 &&
        !string.IsNullOrWhiteSpace(Model) &&
        !string.IsNullOrWhiteSpace(Deepgram.ApiKey);
}

public sealed class DeepgramCallTranscriptionOptions
{
    public string? ApiKey { get; set; }
    public string Endpoint { get; set; } = "wss://api.deepgram.com/v1/listen";
}
