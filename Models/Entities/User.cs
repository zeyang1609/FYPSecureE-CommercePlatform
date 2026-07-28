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

        // Navigation Properties
        public virtual ICollection<Order> Orders { get; set; }
        public virtual ICollection<Product> Products { get; set; }
        public virtual ICollection<AuditLog> AuditLogs { get; set; }
    }

}