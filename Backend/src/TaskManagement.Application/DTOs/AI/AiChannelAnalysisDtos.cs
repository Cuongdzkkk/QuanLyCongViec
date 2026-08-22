using System;
using System.Collections.Generic;

namespace TaskManagement.Application.DTOs.AI;

public sealed class AiChannelAnalysisRequestDto
{
    public string RequestId { get; set; } = string.Empty;
    public List<Guid> MessageIds { get; set; } = new();
    public string? Question { get; set; }
}

public sealed class AiChannelAnalysisResponseDto
{
    public string RequestId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string Scope { get; set; } = "text-channel";
    public int SourceMessageCount { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<AiChannelDecisionDto> Decisions { get; set; } = new();
    public List<AiChannelActionItemDto> ActionItems { get; set; } = new();
    public List<AiChannelOpenQuestionDto> OpenQuestions { get; set; } = new();
    public List<string> ImportantPoints { get; set; } = new();
    public AiChannelQuestionAnswerDto? QuestionAnswer { get; set; }
}

public sealed class AiChannelDecisionDto
{
    public string Text { get; set; } = string.Empty;
    public List<Guid> EvidenceMessageIds { get; set; } = new();
    public DateTime? EvidenceTimestamp { get; set; }
}

public sealed class AiChannelActionItemDto
{
    public string Text { get; set; } = string.Empty;
    public string? AssigneeCandidate { get; set; }
    public string? DeadlineCandidate { get; set; }
    public double Confidence { get; set; }
    public List<Guid> EvidenceMessageIds { get; set; } = new();
}

public sealed class AiChannelOpenQuestionDto
{
    public string Text { get; set; } = string.Empty;
    public List<Guid> EvidenceMessageIds { get; set; } = new();
}

public sealed class AiChannelQuestionAnswerDto
{
    public string Answer { get; set; } = string.Empty;
    public bool Unsupported { get; set; }
    public List<Guid> EvidenceMessageIds { get; set; } = new();
}
