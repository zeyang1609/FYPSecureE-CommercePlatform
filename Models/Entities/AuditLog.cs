using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class AuditLog
    {
        [Key]
        [MaxLength(50)]
        public string LogID { get; set; }

        [Required]
        [MaxLength(50)]
        public string UserID { get; set; }

        [Required]
        [MaxLength(255)]
        public string Action { get; set; }

        [Required]
        [MaxLength(45)]
        public string IP_Address { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        [ForeignKey("UserID")]
        public virtual User User { get; set; }
    }
}