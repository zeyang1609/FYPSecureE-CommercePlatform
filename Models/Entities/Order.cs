using FYP.Models.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class Order
    {
        [Key]
        [MaxLength(50)]
        public string OrderID { get; set; }

        [Required]
        [MaxLength(50)]
        public string BuyerID { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        [MaxLength(50)]
        public string ServiceType { get; set; }

        [MaxLength(100)]
        public string? DeliveryID { get; set; }

        public DateTime? CompletedAt { get; set; }

        [Required]
        public bool IsRated { get; set; } = false;

        [ForeignKey("BuyerID")]
        public virtual User Buyer { get; set; }

        // Navigation Properties
        public virtual ICollection<OrderItem> OrderItems { get; set; }
        public virtual Payment Payment { get; set; }
        public virtual FraudAlert FraudAlert { get; set; }
        public virtual ICollection<Review> Reviews { get; set; }
        public virtual ICollection<Refund> Refunds { get; set; }
    }
}