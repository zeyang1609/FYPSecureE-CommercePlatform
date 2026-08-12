using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace FYP.Models.Entities
{
    public class HelpCategory
    {
        [Key]
        public int HelpCategoryID { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string IconClass { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }

        // Navigation Property
        public virtual ICollection<HelpArticle> Articles { get; set; } = new List<HelpArticle>();
    }
}
