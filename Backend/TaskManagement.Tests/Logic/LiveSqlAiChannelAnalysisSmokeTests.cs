using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using TaskManagement.Application.DTOs.AI;
using TaskManagement.Infrastructure.AI;
using TaskManagement.Infrastructure.Data;
using TaskManagement.Infrastructure.Services;
using Xunit.Abstractions;

namespace TaskManagement.Tests.Logic;

public sealed class LiveSqlAiChannelAnalysisSmokeTests
{
    private readonly ITestOutputHelper _output;

    public LiveSqlAiChannelAnalysisSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task OneRealAnalysisAndOneConstrainedQuestionUseOnlyChannelText()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CHAT_AI_LIVE_SMOKE"), "1", StringComparison.Ordinal)) return;
        var connectionString = Environment.GetEnvironmentVariable("CHAT_AI_SQL_CONNECTION");
        var apiKey = Environment.GetEnvironmentVariable("ZenMux__ApiKey");
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(apiKey)) return;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var context = new ApplicationDbContext(options);
        var channel = await context.CollaborationChannels
            .Where(item => item.Name == "general" && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id })
            .FirstAsync();
        var userId = await context.CollaborationChannelMembers
            .Where(item => item.ChannelId == channel.Id && item.IsActive && item.LeftAt == null)
            .Select(item => item.UserId)
            .FirstAsync();
        var sourceMessageIds = await context.ChannelMessages
            .Where(item => item.CollaborationChannelId == channel.Id)
            .OrderBy(item => item.SentAt)
            .ThenBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync();
        var plan = await context.AiPricingPlans.SingleAsync(item => item.Code == "free");
        plan.IncludedAiCredits = 100;
        await context.SaveChangesAsync();
        var reservationCountBefore = await context.AiCreditReservations.CountAsync();
        var usageCountBefore = await context.AITokenUsages.CountAsync();

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ZenMux:ApiKey"] = apiKey,
                ["ZenMux:BaseUrl"] = Environment.GetEnvironmentVariable("ZenMux__BaseUrl") ?? "https://zenmux.ai/api/v1",
                ["ZenMux:Model"] = Environment.GetEnvironmentVariable("ZenMux__Model") ?? "deepseek/deepseek-v4-flash"
            })
            .Build();
        var provider = new ZenMuxAiClient(new HttpClient { Timeout = TimeSpan.FromSeconds(60) }, configuration);
        var service = new AiChannelAnalysisService(
            context,
            new ResourceAuthorizationService(context),
            provider,
            new AiCreditUsageService(context),
            configuration,
            new MemoryCache(new MemoryCacheOptions()));

        var analysisRequestId = "live-chat-ai-1-analysis-" + Guid.NewGuid().ToString("N");
        AiChannelAnalysisResponseDto analysis;
        try
        {
            analysis = await service.AnalyzeAsync(userId, channel.Id, new AiChannelAnalysisRequestDto
            {
                RequestId = analysisRequestId
            });
        }
        catch (Exception exception)
        {
            var diagnostics = provider.LastResponseDiagnostics;
            if (diagnostics != null)
            {
                _output.WriteLine($"LIVE_HTTP_STATUS={diagnostics.HttpStatus}");
                _output.WriteLine($"LIVE_FINISH_REASON={diagnostics.FinishReason}");
                _output.WriteLine($"LIVE_CHOICE_COUNT={diagnostics.ChoiceCount}");
                _output.WriteLine($"LIVE_CONTENT_LENGTH={diagnostics.ContentLength}");
                _output.WriteLine($"LIVE_TOOL_CALLS_PRESENT={diagnostics.ToolCallsPresent}");
                _output.WriteLine($"LIVE_REASONING_PRESENT={diagnostics.ReasoningPresent}");
                _output.WriteLine($"LIVE_REFUSAL_PRESENT={diagnostics.RefusalPresent}");
            }
            _output.WriteLine($"LIVE_FAILURE_TYPE={exception.GetType().Name}");
            throw;
        }
        var analysisDiagnostics = provider.LastResponseDiagnostics;
        var replay = await service.AnalyzeAsync(userId, channel.Id, new AiChannelAnalysisRequestDto
        {
            RequestId = analysisRequestId
        });
        var question = await service.AnalyzeAsync(userId, channel.Id, new AiChannelAnalysisRequestDto
        {
            RequestId = "live-chat-ai-1-question-" + Guid.NewGuid().ToString("N"),
            Question = "Cuối cùng nhóm đã chốt phương án nào?"
        });
        var questionDiagnostics = provider.LastResponseDiagnostics;
        var unsupported = await service.AnalyzeAsync(userId, channel.Id, new AiChannelAnalysisRequestDto
        {
            RequestId = "live-chat-ai-1-unsupported-" + Guid.NewGuid().ToString("N"),
            Question = "Doanh thu tháng trước là bao nhiêu?"
        });
        var unsupportedDiagnostics = provider.LastResponseDiagnostics;
        _output.WriteLine($"QA_UNSUPPORTED_NORMALIZED={question.QuestionAnswer?.Unsupported}");
        _output.WriteLine($"QA_EVIDENCE_COUNT={question.QuestionAnswer?.EvidenceMessageIds.Count}");
        _output.WriteLine($"QA_ANSWER_LENGTH={question.QuestionAnswer?.Answer?.Length}");
        _output.WriteLine($"QA_PROVIDER_OUTPUT_PRESENT={service.LastProviderOutputDiagnostics?.QuestionAnswerPresent}");
        _output.WriteLine($"QA_PROVIDER_UNSUPPORTED={service.LastProviderOutputDiagnostics?.QuestionUnsupported}");
        _output.WriteLine($"QA_PROVIDER_EVIDENCE_REFS={service.LastProviderOutputDiagnostics?.QuestionEvidenceRefCount}");

        analysis.Summary.Should().NotBeNullOrWhiteSpace();
        analysis.SourceMessageCount.Should().BeGreaterThan(0);
        analysis.Decisions.Should().NotBeEmpty();
        analysis.Decisions.SelectMany(item => item.EvidenceMessageIds).Should().NotBeEmpty();
        analysis.Decisions.Should().NotContain(item => item.Text.Contains("Redis", StringComparison.OrdinalIgnoreCase));
        analysis.ActionItems.Should().Contain(item =>
            item.AssigneeCandidate == "Alice" &&
            item.DeadlineCandidate == "2026-08-30" &&
            item.EvidenceMessageIds.Count > 0);
        analysis.ActionItems.Where(item => item.AssigneeCandidate != null)
            .Should().OnlyContain(item => item.AssigneeCandidate == "Alice");
        analysis.ActionItems.Where(item => item.DeadlineCandidate != null)
            .Should().OnlyContain(item => item.DeadlineCandidate == "2026-08-30" ||
                item.DeadlineCandidate!.Contains("tuần sau", StringComparison.OrdinalIgnoreCase));
        analysis.Decisions.SelectMany(item => item.EvidenceMessageIds)
            .Concat(analysis.ActionItems.SelectMany(item => item.EvidenceMessageIds))
            .Concat(analysis.OpenQuestions.SelectMany(item => item.EvidenceMessageIds))
            .Should().OnlyContain(id => sourceMessageIds.Contains(id));
        analysis.Summary.ToLowerInvariant().Should().NotContain("server secrets");
        replay.Summary.Should().Be(analysis.Summary);
        question.QuestionAnswer.Should().NotBeNull();
        question.QuestionAnswer!.Unsupported.Should().BeFalse();
        question.QuestionAnswer.Answer.Should().NotBeNullOrWhiteSpace();
        question.QuestionAnswer.EvidenceMessageIds.Should().NotBeEmpty();
        question.QuestionAnswer.EvidenceMessageIds.Should().OnlyContain(id => sourceMessageIds.Contains(id));
        unsupported.QuestionAnswer.Should().NotBeNull();
        unsupported.QuestionAnswer!.Unsupported.Should().BeTrue();
        unsupported.QuestionAnswer.Answer.Should().Contain("Không đủ thông tin");

        var reservationsAfter = await context.AiCreditReservations.CountAsync();
        var usageAfter = await context.AITokenUsages.CountAsync();
        reservationsAfter.Should().Be(reservationCountBefore + 3);
        usageAfter.Should().Be(usageCountBefore + 3);
        (await context.AiCreditReservations
            .Where(item => item.CreatedAt >= DateTime.UtcNow.AddMinutes(-5))
            .CountAsync(item => item.Status == "Finalized"))
            .Should().BeGreaterThanOrEqualTo(3);

        analysisDiagnostics.Should().NotBeNull();
        questionDiagnostics.Should().NotBeNull();
        unsupportedDiagnostics.Should().NotBeNull();
        _output.WriteLine($"LIVE_HTTP_STATUS={analysisDiagnostics!.HttpStatus}");
        _output.WriteLine($"LIVE_FINISH_REASON={analysisDiagnostics.FinishReason}");
        _output.WriteLine($"LIVE_CHOICE_COUNT={analysisDiagnostics.ChoiceCount}");
        _output.WriteLine($"LIVE_CONTENT_LENGTH={analysisDiagnostics.ContentLength}");
        _output.WriteLine($"LIVE_TOOL_CALLS_PRESENT={analysisDiagnostics.ToolCallsPresent}");
        _output.WriteLine($"LIVE_REASONING_PRESENT={analysisDiagnostics.ReasoningPresent}");
        _output.WriteLine($"LIVE_REFUSAL_PRESENT={analysisDiagnostics.RefusalPresent}");
        _output.WriteLine($"SUMMARY_EXISTS={(!string.IsNullOrWhiteSpace(analysis.Summary))}");
        _output.WriteLine($"DECISIONS={analysis.Decisions.Count}");
        _output.WriteLine($"ACTION_ITEMS={analysis.ActionItems.Count}");
        _output.WriteLine($"OPEN_QUESTIONS={analysis.OpenQuestions.Count}");
        _output.WriteLine($"EVIDENCE_VALID={analysis.Decisions.SelectMany(item => item.EvidenceMessageIds).Concat(analysis.ActionItems.SelectMany(item => item.EvidenceMessageIds)).Concat(analysis.OpenQuestions.SelectMany(item => item.EvidenceMessageIds)).All(sourceMessageIds.Contains)}");
        _output.WriteLine($"QA_EVIDENCE_VALID={question.QuestionAnswer.EvidenceMessageIds.All(sourceMessageIds.Contains)}");
        _output.WriteLine($"QA_UNSUPPORTED={question.QuestionAnswer.Unsupported}");
        _output.WriteLine($"UNSUPPORTED_QA={unsupported.QuestionAnswer.Unsupported}");
        _output.WriteLine("LIVE_PROVIDER_CALL_COUNT=3");
        _output.WriteLine($"LIVE_CREDIT_RESERVATIONS={reservationsAfter - reservationCountBefore}");
        _output.WriteLine($"LIVE_CREDIT_FINALIZATIONS={usageAfter - usageCountBefore}");
    }
}
