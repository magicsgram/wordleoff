using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WordleOff.Server.Migrations
{
    public partial class AddConnectionIdToSessionId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConnectionIdToSessionIds",
                columns: table => new
                {
                    ConnectionId = table.Column<string>(type: "text", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectionIdToSessionIds", x => x.ConnectionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConnectionIdToSessionIds_ConnectionId",
                table: "ConnectionIdToSessionIds",
                column: "ConnectionId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConnectionIdToSessionIds");
        }
    }
}
