using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoa.Functions.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAndSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SpeechEnabled = table.Column<bool>(type: "bit", nullable: true),
                    SpeechBaseTimeoutSeconds = table.Column<int>(type: "int", nullable: true),
                    SpeechTimeoutPerAudioMinuteSeconds = table.Column<int>(type: "int", nullable: true),
                    SearchMinSimilarity = table.Column<double>(type: "float", nullable: true),
                    SearchMaxResults = table.Column<int>(type: "int", nullable: true),
                    TaggingMinSimilarity = table.Column<double>(type: "float", nullable: true),
                    TaggingPendingTimeoutMinutes = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                    table.CheckConstraint("CK_SystemSettings_Singleton", "[Id] = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");
        }
    }
}
