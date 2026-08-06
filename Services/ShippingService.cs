using FYP.Data;
using FYP.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FYP.Services
{
    public class ShippingService : IShippingService
    {
        private readonly ApplicationDbContext _context;

        public ShippingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(decimal OriginalFee, decimal FinalFee, Courier AssignedCourier)> CalculateAndAssignShippingAsync(System.Collections.Generic.IEnumerable<(string ProductID, int Quantity)> items, decimal subtotal, string addressString, string selectedCourierId = null)
        {
            if (items == null || !items.Any())
            {
                throw new ArgumentException("Invalid items provided for shipping calculation.");
            }

            // 1. Calculate Total Weight
            decimal totalWeightKg = 0;
            var productIds = items.Select(i => i.ProductID).ToList();
            var products = await _context.Products.Where(p => productIds.Contains(p.ProductID)).ToListAsync();
            
            foreach (var item in items)
            {
                var product = products.FirstOrDefault(p => p.ProductID == item.ProductID);
                if (product != null)
                {
                    totalWeightKg += product.WeightKg * item.Quantity;
                }
            }

            if (totalWeightKg == 0) totalWeightKg = 1.00m; // Default minimum weight

            // 2. Determine Zone
            string zoneRegion = DetermineZone(addressString);

            // 3. Find applicable rules for the zone
            var rules = await _context.DeliveryPricingRules
                .Include(r => r.Courier)
                .Where(r => r.ZoneRegion == zoneRegion && r.Courier.IsActive)
                .ToListAsync();

            if (!rules.Any())
            {
                // Fallback to a default fee if no rules are found
                return (10.00m, 10.00m, null);
            }

            // 4. Calculate fee for each courier and find the cheapest (Shopee auto-assign algorithm)
            decimal cheapestFee = decimal.MaxValue;
            Courier bestCourier = null;

            if (!string.IsNullOrEmpty(selectedCourierId))
            {
                var selectedRule = rules.FirstOrDefault(r => r.CourierID == selectedCourierId);
                if (selectedRule != null)
                {
                    cheapestFee = CalculateFee(totalWeightKg, selectedRule);
                    bestCourier = selectedRule.Courier;
                }
            }

            if (bestCourier == null)
            {
                foreach (var rule in rules)
                {
                    decimal fee = CalculateFee(totalWeightKg, rule);
                    if (fee < cheapestFee)
                    {
                        cheapestFee = fee;
                        bestCourier = rule.Courier;
                    }
                }
            }

            // 5. Tiered Discount Logic
            decimal discount = 0.00m;

            if (subtotal >= 600.00m && totalWeightKg <= 5.00m)
            {
                // Most profitable tier: High spend, low weight
                discount = cheapestFee; 
            }
            else if (subtotal >= 500.00m && totalWeightKg >= 10.00m)
            {
                // Heavy bulk order tier: Protect margins, give fixed discount
                discount = 10.00m;
            }
            else if (subtotal >= 100.00m)
            {
                discount = 5.00m;
            }
            else if (subtotal >= 50.00m)
            {
                discount = 3.00m;
            }

            // Ensure the fee never drops below zero
            decimal finalFee = Math.Max(0.00m, cheapestFee - discount);

            return (cheapestFee, finalFee, bestCourier);
        }

        public async Task<System.Collections.Generic.List<FYP.Models.ViewModels.ShippingOptionViewModel>> GetShippingOptionsAsync(System.Collections.Generic.IEnumerable<(string ProductID, int Quantity)> items, decimal subtotal, string addressString)
        {
            var options = new System.Collections.Generic.List<FYP.Models.ViewModels.ShippingOptionViewModel>();
            if (items == null || !items.Any()) return options;

            // 1. Calculate Total Weight
            decimal totalWeightKg = 0;
            var productIds = items.Select(i => i.ProductID).ToList();
            var products = await _context.Products.Where(p => productIds.Contains(p.ProductID)).ToListAsync();
            
            foreach (var item in items)
            {
                var product = products.FirstOrDefault(p => p.ProductID == item.ProductID);
                if (product != null)
                {
                    totalWeightKg += product.WeightKg * item.Quantity;
                }
            }

            if (totalWeightKg == 0) totalWeightKg = 1.00m; // Default minimum weight

            // 2. Determine Zone
            string zoneRegion = DetermineZone(addressString);

            // 3. Find applicable rules for the zone
            var rules = await _context.DeliveryPricingRules
                .Include(r => r.Courier)
                .Where(r => r.ZoneRegion == zoneRegion && r.Courier.IsActive)
                .ToListAsync();

            // 4. Calculate Discount
            decimal discount = 0.00m;
            decimal cheapestFeeOverall = rules.Any() ? rules.Min(r => CalculateFee(totalWeightKg, r)) : 10.00m;

            if (subtotal >= 600.00m && totalWeightKg <= 5.00m)
            {
                discount = cheapestFeeOverall; 
            }
            else if (subtotal >= 500.00m && totalWeightKg >= 10.00m)
            {
                discount = 10.00m;
            }
            else if (subtotal >= 100.00m)
            {
                discount = 5.00m;
            }
            else if (subtotal >= 50.00m)
            {
                discount = 3.00m;
            }

            foreach (var rule in rules)
            {
                decimal originalFee = CalculateFee(totalWeightKg, rule);
                decimal finalFee = Math.Max(0.00m, originalFee - discount);
                
                options.Add(new FYP.Models.ViewModels.ShippingOptionViewModel
                {
                    CourierID = rule.CourierID,
                    CourierName = rule.Courier.Name,
                    OriginalFee = originalFee,
                    FinalFee = finalFee
                });
            }

            return options;
        }

        private decimal CalculateFee(decimal totalWeight, DeliveryPricingRule rule)
        {
            if (totalWeight <= rule.BaseWeightKg)
            {
                return rule.BasePrice;
            }

            decimal extraWeight = totalWeight - rule.BaseWeightKg;
            decimal increments = Math.Ceiling(extraWeight / rule.IncrementalWeightKg);
            return rule.BasePrice + (increments * rule.IncrementalPrice);
        }

        private string DetermineZone(string stateArea)
        {
            stateArea = stateArea?.ToLower() ?? "";
            
            // Basic East Malaysia detection
            if (stateArea.Contains("sabah") || stateArea.Contains("sarawak") || stateArea.Contains("labuan"))
            {
                return "East Malaysia";
            }

            return "West Malaysia";
        }
    }
}
