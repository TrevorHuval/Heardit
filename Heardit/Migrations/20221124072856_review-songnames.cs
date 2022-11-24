using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heardit.Migrations
{
    /// <inheritdoc />
    public partial class reviewsongnames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SongName",
                table: "Reviews",
                type: "nvarchar(max)",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SongName",
                table: "Reviews");
        }
    }
}
