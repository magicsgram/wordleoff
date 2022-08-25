using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WordleOff.Server.Migrations
{
    public partial class AddSpectatorKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpectatorKey",
                table: "GameSessions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpectatorKey",
                table: "GameSessions");
        }
    }
}
