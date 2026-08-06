using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class CartItem
    {
        [Key]
        [MaxLength(50)]
        public string CartItemID { get; set; } = Guid.NewGuid().ToString("N").ToUpper();

        [Required]
        [MaxLength(50)]
        public string CartID { get; set; }

        [Required]
        [MaxLength(50)]
        public string ProductID { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        public bool IsSelected { get; set; } = true;

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("CartID")]
        public virtual Cart Cart { get; set; }

        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; }
    }
}
