using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImageBlacklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlacklistedImageHashes",
                columns: table => new
                {
                    HashID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SHA256Hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AddedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AddedByAdminID = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlacklistedImageHashes", x => x.HashID);
                    table.ForeignKey(
                        name: "FK_BlacklistedImageHashes_Users_AddedByAdminID",
                        column: x => x.AddedByAdminID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 12, 16, 43, 0, 560, DateTimeKind.Utc).AddTicks(9300));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 12, 16, 43, 0, 560, DateTimeKind.Utc).AddTicks(1301));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 12, 16, 43, 0, 560, DateTimeKind.Utc).AddTicks(9305));

            migrationBuilder.CreateIndex(
                name: "IX_BlacklistedImageHashes_AddedByAdminID",
                table: "BlacklistedImageHashes",
                column: "AddedByAdminID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlacklistedImageHashes");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 11, 16, 19, 41, 363, DateTimeKind.Utc).AddTicks(7044));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 11, 16, 19, 41, 362, DateTimeKind.Utc).AddTicks(8739));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 11, 16, 19, 41, 363, DateTimeKind.Utc).AddTicks(7048));
        }
    }
}
