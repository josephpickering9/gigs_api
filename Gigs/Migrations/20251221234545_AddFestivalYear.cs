using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gigs.Migrations
{
    /// <inheritdoc />
    public partial class AddFestivalYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "Festival",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Year",
                table: "Festival");
        }
    }
}
