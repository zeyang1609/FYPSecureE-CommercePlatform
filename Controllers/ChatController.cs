using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using FYP.Models.Entities;
using FYP.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PythonAiClient _aiClient;

        public ChatController(ApplicationDbContext context, PythonAiClient aiClient)
        {
            _context = context;
            _aiClient = aiClient;
        }

        [HttpGet]
        public async Task<IActionResult> Room(string senderId, string receiverId)
        {
            var messages = await _context.ChatMessages
                .Where(m => (m.SenderID == senderId && m.ReceiverID == receiverId) ||
                            (m.SenderID == receiverId && m.ReceiverID == senderId))
                .OrderBy(m => m.ChatID) // Ordered sequentially by string ID
                .ToListAsync();

            ViewBag.SenderID = senderId;
            ViewBag.ReceiverID = receiverId;
            return View(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(string senderId, string receiverId, string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return Json(new { success = false, message = "Message text cannot be empty." });
            }

            // 1. Pass payload through Custom TF-IDF Random Forest NLP AI Engine
            bool isMalicious = await _aiClient.ScanChatMessageAsync(payload);

            // 2. Generate explicit primary key
            string chatId = "CHT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

            var chatMessage = new ChatMessage
            {
                ChatID = chatId,
                SenderID = senderId,
                ReceiverID = receiverId,
                Payload = payload,
                NLP_Flag = isMalicious
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            if (isMalicious)
            {
                return Json(new
                {
                    success = false,
                    isBlocked = true,
                    message = "Security Warning: Message blocked by NLP AI. Phishing or off-platform payment steering detected."
                });
            }

            return Json(new { success = true, data = chatMessage });
        }
    }
}