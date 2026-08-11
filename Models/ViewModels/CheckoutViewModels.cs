using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FYP.Models.Entities;

namespace FYP.Models.ViewModels
{
    public class CheckoutViewModel
    {
        // --- 1. Core Order & Buyer Details ---
        [Required]
        public string BuyerID { get; set; } = "USR-BUYER-DEMO";

        public string? SecurityToken { get; set; }


        [Required(ErrorMessage = "Full shipping address is required.")]
        [Display(Name = "Shipping Address")]
        public string ShippingAddress { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Credit Card";

        [Display(Name = "Shipping Option")]
        public string? ServiceType { get; set; } = "Standard Delivery";

        [Range(0.01, 1000000.00)]
        public decimal TotalAmount { get; set; }

        public decimal OriginalShippingFee { get; set; }
        public decimal ShippingFee { get; set; }
        public string CourierName { get; set; } = string.Empty;
        public string CourierID { get; set; } = string.Empty;

        public List<CartItemViewModel> CartItems { get; set; } = new List<CartItemViewModel>();

        public string? MessageToSeller { get; set; }
        public int SelectedAddressID { get; set; }
        public List<Address> AvailableAddresses { get; set; } = new List<Address>();

        public string? SelectedSavedCardID { get; set; }
        public List<SavedBankCard> SavedCards { get; set; } = new List<SavedBankCard>();

        // --- 2. Transient Payment Credentials (PCI-DSS compliant - Never saved to DB) ---
        [Display(Name = "Card Number")]
        public string? RawCardNumber { get; set; } = string.Empty;

        // ALIAS PROPERTY: This bridges 'CardNumber' directly to 'RawCardNumber' to stop the compiler errors
        public string? CardNumber
        {
            get => RawCardNumber;
            set => RawCardNumber = value;
        }

        [RegularExpression(@"^(0[1-9]|1[0-2])\/?([0-9]{2})$", ErrorMessage = "Format must be MM/YY")]
        [Display(Name = "Expiry Date (MM/YY)")]
        public string? ExpiryDate { get; set; } = string.Empty;

        [StringLength(4, MinimumLength = 3, ErrorMessage = "CVV must be 3 or 4 digits.")]
        [Display(Name = "CVV")]
        public string? CVV { get; set; } = string.Empty;

        // --- 3. Telemetry Features for XGBoost AI Risk Scoring ---
        public int AccountAgeDays { get; set; }
        public int FailedLoginAttempts { get; set; }
        public int ShippingDistanceKm { get; set; }

        // --- 4. Network Security Shield ---
        [Required]
        public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N").ToUpper();

        // --- 5. Stripe Configuration ---
        public string StripePublishableKey { get; set; } = string.Empty;
    }

    public class ShippingOptionViewModel
    {
        public string CourierID { get; set; } = string.Empty;
        public string CourierName { get; set; } = string.Empty;
        public decimal OriginalFee { get; set; }
        public decimal FinalFee { get; set; }
    }
}