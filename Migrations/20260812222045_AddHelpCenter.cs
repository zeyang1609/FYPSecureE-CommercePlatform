using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FYP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHelpCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HelpCategories",
                columns: table => new
                {
                    HelpCategoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IconClass = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpCategories", x => x.HelpCategoryID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HelpArticles",
                columns: table => new
                {
                    HelpArticleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    HelpCategoryID = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Content = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPopular = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HelpArticles", x => x.HelpArticleID);
                    table.ForeignKey(
                        name: "FK_HelpArticles_HelpCategories_HelpCategoryID",
                        column: x => x.HelpCategoryID,
                        principalTable: "HelpCategories",
                        principalColumn: "HelpCategoryID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "HelpCategories",
                columns: new[] { "HelpCategoryID", "DisplayOrder", "IconClass", "Name" },
                values: new object[,]
                {
                    { 1, 1, "shield-lock", "Account & Security" },
                    { 2, 2, "credit-card", "Payments" },
                    { 3, 3, "truck", "Orders & Shipping" },
                    { 4, 4, "arrow-return-left", "Returns & Refunds" },
                    { 5, 5, "file-text", "Policies & General" }
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 12, 22, 20, 43, 893, DateTimeKind.Utc).AddTicks(489));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 12, 22, 20, 43, 892, DateTimeKind.Utc).AddTicks(647));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 12, 22, 20, 43, 893, DateTimeKind.Utc).AddTicks(493));

            migrationBuilder.InsertData(
                table: "HelpArticles",
                columns: new[] { "HelpArticleID", "Content", "CreatedAt", "HelpCategoryID", "IsPopular", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "<h4>Creating Your SecurePlatform Account</h4><p>Follow these simple steps to get started:</p><ol><li>Click the <strong>Sign Up</strong> button at the top right corner of the homepage.</li><li>Enter your email address and create a strong password. Your password must contain at least 8 characters, including uppercase, lowercase, numbers, and special characters.</li><li>Verify your email address by entering the OTP (One-Time Password) sent to your inbox.</li><li>Complete your profile by adding your name and phone number.</li></ol><p>Once verified, you can start browsing and purchasing products immediately!</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, false, "How to create an account?", null },
                    { 2, "<h4>Troubleshooting OTP Issues</h4><p>If you haven't received your OTP, try the following:</p><ul><li><strong>Check your Spam/Junk folder</strong> — OTP emails sometimes get filtered by email providers.</li><li><strong>Wait a moment</strong> — OTPs can take up to 2 minutes to arrive depending on your email provider.</li><li><strong>Verify your email address</strong> — Make sure the email address you entered is correct with no typos.</li><li><strong>Request a new OTP</strong> — Click the 'Resend OTP' button on the verification page.</li></ul><p>If you continue to experience issues, please contact our support team via the live chat.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, "Why did I not receive my OTP?", null },
                    { 3, "<h4>Resetting Your Password</h4><p>If you've forgotten your password, follow these steps:</p><ol><li>Go to the <strong>Login</strong> page and click <em>'Forgot Password?'</em>.</li><li>Enter the email address associated with your account.</li><li>You will receive an OTP on your email. Enter the OTP to verify your identity.</li><li>Create a new password that meets our security requirements (minimum 8 characters with uppercase, lowercase, number, and special character).</li></ol><p><strong>Tip:</strong> If you have TOTP (Authenticator App) enabled, you can also verify using your authenticator code for faster access.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, false, "How to reset my password?", null },
                    { 4, "<h4>Staying Safe on SecurePlatform</h4><p>Our platform uses <strong>AI-powered fraud detection</strong> (XGBoost model) to monitor transactions, but here are tips to protect yourself:</p><ul><li><strong>Never share your password or OTP</strong> with anyone — our staff will never ask for these.</li><li><strong>Check seller ratings and reviews</strong> before making a purchase.</li><li><strong>Be cautious of deals that seem too good to be true</strong> — extremely low prices may indicate fraudulent listings.</li><li><strong>Always pay through the platform</strong> — never transfer money directly to a seller outside of SecurePlatform.</li><li><strong>Enable Two-Factor Authentication (TOTP)</strong> in your account settings for an extra layer of security.</li></ul><p>If you suspect fraudulent activity, report it immediately through our chat support.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, false, "How to spot scams and buy safely", null },
                    { 5, "<h4>Supported Payment Methods</h4><p>SecurePlatform supports the following secure payment options:</p><ul><li><strong>Credit / Debit Card</strong> — Visa, Mastercard, and other major cards are accepted. All card data is encrypted and tokenized for security. You can also save cards for faster checkout.</li><li><strong>Online Banking (FPX)</strong> — Pay directly from your Malaysian bank account via FPX. Supported banks include Maybank, CIMB, Public Bank, RHB, Hong Leong Bank, and more.</li></ul><p>All payments are processed through secure, PCI-compliant channels with end-to-end encryption.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "What payment methods are supported?", null },
                    { 6, "<h4>Common Reasons for Payment Failure</h4><p>If your payment was declined, it could be due to:</p><ul><li><strong>Insufficient funds</strong> — Ensure your account has enough balance to cover the order total including shipping fees.</li><li><strong>Card expired or blocked</strong> — Check with your bank if your card is active and enabled for online transactions.</li><li><strong>Incorrect card details</strong> — Double-check your card number, expiry date, and CVV.</li><li><strong>Bank security block</strong> — Some banks may flag unfamiliar online transactions. Contact your bank to authorize the payment.</li><li><strong>AI Security Block</strong> — Our fraud detection system may flag high-risk transactions. If your order was blocked, you will see a notification explaining the reason.</li></ul><p>You can retry the payment or try a different payment method.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, false, "Why did my payment fail?", null },
                    { 7, "<h4>FPX Payment Flow</h4><p>FPX (Financial Process Exchange) allows you to pay directly from your bank account:</p><ol><li>At checkout, select <strong>Online Banking (FPX)</strong> as your payment method.</li><li>Choose your bank from the list of supported banks.</li><li>You will be redirected to your bank's secure login page.</li><li>Log in to your online banking and authorize the payment.</li><li>Once approved, you will be redirected back to SecurePlatform with a payment confirmation.</li></ol><p><strong>Important:</strong> Never close the browser window during the FPX process. If the session is interrupted, the payment may still be deducted. Contact us if this happens and we will investigate.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, false, "How does the FPX authorization process work?", null },
                    { 8, "<h4>Tracking Your Order</h4><p>You can track your order at every stage of the delivery process:</p><ol><li>Go to <strong>My Purchases</strong> from your profile dropdown menu.</li><li>Find your order and click on it to open the <strong>Order Details</strong> page.</li><li>The order timeline shows real-time status updates: <em>Processing, Shipped, In Transit, Delivered</em>.</li><li>A tracking number is generated once the courier picks up your parcel. Use this to track on the courier website.</li></ol><p>You will also receive notifications at each stage of the delivery process.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, true, "How can I track my order status?", null },
                    { 9, "<h4>Changing Your Shipping Address</h4><p>Unfortunately, once an order has been placed and payment is confirmed, the shipping address <strong>cannot be changed</strong> as the order is immediately queued for processing.</p><p><strong>What you can do:</strong></p><ul><li>If the order is still in <em>Processing</em> status, contact the seller via our chat system to request a cancellation. Then place a new order with the correct address.</li><li>Make sure to update your default address in <strong>My Account, Addresses</strong> before placing future orders.</li></ul><p><strong>Tip:</strong> Always double-check your selected delivery address on the checkout page before confirming your order.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, false, "How to change my shipping address after ordering?", null },
                    { 10, "<h4>Shipping Fee Calculation</h4><p>Shipping fees on SecurePlatform are calculated dynamically based on:</p><ul><li><strong>Parcel weight</strong> — Each product has a weight value. The total weight of all items in your order determines the base shipping fee.</li><li><strong>Delivery zone</strong> — Your address is classified as either <em>West Malaysia</em> or <em>East Malaysia</em>. East Malaysia deliveries have higher base rates.</li><li><strong>Courier pricing rules</strong> — We compare rates from available couriers (J and T Express, PosLaju) and automatically assign the most affordable option.</li></ul><h5>Shipping Discounts</h5><ul><li>Orders above <strong>RM 600</strong> (under 5kg) — <em>Free shipping!</em></li><li>Orders above <strong>RM 500</strong> (10kg+) — <strong>RM 10 off</strong> shipping.</li><li>Orders above <strong>RM 100</strong> — <strong>RM 5 off</strong> shipping.</li></ul>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, false, "How are shipping fees calculated?", null },
                    { 11, "<h4>Requesting a Refund/Return</h4><p>If you are not satisfied with your purchase, you can request a refund:</p><ol><li>Go to <strong>My Purchases</strong> and find the order.</li><li>Click on the order to open <strong>Order Details</strong>.</li><li>Click the <strong>Request Refund</strong> button (available after order is received).</li><li>Select your reason for the return and choose your preferred return method: <em>Pick-Up</em> (courier comes to you) or <em>Drop-Off</em> (you drop it at a branch).</li><li>Upload photos as evidence if the product is damaged or incorrect.</li><li>Submit the request and wait for seller approval.</li></ol><p>Once approved, a courier will be assigned for pick-up or you will receive drop-off instructions.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, true, "How do I request a refund or return?", null },
                    { 12, "<h4>Refund for Cancelled Orders</h4><p>Refunds are processed based on your original payment method:</p><ul><li><strong>Credit / Debit Card</strong> — The refund will be credited back to the same card within 7-14 business days, depending on your bank.</li><li><strong>Online Banking (FPX)</strong> — Refunds are processed back to your bank account within 5-10 business days.</li></ul><p><strong>Tracking your refund:</strong></p><ol><li>Go to <strong>My Purchases</strong> and click on the refunded order.</li><li>The refund status will show: <em>Requested, Approved, Refund In Transit, Completed</em>.</li></ol><p>If your refund has not arrived after the stated timeframe, please contact our support team.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, true, "How will I get my refund for cancelled orders?", null },
                    { 13, "<h4>Refund Processing Timeline</h4><table style='width:100%; border-collapse:collapse; margin:15px 0;'><thead><tr style='background:#f8f9fa; border-bottom:2px solid #dee2e6;'><th style='padding:10px; text-align:left;'>Stage</th><th style='padding:10px; text-align:left;'>Estimated Time</th></tr></thead><tbody><tr style='border-bottom:1px solid #dee2e6;'><td style='padding:10px;'>Seller Review and Approval</td><td style='padding:10px;'>1-3 business days</td></tr><tr style='border-bottom:1px solid #dee2e6;'><td style='padding:10px;'>Return Shipping (if applicable)</td><td style='padding:10px;'>3-5 business days</td></tr><tr style='border-bottom:1px solid #dee2e6;'><td style='padding:10px;'>Refund Processing</td><td style='padding:10px;'>1-2 business days after item received</td></tr><tr style='border-bottom:1px solid #dee2e6;'><td style='padding:10px;'>Bank Processing (Card)</td><td style='padding:10px;'>7-14 business days</td></tr><tr><td style='padding:10px;'>Bank Processing (FPX)</td><td style='padding:10px;'>5-10 business days</td></tr></tbody></table><p>Total estimated time: <strong>12-24 business days</strong> from request to refund in your account.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, false, "How long does the refund process take?", null },
                    { 14, "<h4>SecurePlatform Privacy Policy</h4><p>We take your privacy seriously. Here is how we handle your data:</p><h5>Data We Collect</h5><ul><li><strong>Account information</strong> — Name, email, phone number for account management.</li><li><strong>Payment data</strong> — Card details are encrypted and tokenized; we never store raw card numbers.</li><li><strong>Transaction history</strong> — Order records for your purchase history and our fraud prevention systems.</li><li><strong>Device information</strong> — Device fingerprints for security and anti-fraud measures.</li></ul><h5>How We Use Your Data</h5><ul><li>Processing your orders and payments securely.</li><li>AI-powered fraud detection to protect your account.</li><li>Sending order status updates and important notifications.</li><li>Improving our platform and user experience.</li></ul><h5>Your Rights</h5><p>You can request access to, correction of, or deletion of your personal data at any time by contacting our support team.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5, false, "Privacy Policy", null },
                    { 15, "<h4>SecurePlatform Terms of Service</h4><h5>1. Account Responsibility</h5><p>You are responsible for maintaining the confidentiality of your account credentials. Any activity under your account is your responsibility. Enable Two-Factor Authentication for enhanced security.</p><h5>2. Purchasing</h5><p>All prices are in Malaysian Ringgit (RM). Prices include product cost but shipping fees are calculated separately at checkout. Orders are binding once payment is confirmed.</p><h5>3. Seller Obligations</h5><p>Sellers must provide accurate product descriptions and images. Sellers must process and ship orders within 3 business days. Failure to do so may result in automatic cancellation.</p><h5>4. Prohibited Activities</h5><ul><li>Creating multiple accounts to exploit promotions.</li><li>Selling counterfeit or prohibited items.</li><li>Harassment of buyers or sellers through the chat system.</li><li>Attempting to bypass security measures or the fraud detection system.</li></ul><h5>5. Dispute Resolution</h5><p>Disputes between buyers and sellers are mediated by our support team. Decisions made by our admin team are final.</p>", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5, false, "Terms of Service", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_HelpArticles_HelpCategoryID",
                table: "HelpArticles",
                column: "HelpCategoryID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HelpArticles");

            migrationBuilder.DropTable(
                name: "HelpCategories");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 11, 16, 19, 41, 363, DateTimeKind.Utc).AddTicks(7044));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 11, 16, 19, 41, 362, DateTimeKind.Utc).AddTicks(8739));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 11, 16, 19, 41, 363, DateTimeKind.Utc).AddTicks(7048));
        }
    }
}
