using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class SavedBankCard
    {
        [Key]
        [MaxLength(50)]
        public string CardID { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(50)]
        public string UserID { get; set; }

        [Required]
        [MaxLength(255)]
        public string PaymentToken { get; set; } // The provider's payment method ID

        [MaxLength(255)]
        public string? Fingerprint { get; set; } // Unique card identifier provided by Stripe for duplicate detection

        [MaxLength(50)]
        public string? CardHolderName { get; set; }

        [MaxLength(20)]
        public string? Brand { get; set; } // Visa, Mastercard, etc.

        [MaxLength(4)]
        public string? Last4 { get; set; }

        public int ExpMonth { get; set; }
        public int ExpYear { get; set; }

        public bool IsDefault { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserID")]
        public virtual User? User { get; set; }
    }
}
