using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseInventory.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryStatusOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatusOptions",
                table: "Inventories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "Available, In use, Broken");

            migrationBuilder.Sql("""
                UPDATE "Inventories"
                SET "StatusOptions" = 'Available, In use, Broken'
                WHERE COALESCE(TRIM("StatusOptions"), '') = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusOptions",
                table: "Inventories");
        }
    }
}
