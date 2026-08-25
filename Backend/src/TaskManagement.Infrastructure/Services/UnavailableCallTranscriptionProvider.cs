using TaskManagement.Application.DTOs.Collaboration;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Infrastructure.Services;

public sealed class UnavailableCallTranscriptionProvider : ICallTranscriptionProvider
{
    public bool IsConfigured => false;
    public string ProviderName => "Unavailable";
    public IReadOnlyList<string> SupportedLanguages => [];
    public string DefaultLanguage => "vi";

    public Task<CallTranscriptionResult?> TranscribeAsync(
        CallAudioChunk chunk,
        CancellationToken cancellationToken = default) =>
        throw new CallTranscriptionProviderUnavailableException();
}
