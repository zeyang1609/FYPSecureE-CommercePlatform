using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using FYP.Models.Entities;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<List<Product>> GetProductsBySearchAsync(string searchQuery, string categoryId)
        {
            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Where(p => p.StockLevel > 0)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                string normalizedQuery = searchQuery.Replace(" ", "").ToLower();
                var allProducts = await productsQuery.Select(p => new { p.ProductID, p.Title }).ToListAsync();
                
                var matchedIds = allProducts.Where(p => {
                    if (string.IsNullOrEmpty(p.Title)) return false;
                    string normalizedTitle = p.Title.Replace(" ", "").ToLower();
                    
                    if (normalizedTitle.Contains(normalizedQuery)) return true;
                    if (normalizedQuery.Contains(normalizedTitle)) return true;
                    
                    int distance = MinDistanceSubstring(normalizedTitle, normalizedQuery);
                    double similarity = 1.0 - (double)distance / normalizedQuery.Length;
                    
                    return similarity >= 0.7; // 70% similarity threshold for typos
                }).Select(p => p.ProductID).ToList();
                
                productsQuery = productsQuery.Where(p => matchedIds.Contains(p.ProductID));
            }

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                productsQuery = productsQuery.Where(p => p.CategoryID == categoryId);
            }

            return await productsQuery.OrderByDescending(p => p.ProductID).ToListAsync();
        }

        // GET: /Home/Index
        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Index(string searchQuery, string categoryId)
        {
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.SelectedCategory = categoryId;
            ViewBag.SearchQuery = searchQuery;

            var products = await GetProductsBySearchAsync(searchQuery, categoryId);
            return View(products);
        }

        // GET: /Home/SearchAjax
        [HttpGet]
        public async Task<IActionResult> SearchAjax(string searchQuery, string categoryId)
        {
            ViewBag.SearchQuery = searchQuery;
            var products = await GetProductsBySearchAsync(searchQuery, categoryId);
            return PartialView("_ProductGrid", products);
        }

        // GET: /Home/SearchAutocomplete
        [HttpGet]
        public async Task<IActionResult> SearchAutocomplete(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Json(new List<object>());
            }
            
            var products = await GetProductsBySearchAsync(query, null);
            var suggestions = products.Take(6).Select(p => new {
                title = p.Title,
                category = p.Category?.Name ?? "Uncategorized",
                url = $"/Home/ProductDetails/{p.ProductID}"
            }).ToList();
            
            return Json(suggestions);
        }

        // GET: /Home/ProductDetails/PRD-1234567890AB
        [HttpGet]
        public async Task<IActionResult> ProductDetails(string id)
        {
            var product = await _context.Products
                .Include(p => p.Seller)
                .FirstOrDefaultAsync(p => p.ProductID == id);

            if (product == null)
            {
                return NotFound();
            }

            // Sync Total Sales (quantity from Completed or Delivered orders)
            var totalSales = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.ProductID == id && (oi.Order.Status == "Completed" || oi.Order.Status == "Delivered") && !oi.Order.Refunds.Any())
                .SumAsync(oi => oi.Quantity);

            // Fetch all actual reviews
            var reviews = await _context.Reviews
                .Include(r => r.Buyer)
                .Where(r => r.ProductID == id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            decimal averageRating = reviews.Any() ? (decimal)reviews.Average(r => r.Rating) : 0m;
            int reviewCount = reviews.Count;

            // Sync the entity so the rest of the system sees accurate data
            if (product.TotalSales != totalSales || product.AverageRating != averageRating || product.ReviewCount != reviewCount)
            {
                product.TotalSales = totalSales;
                product.AverageRating = averageRating;
                product.ReviewCount = reviewCount;
                await _context.SaveChangesAsync();
            }

            ViewBag.Reviews = reviews;

            return View(product);
        }

        // GET: /Home/ShopProfile/{id}
        [HttpGet]
        public async Task<IActionResult> ShopProfile(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var seller = await _context.Users.FirstOrDefaultAsync(u => u.UserID == id);
            if (seller == null) return NotFound();

            // Fetch all products belonging to this seller (with stock > 0)
            var products = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.SellerID == id && p.StockLevel > 0)
                .ToListAsync();

            // Aggregate stats
            int totalProducts = products.Count;
            int totalSales = products.Sum(p => p.TotalSales);
            decimal overallRating = products.Any(p => p.ReviewCount > 0)
                ? products.Where(p => p.ReviewCount > 0).Average(p => p.AverageRating)
                : 0m;

            // Group products by category (for "Home" tab sections)
            var sellerCategories = products
                .Where(p => p.Category != null)
                .Select(p => p.Category)
                .DistinctBy(c => c.CategoryID)
                .OrderBy(c => c.Name)
                .ToList();

            var categoryProducts = new Dictionary<string, List<Product>>();
            foreach (var cat in sellerCategories)
            {
                categoryProducts[cat.Name] = products
                    .Where(p => p.CategoryID == cat.CategoryID)
                    .OrderByDescending(p => p.TotalSales)
                    .Take(5)
                    .ToList();
            }

            var vm = new FYP.Models.ViewModels.ShopProfileViewModel
            {
                Seller = seller,
                TotalProducts = totalProducts,
                TotalSales = totalSales,
                OverallRating = overallRating,
                SellerCategories = sellerCategories,
                CategoryProducts = categoryProducts
            };

            return View(vm);
        }

        // GET: /Home/ShopProducts (AJAX partial for "All Products" tab)
        [HttpGet]
        public async Task<IActionResult> ShopProducts(string sellerId, string categoryId, string sort = "popular", int page = 1)
        {
            const int pageSize = 20;

            var query = _context.Products
                .Include(p => p.Category)
                .Where(p => p.SellerID == sellerId && p.StockLevel > 0);

            // Filter by category
            if (!string.IsNullOrEmpty(categoryId))
                query = query.Where(p => p.CategoryID == categoryId);

            // Apply sorting
            query = sort switch
            {
                "latest" => query.OrderByDescending(p => p.ProductID),
                "topsales" => query.OrderByDescending(p => p.TotalSales),
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                _ => query.OrderByDescending(p => p.TotalSales) // "popular" default
            };

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            page = Math.Clamp(page, 1, Math.Max(totalPages, 1));

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new FYP.Models.ViewModels.ShopProductGridViewModel
            {
                SellerId = sellerId,
                Products = products,
                CurrentPage = page,
                TotalPages = totalPages,
                SelectedCategory = categoryId,
                SortBy = sort
            };

            return PartialView("_ShopProductGrid", vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> SeedSeller([FromServices] FYP.Data.ApplicationDbContext context)
        {
            var existing = await context.Users.FirstOrDefaultAsync(u => u.Email == "seller@test.com");
            if (existing == null)
            {
                var newSeller = new FYP.Models.Entities.User
                {
                    UserID = "USR-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper(),
                    Name = "Test Seller",
                    Email = "seller@test.com",
                    PasswordHash = FYP.Security.Argon2idHasher.HashPassword("Password@123"),
                    Role = "Seller",
                    DeviceHash = "",
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(newSeller);
                await context.SaveChangesAsync();
                return Content("Created Seller account! Email: seller@test.com | Password: Password@123");
            }
            return Content("Seller account already exists! Email: seller@test.com | Password: Password@123");
        }

        [HttpGet]
        public async Task<IActionResult> UnlockDemoSeller([FromServices] FYP.Data.ApplicationDbContext context)
        {
            var demoSeller = await context.Users.FirstOrDefaultAsync(u => u.Email == "demo_seller@secureplatform.com");
            if (demoSeller != null)
            {
                demoSeller.PasswordHash = FYP.Security.Argon2idHasher.HashPassword("Password@123");
                demoSeller.MFA_Enabled = false; // Disable MFA for easy login
                await context.SaveChangesAsync();
                return Content("Unlocked! Email: demo_seller@secureplatform.com | Password: Password@123");
            }
            return Content("Demo seller not found.");
        }

        private int MinDistanceSubstring(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return 0;
            if (string.IsNullOrEmpty(text)) return pattern.Length;

            int[] v0 = new int[pattern.Length + 1];
            int[] v1 = new int[pattern.Length + 1];

            for (int i = 0; i < v0.Length; i++) v0[i] = i;

            int minDistance = pattern.Length;

            for (int i = 0; i < text.Length; i++)
            {
                v1[0] = 0; 
                
                for (int j = 0; j < pattern.Length; j++)
                {
                    int cost = (text[i] == pattern[j]) ? 0 : 1;
                    v1[j + 1] = System.Math.Min(v1[j] + 1, System.Math.Min(v0[j + 1] + 1, v0[j] + cost));
                }
                for (int j = 0; j < v0.Length; j++) v0[j] = v1[j];
                
                if (v1[pattern.Length] < minDistance)
                {
                    minDistance = v1[pattern.Length];
                }
            }
            return minDistance;
        }
    }
}