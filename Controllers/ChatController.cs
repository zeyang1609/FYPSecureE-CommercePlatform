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

                if (attachment.ContentType.StartsWith("image/"))
                {
                    using var memoryStream = new MemoryStream();
                    await attachment.CopyToAsync(memoryStream);
                    var imageBytes = memoryStream.ToArray();
                    
                    // Synchronous AI Image Scan for NSFW/Forgery
                    var scanResult = await _aiClient.ScanImageForForgeryAsync(imageBytes);
                    if (scanResult.IsForgeryDetected)
                    {
                        return Json(new
                        {
                            success = false,
                            isBlocked = true,
                            message = $"Security Warning: Image blocked. {scanResult.ForgeryReason}"
                        });
                    }

                    await System.IO.File.WriteAllBytesAsync(filePath, imageBytes);
                    attachmentType = "image";
                }
                else
                {
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await attachment.CopyToAsync(fileStream);
                    }
                    attachmentType = "video";
                }

                attachmentUrl = "/uploads/chat/" + uniqueFileName;
            }

            // 2. Save the message immediately (no AI delay)
            string chatId = "CHT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

            // Phishing URL Detection
            bool isPhishingUrl = System.Text.RegularExpressions.Regex.IsMatch(safePayload, @"(bit\.ly|tinyurl\.com|ngrok\.io|t\.co|wa\.me\/|t\.me\/)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var chatMessage = new ChatMessage
            {
                ChatID = chatId,
                SenderID = senderId,
                ReceiverID = receiverId,
                Payload = safePayload,
                NLP_Flag = isPhishingUrl, // Flag immediately if suspicious link detected
                IsRead = false,
                Timestamp = DateTime.UtcNow,
                AttachmentUrl = attachmentUrl,
                AttachmentType = attachmentType
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            // 3. Deliver instantly via SignalR
            await _hubContext.Clients.User(receiverId).SendAsync("ReceiveMessage", senderId, safePayload, chatMessage.Timestamp.ToLocalTime().ToString("HH:mm"), attachmentUrl, attachmentType, chatMessage.ChatID);
            await _hubContext.Clients.User(receiverId).SendAsync("UpdateUnreadBadge");

            // 4. Fire-and-forget background NLP scan (no delay for the user)
            if (!string.IsNullOrWhiteSpace(safePayload))
            {
                var savedChatId = chatMessage.ChatID;
                var scopeFactory = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();
                var capturedSenderId = senderId;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        bool isMalicious = await _aiClient.ScanChatMessageAsync(safePayload);
                        if (isMalicious)
                        {
                            // Use a new DbContext scope since we're on a background thread
                            using var scope = scopeFactory.CreateScope();
                            var bgContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                            var msg = await bgContext.ChatMessages.FindAsync(savedChatId);
                            if (msg != null)
                            {
                                msg.NLP_Flag = true;
                                await bgContext.SaveChangesAsync();
                            }

                            // Notify sender that their message was flagged
                            await _hubContext.Clients.User(capturedSenderId).SendAsync("MessageFlagged", savedChatId,
                                "⚠️ Your message was flagged by our NLP security system for potential phishing content.");

                            // Notify receiver with a warning
                            await _hubContext.Clients.User(receiverId).SendAsync("MessageFlagged", savedChatId,
                                "⚠️ Warning: This message has been flagged for suspicious content.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Background NLP scan failed: {ex.Message}");
                    }
                });
            }

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

        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Json(0);

            var unreadCount = await _context.ChatMessages
                .CountAsync(m => m.ReceiverID == currentUserId && !m.IsRead);

            return Json(unreadCount);
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
                    Timestamp = g.First().Timestamp.ToLocalTime().ToString("MMM dd, HH:mm"),
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
                    chatId = m.ChatID,
                    isMine = m.SenderID == currentUserId, // Boolean flag so JS knows which color to use
                    payload = m.Payload,
                    timestamp = m.Timestamp.ToLocalTime().ToString("HH:mm"),
                    attachmentUrl = m.AttachmentUrl,
                    attachmentType = m.AttachmentType,
                    nlpFlag = m.NLP_Flag
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