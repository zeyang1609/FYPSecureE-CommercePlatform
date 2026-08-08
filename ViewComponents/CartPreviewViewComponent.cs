using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FYP.Data;
using FYP.Models.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;

namespace FYP.ViewComponents
{
    public class CartPreviewViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public CartPreviewViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return View(new CartPreviewViewModel());
            }

            var userId = ((ClaimsIdentity)User.Identity).FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return View(new CartPreviewViewModel());
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.UserID == userId);

            if (cart == null || !cart.Items.Any())
            {
                return View(new CartPreviewViewModel());
            }

            var totalItems = cart.Items.Sum(i => i.Quantity);
            var uniqueItemsCount = cart.Items.Count;

            // Fetch the 5 most recently added items (assuming higher CartItemID means more recent)
            var recentItems = cart.Items
                .OrderByDescending(i => i.CartItemID)
                .Take(5)
                .Select(i => new CartPreviewItemViewModel
                {
                    ProductID = i.ProductID,
                    Title = i.Product?.Title ?? "Unknown Product",
                    Price = i.Product?.Price ?? 0
                })
                .ToList();

            var viewModel = new CartPreviewViewModel
            {
                TotalItems = totalItems,
                UniqueItemsCount = uniqueItemsCount,
                RecentItems = recentItems
            };

            return View(viewModel);
        }
    }
}
