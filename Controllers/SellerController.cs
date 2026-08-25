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
using Microsoft.AspNetCore.SignalR;
using FYP.Hubs;
using Stripe;
using Microsoft.Extensions.Configuration;

namespace FYP.Controllers
{
    [Authorize(Roles = "Seller")]
    public class SellerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IOtpService _otpService;
        private readonly IHubContext<OrderHub> _orderHubContext;
        private readonly IConfiguration _configuration;
        private readonly IPaymentEncryptionService _paymentEncryptionService;
        private readonly PythonAiClient _aiClient;

        public SellerController(ApplicationDbContext context, IOtpService otpService, IHubContext<OrderHub> orderHubContext, IConfiguration configuration, IPaymentEncryptionService paymentEncryptionService, PythonAiClient aiClient)
        {
            _context = context;
            _otpService = otpService;
            _orderHubContext = orderHubContext;
            _configuration = configuration;
            _paymentEncryptionService = paymentEncryptionService;
            _aiClient = aiClient;
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
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == sellerId);
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
                        Action = "User successfully changed their password via Seller Profile settings.",
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
            ViewBag.LowStockCount = products.Count(p => p.StockLevel <= 5);
            ViewBag.TotalRevenue = recentSales.Sum(oi => oi.UnitPrice * oi.Quantity);
            ViewBag.RecentSales = recentSales;

