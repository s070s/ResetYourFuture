using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResetYourFuture.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCallSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CallEvent",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CallSessionId",
                table: "ChatMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CallSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitiatorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndReason = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallSessions_AspNetUsers_InitiatorId",
                        column: x => x.InitiatorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CallSessions_ChatConversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "ChatConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CallParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CallSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InvitedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeftAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallParticipants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CallParticipants_CallSessions_CallSessionId",
                        column: x => x.CallSessionId,
                        principalTable: "CallSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_CallSessionId",
                table: "ChatMessages",
                column: "CallSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CallParticipants_CallSessionId_UserId",
                table: "CallParticipants",
                columns: new[] { "CallSessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CallParticipants_UserId",
                table: "CallParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CallSessions_ConversationId",
                table: "CallSessions",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_CallSessions_EndedAt",
                table: "CallSessions",
                column: "EndedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CallSessions_InitiatorId_StartedAt",
                table: "CallSessions",
                columns: new[] { "InitiatorId", "StartedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_CallSessions_CallSessionId",
                table: "ChatMessages",
                column: "CallSessionId",
                principalTable: "CallSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_CallSessions_CallSessionId",
                table: "ChatMessages");

            migrationBuilder.DropTable(
                name: "CallParticipants");

            migrationBuilder.DropTable(
                name: "CallSessions");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_CallSessionId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "CallEvent",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "CallSessionId",
                table: "ChatMessages");
        }
    }
}
