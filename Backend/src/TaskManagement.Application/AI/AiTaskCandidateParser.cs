using System.Globalization;
using System.Text.RegularExpressions;

namespace TaskManagement.Application.AI;

public sealed class AiTaskCandidateDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DueDate { get; set; }
    public int Priority { get; set; } = 3;
    public string? AssigneeSuggestion { get; set; }
    public string? Reason { get; set; }
    public string SourceProvider { get; set; } = string.Empty;
    public string SourceItemId { get; set; } = string.Empty;
    public string? AttachmentFileName { get; set; }
    public bool Uncertain { get; set; }
    public List<AiTaskCandidateEvidenceDto> Evidence { get; set; } = new();
}

public sealed class AiTaskCandidateEvidenceDto
{
    public string Field { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Type { get; set; } = "Inferred";
    public string? AttachmentFileName { get; set; }
}

public static partial class AiTaskCandidateParser
{
    public static AiTaskCandidateDto? ExtractStructuredCandidate(string content, string provider, Guid sourceItemId)
    {
        var title = MatchValue(content, "Task title|Tiêu đề task");
        if (string.IsNullOrWhiteSpace(title)) return null;

        var attachmentFileName = MatchValue(content, "Attachment");
        var sourceValue = MatchValue(content, "Source");
        if (string.IsNullOrWhiteSpace(attachmentFileName)
            && sourceValue?.StartsWith("attachment ", StringComparison.OrdinalIgnoreCase) == true)
        {
            attachmentFileName = sourceValue["attachment ".Length..].Trim();
        }
        var description = MatchValue(content, "Description|Mô tả");
        var dueDateText = MatchValue(content, "Due date|Hạn");
        var priorityText = MatchValue(content, "Priority|Ưu tiên");
        var assigneeSuggestion = MatchValue(content, "Assignee suggestion|Gợi ý người được giao|Assignee");
        var dueDate = TryNormalizeDate(dueDateText);
        var priority = ParsePriority(priorityText);
        var source = string.IsNullOrWhiteSpace(attachmentFileName)
            ? $"{provider}/inbox/{sourceItemId:N}"
            : $"{provider}/attachment/{attachmentFileName}";

        var candidate = new AiTaskCandidateDto
        {
            Id = $"source-{sourceItemId:N}-1",
            Title = title.Trim(),
            Description = NullIfEmpty(description),
            DueDate = dueDate,
            Priority = priority,
            AssigneeSuggestion = NullIfEmpty(assigneeSuggestion),
            SourceProvider = provider,
            SourceItemId = sourceItemId.ToString(),
            AttachmentFileName = NullIfEmpty(attachmentFileName),
            Reason = "Được trích xuất từ dữ liệu inbox/attachment đã xác thực.",
            Uncertain = string.IsNullOrWhiteSpace(dueDateText) || string.IsNullOrWhiteSpace(priorityText)
        };

        AddEvidence(candidate, "title", candidate.Title, source, attachmentFileName);
        if (!string.IsNullOrWhiteSpace(candidate.Description)) AddEvidence(candidate, "description", candidate.Description, source, attachmentFileName);
        if (!string.IsNullOrWhiteSpace(candidate.DueDate)) AddEvidence(candidate, "dueDate", candidate.DueDate, source, attachmentFileName);
        if (!string.IsNullOrWhiteSpace(priorityText)) AddEvidence(candidate, "priority", priorityText.Trim(), source, attachmentFileName);
        if (!string.IsNullOrWhiteSpace(candidate.AssigneeSuggestion)) AddEvidence(candidate, "assigneeSuggestion", candidate.AssigneeSuggestion, source, attachmentFileName);
        return candidate;
    }

    public static int ParsePriority(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "1" or "urgent" or "critical" or "khẩn cấp" => 1,
            "2" or "high" or "cao" => 2,
            "4" or "low" or "thấp" => 4,
            _ => 3
        };
    }

    private static string? MatchValue(string content, string labels)
    {
        var match = Regex.Match(
            content ?? string.Empty,
            $@"(?im)^[ \t]*(?:{labels})[ \t]*:[ \t]*(?:(?<value>[^\r\n]+?)[ \t]*|(?:\r?\n[ \t]*)+(?<value>[^\r\n]+?)[ \t]*)(?:\r?$)",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static string? TryNormalizeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
            ? parsed.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
            : null;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddEvidence(AiTaskCandidateDto candidate, string field, string? value, string source, string? attachmentFileName)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        candidate.Evidence.Add(new AiTaskCandidateEvidenceDto
        {
            Field = field,
            Value = value.Trim(),
            Source = source,
            Type = "Extracted",
            AttachmentFileName = NullIfEmpty(attachmentFileName)
        });
    }
}
