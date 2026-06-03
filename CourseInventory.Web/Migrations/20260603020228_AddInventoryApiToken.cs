using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseInventory.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryApiToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApiTokenCreatedAt",
                table: "Inventories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiTokenHash",
                table: "Inventories",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_ApiTokenHash",
                table: "Inventories",
                column: "ApiTokenHash",
                unique: true,
                filter: "\"ApiTokenHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inventories_ApiTokenHash",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "ApiTokenCreatedAt",
                table: "Inventories");

            migrationBuilder.DropColumn(
                name: "ApiTokenHash",
                table: "Inventories");
        }
    }
}
