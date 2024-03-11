using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramMultiBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class FixMilliseconds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "RenderTime",
                table: "JobResult",
                type: "double",
                nullable: false,
                oldClrType: typeof(TimeSpan),
                oldType: "time(6)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<TimeSpan>(
                name: "RenderTime",
                table: "JobResult",
                type: "time(6)",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double");
        }
    }
}
