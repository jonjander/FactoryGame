namespace FactoryGame.Web;

public sealed record MachineSettingOption(int Value, string Label);

public sealed record MachineSettingField(string JsonKey, string Label, IReadOnlyList<MachineSettingOption> Options);

/// <summary>Discrete machine settings for factory UI (dropdowns only).</summary>
public static class MachineSettingsUi
{
    private static readonly MachineSettingOption[] HeatPower =
    [
        new(4, "Låg"),
        new(8, "Medium"),
        new(16, "Hög"),
        new(24, "Max")
    ];

    private static readonly MachineSettingOption[] CoolPower =
    [
        new(4, "Låg"),
        new(8, "Medium"),
        new(12, "Hög"),
        new(16, "Max")
    ];

    private static readonly MachineSettingOption[] CutBand =
    [
        new(1024, "Låg"),
        new(2048, "Medium"),
        new(3072, "Hög")
    ];

    private static readonly MachineSettingOption[] MixIntensity =
    [
        new(350, "Fattig"),
        new(600, "Balanserad"),
        new(850, "Ostabil")
    ];

    private static readonly MachineSettingOption[] MixRatio =
    [
        new(250, "25 % in1"),
        new(500, "50 / 50"),
        new(750, "75 % in1")
    ];

    private static readonly MachineSettingOption[] Reflux =
    [
        new(0, "Av"),
        new(150, "Låg"),
        new(300, "Hög")
    ];

    private static readonly MachineSettingOption[] Chill =
    [
        new(8, "Långsam"),
        new(16, "Normal"),
        new(32, "Snabb")
    ];

    private static readonly MachineSettingOption[] MeltHeat =
    [
        new(12, "Låg"),
        new(20, "Normal"),
        new(32, "Kraftig")
    ];

    private static readonly MachineSettingOption[] Condense =
    [
        new(8, "Mild"),
        new(12, "Normal"),
        new(20, "Kraftig")
    ];

    public static IReadOnlyList<MachineSettingField> GetFields(string machineType)
    {
        var t = machineType.Trim();
        if (t.Equals("Boiler", StringComparison.OrdinalIgnoreCase))
            return [new("heatDelta", "Värme", HeatPower)];
        if (t.Equals("Heater", StringComparison.OrdinalIgnoreCase))
            return [new("heatDelta", "Värme", HeatPower)];
        if (t.Equals("Cooler", StringComparison.OrdinalIgnoreCase))
            return [new("coolDelta", "Kylning", CoolPower)];
        if (t.Equals("Condenser", StringComparison.OrdinalIgnoreCase))
            return [new("condenseDelta", "Kondensering", Condense)];
        if (t.Equals("Crystallizer", StringComparison.OrdinalIgnoreCase))
            return
            [
                new("cutFreeze", "Frys-cut", CutBand),
                new("chillDelta", "Kylhastighet", Chill)
            ];
        if (t.Equals("Melter", StringComparison.OrdinalIgnoreCase))
            return
            [
                new("cutBoiling", "Smält-cut", CutBand),
                new("heatDelta", "Smälteffekt", MeltHeat)
            ];
        if (t.Equals("Mixer", StringComparison.OrdinalIgnoreCase))
            return
            [
                new("mixIntensityPermille", "Blandning", MixIntensity),
                new("ratioPermille", "Ratio in1", MixRatio)
            ];
        if (t.Equals("Destilator", StringComparison.OrdinalIgnoreCase))
            return
            [
                new("cutBoiling", "Kok-cut", CutBand),
                new("refluxPermille", "Reflux", Reflux)
            ];
        if (t.Equals("LiquidSeparator", StringComparison.OrdinalIgnoreCase))
            return [new("cutFreeze", "Separations-cut", CutBand)];
        if (t.Equals("Sorter", StringComparison.OrdinalIgnoreCase))
            return
            [
                new("port1", "Port 1", []),
                new("port2", "Port 2", []),
                new("port3", "Port 3", [])
            ];
        if (t.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase)
            || t.Equals("SeaportIn", StringComparison.OrdinalIgnoreCase))
            return [new("outElementId", "Ut-element (pool)", [])];

        return Array.Empty<MachineSettingField>();
    }

    public static bool UsesElementPicker(MachineSettingField field) =>
        field.JsonKey is "outElementId" or "port1" or "port2" or "port3";

    public static int GetEffectiveValue(MachineSettingField field, int? stored) =>
        stored ?? field.Options[0].Value;

    public static IReadOnlyList<MachineSettingOption> ElementOptions(
        IReadOnlyList<Models.ElementContentItem> elements,
        bool allowEmpty,
        IReadOnlySet<int>? ownedElementIds = null)
    {
        var list = new List<MachineSettingOption>();
        if (allowEmpty)
            list.Add(new(0, "(ingenting)"));
        foreach (var el in elements.OrderBy(e => e.Id))
        {
            if (ownedElementIds != null && !ownedElementIds.Contains(el.Id))
                continue;
            list.Add(new(el.Id, $"{el.Symbol} — {el.Name}"));
        }
        return list;
    }

    public static IReadOnlyList<PoolVariantOption> PoolVariantOptions(
        IReadOnlyList<FactoryGame.Contracts.Pool.PoolVariantStackDto> variants,
        bool allowEmpty)
    {
        var list = new List<PoolVariantOption>();
        if (allowEmpty)
            list.Add(new(0, 0, "(ingenting)"));
        foreach (var v in variants.OrderBy(x => x.ElementId).ThenBy(x => x.Phase, StringComparer.Ordinal))
        {
            list.Add(new(v.ElementId, v.Dna, $"{v.Symbol} — {v.PhaseLabel} ({v.Quantity})"));
        }
        return list;
    }
}

public sealed record PoolVariantOption(int ElementId, long Dna, string Label);
