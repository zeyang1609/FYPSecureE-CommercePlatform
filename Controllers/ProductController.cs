using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using FYP.Models.Entities;
using FYP.Models.ViewModels;
using FYP.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PythonAiClient _aiClient;

        public ProductController(ApplicationDbContext context, PythonAiClient aiClient)
        {
            _context = context;
            _aiClient = aiClient;
        }

        // GET: /Product/Index
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .OrderByDescending(p => p.ProductID)
                .ToListAsync();

            return View(products);
        }

        // GET: /Product/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Fetch active categories from MySQL to populate the selector dropdown
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            return View();
        }

        // POST: /Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductUploadViewModel model, string categoryId)
        {
            // Re-populate categories in case validation fails and we return to the view
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();

            if (string.IsNullOrWhiteSpace(categoryId))
            {
                ModelState.AddModelError("", "Please select a storefront category.");
                return View(model);
            }

            if (model.ImageFile == null || model.ImageFile.Length == 0)
            {
                ModelState.AddModelError("ImageFile", "Please upload a product image for AI verification.");
                return View(model);
            }

            // 1. Read uploaded image file into a byte array
            byte[] imageBytes;
            using (var memoryStream = new MemoryStream())
            {
                await model.ImageFile.CopyToAsync(memoryStream);
                imageBytes = memoryStream.ToArray();
            }

            // 2. Compute SHA-256 ImageHash for database indexing and blacklisting
            string imageHash;
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(imageBytes);
                imageHash = Convert.ToHexString(hashBytes).ToLower();
            }

            // 3. Check database if this exact image hash was previously flagged for fraud
            bool isBlacklisted = await _context.Products.AnyAsync(p => p.ImageHash == imageHash);
            if (isBlacklisted)
            {
                ModelState.AddModelError("ImageFile", "Security Block: This image matches a previously flagged counterfeit listing.");
                return View(model);
            }

            // 4. Send image to Python Microservice (Runs OpenCV ELA scan & HuggingFace NSFW check)
            ImageScanResult scanResult = await _aiClient.ScanImageForForgeryAsync(imageBytes);

            if (scanResult.IsForgeryDetected)
            {
                ModelState.AddModelError("ImageFile", $"AI Security Block: {scanResult.ForgeryReason}");
                return View(model);
            }

            // 5. Generate explicit production primary key and map entity properties
            string productId = "PRD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();

            var product = new Product
            {
                ProductID = productId,
                SellerID = "USR-SELLER-DEMO", // In production, retrieve from authenticated user session
                CategoryID = categoryId,
                Title = model.Title,
                Price = model.Price,
                StockLevel = model.StockLevel,
                ImageHash = imageHash
            };

            // 6. Save verified image to disk using ProductID as the filename
            string fileName = $"{product.ProductID}.jpg";
            string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", fileName);

            Directory.CreateDirectory(Path.GetDirectoryName(uploadPath)!);
            await System.IO.File.WriteAllBytesAsync(uploadPath, imageBytes);

            // 7. Record immutable security audit trail
            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = product.SellerID,
                Action = $"Listed new product {product.Title} ({productId}) - AI Vision Verified",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };

            _context.Products.Add(product);
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Product verified by AI vision forensic scan and listed successfully!";
            return RedirectToAction("Index", "Home");
        }
    }
}