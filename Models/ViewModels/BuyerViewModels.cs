using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FYP.Models.Entities;

namespace FYP.Models.ViewModels
{
    public class BuyerDashboardViewModel
    {
        public string BuyerID { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int TotalOrders { get; set; }
        public decimal TotalSpent { get; set; }
        public int ActiveOrdersCount { get; set; }
        public bool MfaEnabled { get; set; }
        public IEnumerable<Order> RecentOrders { get; set; } = new List<Order>();
    }

    public class BuyerProfileViewModel
    {
        public string UserID { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Enable Multi-Factor Authentication (MFA)")]
        public bool MfaEnabled { get; set; }

        [Display(Name = "Name")]
        public string? Name { get; set; }

        [Display(Name = "Phone Number")]
        [Phone]
        public string? PhoneNumber { get; set; }

        public string? Gender { get; set; }

        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public System.DateTime? DateOfBirth { get; set; }

        public string Role { get; set; } = "Buyer";
        public string DeviceHash { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "New Password (Optional)")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&^#_.:,+-])[a-zA-Z\d@$!%*?&^#_.:,+-]{8,}$", ErrorMessage = "Password must be at least 8 characters long, contain no spaces or emojis, and include at least 1 uppercase letter, 1 lowercase letter, 1 number, and 1 special character.")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new passwords do not match.")]
        public string? ConfirmPassword { get; set; }
    }

    public class ChangePasswordVerifyViewModel
    {
        [Required(ErrorMessage = "Please enter your current password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; } = string.Empty;
    }

    public class ChangePasswordOtpViewModel
    {
        [Required(ErrorMessage = "Please enter the 6-digit verification code.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 digits.")]
        [Display(Name = "6-Digit Verification Code")]
        public string OtpCode { get; set; } = string.Empty;
    }

    public class ChangePasswordNewViewModel
    {
        [Required(ErrorMessage = "New password is required.")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&^#_.:,+-])[a-zA-Z\d@$!%*?&^#_.:,+-]{8,}$", ErrorMessage = "Password must be at least 8 characters long, contain no spaces or emojis, and include at least 1 uppercase letter, 1 lowercase letter, 1 number, and 1 special character.")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
    public class BuyerInsightsViewModel
    {
        public decimal TotalSpent { get; set; }
        public int TotalOrdersCompleted { get; set; }
        public string FavoriteCategory { get; set; } = "None";
        public string CurrentTimeFilter { get; set; } = "All Time";
        public Dictionary<string, decimal> SpendingByCategory { get; set; } = new Dictionary<string, decimal>();
        public List<Product> RecommendedProducts { get; set; } = new List<Product>();
        public List<WishlistItemViewModel> WishlistItems { get; set; } = new List<WishlistItemViewModel>();
    }

    public class WishlistItemViewModel
    {
        public string ProductID { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ImageHash { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockLevel { get; set; }
        public bool IsLowStock { get; set; }
    }
}