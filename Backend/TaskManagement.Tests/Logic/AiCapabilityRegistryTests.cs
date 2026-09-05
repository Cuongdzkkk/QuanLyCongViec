using FluentAssertions;
using TaskManagement.Application.AI;

namespace TaskManagement.Tests.Logic;

public sealed class AiCapabilityRegistryTests
{
    [Fact]
    public void CanonicalTaskCapabilitiesExposeExecutableContracts()
    {
        var create = AiActionCatalog.Definitions["create_task"];
        var status = AiActionCatalog.Definitions["update_task_status"];
        var assign = AiActionCatalog.Definitions["assign_task"];
        var comment = AiActionCatalog.Definitions["add_comment"];

        create.ActionKey.Should().Be("task.create");
        create.ArgumentSchema.Should().ContainKey("title");
        create.ArgumentSchema["title"].Required.Should().BeTrue();
        create.RequiresConfirmation.Should().BeTrue();
        create.Executor.Should().Be("AiController.ExecuteCreateTaskAsync");

        status.ActionKey.Should().Be("task.changeStatus");
        assign.ActionKey.Should().Be("task.assign");
        comment.ActionKey.Should().Be("task.comment");
        new[] { status, assign, comment }.Should().AllSatisfy(capability =>
        {
            capability.RequiresConfirmation.Should().BeTrue();
            capability.Available.Should().BeTrue();
            capability.RequiredPermission.Should().Be("project.write");
            capability.ConfirmationPolicy.Should().Be("explicit_user_confirmation");
            capability.Executor.Should().StartWith("AiController.Execute");
        });
    }

    [Fact]
    public void QuickToolsAreMarkedOnlyForCapabilitiesWithRealExecutors()
    {
        AiActionCatalog.Definitions
            .Where(pair => pair.Value.QuickTool)
            .Should().AllSatisfy(pair =>
            {
                pair.Value.Available.Should().BeTrue();
                pair.Value.Executor.Should().NotBeNullOrWhiteSpace();
                pair.Value.QuickPrompt.Should().NotBeNullOrWhiteSpace();
            });
    }
}
