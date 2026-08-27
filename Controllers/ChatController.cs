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
        private readonly IPaymentEncryptionService _encryptionService;

        public ChatController(ApplicationDbContext context, PythonAiClient aiClient, IHubContext<ChatHub> hubContext, IWebHostEnvironment env, IPaymentEncryptionService encryptionService)
        {
            _context = context;
            _aiClient = aiClient;
            _hubContext = hubContext;
            _env = env;
            _encryptionService = encryptionService;
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

            // Decrypt payloads before displaying
            foreach (var msg in messages)
            {
                msg.Payload = _encryptionService.DecryptSafe(msg.Payload);
            }

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

            // Spam/Flood Prevention: Limit to 5 consecutive messages without a reply
            var lastMessages = await _context.ChatMessages
                .Where(m => (m.SenderID == senderId && m.ReceiverID == receiverId) ||
                            (m.SenderID == receiverId && m.ReceiverID == senderId))
                .OrderByDescending(m => m.Timestamp)
                .Take(5)
                .ToListAsync();

            if (lastMessages.Count == 5 && lastMessages.All(m => m.SenderID == senderId))
            {
                return Json(new { 
                    success = false, 
                    isBlocked = true, 
                    message = "🚨 Flood protection: You have sent 5 consecutive messages. Please wait for a reply before sending more." 
                });
            }

            // XSS Protection: HTML-encode the payload to prevent stored XSS attacks
            string safePayload = string.IsNullOrWhiteSpace(payload) ? "" : System.Net.WebUtility.HtmlEncode(payload);

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

            // FAST CHECK: Phishing URL Detection (instant — regex is negligible cost)
            bool isPhishingUrl = System.Text.RegularExpressions.Regex.IsMatch(payload ?? "", @"(bit\.ly|tinyurl\.com|ngrok\.io|t\.co|wa\.me\/|t\.me\/)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Block immediately if phishing URL detected (no delay — this is regex only)
            if (isPhishingUrl)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = senderId,
                    Action = $"Chat message blocked (URL Shortener regex matched).",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = false,
                    isBlocked = true,
                    message = "🚨 Message blocked: Suspicious phishing URL detected. Links to URL shorteners are not allowed for security reasons."
                });
            }

            // DB Check: Malicious Link Blacklist
            if (!string.IsNullOrWhiteSpace(payload))
            {
                var lowerPayload = payload.ToLowerInvariant();
                var blacklistedUrls = await _context.UrlBlacklists.Select(b => b.Domain).ToListAsync();
                var matchedDomain = blacklistedUrls.FirstOrDefault(d => lowerPayload.Contains(d));
                
                if (matchedDomain != null)
                {
                    _context.AuditLogs.Add(new AuditLog
                    {
                        LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = senderId,
                        Action = $"Chat message blocked (Matched DB Blacklist: {matchedDomain}).",
                        IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                        Timestamp = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();

                    return Json(new
                    {
                        success = false,
                        isBlocked = true,
                        message = $"🚨 Message blocked: The link '{matchedDomain}' is blacklisted by the admin for security reasons."
                    });
                }
            }

            // Save and deliver instantly — zero delay for the user
            var chatMessage = new ChatMessage
            {
                ChatID = chatId,
                SenderID = senderId,
                ReceiverID = receiverId,
                Payload = _encryptionService.Encrypt(safePayload),
                NLP_Flag = false,
                IsRead = false,
                Timestamp = DateTime.UtcNow,
                AttachmentUrl = attachmentUrl,
                AttachmentType = attachmentType
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            // 3. Deliver instantly via SignalR (payload is already HTML-encoded)
            await _hubContext.Clients.User(receiverId).SendAsync("ReceiveMessage", senderId, safePayload, chatMessage.Timestamp.ToLocalTime().ToString("HH:mm"), attachmentUrl, attachmentType, chatMessage.ChatID);
            await _hubContext.Clients.User(receiverId).SendAsync("UpdateUnreadBadge");

            // 4. Background AI NLP Scan — multilingual spam detection (no delay for the user)
            //    If flagged: warns both users via SignalR + marks message in DB
            if (!string.IsNullOrWhiteSpace(payload))
            {
                var savedChatId = chatMessage.ChatID;
                var scopeFactory = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();
                var capturedSenderId = senderId;
                var capturedReceiverId = receiverId;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var bgContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var bgAiClient = scope.ServiceProvider.GetRequiredService<PythonAiClient>();

                        var scanResult = await bgAiClient.ScanChatMessageAsync(payload);
                        if (scanResult.IsMalicious)
                        {
                            var msg = await bgContext.ChatMessages.FindAsync(savedChatId);
                            if (msg != null)
                            {
                                msg.NLP_Flag = true;
                            }

                            bgContext.AuditLogs.Add(new AuditLog
                            {
                                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                                UserID = capturedSenderId,
                                Action = $"Chat message flagged by AI: {scanResult.Reason}",
                                IP_Address = "BackgroundWorker",
                                Timestamp = DateTime.UtcNow
                            });

                            await bgContext.SaveChangesAsync();

                            // Push warning to sender
                            await _hubContext.Clients.User(capturedSenderId).SendAsync("MessageFlagged", savedChatId,
                                $"⚠️ Your message was flagged: {scanResult.Reason}");

                            // Push warning to receiver
                            await _hubContext.Clients.User(capturedReceiverId).SendAsync("MessageFlagged", savedChatId,
                                "⚠️ Warning: This message has been flagged as potential spam/phishing by our multilingual AI security system.");
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
                .Where(u => u.UserID != currentUserId && (u.Email.Contains(query) || (u.Name != null && u.Name.Contains(query))))
                .Select(u => new
                {
                    id = u.UserID,
                    email = u.Email,
                    name = string.IsNullOrEmpty(u.Name) ? u.Email : u.Name,
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

            // Decrypt messages
            foreach(var msg in messages)
            {
                msg.Payload = _encryptionService.DecryptSafe(msg.Payload);
            }

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

            // 1. Fetch chronological message history from DB
            var dbMessages = await _context.ChatMessages
                .Where(m => (m.SenderID == currentUserId && m.ReceiverID == contactId) ||
                            (m.SenderID == contactId && m.ReceiverID == currentUserId))
                .OrderBy(m => m.Timestamp) // Oldest at the top, newest at the bottom
                .ToListAsync();

            // Decrypt and project to View Model
            var messages = dbMessages
                .Select(m => new
                {
                    chatId = m.ChatID,
                    isMine = m.SenderID == currentUserId, // Boolean flag so JS knows which color to use
                    payload = _encryptionService.DecryptSafe(m.Payload),
                    timestamp = m.Timestamp.ToLocalTime().ToString("HH:mm"),
                    attachmentUrl = m.AttachmentUrl,
                    attachmentType = m.AttachmentType,
                    nlpFlag = m.NLP_Flag
                })
                .ToList();

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