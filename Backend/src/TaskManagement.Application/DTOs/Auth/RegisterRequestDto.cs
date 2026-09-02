using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Application.DTOs.Auth
{
    public class RegisterRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 2)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 100 ký tự.")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,}$", 
            ErrorMessage = "Mật khẩu phải có ít nhất 1 chữ hoa, 1 chữ số và 1 ký tự đặc biệt.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Token xác thực email là bắt buộc.")]
        [StringLength(43, MinimumLength = 43, ErrorMessage = "Token xác thực email không hợp lệ.")]
        [RegularExpression("^[A-Za-z0-9_-]{43}$", ErrorMessage = "Token xác thực email không hợp lệ.")]
        public string OtpToken { get; set; } = string.Empty;
    }
}
