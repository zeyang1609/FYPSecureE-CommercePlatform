using FYP.Data;
using FYP.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    [Authorize(Roles = "Courier")]
    public class CourierController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CourierController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Courier/Login
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            // Return Courier Login View
            return View(new FYP.Models.ViewModels.LoginViewModel { Role = "Courier" });
        }

        // GET: /Courier/Register
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register()
        {
            return View(new FYP.Models.ViewModels.RegisterViewModel { Role = "Courier" });
        }

        // GET: /Courier/Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            // Get all shipped deliveries waiting to be delivered
            var deliveries = await _context.Deliveries
                .Where(d => d.Status == "Shipped")
                .OrderBy(d => d.EstimatedDeliveryDate)
                .ToListAsync();

            var orderIds = deliveries.Select(d => d.OrderID).ToList();
            
            var deliveryHistory = await _context.Deliveries
                .Where(d => d.Status == "Delivered")
                .OrderByDescending(d => d.ActualDeliveryDate)
                .Take(50)
                .ToListAsync();
            
            var historyOrderIds = deliveryHistory.Select(d => d.OrderID).ToList();
            
            // Get return and refund deliveries
            var returnDeliveries = await _context.Refunds
                .Where(r => (r.Status == "RETURN_APPROVED" && r.ReturnMethod == "Pick-Up") || r.Status == "RETURN_IN_TRANSIT")
                .OrderBy(r => r.RequestedAt)
                .ToListAsync();
                
            var returnOrderIds = returnDeliveries.Select(r => r.OrderID).ToList();

            var returnHistory = await _context.Refunds
                .Where(r => r.Status == "RETURN_RECEIVED" || r.Status == "REFUND_COMPLETED" || r.Status == "DISPUTED")
                .OrderByDescending(r => r.RequestedAt)
                .Take(50)
                .ToListAsync();
            var returnHistoryOrderIds = returnHistory.Select(r => r.OrderID).ToList();

            var allOrderIds = orderIds.Concat(historyOrderIds).Concat(returnOrderIds).Concat(returnHistoryOrderIds).Distinct().ToList();

            var orders = await _context.Orders
                .Include(o => o.Buyer)
                    .ThenInclude(u => u.Addresses)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => allOrderIds.Contains(o.OrderID))
                .ToDictionaryAsync(o => o.OrderID);

            ViewBag.Orders = orders;
            ViewBag.DeliveryHistory = deliveryHistory;
            ViewBag.ReturnDeliveries = returnDeliveries;
            ViewBag.ReturnHistory = returnHistory;

            return View(deliveries);
        }

        // POST: /Courier/MarkDelivered
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkDelivered(string deliveryId)
        {
            string courierId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var delivery = await _context.Deliveries.FirstOrDefaultAsync(d => d.DeliveryID == deliveryId);
            if (delivery == null || delivery.Status != "Shipped")
            {
                return NotFound("Delivery not found or not in shipped state.");
            }

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == delivery.OrderID);
            if (order != null)
            {
                order.Status = "Delivered";
            }

            delivery.Status = "Delivered";
            delivery.ActualDeliveryDate = DateTime.UtcNow;

            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = courierId,
                Action = $"Courier marked delivery {deliveryId} (Tracking: {delivery.TrackingNumber}) as Delivered.",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Tracking {delivery.TrackingNumber} successfully marked as Delivered!";
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message = TempData["SuccessMessage"] });
                
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PickupReturn(string refundId)
        {
            var refund = await _context.Refunds.FirstOrDefaultAsync(r => r.RefundID == refundId);
            if (refund != null && refund.Status == "RETURN_APPROVED")
            {
                refund.Status = "RETURN_IN_TRANSIT";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Return parcel {refund.ReturnTrackingNumber} marked as Picked Up.";
            }
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message = TempData["SuccessMessage"] });
                
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeliverReturn(string refundId)
        {
            var refund = await _context.Refunds.FirstOrDefaultAsync(r => r.RefundID == refundId);
            if (refund != null && refund.Status == "RETURN_IN_TRANSIT")
            {
                refund.Status = "RETURN_RECEIVED";
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Return parcel {refund.ReturnTrackingNumber} successfully delivered to Seller.";
            }
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message = TempData["SuccessMessage"] });
                
            return RedirectToAction(nameof(Dashboard));
        }
    }
}
