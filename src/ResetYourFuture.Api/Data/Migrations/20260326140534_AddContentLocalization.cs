using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResetYourFuture.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContentLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Courses",
                newName: "TitleEn");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "Courses",
                newName: "DescriptionEn");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "AssessmentDefinitions",
                newName: "TitleEn");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "AssessmentDefinitions",
                newName: "DescriptionEn");

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEl",
                table: "Courses",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleEl",
                table: "Courses",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEl",
                table: "AssessmentDefinitions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleEl",
                table: "AssessmentDefinitions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionEl",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "TitleEl",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "DescriptionEl",
                table: "AssessmentDefinitions");

            migrationBuilder.DropColumn(
                name: "TitleEl",
                table: "AssessmentDefinitions");

            migrationBuilder.RenameColumn(
                name: "TitleEn",
                table: "Courses",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "DescriptionEn",
                table: "Courses",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "TitleEn",
                table: "AssessmentDefinitions",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "DescriptionEn",
                table: "AssessmentDefinitions",
                newName: "Description");
        }
    }
}
