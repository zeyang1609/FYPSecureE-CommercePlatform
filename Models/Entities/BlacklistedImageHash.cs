using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class BlacklistedImageHash
    {
        [Key]
        public int HashID { get; set; }

        [Required]
        [StringLength(64, MinimumLength = 64)]
        public string SHA256Hash { get; set; }

        [MaxLength(255)]
        public string Reason { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(50)]
        public string AddedByAdminID { get; set; }

        [ForeignKey("AddedByAdminID")]
        public virtual User AddedByAdmin { get; set; }
    }
}
