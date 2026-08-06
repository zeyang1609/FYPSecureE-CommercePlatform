using System.Collections.Generic;
using System.Threading.Tasks;
using FYP.Models.Entities;
using FYP.Models.ViewModels;

namespace FYP.Services
{
    public interface IShippingService
    {
        Task<(decimal OriginalFee, decimal FinalFee, Courier AssignedCourier)> CalculateAndAssignShippingAsync(IEnumerable<(string ProductID, int Quantity)> items, decimal subtotal, string addressString, string selectedCourierId = null);
        Task<List<ShippingOptionViewModel>> GetShippingOptionsAsync(IEnumerable<(string ProductID, int Quantity)> items, decimal subtotal, string addressString);
    }
}
