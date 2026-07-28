using FYP.Models.Entities;
using System.Collections.Generic;

namespace FYP.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalAlerts { get; set; }
        public int HighRiskCount { get; set; }
        public int ActiveUsersCount { get; set; }
        public List<FraudAlert> RecentFraudAlerts { get; set; } = new();
        public List<AuditLog> RecentSystemLogs { get; set; } = new();
    }

    public class SellerDashboardViewModel
    {
        public string SellerID { get; set; } = string.Empty;
        public int TotalProducts { get; set; }
        public int LowStockCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<Product> Products { get; set; } = new();
        public List<OrderItem> RecentSales { get; set; } = new();
    }

    public class BuyerDashboardViewModel
    {
        public string BuyerID { get; set; } = string.Empty;
        public List<Order> RecentOrders { get; set; } = new();
        public List<Notification> Notifications { get; set; } = new();
    }
}