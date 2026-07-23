namespace FactoryGame.Domain.Market;

/// <summary>
/// Small periodic price skew from global supply: rare materials drift up, common ones drift down.
/// </summary>
public static class MarketCommonnessPriceNudge
{
    /// <summary>
    /// Maps pool quantity to [0,1] commonness where 1 = most held globally.
    /// </summary>
    public static IReadOnlyDictionary<int, decimal> ComputeCommonnessScores(
        IEnumerable<int> allElementIds,
        IReadOnlyDictionary<int, long> globalPoolQuantityByElement)
    {
        var ids = allElementIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<int, decimal>();

        var maxQty = ids.Max(id => globalPoolQuantityByElement.GetValueOrDefault(id, 0L));
        if (maxQty <= 0)
            return ids.ToDictionary(id => id, _ => 0.5m);

        return ids.ToDictionary(
            id => id,
            id => globalPoolQuantityByElement.GetValueOrDefault(id, 0L) / (decimal)maxQty);
    }

    /// <summary>
    /// Price multiplier: rare (low commonness) &gt; 1, common (high commonness) &lt; 1.
    /// </summary>
    public static decimal ComputeMultiplier(
        decimal commonnessScore,
        decimal maxFraction,
        decimal aliveJitterFraction = 0m)
    {
        commonnessScore = Math.Clamp(commonnessScore, 0m, 1m);
        maxFraction = Math.Clamp(maxFraction, 0m, 0.2m);
        aliveJitterFraction = Math.Clamp(aliveJitterFraction, -0.05m, 0.05m);

        var skew = 0.5m - commonnessScore;
        var multiplier = 1m + maxFraction * skew + aliveJitterFraction;
        return Math.Clamp(multiplier, 0.5m, 2m);
    }

    public static decimal ApplyToPrice(decimal price, decimal multiplier) =>
        Math.Round(Math.Max(0.01m, price * multiplier), 2, MidpointRounding.AwayFromZero);
}
