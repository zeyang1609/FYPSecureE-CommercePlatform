using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdminAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 6, 11, 12, 47, 944, DateTimeKind.Utc).AddTicks(3007));

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserID", "CreatedAt", "DateOfBirth", "DeviceHash", "Email", "Gender", "MFA_Enabled", "Name", "PasswordHash", "PaymentGatewayCustomerId", "PhoneNumber", "Role", "TotpSecret" },
                values: new object[] { "admin_demo_1", new DateTime(2026, 8, 6, 11, 12, 47, 945, DateTimeKind.Utc).AddTicks(394), null, "SEED", "demo_admin@secureplatform.com", null, true, "System Administrator", "SEED_NO_LOGIN", null, "0123456789", "Admin", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 6, 10, 45, 44, 546, DateTimeKind.Utc).AddTicks(9220));
        }
    }
}
