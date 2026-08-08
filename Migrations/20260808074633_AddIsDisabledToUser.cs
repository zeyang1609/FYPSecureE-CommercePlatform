using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDisabledToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDisabled",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                columns: new[] { "CreatedAt", "IsDisabled" },
                values: new object[] { new DateTime(2026, 8, 8, 7, 46, 31, 847, DateTimeKind.Utc).AddTicks(7447), false });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                columns: new[] { "CreatedAt", "IsDisabled" },
                values: new object[] { new DateTime(2026, 8, 8, 7, 46, 31, 845, DateTimeKind.Utc).AddTicks(2081), false });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                columns: new[] { "CreatedAt", "IsDisabled" },
                values: new object[] { new DateTime(2026, 8, 8, 7, 46, 31, 847, DateTimeKind.Utc).AddTicks(7456), false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDisabled",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 7, 17, 33, 52, 582, DateTimeKind.Utc).AddTicks(9592));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 7, 17, 33, 52, 582, DateTimeKind.Utc).AddTicks(1052));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 7, 17, 33, 52, 582, DateTimeKind.Utc).AddTicks(9595));
        }
    }
}
