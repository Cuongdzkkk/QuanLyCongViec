using System.Text;
using FluentAssertions;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class DeepgramDiagnosticTests
{
    [Fact]
    public void Results_with_transcript_is_classified_without_transcript_content()
    {
        const string json = "{\"type\":\"Results\",\"is_final\":false,\"speech_final\":false,\"channel\":{\"alternatives\":[{\"transcript\":\"xin chao\",\"confidence\":0.9}]}}";

        var diagnostics = DeepgramCallTranscriptionProvider.ClassifyResponse(json);
        var parsed = DeepgramCallTranscriptionProvider.ParseTranscript(json);

        diagnostics.MessageType.Should().Be("Results");
        diagnostics.ParseSucceeded.Should().BeTrue();
        diagnostics.HasChannel.Should().BeTrue();
        diagnostics.AlternativesCount.Should().Be(1);
        diagnostics.HasTranscript.Should().BeTrue();
        diagnostics.TranscriptLength.Should().Be(8);
        parsed!.Channel!.Alternatives[0].Transcript.Should().Be("xin chao");
    }

    [Fact]
    public void Results_with_blank_transcript_is_distinguishable()
    {
        var diagnostics = DeepgramCallTranscriptionProvider.ClassifyResponse(
            "{\"type\":\"Results\",\"channel\":{\"alternatives\":[{\"transcript\":\"  \"}]}}");

        diagnostics.MessageType.Should().Be("Results");
        diagnostics.HasTranscript.Should().BeTrue();
        diagnostics.TranscriptLength.Should().Be(2);
        diagnostics.ParseSucceeded.Should().BeTrue();
    }

    [Fact]
    public void Results_without_alternatives_is_distinguishable()
    {
        var diagnostics = DeepgramCallTranscriptionProvider.ClassifyResponse(
            "{\"type\":\"Results\",\"channel\":{\"alternatives\":[]}}");

        diagnostics.MessageType.Should().Be("Results");
        diagnostics.HasChannel.Should().BeTrue();
        diagnostics.AlternativesCount.Should().Be(0);
        diagnostics.ParseSucceeded.Should().BeTrue();
    }

    [Fact]
    public void Metadata_is_classified_as_a_non_results_message()
    {
        var diagnostics = DeepgramCallTranscriptionProvider.ClassifyResponse(
            "{\"type\":\"Metadata\",\"request_id\":\"test\",\"channels\":1}");

        diagnostics.MessageType.Should().Be("Metadata");
        diagnostics.HasChannel.Should().BeFalse();
        diagnostics.HasTranscript.Should().BeFalse();
        diagnostics.ParseSucceeded.Should().BeTrue();
    }

    [Fact]
    public void Malformed_json_is_reported_as_parse_failure_without_exposing_payload()
    {
        const string json = "{\"type\":\"Results\",\"secret\":\"never-log-this\"";
        var diagnostics = DeepgramCallTranscriptionProvider.ClassifyResponse(json);

        diagnostics.ParseSucceeded.Should().BeFalse();
        diagnostics.MessageType.Should().Be("unknown");
        Encoding.UTF8.GetBytes(json).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void All_zero_pcm_is_silence()
    {
        var diagnostics = DeepgramCallTranscriptionProvider.AnalyzePcm(new byte[8000]);

        diagnostics.SampleCount.Should().Be(4000);
        diagnostics.NonZeroSampleCount.Should().Be(0);
        diagnostics.ZeroSamplePercent.Should().Be(100);
        diagnostics.Rms.Should().Be(0);
        diagnostics.SignalClass.Should().Be("SILENCE");
    }

    [Fact]
    public void Synthetic_non_zero_pcm_is_active()
    {
        var bytes = new byte[8000];
        for (var index = 0; index < bytes.Length; index += 2)
        {
            var sample = (short)((index / 2 % 32) < 16 ? 12000 : -12000);
            BitConverter.TryWriteBytes(bytes.AsSpan(index, 2), sample);
        }

        var diagnostics = DeepgramCallTranscriptionProvider.AnalyzePcm(bytes);

        diagnostics.NonZeroSampleCount.Should().Be(4000);
        diagnostics.Rms.Should().BeGreaterThan(0.012);
        diagnostics.SignalClass.Should().Be("ACTIVE");
    }
}
