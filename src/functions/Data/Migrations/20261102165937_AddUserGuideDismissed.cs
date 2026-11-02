using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoa.Functions.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGuideDismissed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GuideDismissedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuideDismissedAt",
                table: "Users");
        }
    }
}
