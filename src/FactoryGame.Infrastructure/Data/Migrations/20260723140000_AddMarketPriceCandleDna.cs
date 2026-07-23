using FactoryGame.Domain.Content;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryGame.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketPriceCandleDna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketPriceCandles_ElementId_BucketStart",
                table: "MarketPriceCandles");

            migrationBuilder.AddColumn<long>(
                name: "Dna",
                table: "MarketPriceCandles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            foreach (var element in ElementCatalog.All)
            {
                migrationBuilder.Sql(
                    $"UPDATE MarketPriceCandles SET Dna = {element.Dna} WHERE ElementId = {element.Id} AND Dna = 0");
            }

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceCandles_ElementId_Dna_BucketStart",
                table: "MarketPriceCandles",
                columns: new[] { "ElementId", "Dna", "BucketStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarketPriceCandles_ElementId_Dna_BucketStart",
                table: "MarketPriceCandles");

            migrationBuilder.DropColumn(
                name: "Dna",
                table: "MarketPriceCandles");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceCandles_ElementId_BucketStart",
                table: "MarketPriceCandles",
                columns: new[] { "ElementId", "BucketStart" },
                unique: true);
        }
    }
}
