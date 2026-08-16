using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Application.DTOs.Project
{
    public class CreateProjectDto
    {
        [Required(ErrorMessage = "Tên dự án là bắt buộc.")]
        [StringLength(300, ErrorMessage = "Tên dự án không quá 300 ký tự.")]
        public string Name { get; set; } = string.Empty;

        public string? Key { get; set; }

        public string? Description { get; set; }

        [Required(ErrorMessage = "Ngày bắt đầu là bắt buộc.")]
        public DateTime StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public Guid? WorkspaceId { get; set; }

        public Guid? DepartmentId { get; set; }

        public Guid? ProjectTemplateId { get; set; }

        public string? NetworkType { get; set; }

        public string? Cover { get; set; }

        public string? Icon { get; set; }

        public Guid? LeadUserId { get; set; }
    }
}
