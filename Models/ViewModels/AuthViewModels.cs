using System.ComponentModel.DataAnnotations;

namespace FYP.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        [RegularExpression(@"^[a-zA-Z\s.,'-]+$", ErrorMessage = "Name cannot contain emojis, numbers, or special characters.")]
        [Display(Name = "Full Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&^#_.:,+-])[a-zA-Z\d@$!%*?&^#_.:,+-]{8,}$", ErrorMessage = "Password must be at least 8 characters long, contain no spaces or emojis, and include at least 1 uppercase, 1 lowercase, 1 number, and 1 special character.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [RegularExpression(@"^(011-\d{4}\s\d{4}|01[02-9]-\d{3}\s\d{4})$", ErrorMessage = "Format should be 01X-XXX XXXX (10 digits) or 011-XXXX XXXX (11 digits).")]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [Required]
        public string Role { get; set; } = "Buyer";

        [Display(Name = "Store Name")]
        public string? StoreName { get; set; }

        [Display(Name = "SSM Registration Number")]
        public string? SSMNumber { get; set; }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }

        public string? Role { get; set; }
    }

    public class VerifyOtpViewModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the 6-digit verification code.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 digits.")]
        [Display(Name = "6-Digit Verification Code")]
        public string OtpCode { get; set; } = string.Empty;
    }

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordOtpViewModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the 6-digit verification code.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 digits.")]
        [Display(Name = "6-Digit Verification Code")]
        public string OtpCode { get; set; } = string.Empty;
    }

    public class SetNewPasswordViewModel
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "New password is required.")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&^#_.:,+-])[a-zA-Z\d@$!%*?&^#_.:,+-]{8,}$", ErrorMessage = "Password must be at least 8 characters long, contain no spaces or emojis, and include at least 1 uppercase, 1 lowercase, 1 number, and 1 special character.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class PendingDeviceApproval
    {
        public string UserID { get; set; }
        public string DeviceHash { get; set; }
        public string IPAddress { get; set; }
        public string UserAgent { get; set; }
        public bool IsApproved { get; set; } = false;
    }
}