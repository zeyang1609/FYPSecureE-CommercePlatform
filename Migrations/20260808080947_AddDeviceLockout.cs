using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceLockouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DeviceIdentifier = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceLockouts", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 8, 8, 9, 46, 906, DateTimeKind.Utc).AddTicks(4595));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 8, 8, 9, 46, 905, DateTimeKind.Utc).AddTicks(7244));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 8, 8, 9, 46, 906, DateTimeKind.Utc).AddTicks(4600));

            migrationBuilder.CreateIndex(
                name: "IX_DeviceLockouts_DeviceIdentifier",
                table: "DeviceLockouts",
                column: "DeviceIdentifier",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceLockouts");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 8, 7, 46, 31, 847, DateTimeKind.Utc).AddTicks(7447));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 8, 7, 46, 31, 845, DateTimeKind.Utc).AddTicks(2081));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 8, 7, 46, 31, 847, DateTimeKind.Utc).AddTicks(7456));
        }
    }
}
