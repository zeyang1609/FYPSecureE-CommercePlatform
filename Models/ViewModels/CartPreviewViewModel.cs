using System.Collections.Generic;

namespace FYP.Models.ViewModels
{
    public class CartPreviewItemViewModel
    {
        public string ProductID { get; set; }
        public string Title { get; set; }
        public decimal Price { get; set; }
    }

    public class CartPreviewViewModel
    {
        public int TotalItems { get; set; }
        public int UniqueItemsCount { get; set; }
        public List<CartPreviewItemViewModel> RecentItems { get; set; } = new List<CartPreviewItemViewModel>();
    }
}
