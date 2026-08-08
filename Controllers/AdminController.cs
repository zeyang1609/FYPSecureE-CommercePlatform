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
            // 1. Secret Key Protection (Security by Obscurity)
            // The URL parameter is the encrypted string: ?key=aadec5c7857263ec97afa3b25ef6baed48af91911fa449e499ee693bfc6afd0b
            string secretKey = Request.Query["key"];
            if (string.IsNullOrEmpty(secretKey) || secretKey != "aadec5c7857263ec97afa3b25ef6baed48af91911fa449e499ee693bfc6afd0b")
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
            ViewBag.ActiveDisputes = await _context.Refunds.CountAsync(r => r.Status == "Dispute");
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
    }
}