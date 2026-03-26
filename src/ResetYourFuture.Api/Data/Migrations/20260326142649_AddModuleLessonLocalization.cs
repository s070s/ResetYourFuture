using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResetYourFuture.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleLessonLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Modules",
                newName: "TitleEn");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Modules",
                newName: "DescriptionEn");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Lessons",
                newName: "TitleEn");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "Lessons",
                newName: "ContentEn");

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEl",
                table: "Modules",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleEl",
                table: "Modules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentEl",
                table: "Lessons",
                type: "nvarchar(max)",
                maxLength: 50000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleEl",
                table: "Lessons",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionEl",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "TitleEl",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "ContentEl",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "TitleEl",
                table: "Lessons");

            migrationBuilder.RenameColumn(
                name: "TitleEn",
                table: "Modules",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "DescriptionEn",
                table: "Modules",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "TitleEn",
                table: "Lessons",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "ContentEn",
                table: "Lessons",
                newName: "Content");
        }
    }
}
