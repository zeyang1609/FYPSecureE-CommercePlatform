using FYP.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class Notification
    {
        [Key]
        [MaxLength(50)]
        public string NotificationID { get; set; }

        [Required]
        [MaxLength(50)]
        public string UserID { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; }

        [Required]
        [MaxLength(255)]
        public string Content { get; set; }

        [ForeignKey("UserID")]
        public virtual User User { get; set; }
    }
}