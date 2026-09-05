using System.Collections.Generic;

namespace TaskManagement.Application.AI;

public enum AiCapabilityKind
{
    Read,
    Write,
    Analyze
}

[Flags]
public enum AiCapabilityContext
{
    General = 1,
    Dashboard = 2,
    Project = 4,
    Goal = 8,
    All = General | Dashboard | Project | Goal
}

public sealed record AiActionDefinition(
    string EntityType,
    bool RequiresConfirmation,
    string DisplayName,
    AiCapabilityKind CapabilityKind,
    AiCapabilityContext Context = AiCapabilityContext.All,
    bool DirectExecution = false)
{
    // Legacy dictionary keys remain accepted by the API. ActionKey is the
    // stable capability identifier exposed to the model and clients.
    public string ActionKey { get; init; } = string.Empty;
    public IReadOnlyList<string> AliasesIntents { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, AiArgumentDefinition> ArgumentSchema { get; init; } =
        new Dictionary<string, AiArgumentDefinition>(StringComparer.OrdinalIgnoreCase);
    public string RiskLevel { get; init; } = "low";
    public string RequiredPermission { get; init; } = "workspace.read";
    public string ConfirmationPolicy { get; init; } = "none";
    public string Executor { get; init; } = string.Empty;
    public bool Available { get; init; } = true;
    public bool QuickTool { get; init; }
    public string QuickPrompt { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
}

public sealed record AiArgumentDefinition(
    string Type,
    bool Required = false,
    string Description = "");

public static class AiActionCatalog
{
    private static readonly IReadOnlyDictionary<string, (string ActionKey, string[] Aliases, Dictionary<string, AiArgumentDefinition> Schema)> CanonicalWriteMetadata =
        new Dictionary<string, (string, string[], Dictionary<string, AiArgumentDefinition>)>(StringComparer.OrdinalIgnoreCase)
        {
            ["create_task"] = ("task.create", ["task.create", "create_task", "create work item", "tạo task", "tạo công việc"],
                new(StringComparer.OrdinalIgnoreCase)
                {
                    ["title"] = new("string", true, "Task title"),
                    ["projectId"] = new("uuid", false, "Destination project"),
                    ["description"] = new("string", false, "Task description"),
                    ["dueDate"] = new("date-time", false, "Due date"),
                    ["priority"] = new("integer", false, "1 urgent, 2 high, 3 medium, 4 low"),
                    ["assigneeId"] = new("uuid", false, "Active project member")
                }),
            ["update_task_status"] = ("task.changeStatus", ["task.changeStatus", "update_task_status", "move_task", "đổi trạng thái task"],
                new(StringComparer.OrdinalIgnoreCase)
                {
                    ["taskId"] = new("uuid", true, "Task to update"),
                    ["statusName"] = new("string", false, "Target status name"),
                    ["taskStatusId"] = new("uuid", false, "Target status id")
                }),
            ["assign_task"] = ("task.assign", ["task.assign", "assign_task", "assign work item", "giao task", "phân công công việc"],
                new(StringComparer.OrdinalIgnoreCase)
                {
                    ["taskId"] = new("uuid", true, "Task to assign"),
                    ["assigneeId"] = new("uuid", true, "Active project member")
                }),
            ["add_comment"] = ("task.comment", ["task.comment", "add_comment", "comment", "thêm bình luận", "bình luận task"],
                new(StringComparer.OrdinalIgnoreCase)
                {
                    ["entityId"] = new("uuid", true, "Comment target"),
                    ["entityType"] = new("string", false, "WorkTask, Project, or Goal"),
                    ["content"] = new("string", true, "Comment content")
                })
        };

    public static IReadOnlyDictionary<string, AiActionDefinition> Definitions { get; } = BuildDefinitions();

    private static IReadOnlyDictionary<string, AiActionDefinition> BuildDefinitions()
    {
        var definitions = new Dictionary<string, AiActionDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["create_project"] = new("Project", true, "Tạo dự án", AiCapabilityKind.Write),
            ["create_task"] = new("WorkTask", true, "Tạo công việc", AiCapabilityKind.Write),
            ["create_cycle"] = new("Sprint", true, "Tạo sprint", AiCapabilityKind.Write),
            ["create_module"] = new("Module", true, "Tạo module", AiCapabilityKind.Write),
            ["create_page"] = new("Page", true, "Tạo trang", AiCapabilityKind.Write),
            ["create_view"] = new("ProjectView", true, "Tạo view", AiCapabilityKind.Write),
            ["create_intake_request"] = new("Intake", true, "Tạo yêu cầu tiếp nhận", AiCapabilityKind.Write),
            ["update_task_status"] = new("WorkTask", true, "Cập nhật trạng thái công việc", AiCapabilityKind.Write),
            ["update_task_priority"] = new("WorkTask", true, "Cập nhật độ ưu tiên công việc", AiCapabilityKind.Write),
            ["update_task_due_date"] = new("WorkTask", true, "Cập nhật hạn công việc", AiCapabilityKind.Write),
            ["assign_task"] = new("WorkTask", true, "Phân công công việc", AiCapabilityKind.Write),
            ["add_comment"] = new("Comment", true, "Thêm bình luận", AiCapabilityKind.Write),
            ["create_goal"] = new("Goal", true, "Tạo mục tiêu", AiCapabilityKind.Write),
            ["summarize_dashboard"] = new("Summary", false, "Tóm tắt dashboard", AiCapabilityKind.Analyze, AiCapabilityContext.General | AiCapabilityContext.Dashboard),
            ["summarize_project"] = new("Summary", false, "Tóm tắt dự án", AiCapabilityKind.Analyze, AiCapabilityContext.Project),
            ["list_work_items"] = new("WorkTaskList", false, "Liệt kê công việc", AiCapabilityKind.Read, AiCapabilityContext.Project),
            ["list_cycles"] = new("SprintList", false, "Liệt kê sprint", AiCapabilityKind.Read, AiCapabilityContext.Project),
            ["list_modules"] = new("ModuleList", false, "Liệt kê module", AiCapabilityKind.Read, AiCapabilityContext.Project),
            ["list_pages"] = new("PageList", false, "Liệt kê trang", AiCapabilityKind.Read, AiCapabilityContext.Project),
            ["list_views"] = new("ProjectViewList", false, "Liệt kê view", AiCapabilityKind.Read, AiCapabilityContext.Project),
            ["list_intakes"] = new("IntakeList", false, "Liệt kê yêu cầu tiếp nhận", AiCapabilityKind.Read, AiCapabilityContext.Project),
            ["list_overdue_tasks"] = new("WorkTaskList", false, "Liệt kê công việc quá hạn", AiCapabilityKind.Read, AiCapabilityContext.Dashboard | AiCapabilityContext.Project),
            ["get_workload"] = new("Workload", false, "Phân tích khối lượng công việc", AiCapabilityKind.Analyze, AiCapabilityContext.Project),
            ["explain_report"] = new("ReportExplanation", false, "Giải thích báo cáo", AiCapabilityKind.Analyze, AiCapabilityContext.Project),
            ["summarize_page"] = new("PageSummary", false, "Tóm tắt trang", AiCapabilityKind.Analyze, AiCapabilityContext.Project),
            ["summarize_intakes"] = new("IntakeSummary", false, "Tóm tắt yêu cầu tiếp nhận", AiCapabilityKind.Analyze, AiCapabilityContext.Project),
            ["suggest_view_filter"] = new("ViewFilterSuggestion", false, "Gợi ý bộ lọc view", AiCapabilityKind.Analyze, AiCapabilityContext.Project),
            ["get_task_details"] = new("WorkTask", false, "Xem chi tiết công việc", AiCapabilityKind.Read, AiCapabilityContext.Project, true),
            ["search_tasks"] = new("WorkTaskList", false, "Tìm kiếm công việc", AiCapabilityKind.Read, AiCapabilityContext.Project, true),
            ["list_task_comments"] = new("CommentList", false, "Liệt kê bình luận công việc", AiCapabilityKind.Read, AiCapabilityContext.Project, true),
            ["list_task_dependencies"] = new("TaskDependencyList", false, "Liệt kê phụ thuộc công việc", AiCapabilityKind.Read, AiCapabilityContext.Project, true),
            ["list_project_labels"] = new("LabelList", false, "Liệt kê nhãn dự án", AiCapabilityKind.Read, AiCapabilityContext.Project, true),
            ["list_task_custom_fields"] = new("CustomFieldList", false, "Xem trường tùy chỉnh công việc", AiCapabilityKind.Read, AiCapabilityContext.Project, true),
            ["list_project_members"] = new("ProjectMemberList", false, "Liệt kê thành viên dự án", AiCapabilityKind.Read, AiCapabilityContext.Project, true),
            ["get_goal_details"] = new("Goal", false, "Xem chi tiết mục tiêu", AiCapabilityKind.Read, AiCapabilityContext.General | AiCapabilityContext.Goal, true),
            ["get_personal_work_summary"] = new("PersonalWorkSummary", false, "Tóm tắt công việc cá nhân", AiCapabilityKind.Analyze, AiCapabilityContext.General | AiCapabilityContext.Dashboard, true),
            ["get_planning_summary"] = new("PlanningSummary", false, "Tóm tắt kế hoạch dự án", AiCapabilityKind.Analyze, AiCapabilityContext.Project, true)
        };

        var quickTools = new Dictionary<string, (string Prompt, string Icon)>(StringComparer.OrdinalIgnoreCase)
        {
            ["create_task"] = ("Tạo task mới trong project hiện tại với tiêu đề và thông tin tôi sẽ cung cấp.", "fa-solid fa-square-plus"),
            ["summarize_project"] = ("Tóm tắt dự án hiện tại và nêu 3 điểm cần chú ý.", "fa-solid fa-chart-simple"),
            ["get_workload"] = ("Cho tôi biết tải công việc hiện tại của team.", "fa-solid fa-users-viewfinder"),
            ["update_task_status"] = ("Cập nhật trạng thái task tôi chỉ định sang trạng thái tôi nêu.", "fa-solid fa-arrow-right-arrow-left"),
            ["create_cycle"] = ("Tạo sprint mới cho project hiện tại với thông tin tôi sẽ cung cấp.", "fa-solid fa-arrows-spin"),
            ["assign_task"] = ("Giao task tôi chỉ định cho thành viên tôi nêu.", "fa-solid fa-user-check"),
            ["add_comment"] = ("Thêm bình luận vào task tôi chỉ định với nội dung tôi sẽ cung cấp.", "fa-solid fa-comment-medical"),
            ["list_overdue_tasks"] = ("Liệt kê các task đang quá hạn và đề xuất thứ tự xử lý.", "fa-solid fa-calendar-xmark"),
            ["summarize_page"] = ("Tóm tắt trang hiện tại và nêu 3 điểm cần chú ý.", "fa-regular fa-file-lines"),
            ["explain_report"] = ("Giải thích các điểm chính trong báo cáo hiện tại.", "fa-solid fa-magnifying-glass-chart"),
            ["suggest_view_filter"] = ("Gợi ý bộ lọc hữu ích cho dữ liệu hiện tại.", "fa-solid fa-filter-circle-dollar")
        };

        return definitions.ToDictionary(
            pair => pair.Key,
            pair =>
            {
                CanonicalWriteMetadata.TryGetValue(pair.Key, out var metadata);
                quickTools.TryGetValue(pair.Key, out var quickTool);
                var isWrite = pair.Value.CapabilityKind == AiCapabilityKind.Write;
                var actionKey = string.IsNullOrWhiteSpace(metadata.ActionKey) ? pair.Key : metadata.ActionKey;
                var permission = isWrite
                    ? pair.Key is "create_project" or "create_goal" ? "workspace.write" : "project.write"
                    : pair.Value.CapabilityKind == AiCapabilityKind.Read ? "project.read" : "project.read";
                return pair.Value with
                {
                    ActionKey = actionKey,
                    AliasesIntents = metadata.Aliases ?? Array.Empty<string>(),
                    ArgumentSchema = metadata.Schema ?? new Dictionary<string, AiArgumentDefinition>(StringComparer.OrdinalIgnoreCase),
                    RiskLevel = isWrite ? "medium" : "low",
                    RequiredPermission = permission,
                    ConfirmationPolicy = pair.Value.RequiresConfirmation ? "explicit_user_confirmation" : "none",
                    Executor = $"AiController.{ToExecutorName(pair.Key)}",
                    QuickTool = quickTools.ContainsKey(pair.Key),
                    QuickPrompt = quickTool.Prompt ?? string.Empty,
                    Icon = quickTool.Icon ?? string.Empty
                };
            },
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ToExecutorName(string actionKey)
    {
        var parts = actionKey.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return $"Execute{string.Concat(parts.Select(part => char.ToUpperInvariant(part[0]) + part[1..]))}Async";
    }
}
