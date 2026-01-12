using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoa.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTitleToEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Entries",
                type: "text",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "Entries");
        }
    }
}
