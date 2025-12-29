using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gigs.Migrations
{
    /// <inheritdoc />
    public partial class AddPosterImageUrlAndVenueIdToFestival : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PosterImageUrl",
                table: "Festival",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VenueId",
                table: "Festival",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Festival_VenueId",
                table: "Festival",
                column: "VenueId");

            migrationBuilder.AddForeignKey(
                name: "FK_Festival_Venue_VenueId",
                table: "Festival",
                column: "VenueId",
                principalTable: "Venue",
                principalColumn: "Id");

            // Data Migration: Move ImageUrl content to PosterImageUrl and clear ImageUrl
            migrationBuilder.Sql("UPDATE \"Festival\" SET \"PosterImageUrl\" = \"ImageUrl\";");
            migrationBuilder.Sql("UPDATE \"Festival\" SET \"ImageUrl\" = NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data Restoration (Best Effort): Move PosterImageUrl back to ImageUrl
            migrationBuilder.Sql("UPDATE \"Festival\" SET \"ImageUrl\" = \"PosterImageUrl\";");

            migrationBuilder.DropForeignKey(
                name: "FK_Festival_Venue_VenueId",
                table: "Festival");

            migrationBuilder.DropIndex(
                name: "IX_Festival_VenueId",
                table: "Festival");

            migrationBuilder.DropColumn(
                name: "PosterImageUrl",
                table: "Festival");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "Festival");
        }
    }
}
