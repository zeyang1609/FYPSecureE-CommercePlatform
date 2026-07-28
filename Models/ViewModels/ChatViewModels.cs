using FYP.Models.Entities;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FYP.Models.ViewModels
{
    public class ChatRoomViewModel
    {
        public string CurrentUserID { get; set; } = string.Empty;
        public string TargetUserID { get; set; } = string.Empty;
        public string TargetUserName { get; set; } = string.Empty;
        public List<ChatMessage> ConversationHistory { get; set; } = new();

        [Required(ErrorMessage = "Cannot send an empty message.")]
        [StringLength(500, ErrorMessage = "Message cannot exceed 500 characters.")]
        public string NewMessagePayload { get; set; } = string.Empty;
    }
}