using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class Payment
    {
        [Key]
        public string PaymentID { get; set; } = string.Empty;

        [Required]
        public string OrderID { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public string PaymentMethod { get; set; } = "Credit Card";

        public string PaymentToken { get; set; } = string.Empty;

        public string IdempotencyKey { get; set; } = string.Empty;

        public string Status { get; set; } = "Authorized";

        public string TransactionHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Property
        [ForeignKey("OrderID")]
        public virtual Order? Order { get; set; }
    }
}