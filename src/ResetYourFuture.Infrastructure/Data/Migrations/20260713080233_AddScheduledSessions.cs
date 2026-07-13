using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResetYourFuture.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleEl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartsAtUtc = table.Column<string>(type: "nvarchar(48)", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxParticipants = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CallSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReminderSentAt = table.Column<string>(type: "nvarchar(48)", nullable: true),
                    CreatedAt = table.Column<string>(type: "nvarchar(48)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledSessions_AspNetUsers_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledSessions_CallSessions_CallSessionId",
                        column: x => x.CallSessionId,
                        principalTable: "CallSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScheduledSessions_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SessionRegistrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RegisteredAt = table.Column<string>(type: "nvarchar(48)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionRegistrations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionRegistrations_ScheduledSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ScheduledSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledSessions_CallSessionId",
                table: "ScheduledSessions",
                column: "CallSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledSessions_CourseId",
                table: "ScheduledSessions",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledSessions_HostUserId",
                table: "ScheduledSessions",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledSessions_StartsAtUtc",
                table: "ScheduledSessions",
                column: "StartsAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledSessions_Status",
                table: "ScheduledSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRegistrations_SessionId_UserId",
                table: "SessionRegistrations",
                columns: new[] { "SessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionRegistrations_UserId",
                table: "SessionRegistrations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionRegistrations");

            migrationBuilder.DropTable(
                name: "ScheduledSessions");
        }
    }
}
