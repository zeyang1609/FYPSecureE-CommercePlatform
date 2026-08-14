using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class Wishlist
    {
        [Key]
        public int WishlistID { get; set; }

        [Required]
        [MaxLength(50)]
        public string BuyerID { get; set; }

        [Required]
        [MaxLength(50)]
        public string ProductID { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("BuyerID")]
        public virtual User Buyer { get; set; }

        [ForeignKey("ProductID")]
        public virtual Product Product { get; set; }
    }
}
