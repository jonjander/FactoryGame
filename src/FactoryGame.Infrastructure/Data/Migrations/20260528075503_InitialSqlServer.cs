using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryGame.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EconomyTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CashDelta = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Metadata = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EconomyTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ElementId = table.Column<int>(type: "int", nullable: false),
                    Dna = table.Column<long>(type: "bigint", nullable: false),
                    Side = table.Column<byte>(type: "tinyint", nullable: false),
                    LimitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    QuantityRemaining = table.Column<long>(type: "bigint", nullable: false),
                    OriginalQuantity = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsSynthetic = table.Column<bool>(type: "bit", nullable: false),
                    SponsorCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketPriceCandles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ElementId = table.Column<int>(type: "int", nullable: false),
                    BucketStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Open = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    High = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Low = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Close = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Volume = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketPriceCandles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuestDeviceKeyHash = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsSponsorAccount = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SimulationClock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CurrentTick = table.Column<long>(type: "bigint", nullable: false),
                    LastAdvancedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimulationClock", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradeExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ElementId = table.Column<int>(type: "int", nullable: false),
                    Dna = table.Column<long>(type: "bigint", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: false),
                    BuyerPlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SellerPlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuyOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SellOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsSynthetic = table.Column<bool>(type: "bit", nullable: false),
                    BuyerSponsorCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SellerSponsorCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyHash = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scopes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeys_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Boards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Mode = table.Column<byte>(type: "tinyint", nullable: false),
                    RevisionVersion = table.Column<int>(type: "int", nullable: false),
                    SimulationTick = table.Column<long>(type: "bigint", nullable: false),
                    LastSnapshotNote = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Boards_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryPools",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaxVolume = table.Column<long>(type: "bigint", nullable: false),
                    UsedVolume = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryPools", x => x.PlayerId);
                    table.ForeignKey(
                        name: "FK_InventoryPools_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerBalances",
                columns: table => new
                {
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cash = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerBalances", x => x.PlayerId);
                    table.ForeignKey(
                        name: "FK_PlayerBalances_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerMachineStocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMachineStocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerMachineStocks_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerSessions",
                columns: table => new
                {
                    Token = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSessions", x => x.Token);
                    table.ForeignKey(
                        name: "FK_PlayerSessions_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SponsorCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FundingMode = table.Column<int>(type: "int", nullable: false),
                    BudgetRemaining = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TotalBudget = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    VirtualSpend = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ExposureTier = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsorCompanies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SponsorCompanies_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BoardKeyframes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tick = table.Column<long>(type: "bigint", nullable: false),
                    RevisionVersion = table.Column<int>(type: "int", nullable: false),
                    LineStateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SeaportDeltaJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardKeyframes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardKeyframes_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BoardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    PlanJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardRevisions_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoolStacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ElementId = table.Column<int>(type: "int", nullable: false),
                    Dna = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<long>(type: "bigint", nullable: false),
                    VolumePerUnit = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoolStacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoolStacks_InventoryPools_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "InventoryPools",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SponsorCompanyOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SponsorCompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ElementId = table.Column<int>(type: "int", nullable: false),
                    Dna = table.Column<long>(type: "bigint", nullable: false),
                    Side = table.Column<byte>(type: "tinyint", nullable: false),
                    LimitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TargetQuantity = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LinkedMarketOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SponsorCompanyOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SponsorCompanyOrders_SponsorCompanies_SponsorCompanyId",
                        column: x => x.SponsorCompanyId,
                        principalTable: "SponsorCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_PlayerId",
                table: "ApiKeys",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardKeyframes_BoardId_Tick",
                table: "BoardKeyframes",
                columns: new[] { "BoardId", "Tick" });

            migrationBuilder.CreateIndex(
                name: "IX_BoardRevisions_BoardId_Version",
                table: "BoardRevisions",
                columns: new[] { "BoardId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Boards_PlayerId",
                table: "Boards",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_EconomyTransactions_PlayerId",
                table: "EconomyTransactions",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketOrders_ElementId_Dna_IsSynthetic_Status",
                table: "MarketOrders",
                columns: new[] { "ElementId", "Dna", "IsSynthetic", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketOrders_ElementId_Dna_Status_Side",
                table: "MarketOrders",
                columns: new[] { "ElementId", "Dna", "Status", "Side" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketOrders_PlayerId_IdempotencyKey",
                table: "MarketOrders",
                columns: new[] { "PlayerId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MarketOrders_SponsorCompanyId",
                table: "MarketOrders",
                column: "SponsorCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceCandles_ElementId_BucketStart",
                table: "MarketPriceCandles",
                columns: new[] { "ElementId", "BucketStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMachineStocks_PlayerId",
                table: "PlayerMachineStocks",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_GuestDeviceKeyHash",
                table: "Players",
                column: "GuestDeviceKeyHash",
                unique: true,
                filter: "[GuestDeviceKeyHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerSessions_PlayerId",
                table: "PlayerSessions",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PoolStacks_PlayerId_ElementId_Dna",
                table: "PoolStacks",
                columns: new[] { "PlayerId", "ElementId", "Dna" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SponsorCompanies_IsActive",
                table: "SponsorCompanies",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SponsorCompanies_PlayerId",
                table: "SponsorCompanies",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SponsorCompanyOrders_SponsorCompanyId_IsActive",
                table: "SponsorCompanyOrders",
                columns: new[] { "SponsorCompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeExecutions_BuyerSponsorCompanyId",
                table: "TradeExecutions",
                column: "BuyerSponsorCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeExecutions_ElementId_Dna",
                table: "TradeExecutions",
                columns: new[] { "ElementId", "Dna" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeExecutions_ElementId_Dna_CreatedAt",
                table: "TradeExecutions",
                columns: new[] { "ElementId", "Dna", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeExecutions_SellerSponsorCompanyId",
                table: "TradeExecutions",
                column: "SellerSponsorCompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "BoardKeyframes");

            migrationBuilder.DropTable(
                name: "BoardRevisions");

            migrationBuilder.DropTable(
                name: "EconomyTransactions");

            migrationBuilder.DropTable(
                name: "MarketOrders");

            migrationBuilder.DropTable(
                name: "MarketPriceCandles");

            migrationBuilder.DropTable(
                name: "PlayerBalances");

            migrationBuilder.DropTable(
                name: "PlayerMachineStocks");

            migrationBuilder.DropTable(
                name: "PlayerSessions");

            migrationBuilder.DropTable(
                name: "PoolStacks");

            migrationBuilder.DropTable(
                name: "SimulationClock");

            migrationBuilder.DropTable(
                name: "SponsorCompanyOrders");

            migrationBuilder.DropTable(
                name: "TradeExecutions");

            migrationBuilder.DropTable(
                name: "Boards");

            migrationBuilder.DropTable(
                name: "InventoryPools");

            migrationBuilder.DropTable(
                name: "SponsorCompanies");

            migrationBuilder.DropTable(
                name: "Players");
        }
    }
}
