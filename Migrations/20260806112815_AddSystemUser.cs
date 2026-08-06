using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserID", "CreatedAt", "DateOfBirth", "DeviceHash", "Email", "Gender", "MFA_Enabled", "Name", "PasswordHash", "PaymentGatewayCustomerId", "PhoneNumber", "Role", "TotpSecret" },
                values: new object[] { "SYSTEM", new DateTime(2026, 8, 6, 11, 28, 14, 422, DateTimeKind.Utc).AddTicks(8503), null, "SEED", "system@secureplatform.com", null, false, "SYSTEM", "SEED_NO_LOGIN", null, "0000000000", "Admin", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 6, 11, 12, 47, 945, DateTimeKind.Utc).AddTicks(394));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 6, 11, 12, 47, 944, DateTimeKind.Utc).AddTicks(3007));
        }
    }
}
