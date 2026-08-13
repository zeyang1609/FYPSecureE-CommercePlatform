using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIpFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IpFilters",
                columns: table => new
                {
                    FilterID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IpAddress = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FilterAction = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Reason = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AddedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AddedByAdminID = table.Column<string>(type: "varchar(50)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IpFilters", x => x.FilterID);
                    table.ForeignKey(
                        name: "FK_IpFilters_Users_AddedByAdminID",
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
                value: new DateTime(2026, 8, 12, 17, 5, 1, 485, DateTimeKind.Utc).AddTicks(173));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 12, 17, 5, 1, 482, DateTimeKind.Utc).AddTicks(5492));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                column: "CreatedAt",
                value: new DateTime(2026, 8, 12, 17, 5, 1, 485, DateTimeKind.Utc).AddTicks(182));

            migrationBuilder.CreateIndex(
                name: "IX_IpFilters_AddedByAdminID",
                table: "IpFilters",
                column: "AddedByAdminID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IpFilters");

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
        }
    }
}
