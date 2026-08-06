using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class AddProductDetailsAndSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AverageRating",
                table: "Products",
                type: "decimal(3,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Products",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ReviewCount",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalSales",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "CategoryID", "Description", "IconSvg", "Name" },
                values: new object[] { "cat_tech_1", "Latest gadgets, electronics, and smart devices.", "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\"><rect x=\"4\" y=\"4\" width=\"16\" height=\"16\" rx=\"2\"></rect><rect x=\"9\" y=\"9\" width=\"6\" height=\"6\"></rect><line x1=\"9\" y1=\"1\" x2=\"9\" y2=\"4\"></line><line x1=\"15\" y1=\"1\" x2=\"15\" y2=\"4\"></line><line x1=\"9\" y1=\"20\" x2=\"9\" y2=\"23\"></line><line x1=\"15\" y1=\"20\" x2=\"15\" y2=\"23\"></line><line x1=\"20\" y1=\"9\" x2=\"23\" y2=\"9\"></line><line x1=\"20\" y1=\"14\" x2=\"23\" y2=\"14\"></line><line x1=\"1\" y1=\"9\" x2=\"4\" y2=\"9\"></line><line x1=\"1\" y1=\"14\" x2=\"4\" y2=\"14\"></line></svg>", "Tech & Gadgets" });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserID", "DateOfBirth", "DeviceHash", "Email", "Gender", "MFA_Enabled", "Name", "PasswordHash", "PaymentGatewayCustomerId", "PhoneNumber", "Role" },
                values: new object[] { "seller_demo_1", null, "SEED", "demo_seller@secureplatform.com", null, true, "Official Tech Store", "SEED_NO_LOGIN", null, "0123456789", "Seller" });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "ProductID", "AverageRating", "CategoryID", "Description", "ImageHash", "Price", "ReviewCount", "SellerID", "StockLevel", "Title", "TotalSales" },
                values: new object[,]
                {
                    { "PROD_001", 4.9m, "cat_tech_1", "Experience pure sound with industry-leading active noise cancellation. Features 30-hour battery life, touch sensor controls, and speak-to-chat technology.", "SEED", 899.00m, 342, "seller_demo_1", 45, "Wireless Noise-Cancelling Headphones Pro", 1250 },
                    { "PROD_002", 4.8m, "cat_tech_1", "Advanced health monitoring right on your wrist. Measure your blood oxygen level, take an ECG anytime, and track your daily activity.", "SEED", 1299.00m, 890, "seller_demo_1", 120, "Smart Fitness Watch Series 7", 3400 },
                    { "PROD_003", 4.7m, "cat_tech_1", "Tactile mechanical switches for ultimate gaming performance. Features customizable per-key RGB lighting and an aircraft-grade aluminum alloy frame.", "SEED", 450.00m, 156, "seller_demo_1", 3, "Mechanical Gaming Keyboard RGB", 850 },
                    { "PROD_004", 4.9m, "cat_tech_1", "Weighing only 63 grams, this mouse is designed for professional esports. Features a 25K DPI sensor and zero-additive PTFE feet for smooth gliding.", "SEED", 320.00m, 512, "seller_demo_1", 80, "Ultra-Light Wireless Esports Mouse", 2100 },
                    { "PROD_005", 4.6m, "cat_tech_1", "Stunning 4K resolution with 99% sRGB color accuracy. Factory calibrated for creators who demand perfect color representation and crisp text.", "SEED", 1850.00m, 89, "seller_demo_1", 15, "27-inch 4K IPS Creator Monitor", 420 },
                    { "PROD_006", 4.9m, "cat_tech_1", "Never run out of battery again. This high-capacity power bank supports 65W Power Delivery, allowing you to fast-charge your smartphone, tablet, and even your laptop on the go.", "SEED", 150.00m, 1240, "seller_demo_1", 250, "20,000mAh PD Fast Charge Power Bank", 5600 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_001");

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_002");

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_003");

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_004");

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_005");

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_006");

            migrationBuilder.DeleteData(
                table: "Categories",
                keyColumn: "CategoryID",
                keyValue: "cat_tech_1");

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1");

            migrationBuilder.DropColumn(
                name: "AverageRating",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ReviewCount",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TotalSales",
                table: "Products");
        }
    }
}
