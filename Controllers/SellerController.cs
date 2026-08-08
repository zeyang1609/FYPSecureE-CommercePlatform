using FYP.Data;
using FYP.Models.Entities;
using FYP.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using FYP.Services;
using FYP.Security;

namespace FYP.Controllers
{
    [Authorize(Roles = "Seller")]
    public class SellerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IOtpService _otpService;

        public SellerController(ApplicationDbContext context, IOtpService otpService)
        {
            _context = context;
            _otpService = otpService;
        }

        // GET: /Seller/Onboard
        [HttpGet]
        public IActionResult Onboard()
        {
            if (User.IsInRole("Seller"))
            {
                return RedirectToAction("Dashboard");
            }
            return View();
        }

        // ==========================================
        // SELLER PROFILE
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var sellerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(sellerId)) return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == sellerId);
            if (user == null) return RedirectToAction("Login", "Auth");

            var viewModel = new SellerProfileViewModel
            {
                UserID = user.UserID,
                Email = user.Email,
                Name = user.Name,
                PhoneNumber = user.PhoneNumber,
                StoreName = user.StoreName ?? "",
                SSMNumber = user.SSMNumber,
                MfaEnabled = user.MFA_Enabled,
                Role = user.Role
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(SellerProfileViewModel model)
        {
            var sellerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(sellerId) || sellerId != model.UserID) return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == sellerId);
            if (user == null) return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid)
            {
                return View("Profile", model);
            }

            user.Name = model.Name;
            user.PhoneNumber = model.PhoneNumber;
            user.StoreName = model.StoreName;
            user.SSMNumber = model.SSMNumber;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Profile updated successfully.";

            return RedirectToAction("Profile");
        }

        // ==========================================
        // TRUSTED DEVICES
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> TrustedDevices()
        {
            var sellerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(sellerId)) return RedirectToAction("Login", "Auth");

            var user = await _context.Users
                .Include(u => u.UserDevices)
                .FirstOrDefaultAsync(u => u.UserID == sellerId);

            if (user == null) return RedirectToAction("Login", "Auth");

            var currentDevice = HttpContext.Request.Headers["User-Agent"].ToString();

            if (!string.IsNullOrEmpty(currentDevice) && !user.UserDevices.Any(ud => ud.DeviceHash == currentDevice))
            {
                string os = currentDevice.Contains("Windows") ? "Windows" : currentDevice.Contains("Mac OS") ? "Mac OS" : currentDevice.Contains("Linux") ? "Linux" : "Unknown OS";
                string browser = currentDevice.Contains("Chrome") ? "Chrome" : currentDevice.Contains("Firefox") ? "Firefox" : currentDevice.Contains("Safari") ? "Safari" : "Unknown Browser";
                string currentIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                var newDevice = new UserDevice
                {
                    UserID = user.UserID,
                    DeviceHash = currentDevice,
                    OS = os,
                    Browser = browser,
                    IPAddress = currentIp,
                    AddedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow
                };

                _context.UserDevices.Add(newDevice);
                user.UserDevices.Add(newDevice);
                await _context.SaveChangesAsync();
            }

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDevice(int deviceId)
        {
            var sellerId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(sellerId)) return RedirectToAction("Login", "Auth");

            var device = await _context.UserDevices.FirstOrDefaultAsync(d => d.Id == deviceId && d.UserID == sellerId);
            if (device != null)
            {
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
                        Action = "User successfully changed their password via Seller Profile settings.",
                        IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                        Timestamp = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Your password has been changed successfully.";
                    return RedirectToAction("Profile");
                }
            }
            return View(model);
        }

        // POST: /Seller/UpgradeAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpgradeAccount(string storeName, string ssmNumber)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null) return NotFound();

            user.Role = "Seller";
            user.StoreName = storeName;
            user.SSMNumber = ssmNumber;

            _context.AuditLogs.Add(new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = user.UserID,
                Action = $"Account upgraded to Merchant. Store: {storeName}, SSM: {ssmNumber}",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            // Destroy old cookie and re-issue new cookie containing "Seller" role claim
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserID),
                new Claim(ClaimTypes.Name, user.Email.Split('@')[0]),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, "Seller")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            TempData["SuccessMessage"] = $"Welcome aboard, {storeName}! Your merchant account is now active.";
            return RedirectToAction("Dashboard");
        }

        // GET: /Seller/Login (Dedicated B2B Login Gateway)
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated && User.IsInRole("Seller"))
            {
                return RedirectToAction("Dashboard");
            }
            return View(new LoginViewModel());
        }

        // GET: /Seller/Dashboard
        [HttpGet]
        [Authorize(Roles = "Seller")]
        public async Task<IActionResult> Dashboard()
        {
            // IDOR Protection: Read Seller ID directly from authenticated claims
            string sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.SellerID == sellerId)
                .OrderByDescending(p => p.ProductID)
                .ToListAsync();

            var productIds = products.Select(p => p.ProductID).ToList();

            var recentSales = await _context.OrderItems
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.Buyer)
                .Include(oi => oi.Product)
                .Where(oi => productIds.Contains(oi.ProductID))
                .OrderByDescending(oi => oi.Order.CreatedAt)
                .Take(10)
                .ToListAsync();

            ViewBag.SellerID = sellerId;
            ViewBag.TotalProducts = products.Count;
            ViewBag.LowStockCount = products.Count(p => p.StockLevel < 5);
            ViewBag.TotalRevenue = recentSales.Sum(oi => oi.UnitPrice * oi.Quantity);
            ViewBag.RecentSales = recentSales;

            return View(products);
        }

        // GET: /Seller/MyProducts
        [HttpGet]
        [Authorize(Roles = "Seller")]
        public async Task<IActionResult> MyProducts()
        {
            // IDOR Protection: Read Seller ID directly from authenticated claims
            string sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.SellerID == sellerId)
                .OrderByDescending(p => p.ProductID)
                .ToListAsync();

            ViewBag.SellerID = sellerId;
            return View(products);
        }

        // POST: /Seller/UpdateStock
        [HttpPost]
        [Authorize(Roles = "Seller")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStock(string productId, int newStockLevel)
        {
            // IDOR Protection: Restrict stock modifications strictly to the authenticated seller's items
            string sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == productId && p.SellerID == sellerId);
            if (product == null)
            {
                return NotFound();
            }

            int oldStock = product.StockLevel;
            product.StockLevel = newStockLevel;

            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = sellerId,
                Action = $"Updated stock for {product.Title} from {oldStock} to {newStockLevel}",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Stock level updated for {product.Title}.";
            return RedirectToAction(nameof(MyProducts));
        }

        // GET: /Seller/Orders
        [HttpGet]
        [Authorize(Roles = "Seller")]
        public async Task<IActionResult> Orders()
        {
            // IDOR Protection: Read Seller ID directly from authenticated claims
            string sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var productIds = await _context.Products
                .Where(p => p.SellerID == sellerId)
                .Select(p => p.ProductID)
                .ToListAsync();

            var orderItems = await _context.OrderItems
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.Buyer)
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.Refunds)
                .Include(oi => oi.Product)
                .Where(oi => productIds.Contains(oi.ProductID))
                .OrderByDescending(oi => oi.Order.CreatedAt)
                .ToListAsync();

            ViewBag.SellerID = sellerId;
            return View(orderItems);
        }

        // POST: /Seller/ShipOrder
        [HttpPost]
        [Authorize(Roles = "Seller")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ShipOrder(string orderId)
        {
            string sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderID == orderId && o.OrderItems.Any(oi => oi.Product.SellerID == sellerId));
            if (order == null) return NotFound("Order not found or access denied.");

            // Update Delivery Record
            var delivery = await _context.Deliveries.FirstOrDefaultAsync(d => d.OrderID == orderId);
            if (delivery != null && (order.Status == "Processing" || order.Status == "Paid"))
            {
                delivery.TrackingNumber = "TRK-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
                delivery.Status = "Pending Pickup";
                delivery.EstimatedDeliveryDate = DateTime.UtcNow.AddDays(3);
                
                // Update Order Status
                order.Status = "Shipped";

                var auditLog = new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = sellerId,
                    Action = $"Shipped order {orderId}. Tracking: {delivery.TrackingNumber}",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Order {orderId} has been shipped! Tracking: {delivery.TrackingNumber}";
            }
            
            return RedirectToAction(nameof(Orders));
        }

        // GET: /Seller/Refunds
        [HttpGet]
        [Authorize(Roles = "Seller")]
        public async Task<IActionResult> Refunds()
        {
            string sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var productIds = await _context.Products
                .Where(p => p.SellerID == sellerId)
                .Select(p => p.ProductID)
                .ToListAsync();

            var orderIds = await _context.OrderItems
                .Where(oi => productIds.Contains(oi.ProductID))
                .Select(oi => oi.OrderID)
                .Distinct()
                .ToListAsync();

            var refunds = await _context.Refunds
                .Include(r => r.Order)
                    .ThenInclude(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                .Where(r => orderIds.Contains(r.OrderID))
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            ViewBag.SellerID = sellerId;
            return View(refunds);
        }

        [HttpPost]
        [Authorize(Roles = "Seller")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRefund(string refundId)
        {
            string sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var refund = await _context.Refunds
                .Include(r => r.Order)
                .ThenInclude(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(r => r.RefundID == refundId && r.Order.OrderItems.Any(oi => oi.Product.SellerID == sellerId));
            if (refund != null && (refund.Status == "RETURN_REQUESTED" || refund.Status == "Requested"))
            {
                refund.Status = "RETURN_APPROVED";
                refund.ReturnTrackingNumber = "RET-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
                refund.ReturnCourier = "J&T Express (Return)";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Refund approved. Return label generated.";
            }
            return RedirectToAction(nameof(Refunds));
        }

        [HttpPost]
        [Authorize(Roles = "Seller")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRefund(string refundId, string reason)
        {
            string sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var refund = await _context.Refunds
                .Include(r => r.Order)
                .ThenInclude(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(r => r.RefundID == refundId && r.Order.OrderItems.Any(oi => oi.Product.SellerID == sellerId));
            if (refund != null && (refund.Status == "RETURN_REQUESTED" || refund.Status == "Requested"))
            {
                refund.Status = "DISPUTED";
                refund.SellerNotes = reason;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Refund rejected. Sent to Admin for dispute resolution.";
            }
            return RedirectToAction(nameof(Refunds));
        }

        [HttpPost]
        [Authorize(Roles = "Seller")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmRefundReceipt(string refundId)
        {
            string sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var refund = await _context.Refunds
                .Include(r => r.Order)
                .ThenInclude(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(r => r.RefundID == refundId && r.Order.OrderItems.Any(oi => oi.Product.SellerID == sellerId));
            if (refund != null && refund.Status == "RETURN_RECEIVED")
            {
                refund.Status = "REFUND_COMPLETED";
                
                // Also update the Order status if needed
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == refund.OrderID);
                if(order != null) {
                    order.Status = "Refunded";
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Parcel received. Refund issued to buyer.";
            }
            return RedirectToAction(nameof(Refunds));
        }

        [HttpPost]
        [Authorize(Roles = "Seller")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisputeRefund(string refundId, string issue)
        {
            string sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var refund = await _context.Refunds
                .Include(r => r.Order)
                .ThenInclude(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(r => r.RefundID == refundId && r.Order.OrderItems.Any(oi => oi.Product.SellerID == sellerId));
            if (refund != null && refund.Status == "RETURN_RECEIVED")
            {
                refund.Status = "DISPUTED";
                refund.SellerNotes = issue;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Issue reported. Sent to Admin for arbitration.";
            }
            return RedirectToAction(nameof(Refunds));
        }

        [HttpPost]
        [Authorize(Roles = "Seller")]
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

                string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
    }
}