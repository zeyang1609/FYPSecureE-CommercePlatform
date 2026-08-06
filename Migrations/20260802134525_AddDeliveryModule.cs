using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            

            migrationBuilder.AlterColumn<string>(
                name: "DeliveryID",
                table: "Orders",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Couriers",
                columns: table => new
                {
                    CourierID = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TrackingUrlTemplate = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Couriers", x => x.CourierID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    DeliveryID = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrderID = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CourierID = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TrackingNumber = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShippingFee = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EstimatedDeliveryDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ActualDeliveryDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.DeliveryID);
                    table.ForeignKey(
                        name: "FK_Deliveries_Couriers_CourierID",
                        column: x => x.CourierID,
                        principalTable: "Couriers",
                        principalColumn: "CourierID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Deliveries_Orders_OrderID",
                        column: x => x.OrderID,
                        principalTable: "Orders",
                        principalColumn: "OrderID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DeliveryPricingRules",
                columns: table => new
                {
                    DeliveryRuleID = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CourierID = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ZoneRegion = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BaseWeightKg = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IncrementalWeightKg = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    IncrementalPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryPricingRules", x => x.DeliveryRuleID);
                    table.ForeignKey(
                        name: "FK_DeliveryPricingRules_Couriers_CourierID",
                        column: x => x.CourierID,
                        principalTable: "Couriers",
                        principalColumn: "CourierID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Couriers",
                columns: new[] { "CourierID", "IsActive", "Name", "TrackingUrlTemplate" },
                values: new object[,]
                {
                    { "COUR_JNT", true, "J&T Express", "https://www.jtexpress.my/tracking/{0}" },
                    { "COUR_POS", true, "PosLaju", "https://track.pos.com.my/tracking/{0}" }
                });

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_001",
                column: "WeightKg",
                value: 1.00m);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_002",
                column: "WeightKg",
                value: 1.00m);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_003",
                column: "WeightKg",
                value: 1.00m);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_004",
                column: "WeightKg",
                value: 1.00m);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_005",
                column: "WeightKg",
                value: 1.00m);

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_006",
                column: "WeightKg",
                value: 1.00m);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                columns: new[] { "CreatedAt", "TotpSecret" },
                values: new object[] { new DateTime(2026, 8, 2, 13, 45, 24, 271, DateTimeKind.Utc).AddTicks(5224), null });

            migrationBuilder.InsertData(
                table: "DeliveryPricingRules",
                columns: new[] { "DeliveryRuleID", "BasePrice", "BaseWeightKg", "CourierID", "IncrementalPrice", "IncrementalWeightKg", "ZoneRegion" },
                values: new object[,]
                {
                    { "RULE_JNT_EM", 12.00m, 1.00m, "COUR_JNT", 2.50m, 0.50m, "East Malaysia" },
                    { "RULE_JNT_WM", 4.90m, 1.00m, "COUR_JNT", 1.00m, 0.50m, "West Malaysia" },
                    { "RULE_POS_EM", 10.00m, 1.00m, "COUR_POS", 3.00m, 0.50m, "East Malaysia" },
                    { "RULE_POS_WM", 6.00m, 2.00m, "COUR_POS", 1.50m, 1.00m, "West Malaysia" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_CourierID",
                table: "Deliveries",
                column: "CourierID");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_OrderID",
                table: "Deliveries",
                column: "OrderID");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPricingRules_CourierID",
                table: "DeliveryPricingRules",
                column: "CourierID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "DeliveryPricingRules");

            migrationBuilder.DropTable(
                name: "Couriers");


            migrationBuilder.UpdateData(
                table: "Orders",
                keyColumn: "DeliveryID",
                keyValue: null,
                column: "DeliveryID",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "DeliveryID",
                table: "Orders",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
