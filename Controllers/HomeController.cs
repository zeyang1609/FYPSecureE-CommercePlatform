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

            return View(product);
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}