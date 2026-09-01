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
    bool DirectExecution = false);

public static class AiActionCatalog
{
    public static IReadOnlyDictionary<string, AiActionDefinition> Definitions { get; } =
        new Dictionary<string, AiActionDefinition>(StringComparer.OrdinalIgnoreCase)
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
}
