using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Simulation;

public enum MachineInputFit
{
    Good,
    Limited,
    Blocked
}

public readonly record struct MachineInputSuitability(
    string MachineType,
    MachineInputFit Fit,
    string? Note);

/// <summary>Assesses whether material DNA is a good direct input for each machine type (default machine settings).</summary>
public static class MachineInputCompatibility
{
    private const int MinSpreadPermille = 220;
    private const int DefaultCutBoil = 2048;
    private const int DefaultCutFreeze = 2048;
    private const int DefaultMeltHeatPerPass = 20;
    private const int DefaultChillPerPass = 16;
    private const int SlowProcessPasses = 10;

    private static readonly string[] InputMachineTypes =
    [
        "Boiler",
        "Heater",
        "Cooler",
        "Condenser",
        "Crystallizer",
        "Melter",
        "Mixer",
        "GasMixer",
        "Burner",
        "Destilator",
        "LiquidSeparator",
        "Sorter",
        "Tank",
        "Junction",
        "RateLimiter",
        "SeaportConnector"
    ];

    public static IReadOnlyList<MachineInputSuitability> AssessElementInput(long dna, int elementId)
    {
        var results = new List<MachineInputSuitability>(InputMachineTypes.Length);
        foreach (var machineType in InputMachineTypes)
        {
            if (!MachinePortCatalog.GetPorts(machineType).Any(p => p.Direction == PortDirection.In))
                continue;

            var fit = Assess(machineType, dna, elementId);
            var note = fit switch
            {
                MachineInputFit.Blocked => GetPlayerBlockReason(machineType, dna),
                MachineInputFit.Limited => GetLimitedReason(machineType, dna, elementId),
                _ => null
            };
            results.Add(new MachineInputSuitability(machineType, fit, note));
        }

        return results
            .OrderBy(r => r.Fit)
            .ThenBy(r => r.MachineType, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static MachineInputFit Assess(string machineType, long dna, int elementId)
    {
        if (GetPlayerBlockReason(machineType, dna) != null)
            return MachineInputFit.Blocked;

        return machineType switch
        {
            "Melter" => AssessMelter(dna),
            "Crystallizer" => AssessCrystallizer(dna),
            "Condenser" => AssessCondenser(dna),
            "Destilator" => AssessDestilator(dna),
            "Sorter" => MachineInputFit.Limited,
            _ => MachineInputFit.Good
        };
    }

    public static string? GetPlayerBlockReason(string machineType, long dna)
    {
        var d = DnaDecoder.Decode(dna);
        var t = machineType.Trim();

        if (t.Equals("Boiler", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Liquid)
            return "Requires liquid phase — melt solids with Melter first.";

        if (t.Equals("LiquidSeparator", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Liquid)
            return "Requires liquid phase.";

        if (t.Equals("Destilator", StringComparison.OrdinalIgnoreCase) && d.Phase == MaterialPhase.Solid)
            return "Blocked by solid phase — melt or pick liquid/gas.";

        if (t.Equals("Condenser", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Gas)
            return "Requires gas phase — heat or boil into vapour first.";

        if (t.Equals("Crystallizer", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Liquid)
            return "Requires liquid phase.";

        if (t.Equals("Melter", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Solid)
            return "Requires solid phase.";

        if (t.Equals("GasMixer", StringComparison.OrdinalIgnoreCase) && d.Phase != MaterialPhase.Gas)
            return "Requires gas phase — boil or distil light fractions first.";

        if (t.Equals("Burner", StringComparison.OrdinalIgnoreCase))
        {
            if (d.Phase != MaterialPhase.Gas)
                return "Requires gas phase.";
            if (d.Flammability < 40)
                return "Too inert to ignite — needs a more flammable gas.";
            if (d.Explosivity > 90)
                return "Too explosive for a controlled flare.";
        }

        if (t.Equals("Heater", StringComparison.OrdinalIgnoreCase) && d.Explosivity > 85)
            return "Explosivity too high for safe heating.";

        if (t.Equals("Cooler", StringComparison.OrdinalIgnoreCase) && d.Toxicity > 90)
            return "Toxicity too high — fouls the heat exchanger.";

        return null;
    }

    private static string? GetLimitedReason(string machineType, long dna, int elementId)
    {
        if (machineType.Equals("Sorter", StringComparison.OrdinalIgnoreCase))
            return $"Routes to the rest port unless you assign element {elementId} to port 1–3.";

        if (machineType.Equals("Melter", StringComparison.OrdinalIgnoreCase))
            return GetMelterLimitedReason(dna);

        if (machineType.Equals("Crystallizer", StringComparison.OrdinalIgnoreCase))
            return GetCrystallizerLimitedReason(dna);

        if (machineType.Equals("Condenser", StringComparison.OrdinalIgnoreCase))
            return GetCondenserLimitedReason(dna);

        if (machineType.Equals("Destilator", StringComparison.OrdinalIgnoreCase))
            return GetDestilatorLimitedReason(dna);

        return null;
    }

    private static MachineInputFit AssessCondenser(long dna)
    {
        var d = DnaDecoder.Decode(dna);
        if (d.Phase != MaterialPhase.Gas)
            return MachineInputFit.Blocked;

        var need = Math.Max(0, d.BoilingPoint - DefaultCutBoil);
        if (need > DefaultChillPerPass * SlowProcessPasses)
            return MachineInputFit.Limited;

        return MachineInputFit.Good;
    }

    private static MachineInputFit AssessDestilator(long dna)
    {
        if (DnaDecoder.Decode(dna).Phase == MaterialPhase.Solid)
            return MachineInputFit.Blocked;

        return MachineInputFit.Good;
    }

    private static string? GetCondenserLimitedReason(long dna)
    {
        var d = DnaDecoder.Decode(dna);
        var need = Math.Max(0, d.BoilingPoint - DefaultCutBoil);
        if (need > DefaultChillPerPass * SlowProcessPasses)
            return "High boiling point — many chill cycles before it becomes liquid.";

        return null;
    }

    private static string? GetDestilatorLimitedReason(long dna)
    {
        var phase = DnaDecoder.Decode(dna).Phase;
        if (phase == MaterialPhase.Gas)
            return "Gas in — light fraction stays gas on out2, heavy condenses to liquid on out1.";

        return null;
    }

    private static MachineInputFit AssessMelter(long dna)
    {
        var spread = DnaTransforms.MeasureDnaSpreadPermille(dna);
        if (spread < MinSpreadPermille)
            return MachineInputFit.Limited;

        var d = DnaDecoder.Decode(dna);
        var need = Math.Max(0, DefaultCutBoil - d.BoilingPoint);
        if (need > DefaultMeltHeatPerPass * SlowProcessPasses)
            return MachineInputFit.Limited;

        return MachineInputFit.Good;
    }

    private static MachineInputFit AssessCrystallizer(long dna)
    {
        var d = DnaDecoder.Decode(dna);
        var spread = DnaTransforms.MeasureDnaSpreadPermille(dna);
        if (spread < MinSpreadPermille)
            return MachineInputFit.Limited;

        var need = Math.Max(0, d.FreezePoint - DefaultCutFreeze);
        if (need > DefaultChillPerPass * SlowProcessPasses)
            return MachineInputFit.Limited;

        return MachineInputFit.Good;
    }

    private static string? GetMelterLimitedReason(long dna)
    {
        var spread = DnaTransforms.MeasureDnaSpreadPermille(dna);
        if (spread < MinSpreadPermille)
            return "Compact solid — passes through without melting.";

        var d = DnaDecoder.Decode(dna);
        var need = Math.Max(0, DefaultCutBoil - d.BoilingPoint);
        if (need > DefaultMeltHeatPerPass * SlowProcessPasses)
            return "Boiling point far below default cut — many heat cycles before it pours.";

        return null;
    }

    private static string? GetCrystallizerLimitedReason(long dna)
    {
        var spread = DnaTransforms.MeasureDnaSpreadPermille(dna);
        if (spread < MinSpreadPermille)
            return "Stable liquid — passes through without crystallizing.";

        var d = DnaDecoder.Decode(dna);
        var need = Math.Max(0, d.FreezePoint - DefaultCutFreeze);
        if (need > DefaultChillPerPass * SlowProcessPasses)
            return "Freeze point far above default cut — many chill cycles before solid forms.";

        return null;
    }
}
