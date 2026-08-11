using FYP.Data;
using FYP.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using FYP.Hubs;

namespace FYP.Controllers
{
    [Authorize(Roles = "Courier")]
    public class CourierController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<OrderHub> _orderHubContext;

        public CourierController(ApplicationDbContext context, IHubContext<OrderHub> orderHubContext)
        {
            _context = context;
            _orderHubContext = orderHubContext;
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
            // Get all shipped or pending pickup deliveries waiting to be delivered
            var deliveries = await _context.Deliveries
                .Where(d => d.Status == "Shipped" || d.Status == "Pending Pickup")
                .OrderBy(d => d.EstimatedDeliveryDate)
                .ToListAsync();

            var orderIds = deliveries.Select(d => d.OrderID).ToList();
            
            var deliveryHistory = await _context.Deliveries
                .Where(d => d.Status == "Delivered")
                .OrderByDescending(d => d.ActualDeliveryDate)
                .Take(50)
                .ToListAsync();
            
            var historyOrderIds = deliveryHistory.Select(d => d.OrderID).ToList();
            
            // Backfill legacy missing tracking numbers for approved/completed returns
            var legacyRefunds = await _context.Refunds
                .Where(r => string.IsNullOrEmpty(r.ReturnTrackingNumber) && r.Status != "RETURN_REQUESTED" && r.Status != "Requested")
                .ToListAsync();
            if (legacyRefunds.Any())
            {
                foreach (var r in legacyRefunds)
                {
                    r.ReturnTrackingNumber = "RET-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
                }
                await _context.SaveChangesAsync();
            }
            
            // Get return and refund deliveries
            var returnDeliveries = await _context.Refunds
                .Where(r => ((r.Status == "RETURN_APPROVED" || r.Status == "RETURN_REQUESTED" || r.Status == "Requested") && (r.ReturnMethod == "Pick-Up" || r.ReturnMethod == "Pickup" || (r.ReturnMethod == "Drop-Off" && !string.IsNullOrEmpty(r.ReturnCourier)))) || r.Status == "RETURN_IN_TRANSIT")
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

            if (order != null && !string.IsNullOrEmpty(order.BuyerID))
            {
                await _orderHubContext.Clients.Group(order.BuyerID).SendAsync("OrderStatusUpdated", order.OrderID, "Delivered", $"Your order {order.OrderID} has been delivered.");
            }

            TempData["SuccessMessage"] = $"Tracking {delivery.TrackingNumber} successfully marked as Delivered!";
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message = TempData["SuccessMessage"] });
                
            return RedirectToAction(nameof(Dashboard));
        }

        // POST: /Courier/PickupDelivery
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PickupDelivery(string deliveryId)
        {
            var delivery = await _context.Deliveries.Include(d => d.Order).FirstOrDefaultAsync(d => d.DeliveryID == deliveryId);
            if (delivery != null && delivery.Status == "Pending Pickup")
            {
                delivery.Status = "Shipped"; // This marks it as 'In Transit'
                if (delivery.Order != null) {
                    delivery.Order.Status = "Shipped";
                }
                await _context.SaveChangesAsync();
                
                if (delivery.Order != null && !string.IsNullOrEmpty(delivery.Order.BuyerID))
                {
                    await _orderHubContext.Clients.Group(delivery.Order.BuyerID).SendAsync("OrderStatusUpdated", delivery.OrderID, "Shipped", $"Your parcel for order {delivery.OrderID} has been picked up by the courier.");
                }

                TempData["SuccessMessage"] = $"Tracking {delivery.TrackingNumber} marked as Picked Up and is now in transit.";
            }
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message = TempData["SuccessMessage"] });
                
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PickupReturn(string refundId)
        {
            var refund = await _context.Refunds
                .Include(r => r.Order)
                .ThenInclude(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(r => r.RefundID == refundId);
            if (refund != null && refund.Status == "RETURN_APPROVED")
            {
                refund.Status = "RETURN_IN_TRANSIT";
                if (refund.ReturnMethod == "Drop-Off")
                {
                    refund.PickupDate = DateTime.Now;
                }
                await _context.SaveChangesAsync();
                
                if (refund.Order != null)
                {
                    await _orderHubContext.Clients.Group(refund.Order.BuyerID).SendAsync("ReceiveReturnUpdate");
                    var sellerId = refund.Order.OrderItems.FirstOrDefault()?.Product?.SellerID;
                    if (sellerId != null) await _orderHubContext.Clients.Group(sellerId).SendAsync("ReceiveReturnUpdate");
                }
                
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
            var refund = await _context.Refunds
                .Include(r => r.Order)
                .ThenInclude(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(r => r.RefundID == refundId);
            if (refund != null && refund.Status == "RETURN_IN_TRANSIT")
            {
                refund.Status = "RETURN_RECEIVED";
                await _context.SaveChangesAsync();
                
                if (refund.Order != null)
                {
                    await _orderHubContext.Clients.Group(refund.Order.BuyerID).SendAsync("ReceiveReturnUpdate");
                    var sellerId = refund.Order.OrderItems.FirstOrDefault()?.Product?.SellerID;
                    if (sellerId != null) await _orderHubContext.Clients.Group(sellerId).SendAsync("ReceiveReturnUpdate");
                }
                
                TempData["SuccessMessage"] = $"Return parcel {refund.ReturnTrackingNumber} successfully delivered to Seller.";
            }
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message = TempData["SuccessMessage"] });
                
            return RedirectToAction(nameof(Dashboard));
        }
    }
}
