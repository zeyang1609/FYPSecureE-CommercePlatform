using FYP.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FYP.Models.Entities
{
    public class ChatMessage
    {
        [Key]
        [MaxLength(50)]
        public string ChatID { get; set; }

        [Required]
        [MaxLength(50)]
        public string SenderID { get; set; }

        [Required]
        [MaxLength(50)]
        public string ReceiverID { get; set; }

        [Required]
        public string Payload { get; set; }

        [Required]
        public bool NLP_Flag { get; set; } = false;

        [ForeignKey("SenderID")]
        public virtual User Sender { get; set; }

        [ForeignKey("ReceiverID")]
        public virtual User Receiver { get; set; }
    }
}