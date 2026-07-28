using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace FYP.Models.ViewModels
{
    public class ProductUploadViewModel
    {
        [Required(ErrorMessage = "Product title is required.")]
        [StringLength(150, ErrorMessage = "Title cannot exceed 150 characters.")]
        [Display(Name = "Product Title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Price is required.")]
        [Range(0.01, 100000.00, ErrorMessage = "Price must be between $0.01 and $100,000.00")]
        [DataType(DataType.Currency)]
        [Display(Name = "Price ($)")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Initial stock level is required.")]
        [Range(1, 10000, ErrorMessage = "Stock must be at least 1 unit.")]
        [Display(Name = "Stock Quantity")]
        public int StockLevel { get; set; }

        [Required(ErrorMessage = "Please upload a product image for AI verification.")]
        [Display(Name = "Product Image (JPG/PNG)")]
        public IFormFile? ImageFile { get; set; }
    }
}