            // --- AI Demand Forecasting Logic ---
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            
            // Fetch order items from last 30 days for seller's products
            var last30DaysOrderItems = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => productIds.Contains(oi.ProductID) 
                             && oi.Order.CreatedAt >= thirtyDaysAgo
                             && (oi.Order.Status == "Completed" || oi.Order.Status == "To Ship" || oi.Order.Status == "To Receive" || oi.Order.Status == "Paid"))
                .ToListAsync();

            // Prepare payload for top 5 products (to keep the UI clean)
            var topProducts = products.Take(5).ToList();
            var payloadProducts = new List<object>();

            foreach (var p in topProducts)
            {
                int[] salesLast30Days = new int[30];
                var productOrders = last30DaysOrderItems.Where(oi => oi.ProductID == p.ProductID).ToList();
                
                foreach (var po in productOrders)
                {
                    int dayIndex = (po.Order.CreatedAt.Date - thirtyDaysAgo.Date).Days;
                    if (dayIndex >= 0 && dayIndex < 30)
                    {
                        salesLast30Days[dayIndex] += po.Quantity;
                    }
                }
                
                payloadProducts.Add(new
                {
                    id = p.ProductID,
                    title = p.Title,
                    stock = p.StockLevel,
                    recent_sales_30d = salesLast30Days
                });
            }

            var aiPayload = new { products = payloadProducts };
            var forecastResults = await _aiClient.ForecastDemandAsync(aiPayload);
            ViewBag.DemandForecasts = forecastResults;

            return View(products);
        }

        // GET: /Seller/Analytics
        [HttpGet]
        [Authorize(Roles = "Seller")]
        public async Task<IActionResult> Analytics()
        {
            string sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var products = await _context.Products
                .Where(p => p.SellerID == sellerId)
                .ToListAsync();

            var productIds = products.Select(p => p.ProductID).ToList();

            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var recentOrderItems = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)
                .Where(oi => productIds.Contains(oi.ProductID) 
                             && oi.Order.CreatedAt >= thirtyDaysAgo
                             && (oi.Order.Status == "Completed" || oi.Order.Status == "To Ship" || oi.Order.Status == "To Receive" || oi.Order.Status == "Paid" || oi.Order.Status == "Success" || oi.Order.Status == "Approved"))
                .ToListAsync();

            // 1. Sales by Product (Chart)
            var salesByProduct = recentOrderItems
                .GroupBy(oi => oi.Product.Title)
                .Select(g => new { ProductName = g.Key, Quantity = g.Sum(oi => oi.Quantity) })
                .OrderByDescending(x => x.Quantity)
                .Take(10)
                .ToList();

            // 2. Revenue Over Time (Chart)
            var revenueOverTime = recentOrderItems
                .GroupBy(oi => oi.Order.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new { Date = g.Key.ToString("MMM dd"), Revenue = g.Sum(oi => oi.UnitPrice * oi.Quantity) })
                .ToList();

            ViewBag.SalesByProductLabels = salesByProduct.Select(x => x.ProductName).ToArray();
            ViewBag.SalesByProductData = salesByProduct.Select(x => x.Quantity).ToArray();

            ViewBag.RevenueDates = revenueOverTime.Select(x => x.Date).ToArray();
            ViewBag.RevenueData = revenueOverTime.Select(x => x.Revenue).ToArray();
            
            ViewBag.SellerID = sellerId;
            return View(products.OrderByDescending(p => p.TotalSales).ToList());
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

            // Notify wishlisted buyers if restocked or low stock
            var wishlistedBuyerIds = await _context.Wishlists
                .Where(w => w.ProductID == product.ProductID)
                .Select(w => w.BuyerID)
                .Distinct()
                .ToListAsync();

            if (wishlistedBuyerIds.Any())
            {
                // Restock alert (0 -> >0)
                if (oldStock == 0 && newStockLevel > 0)
                {
                    foreach (var buyerId in wishlistedBuyerIds)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            UserID = buyerId,
                            Type = "Restock Alert",
                            Content = $"Back in stock! '{product.Title}' in your wishlist is now available with {newStockLevel} units."
                        });
                    }
                }
                // Low stock alert (>5 -> <=5 and >0)
                else if (oldStock > 5 && newStockLevel <= 5 && newStockLevel > 0)
                {
                    foreach (var buyerId in wishlistedBuyerIds)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            UserID = buyerId,
                            Type = "Low Stock Alert",
                            Content = $"Hurry! '{product.Title}' in your wishlist is running low on stock (only {newStockLevel} left)."
                        });
                    }
                }
            }

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
                .Where(oi => productIds.Contains(oi.ProductID) && oi.Order.Status != "Pending Approve" && oi.Order.Status != "Rejected")
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
                
                // Update Order Status to Pending Pickup until courier actually collects it
                order.Status = "Pending Pickup";

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

                // Broadcast real-time update to the buyer
                if (!string.IsNullOrEmpty(order.BuyerID))
                {
                    await _orderHubContext.Clients.Group(order.BuyerID).SendAsync("OrderStatusUpdated", order.OrderID, "Shipped", $"Your order {order.OrderID} has been shipped out.");
                    
                    _context.Notifications.Add(new Notification
                    {
                        NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                        UserID = order.BuyerID,
                        Type = "Delivery Update",
                        Content = $"Your order {orderId} has been prepared and is pending pickup!"
                    });
                    await _context.SaveChangesAsync();
                }

                // Broadcast real-time update to couriers
                await _orderHubContext.Clients.Group("Couriers").SendAsync("NewPickupReady", delivery.DeliveryID, $"New parcel ready for pickup: {delivery.TrackingNumber}");

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
                .Include(r => r.Order)
                    .ThenInclude(o => o.Buyer)
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
                if (refund.ReturnMethod == "Pick-Up" || refund.ReturnMethod == "Pickup") {
                    refund.ReturnCourier = "J&T Express";
                } else {
                    refund.ReturnCourier = "";
                }
                await _context.SaveChangesAsync();
                
                await _orderHubContext.Clients.Group(refund.Order.BuyerID).SendAsync("ReceiveReturnUpdate");
                await _orderHubContext.Clients.Group("Couriers").SendAsync("ReceiveReturnUpdate");
                
                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = sellerId,
                    Action = $"Approved refund {refundId} for order {refund.OrderID}",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                });
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

                await _orderHubContext.Clients.Group(refund.Order.BuyerID).SendAsync("ReceiveReturnUpdate");
                await _orderHubContext.Clients.Group("Admins").SendAsync("ReceiveReturnUpdate");

                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = sellerId,
                    Action = $"Rejected refund {refundId} for order {refund.OrderID}. Reason: {reason}",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                });
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
                            IdempotencyKey = $"refund_{refund.RefundID}"
                        };

                        var refundService = new Stripe.RefundService();
                        var stripeRefund = await refundService.CreateAsync(options, requestOptions);

                        refund.StripeRefundId = stripeRefund.Id;
                        refund.RefundedAt = DateTime.UtcNow;
                    }
                    catch (StripeException ex)
                    {
                        TempData["ErrorMessage"] = $"Stripe Refund Failed: {ex.Message}";
                        return RedirectToAction(nameof(Refunds));
                    }
                    }
                }
                else
                {
                    refund.RefundedAt = DateTime.UtcNow;
                }

                refund.Status = "REFUND_COMPLETED";
                
                // Also update the Order status if needed
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == refund.OrderID);
                if (order != null)
                {
                    order.Status = "Refunded";
                }

                await _context.SaveChangesAsync();
                
                await _orderHubContext.Clients.Group(refund.Order.BuyerID).SendAsync("ReceiveReturnUpdate");
                
                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = sellerId,
                    Action = $"Confirmed receipt and processed refund {refundId} for order {refund.OrderID}",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Parcel received. Refund issued successfully to buyer.";
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
                
                await _orderHubContext.Clients.Group(refund.Order.BuyerID).SendAsync("ReceiveReturnUpdate");
                await _orderHubContext.Clients.Group("Admins").SendAsync("ReceiveReturnUpdate");
                
                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = sellerId,
                    Action = $"Disputed refund {refundId} for order {refund.OrderID}. Issue: {issue}",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                });
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

    }
}
