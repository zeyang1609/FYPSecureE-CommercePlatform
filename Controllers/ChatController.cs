using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using FYP.Models.Entities;
using FYP.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using FYP.Hubs;

namespace FYP.Controllers
{
    [Authorize] // Lock down the entire controller
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PythonAiClient _aiClient;
        private readonly IHubContext<ChatHub> _hubContext; // Inject SignalR Hub
        private readonly IWebHostEnvironment _env;

        public ChatController(ApplicationDbContext context, PythonAiClient aiClient, IHubContext<ChatHub> hubContext, IWebHostEnvironment env)
        {
            _context = context;
            _aiClient = aiClient;
            _hubContext = hubContext;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Room(string receiverId)
        {
            // 1. Secure Identity Extraction (IDOR Protection)
            string senderId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var messages = await _context.ChatMessages
                .Where(m => (m.SenderID == senderId && m.ReceiverID == receiverId) ||
                            (m.SenderID == receiverId && m.ReceiverID == senderId))
                .OrderBy(m => m.Timestamp) // 2. Fixed Sorting: Chronological order
                .ToListAsync();

            ViewBag.SenderID = senderId;
            ViewBag.ReceiverID = receiverId;
            return View(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(string receiverId, string payload, IFormFile? attachment)
        {
            // 1. Secure Identity Extraction (Spoofing Protection)
            string senderId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(payload) && attachment == null)
            {
                return Json(new { success = false, message = "Message text or attachment cannot be empty." });
            }

            string safePayload = string.IsNullOrWhiteSpace(payload) ? "" : payload;

            // Handle file upload if present
            string? attachmentUrl = null;
            string? attachmentType = null;
            if (attachment != null && attachment.Length > 0)
            {
                string uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "chat");
                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                string uniqueFileName = Guid.NewGuid().ToString("N") + "_" + attachment.FileName;
                string filePath = Path.Combine(uploadFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await attachment.CopyToAsync(fileStream);
                }

                attachmentUrl = "/uploads/chat/" + uniqueFileName;
                attachmentType = attachment.ContentType.StartsWith("video/") ? "video" : "image";
            }

            // 2. Pass payload through Custom TF-IDF Random Forest NLP AI Engine
            bool isMalicious = false;
            if (!string.IsNullOrWhiteSpace(safePayload)) 
            {
                 isMalicious = await _aiClient.ScanChatMessageAsync(safePayload);
            }

            string chatId = "CHT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

            var chatMessage = new ChatMessage
            {
                ChatID = chatId,
                SenderID = senderId,
                ReceiverID = receiverId,
                Payload = safePayload,
                NLP_Flag = isMalicious,
                IsRead = false,
                Timestamp = DateTime.UtcNow,
                AttachmentUrl = attachmentUrl,
                AttachmentType = attachmentType
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

            // 3. Trigger Real-Time WebSocket Push via SignalR Context!
            await _hubContext.Clients.User(receiverId).SendAsync("ReceiveMessage", senderId, safePayload, chatMessage.Timestamp.ToString("HH:mm"), attachmentUrl, attachmentType);
            await _hubContext.Clients.User(receiverId).SendAsync("UpdateUnreadBadge");

            return Json(new { success = true, data = chatMessage });
        }

        // GET: /Chat/SearchUsers?query=justin
        [HttpGet]
        public async Task<IActionResult> SearchUsers(string query)
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Require at least 2 characters to prevent massive database queries
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Json(new List<object>());
            }

            var users = await _context.Users
                .Where(u => u.UserID != currentUserId && u.Email.Contains(query))
                .Select(u => new
                {
                    id = u.UserID,
                    email = u.Email,
                    role = u.Role
                })
                .Take(5) // Limit to top 5 results for UI performance
                .ToListAsync();

            return Json(users);
        }

        // GET: /Chat/GetRecentConversations
        [HttpGet]
        public async Task<IActionResult> GetRecentConversations()
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Fetch all messages involving the current user, including user details
            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => m.SenderID == currentUserId || m.ReceiverID == currentUserId)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            // 2. Group by the contact (the person who is NOT the current user)
            var recentConvos = messages
                .GroupBy(m => m.SenderID == currentUserId ? m.ReceiverID : m.SenderID)
                .Select(g => new
                {
                    ContactId = g.Key,
                    ContactEmail = g.First().SenderID == currentUserId ? g.First().Receiver.Email : g.First().Sender.Email,
                    LastMessage = g.First().Payload,
                    Timestamp = g.First().Timestamp.ToString("MMM dd, HH:mm"),
                    // Bonus: Count how many unread messages we have from this specific contact
                    UnreadCount = g.Count(m => m.ReceiverID == currentUserId && !m.IsRead)
                })
                .Take(15) // Show the 15 most recent chats
                .ToList();

            return Json(recentConvos);
        }
        // GET: /Chat/GetChatHistory?contactId=USER-ID
        [HttpGet]
        public async Task<IActionResult> GetChatHistory(string contactId)
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Fetch chronological message history
            var messages = await _context.ChatMessages
                .Where(m => (m.SenderID == currentUserId && m.ReceiverID == contactId) ||
                            (m.SenderID == contactId && m.ReceiverID == currentUserId))
                .OrderBy(m => m.Timestamp) // Oldest at the top, newest at the bottom
                .Select(m => new
                {
                    isMine = m.SenderID == currentUserId, // Boolean flag so JS knows which color to use
                    payload = m.Payload,
                    timestamp = m.Timestamp.ToString("HH:mm"),
                    attachmentUrl = m.AttachmentUrl,
                    attachmentType = m.AttachmentType
                })
                .ToListAsync();

            // 2. Clear unread notifications
            var unreadMessages = await _context.ChatMessages
                .Where(m => m.SenderID == contactId && m.ReceiverID == currentUserId && !m.IsRead)
                .ToListAsync();

            if (unreadMessages.Any())
            {
                foreach (var msg in unreadMessages)
                {
                    msg.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }

            return Json(messages);
        }
    }
}