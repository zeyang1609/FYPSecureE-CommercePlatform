using FYP.Models.Entities;
using System.Collections.Generic;

namespace FYP.Models.ViewModels
{
    /// <summary>
    /// ViewModel for the full Seller Shop Profile page.
    /// </summary>
    public class ShopProfileViewModel
    {
        public User Seller { get; set; }
        public int TotalProducts { get; set; }
        public int TotalSales { get; set; }
        public decimal OverallRating { get; set; }
        public List<Category> SellerCategories { get; set; } = new();

        /// <summary>
        /// Category Name → Top products for the "Home" tab sections.
        /// </summary>
        public Dictionary<string, List<Product>> CategoryProducts { get; set; } = new();
    }

    /// <summary>
    /// ViewModel for the AJAX-driven product grid partial in the "All Products" tab.
    /// </summary>
    public class ShopProductGridViewModel
    {
        public string SellerId { get; set; }
        public List<Product> Products { get; set; } = new();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string SelectedCategory { get; set; }
        public string SortBy { get; set; }
    }
}
