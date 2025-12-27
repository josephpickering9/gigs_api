using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gigs.Migrations
{
    /// <inheritdoc />
    public partial class AddFestivals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FestivalId",
                table: "Gig",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Festival",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Festival", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Gig_FestivalId",
                table: "Gig",
                column: "FestivalId");

            migrationBuilder.CreateIndex(
                name: "IX_Festival_Slug",
                table: "Festival",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Gig_Festival_FestivalId",
                table: "Gig",
                column: "FestivalId",
                principalTable: "Festival",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gig_Festival_FestivalId",
                table: "Gig");

            migrationBuilder.DropTable(
                name: "Festival");

            migrationBuilder.DropIndex(
                name: "IX_Gig_FestivalId",
                table: "Gig");

            migrationBuilder.DropColumn(
                name: "FestivalId",
                table: "Gig");
        }
    }
}
