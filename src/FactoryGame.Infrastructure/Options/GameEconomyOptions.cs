namespace FactoryGame.Infrastructure.Options;

public sealed class GameEconomyOptions
{
    public const string SectionName = "GameEconomy";

    public decimal StartingCash { get; set; } = 25000m;

    public long PoolMaxVolume { get; set; } = 10000;

    public decimal BaseIncomeAmount { get; set; } = 10m;

    public int BaseIncomeIntervalMinutes { get; set; } = 5;

    /// <summary>MVP: grant new players starter material for market testing.</summary>
    public int? DevStartingElementId { get; set; } = 1;

    public long DevStartingElementQuantity { get; set; } = 100;

    public decimal MachinePlacementCost { get; set; } = 100m;

    public int SimulationTickIntervalSeconds { get; set; } = 1;

    public int SimulationMaxCatchUpTicks { get; set; } = 20;
}
