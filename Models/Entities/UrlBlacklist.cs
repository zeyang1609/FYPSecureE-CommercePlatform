using System;
using System.ComponentModel.DataAnnotations;

namespace FYP.Models.Entities
{
    public class UrlBlacklist
    {
        [Key]
        [MaxLength(50)]
        public string BlacklistID { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Domain { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
