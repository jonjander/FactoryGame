namespace FactoryGame.Infrastructure.Options;

public sealed class MarketLiquidityOptions
{
    public const string SectionName = "MarketLiquidity";

    public bool Enabled { get; set; } = true;

    /// <summary>When false, periodic/startup refresh is skipped (integration tests).</summary>
    public bool BackgroundRefreshEnabled { get; set; } = true;

    /// <summary>Fixed NPC market-maker player id.</summary>
    public Guid SystemPlayerId { get; set; } = Guid.Parse("00000000-0000-4000-8000-000000000001");

    public int LevelsPerSide { get; set; } = 5;

    /// <summary>Spread step per level as fraction of mid (0.005 = 0.5%).</summary>
    public decimal SpreadStepFraction { get; set; } = 0.005m;

    public long MinLotSize { get; set; } = 25;

    public long MaxLotSize { get; set; } = 75;

    /// <summary>Max synthetic qty relative to visible player depth when player orders exist.</summary>
    public decimal CapRatio { get; set; } = 0.30m;

    public int HistoryCandlePoints { get; set; } = 48;

    public int HistoryTradeSamples { get; set; } = 30;

    public int SeedVersion { get; set; } = 1;

    public decimal SystemCash { get; set; } = 1_000_000_000m;

    public long SystemPoolQuantityPerElement { get; set; } = 1_000_000;

    public int RefreshIntervalMinutes { get; set; } = 15;

    /// <summary>When false, GET /summary does not rebuild synthetic books (use depth or background refresh).</summary>
    public bool RefreshOnSummaryRequest { get; set; } = false;

    /// <summary>Skip full synthetic ladder rebuild if open synthetics were created within this window.</summary>
    public int ElementRefreshCooldownMinutes { get; set; } = 15;

    /// <summary>Cap work per background tick across all pooled elements.</summary>
    public int MaxElementsPerBackgroundRefresh { get; set; } = 25;

    /// <summary>Skew prices from global pool holdings every refresh (rare up, common down).</summary>
    public bool CommonnessDriftEnabled { get; set; } = true;

    /// <summary>Max fractional skew at extremes (0.03 = ±1.5% vs neutral element).</summary>
    public decimal CommonnessDriftMaxFraction { get; set; } = 0.03m;

    /// <summary>Extra ± jitter per element per refresh bucket so prices feel alive.</summary>
    public decimal AliveDriftMaxFraction { get; set; } = 0.002m;
}
