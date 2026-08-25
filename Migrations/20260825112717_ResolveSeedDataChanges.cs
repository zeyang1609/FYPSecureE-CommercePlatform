using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class ResolveSeedDataChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                column: "PhoneNumber",
                value: "");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "PhoneNumber",
                value: "");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                column: "PhoneNumber",
                value: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "admin_demo_1",
                column: "PhoneNumber",
                value: "N77g4NDFtkWf6WCY:KoCEGPcBH3Riy01Ua9mIDw==:BhDwHAVTXmYzpg==");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "seller_demo_1",
                column: "PhoneNumber",
                value: "Y8zM4YiuEQXNH/HK:gq2tfWERguMV7kpq8/cJiA==:HtLPWjl+Nck0OQ==");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserID",
                keyValue: "SYSTEM",
                column: "PhoneNumber",
                value: "zw9AjqTenjKEQMxl:mq4LmGAlgjJq/hZ0xnqzMQ==:h+UQJzeZPovzpw==");
        }
    }
}
