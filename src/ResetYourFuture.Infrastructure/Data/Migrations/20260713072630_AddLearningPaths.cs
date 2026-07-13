using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResetYourFuture.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningPaths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleEl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DescriptionEl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<string>(type: "nvarchar(48)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<string>(type: "nvarchar(48)", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAt = table.Column<string>(type: "nvarchar(48)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<string>(type: "nvarchar(48)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningPaths", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningPaths_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LearningPathSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningPathId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningPathSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningPathSteps_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningPathSteps_LearningPaths_LearningPathId",
                        column: x => x.LearningPathId,
                        principalTable: "LearningPaths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningPaths_CategoryId",
                table: "LearningPaths",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPaths_IsPublished",
                table: "LearningPaths",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathSteps_CourseId",
                table: "LearningPathSteps",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathSteps_LearningPathId_StepOrder",
                table: "LearningPathSteps",
                columns: new[] { "LearningPathId", "StepOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningPathSteps");

            migrationBuilder.DropTable(
                name: "LearningPaths");
        }
    }
}
