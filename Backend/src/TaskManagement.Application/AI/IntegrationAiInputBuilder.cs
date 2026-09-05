using TaskManagement.Application.Interfaces;

namespace TaskManagement.Application.AI;

public static class IntegrationAiInputBuilder
{
    private const string CanonicalNewLine = "\n";

    public static string BuildGmailInput(
        string provider,
        string source,
        string externalId,
        string subject,
        string body,
        string? sender,
        IReadOnlyCollection<NormalizedAttachment> attachments,
        IReadOnlyCollection<string>? failedAttachmentNames = null)
    {
        var sections = new List<string>
        {
            string.Join(CanonicalNewLine, new[]
            {
                "SOURCE: Gmail metadata",
                $"PROVIDER: {provider}",
                $"SOURCE_KIND: {source}",
                $"EXTERNAL_ID: {externalId}",
                $"FROM: {sender ?? string.Empty}",
                $"SUBJECT: {subject}"
            }),
            string.Join(CanonicalNewLine, new[]
            {
                "SOURCE: Gmail body",
                string.IsNullOrWhiteSpace(body) ? "(empty)" : body
            })
        };

        foreach (var attachment in attachments)
        {
            if (string.IsNullOrWhiteSpace(attachment.TextContent)) continue;

            sections.Add(string.Join(CanonicalNewLine, new[]
            {
                $"SOURCE: {attachment.Source}",
                $"Attachment: {attachment.FileName}",
                $"FILENAME: {attachment.FileName}",
                $"TYPE: {attachment.MimeType}",
                $"EXTRACTION_METHOD: {attachment.ExtractionMethod}",
                "CONTENT:",
                attachment.TextContent
            }));
        }

        foreach (var fileName in failedAttachmentNames ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(fileName)) continue;

            sections.Add(string.Join(CanonicalNewLine, new[]
            {
                "ATTACHMENT_ANALYSIS_FAILED",
                $"FILENAME: {Path.GetFileName(fileName)}",
                "CANDIDATE_BASIS: Gmail body only"
            }));
        }

        return string.Join(CanonicalNewLine + CanonicalNewLine, sections);
    }
}
