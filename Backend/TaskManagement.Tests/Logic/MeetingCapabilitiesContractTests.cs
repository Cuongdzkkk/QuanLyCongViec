using FluentAssertions;
using TaskManagement.Infrastructure.Services;

namespace TaskManagement.Tests.Logic;

public sealed class MeetingCapabilitiesContractTests
{
    [Fact]
    public void Transcription_defaults_are_safe_and_limited_to_supported_languages()
    {
        var options = new CallTranscriptionOptions();

        options.Enabled.Should().BeFalse();
        options.Provider.Should().Be("Deepgram");
        options.Language.Should().Be("vi");
        options.SupportedLanguages.Should().BeEquivalentTo(["vi", "en"]);
        options.Deepgram.ApiKey.Should().BeNull();
    }
}
