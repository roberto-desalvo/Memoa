using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdateAtAndAudioTranscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AudioTranscription",
                table: "Entries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Entries",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioTranscription",
                table: "Entries");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Entries");
        }
    }
}
