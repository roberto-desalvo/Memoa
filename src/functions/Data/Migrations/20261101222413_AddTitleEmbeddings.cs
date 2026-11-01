using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Memoa.Functions.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTitleEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<SqlVector<float>>(
                name: "NameEmbedding",
                table: "Tags",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.AddColumn<SqlVector<float>>(
                name: "TitleEmbedding",
                table: "Records",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleEmbeddingModel",
                table: "Records",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameEmbedding",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "TitleEmbedding",
                table: "Records");

            migrationBuilder.DropColumn(
                name: "TitleEmbeddingModel",
                table: "Records");
        }
    }
}
