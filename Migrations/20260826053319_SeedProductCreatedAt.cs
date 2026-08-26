using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class SeedProductCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_001",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_002",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_003",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_004",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_005",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_006",
                column: "CreatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_001",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 26, 5, 20, 34, 621, DateTimeKind.Utc).AddTicks(3148));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_002",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 26, 5, 20, 34, 621, DateTimeKind.Utc).AddTicks(7793));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_003",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 26, 5, 20, 34, 621, DateTimeKind.Utc).AddTicks(7800));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_004",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 26, 5, 20, 34, 621, DateTimeKind.Utc).AddTicks(7805));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_005",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 26, 5, 20, 34, 621, DateTimeKind.Utc).AddTicks(7860));

            migrationBuilder.UpdateData(
                table: "Products",
                keyColumn: "ProductID",
                keyValue: "PROD_006",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 26, 5, 20, 34, 621, DateTimeKind.Utc).AddTicks(7864));
        }
    }
}
