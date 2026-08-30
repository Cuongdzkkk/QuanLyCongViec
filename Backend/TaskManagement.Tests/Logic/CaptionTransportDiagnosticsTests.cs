using FluentAssertions;
using TaskManagement.Application.Diagnostics;

namespace TaskManagement.Tests.Logic;

public sealed class CaptionTransportDiagnosticsTests
{
    [Fact]
    public void Same_bytes_produce_matching_sha256_diagnostic()
    {
        var bytes = new byte[] { 0, 1, 2, 3, 255, 128 };
        var hash = CaptionTransportDiagnostics.ComputeSha256Hex(bytes);

        hash.Should().Be(CaptionTransportDiagnostics.ComputeSha256Hex(bytes));
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Transport_diagnostic_sampling_is_optional_for_backward_safe_audio_binding()
    {
        CaptionTransportDiagnostics.IsSampledChunk(1).Should().BeTrue();
        CaptionTransportDiagnostics.IsSampledChunk(2).Should().BeFalse();
        CaptionTransportDiagnostics.IsSampledChunk(20).Should().BeTrue();
        CaptionTransportDiagnostics.IsSampledChunk(40).Should().BeTrue();
        CaptionTransportDiagnostics.IsSampledChunk(39).Should().BeFalse();
    }

    [Fact]
    public void One_byte_mutation_produces_a_different_sha256_diagnostic()
    {
        var original = new byte[] { 10, 20, 30, 40 };
        var mutated = original.ToArray();
        mutated[0]++;

        CaptionTransportDiagnostics.ComputeSha256Hex(original)
            .Should().NotBe(CaptionTransportDiagnostics.ComputeSha256Hex(mutated));
    }
}
