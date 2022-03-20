using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WordleOff.Server.Migrations
{
    public partial class AddWordStats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ConnectionIdToSessionIds",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "WordStats",
                columns: table => new
                {
                    Word = table.Column<string>(type: "text", nullable: false),
                    SubmitCountTotal = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SubmitCountRound1 = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SubmitCountRound2 = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SubmitCountRound3 = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SubmitCountRound4 = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SubmitCountRound5 = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    SubmitCountRound6 = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WordStats", x => x.Word);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WordStats_Word",
                table: "WordStats",
                column: "Word",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WordStats");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ConnectionIdToSessionIds");
        }
    }
}
