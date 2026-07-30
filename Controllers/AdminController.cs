using FYP.Data;
using FYP.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace FYP.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

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

        [HttpGet]
        public async Task<IActionResult> XaiDetails(string alertId)
        {
            var alert = await _context.FraudAlerts
                .Include(f => f.Order)
                .FirstOrDefaultAsync(a => a.AlertID == alertId);

            if (alert == null)
            {
                return NotFound();
            }

            // Returns alert containing SHAP_Data JSON string to the Razor View for rendering
            return View(alert);
        }

        // GET: /Admin/Categories
        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(categories);
        }

        // POST: /Admin/CreateCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(string name, string description, string iconSvg)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Category name cannot be empty.";
                return RedirectToAction(nameof(Categories));
            }

            // Default icon if admin leaves it blank
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

            // Log security audit trail[cite: 3]
            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = "ADMIN", // In production, retrieve from authenticated session[cite: 12]
                Action = $"Created new catalog category: {name} ({categoryId})",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };

    _context.Categories.Add(category);
            _context.AuditLogs.Add(auditLog); 
    await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Category '{name}' added successfully!";
            return RedirectToAction(nameof(Categories));
        }
    }
}