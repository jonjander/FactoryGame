namespace FactoryGame.Infrastructure.Options;

public sealed class SponsorCompanyOptions
{
    public const string SectionName = "SponsorCompany";

    public bool Enabled { get; set; } = true;

    public bool BackgroundRefreshEnabled { get; set; } = true;

    public int RefreshIntervalMinutes { get; set; } = 2;

    public decimal DefaultStartingCash { get; set; } = 500_000m;

    public long DefaultPoolMaxVolume { get; set; } = 500_000;

    public long DefaultSellPoolQuantity { get; set; } = 10_000;

    /// <summary>Max trades per hour per exposure tier (index 0 = tier 1).</summary>
    public int[] MaxTradesPerHourByTier { get; set; } = [2, 5, 10, 20, 50];

    /// <summary>Max lot size per exposure tier (index 0 = tier 1).</summary>
    public long[] MaxLotSizeByTier { get; set; } = [25, 50, 100, 200, 500];
}
