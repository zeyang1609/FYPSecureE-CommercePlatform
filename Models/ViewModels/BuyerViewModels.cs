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

        public string Role { get; set; } = "Buyer";
        public string DeviceHash { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "New Password (Optional)")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new passwords do not match.")]
        public string? ConfirmPassword { get; set; }
    }
}