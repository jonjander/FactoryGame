using FactoryGame.Domain.Content;

namespace FactoryGame.Domain.Simulation;

/// <summary>Pool withdrawals/deposits during one board tick, keyed by element + DNA variant.</summary>
public sealed class SeaportTickDelta
{
    public Dictionary<string, decimal> WithdrawnFromPool { get; set; } = new();

    public Dictionary<string, decimal> DepositedToPool { get; set; } = new();

    public void AddWithdraw(int elementId, long dna, decimal qty) =>
        Add(WithdrawnFromPool, elementId, dna, qty);

    public void AddDeposit(int elementId, long dna, decimal qty) =>
        Add(DepositedToPool, elementId, dna, qty);

    public IEnumerable<KeyValuePair<PoolStackKey, decimal>> WithdrawVariants() =>
        EnumerateVariants(WithdrawnFromPool);

    public IEnumerable<KeyValuePair<PoolStackKey, decimal>> DepositVariants() =>
        EnumerateVariants(DepositedToPool);

    public static string VariantKey(int elementId, long dna) => $"{elementId}:{dna}";

    public static PoolStackKey ParseVariantKey(string key)
    {
        var sep = key.IndexOf(':');
        if (sep < 0)
        {
            var elementId = int.Parse(key);
            return new PoolStackKey(elementId, ElementCatalogLookup.CatalogDnaFor(elementId));
        }

        var elementIdPart = int.Parse(key.AsSpan(0, sep));
        var dnaPart = long.Parse(key.AsSpan(sep + 1));
        return new PoolStackKey(elementIdPart, dnaPart);
    }

    private static void Add(Dictionary<string, decimal> dict, int elementId, long dna, decimal qty)
    {
        if (qty <= 0)
            return;

        var key = VariantKey(elementId, dna);
        dict[key] = dict.GetValueOrDefault(key) + qty;
    }

    private static IEnumerable<KeyValuePair<PoolStackKey, decimal>> EnumerateVariants(
        Dictionary<string, decimal> dict)
    {
        foreach (var (key, qty) in dict)
        {
            if (qty <= 0)
                continue;
            yield return KeyValuePair.Create(ParseVariantKey(key), qty);
        }
    }
}
