using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoa.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AudioDurationMs",
                table: "Entries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioMime",
                table: "Entries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioPath",
                table: "Entries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AudioSizeBytes",
                table: "Entries",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioDurationMs",
                table: "Entries");

            migrationBuilder.DropColumn(
                name: "AudioMime",
                table: "Entries");

            migrationBuilder.DropColumn(
                name: "AudioPath",
                table: "Entries");

            migrationBuilder.DropColumn(
                name: "AudioSizeBytes",
                table: "Entries");
        }
    }
}
