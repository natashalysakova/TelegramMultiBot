using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramMultiBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFailureCountToAlert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlertMessage",
                table: "Alerts",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "FailureCount",
                table: "Alerts",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertMessage",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "FailureCount",
                table: "Alerts");
        }
    }
}
