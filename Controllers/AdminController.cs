using FYP.Data;
using FYP.Models.Entities;
using FYP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

using Microsoft.AspNetCore.SignalR;
using FYP.Hubs;
using Stripe;
using Microsoft.Extensions.Configuration;

namespace FYP.Controllers
{
    [Authorize(Roles = "Admin")] 
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<OrderHub> _orderHubContext;
        private readonly IConfiguration _configuration;
        private readonly IPaymentEncryptionService _paymentEncryptionService;

        public AdminController(ApplicationDbContext context, IHubContext<OrderHub> orderHubContext, IConfiguration configuration, IPaymentEncryptionService paymentEncryptionService)
        {
            _context = context;
            _orderHubContext = orderHubContext;
            _configuration = configuration;
            _paymentEncryptionService = paymentEncryptionService;
        }

        // GET: /Admin/Login
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            // 1. Secret Key Protection (Security by Obscurity replaced by Config)
            string secretKey = Request.Query["key"];
            string expectedKey = _configuration["BuiltInAdmin:AdminSecretKey"];

            if (string.IsNullOrEmpty(secretKey) || secretKey != expectedKey)
            {
                // Return a fake 404 Not Found to hide the existence of the login page from scanners
                return NotFound();
            }

            // 2. Render the dedicated AdminOS Login View
            return View(new FYP.Models.ViewModels.LoginViewModel());
        }
        // 1. MAIN DASHBOARD: Fraud Alerts & Audit Logs
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var alerts = await _context.FraudAlerts
                .Include(f => f.Order)
                .OrderByDescending(f => f.AlertID)
                .Take(50)
                .ToListAsync();

            var logs = await _context.AuditLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(50)
                .ToListAsync();

            ViewBag.TotalAlerts = alerts.Count;
            ViewBag.HighRiskCount = alerts.Count(a => a.RiskScore > 0.85m);
            ViewBag.TotalAuditLogs = await _context.AuditLogs.CountAsync();
            ViewBag.ActiveDisputes = await _context.Refunds.CountAsync(r => r.Status == "Dispute" || r.Status == "DISPUTED");
            ViewBag.AuditLogs = logs;

            var adminId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var securityAlerts = await _context.Notifications
                .Where(n => n.UserID == adminId && n.Type == "Security Alert")
                .OrderByDescending(n => n.NotificationID)
                .Take(20)
                .ToListAsync();
            ViewBag.SecurityAlerts = securityAlerts;

            return View(alerts);
        }

        // 2. EXPLAINABLE AI (XAI): Deep dive into SHAP tensors
        [HttpGet]
        public async Task<IActionResult> XaiDetails(string alertId)
        {
            var alert = await _context.FraudAlerts
                .Include(f => f.Order)
                .FirstOrDefaultAsync(a => a.AlertID == alertId);

            if (alert == null) return NotFound();

            return View(alert);
        }

        // 3. USER MANAGEMENT: Monitor accounts and MFA status
        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            ViewBag.TotalUsers = users.Count;
            ViewBag.MfaEnabledCount = users.Count(u => u.MFA_Enabled);

            return View(users);
        }

        // 4. CATEGORY MANAGEMENT: Storefront taxonomy
        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(categories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(string name, string description, IFormFile iconFile)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Category name cannot be empty.";
                return RedirectToAction(nameof(Categories));
            }

            string iconSvg = "<svg width=\"32\" height=\"32\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><rect x=\"3\" y=\"3\" width=\"18\" height=\"18\" rx=\"2\" ry=\"2\"></rect><line x1=\"3\" y1=\"9\" x2=\"21\" y2=\"9\"></line><line x1=\"9\" y1=\"21\" x2=\"9\" y2=\"9\"></line></svg>";

            if (iconFile != null && iconFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "categories");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + iconFile.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await iconFile.CopyToAsync(fileStream);
                }
                
                iconSvg = "/images/categories/" + uniqueFileName;
            }

            string categoryId = "CAT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
            var category = new Category
            {
                CategoryID = categoryId,
                Name = name,
                Description = description,
                IconSvg = iconSvg
            };

            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Action = $"Created new catalog category: {name}",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };

            _context.Categories.Add(category);
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Category '{name}' added successfully!";
            return RedirectToAction(nameof(Categories));
        }

        [HttpGet]
        public async Task<IActionResult> EditCategory(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(string id, string name, string description, Microsoft.AspNetCore.Http.IFormFile iconFile)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Category name cannot be empty.";
                return View(category);
            }

            category.Name = name;
            category.Description = description;

            if (iconFile != null && iconFile.Length > 0)
            {
                var uploadsFolder = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", "images", "categories");
                if (!System.IO.Directory.Exists(uploadsFolder))
                {
                    System.IO.Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + iconFile.FileName;
                var filePath = System.IO.Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                {
                    await iconFile.CopyToAsync(fileStream);
                }
                
                category.IconSvg = "/images/categories/" + uniqueFileName;
            }

            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Action = $"Edited catalog category: {name}",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };

            _context.Categories.Update(category);
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Category '{name}' updated successfully!";
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.CategoryID == id);
            
            if (category == null) return NotFound();

            // Create or get "Undefined" category
            var undefinedCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "Undefined");
            if (undefinedCategory == null)
            {
                undefinedCategory = new Category
                {
                    CategoryID = "CAT-UNDEFINED",
                    Name = "Undefined",
                    Description = "System category for products without an assigned category.",
                    IconSvg = "<svg width=\"32\" height=\"32\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><rect x=\"3\" y=\"3\" width=\"18\" height=\"18\" rx=\"2\" ry=\"2\"></rect><line x1=\"3\" y1=\"9\" x2=\"21\" y2=\"9\"></line><line x1=\"9\" y1=\"21\" x2=\"9\" y2=\"9\"></line></svg>"
                };
                _context.Categories.Add(undefinedCategory);
                await _context.SaveChangesAsync();
            }

            // Don't allow deleting the Undefined category itself
            if (category.CategoryID == undefinedCategory.CategoryID)
            {
                TempData["ErrorMessage"] = "The 'Undefined' category cannot be deleted.";
                return RedirectToAction(nameof(Categories));
            }

            if (category.Products.Any())
            {
                foreach(var product in category.Products)
                {
                    product.CategoryID = undefinedCategory.CategoryID;
                    _context.Products.Update(product);
                }
            }

            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Action = $"Deleted catalog category: {category.Name}. Reassigned {category.Products.Count} products.",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };

            _context.Categories.Remove(category);
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Category '{category.Name}' deleted. Products reassigned if any existed.";
            return RedirectToAction(nameof(Categories));
        }
        [HttpGet]
        public async Task<IActionResult> Disputes()
        {
            var disputedRefunds = await _context.Refunds
                .Include(r => r.Order)
                .Where(r => r.Status == "DISPUTED")
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            return View(disputedRefunds);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceRefund(string refundId, string adminNote)
        {
            var refund = await _context.Refunds
                .Include(r => r.Order)
                .ThenInclude(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(r => r.RefundID == refundId);
                
            if (refund != null && refund.Status == "DISPUTED")
            {
                var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderID == refund.OrderID);
                if (payment != null && !string.IsNullOrEmpty(payment.PaymentToken))
                {
                    var decryptedToken = _paymentEncryptionService.DecryptSafe(payment.PaymentToken);
                    if (decryptedToken.StartsWith("pi_"))
                    {
                    try
                    {
                        StripeConfiguration.ApiKey = _configuration["PaymentGateway:SecretKey"];
                        var options = new Stripe.RefundCreateOptions
                        {
                            PaymentIntent = decryptedToken,
                            Amount = Convert.ToInt64(Math.Round(refund.RefundAmount * 100m))
                        };
                        var requestOptions = new Stripe.RequestOptions
                        {
                            IdempotencyKey = $"admin_refund_{refund.RefundID}"
                        };

                        var refundService = new Stripe.RefundService();
                        var stripeRefund = await refundService.CreateAsync(options, requestOptions);

                        refund.StripeRefundId = stripeRefund.Id;
                        refund.RefundedAt = DateTime.UtcNow;
                    }
                    catch (StripeException ex)
                    {
                        TempData["ErrorMessage"] = $"Stripe Refund Failed: {ex.Message}";
                        return RedirectToAction(nameof(Disputes));
                    }
                    }
                }
                else
                {
                    refund.RefundedAt = DateTime.UtcNow;
                }

                refund.Status = "REFUND_COMPLETED";
                refund.AdminResolution = adminNote;
                
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == refund.OrderID);
                if (order != null) order.Status = "Refunded";

                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    Action = $"Arbitration: Forced refund for {refundId} (StripeRef: {refund.StripeRefundId ?? "Local"}). Notes: {adminNote}",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                
                if (refund.Order != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = refund.Order.BuyerID,
                        Type = "Refund Alert",
                        Content = $"Your dispute for Order {refund.OrderID} was resolved in your favor. Refund processed."
                    });

                    await _orderHubContext.Clients.Group(refund.Order.BuyerID).SendAsync("ReceiveReturnUpdate");
                    var sellerId = refund.Order.OrderItems.FirstOrDefault()?.Product?.SellerID;
                    if (sellerId != null) 
                    {
                        _context.Notifications.Add(new Notification
                        {
                            NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            UserID = sellerId,
                            Type = "Refund Alert",
                            Content = $"Admin has forced a refund for Order {refund.OrderID} in favor of the buyer."
                        });
                        await _orderHubContext.Clients.Group(sellerId).SendAsync("ReceiveReturnUpdate");
                    }
                    await _context.SaveChangesAsync();
                }
                
                TempData["SuccessMessage"] = "Dispute resolved in favor of Buyer (Refund Issued).";
            }
            return RedirectToAction(nameof(Disputes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectReturn(string refundId, string adminNote)
        {
            var refund = await _context.Refunds
                .Include(r => r.Order)
                .ThenInclude(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(r => r.RefundID == refundId);
                
            if (refund != null && refund.Status == "DISPUTED")
            {
                // Rejecting the return implies the order stays complete, refund is cancelled.
                refund.Status = "RETURN_REJECTED";
                refund.AdminResolution = adminNote;

                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    Action = $"Arbitration: Rejected return for {refundId}. Notes: {adminNote}",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                
                if (refund.Order != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = refund.Order.BuyerID,
                        Type = "Refund Alert",
                        Content = $"Your dispute for Order {refund.OrderID} was rejected."
                    });

                    await _orderHubContext.Clients.Group(refund.Order.BuyerID).SendAsync("ReceiveReturnUpdate");
                    var sellerId = refund.Order.OrderItems.FirstOrDefault()?.Product?.SellerID;
                    if (sellerId != null) 
                    {
                        _context.Notifications.Add(new Notification
                        {
                            NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            UserID = sellerId,
                            Type = "Refund Alert",
                            Content = $"Admin has rejected the buyer's dispute for Order {refund.OrderID}."
                        });
                        await _orderHubContext.Clients.Group(sellerId).SendAsync("ReceiveReturnUpdate");
                    }
                    await _context.SaveChangesAsync();
                }
                
                TempData["SuccessMessage"] = "Dispute resolved in favor of Seller (Return Rejected).";
            }
            return RedirectToAction(nameof(Disputes));
        }

        // Demo Helper: Simulates the courier returning the parcel to the seller
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SimulateReturnReceived(string refundId)
        {
            var refund = await _context.Refunds.FirstOrDefaultAsync(r => r.RefundID == refundId);
            if (refund != null && (refund.Status == "RETURN_APPROVED" || refund.Status == "RETURN_IN_TRANSIT"))
            {
                refund.Status = "RETURN_RECEIVED";
                await _context.SaveChangesAsync();
            }
            // Just redirect back to the page the request came from (Seller Refunds)
            return Redirect(Request.Headers["Referer"].ToString());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddUser(string email, string name, string role, string password)
        {
            var actingAdminEmail = User.FindFirstValue(ClaimTypes.Email);
            if (role == "Admin" && actingAdminEmail != "demo_admin@secureplatform.com")
            {
                TempData["ErrorMessage"] = "Access Denied: Only the Seed Admin can create new administrators.";
                return RedirectToAction(nameof(Users));
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&^#_.:,+-])[a-zA-Z\d@$!%*?&^#_.:,+-]{8,}$"))
            {
                TempData["ErrorMessage"] = "Password does not meet the strict security policy requirements.";
                return RedirectToAction(nameof(Users));
            }

            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                TempData["ErrorMessage"] = "Email is already registered.";
                return RedirectToAction(nameof(Users));
            }

            string hashedPassword = FYP.Security.Argon2idHasher.HashPassword(password);
            string userId = "USR-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

            var newUser = new User
            {
                UserID = userId,
                Name = name,
                Email = email,
                PasswordHash = hashedPassword,
                Role = role,
                MFA_Enabled = false,
                DeviceHash = "Pending"
            };

            _context.Users.Add(newUser);
            
            _context.AuditLogs.Add(new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Action = $"Created new user {email} with role {role}",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"User {email} successfully created.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            var actingAdminEmail = User.FindFirstValue(ClaimTypes.Email);
            var actingAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null) return NotFound();
            
            if (user.UserID == actingAdminId)
            {
                TempData["ErrorMessage"] = "You cannot disable your own account.";
                return RedirectToAction(nameof(Users));
            }

            if (user.Role == "Admin" && actingAdminEmail != "demo_admin@secureplatform.com")
            {
                TempData["ErrorMessage"] = "Access Denied: Only the Seed Admin can modify administrators.";
                return RedirectToAction(nameof(Users));
            }

            user.IsDisabled = !user.IsDisabled;
            
            _context.AuditLogs.Add(new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = actingAdminId,
                Action = $"{(user.IsDisabled ? "Disabled" : "Enabled")} account {user.Email}",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"User account {(user.IsDisabled ? "disabled" : "enabled")}.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var actingAdminEmail = User.FindFirstValue(ClaimTypes.Email);
            var actingAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
            if (user == null) return NotFound();
            
            if (user.UserID == actingAdminId)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Users));
            }

            if (user.Role == "Admin" && actingAdminEmail != "demo_admin@secureplatform.com")
            {
                TempData["ErrorMessage"] = "Access Denied: Only the Seed Admin can delete administrators.";
                return RedirectToAction(nameof(Users));
            }

            // Manually remove Restricted dependent entities to allow user deletion
            var chatMessages = await _context.ChatMessages
                .Where(cm => cm.SenderID == userId || cm.ReceiverID == userId)
                .ToListAsync();
            _context.ChatMessages.RemoveRange(chatMessages);

            var reviews = await _context.Reviews
                .Where(r => r.BuyerID == userId)
                .ToListAsync();
            _context.Reviews.RemoveRange(reviews);

            _context.Users.Remove(user);
            
            _context.AuditLogs.Add(new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = actingAdminId,
                Action = $"Deleted user account {user.Email}",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "User successfully deleted.";
            return RedirectToAction(nameof(Users));
        }
    

        [HttpPost]
        public async Task<IActionResult> SendBulkNotification(string targetAudience, string type, string content)
        {
            var usersQuery = _context.Users.AsQueryable();
            
            if (targetAudience == "Buyers") 
                usersQuery = usersQuery.Where(u => u.Role == "Buyer");
            else if (targetAudience == "Sellers") 
                usersQuery = usersQuery.Where(u => u.Role == "Seller");
            
            var users = await usersQuery.ToListAsync();
            var notifications = new System.Collections.Generic.List<Notification>();
            
            foreach (var user in users)
            {
                notifications.Add(new Notification
                {
                    NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = user.UserID,
                    Type = type,
                    Content = content
                });
            }

            _context.Notifications.AddRange(notifications);
            
            var adminId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            _context.AuditLogs.Add(new AuditLog {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = adminId ?? "SYS-ADMIN",
                Action = $"Admin sent bulk notification to {targetAudience}",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Bulk notification successfully sent to {users.Count} users.";
            return RedirectToAction("Dashboard");
        }

    
        // ==========================================
        // TRUSTED DEVICES MANAGEMENT
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> TrustedDevices()
        {
            var adminId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(adminId)) return RedirectToAction("Login", "Auth");

            var user = await _context.Users
                .Include(u => u.UserDevices)
                .FirstOrDefaultAsync(u => u.UserID == adminId);

            if (user == null) return RedirectToAction("Login", "Auth");

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDevice(int deviceId)
        {
            var adminId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(adminId)) return RedirectToAction("Login", "Auth");

            var device = await _context.UserDevices.FirstOrDefaultAsync(d => d.Id == deviceId && d.UserID == adminId);
            if (device != null)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == adminId);
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

        [HttpGet]
        public IActionResult Announcements()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAnnouncement(string message, string targetRoles)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                TempData["ErrorMessage"] = "Announcement message cannot be empty.";
                return RedirectToAction(nameof(Announcements));
            }

            var usersQuery = _context.Users.AsQueryable();
            
            if (targetRoles != "All")
            {
                usersQuery = usersQuery.Where(u => u.Role == targetRoles);
            }

            var userIds = await usersQuery.Select(u => u.UserID).ToListAsync();
            var notifications = new List<Notification>();

            foreach (var userId in userIds)
            {
                notifications.Add(new Notification
                {
                    NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = userId,
                    Type = "System Announcement",
                    Content = message
                });
            }

            if (notifications.Any())
            {
                _context.Notifications.AddRange(notifications);
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"Announcement sent to {notifications.Count} users successfully.";
            return RedirectToAction(nameof(Announcements));
        }
        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
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


        // GET: /Admin/ManageImageBlacklist
        [HttpGet]
        public async Task<IActionResult> ManageImageBlacklist()
        {
            var blacklist = await _context.BlacklistedImageHashes
                .Include(b => b.AddedByAdmin)
                .OrderByDescending(b => b.AddedAt)
                .ToListAsync();
            return View(blacklist);
        }

        // POST: /Admin/AddBlacklistHash
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBlacklistHash(string sha256Hash, string reason)
        {
            if (string.IsNullOrWhiteSpace(sha256Hash) || sha256Hash.Length != 64)
            {
                TempData["ErrorMessage"] = "Invalid SHA-256 hash length. Must be exactly 64 characters.";
                return RedirectToAction(nameof(ManageImageBlacklist));
            }

            bool exists = await _context.BlacklistedImageHashes.AnyAsync(b => b.SHA256Hash == sha256Hash);
            if (exists)
            {
                TempData["ErrorMessage"] = "This hash is already blacklisted.";
                return RedirectToAction(nameof(ManageImageBlacklist));
            }

            var adminId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            var newHash = new BlacklistedImageHash
            {
                SHA256Hash = sha256Hash.ToLowerInvariant(),
                Reason = reason,
                AddedByAdminID = adminId
            };

            _context.BlacklistedImageHashes.Add(newHash);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Hash successfully added to the global blacklist.";
            return RedirectToAction(nameof(ManageImageBlacklist));
        }

        // POST: /Admin/RemoveBlacklistHash
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveBlacklistHash(int id)
        {
            var hash = await _context.BlacklistedImageHashes.FindAsync(id);
            if (hash != null)
            {
                _context.BlacklistedImageHashes.Remove(hash);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Hash successfully removed from the blacklist.";
            }
            return RedirectToAction(nameof(ManageImageBlacklist));
        }
        // GET: /Admin/ManageIpFilters
        [HttpGet]
        public async Task<IActionResult> ManageIpFilters()
        {
            var filters = await _context.IpFilters
                .Include(f => f.AddedByAdmin)
                .OrderByDescending(f => f.AddedAt)
                .ToListAsync();

            return View(filters);
        }

        // POST: /Admin/AddIpFilter
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddIpFilter(string ipAddress, string filterAction, string reason)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || string.IsNullOrWhiteSpace(filterAction))
            {
                TempData["ErrorMessage"] = "IP Address and Action are required.";
                return RedirectToAction(nameof(ManageIpFilters));
            }

            var adminId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (adminId == null) return Unauthorized();

            // Check if IP already has a rule
            var existing = await _context.IpFilters.FirstOrDefaultAsync(f => f.IpAddress == ipAddress);
            if (existing != null)
            {
                TempData["ErrorMessage"] = "This IP address already has an active rule.";
                return RedirectToAction(nameof(ManageIpFilters));
            }

            var filter = new IpFilter
            {
                IpAddress = ipAddress.Trim(),
                FilterAction = filterAction.Trim(),
                Reason = reason,
                AddedByAdminID = adminId
            };

            _context.IpFilters.Add(filter);
            await _context.SaveChangesAsync();

            // Clear cache for IP Filtering Middleware
            var cache = HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache)) as Microsoft.Extensions.Caching.Memory.IMemoryCache;
            if (cache != null)
            {
                cache.Remove("IpFilters_Whitelist");
                cache.Remove("IpFilters_Blacklist");
            }

            TempData["SuccessMessage"] = $"IP {filterAction} rule successfully added.";
            return RedirectToAction(nameof(ManageIpFilters));
        }

        // POST: /Admin/RemoveIpFilter
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveIpFilter(int id)
        {
            var filter = await _context.IpFilters.FindAsync(id);
            if (filter != null)
            {
                _context.IpFilters.Remove(filter);
                await _context.SaveChangesAsync();

                // Clear cache for IP Filtering Middleware
                var cache = HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache)) as Microsoft.Extensions.Caching.Memory.IMemoryCache;
                if (cache != null)
                {
                    cache.Remove("IpFilters_Whitelist");
                    cache.Remove("IpFilters_Blacklist");
                }

                TempData["SuccessMessage"] = "IP filter rule successfully removed.";
            }
            return RedirectToAction(nameof(ManageIpFilters));
        }

        // GET: /Admin/AuditLogs
        [HttpGet]
        public async Task<IActionResult> AuditLogs(string sortOrder, DateTime? startDate, DateTime? endDate, string nameFilter, string actionFilter)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["DateSortParm"] = String.IsNullOrEmpty(sortOrder) ? "date_asc" : "";
            ViewData["UserSortParm"] = sortOrder == "User" ? "user_desc" : "User";
            
            ViewData["StartDateFilter"] = startDate?.ToString("yyyy-MM-dd");
            ViewData["EndDateFilter"] = endDate?.ToString("yyyy-MM-dd");
            ViewData["NameFilter"] = nameFilter;
            ViewData["ActionFilter"] = actionFilter;

            var logs = from l in _context.AuditLogs.Include(a => a.User)
                       select l;

            if (startDate.HasValue)
            {
                logs = logs.Where(s => s.Timestamp >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                var end = endDate.Value.AddDays(1); // Make it inclusive
                logs = logs.Where(s => s.Timestamp < end);
            }
            if (!String.IsNullOrEmpty(nameFilter))
            {
                logs = logs.Where(s => (s.User != null && s.User.Name.Contains(nameFilter)) || (s.User != null && s.User.Email.Contains(nameFilter)));
            }
            if (!String.IsNullOrEmpty(actionFilter))
            {
                logs = logs.Where(s => s.Action.Contains(actionFilter));
            }

            switch (sortOrder)
            {
                case "date_asc":
                    logs = logs.OrderBy(s => s.Timestamp);
                    break;
                case "User":
                    logs = logs.OrderBy(s => s.User.Name);
                    break;
                case "user_desc":
                    logs = logs.OrderByDescending(s => s.User.Name);
                    break;
                default:
                    logs = logs.OrderByDescending(s => s.Timestamp);
                    break;
            }

            return View(await logs.AsNoTracking().ToListAsync());
        }
    }
}
