using FYP.Models.Entities;
using System;
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

        [MaxLength(255)]
        public string? AttachmentUrl { get; set; }

        [MaxLength(20)]
        public string? AttachmentType { get; set; } // "image", "video", or null

        // --- REQUIRED FOR CHAT UI FUNCTIONALITY ---
        public bool IsRead { get; set; } = false;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [ForeignKey("SenderID")]
        public virtual User Sender { get; set; }

        [ForeignKey("ReceiverID")]
        public virtual User Receiver { get; set; }
    }
}