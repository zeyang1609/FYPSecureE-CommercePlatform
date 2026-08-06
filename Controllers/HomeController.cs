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

        // GET: /Home/Index
        [HttpGet]
        public async Task<IActionResult> Index(string searchQuery, string categoryId)
        {
            // 1. Fetch dynamic categories for the top grid
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.SelectedCategory = categoryId;

            // 2. Fetch products[cite: 10]
            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Where(p => p.StockLevel > 0)
                .AsQueryable();

            // Filter by Search Query
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                productsQuery = productsQuery.Where(p => p.Title.Contains(searchQuery)); 
        ViewBag.SearchQuery = searchQuery;
            }

            // Filter by Category Click
            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                productsQuery = productsQuery.Where(p => p.CategoryID == categoryId);
            }

            var products = await productsQuery
                .OrderByDescending(p => p.ProductID)
                .ToListAsync();

            return View(products);
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
    }
}