using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gigs.Migrations
{
    /// <inheritdoc />
    public partial class AddGigArtistSongDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CoverArtistId",
                table: "GigArtistSong",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Info",
                table: "GigArtistSong",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTape",
                table: "GigArtistSong",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "WithArtistId",
                table: "GigArtistSong",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GigArtistSong_CoverArtistId",
                table: "GigArtistSong",
                column: "CoverArtistId");

            migrationBuilder.CreateIndex(
                name: "IX_GigArtistSong_WithArtistId",
                table: "GigArtistSong",
                column: "WithArtistId");

            migrationBuilder.AddForeignKey(
                name: "FK_GigArtistSong_Artist_CoverArtistId",
                table: "GigArtistSong",
                column: "CoverArtistId",
                principalTable: "Artist",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GigArtistSong_Artist_WithArtistId",
                table: "GigArtistSong",
                column: "WithArtistId",
                principalTable: "Artist",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GigArtistSong_Artist_CoverArtistId",
                table: "GigArtistSong");

            migrationBuilder.DropForeignKey(
                name: "FK_GigArtistSong_Artist_WithArtistId",
                table: "GigArtistSong");

            migrationBuilder.DropIndex(
                name: "IX_GigArtistSong_CoverArtistId",
                table: "GigArtistSong");

            migrationBuilder.DropIndex(
                name: "IX_GigArtistSong_WithArtistId",
                table: "GigArtistSong");

            migrationBuilder.DropColumn(
                name: "CoverArtistId",
                table: "GigArtistSong");

            migrationBuilder.DropColumn(
                name: "Info",
                table: "GigArtistSong");

            migrationBuilder.DropColumn(
                name: "IsTape",
                table: "GigArtistSong");

            migrationBuilder.DropColumn(
                name: "WithArtistId",
                table: "GigArtistSong");
        }
    }
}
