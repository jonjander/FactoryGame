using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryGame.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApiKeysForeignKeyToPlayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeys_Players_PlayerId",
                table: "ApiKeys",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeys_Players_PlayerId",
                table: "ApiKeys");
        }
    }
}
