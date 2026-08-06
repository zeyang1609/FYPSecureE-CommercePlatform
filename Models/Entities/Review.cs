using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class Review
    {
        [Key]
        [MaxLength(50)]
        public string ReviewID { get; set; }

        [Required]
        [MaxLength(50)]
        public string OrderID { get; set; }

        [Required]
        [MaxLength(50)]
        public string ProductID { get; set; }

        [Required]
        [MaxLength(50)]
        public string BuyerID { get; set; }

        [Required]
        [Range(1, 5)]
        public int Rating { get; set; } // 1 to 5 stars

        [MaxLength(1000)]
        public string? Comment { get; set; }

        [MaxLength(255)]
        public string? MediaUrl { get; set; } // For future image/video support

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("OrderID")]
        public virtual Order Order { get; set; }

        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; }

        [ForeignKey("BuyerID")]
        public virtual User Buyer { get; set; }
    }
}
