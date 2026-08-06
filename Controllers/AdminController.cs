using FYP.Data;
using FYP.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace FYP.Controllers
{
    [Authorize(Roles = "Admin")] 
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Admin/Login
        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            // 1. Network Geofencing (IP Allowlist Simulation)
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            // Example: Only allow localhost (::1 or 127.0.0.1) or specific university network IPs
            List<string> allowedIps = new List<string> { "::1", "127.0.0.1" };

            if (!allowedIps.Contains(clientIp))
            {
                // Drop the connection immediately for unauthorized networks
                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = "SYSTEM",
                    Action = $"SECURITY BLOCK: Unauthorized network {clientIp} attempted to access AdminOS gateway.",
                    IP_Address = clientIp,
                    Timestamp = DateTime.UtcNow
                });
                _context.SaveChanges();

                return Unauthorized("Error 401: Connection dropped. Unauthorized network IP.");
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
            ViewBag.AuditLogs = logs;

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
        public async Task<IActionResult> CreateCategory(string name, string description, string iconSvg)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Category name cannot be empty.";
                return RedirectToAction(nameof(Categories));
            }

            if (string.IsNullOrWhiteSpace(iconSvg))
            {
                iconSvg = "<svg width=\"32\" height=\"32\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><rect x=\"3\" y=\"3\" width=\"18\" height=\"18\" rx=\"2\" ry=\"2\"></rect><line x1=\"3\" y1=\"9\" x2=\"21\" y2=\"9\"></line><line x1=\"9\" y1=\"21\" x2=\"9\" y2=\"9\"></line></svg>";
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
            var refund = await _context.Refunds.FirstOrDefaultAsync(r => r.RefundID == refundId);
            if (refund != null && refund.Status == "DISPUTED")
            {
                refund.Status = "REFUND_COMPLETED";
                refund.AdminResolution = adminNote;
                
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderID == refund.OrderID);
                if(order != null) order.Status = "Refunded";

                _context.AuditLogs.Add(new AuditLog
                {
                    LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    Action = $"Arbitration: Forced refund for {refundId}. Notes: {adminNote}",
                    IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Dispute resolved in favor of Buyer (Refund Issued).";
            }
            return RedirectToAction(nameof(Disputes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectReturn(string refundId, string adminNote)
        {
            var refund = await _context.Refunds.FirstOrDefaultAsync(r => r.RefundID == refundId);
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
    }
}