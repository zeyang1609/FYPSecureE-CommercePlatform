using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FYP.Models.Entities
{
    public class Category
    {
        [Key]
        [MaxLength(50)]
        public string CategoryID { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(255)]
        public string Description { get; set; } = string.Empty;

        // Stores raw SVG vector XML or an image path for the storefront icon
        [MaxLength(1000)]
        public string IconSvg { get; set; } = string.Empty;

        // Navigation Property: One Category has many Products
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}