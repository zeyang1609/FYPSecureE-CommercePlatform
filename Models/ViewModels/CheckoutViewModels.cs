using System.ComponentModel.DataAnnotations;

namespace FYP.Models.ViewModels
{
    public class CheckoutViewModel
    {
        [Required]
        public string BuyerID { get; set; } = string.Empty;

        [Required]
        [Range(0.01, 1000000.00)]
        public decimal Amount { get; set; }

        [Required]
        public string ServiceType { get; set; } = "Standard Delivery";

        // Transient Payment Fields (Never saved to DB)
        [Required(ErrorMessage = "Card number is required.")]
        [CreditCard(ErrorMessage = "Invalid credit card number.")]
        [Display(Name = "Card Number")]
        public string RawCardNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Expiration date required.")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/?([0-9]{2})$", ErrorMessage = "Format must be MM/YY")]
        [Display(Name = "Expiry Date (MM/YY)")]
        public string ExpiryDate { get; set; } = string.Empty;

        [Required(ErrorMessage = "CVV required.")]
        [StringLength(4, MinimumLength = 3, ErrorMessage = "CVV must be 3 or 4 digits.")]
        [Display(Name = "CVV")]
        public string CVV { get; set; } = string.Empty;

        // Telemetry Fields for XGBoost AI Risk Scoring
        public int AccountAgeDays { get; set; }
        public int FailedLoginAttempts { get; set; }
        public int ShippingDistanceKm { get; set; }

        // Security
        [Required]
        public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
    }
}