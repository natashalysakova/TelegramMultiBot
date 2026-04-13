using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramMultiBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVideoDownloadTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `VideoDownloads` SET `Status` = CASE `Status` WHEN 'pending' THEN 0 WHEN 'sent' THEN 1 WHEN 'error' THEN 2 ELSE 0 END");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "VideoDownloads",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "VideoDownloads",
                keyColumn: "RequestedBy",
                keyValue: null,
                column: "RequestedBy",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "RequestedBy",
                table: "VideoDownloads",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "MessageToDelete",
                table: "VideoDownloads",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "MessageThreadId",
                table: "VideoDownloads",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE `VideoDownloads` SET `Status` = CASE `Status` WHEN 0 THEN 'pending' WHEN 1 THEN 'sent' WHEN 2 THEN 'error' ELSE 'pending' END");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "VideoDownloads",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "RequestedBy",
                table: "VideoDownloads",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "MessageToDelete",
                table: "VideoDownloads",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MessageThreadId",
                table: "VideoDownloads",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
