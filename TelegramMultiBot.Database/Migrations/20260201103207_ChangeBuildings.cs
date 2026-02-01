using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramMultiBot.Database.Migrations
{
    /// <inheritdoc />
    public partial class ChangeBuildings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Building",
                table: "AddressJobs",
                newName: "Number");

            migrationBuilder.CreateIndex(
                name: "IX_AddressJobs_BuildingId",
                table: "AddressJobs",
                column: "BuildingId");

            migrationBuilder.AddForeignKey(
                name: "FK_AddressJobs_Buildings_BuildingId",
                table: "AddressJobs",
                column: "BuildingId",
                principalTable: "Buildings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AddressJobs_Buildings_BuildingId",
                table: "AddressJobs");

            migrationBuilder.DropIndex(
                name: "IX_AddressJobs_BuildingId",
                table: "AddressJobs");

            migrationBuilder.RenameColumn(
                name: "Number",
                table: "AddressJobs",
                newName: "Building");
        }
    }
}
