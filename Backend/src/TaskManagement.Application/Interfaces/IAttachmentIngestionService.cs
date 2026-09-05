namespace TaskManagement.Application.Interfaces;

public interface IAttachmentIngestionService
{
    Task<NormalizedAttachment> NormalizeAsync(
        string fileName,
        string mimeType,
        Stream content,
        long fileSize,
        string source,
        CancellationToken cancellationToken = default);
}

public sealed class NormalizedAttachment
{
    public string FileName { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public string? TextContent { get; init; }
    public IReadOnlyDictionary<string, object?> StructuredContent { get; init; } =
        new Dictionary<string, object?>();
    public string Source { get; init; } = string.Empty;
    public string ExtractionMethod { get; init; } = string.Empty;
}
