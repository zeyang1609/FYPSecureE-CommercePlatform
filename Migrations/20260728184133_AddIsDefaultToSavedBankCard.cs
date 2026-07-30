using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FYP.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDefaultToSavedBankCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "SavedBankCards",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "SavedBankCards");
        }
    }
}
