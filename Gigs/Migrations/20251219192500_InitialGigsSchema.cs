using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Work_Experience_Search.Migrations
{
    /// <inheritdoc />
    public partial class InitialGigsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Artist",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Slug = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artist", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Person",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Person", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Venue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Slug = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Song",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Song", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Song_Artist_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artist",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Gig",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TicketCost = table.Column<decimal>(type: "numeric", nullable: true),
                    TicketType = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Slug = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gig", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gig_Venue_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venue",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GigArtist",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GigId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsHeadliner = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    SetlistUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigArtist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GigArtist_Artist_ArtistId",
                        column: x => x.ArtistId,
                        principalTable: "Artist",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GigArtist_Gig_GigId",
                        column: x => x.GigId,
                        principalTable: "Gig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GigAttendee",
                columns: table => new
                {
                    GigId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigAttendee", x => new { x.GigId, x.PersonId });
                    table.ForeignKey(
                        name: "FK_GigAttendee_Gig_GigId",
                        column: x => x.GigId,
                        principalTable: "Gig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GigAttendee_Person_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Person",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GigArtistSong",
                columns: table => new
                {
                    GigArtistId = table.Column<Guid>(type: "uuid", nullable: false),
                    SongId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GigArtistSong", x => new { x.GigArtistId, x.SongId });
                    table.ForeignKey(
                        name: "FK_GigArtistSong_GigArtist_GigArtistId",
                        column: x => x.GigArtistId,
                        principalTable: "GigArtist",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GigArtistSong_Song_SongId",
                        column: x => x.SongId,
                        principalTable: "Song",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Artist_Slug",
                table: "Artist",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gig_Slug",
                table: "Gig",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gig_VenueId",
                table: "Gig",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_GigArtist_ArtistId",
                table: "GigArtist",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_GigArtist_GigId",
                table: "GigArtist",
                column: "GigId");

            migrationBuilder.CreateIndex(
                name: "IX_GigArtistSong_SongId",
                table: "GigArtistSong",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_GigAttendee_PersonId",
                table: "GigAttendee",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Person_Slug",
                table: "Person",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Song_ArtistId",
                table: "Song",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_Song_Slug",
                table: "Song",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Venue_Slug",
                table: "Venue",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GigArtistSong");

            migrationBuilder.DropTable(
                name: "GigAttendee");

            migrationBuilder.DropTable(
                name: "GigArtist");

            migrationBuilder.DropTable(
                name: "Song");

            migrationBuilder.DropTable(
                name: "Person");

            migrationBuilder.DropTable(
                name: "Gig");

            migrationBuilder.DropTable(
                name: "Artist");

            migrationBuilder.DropTable(
                name: "Venue");
        }
    }
}
