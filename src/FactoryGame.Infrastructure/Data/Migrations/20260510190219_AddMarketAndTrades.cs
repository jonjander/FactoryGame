using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryGame.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketAndTrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElementId = table.Column<int>(type: "integer", nullable: false),
                    Side = table.Column<byte>(type: "smallint", nullable: false),
                    LimitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    QuantityRemaining = table.Column<long>(type: "bigint", nullable: false),
                    OriginalQuantity = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradeExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElementId = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: false),
                    BuyerPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeExecutions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketOrders_ElementId_Status_Side",
                table: "MarketOrders",
                columns: new[] { "ElementId", "Status", "Side" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketOrders_PlayerId_IdempotencyKey",
                table: "MarketOrders",
                columns: new[] { "PlayerId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeExecutions_ElementId",
                table: "TradeExecutions",
                column: "ElementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketOrders");

            migrationBuilder.DropTable(
                name: "TradeExecutions");
        }
    }
}
