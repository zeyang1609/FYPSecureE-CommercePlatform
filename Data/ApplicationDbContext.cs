using FYP.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace FYP.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Register all database tables
        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Refund> Refunds { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<FraudAlert> FraudAlerts { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<SavedBankCard> SavedBankCards { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Courier> Couriers { get; set; }
        public DbSet<DeliveryPricingRule> DeliveryPricingRules { get; set; }
        public DbSet<Delivery> Deliveries { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<DeviceLockout> DeviceLockouts { get; set; }
        public DbSet<UserDevice> UserDevices { get; set; }

        public DbSet<BlacklistedImageHash> BlacklistedImageHashes { get; set; }
        public DbSet<IpFilter> IpFilters { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Configure ChatMessage Relationships (Dual Foreign Keys to User Table)
            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderID)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading deletes to preserve forensic data

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverID)
                .OnDelete(DeleteBehavior.Restrict);

            // 2. Enforce Cryptographic Uniqueness Constraints
            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.IdempotencyKey)
                .IsUnique(); // Mathematically blocks accidental double-charges at the database level

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<DeviceLockout>()
                .HasIndex(dl => dl.DeviceIdentifier)
                .IsUnique();

            // 3. Configure Precise Decimal Precisions for Financial Integrity
            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.UnitPrice)
                .HasPrecision(10, 2);

            modelBuilder.Entity<Refund>()
                .Property(r => r.RefundAmount)
                .HasPrecision(10, 2);

            modelBuilder.Entity<FraudAlert>()
                .Property(fa => fa.RiskScore)
                .HasPrecision(5, 2);
            
            modelBuilder.Entity<Category>()
                .HasIndex(c => c.Name)
                .IsUnique(); 

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Address>()
                .HasOne(a => a.User)
                .WithMany(u => u.Addresses)
                .HasForeignKey(a => a.UserID)
                .OnDelete(DeleteBehavior.Cascade);
                
            // 4. Configure Cart Relationships
            modelBuilder.Entity<Cart>()
                .HasOne(c => c.User)
                .WithOne(u => u.Cart)
                .HasForeignKey<Cart>(c => c.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserDevice>()
                .HasOne(ud => ud.User)
                .WithMany(u => u.UserDevices)
                .HasForeignKey(ud => ud.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Cart)
                .WithMany(c => c.Items)
                .HasForeignKey(ci => ci.CartID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Product)
                .WithMany()
                .HasForeignKey(ci => ci.ProductID)
                .OnDelete(DeleteBehavior.Cascade);

            // 5. Configure Review Relationships (Prevent multiple cascade paths)
            modelBuilder.Entity<Review>()
                .HasOne(r => r.Order)
                .WithMany(o => o.Reviews)
                .HasForeignKey(r => r.OrderID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Buyer)
                .WithMany()
                .HasForeignKey(r => r.BuyerID)
                .OnDelete(DeleteBehavior.Restrict);

            // ==========================================
            // SEED DATA: Realistic eCommerce Environment
            // ==========================================
            
            // 1. Seed Seller Account
            var demoSellerId = "seller_demo_1";
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    UserID = demoSellerId,
                    Role = "Seller",
                    Email = "demo_seller@secureplatform.com",
                    PasswordHash = "SEED_NO_LOGIN", // Not meant for real login, just relationship
                    DeviceHash = "SEED",
                    MFA_Enabled = true,
                    Name = "Official Tech Store",
                    PhoneNumber = "0123456789"
                }
            );

            // 1.5. Seed Admin Account
            var demoAdminId = "admin_demo_1";
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    UserID = demoAdminId,
                    Role = "Admin",
                    Email = "demo_admin@secureplatform.com",
                    PasswordHash = "SEED_NO_LOGIN",
                    DeviceHash = "SEED",
                    MFA_Enabled = true,
                    Name = "System Administrator",
                    PhoneNumber = "0123456789"
                },
                new User
                {
                    UserID = "SYSTEM",
                    Role = "Admin",
                    Email = "system@secureplatform.com",
                    PasswordHash = "SEED_NO_LOGIN",
                    DeviceHash = "SEED",
                    MFA_Enabled = false,
                    Name = "SYSTEM",
                    PhoneNumber = "0000000000"
                }
            );

            // 2. Seed Category
            var techCategoryId = "cat_tech_1";
            modelBuilder.Entity<Category>().HasData(
                new Category
                {
                    CategoryID = techCategoryId,
                    Name = "Tech & Gadgets",
                    Description = "Latest gadgets, electronics, and smart devices.",
                    IconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"><rect x=\"4\" y=\"4\" width=\"16\" height=\"16\" rx=\"2\"></rect><rect x=\"9\" y=\"9\" width=\"6\" height=\"6\"></rect><line x1=\"9\" y1=\"1\" x2=\"9\" y2=\"4\"></line><line x1=\"15\" y1=\"1\" x2=\"15\" y2=\"4\"></line><line x1=\"9\" y1=\"20\" x2=\"9\" y2=\"23\"></line><line x1=\"15\" y1=\"20\" x2=\"15\" y2=\"23\"></line><line x1=\"20\" y1=\"9\" x2=\"23\" y2=\"9\"></line><line x1=\"20\" y1=\"14\" x2=\"23\" y2=\"14\"></line><line x1=\"1\" y1=\"9\" x2=\"4\" y2=\"9\"></line><line x1=\"1\" y1=\"14\" x2=\"4\" y2=\"14\"></line></svg>"
                }
            );

            // 3. Seed 6 Products
            modelBuilder.Entity<Product>().HasData(
                new Product { 
                    ProductID = "PROD_001", SellerID = demoSellerId, CategoryID = techCategoryId,
                    Title = "Wireless Noise-Cancelling Headphones Pro", Price = 899.00m, StockLevel = 45, TotalSales = 1250, AverageRating = 4.9m, ReviewCount = 342,
                    ImageHash = "SEED", Description = "Experience pure sound with industry-leading active noise cancellation. Features 30-hour battery life, touch sensor controls, and speak-to-chat technology."
                },
                new Product { 
                    ProductID = "PROD_002", SellerID = demoSellerId, CategoryID = techCategoryId,
                    Title = "Smart Fitness Watch Series 7", Price = 1299.00m, StockLevel = 120, TotalSales = 3400, AverageRating = 4.8m, ReviewCount = 890,
                    ImageHash = "SEED", Description = "Advanced health monitoring right on your wrist. Measure your blood oxygen level, take an ECG anytime, and track your daily activity."
                },
                new Product { 
                    ProductID = "PROD_003", SellerID = demoSellerId, CategoryID = techCategoryId,
                    Title = "Mechanical Gaming Keyboard RGB", Price = 450.00m, StockLevel = 3, TotalSales = 850, AverageRating = 4.7m, ReviewCount = 156,
                    ImageHash = "SEED", Description = "Tactile mechanical switches for ultimate gaming performance. Features customizable per-key RGB lighting and an aircraft-grade aluminum alloy frame."
                },
                new Product { 
                    ProductID = "PROD_004", SellerID = demoSellerId, CategoryID = techCategoryId,
                    Title = "Ultra-Light Wireless Esports Mouse", Price = 320.00m, StockLevel = 80, TotalSales = 2100, AverageRating = 4.9m, ReviewCount = 512,
                    ImageHash = "SEED", Description = "Weighing only 63 grams, this mouse is designed for professional esports. Features a 25K DPI sensor and zero-additive PTFE feet for smooth gliding."
                },
                new Product { 
                    ProductID = "PROD_005", SellerID = demoSellerId, CategoryID = techCategoryId,
                    Title = "27-inch 4K IPS Creator Monitor", Price = 1850.00m, StockLevel = 15, TotalSales = 420, AverageRating = 4.6m, ReviewCount = 89,
                    ImageHash = "SEED", Description = "Stunning 4K resolution with 99% sRGB color accuracy. Factory calibrated for creators who demand perfect color representation and crisp text."
                },
                new Product { 
                    ProductID = "PROD_006", SellerID = demoSellerId, CategoryID = techCategoryId,
                    Title = "20,000mAh PD Fast Charge Power Bank", Price = 150.00m, StockLevel = 250, TotalSales = 5600, AverageRating = 4.9m, ReviewCount = 1240,
                    ImageHash = "SEED", Description = "Never run out of battery again. This high-capacity power bank supports 65W Power Delivery, allowing you to fast-charge your smartphone, tablet, and even your laptop on the go."
                }
            );

            // 4. Seed Couriers
            var jntId = "COUR_JNT";
            var poslajuId = "COUR_POS";
            modelBuilder.Entity<Courier>().HasData(
                new Courier { CourierID = jntId, Name = "J&T Express", TrackingUrlTemplate = "https://www.jtexpress.my/tracking/{0}", IsActive = true },
                new Courier { CourierID = poslajuId, Name = "PosLaju", TrackingUrlTemplate = "https://track.pos.com.my/tracking/{0}", IsActive = true }
            );

            // 5. Seed Delivery Pricing Rules
            modelBuilder.Entity<DeliveryPricingRule>().HasData(
                // J&T West Malaysia
                new DeliveryPricingRule { DeliveryRuleID = "RULE_JNT_WM", CourierID = jntId, ZoneRegion = "West Malaysia", BaseWeightKg = 1.00m, BasePrice = 4.90m, IncrementalWeightKg = 0.50m, IncrementalPrice = 1.00m },
                // J&T East Malaysia
                new DeliveryPricingRule { DeliveryRuleID = "RULE_JNT_EM", CourierID = jntId, ZoneRegion = "East Malaysia", BaseWeightKg = 1.00m, BasePrice = 12.00m, IncrementalWeightKg = 0.50m, IncrementalPrice = 2.50m },
                // PosLaju West Malaysia
                new DeliveryPricingRule { DeliveryRuleID = "RULE_POS_WM", CourierID = poslajuId, ZoneRegion = "West Malaysia", BaseWeightKg = 2.00m, BasePrice = 6.00m, IncrementalWeightKg = 1.00m, IncrementalPrice = 1.50m },
                // PosLaju East Malaysia
                new DeliveryPricingRule { DeliveryRuleID = "RULE_POS_EM", CourierID = poslajuId, ZoneRegion = "East Malaysia", BaseWeightKg = 1.00m, BasePrice = 10.00m, IncrementalWeightKg = 0.50m, IncrementalPrice = 3.00m }
            );
        }
    }
}