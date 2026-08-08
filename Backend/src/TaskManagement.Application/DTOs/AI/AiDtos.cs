using System;
using System.Collections.Generic;

namespace TaskManagement.Application.DTOs.AI
{
    public class AiSubTaskDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public double EstHours { get; set; }
        public int Priority { get; set; } = 3;
    }

    public class AiBreakdownRequestDto
    {
        public Guid ProjectId { get; set; }
        public Guid? ParentTaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool CreateSubtasks { get; set; }
    }

    public class AiCreateSubtasksFromPreviewRequestDto
    {
        public Guid ProjectId { get; set; }
        public Guid ParentTaskId { get; set; }
        public List<AiSubTaskDto> Subtasks { get; set; } = new();
    }

    public class AiChatMessageDto
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class AiChatRequestDto
    {
        public string Message { get; set; } = string.Empty;
        public List<AiChatMessageDto>? History { get; set; }
    }

    public class AiGenerateDescriptionRequestDto
    {
        public string Prompt { get; set; } = string.Empty;
        public string? Context { get; set; }
    }

    public class AiEstimateSuggestionRequestDto
    {
        public Guid ProjectId { get; set; }
        public Guid? WorkItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Priority { get; set; }
        public double StoryPoints { get; set; }
        public int AssigneeCount { get; set; }
        public int SubtaskCount { get; set; }
    }

    public class AiEstimateSuggestionDto
    {
        public double SuggestedHours { get; set; }
        public double SuggestedStoryPoints { get; set; }
        public int SuggestedDays { get; set; }
        public string Complexity { get; set; } = "Medium";
        public string Reasoning { get; set; } = string.Empty;
    }

    public class AiAssigneeSuggestionRequestDto
    {
        public Guid ProjectId { get; set; }
        public Guid? WorkItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Priority { get; set; }
        public double StoryPoints { get; set; }
        public double EstimatedHours { get; set; }
        public int CandidateCount { get; set; } = 3;
    }

    public class AiAssigneeSuggestionCandidateDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string ProjectRole { get; set; } = string.Empty;
        public int ActiveTaskCount { get; set; }
        public double ActiveEstimatedHours { get; set; }
        public double CompletedStoryPoints { get; set; }
        public double AverageAccuracyPercent { get; set; }
        public double LoggedHoursLast30Days { get; set; }
        public double FitScore { get; set; }
        public double SuggestedContributionWeight { get; set; }
        public double SuggestedEstimatedHours { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }

    public class AiAssigneeSuggestionDto
    {
        public string Summary { get; set; } = string.Empty;
        public int RecommendedAssigneeCount { get; set; } = 1;
        public List<AiAssigneeSuggestionCandidateDto> Suggestions { get; set; } = new();
    }

    public class AiRepositoryAnalysisRequestDto
    {
        public string RepoUrl { get; set; } = string.Empty;
        public string? GitHubToken { get; set; }
        public string? Focus { get; set; }
    }

    public class AiRepositoryBacklogItemDto
    {
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = "medium";
        public double SuggestedHours { get; set; }
        public int Priority { get; set; } = 3;
        public string Reasoning { get; set; } = string.Empty;
    }

    public class AiRepositoryAnalysisDto
    {
        public string Repository { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<AiRepositoryBacklogItemDto> QuickWins { get; set; } = new();
        public List<AiRepositoryBacklogItemDto> MediumTasks { get; set; } = new();
        public List<AiRepositoryBacklogItemDto> RiskyTasks { get; set; } = new();
        public List<string> TestPlan { get; set; } = new();
        public string SuggestedPrompt { get; set; } = string.Empty;
    }

    public class AiCreateBacklogFromAnalysisRequestDto
    {
        public Guid ProjectId { get; set; }
        public Guid? TargetSprintId { get; set; }
        public string Repository { get; set; } = string.Empty;
        public bool IncludeQuickWins { get; set; } = true;
        public bool IncludeMediumTasks { get; set; } = true;
        public bool IncludeRiskyTasks { get; set; } = true;
        public List<AiRepositoryBacklogItemDto> SelectedItems { get; set; } = new();
        public List<AiRepositoryBacklogItemDto> QuickWins { get; set; } = new();
        public List<AiRepositoryBacklogItemDto> MediumTasks { get; set; } = new();
        public List<AiRepositoryBacklogItemDto> RiskyTasks { get; set; } = new();
    }

    public class AiUsageDto
    {
        public long UsedTokensThisMonth { get; set; }
        public long MonthlyTokenQuota { get; set; }
        public long RemainingTokens => Math.Max(0, MonthlyTokenQuota - UsedTokensThisMonth);
        public string PlanCode { get; set; } = string.Empty;
        public string EntitlementSource { get; set; } = string.Empty;
        public int IncludedCredits { get; set; }
        public int UsedCredits { get; set; }
        public int RemainingCredits => Math.Max(0, IncludedCredits - UsedCredits);
        public bool IsCreditQuotaExceeded { get; set; }
    }
}
