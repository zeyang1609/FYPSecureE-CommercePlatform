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

namespace FYP.Controllers
{
    [Authorize]
    public class SellerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SellerController(ApplicationDbContext context)
        {
            _context = context;
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

        // POST: /Seller/UpgradeAccount
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpgradeAccount(string storeName, string ssmNumber)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);

            if (user == null) return NotFound();

            user.Role = "Seller";

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
                .Include(oi => oi.Product)
                .Where(oi => productIds.Contains(oi.ProductID))
                .OrderByDescending(oi => oi.Order.CreatedAt)
                .ToListAsync();

            ViewBag.SellerID = sellerId;
            return View(orderItems);
        }
    }
}