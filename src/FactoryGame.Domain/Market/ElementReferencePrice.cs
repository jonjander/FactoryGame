namespace FactoryGame.Domain.Market;

/// <summary>Deterministic reference price from element DNA (no per-id hardcoding).</summary>
public static class ElementReferencePrice
{
    private const decimal MinPrice = 8m;
    private const decimal MaxPrice = 240m;

    public static decimal Compute(long dna)
    {
        var u = (ulong)dna;
        var mixed = u ^ (u >> 32) ^ (u >> 16);
        var fraction = (mixed % 10_000) / 10_000m;
        return MinPrice + (MaxPrice - MinPrice) * fraction;
    }
}
