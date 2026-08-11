using FYP.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class Refund
    {
        [Key]
        [MaxLength(50)]
        public string RefundID { get; set; }

        [Required]
        [MaxLength(50)]
        public string OrderID { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal RefundAmount { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; }

        [MaxLength(100)]
        public string IssueType { get; set; } // "Received item with issues?" or "Did not receive Some/All of items?"

        [MaxLength(100)]
        public string Reason { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        [MaxLength(500)]
        public string MediaUrl { get; set; }

        [MaxLength(255)]
        public string RefundEmail { get; set; }

        public DateTime? RequestedAt { get; set; }

        [MaxLength(100)]
        public string ReturnTrackingNumber { get; set; }

        [MaxLength(100)]
        public string ReturnCourier { get; set; }

        [MaxLength(50)]
        public string ReturnMethod { get; set; }
        
        public DateTime? PickupDate { get; set; }
        
        public int? PickupAddressID { get; set; }

        [MaxLength(1000)]
        public string SellerNotes { get; set; }

        [MaxLength(1000)]
        public string AdminResolution { get; set; }

        [MaxLength(100)]
        public string? StripeRefundId { get; set; }

        public DateTime? RefundedAt { get; set; }

        [ForeignKey("OrderID")]
        public virtual Order Order { get; set; }
    }
}