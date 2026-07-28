using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using FYP.Models.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    public class SellerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SellerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Seller/Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard(string sellerId = "USR-SELLER-DEMO")
        {
            // 1. Fetch products with eager loading for Category names
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.SellerID == sellerId)
                .OrderByDescending(p => p.ProductID)
                .ToListAsync();

            var productIds = products.Select(p => p.ProductID).ToList();

            // 2. Fetch order items containing this seller's products
            var recentSales = await _context.OrderItems
                .Include(oi => oi.Order)
                    .ThenInclude(o => o.Buyer)
                .Include(oi => oi.Product)
                .Where(oi => productIds.Contains(oi.ProductID))
                .OrderByDescending(oi => oi.Order.CreatedAt)
                .Take(10)
                .ToListAsync();

            // 3. Populate ViewBag KPIs for the dashboard layout
            ViewBag.SellerID = sellerId;
            ViewBag.TotalProducts = products.Count;
            ViewBag.LowStockCount = products.Count(p => p.StockLevel < 5);
            ViewBag.TotalRevenue = recentSales.Sum(oi => oi.UnitPrice * oi.Quantity);
            ViewBag.RecentSales = recentSales;

            return View(products);
        }

        // GET: /Seller/MyProducts
        [HttpGet]
        public async Task<IActionResult> MyProducts(string sellerId = "USR-SELLER-DEMO")
        {
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStock(string productId, int newStockLevel, string sellerId = "USR-SELLER-DEMO")
        {
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
            return RedirectToAction(nameof(MyProducts), new { sellerId = sellerId });
        }

        // GET: /Seller/Orders
        [HttpGet]
        public async Task<IActionResult> Orders(string sellerId = "USR-SELLER-DEMO")
        {
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