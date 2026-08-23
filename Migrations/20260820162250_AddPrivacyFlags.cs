using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacyFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowPersonalizedAds",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsProfilePublic",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShareDataWithThirdParties",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                columns: new[] { "AllowPersonalizedAds", "CreatedAt", "IsProfilePublic", "ShareDataWithThirdParties" },
                values: new object[] { true, new DateTime(2026, 8, 20, 16, 22, 49, 370, DateTimeKind.Utc).AddTicks(6288), false, false });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                columns: new[] { "AllowPersonalizedAds", "CreatedAt", "IsProfilePublic", "ShareDataWithThirdParties" },
                values: new object[] { true, new DateTime(2026, 8, 20, 16, 22, 49, 369, DateTimeKind.Utc).AddTicks(7624), false, false });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                columns: new[] { "AllowPersonalizedAds", "CreatedAt", "IsProfilePublic", "ShareDataWithThirdParties" },
                values: new object[] { true, new DateTime(2026, 8, 20, 16, 22, 49, 370, DateTimeKind.Utc).AddTicks(6292), false, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowPersonalizedAds",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsProfilePublic",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ShareDataWithThirdParties",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 14, 0, 24, 51, 689, DateTimeKind.Utc).AddTicks(5379));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 14, 0, 24, 51, 688, DateTimeKind.Utc).AddTicks(4623));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 14, 0, 24, 51, 689, DateTimeKind.Utc).AddTicks(5383));
        }
    }
}
