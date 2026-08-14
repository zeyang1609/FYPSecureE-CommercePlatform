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
using Microsoft.AspNetCore.Authorization;

namespace FYP.Controllers
{
    [Authorize]
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

            // 3. Check database if this exact image hash was previously flagged for fraud (Duplicate Check)
            bool isDuplicate = await _context.Products.AnyAsync(p => p.ImageHash == imageHash);
            if (isDuplicate)
            {
                ModelState.AddModelError("ImageFile", "Security Block: This image matches a previously uploaded product listing.");
                return View(model);
            }

            // 3b. Check Global Image Blacklist
            bool isBlacklisted = await _context.BlacklistedImageHashes.AnyAsync(b => b.SHA256Hash == imageHash);
            if (isBlacklisted)
            {
                ModelState.AddModelError("ImageFile", "Security Block: This image is globally blacklisted for fraud/illegal content.");
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
                SellerID = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                CategoryID = categoryId,
                Title = model.Title,
                Price = model.Price,
                StockLevel = model.StockLevel,
                WeightKg = model.WeightKg,
                Description = model.Description,
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

            // Notify past buyers about the new product launch
            var pastBuyerIds = await _context.Orders
                .Where(o => o.OrderItems.Any(oi => oi.Product.SellerID == product.SellerID))
                .Select(o => o.BuyerID)
                .Distinct()
                .ToListAsync();

            var newNotifications = new List<Notification>();
            foreach (var buyerId in pastBuyerIds)
            {
                newNotifications.Add(new Notification
                {
                    NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    UserID = buyerId,
                    Type = "Live Alert",
                    Content = $"New Product Launch! Your favorite seller just added: {product.Title}."
                });
            }
            if (newNotifications.Any())
            {
                _context.Notifications.AddRange(newNotifications);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Product verified by AI vision forensic scan and listed successfully!";
            return RedirectToAction("MyProducts", "Seller");
        }

        // GET: /Product/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == id);
            if (product == null) return NotFound();

            string currentUserId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (product.SellerID != currentUserId)
            {
                return Forbid();
            }

            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();

            var model = new ProductEditViewModel
            {
                ProductID = product.ProductID,
                Title = product.Title,
                Price = product.Price,
                StockLevel = product.StockLevel,
                WeightKg = product.WeightKg,
                Description = product.Description
            };
            ViewBag.CurrentCategory = product.CategoryID;

            return View(model);
        }

        // POST: /Product/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductEditViewModel model, string categoryId)
        {
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CurrentCategory = categoryId;

            if (string.IsNullOrWhiteSpace(categoryId))
            {
                ModelState.AddModelError("", "Please select a storefront category.");
                return View(model);
            }

            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductID == model.ProductID);
            if (product == null) return NotFound();

            string currentUserId = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (product.SellerID != currentUserId)
            {
                return Forbid();
            }

            // If a new image is provided, run AI scan
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                byte[] imageBytes;
                using (var memoryStream = new MemoryStream())
                {
                    await model.ImageFile.CopyToAsync(memoryStream);
                    imageBytes = memoryStream.ToArray();
                }

                string imageHash;
                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(imageBytes);
                    imageHash = Convert.ToHexString(hashBytes).ToLower();
                }

                // 3. Check database if this exact image hash was previously flagged for fraud (Duplicate Check)
                bool isDuplicate = await _context.Products.AnyAsync(p => p.ImageHash == imageHash && p.ProductID != product.ProductID);
                if (isDuplicate)
                {
                    ModelState.AddModelError("ImageFile", "Security Block: This image matches a previously uploaded product listing.");
                    return View(model);
                }

                // 3b. Check Global Image Blacklist
                bool isBlacklisted = await _context.BlacklistedImageHashes.AnyAsync(b => b.SHA256Hash == imageHash);
                if (isBlacklisted)
                {
                    ModelState.AddModelError("ImageFile", "Security Block: This image is globally blacklisted for fraud/illegal content.");
                    return View(model);
                }

                ImageScanResult scanResult = await _aiClient.ScanImageForForgeryAsync(imageBytes);
                if (scanResult.IsForgeryDetected)
                {
                    ModelState.AddModelError("ImageFile", $"AI Security Block: {scanResult.ForgeryReason}");
                    return View(model);
                }

                // AI Passed, update hash and save file
                product.ImageHash = imageHash;
                string fileName = $"{product.ProductID}.jpg";
                string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(uploadPath)!);
                await System.IO.File.WriteAllBytesAsync(uploadPath, imageBytes);
            }

            decimal oldPrice = product.Price;
            int oldStock = product.StockLevel;

            // Update details
            product.Title = model.Title;
            product.Price = model.Price;
            product.StockLevel = model.StockLevel;
            product.WeightKg = model.WeightKg;
            product.Description = model.Description;
            product.CategoryID = categoryId;

            // Notify wishlisted buyers of price changes or restocks
            var wishlistedBuyerIds = await _context.Wishlists
                .Where(w => w.ProductID == product.ProductID)
                .Select(w => w.BuyerID)
                .Distinct()
                .ToListAsync();

            if (wishlistedBuyerIds.Any())
            {
                // Price change alerts
                if (model.Price < oldPrice)
                {
                    foreach (var buyerId in wishlistedBuyerIds)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            UserID = buyerId,
                            Type = "Price Drop Alert",
                            Content = $"Good news! '{product.Title}' in your wishlist dropped in price from RM {oldPrice:0.00} to RM {model.Price:0.00}."
                        });
                    }
                }
                else if (model.Price > oldPrice)
                {
                    foreach (var buyerId in wishlistedBuyerIds)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            UserID = buyerId,
                            Type = "Price Change Alert",
                            Content = $"Notice: '{product.Title}' in your wishlist has increased in price from RM {oldPrice:0.00} to RM {model.Price:0.00}."
                        });
                    }
                }

                // Restock alert (0 -> >0)
                if (oldStock == 0 && model.StockLevel > 0)
                {
                    foreach (var buyerId in wishlistedBuyerIds)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            UserID = buyerId,
                            Type = "Restock Alert",
                            Content = $"Back in stock! '{product.Title}' in your wishlist is now available with {model.StockLevel} units."
                        });
                    }
                }
                // Low stock alert (>5 -> <=5 and >0)
                else if (oldStock > 5 && model.StockLevel <= 5 && model.StockLevel > 0)
                {
                    foreach (var buyerId in wishlistedBuyerIds)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            NotificationID = "NOT-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                            UserID = buyerId,
                            Type = "Low Stock Alert",
                            Content = $"Hurry! '{product.Title}' in your wishlist is running low on stock (only {model.StockLevel} left)."
                        });
                    }
                }
            }

            var auditLog = new AuditLog
            {
                LogID = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                UserID = product.SellerID,
                Action = $"Edited product {product.Title} ({product.ProductID})",
                IP_Address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                Timestamp = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Product details updated successfully!";
            return RedirectToAction("MyProducts", "Seller");
        }
    }
}