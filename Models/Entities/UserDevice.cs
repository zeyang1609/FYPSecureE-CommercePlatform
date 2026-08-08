using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class UserDevice
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string UserID { get; set; }

        [ForeignKey("UserID")]
        public virtual User User { get; set; }

        [Required]
        [MaxLength(255)]
        public string DeviceHash { get; set; }

        [MaxLength(100)]
        public string? OS { get; set; }

        [MaxLength(100)]
        public string? Browser { get; set; }

        [MaxLength(50)]
        public string? IPAddress { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
    }
}
