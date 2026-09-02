using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YTTrending.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelUploadsPlaylistId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "uploads_playlist_id",
                table: "channels",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "uploads_playlist_id",
                table: "channels");
        }
    }
}
