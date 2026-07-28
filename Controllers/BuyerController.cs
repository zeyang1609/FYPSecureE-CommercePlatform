using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using FYP.Models.Entities;
using System;
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
        public async Task<IActionResult> Dashboard(string buyerId)
        {
            var recentOrders = await _context.Orders
                .Where(o => o.BuyerID == buyerId)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync();

            var notifications = await _context.Notifications
                .Where(n => n.UserID == buyerId)
                .OrderByDescending(n => n.NotificationID)
                .Take(10)
                .ToListAsync(); 

            ViewBag.Notifications = notifications;
            return View(recentOrders);
        }

        // GET: /Buyer/Orders
        [HttpGet]
        public async Task<IActionResult> Orders(string buyerId)
        {
            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.Payment)
                .Where(o => o.BuyerID == buyerId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync(); 

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
                return NotFound();
            }

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

            // Log security audit trail
            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = order.BuyerID,
                Action = $"Refund requested for Order {orderId} (Amount: ${refundAmount})",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };

            // Notify seller
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
    }
}