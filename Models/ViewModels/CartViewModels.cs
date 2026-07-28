using System.Collections.Generic;

namespace FYP.Models.ViewModels
{
    public class CartItemViewModel
    {
        public string ProductID { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public int MaxStock { get; set; }
        public decimal Subtotal => Price * Quantity;
    }

    public class CartViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new List<CartItemViewModel>();
        public decimal GrandTotal { get; set; }
    }
}