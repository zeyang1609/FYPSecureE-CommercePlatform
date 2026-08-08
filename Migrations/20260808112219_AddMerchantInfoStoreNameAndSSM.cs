using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMerchantInfoStoreNameAndSSM : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SSMNumber",
                table: "Users",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StoreName",
                table: "Users",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                columns: new[] { "CreatedAt", "SSMNumber", "StoreName" },
                values: new object[] { new DateTime(2026, 8, 8, 11, 22, 19, 94, DateTimeKind.Utc).AddTicks(8728), null, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                columns: new[] { "CreatedAt", "SSMNumber", "StoreName" },
                values: new object[] { new DateTime(2026, 8, 8, 11, 22, 19, 94, DateTimeKind.Utc).AddTicks(829), null, null });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                columns: new[] { "CreatedAt", "SSMNumber", "StoreName" },
                values: new object[] { new DateTime(2026, 8, 8, 11, 22, 19, 94, DateTimeKind.Utc).AddTicks(8733), null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SSMNumber",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StoreName",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 8, 10, 40, 44, 269, DateTimeKind.Utc).AddTicks(5066));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 8, 10, 40, 44, 268, DateTimeKind.Utc).AddTicks(7544));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 8, 10, 40, 44, 269, DateTimeKind.Utc).AddTicks(5070));
        }
    }
}
