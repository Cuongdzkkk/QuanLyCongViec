using System.Collections.Generic;

namespace TaskManagement.Application.AI;

public enum AiCapabilityKind
{
    Read,
    Write,
    Analyze
}

public sealed record AiActionDefinition(
    string EntityType,
    bool RequiresConfirmation,
    string DisplayName,
    AiCapabilityKind CapabilityKind);

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
            ["summarize_dashboard"] = new("Summary", false, "Tóm tắt dashboard", AiCapabilityKind.Analyze),
            ["summarize_project"] = new("Summary", false, "Tóm tắt dự án", AiCapabilityKind.Analyze),
            ["list_work_items"] = new("WorkTaskList", false, "Liệt kê công việc", AiCapabilityKind.Read),
            ["list_cycles"] = new("SprintList", false, "Liệt kê sprint", AiCapabilityKind.Read),
            ["list_modules"] = new("ModuleList", false, "Liệt kê module", AiCapabilityKind.Read),
            ["list_pages"] = new("PageList", false, "Liệt kê trang", AiCapabilityKind.Read),
            ["list_views"] = new("ProjectViewList", false, "Liệt kê view", AiCapabilityKind.Read),
            ["list_intakes"] = new("IntakeList", false, "Liệt kê yêu cầu tiếp nhận", AiCapabilityKind.Read),
            ["list_overdue_tasks"] = new("WorkTaskList", false, "Liệt kê công việc quá hạn", AiCapabilityKind.Read),
            ["get_workload"] = new("Workload", false, "Phân tích khối lượng công việc", AiCapabilityKind.Analyze),
            ["explain_report"] = new("ReportExplanation", false, "Giải thích báo cáo", AiCapabilityKind.Analyze),
            ["summarize_page"] = new("PageSummary", false, "Tóm tắt trang", AiCapabilityKind.Analyze),
            ["summarize_intakes"] = new("IntakeSummary", false, "Tóm tắt yêu cầu tiếp nhận", AiCapabilityKind.Analyze),
            ["suggest_view_filter"] = new("ViewFilterSuggestion", false, "Gợi ý bộ lọc view", AiCapabilityKind.Analyze)
        };
}
