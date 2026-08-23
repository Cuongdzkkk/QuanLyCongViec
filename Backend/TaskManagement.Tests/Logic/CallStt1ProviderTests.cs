using FluentAssertions;
using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class CallStt1ProviderTests
{
    [Fact]
    public void MissingProviderSecret_isBlockedWithoutAProviderFallbackTranscript()
    {
        var options = new CallTranscriptionOptions
        {
            Enabled = true,
            Provider = "Deepgram",
            Language = "vi",
            Deepgram = new DeepgramCallTranscriptionOptions { ApiKey = null }
        };

        options.IsConfigured.Should().BeFalse();
        new UnavailableCallTranscriptionProvider().IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void DeepgramResult_preservesVietnameseInterimAndFinalFlags()
    {
        const string json = """
            {
              "is_final": true,
              "speech_final": true,
              "duration": 1.25,
              "channel": { "alternatives": [{ "transcript": "Chúng ta chốt phương án B.", "confidence": 0.98 }] }
            }
            """;

        var parsed = DeepgramCallTranscriptionProvider.ParseTranscript(json);

        parsed.Should().NotBeNull();
        parsed!.IsFinal.Should().BeTrue();
        parsed.SpeechFinal.Should().BeTrue();
        parsed.Channel!.Alternatives[0].Transcript.Should().Be("Chúng ta chốt phương án B.");
        parsed.Duration.Should().Be(1.25);
    }

    [Fact]
    public void TranscriptionResult_defaultsToFinalForLegacyProviderContract()
    {
        var result = new CallTranscriptionResult("đã chốt", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, .9);

        result.IsFinal.Should().BeTrue();
        result.IsUtteranceFinal.Should().BeTrue();
    }
}
