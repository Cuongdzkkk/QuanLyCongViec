using FluentAssertions;
using TaskManagement.Application.AI;
using TaskManagement.Application.Interfaces;

namespace TaskManagement.Tests.Logic;

public sealed class IntegrationAiInputBuilderTests
{
    [Fact]
    public void GmailInputKeepsBodyAndAttachmentAsSeparateProvenanceBoundaries()
    {
        var input = IntegrationAiInputBuilder.BuildGmailInput(
            "gmail",
            "email",
            "gmail-message-1",
            "[SprintA Attachment Test] Yêu cầu nằm trong file TXT",
            "Công việc chi tiết nằm trong file đính kèm.",
            "sender@example.com",
            new[]
            {
                new NormalizedAttachment
                {
                    FileName = "sprinta_test_requirements.txt",
                    MimeType = "text/plain",
                    Source = "gmail/attachment/sprinta_test_requirements.txt",
                    ExtractionMethod = "utf8-text",
                    TextContent = "Task title:\nFix AI task creation confirmation flow\n\nPriority:\nHigh\n\nDue date:\n2026-09-07 17:00"
                }
            });

        input.Should().Contain("SOURCE: Gmail body\nCông việc chi tiết nằm trong file đính kèm.");
        input.Should().Contain("SOURCE: gmail/attachment/sprinta_test_requirements.txt");
        input.Should().Contain("TYPE: text/plain");
        input.Should().Contain("Task title:\nFix AI task creation confirmation flow");
        input.IndexOf("SOURCE: Gmail body", StringComparison.Ordinal)
            .Should().BeLessThan(input.IndexOf("SOURCE: gmail/attachment/sprinta_test_requirements.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void GmailInputMarksFailedAttachmentAndLimitsCandidateBasisToBody()
    {
        var input = IntegrationAiInputBuilder.BuildGmailInput(
            "gmail",
            "email",
            "gmail-message-2",
            "Subject",
            "Body",
            null,
            Array.Empty<NormalizedAttachment>(),
            new[] { "broken.txt" });

        input.Should().Contain("ATTACHMENT_ANALYSIS_FAILED");
        input.Should().Contain("FILENAME: broken.txt");
        input.Should().Contain("CANDIDATE_BASIS: Gmail body only");
    }
}
