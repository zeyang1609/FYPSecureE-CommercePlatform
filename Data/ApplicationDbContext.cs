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
        public DbSet<HelpCategory> HelpCategories { get; set; }
        public DbSet<HelpArticle> HelpArticles { get; set; }

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

            // 6. Configure HelpArticle -> HelpCategory Relationship
            modelBuilder.Entity<HelpArticle>()
                .HasOne(a => a.Category)
                .WithMany(c => c.Articles)
                .HasForeignKey(a => a.HelpCategoryID)
                .OnDelete(DeleteBehavior.Cascade);

            // ==========================================
            // SEED DATA: Realistic eCommerce Environment
            // ==================================
            
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

            // ==========================================
            // 6. SEED HELP CENTER DATA
            // ==========================================
            var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            modelBuilder.Entity<HelpCategory>().HasData(
                new HelpCategory { HelpCategoryID = 1, Name = "Account & Security", IconClass = "shield-lock", DisplayOrder = 1 },
                new HelpCategory { HelpCategoryID = 2, Name = "Payments", IconClass = "credit-card", DisplayOrder = 2 },
                new HelpCategory { HelpCategoryID = 3, Name = "Orders & Shipping", IconClass = "truck", DisplayOrder = 3 },
                new HelpCategory { HelpCategoryID = 4, Name = "Returns & Refunds", IconClass = "arrow-return-left", DisplayOrder = 4 },
                new HelpCategory { HelpCategoryID = 5, Name = "Policies & General", IconClass = "file-text", DisplayOrder = 5 }
            );

            modelBuilder.Entity<HelpArticle>().HasData(
                // Account & Security
                new HelpArticle { HelpArticleID = 1, HelpCategoryID = 1, Title = "How to create an account?", IsPopular = false, CreatedAt = seedDate,
                    Content = "<h4>Creating Your SecurePlatform Account</h4><p>Follow these simple steps to get started:</p><ol><li>Click the <strong>Sign Up</strong> button at the top right corner of the homepage.</li><li>Enter your email address and create a strong password. Your password must contain at least 8 characters, including uppercase, lowercase, numbers, and special characters.</li><li>Verify your email address by entering the OTP (One-Time Password) sent to your inbox.</li><li>Complete your profile by adding your name and phone number.</li></ol><p>Once verified, you can start browsing and purchasing products immediately!</p>" },
                new HelpArticle { HelpArticleID = 2, HelpCategoryID = 1, Title = "Why did I not receive my OTP?", IsPopular = true, CreatedAt = seedDate,
                    Content = "<h4>Troubleshooting OTP Issues</h4><p>If you haven't received your OTP, try the following:</p><ul><li><strong>Check your Spam/Junk folder</strong> — OTP emails sometimes get filtered by email providers.</li><li><strong>Wait a moment</strong> — OTPs can take up to 2 minutes to arrive depending on your email provider.</li><li><strong>Verify your email address</strong> — Make sure the email address you entered is correct with no typos.</li><li><strong>Request a new OTP</strong> — Click the 'Resend OTP' button on the verification page.</li></ul><p>If you continue to experience issues, please contact our support team via the live chat.</p>" },
                new HelpArticle { HelpArticleID = 3, HelpCategoryID = 1, Title = "How to reset my password?", IsPopular = false, CreatedAt = seedDate,
                    Content = "<h4>Resetting Your Password</h4><p>If you've forgotten your password, follow these steps:</p><ol><li>Go to the <strong>Login</strong> page and click <em>'Forgot Password?'</em>.</li><li>Enter the email address associated with your account.</li><li>You will receive an OTP on your email. Enter the OTP to verify your identity.</li><li>Create a new password that meets our security requirements (minimum 8 characters with uppercase, lowercase, number, and special character).</li></ol><p><strong>Tip:</strong> If you have TOTP (Authenticator App) enabled, you can also verify using your authenticator code for faster access.</p>" },
                new HelpArticle { HelpArticleID = 4, HelpCategoryID = 1, Title = "How to spot scams and buy safely", IsPopular = false, CreatedAt = seedDate,
                    Content = "<h4>Staying Safe on SecurePlatform</h4><p>Our platform uses <strong>AI-powered fraud detection</strong> (XGBoost model) to monitor transactions, but here are tips to protect yourself:</p><ul><li><strong>Never share your password or OTP</strong> with anyone — our staff will never ask for these.</li><li><strong>Check seller ratings and reviews</strong> before making a purchase.</li><li><strong>Be cautious of deals that seem too good to be true</strong> — extremely low prices may indicate fraudulent listings.</li><li><strong>Always pay through the platform</strong> — never transfer money directly to a seller outside of SecurePlatform.</li><li><strong>Enable Two-Factor Authentication (TOTP)</strong> in your account settings for an extra layer of security.</li></ul><p>If you suspect fraudulent activity, report it immediately through our chat support.</p>" },

                // Payments
                new HelpArticle { HelpArticleID = 5, HelpCategoryID = 2, Title = "What payment methods are supported?", IsPopular = true, CreatedAt = seedDate,
                    Content = "<h4>Supported Payment Methods</h4><p>SecurePlatform supports the following secure payment options:</p><ul><li><strong>Credit / Debit Card</strong> — Visa, Mastercard, and other major cards are accepted. All card data is encrypted and tokenized for security. You can also save cards for faster checkout.</li><li><strong>Online Banking (FPX)</strong> — Pay directly from your Malaysian bank account via FPX. Supported banks include Maybank, CIMB, Public Bank, RHB, Hong Leong Bank, and more.</li></ul><p>All payments are processed through secure, PCI-compliant channels with end-to-end encryption.</p>" },
                new HelpArticle { HelpArticleID = 6, HelpCategoryID = 2, Title = "Why did my payment fail?", IsPopular = false, CreatedAt = seedDate,
                    Content = "<h4>Common Reasons for Payment Failure</h4><p>If your payment was declined, it could be due to:</p><ul><li><strong>Insufficient funds</strong> — Ensure your account has enough balance to cover the order total including shipping fees.</li><li><strong>Card expired or blocked</strong> — Check with your bank if your card is active and enabled for online transactions.</li><li><strong>Incorrect card details</strong> — Double-check your card number, expiry date, and CVV.</li><li><strong>Bank security block</strong> — Some banks may flag unfamiliar online transactions. Contact your bank to authorize the payment.</li><li><strong>AI Security Block</strong> — Our fraud detection system may flag high-risk transactions. If your order was blocked, you will see a notification explaining the reason.</li></ul><p>You can retry the payment or try a different payment method.</p>" },
                new HelpArticle { HelpArticleID = 7, HelpCategoryID = 2, Title = "How does the FPX authorization process work?", IsPopular = false, CreatedAt = seedDate,
                    Content = "<h4>FPX Payment Flow</h4><p>FPX (Financial Process Exchange) allows you to pay directly from your bank account:</p><ol><li>At checkout, select <strong>Online Banking (FPX)</strong> as your payment method.</li><li>Choose your bank from the list of supported banks.</li><li>You will be redirected to your bank's secure login page.</li><li>Log in to your online banking and authorize the payment.</li><li>Once approved, you will be redirected back to SecurePlatform with a payment confirmation.</li></ol><p><strong>Important:</strong> Never close the browser window during the FPX process. If the session is interrupted, the payment may still be deducted. Contact us if this happens and we will investigate.</p>" },

                // Orders & Shipping
                new HelpArticle { HelpArticleID = 8, HelpCategoryID = 3, Title = "How can I track my order status?", IsPopular = true, CreatedAt = seedDate,
                    Content = "<h4>Tracking Your Order</h4><p>You can track your order at every stage of the delivery process:</p><ol><li>Go to <strong>My Purchases</strong> from your profile dropdown menu.</li><li>Find your order and click on it to open the <strong>Order Details</strong> page.</li><li>The order timeline shows real-time status updates: <em>Processing, Shipped, In Transit, Delivered</em>.</li><li>A tracking number is generated once the courier picks up your parcel. Use this to track on the courier website.</li></ol><p>You will also receive notifications at each stage of the delivery process.</p>" },
                new HelpArticle { HelpArticleID = 9, HelpCategoryID = 3, Title = "How to change my shipping address after ordering?", IsPopular = false, CreatedAt = seedDate,
                    Content = "<h4>Changing Your Shipping Address</h4><p>Unfortunately, once an order has been placed and payment is confirmed, the shipping address <strong>cannot be changed</strong> as the order is immediately queued for processing.</p><p><strong>What you can do:</strong></p><ul><li>If the order is still in <em>Processing</em> status, contact the seller via our chat system to request a cancellation. Then place a new order with the correct address.</li><li>Make sure to update your default address in <strong>My Account, Addresses</strong> before placing future orders.</li></ul><p><strong>Tip:</strong> Always double-check your selected delivery address on the checkout page before confirming your order.</p>" },
                new HelpArticle { HelpArticleID = 10, HelpCategoryID = 3, Title = "How are shipping fees calculated?", IsPopular = false, CreatedAt = seedDate,
                    Content = "<h4>Shipping Fee Calculation</h4><p>Shipping fees on SecurePlatform are calculated dynamically based on:</p><ul><li><strong>Parcel weight</strong> — Each product has a weight value. The total weight of all items in your order determines the base shipping fee.</li><li><strong>Delivery zone</strong> — Your address is classified as either <em>West Malaysia</em> or <em>East Malaysia</em>. East Malaysia deliveries have higher base rates.</li><li><strong>Courier pricing rules</strong> — We compare rates from available couriers (J and T Express, PosLaju) and automatically assign the most affordable option.</li></ul><h5>Shipping Discounts</h5><ul><li>Orders above <strong>RM 600</strong> (under 5kg) — <em>Free shipping!</em></li><li>Orders above <strong>RM 500</strong> (10kg+) — <strong>RM 10 off</strong> shipping.</li><li>Orders above <strong>RM 100</strong> — <strong>RM 5 off</strong> shipping.</li></ul>" },

                // Returns & Refunds
                new HelpArticle { HelpArticleID = 11, HelpCategoryID = 4, Title = "How do I request a refund or return?", IsPopular = true, CreatedAt = seedDate,
                    Content = "<h4>Requesting a Refund/Return</h4><p>If you are not satisfied with your purchase, you can request a refund:</p><ol><li>Go to <strong>My Purchases</strong> and find the order.</li><li>Click on the order to open <strong>Order Details</strong>.</li><li>Click the <strong>Request Refund</strong> button (available after order is received).</li><li>Select your reason for the return and choose your preferred return method: <em>Pick-Up</em> (courier comes to you) or <em>Drop-Off</em> (you drop it at a branch).</li><li>Upload photos as evidence if the product is damaged or incorrect.</li><li>Submit the request and wait for seller approval.</li></ol><p>Once approved, a courier will be assigned for pick-up or you will receive drop-off instructions.</p>" },
                new HelpArticle { HelpArticleID = 12, HelpCategoryID = 4, Title = "How will I get my refund for cancelled orders?", IsPopular = true, CreatedAt = seedDate,
                    Content = "<h4>Refund for Cancelled Orders</h4><p>Refunds are processed based on your original payment method:</p><ul><li><strong>Credit / Debit Card</strong> — The refund will be credited back to the same card within 7-14 business days, depending on your bank.</li><li><strong>Online Banking (FPX)</strong> — Refunds are processed back to your bank account within 5-10 business days.</li></ul><p><strong>Tracking your refund:</strong></p><ol><li>Go to <strong>My Purchases</strong> and click on the refunded order.</li><li>The refund status will show: <em>Requested, Approved, Refund In Transit, Completed</em>.</li></ol><p>If your refund has not arrived after the stated timeframe, please contact our support team.</p>" },
                new HelpArticle { HelpArticleID = 13, HelpCategoryID = 4, Title = "How long does the refund process take?", IsPopular = false, CreatedAt = seedDate,
                    Content = "<h4>Refund Processing Timeline</h4><table style='width:100%; border-collapse:collapse; margin:15px 0;'><thead><tr style='background:#f8f9fa; border-bottom:2px solid #dee2e6;'><th style='padding:10px; text-align:left;'>Stage</th><th style='padding:10px; text-align:left;'>Estimated Time</th></tr></thead><tbody><tr style='border-bottom:1px solid #dee2e6;'><td style='padding:10px;'>Seller Review and Approval</td><td style='padding:10px;'>1-3 business days</td></tr><tr style='border-bottom:1px solid #dee2e6;'><td style='padding:10px;'>Return Shipping (if applicable)</td><td style='padding:10px;'>3-5 business days</td></tr><tr style='border-bottom:1px solid #dee2e6;'><td style='padding:10px;'>Refund Processing</td><td style='padding:10px;'>1-2 business days after item received</td></tr><tr style='border-bottom:1px solid #dee2e6;'><td style='padding:10px;'>Bank Processing (Card)</td><td style='padding:10px;'>7-14 business days</td></tr><tr><td style='padding:10px;'>Bank Processing (FPX)</td><td style='padding:10px;'>5-10 business days</td></tr></tbody></table><p>Total estimated time: <strong>12-24 business days</strong> from request to refund in your account.</p>" },

                // Policies & General
                new HelpArticle { HelpArticleID = 14, HelpCategoryID = 5, Title = "Privacy Policy", IsPopular = false, CreatedAt = seedDate,
                    Content = "<h4>SecurePlatform Privacy Policy</h4><p>We take your privacy seriously. Here is how we handle your data:</p><h5>Data We Collect</h5><ul><li><strong>Account information</strong> — Name, email, phone number for account management.</li><li><strong>Payment data</strong> — Card details are encrypted and tokenized; we never store raw card numbers.</li><li><strong>Transaction history</strong> — Order records for your purchase history and our fraud prevention systems.</li><li><strong>Device information</strong> — Device fingerprints for security and anti-fraud measures.</li></ul><h5>How We Use Your Data</h5><ul><li>Processing your orders and payments securely.</li><li>AI-powered fraud detection to protect your account.</li><li>Sending order status updates and important notifications.</li><li>Improving our platform and user experience.</li></ul><h5>Your Rights</h5><p>You can request access to, correction of, or deletion of your personal data at any time by contacting our support team.</p>" },
                new HelpArticle { HelpArticleID = 15, HelpCategoryID = 5, Title = "Terms of Service", IsPopular = false, CreatedAt = seedDate,
                    Content = "<h4>SecurePlatform Terms of Service</h4><h5>1. Account Responsibility</h5><p>You are responsible for maintaining the confidentiality of your account credentials. Any activity under your account is your responsibility. Enable Two-Factor Authentication for enhanced security.</p><h5>2. Purchasing</h5><p>All prices are in Malaysian Ringgit (RM). Prices include product cost but shipping fees are calculated separately at checkout. Orders are binding once payment is confirmed.</p><h5>3. Seller Obligations</h5><p>Sellers must provide accurate product descriptions and images. Sellers must process and ship orders within 3 business days. Failure to do so may result in automatic cancellation.</p><h5>4. Prohibited Activities</h5><ul><li>Creating multiple accounts to exploit promotions.</li><li>Selling counterfeit or prohibited items.</li><li>Harassment of buyers or sellers through the chat system.</li><li>Attempting to bypass security measures or the fraud detection system.</li></ul><h5>5. Dispute Resolution</h5><p>Disputes between buyers and sellers are mediated by our support team. Decisions made by our admin team are final.</p>" }
            );
        }
    }
}