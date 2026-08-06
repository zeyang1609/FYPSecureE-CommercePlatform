using FYP.Data;
using FYP.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace FYP.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;

        public ChatHub(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SendMessage(string receiverId, string payload)
        {
            // 1. Identify the sender securely
            string senderId = Context.UserIdentifier ?? "Unknown";

            // 2. Generate the custom string Key
            string chatId = "MSG-" + Guid.NewGuid().ToString("N").Substring(0, 15).ToUpper();

            // 3. Map to your exact entity
            var chatMsg = new ChatMessage
            {
                ChatID = chatId,
                SenderID = senderId,
                ReceiverID = receiverId,
                Payload = payload,
                NLP_Flag = false, // Set to false initially, let an AI background worker update it later!
                IsRead = false,
                Timestamp = DateTime.UtcNow
            };

            _context.ChatMessages.Add(chatMsg);
            await _context.SaveChangesAsync();

            // 4. Push in real-time to the receiver's screen
            await Clients.User(receiverId).SendAsync("ReceiveMessage", senderId, payload, chatMsg.Timestamp.ToString("HH:mm"));

            // 5. Update unread badge on receiver's UI
            await Clients.User(receiverId).SendAsync("UpdateUnreadBadge");
        }
    }
}