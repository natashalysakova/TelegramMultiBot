using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramMultiBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class ExtendMonitorJobTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextRun",
                table: "Monitor");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "Monitor");

            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "Monitor",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "Monitor",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "MessageThreadId",
                table: "Monitor",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Group",
                table: "ElectricityHistory",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "ElectricityHistory",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Monitor_LocationId",
                table: "Monitor",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Monitor_ElectricityLocations_LocationId",
                table: "Monitor",
                column: "LocationId",
                principalTable: "ElectricityLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Monitor_ElectricityLocations_LocationId",
                table: "Monitor");

            migrationBuilder.DropIndex(
                name: "IX_Monitor_LocationId",
                table: "Monitor");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "Monitor");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Monitor");

            migrationBuilder.DropColumn(
                name: "MessageThreadId",
                table: "Monitor");

            migrationBuilder.DropColumn(
                name: "Group",
                table: "ElectricityHistory");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "ElectricityHistory");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRun",
                table: "Monitor",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "Monitor",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
