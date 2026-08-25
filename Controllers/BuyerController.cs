using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using FYP.Models.Entities;
using FYP.Models.ViewModels;
using FYP.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using FYP.Services;
using Microsoft.AspNetCore.SignalR;
using FYP.Hubs;

namespace FYP.Controllers
{
    [Authorize(Roles = "Buyer")]
    public class BuyerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IOtpService _otpService;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly IHubContext<OrderHub> _orderHubContext;
        private readonly PythonAiClient _aiClient;

        public BuyerController(ApplicationDbContext context, IOtpService otpService, Microsoft.Extensions.Configuration.IConfiguration configuration, IHubContext<OrderHub> orderHubContext, PythonAiClient aiClient)
        {
            _context = context;
            _otpService = otpService;
            _configuration = configuration;
            _orderHubContext = orderHubContext;
            _aiClient = aiClient;
        }

        // GET: /Buyer/Dashboard
        [HttpGet]
        public IActionResult Dashboard()
        {
            // Abandoned the old Buyer Dashboard. Redirecting to Profile as the main landing area.
            return RedirectToAction("Profile");
        }

        // GET: /Buyer/Orders
        [HttpGet]
        public async Task<IActionResult> Orders()
        {
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(buyerId)) return RedirectToAction("Login", "Auth");
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Seller)
                .Include(o => o.Payment)
                .Include(o => o.Reviews)
                .Include(o => o.Refunds)
                .Where(o => o.BuyerID == buyerId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            ViewBag.BuyerID = buyerId;

            bool changesMade = false;
            var now = DateTime.UtcNow;
            foreach (var order in orders)
            {
                if (order.Status == "Pending" || order.Status == "Pending Payment")
                {
                    if (order.CreatedAt.AddHours(5) <= now)
                    {
                        order.Status = "Cancelled";
                        if (!order.ServiceType.Contains("CancelledBySystem"))
                            order.ServiceType += "|CancelledBySystem";
                        changesMade = true;
                    }
                }
            }

            if (changesMade)
            {
                await _context.SaveChangesAsync();
            }

            var savedCards = await _context.SavedBankCards.Where(c => c.UserID == buyerId).ToListAsync();
            ViewBag.SavedCards = savedCards;

            return View(orders);
        }

        // GET: /Buyer/OrderDetails/ORD-1234567890AB
        [HttpGet]
        public async Task<IActionResult> OrderDetails(string orderId)
        {
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Seller)
                .Include(o => o.Payment)
                .Include(o => o.FraudAlert)
                .Include(o => o.Reviews)
                .Include(o => o.Refunds)
                .FirstOrDefaultAsync(o => o.OrderID == orderId && o.BuyerID == buyerId); 

            if (order == null)
            {
                return NotFound("Order not found or access denied.");
            }

            var delivery = await _context.Deliveries.FirstOrDefaultAsync(d => d.OrderID == orderId);

            ViewBag.BuyerID = order.BuyerID;
            ViewBag.Delivery = delivery;
            return View(order);
        }

        // POST: /Buyer/MarkOrderReceived
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkOrderReceived(string orderId)
        {
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId && o.BuyerID == buyerId);
            
            if (order != null)
            {
                order.Status = "Completed";
                order.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Order {orderId} has been marked as received!";
            }
            
            return RedirectToAction("Orders");
        }

        // POST: /Buyer/SubmitReview
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(
            string orderId, 
            string productId, 
            int rating, 
            string comment, 
            string quality, 
            string functionality, 
            string[] tags, 
            bool isAnonymous, 
            List<IFormFile> imageFiles, 
            IFormFile videoFile)
        {
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var order = await _context.Orders.Include(o => o.OrderItems).FirstOrDefaultAsync(o => o.OrderID == orderId && o.BuyerID == buyerId);
            
            if (order == null || order.Status != "Completed")
            {
                return BadRequest("Invalid order or order not completed.");
            }

            string mediaUrl = "";
            var urls = new List<string>();
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "reviews");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            if (imageFiles != null && imageFiles.Count > 0)
            {
                int maxImages = Math.Min(imageFiles.Count, 5); // Limit to 5 images
                for (int i = 0; i < maxImages; i++)
                {
                    var file = imageFiles[i];
                    if (!IsValidImage(file)) return BadRequest("Invalid image format. Only .jpg, .jpeg, and .png are allowed.");
                    if (file.Length > 0 && file.Length <= 10 * 1024 * 1024)
                    {
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }
                        urls.Add("/uploads/reviews/" + uniqueFileName);
                    }
                }
            }

            if (videoFile != null && videoFile.Length > 0)
            {
                if (!IsValidVideo(videoFile)) return BadRequest("Invalid video format. Only .mp4 is allowed.");
                if (videoFile.Length <= 100 * 1024 * 1024)
                {
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + videoFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await videoFile.CopyToAsync(fileStream);
                    }
                    urls.Add("/uploads/reviews/" + uniqueFileName);
                }
            }
            mediaUrl = string.Join(";", urls);

            var formattedComment = "";
            if (isAnonymous)
            {
                formattedComment += "[Anonymous Review]\n";
            }
            if (!string.IsNullOrEmpty(quality))
            {
                formattedComment += $"Quality: {quality}\n";
            }
            if (!string.IsNullOrEmpty(functionality))
            {
                formattedComment += $"Functionality: {functionality}\n";
            }
            if (tags != null && tags.Length > 0)
            {
                formattedComment += $"Tags: {string.Join(", ", tags)}\n";
            }
            if (!string.IsNullOrEmpty(comment))
            {
                formattedComment += $"\n{comment}";
            }

            var reviewId = "REV-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
            var review = new Review
            {
                ReviewID = reviewId,
                OrderID = orderId,
                ProductID = productId,
                BuyerID = buyerId,
                Rating = rating,
                Comment = formattedComment.Trim(),
                MediaUrl = mediaUrl,
                CreatedAt = DateTime.UtcNow
            };

            order.IsRated = true;
            _context.Reviews.Add(review);
            
            // Print server-side debug info to terminal
            Console.WriteLine($"[SubmitReview Debug] OrderId: {orderId}, ProductId: {productId}, Rating: {rating}");
            Console.WriteLine($"[SubmitReview Debug] Image null? {imageFiles == null}, Count: {imageFiles?.Count}. Video null? {videoFile == null}, Length: {videoFile?.Length} B.");
            Console.WriteLine($"[SubmitReview Debug] MediaUrl saved to DB: {mediaUrl}");

            // Log security audit trail
            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = buyerId,
                Action = $"Product rated for Order {orderId} (Rating: {rating}). Images: {imageFiles?.Count ?? 0}, Video: {(videoFile != null ? videoFile.FileName + " (" + videoFile.Length + "B)" : "None")}",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = "Review submitted successfully!";
            return RedirectToAction("Orders");
        }

        // POST: /Buyer/UpdateReview
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReview(string orderId, string comment, List<IFormFile> imageFiles, IFormFile videoFile)
        {
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var review = await _context.Reviews.FirstOrDefaultAsync(r => r.OrderID == orderId && r.BuyerID == buyerId);

            if (review == null)
            {
                return Json(new { success = false, message = "Review not found." });
            }

            if ((imageFiles != null && imageFiles.Count > 0) || (videoFile != null && videoFile.Length > 0))
            {
                var urls = new List<string>();
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "reviews");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                if (imageFiles != null && imageFiles.Count > 0)
                {
                    int maxImages = Math.Min(imageFiles.Count, 5);
                    for (int i = 0; i < maxImages; i++)
                    {
                        var file = imageFiles[i];
                        if (!IsValidImage(file)) return Json(new { success = false, message = "Invalid image format. Only .jpg, .jpeg, and .png are allowed." });
                        if (file.Length > 0 && file.Length <= 10 * 1024 * 1024)
                        {
                            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }
                            urls.Add("/uploads/reviews/" + uniqueFileName);
                        }
                    }
                }

                if (videoFile != null && videoFile.Length > 0 && videoFile.Length <= 100 * 1024 * 1024)
                {
                    if (!IsValidVideo(videoFile)) return Json(new { success = false, message = "Invalid video format. Only .mp4 is allowed." });
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + videoFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await videoFile.CopyToAsync(fileStream);
                    }
                    urls.Add("/uploads/reviews/" + uniqueFileName);
                }
                
                review.MediaUrl = string.Join(";", urls);
            }

            if (!string.IsNullOrWhiteSpace(comment))
            {
                review.Comment = comment.Trim();
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // GET: /Buyer/RequestRefundForm
        [HttpGet]
        public async Task<IActionResult> RequestRefundForm(string orderId, string issueType)
        {
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var order = await _context.Orders
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Product).ThenInclude(p => p.Seller)
                .FirstOrDefaultAsync(o => o.OrderID == orderId && o.BuyerID == buyerId);
            
            if (order == null) return NotFound("Order not found or access denied.");

            var delivery = await _context.Deliveries.FirstOrDefaultAsync(d => d.OrderID == orderId);
            ViewBag.Delivery = delivery;
            
            var buyer = await _context.Users.Include(b => b.Addresses).FirstOrDefaultAsync(b => b.UserID == buyerId);
            ViewBag.AvailableAddresses = buyer?.Addresses?.ToList() ?? new List<Address>();

            ViewBag.IssueType = issueType;
            return View(order);
        }

        // POST: /Buyer/SubmitRefundRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRefundRequest(string orderId, string issueType, string reason, string description, string refundEmail, List<IFormFile> imageFiles, IFormFile videoFile, string returnMethod, int? pickupAddressId)
        {
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderID == orderId && o.BuyerID == buyerId); 
            if (order == null) return NotFound("Order not found or access denied.");

            if (string.IsNullOrWhiteSpace(description))
            {
                TempData["ErrorMessage"] = "Description is required.";
                return RedirectToAction("RequestRefundForm", new { orderId, issueType });
            }

            if (string.IsNullOrWhiteSpace(refundEmail))
            {
                TempData["ErrorMessage"] = "Refund email is required.";
                return RedirectToAction("RequestRefundForm", new { orderId, issueType });
            }

            string mediaUrl = "";
            var urls = new List<string>();
            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "refunds");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            if (imageFiles != null && imageFiles.Count > 0)
            {
                int maxImages = Math.Min(imageFiles.Count, 5); // Limit to 5 images
                for (int i = 0; i < maxImages; i++)
                {
                    var file = imageFiles[i];
                    if (!IsValidImage(file))
                    {
                        TempData["ErrorMessage"] = "Invalid image format. Only .jpg, .jpeg, and .png are allowed.";
                        return RedirectToAction("RequestRefundForm", new { orderId, issueType });
                    }
                    if (file.Length > 10 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "Image size cannot exceed 10MB.";
                        return RedirectToAction("RequestRefundForm", new { orderId, issueType });
                    }
                    
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }
                    urls.Add("/uploads/refunds/" + uniqueFileName);
                }
            }

            if (videoFile != null && videoFile.Length > 0)
            {
                if (!IsValidVideo(videoFile))
                {
                    TempData["ErrorMessage"] = "Invalid video format. Only .mp4 is allowed.";
                    return RedirectToAction("RequestRefundForm", new { orderId, issueType });
                }
                if (videoFile.Length > 100 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "Video size cannot exceed 100MB.";
                    return RedirectToAction("RequestRefundForm", new { orderId, issueType });
                }
                
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + videoFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await videoFile.CopyToAsync(fileStream);
                }
                urls.Add("/uploads/refunds/" + uniqueFileName);
            }

            mediaUrl = string.Join(";", urls);

            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                TempData["ErrorMessage"] = "At least one image or video proof is required.";
                return RedirectToAction("RequestRefundForm", new { orderId, issueType });
            }

            if (returnMethod == "Pickup" && (!pickupAddressId.HasValue || pickupAddressId.Value <= 0))
            {
                TempData["ErrorMessage"] = "Please select a valid pickup address before submitting.";
                return RedirectToAction("RequestRefundForm", new { orderId, issueType });
            }

            string refundId = "RFD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
            var refund = new Refund
            {
                RefundID = refundId,
                OrderID = orderId,
                RefundAmount = order.TotalAmount,
                Status = "RETURN_REQUESTED",
                IssueType = issueType,
                Reason = reason,
                Description = description,
                RefundEmail = refundEmail,
                MediaUrl = mediaUrl,
                ReturnMethod = returnMethod,
                PickupAddressID = pickupAddressId,
                RequestedAt = DateTime.UtcNow,
                AdminResolution = "",
                SellerNotes = "",
                ReturnTrackingNumber = "",
                ReturnCourier = ""
            }; 

            // --- Refund Abuse Detection (Friendly Fraud) ---
            var totalOrders = await _context.Orders.CountAsync(o => o.BuyerID == buyerId);
            var totalRefunds = await _context.Refunds.CountAsync(r => r.Order.BuyerID == buyerId);
            
            // Rule: If >=3 refunds AND Refund Ratio > 30%
            if (totalOrders > 0 && totalRefunds >= 3 && ((double)totalRefunds / totalOrders) > 0.3)
            {
                refund.Status = "DISPUTED"; // Send directly to dispute instead of seller approval
                refund.SellerNotes = "Auto-flagged by System: Refund Abuse Detection (Friendly Fraud Threshold Exceeded)";
                
                var fraudAlert = new FraudAlert
                {
                    AlertID = "ALT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    OrderID = orderId,
                    RiskScore = 0.95m,
                    Reason = $"Refund Abuse Detected: {totalRefunds} refunds on {totalOrders} orders ({(double)totalRefunds/totalOrders:P1})",
                    SHAP_Data = "{}", // No SHAP data for rule-based flags
                    CreatedAt = DateTime.UtcNow
                };
                _context.FraudAlerts.Add(fraudAlert);

                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = buyerId,
                    Action = "SECURITY FLAG: Excessive refund request intercepted and disputed.",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                });
                
                var admins = _context.Users.Where(u => u.Role == "Admin").ToList();
                foreach (var admin in admins)
                {
                    _context.Notifications.Add(new Notification
                    {
                        NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = admin.UserID,
                        Type = "Security Alert",
                        Content = $"Refund Abuse Detected: {totalRefunds} refunds on {totalOrders} orders for User {buyerId}.",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }
            }
            // ----------------------------------------------

            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = order.BuyerID,
                Action = $"Refund requested for Order {orderId} (Amount: RM {order.TotalAmount:0.00})",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };

            var notification = new Notification
            {
                NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = order.BuyerID,
                Type = "Refund Request",
                Content = $"Refund requested for Order {orderId}. Reason: {reason}"
            }; 
            _context.Notifications.Add(notification);

            var sellerIds = order.OrderItems.Where(oi => oi.Product != null).Select(oi => oi.Product.SellerID).Distinct().ToList();
            foreach (var sellerId in sellerIds)
            {
                _context.Notifications.Add(new Notification
                {
                    NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = sellerId,
                    Type = "Refund Request",
                    Content = $"Buyer has requested a refund for Order {orderId}."
                });
            }

            _context.Refunds.Add(refund); 
            _context.AuditLogs.Add(auditLog); 
            await _context.SaveChangesAsync();

            var firstItem = order.OrderItems.FirstOrDefault();
            if (firstItem?.Product?.SellerID != null)
            {
                await _orderHubContext.Clients.Group(firstItem.Product.SellerID).SendAsync("ReceiveReturnUpdate");
            }

            TempData["SuccessMessage"] = "Refund request submitted successfully.";
            return RedirectToAction(nameof(RefundTracking), new { refundId = refundId });
        }


        // GET: /Buyer/Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            // Transfer email-change state to ViewBag (one-time read, won't persist across visits)
            var emailStep = TempData["EmailChangeStep"];
            var pendingEmail = TempData["PendingNewEmail"];
            
            if (emailStep != null)
            {
                ViewBag.EmailChangeStep = (int)emailStep;
                ViewBag.PendingNewEmail = pendingEmail?.ToString();
                // Re-store PendingNewEmail so VerifyEmailChange can read it on form submit
                TempData["PendingNewEmail"] = pendingEmail;
            }

            // Phone change state
            var phoneStep = TempData["PhoneChangeStep"];
            var phoneError = TempData["PhoneChangeError"];
            if (phoneStep != null)
            {
                ViewBag.PhoneChangeStep = (int)phoneStep;
                ViewBag.PhoneChangeError = phoneError?.ToString();
            }
            
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(buyerId)) return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == buyerId);
            if (user == null)
            {
                return NotFound("User profile not found.");
            }

            var viewModel = new BuyerProfileViewModel
            {
                UserID = user.UserID,
                Email = user.Email,
                Name = user.Name,
                PhoneNumber = user.PhoneNumber,
                Gender = user.Gender,
                DateOfBirth = user.DateOfBirth,
                Role = user.Role,
                MfaEnabled = user.MFA_Enabled,
                DeviceHash = user.DeviceHash ?? "No Device Hash Recorded",
                IsProfilePublic = user.IsProfilePublic,
                AllowPersonalizedAds = user.AllowPersonalizedAds,
                ShareDataWithThirdParties = user.ShareDataWithThirdParties
            };

            return View(viewModel);
        }

        // POST: /Buyer/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(BuyerProfileViewModel model)
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId) || userId != model.UserID)
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return View("Profile", model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null)
            {
                return NotFound("User profile not found.");
            }

            user.Email = model.Email;
            user.Name = model.Name;
            user.PhoneNumber = model.PhoneNumber;
            user.Gender = model.Gender;
            user.DateOfBirth = model.DateOfBirth;
            user.MFA_Enabled = model.MfaEnabled;
            
            // Privacy Controls
            user.IsProfilePublic = model.IsProfilePublic;
            user.AllowPersonalizedAds = model.AllowPersonalizedAds;
            user.ShareDataWithThirdParties = model.ShareDataWithThirdParties;

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                user.PasswordHash = Argon2idHasher.HashPassword(model.NewPassword);
            }

            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = user.UserID,
                Action = $"Updated security profile (MFA: {user.MFA_Enabled}, Password Changed: {!string.IsNullOrWhiteSpace(model.NewPassword)})",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your profile has been updated!";
            return RedirectToAction("Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _context.Users
                .Include(u => u.Addresses)
                .Include(u => u.UserDevices)
                .Include(u => u.Orders)
                .FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null) return NotFound();

            // Check if there are active, uncompleted orders
            var activeOrders = user.Orders.Any(o => o.Status != "Completed" && o.Status != "Cancelled" && o.Status != "Success" && o.Status != "Rejected");
            if (activeOrders)
            {
                TempData["PhoneChangeError"] = "Cannot delete account: You have active orders. Please wait until they are completed or cancelled.";
                return RedirectToAction("Profile");
            }

            // Anonymization Strategy (Right to be Forgotten)
            user.Name = "Anonymized User";
            user.Email = $"deleted_{Guid.NewGuid()}@secureplatform.com";
            user.PhoneNumber = null;
            user.Gender = null;
            user.DateOfBirth = null;
            user.IsDisabled = true;
            user.IsProfilePublic = false;
            user.AllowPersonalizedAds = false;
            user.ShareDataWithThirdParties = false;
            user.PasswordHash = "DELETED";

            // Hard delete related PII entities
            _context.Addresses.RemoveRange(user.Addresses);
            _context.UserDevices.RemoveRange(user.UserDevices);

            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = "SYSTEM",
                Action = $"User {userId} exercised Right to be Forgotten. Account anonymized and disabled.",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();

            // Sign out
            return RedirectToAction("Logout", "Auth");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitiateEmailChange(string newEmail)
        {
            if (string.IsNullOrWhiteSpace(newEmail) || !newEmail.Contains("@"))
            {
                TempData["ErrorMessage"] = "Please enter a valid email address.";
                return RedirectToAction("Profile");
            }

            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);

            if (user != null && user.Email.Equals(newEmail, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "The new email address cannot be the same as your current email address.";
                return RedirectToAction("Profile");
            }

            // Generate and send OTP to the new email
            await _otpService.GenerateAndSendOtpAsync(newEmail, "Email Update Verification");

            // Store pending email securely in session/tempdata
            TempData["PendingNewEmail"] = newEmail;
            TempData["EmailChangeStep"] = 2; // Trigger step 2 UI

            return RedirectToAction("Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePhoneNumber(string newPhoneNumber)
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null) return NotFound("User profile not found.");

            // Normalize: strip spaces, dashes, and leading +
            var cleaned = (newPhoneNumber ?? "").Replace(" ", "").Replace("-", "").Replace("+", "").Trim();

            // Malaysian phone number validation
            // Valid: 60 + 1x + 7-8 digits (total 11-12) or 01x + 7-8 digits (total 10-11)
            bool isValid = false;
            if (cleaned.StartsWith("60") && cleaned.Length >= 11 && cleaned.Length <= 12)
            {
                isValid = System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^601[0-9]\d{7,8}$");
            }
            else if (cleaned.StartsWith("01") && cleaned.Length >= 10 && cleaned.Length <= 11)
            {
                isValid = System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^01[0-9]\d{7,8}$");
            }

            if (!isValid)
            {
                TempData["PhoneChangeError"] = "Please enter a valid Malaysian phone number (e.g. 012-3456789 or +6012-3456789).";
                TempData["PhoneChangeStep"] = 1;
                return RedirectToAction("Profile");
            }

            // Check if same as current
            var currentCleaned = (user.PhoneNumber ?? "").Replace(" ", "").Replace("-", "").Replace("+", "").Trim();
            if (cleaned == currentCleaned)
            {
                TempData["PhoneChangeError"] = "The new phone number cannot be the same as your current phone number.";
                TempData["PhoneChangeStep"] = 1;
                return RedirectToAction("Profile");
            }

            // Format and save as +60xxxxxxxxx
            if (cleaned.StartsWith("01"))
            {
                cleaned = "60" + cleaned.Substring(1);
            }
            user.PhoneNumber = "+" + cleaned;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Phone number successfully updated.";
            return RedirectToAction("Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmailChange(string otpCode, string pendingEmail)
        {
            Console.WriteLine($"[VerifyEmailChange] pendingEmail='{pendingEmail}', otpCode='{otpCode}'");
            
            if (string.IsNullOrEmpty(pendingEmail) || string.IsNullOrEmpty(otpCode))
            {
                Console.WriteLine("[VerifyEmailChange] FAILED: pendingEmail or otpCode is null/empty");
                TempData["ErrorMessage"] = "Session expired or invalid request. Please try again.";
                return RedirectToAction("Profile");
            }

            var isValid = _otpService.ValidateOtp(pendingEmail, otpCode);
            Console.WriteLine($"[VerifyEmailChange] ValidateOtp result: {isValid}");
            
            if (!isValid)
            {
                TempData["ErrorMessage"] = "Invalid or expired verification code. Please try again.";
                TempData["PendingNewEmail"] = pendingEmail;
                TempData["EmailChangeStep"] = 2;
                return RedirectToAction("Profile");
            }

            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            
            if (user != null)
            {
                user.Email = pendingEmail;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Email address successfully updated.";
            }

            return RedirectToAction("Profile");
        }

        [HttpGet]
        public async Task<IActionResult> BanksAndCards()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Auth");

            var savedCards = await _context.SavedBankCards
                .Where(c => c.UserID == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var viewModel = new BanksAndCardsViewModel
            {
                SavedCards = savedCards,
                StripePublishableKey = _configuration["PaymentGateway:PublishableKey"] ?? ""
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSetupIntent()
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return Unauthorized(new { error = "Not authenticated" });

                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
                if (user == null) return NotFound(new { error = "User not found" });

                Stripe.StripeConfiguration.ApiKey = _configuration["PaymentGateway:SecretKey"];

                var customerService = new Stripe.CustomerService();
                Stripe.Customer customer;

                if (string.IsNullOrEmpty(user.PaymentGatewayCustomerId))
                {
                    // Create a new customer on the gateway
                    var customerOptions = new Stripe.CustomerCreateOptions
                    {
                        Email = user.Email,
                        Name = user.Name
                    };
                    customer = await customerService.CreateAsync(customerOptions);
                    
                    user.PaymentGatewayCustomerId = customer.Id;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    customer = await customerService.GetAsync(user.PaymentGatewayCustomerId);
                }

                var setupIntentService = new Stripe.SetupIntentService();
                var setupIntentOptions = new Stripe.SetupIntentCreateOptions
                {
                    Customer = customer.Id,
                    PaymentMethodTypes = new List<string> { "card" },
                };
                var setupIntent = await setupIntentService.CreateAsync(setupIntentOptions);

                return Json(new { clientSecret = setupIntent.ClientSecret });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SyncSavedCard([FromBody] SyncCardRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.PaymentMethodId)) return BadRequest(new { error = "Invalid payment method" });

                var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return Unauthorized(new { error = "Not authenticated" });

                Stripe.StripeConfiguration.ApiKey = _configuration["PaymentGateway:SecretKey"];
                var paymentMethodService = new Stripe.PaymentMethodService();
                var pm = await paymentMethodService.GetAsync(request.PaymentMethodId);

                if (pm == null || pm.Type != "card") return BadRequest(new { error = "Invalid card" });

                // Duplicate Check: PCI DSS compliant check using Stripe Fingerprint with fallback for test mode
                var fingerprint = pm.Card.Fingerprint;
                var isDuplicate = await _context.SavedBankCards.AnyAsync(c => 
                    c.UserID == userId && 
                    (
                        (!string.IsNullOrEmpty(fingerprint) && c.Fingerprint == fingerprint) || 
                        (c.Last4 == pm.Card.Last4 && c.Brand == pm.Card.Brand && c.ExpMonth == pm.Card.ExpMonth && c.ExpYear == pm.Card.ExpYear)
                    )
                );

                if (isDuplicate)
                {
                    // Optionally detach the duplicate PaymentMethod from Stripe to keep it clean
                    try { await paymentMethodService.DetachAsync(request.PaymentMethodId); } catch { }
                    return BadRequest(new { error = "This card has already been added to your account." });
                }

                var isFirstCard = !await _context.SavedBankCards.AnyAsync(c => c.UserID == userId);
                var setAsDefault = isFirstCard || request.IsDefault;

                if (setAsDefault)
                {
                    var existingDefault = await _context.SavedBankCards.Where(c => c.UserID == userId && c.IsDefault).ToListAsync();
                    foreach (var c in existingDefault)
                    {
                        c.IsDefault = false;
                    }
                }

                var savedCard = new SavedBankCard
                {
                    UserID = userId,
                    PaymentToken = pm.Id,
                    Fingerprint = fingerprint,
                    Brand = pm.Card.Brand,
                    Last4 = pm.Card.Last4,
                    ExpMonth = (int)pm.Card.ExpMonth,
                    ExpYear = (int)pm.Card.ExpYear,
                    CardHolderName = pm.BillingDetails?.Name,
                    IsDefault = setAsDefault
                };

                _context.SavedBankCards.Add(savedCard);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Card successfully added.";
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCard(string cardId)
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var card = await _context.SavedBankCards.FirstOrDefaultAsync(c => c.CardID == cardId && c.UserID == userId);

            if (card != null)
            {
                try
                {
                    Stripe.StripeConfiguration.ApiKey = _configuration["PaymentGateway:SecretKey"];
                    var paymentMethodService = new Stripe.PaymentMethodService();
                    await paymentMethodService.DetachAsync(card.PaymentToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DeleteCard] Failed to detach from Stripe: {ex.Message}");
                    // Proceed to delete from local DB anyway
                }

                _context.SavedBankCards.Remove(card);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Card successfully deleted.";
            }

            return RedirectToAction("BanksAndCards");
        }

        [HttpPost]
        public async Task<IActionResult> SetDefaultCard(string cardId)
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var newDefault = await _context.SavedBankCards.FirstOrDefaultAsync(c => c.CardID == cardId && c.UserID == userId);

            if (newDefault != null)
            {
                var existingDefaults = await _context.SavedBankCards.Where(c => c.UserID == userId && c.IsDefault).ToListAsync();
                foreach (var c in existingDefaults)
                {
                    c.IsDefault = false;
                }

                newDefault.IsDefault = true;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Default card updated successfully.";
            }

            return RedirectToAction("BanksAndCards");
        }
    [HttpGet]
    public async Task<IActionResult> Addresses()
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Auth");

        var addresses = await _context.Addresses
            .Where(a => a.UserID == userId)
            .OrderByDescending(a => a.IsDefault)
            .ToListAsync();

        return View(addresses);
    }

    [HttpPost]
    public async Task<IActionResult> SaveAddress(int? AddressID, string FullName, string PhoneNumber, string StateArea, string PostalCode, string UnitNumber, string HouseBuildingStreet, string Label, bool IsDefault, decimal? Latitude, decimal? Longitude, string returnUrl = null)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Auth");

        var isFirst = !await _context.Addresses.AnyAsync(a => a.UserID == userId);
        
        if (IsDefault || isFirst)
        {
            var existingDefaults = await _context.Addresses.Where(a => a.UserID == userId && a.IsDefault).ToListAsync();
            foreach (var a in existingDefaults) a.IsDefault = false;
            IsDefault = true;
        }

        if (AddressID.HasValue && AddressID.Value > 0)
        {
            // Edit Existing
            var addr = await _context.Addresses.FirstOrDefaultAsync(a => a.AddressID == AddressID.Value && a.UserID == userId);
            if (addr != null)
            {
                addr.FullName = FullName;
                addr.PhoneNumber = PhoneNumber;
                addr.StateArea = StateArea;
                addr.PostalCode = PostalCode;
                addr.UnitNumber = UnitNumber;
                addr.HouseBuildingStreet = HouseBuildingStreet;
                addr.Label = Label;
                addr.IsDefault = IsDefault;
                addr.Latitude = Latitude;
                addr.Longitude = Longitude;
                TempData["SuccessMessage"] = "Address updated successfully.";
            }
        }
        else
        {
            // Add New
            var addr = new Address
            {
                UserID = userId,
                FullName = FullName,
                PhoneNumber = PhoneNumber,
                StateArea = StateArea,
                PostalCode = PostalCode,
                UnitNumber = UnitNumber,
                HouseBuildingStreet = HouseBuildingStreet,
                Label = Label,
                IsDefault = IsDefault,
                Latitude = Latitude,
                Longitude = Longitude
            };
            _context.Addresses.Add(addr);
            TempData["SuccessMessage"] = "Address added successfully.";
        }

        await _context.SaveChangesAsync();
        
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            var savedAddr = AddressID.HasValue && AddressID.Value > 0 
                ? await _context.Addresses.FirstOrDefaultAsync(a => a.AddressID == AddressID.Value && a.UserID == userId)
                : await _context.Addresses.OrderByDescending(a => a.AddressID).FirstOrDefaultAsync(a => a.UserID == userId);
                
            return Json(new { 
                success = true, 
                address = savedAddr 
            });
        }

        if (!string.IsNullOrEmpty(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Addresses");
    }

    [HttpPost]
    public async Task<IActionResult> SetDefaultAddress(int id)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var addr = await _context.Addresses.FirstOrDefaultAsync(a => a.AddressID == id && a.UserID == userId);

        if (addr != null)
        {
            var existingDefaults = await _context.Addresses.Where(a => a.UserID == userId && a.IsDefault).ToListAsync();
            foreach (var a in existingDefaults) a.IsDefault = false;

            addr.IsDefault = true;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Default address updated successfully.";
        }

        return RedirectToAction("Addresses");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAddress(int id, string returnUrl = null)
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var addr = await _context.Addresses.FirstOrDefaultAsync(a => a.AddressID == id && a.UserID == userId);

        if (addr != null)
        {
            _context.Addresses.Remove(addr);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Address successfully deleted.";
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true });
        }

        if (!string.IsNullOrEmpty(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Addresses");
    }

        // ==========================================
        // TRUSTED DEVICES MANAGEMENT
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> TrustedDevices()
        {
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(buyerId)) return RedirectToAction("Login", "Auth");

            var user = await _context.Users
                .Include(u => u.UserDevices)
                .FirstOrDefaultAsync(u => u.UserID == buyerId);

            if (user == null) return RedirectToAction("Login", "Auth");



            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDevice(int deviceId)
        {
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(buyerId)) return RedirectToAction("Login", "Auth");

            var device = await _context.UserDevices.FirstOrDefaultAsync(d => d.Id == deviceId && d.UserID == buyerId);
            if (device != null)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == buyerId);
                if (user != null && user.DeviceHash == device.DeviceHash)
                {
                    user.DeviceHash = "";
                }

                _context.UserDevices.Remove(device);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Device successfully removed.";
            }
            else
            {
                TempData["ErrorMessage"] = "Device not found.";
            }

            return RedirectToAction("TrustedDevices");
        }

        // ==========================================
        // CHANGE PASSWORD FLOW
        // ==========================================

    [HttpGet]
    public IActionResult ChangePasswordVerify()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePasswordVerify(ChangePasswordVerifyViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);

            if (user != null)
            {
                if (Argon2idHasher.VerifyHash(model.CurrentPassword, user.PasswordHash))
                {
                    await _otpService.GenerateAndSendOtpAsync(user.Email, "Change Password");
                    return RedirectToAction("ChangePasswordOtp");
                }
                ModelState.AddModelError("CurrentPassword", "Incorrect current password.");
            }
        }
        return View(model);
    }

    [HttpGet]
    public IActionResult ChangePasswordOtp()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePasswordOtp(ChangePasswordOtpViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);

            if (user != null && _otpService.ValidateOtp(user.Email, model.OtpCode))
            {
                return RedirectToAction("ChangePasswordNew");
            }
            ModelState.AddModelError("OtpCode", "Invalid or expired reset code.");
        }
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendChangePasswordOtp()
    {
        var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);

        if (user != null)
        {
            await _otpService.GenerateAndSendOtpAsync(user.Email, "Change Password");
            return Json(new { success = true, message = "OTP sent successfully." });
        }
        return Json(new { success = false, message = "User not found." });
    }

    [HttpGet]
    public IActionResult ChangePasswordNew()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePasswordNew(ChangePasswordNewViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            
            if (user != null)
            {
                if (Argon2idHasher.VerifyHash(model.NewPassword, user.PasswordHash))
                {
                    ModelState.AddModelError("NewPassword", "New password cannot be the same as your current password.");
                    return View(model);
                }

                user.PasswordHash = Argon2idHasher.HashPassword(model.NewPassword);

                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = user.UserID,
                    Action = "User successfully changed their password via Profile settings.",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                });

                _context.Notifications.Add(new Notification
                {
                    NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = user.UserID,
                    Type = "Security Alert",
                    Content = "Security Alert: Your password has been successfully changed."
                });

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Your password has been changed successfully.";
                return RedirectToAction("Profile");
            }
        }
        return View(model);
    }

        public async Task<IActionResult> RefundTracking(string refundId)
        {
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var refund = await _context.Refunds
                .Include(r => r.Order)
                .ThenInclude(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Include(r => r.Order.Buyer)
                .ThenInclude(b => b.Addresses)
                .FirstOrDefaultAsync(r => r.RefundID == refundId && r.Order.BuyerID == buyerId);

            if (refund == null)
            {
                return NotFound("Refund request not found or access denied.");
            }

            return View(refund);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateReturnMethod(string refundId, string returnMethod, string? dropOffCourier, string? pickupDate, int? pickupAddressId)
        {
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var refund = await _context.Refunds.Include(r => r.Order).FirstOrDefaultAsync(r => r.RefundID == refundId && r.Order.BuyerID == buyerId);
            if (refund != null)
            {
                refund.ReturnMethod = returnMethod;
                
                if (returnMethod == "Drop-Off" && !string.IsNullOrEmpty(dropOffCourier))
                {
                    refund.ReturnCourier = dropOffCourier;
                    refund.PickupDate = null;
                    refund.PickupAddressID = null;
                }
                else if (returnMethod == "Pick-Up")
                {
                    refund.ReturnCourier = "J&T Express";
                    
                    if (!string.IsNullOrEmpty(pickupDate) && DateTime.TryParse(pickupDate, out DateTime pd))
                    {
                        refund.PickupDate = pd;
                    }
                    if (pickupAddressId.HasValue)
                    {
                        refund.PickupAddressID = pickupAddressId.Value;
                    }
                }
                
                await _context.SaveChangesAsync();
                await _orderHubContext.Clients.Group("Couriers").SendAsync("ReceiveReturnUpdate");
                return Ok();
            }
            return NotFound();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRefundRequest(string refundId)
        {
            var refund = await _context.Refunds.FirstOrDefaultAsync(r => r.RefundID == refundId);
            
            // Only allow cancellation if the request is still pending seller approval or pending pickup/drop-off
            if (refund != null && (refund.Status == "RETURN_REQUESTED" || refund.Status == "Requested" || refund.Status == "RETURN_APPROVED"))
            {
                _context.Refunds.Remove(refund);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Refund request cancelled successfully.";
                return RedirectToAction("Orders", new { buyerId = refund.Order?.BuyerID });
            }
            
            TempData["ErrorMessage"] = "Refund request cannot be cancelled at this stage.";
            return RedirectToAction(nameof(RefundTracking), new { refundId = refundId });
        }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder([FromBody] System.Text.Json.JsonElement requestData)
    {
        try
        {
            string orderId = requestData.GetProperty("orderId").GetString();
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderID == orderId && o.BuyerID == userId);
            
            if (order == null || (order.Status != "Pending" && order.Status != "Pending Payment"))
            {
                return BadRequest(new { success = false, message = "Order cannot be cancelled at this stage." });
            }
            
            order.Status = "Cancelled";
            if (!order.ServiceType.Contains("CancelledByUser"))
                order.ServiceType += "|CancelledByUser";
                
            var sellerIds = order.OrderItems.Select(oi => oi.Product.SellerID).Distinct().ToList();
            foreach (var sellerId in sellerIds)
            {
                _context.Notifications.Add(new Notification
                {
                    NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = sellerId,
                    Type = "Order Alert",
                    Content = $"Order {orderId} has been cancelled by the buyer."
                });
            }

            await _context.SaveChangesAsync();
            
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

        private bool IsValidImage(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension)) return false;

            try
            {
                using (var stream = file.OpenReadStream())
                {
                    byte[] header = new byte[8];
                    stream.ReadExactly(header, 0, 8);
                    string headerHex = BitConverter.ToString(header).Replace("-", string.Empty);
                    
                    if (extension == ".jpg" || extension == ".jpeg")
                    {
                        return headerHex.StartsWith("FFD8FF");
                    }
                    if (extension == ".png")
                    {
                        return headerHex.StartsWith("89504E470D0A1A0A");
                    }
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        private bool IsValidVideo(IFormFile file)
        {
            var allowedExtensions = new[] { ".mp4" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return allowedExtensions.Contains(extension);
        }

        [HttpPost]
        [Authorize(Roles = "Buyer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProfilePicture(IFormFile profilePicture, IFormFile? originalPicture = null)
        {
            if (profilePicture != null && profilePicture.Length > 0)
            {
                if (profilePicture.Length > 1024 * 1024 || (originalPicture != null && originalPicture.Length > 1024 * 1024 * 5)) // 1MB limit for cropped, 5MB for original
                {
                    TempData["ErrorMessage"] = "Image size limit exceeded.";
                    return RedirectToAction(nameof(Profile));
                }

                string userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
                string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                
                // Ensure directory exists
                if (!Directory.Exists(uploadPath))
                {
                    Directory.CreateDirectory(uploadPath);
                }

                // Force format to JPG and name it {UserId}.jpg
                string fileName = $"{userId}.jpg";
                string filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await profilePicture.CopyToAsync(stream);
                }

                // Save original if provided
                if (originalPicture != null && originalPicture.Length > 0)
                {
                    string originalFileName = $"{userId}_full.jpg";
                    string originalFilePath = Path.Combine(uploadPath, originalFileName);
                    using (var stream = new FileStream(originalFilePath, FileMode.Create))
                    {
                        await originalPicture.CopyToAsync(stream);
                    }
                }

                TempData["SuccessMessage"] = "Profile picture updated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Please select a valid image.";
            }

            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var notifications = await _context.Notifications
                .Where(n => n.UserID == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            var unreadNotifications = notifications.Where(n => !n.IsRead).ToList();
            if (unreadNotifications.Any())
            {
                foreach (var n in unreadNotifications) { n.IsRead = true; }
                await _context.SaveChangesAsync();
            }

            return View(notifications);
        }
        [HttpGet]
        public async Task<IActionResult> DownloadReceipt(string orderId, [FromServices] IPdfReceiptService pdfReceiptService)
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Seller)
                .Include(o => o.Buyer)
                    .ThenInclude(b => b.Addresses)
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null)
            {
                return NotFound("Order not found.");
            }

            if (order.BuyerID != userId)
            {
                // IDOR Prevention: Ensure the authenticated user owns this order
                return Forbid();
            }

            // Generate Unique Receipt Number
            var receiptNumber = $"REC-{order.CreatedAt:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 5).ToUpper()}";

            // Generate PDF
            var pdfBytes = await pdfReceiptService.GenerateReceiptAsync(order, receiptNumber);

            return File(pdfBytes, "application/pdf", $"{receiptNumber}.pdf");
        }
        [HttpPost]
        public async Task<IActionResult> ToggleWishlist(string productId)
        {
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(buyerId)) return Unauthorized();

            var existing = await _context.Wishlists.FirstOrDefaultAsync(w => w.BuyerID == buyerId && w.ProductID == productId);
            bool isWishlisted = false;
            
            if (existing != null)
            {
                _context.Wishlists.Remove(existing);
            }
            else
            {
                _context.Wishlists.Add(new Wishlist { BuyerID = buyerId, ProductID = productId });
                isWishlisted = true;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, isWishlisted });
        }

        [HttpGet]
        public async Task<IActionResult> Insights(string timeFilter = "All Time")
        {
            var buyerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(buyerId)) return RedirectToAction("Login", "Auth");

            // Apply time filter
            var query = _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .ThenInclude(p => p.Category)
                .Where(oi => oi.Order.BuyerID == buyerId && oi.Order.Status == "Completed");

            DateTime now = DateTime.UtcNow;
            if (timeFilter == "This Month")
            {
                var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                query = query.Where(oi => oi.Order.CreatedAt >= startOfMonth);
            }
            else if (timeFilter == "This Year")
            {
                var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                query = query.Where(oi => oi.Order.CreatedAt >= startOfYear);
            }

            var orderItems = await query.ToListAsync();

            decimal totalSpent = 0;
            var spendingByCategory = new Dictionary<string, decimal>();
            var countByCategory = new Dictionary<string, int>();
            var uniqueOrders = new HashSet<string>();

            foreach (var item in orderItems)
            {
                uniqueOrders.Add(item.OrderID);
                if (item.Product?.Category?.Name != null)
                {
                    decimal amount = item.UnitPrice * item.Quantity;
                    totalSpent += amount;
                    string catName = item.Product.Category.Name;

                    if (spendingByCategory.ContainsKey(catName))
                    {
                        spendingByCategory[catName] += amount;
                        countByCategory[catName] += item.Quantity;
                    }
                    else
                    {
                        spendingByCategory[catName] = amount;
                        countByCategory[catName] = item.Quantity;
                    }
                }
            }

            int totalOrdersCompleted = uniqueOrders.Count;
            string favoriteCategory = "None";
            if (countByCategory.Any())
            {
                favoriteCategory = countByCategory.OrderByDescending(c => c.Value).First().Key;
            }

            // Wishlist items
            var wishlists = await _context.Wishlists
                .Include(w => w.Product)
                .ThenInclude(p => p.Category)
                .Where(w => w.BuyerID == buyerId)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();

            var wishlistItems = wishlists.Where(w => w.Product != null).Select(w => new WishlistItemViewModel
            {
                ProductID = w.ProductID,
                Title = w.Product.Title,
                ImageHash = w.Product.ImageHash,
                Price = w.Product.Price,
                StockLevel = w.Product.StockLevel,
                IsLowStock = w.Product.StockLevel <= 5
            }).ToList();

            // Recommended Products (AI Powered)
            var recommendedProducts = new List<Product>();
            var buyerHistory = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .ThenInclude(p => p.Category)
                .Where(oi => oi.Order.BuyerID == buyerId)
                .Select(oi => $"{oi.Product.Category.Name} {oi.Product.Title} {oi.Product.Description}")
                .Distinct()
                .ToListAsync();

            var cartItems = await _context.CartItems
                .Include(ci => ci.Cart)
                .Include(ci => ci.Product)
                .ThenInclude(p => p.Category)
                .Where(ci => ci.Cart.UserID == buyerId)
                .Select(ci => $"{ci.Product.Category.Name} {ci.Product.Title} {ci.Product.Description}")
                .Distinct()
                .ToListAsync();

            var wishlistHistory = await _context.Wishlists
                .Include(w => w.Product)
                .ThenInclude(p => p.Category)
                .Where(w => w.BuyerID == buyerId)
                .Select(w => $"{w.Product.Category.Name} {w.Product.Title} {w.Product.Description}")
                .Distinct()
                .ToListAsync();

            buyerHistory.AddRange(cartItems);
            buyerHistory.AddRange(wishlistHistory);
            buyerHistory = buyerHistory.Distinct().ToList();

            var candidateProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.StockLevel > 0)
                .Select(p => new {
                    id = p.ProductID,
                    text = $"{p.Category.Name} {p.Title} {p.Description}"
                })
                .ToListAsync();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == buyerId);
            if (user != null && user.AllowPersonalizedAds && buyerHistory.Any() && candidateProducts.Any())
            {
                var aiPayload = new {
                    buyer_history = buyerHistory,
                    candidate_products = candidateProducts
                };

                var recommendedIds = await _aiClient.GetRecommendationsAsync(aiPayload);
                if (recommendedIds.Any())
                {
                    recommendedProducts = await _context.Products
                        .Include(p => p.Category)
                        .Where(p => recommendedIds.Contains(p.ProductID))
                        .ToListAsync();
                }
            }
            
            if (!recommendedProducts.Any())
            {
                // Fallback to top rated products
                recommendedProducts = await _context.Products
                    .Include(p => p.Category)
                    .Where(p => p.StockLevel > 0)
                    .OrderByDescending(p => p.AverageRating)
                    .Take(8)
                    .ToListAsync();
            }

            var viewModel = new BuyerInsightsViewModel
            {
                TotalSpent = totalSpent,
                TotalOrdersCompleted = totalOrdersCompleted,
                FavoriteCategory = favoriteCategory,
                CurrentTimeFilter = timeFilter,
                SpendingByCategory = spendingByCategory,
                WishlistItems = wishlistItems,
                RecommendedProducts = recommendedProducts
            };

            return View(viewModel);
        }
    }

    public class SyncCardRequest
    {
        public required string PaymentMethodId { get; set; }
        public bool IsDefault { get; set; }
    }
}