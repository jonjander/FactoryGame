namespace FactoryGame.Domain.Content;

/// <summary>Catalog of purchasable machines (display name, canonical type, price).</summary>
public static class MachineStoreCatalog
{
    public static IReadOnlyList<MachineStoreEntry> All { get; } =
    [
        new("Boiler", "Liquid boiler", 4000m),
        new("Mixer", "Mixer", 2500m),
        new("Heater", "Heater", 1800m),
        new("Cooler", "Cooler", 1800m),
        new("LiquidSeparator", "Liquid separator", 3200m),
        new("Destilator", "Destilator", 3500m),
        new("Sorter", "Sorter", 5000m)
    ];

    public static bool TryGetCanonicalType(string machineTypeOrAlias, out string canonicalType)
    {
        canonicalType = "";
        var t = machineTypeOrAlias.Trim();
        if (string.IsNullOrEmpty(t))
            return false;
        var hit = All.FirstOrDefault(e =>
            e.MachineType.Equals(t, StringComparison.OrdinalIgnoreCase));
        if (hit == null)
            return false;
        canonicalType = hit.MachineType;
        return true;
    }

    public static MachineStoreEntry? TryGetEntry(string canonicalType)
    {
        var t = canonicalType.Trim();
        return All.FirstOrDefault(e => e.MachineType.Equals(t, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record MachineStoreEntry(string MachineType, string DisplayName, decimal Price);
