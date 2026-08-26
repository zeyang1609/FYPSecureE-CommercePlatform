using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class Product
    {
        [Key]
        [MaxLength(50)]
        public string ProductID { get; set; }

        [Required]
        [MaxLength(50)]
        public string SellerID { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        [Required]
        public int StockLevel { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        public decimal WeightKg { get; set; } = 1.00m;

        [MaxLength(128)]
        public string ImageHash { get; set; }

        public string Description { get; set; } = "No description available.";

        public int TotalSales { get; set; } = 0;

        [Column(TypeName = "decimal(3,2)")]
        public decimal AverageRating { get; set; } = 0.00m;

        public int ReviewCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("SellerID")]
        public virtual User Seller { get; set; }

        // Add these two properties inside your existing Product class:
        [Required]
        [MaxLength(50)]
        public string CategoryID { get; set; } = string.Empty;

        [ForeignKey("CategoryID")]
        public virtual Category Category { get; set; }
    }
}