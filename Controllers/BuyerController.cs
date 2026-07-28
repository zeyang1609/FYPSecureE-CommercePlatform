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

namespace FYP.Controllers
{
    public class BuyerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BuyerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Buyer/Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard(string buyerId = "USR-BUYER-DEMO")
        {
            // 1. Fetch recent orders for table display
            var recentOrders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.BuyerID == buyerId)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync();

            // 2. Fetch user notifications
            var notifications = await _context.Notifications
                .Where(n => n.UserID == buyerId)
                .OrderByDescending(n => n.NotificationID)
                .Take(10)
                .ToListAsync(); 

            // 3. Calculate spending KPIs for dashboard metric cards
            var allOrders = await _context.Orders.Where(o => o.BuyerID == buyerId).ToListAsync();
            ViewBag.TotalOrders = allOrders.Count;
            ViewBag.TotalSpent = allOrders.Sum(o => o.TotalAmount);
            ViewBag.ActiveOrdersCount = allOrders.Count(o => o.Status == "Pending" || o.Status == "Processing" || o.Status == "Shipped");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == buyerId);
            ViewBag.MfaEnabled = user?.MFA_Enabled ?? true;
            ViewBag.BuyerID = buyerId;
            ViewBag.Email = user?.Email ?? "buyer.demo@secureplatform.com";
            ViewBag.Notifications = notifications;

            return View(recentOrders);
        }

        // GET: /Buyer/Orders
        [HttpGet]
        public async Task<IActionResult> Orders(string buyerId = "USR-BUYER-DEMO")
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.Payment)
                .Where(o => o.BuyerID == buyerId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync(); 

            ViewBag.BuyerID = buyerId;
            return View(orders);
        }

        // GET: /Buyer/OrderDetails/ORD-1234567890AB
        [HttpGet]
        public async Task<IActionResult> OrderDetails(string orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.Payment)
                .Include(o => o.FraudAlert)
                .FirstOrDefaultAsync(o => o.OrderID == orderId); 

            if (order == null)
            {
                return NotFound("Order not found.");
            }

            ViewBag.BuyerID = order.BuyerID;
            return View(order);
        }

        // POST: /Buyer/RequestRefund
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestRefund(string orderId, decimal refundAmount, string reason)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == orderId); 
            if (order == null)
            {
                return NotFound();
            }

            string refundId = "RFD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
            var refund = new Refund
            {
                RefundID = refundId,
                OrderID = orderId,
                RefundAmount = refundAmount,
                Status = "Requested"
            }; 

            // Log security audit trail (Updated currency to RM)
            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = order.BuyerID,
                Action = $"Refund requested for Order {orderId} (Amount: RM {refundAmount:0.00})",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };

            // Notify buyer/platform
            var notification = new Notification
            {
                NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = order.BuyerID,
                Type = "Refund Request",
                Content = $"Refund requested for Order {orderId}. Reason: {reason}"
            }; 

            _context.Refunds.Add(refund); 
            _context.AuditLogs.Add(auditLog); 
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Refund request submitted successfully.";
            return RedirectToAction(nameof(Orders), new { buyerId = order.BuyerID });
        }

        // GET: /Buyer/Profile
        [HttpGet]
        public async Task<IActionResult> Profile(string buyerId = "USR-BUYER-DEMO")
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == buyerId);
            if (user == null)
            {
                return NotFound("User profile not found.");
            }

            var viewModel = new BuyerProfileViewModel
            {
                UserID = user.UserID,
                Email = user.Email,
                Role = user.Role,
                MfaEnabled = user.MFA_Enabled,
                DeviceHash = user.DeviceHash ?? "No Device Hash Recorded"
            };

            return View(viewModel);
        }

        // POST: /Buyer/UpdateProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(BuyerProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Profile", model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == model.UserID);
            if (user == null)
            {
                return NotFound("User profile not found.");
            }

            user.Email = model.Email;
            user.MFA_Enabled = model.MfaEnabled;

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
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your security profile and zero-trust preferences have been updated!";
            return RedirectToAction("Profile", new { buyerId = user.UserID });
        }
    }
}