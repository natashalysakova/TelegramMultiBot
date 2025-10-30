using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramMultiBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Group",
                table: "Monitor");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "ElectricityHistory");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Monitor",
                type: "char(36)",
                nullable: false,
                collation: "ascii_general_ci",
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "Monitor",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "ElectricityHistory",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "ElectricityGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LocationRegion = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GroupCode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GroupName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DataSnapshot = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElectricityGroups", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Monitor_GroupId",
                table: "Monitor",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ElectricityHistory_GroupId",
                table: "ElectricityHistory",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_ElectricityHistory_ElectricityGroups_GroupId",
                table: "ElectricityHistory",
                column: "GroupId",
                principalTable: "ElectricityGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Monitor_ElectricityGroups_GroupId",
                table: "Monitor",
                column: "GroupId",
                principalTable: "ElectricityGroups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ElectricityHistory_ElectricityGroups_GroupId",
                table: "ElectricityHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_Monitor_ElectricityGroups_GroupId",
                table: "Monitor");

            migrationBuilder.DropTable(
                name: "ElectricityGroups");

            migrationBuilder.DropIndex(
                name: "IX_Monitor_GroupId",
                table: "Monitor");

            migrationBuilder.DropIndex(
                name: "IX_ElectricityHistory_GroupId",
                table: "ElectricityHistory");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Monitor");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "ElectricityHistory");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Monitor",
                type: "int",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "Monitor",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "ElectricityHistory",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
