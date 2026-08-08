using System.ComponentModel.DataAnnotations;

namespace FYP.Models.ViewModels
{
    public class SellerProfileViewModel
    {
        public string UserID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Name")]
        public string? Name { get; set; }

        [Display(Name = "Phone Number")]
        [Phone]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Store Name")]
        [Required(ErrorMessage = "Store Name is required.")]
        public string StoreName { get; set; } = string.Empty;

        [Display(Name = "SSM Registration Number")]
        public string? SSMNumber { get; set; }

        [Display(Name = "Enable Multi-Factor Authentication (MFA)")]
        public bool MfaEnabled { get; set; }

        public string Role { get; set; } = "Seller";
    }
}
