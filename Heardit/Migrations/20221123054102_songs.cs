using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heardit.Migrations
{
    /// <inheritdoc />
    public partial class songs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Song",
                table: "Song");

            migrationBuilder.RenameTable(
                name: "Song",
                newName: "Songs");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Songs");

            migrationBuilder.AddColumn<string>(
                name: "Id",
                table: "Songs",
                type: "nvarchar(450)",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "AlbumArt",
                table: "Songs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Songs",
                table: "Songs",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Songs",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "AlbumArt",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Songs");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Songs",
                type: "int",
                nullable: false);

            migrationBuilder.RenameTable(
                name: "Songs",
                newName: "Song");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Song",
                table: "Song",
                column: "Id");
        }
    }
}
