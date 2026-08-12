using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using FYP.Models.Entities;
using FYP.Models.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Controllers
{
    public class HelpController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HelpController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Help
        public async Task<IActionResult> Index()
        {
            var categories = await _context.HelpCategories
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            var popularArticles = await _context.HelpArticles
                .Include(a => a.Category)
                .Where(a => a.IsPopular)
                .OrderBy(a => a.Title)
                .Take(5)
                .ToListAsync();

            var viewModel = new HelpCenterIndexViewModel
            {
                Categories = categories,
                PopularArticles = popularArticles
            };

            return View(viewModel);
        }

        // GET: /Help/Category/1
        public async Task<IActionResult> Category(int id)
        {
            var category = await _context.HelpCategories
                .FirstOrDefaultAsync(c => c.HelpCategoryID == id);

            if (category == null) return NotFound();

            var articles = await _context.HelpArticles
                .Where(a => a.HelpCategoryID == id)
                .OrderBy(a => a.Title)
                .ToListAsync();

            var viewModel = new HelpCategoryViewModel
            {
                Category = category,
                Articles = articles
            };

            return View(viewModel);
        }

        // GET: /Help/Article/1
        public async Task<IActionResult> Article(int id)
        {
            var article = await _context.HelpArticles
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.HelpArticleID == id);

            if (article == null) return NotFound();

            return View(article);
        }

        // GET: /Help/Search?query=refund
        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return RedirectToAction("Index");
            }

            var normalizedQuery = query.Trim().ToLower();

            var results = await _context.HelpArticles
                .Include(a => a.Category)
                .Where(a => a.Title.ToLower().Contains(normalizedQuery) ||
                            a.Content.ToLower().Contains(normalizedQuery))
                .OrderBy(a => a.Title)
                .ToListAsync();

            var viewModel = new HelpSearchResultViewModel
            {
                Query = query,
                Results = results
            };

            return View("SearchResults", viewModel);
        }
    }
}
