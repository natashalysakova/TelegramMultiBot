using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramMultiBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTable2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DataSnapshot",
                table: "ElectricityGroups",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ElectricityGroups",
                keyColumn: "DataSnapshot",
                keyValue: null,
                column: "DataSnapshot",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "DataSnapshot",
                table: "ElectricityGroups",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
