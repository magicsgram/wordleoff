using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WordleOff.Server.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameSessions",
                columns: table => new
                {
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    PlayerDataDictionary = table.Column<string>(type: "jsonb", nullable: false),
                    PastAnswers = table.Column<string>(type: "jsonb", nullable: false),
                    LastUpdateAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSessions", x => x.SessionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameSessions_SessionId",
                table: "GameSessions",
                column: "SessionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameSessions");
        }
    }
}
