using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class User
    {
        [Key]
        [MaxLength(50)]
        public string UserID { get; set; }

        [Required]
        [MaxLength(20)]
        public string Role { get; set; } // Buyer, Seller, Admin

        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } // Argon2id hash

        [Required]
        public bool MFA_Enabled { get; set; } = false;

        [MaxLength(255)]
        public string DeviceHash { get; set; } // For Isolation Forest anomaly detection

        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [MaxLength(10)]
        public string? Gender { get; set; }

        public DateTime? DateOfBirth { get; set; }

        [MaxLength(255)]
        public string? PaymentGatewayCustomerId { get; set; }

        [MaxLength(100)]
        public string? StoreName { get; set; }

        [MaxLength(50)]
        public string? SSMNumber { get; set; }

        // Navigation Properties
        public virtual ICollection<Order> Orders { get; set; }
        public virtual ICollection<Product> Products { get; set; }
        public virtual ICollection<AuditLog> AuditLogs { get; set; }
        public virtual ICollection<Address> Addresses { get; set; }
        public string? TotpSecret { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual Cart Cart { get; set; }
        public bool IsDisabled { get; set; } = false;
        public virtual ICollection<UserDevice> UserDevices { get; set; }

        // Privacy Controls
        public bool IsProfilePublic { get; set; } = false;
        public bool AllowPersonalizedAds { get; set; } = true;
        public bool ShareDataWithThirdParties { get; set; } = false;
    }

}