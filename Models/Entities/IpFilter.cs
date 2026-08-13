using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class IpFilter
    {
        [Key]
        public int FilterID { get; set; }

        [Required]
        [StringLength(45)] // Enough for IPv6
        public string IpAddress { get; set; }

        [Required]
        [StringLength(10)] // "Allow" or "Block"
        public string FilterAction { get; set; }

        [StringLength(255)]
        public string Reason { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        // The Admin who added this filter
        public string AddedByAdminID { get; set; }

        [ForeignKey("AddedByAdminID")]
        public virtual User AddedByAdmin { get; set; }
    }
}
