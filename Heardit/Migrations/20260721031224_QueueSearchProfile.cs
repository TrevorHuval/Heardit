using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heardit.Migrations
{
    /// <inheritdoc />
    public partial class QueueSearchProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "AspNetUsers",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FavoriteTracks",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SongId = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteTracks", x => new { x.UserId, x.SongId });
                    table.ForeignKey(
                        name: "FK_FavoriteTracks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FavoriteTracks_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListenLater",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SongId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListenLater", x => new { x.UserId, x.SongId });
                    table.ForeignKey(
                        name: "FK_ListenLater_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ListenLater_Songs_SongId",
                        column: x => x.SongId,
                        principalTable: "Songs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteTracks_SongId",
                table: "FavoriteTracks",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteTracks_UserId_Position",
                table: "FavoriteTracks",
                columns: new[] { "UserId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ListenLater_SongId",
                table: "ListenLater",
                column: "SongId");

            migrationBuilder.CreateIndex(
                name: "IX_ListenLater_UserId_CreatedAt",
                table: "ListenLater",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FavoriteTracks");

            migrationBuilder.DropTable(
                name: "ListenLater");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "AspNetUsers");
        }
    }
}
