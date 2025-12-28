using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gigs.Migrations
{
    /// <inheritdoc />
    public partial class AddFestivalDetailsAndAttendees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "Festival",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Festival",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "Festival",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FestivalAttendee",
                columns: table => new
                {
                    FestivalId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FestivalAttendee", x => new { x.FestivalId, x.PersonId });
                    table.ForeignKey(
                        name: "FK_FestivalAttendee_Festival_FestivalId",
                        column: x => x.FestivalId,
                        principalTable: "Festival",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FestivalAttendee_Person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Person",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FestivalAttendee_PersonId",
                table: "FestivalAttendee",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FestivalAttendee");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Festival");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Festival");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Festival");
        }
    }
}
