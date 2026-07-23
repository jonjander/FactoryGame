namespace FactoryGame.Domain.Simulation;

/// <summary>Per-machine throughput and port conversion ratios (permille: 1000 = 1 unit/tick baseline).</summary>
public static class MachineRateCatalog
{
    public const int DefaultOperationRatePermille = 1000;

    private static readonly IReadOnlyDictionary<string, MachineRateProfile> Profiles =
        new Dictionary<string, MachineRateProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["Boiler"] = Profile(1000, In("in", 1000), Out("out", 1000), opRate: true, timeDna: true),
            ["Heater"] = Profile(800, In("in", 1000), Out("out", 1000), opRate: true, timeDna: true),
            ["Cooler"] = Profile(600, In("in", 1000), Out("out", 1000), opRate: true, timeDna: true),
            ["Condenser"] = Profile(700, In("in", 1000), Out("out", 1000), opRate: true, timeDna: true),
            ["Crystallizer"] = Profile(650, In("in", 1000), Out("out", 1000), opRate: true, timeDna: true),
            ["Melter"] = Profile(550, In("in", 1000), Out("out", 1000), opRate: true, timeDna: true),
            ["Mixer"] = Profile(500, In("in1", 1000, "in2", 1000), Out("out", 800), opRate: true, timeDna: false),
            ["Destilator"] = Profile(700, In("in", 1000), Out("out1", 280, "out2", 420), opRate: true, timeDna: false),
            ["LiquidSeparator"] = Profile(650, In("in", 1000), Out("out1", 390, "out2", 260), opRate: true, timeDna: false),
            ["Sorter"] = Profile(900, In("in", 1000), Out("out1", 1000, "out2", 1000, "out3", 1000, "out4", 1000)),
            ["SeaportConnector"] = Profile(1000, In("in", 1000), Out("out", 1000)),
            ["Tank"] = Profile(1000, In("in", 1000), Out("out", 1000)),
            ["Junction"] = Profile(1000, In("in", 1000), Out("out1", 1000, "out2", 1000)),
            ["RateLimiter"] = Profile(1000, In("in", 1000), Out("out", 1000), rateCap: true)
        };

    public static bool TryGetProfile(string machineType, out MachineRateProfile profile) =>
        Profiles.TryGetValue(Normalize(machineType), out profile!);

    public static MachineRateProfile GetProfile(string machineType) =>
        TryGetProfile(machineType, out var p) ? p : MachineRateProfile.Default;

    public static int GetOperationRatePermille(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, DefaultOperationRatePermille, 500, 1000,
            "operationRatePermille", "operationRate");

    public static int GetMaxRateLimiterPermille(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 1000, 250, 1000,
            "maxRatePermille", "maxRate");

    public static int GetTankCapacity(string? settingsJson)
    {
        var sizeStr = MachineSettingsJson.ReadString(settingsJson, "tankSize");
        if (!string.IsNullOrEmpty(sizeStr))
        {
            return sizeStr.ToLowerInvariant() switch
            {
                "small" => 8,
                "large" => 64,
                _ => 24
            };
        }

        var sizeInt = MachineSettingsJson.ReadInt(settingsJson, 1, 0, 2, "tankSize");
        return sizeInt switch
        {
            0 => 8,
            2 => 64,
            _ => 24
        };
    }

    public static int GetEffectiveRatePermille(string machineType, string? settingsJson)
    {
        var profile = GetProfile(machineType);
        var rate = profile.BaseRatePermille;

        if (profile.SupportsRateCap)
            rate = Math.Min(rate, GetMaxRateLimiterPermille(settingsJson));

        if (profile.SupportsOperationRate)
            rate = rate * GetOperationRatePermille(settingsJson) / DefaultOperationRatePermille;

        return Math.Max(1, rate);
    }

    public static decimal GetEffectiveRateUnits(string machineType, string? settingsJson, decimal unitsPerTick) =>
        unitsPerTick * GetEffectiveRatePermille(machineType, settingsJson) / DefaultOperationRatePermille;

    public static decimal GetPortInputBudget(string machineType, string portName, string? settingsJson, decimal unitsPerTick)
    {
        var profile = GetProfile(machineType);
        if (!profile.InputPortUnits.TryGetValue(portName, out var portUnits) || portUnits <= 0)
            return 0;
        var machineRate = GetEffectiveRateUnits(machineType, settingsJson, unitsPerTick);
        return machineRate * portUnits / profile.PrimaryInputUnits;
    }

    public static decimal GetPortOutputBudget(string machineType, string portName, string? settingsJson, decimal unitsPerTick)
    {
        var profile = GetProfile(machineType);
        if (!profile.OutputPortUnits.TryGetValue(portName, out var portUnits) || portUnits <= 0)
            return 0;
        var machineRate = GetEffectiveRateUnits(machineType, settingsJson, unitsPerTick);
        var totalOut = profile.TotalOutputUnits;
        return totalOut > 0 ? machineRate * portUnits / totalOut : machineRate;
    }

    public static decimal GetConnectionTransferBudget(
        string sourceType,
        string sourcePort,
        string? sourceSettings,
        string targetType,
        string targetPort,
        string? targetSettings,
        decimal unitsPerTick)
    {
        var outBudget = GetPortOutputBudget(sourceType, sourcePort, sourceSettings, unitsPerTick);
        var inBudget = GetPortInputBudget(targetType, targetPort, targetSettings, unitsPerTick);
        if (outBudget <= 0 || inBudget <= 0)
            return 0;
        return Math.Min(outBudget, inBudget);
    }

    public static decimal GetDownstreamInputCapacity(
        SimulationPlan plan,
        string machineId,
        string outPort,
        decimal unitsPerTick)
    {
        var connections = plan.Connections.Where(c =>
            c.FromId == machineId && c.FromPort.Equals(outPort, StringComparison.Ordinal)).ToList();
        if (connections.Count == 0)
            return GetPortOutputBudget(
                plan.Machines.First(m => m.Id == machineId).Type,
                outPort,
                plan.Machines.First(m => m.Id == machineId).SettingsJson,
                unitsPerTick);

        decimal sum = 0;
        var machineById = plan.Machines.ToDictionary(m => m.Id, StringComparer.Ordinal);
        foreach (var c in connections)
        {
            if (!machineById.TryGetValue(c.ToId, out var target))
                continue;
            sum += GetPortInputBudget(target.Type, c.ToPort, target.SettingsJson, unitsPerTick);
        }
        return sum;
    }

    private static string Normalize(string machineType) => machineType.Trim();

    private static MachineRateProfile Profile(
        int baseRate,
        IReadOnlyDictionary<string, int>? inputs = null,
        IReadOnlyDictionary<string, int>? outputs = null,
        bool opRate = false,
        bool timeDna = false,
        bool rateCap = false) =>
        new(baseRate, inputs ?? EmptyPorts, outputs ?? EmptyPorts, opRate, timeDna, rateCap);

    private static readonly IReadOnlyDictionary<string, int> EmptyPorts =
        new Dictionary<string, int>(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, int> In(string port, int units, params object[] rest)
    {
        var dict = new Dictionary<string, int>(StringComparer.Ordinal) { [port] = units };
        for (var i = 0; i + 1 < rest.Length; i += 2)
            dict[(string)rest[i]] = (int)rest[i + 1];
        return dict;
    }

    private static IReadOnlyDictionary<string, int> Out(string port, int units, params object[] rest)
    {
        var dict = new Dictionary<string, int>(StringComparer.Ordinal) { [port] = units };
        for (var i = 0; i + 1 < rest.Length; i += 2)
            dict[(string)rest[i]] = (int)rest[i + 1];
        return dict;
    }
}

public sealed record MachineRateProfile(
    int BaseRatePermille,
    IReadOnlyDictionary<string, int> InputPortUnits,
    IReadOnlyDictionary<string, int> OutputPortUnits,
    bool SupportsOperationRate = false,
    bool SupportsTimeDna = false,
    bool SupportsRateCap = false)
{
    public static MachineRateProfile Default { get; } = new(1000, Empty, Empty);

    public int PrimaryInputUnits =>
        InputPortUnits.Count > 0 ? InputPortUnits.Values.Max() : 1000;

    public int TotalOutputUnits =>
        OutputPortUnits.Count > 0 ? OutputPortUnits.Values.Sum() : 1000;

    private static readonly IReadOnlyDictionary<string, int> Empty =
        new Dictionary<string, int>(StringComparer.Ordinal);
}
