namespace FactoryGame.Infrastructure.Options;

public sealed class GameEconomyOptions
{
    public const string SectionName = "GameEconomy";

    public decimal StartingCash { get; set; } = 25000m;

    public long PoolMaxVolume { get; set; } = 10000;

    /// <summary>First MVP elements granted once per player (market + factory bootstrap).</summary>
    public int[] StartingElementIds { get; set; } = [1, 2, 3, 4, 5];

    public long StartingElementQuantityPerStack { get; set; } = 25;

    /// <summary>Legacy single-element override; used only when <see cref="StartingElementIds"/> is empty.</summary>
    public int? DevStartingElementId { get; set; }

    public long DevStartingElementQuantity { get; set; } = 100;

    public IReadOnlyList<int> GetStartingElementIds()
    {
        if (StartingElementIds is { Length: > 0 })
            return StartingElementIds;

        if (DevStartingElementId is { } legacyId)
            return [legacyId];

        return [];
    }

    public decimal MachinePlacementCost { get; set; } = 100m;

    public int SimulationTickIntervalSeconds { get; set; } = 1;

    public int SimulationMaxCatchUpTicks { get; set; } = 20;
}
