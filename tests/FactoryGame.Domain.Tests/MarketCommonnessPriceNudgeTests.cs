using FactoryGame.Domain.Market;

namespace FactoryGame.Domain.Tests;

public sealed class MarketCommonnessPriceNudgeTests
{
    [Fact]
    public void ComputeCommonnessScores_rare_has_low_score()
    {
        var scores = MarketCommonnessPriceNudge.ComputeCommonnessScores(
            [1, 2, 3],
            new Dictionary<int, long> { [1] = 100, [2] = 10, [3] = 50 });

        Assert.Equal(1m, scores[1]);
        Assert.Equal(0.1m, scores[2]);
        Assert.Equal(0.5m, scores[3]);
    }

    [Fact]
    public void ComputeMultiplier_rare_is_above_one_common_below_one()
    {
        var rare = MarketCommonnessPriceNudge.ComputeMultiplier(0m, 0.04m);
        var common = MarketCommonnessPriceNudge.ComputeMultiplier(1m, 0.04m);
        var neutral = MarketCommonnessPriceNudge.ComputeMultiplier(0.5m, 0.04m);

        Assert.True(rare > 1m);
        Assert.True(common < 1m);
        Assert.Equal(1m, neutral);
    }

    [Fact]
    public void ApplyToPrice_rounds_to_cents()
    {
        var result = MarketCommonnessPriceNudge.ApplyToPrice(10.00m, 1.015m);
        Assert.Equal(10.15m, result);
    }
}
