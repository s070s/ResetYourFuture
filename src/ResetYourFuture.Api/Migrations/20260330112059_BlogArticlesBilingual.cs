using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResetYourFuture.Api.Migrations
{
    /// <inheritdoc />
    public partial class BlogArticlesBilingual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Title",
                table: "BlogArticles",
                newName: "TitleEn");

            migrationBuilder.RenameColumn(
                name: "Summary",
                table: "BlogArticles",
                newName: "SummaryEn");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "BlogArticles",
                newName: "ContentEn");

            migrationBuilder.AddColumn<string>(
                name: "ContentEl",
                table: "BlogArticles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SummaryEl",
                table: "BlogArticles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleEl",
                table: "BlogArticles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentEl",
                table: "BlogArticles");

            migrationBuilder.DropColumn(
                name: "SummaryEl",
                table: "BlogArticles");

            migrationBuilder.DropColumn(
                name: "TitleEl",
                table: "BlogArticles");

            migrationBuilder.RenameColumn(
                name: "TitleEn",
                table: "BlogArticles",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "SummaryEn",
                table: "BlogArticles",
                newName: "Summary");

            migrationBuilder.RenameColumn(
                name: "ContentEn",
                table: "BlogArticles",
                newName: "Content");
        }
    }
}
