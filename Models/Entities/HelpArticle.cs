using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class HelpArticle
    {
        [Key]
        public int HelpArticleID { get; set; }

        [Required]
        public int HelpCategoryID { get; set; }

        [Required]
        [MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public bool IsPopular { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation Property
        [ForeignKey("HelpCategoryID")]
        public virtual HelpCategory Category { get; set; }
    }
}
