using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WordleOff.Server.Migrations
{
    public partial class AddSessionStats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "GameSessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "GameSessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GameStartedAt",
                table: "GameSessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "MaxPlayersConnected",
                table: "GameSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalGameTimeSeconds",
                table: "GameSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalGamesPlayed",
                table: "GameSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalPlayersConnected",
                table: "GameSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SessionStats",
                columns: table => new
                {
                    Category = table.Column<string>(type: "text", nullable: false),
                    Count = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionStats", x => x.Category);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionStats_Category",
                table: "SessionStats",
                column: "Category",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionStats");

            migrationBuilder.DropColumn(
                name: "GameStartedAt",
                table: "GameSessions");

            migrationBuilder.DropColumn(
                name: "MaxPlayersConnected",
                table: "GameSessions");

            migrationBuilder.DropColumn(
                name: "TotalGameTimeSeconds",
                table: "GameSessions");

            migrationBuilder.DropColumn(
                name: "TotalGamesPlayed",
                table: "GameSessions");

            migrationBuilder.DropColumn(
                name: "TotalPlayersConnected",
                table: "GameSessions");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "GameSessions",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "GameSessions",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");
        }
    }
}
