using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class Cart
    {
        [Key]
        [MaxLength(50)]
        public string CartID { get; set; } = Guid.NewGuid().ToString("N").ToUpper();

        [Required]
        [MaxLength(50)]
        public string UserID { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("UserID")]
        public virtual User User { get; set; }
        
        public virtual ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    }
}
