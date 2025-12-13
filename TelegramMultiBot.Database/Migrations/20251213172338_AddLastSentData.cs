using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramMultiBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddLastSentData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastSentData",
                table: "Svitlobot",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSentData",
                table: "Svitlobot");
        }
    }
}
