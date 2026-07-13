using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResetYourFuture.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConvertDateTimeOffsetToNativeType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Testimonials",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Testimonials",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "SiteSettings",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RegisteredAt",
                table: "SessionRegistrations",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartsAtUtc",
                table: "ScheduledSessions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ReminderSentAt",
                table: "ScheduledSessions",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "ScheduledSessions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "RefreshTokens",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "RefreshTokens",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "RefreshTokens",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Notifications",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Modules",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "Modules",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Modules",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Modules",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Lessons",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "Lessons",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Lessons",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Lessons",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "LearningPaths",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "LearningPaths",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "LearningPaths",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "LearningPaths",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Courses",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "Courses",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Courses",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Courses",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "CourseReviews",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "CourseReviews",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "Categories",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "Categories",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Categories",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "Categories",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "BlogArticles",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "BlogArticles",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "BlogArticles",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "BlogArticles",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "AssessmentSubmissions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "AssessmentDefinitions",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "AssessmentDefinitions",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "AssessmentDefinitions",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "AssessmentDefinitions",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LockoutEnd",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(48)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UpdatedAt",
                table: "Testimonials",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "Testimonials",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedAt",
                table: "SiteSettings",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "RegisteredAt",
                table: "SessionRegistrations",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "StartsAtUtc",
                table: "ScheduledSessions",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "ReminderSentAt",
                table: "ScheduledSessions",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "ScheduledSessions",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "RevokedAt",
                table: "RefreshTokens",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExpiresAt",
                table: "RefreshTokens",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "RefreshTokens",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "Notifications",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedAt",
                table: "Modules",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PublishedAt",
                table: "Modules",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeletedAt",
                table: "Modules",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "Modules",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedAt",
                table: "Lessons",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PublishedAt",
                table: "Lessons",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeletedAt",
                table: "Lessons",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "Lessons",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedAt",
                table: "LearningPaths",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PublishedAt",
                table: "LearningPaths",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeletedAt",
                table: "LearningPaths",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "LearningPaths",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedAt",
                table: "Courses",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PublishedAt",
                table: "Courses",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeletedAt",
                table: "Courses",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "Courses",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedAt",
                table: "CourseReviews",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "CourseReviews",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedAt",
                table: "Categories",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PublishedAt",
                table: "Categories",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeletedAt",
                table: "Categories",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "Categories",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedAt",
                table: "BlogArticles",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PublishedAt",
                table: "BlogArticles",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeletedAt",
                table: "BlogArticles",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "BlogArticles",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "SubmittedAt",
                table: "AssessmentSubmissions",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "UpdatedAt",
                table: "AssessmentDefinitions",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PublishedAt",
                table: "AssessmentDefinitions",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DeletedAt",
                table: "AssessmentDefinitions",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "AssessmentDefinitions",
                type: "nvarchar(48)",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<string>(
                name: "LockoutEnd",
                table: "AspNetUsers",
                type: "nvarchar(48)",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);
        }
    }
}
