using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Heardit.Migrations
{
    /// <inheritdoc />
    public partial class UniqueReviewPerUserSong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove duplicate reviews (keep the most recently updated per user+song); their likes cascade.
            migrationBuilder.Sql(@"
DELETE FROM ""Reviews"" r
USING ""Reviews"" k
WHERE r.""UserId"" = k.""UserId"" AND r.""SongId"" = k.""SongId"" AND r.""ReviewId"" <> k.""ReviewId""
  AND (COALESCE(r.""UpdatedAt"", r.""CreatedAt""), r.""ReviewId"") < (COALESCE(k.""UpdatedAt"", k.""CreatedAt""), k.""ReviewId"");");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId_SongId",
                table: "Reviews",
                columns: new[] { "UserId", "SongId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_UserId_SongId",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews",
                column: "UserId");
        }
    }
}
