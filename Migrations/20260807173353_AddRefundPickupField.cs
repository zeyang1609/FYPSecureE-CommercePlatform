using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundPickupField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PickupAddressID",
                table: "Refunds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupDate",
                table: "Refunds",
                type: "datetime(6)",
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PickupAddressID",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "PickupDate",
                table: "Refunds");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 6, 11, 28, 14, 422, DateTimeKind.Utc).AddTicks(8498));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 6, 11, 28, 14, 422, DateTimeKind.Utc).AddTicks(465));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 6, 11, 28, 14, 422, DateTimeKind.Utc).AddTicks(8503));
        }
    }
}
