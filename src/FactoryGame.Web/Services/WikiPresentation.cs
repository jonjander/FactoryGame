using FactoryGame.Web.Models;

namespace FactoryGame.Web.Services;

public static class WikiPresentation
{
    public static string MachineDisplayName(string type) => type switch
    {
        "Boiler" => "Liquid boiler",
        "LiquidSeparator" => "Liquid separator",
        "Destilator" => "Distillator",
        "Mixer" => "Mixer",
        "Heater" => "Heater",
        "Cooler" => "Cooler",
        "Condenser" => "Condenser",
        "Crystallizer" => "Crystallizer",
        "Melter" => "Melter",
        "Sorter" => "Sorter",
        "Tank" => "Tank",
        "Junction" => "Junction",
        "RateLimiter" => "Rate limiter",
        "SeaportConnector" => "Seaport connector",
        "SeaportIn" => "Seaport in (legacy)",
        "SeaportOut" => "Seaport out (legacy)",
        _ => type
    };

    public static string MachineCategory(string type) => type switch
    {
        "Boiler" or "Heater" or "Cooler" or "Melter" => "heat",
        "Condenser" or "Crystallizer" => "phase",
        "Mixer" or "Destilator" or "LiquidSeparator" or "Sorter" => "separation",
        "Tank" or "Junction" or "RateLimiter" or "SeaportConnector" or "SeaportIn" or "SeaportOut" => "logistics",
        _ => "other"
    };

    public static string MachineCategoryLabel(string category) => category switch
    {
        "heat" => "Heat & melting",
        "phase" => "Phase change",
        "separation" => "Separation & sorting",
        "logistics" => "Seaport & logistics",
        _ => "Other"
    };

    public static string MachineEmoji(string type) => type switch
    {
        "Boiler" or "Heater" => "🔥",
        "Cooler" => "❄️",
        "Condenser" => "💧",
        "Crystallizer" => "🧊",
        "Melter" => "🫠",
        "Mixer" => "🌀",
        "Destilator" or "LiquidSeparator" => "⚗️",
        "Sorter" => "🔀",
        "Tank" => "🛢️",
        "Junction" => "⑂",
        "RateLimiter" => "🚦",
        "SeaportConnector" or "SeaportIn" or "SeaportOut" => "🚢",
        _ => "⚙️"
    };

    public static string ExtendedSummary(string type, string apiSummary) =>
        string.IsNullOrWhiteSpace(GetExtendedSummary(type))
            ? apiSummary
            : GetExtendedSummary(type);

    public static IReadOnlyList<string> MachineTips(string type) => type switch
    {
        "Boiler" =>
        [
            "Requires liquid phase — gas will not pass through.",
            "Raises temperature band in DNA deterministically.",
            "Connect seaport out -> boiler in for a simple starter loop."
        ],
        "Heater" =>
        [
            "Increases energy/temperature band step by step.",
            "Be careful with highly explosive materials.",
            "Good before separation that requires heat."
        ],
        "Cooler" =>
        [
            "Lowers energy/temperature band.",
            "Toxic materials can block cooling.",
            "Use before condensation or crystallization."
        ],
        "Condenser" =>
        [
            "Requires gas phase — never solid input.",
            "Symbol may stay the same; phase (gas->liquid) shows in the pool.",
            "Output material is always liquid."
        ],
        "Crystallizer" =>
        [
            "Freezes unstable/spread-out liquid into solid form.",
            "Never outputs gas.",
            "Compact solids often pass through unchanged."
        ],
        "Melter" =>
        [
            "Melts spread-out solid to liquid via boiling band.",
            "Compact solids may pass through unchanged.",
            "Good intermediate step before liquid processes."
        ],
        "Mixer" =>
        [
            "Two inputs — ratio and intensity control DNA.",
            "Low intensity -> compact, stable DNA.",
            "High intensity -> volatile DNA for distillation."
        ],
        "Destilator" =>
        [
            "Separates into two fractions based on boiling point.",
            "Requires liquid or gas phase.",
            "Set reflux and cut for desired fractions."
        ],
        "LiquidSeparator" =>
        [
            "Liquids only — cut controls out1 vs out2.",
            "Simpler than distillator when you only need to split flow.",
            "Cut near the middle gives more even distribution."
        ],
        "Sorter" =>
        [
            "Configure base elements on ports 1-3.",
            "Everything unmatched goes to the rest port.",
            "Check that downstream tolerates the element DNA."
        ],
        "SeaportConnector" =>
        [
            "Out from pool -> in to factory.",
            "In to pool stores production per DNA variant.",
            "Pick the right phase (gas vs liquid) in settings."
        ],
        _ => ["Place from machine inventory.", "Save the plan after wiring.", "Start the factory when the loop is ready."]
    };

    public static IReadOnlyList<(string In, string Out)> ParsePortRatio(string ports)
    {
        var parts = ports.Split(':', 2);
        if (parts.Length != 2)
            return [(ports, "")];

        return [(parts[0].Trim(), parts[1].Trim())];
    }

    /// <summary>Parses wiki port spec <c>in[,in2]:out[,out2]</c> (canonical) or legacy <c>1:1</c> counts.</summary>
    public static (string[] InPorts, string[] OutPorts) ParsePortSpec(string ports)
    {
        var parts = ports.Split(':', 2);
        if (parts.Length != 2)
            return ([ports.Trim()], []);

        var inPart = parts[0].Trim();
        var outPart = parts[1].Trim();
        if (inPart.Contains(',') || outPart.Contains(','))
        {
            return (
                inPart.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                outPart.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        var legacy = ParsePortRatio(ports).FirstOrDefault();
        var inLabel = string.IsNullOrEmpty(legacy.In) ? "in" : $"in x{legacy.In}";
        var outLabel = string.IsNullOrEmpty(legacy.Out) ? "out" : $"out x{legacy.Out}";
        return ([inLabel], [outLabel]);
    }

    /// <summary>Short label for wiki icon/diagram (strips legacy count suffix).</summary>
    public static string PortDisplayLabel(string portLabel)
    {
        var s = portLabel.Trim();
        var times = s.IndexOf(" x", StringComparison.Ordinal);
        return times > 0 ? s[..times] : s;
    }

    public static string PhaseCssClass(string phase) => phase.ToLowerInvariant() switch
    {
        "liquid" => "fg-phase-liquid",
        "gas" => "fg-phase-gas",
        _ => "fg-phase-solid"
    };

    public static string PhaseLabel(string phase) => phase switch
    {
        "Liquid" => "Liquid",
        "Gas" => "Gas",
        _ => "Solid"
    };

    public static string DailyTip(IReadOnlyList<WikiMachineItem> machines)
    {
        if (machines.Count == 0)
            return "The wiki is generated live from the same rule data as the server — no manual pages.";

        var day = DateTime.UtcNow.DayOfYear;
        var machine = machines[day % machines.Count];
        return $"Machine tip of the day ({MachineEmoji(machine.Type)} {MachineDisplayName(machine.Type)}): {MachineTips(machine.Type)[0]}";
    }

    private static string GetExtendedSummary(string type) => type switch
    {
        "Boiler" => "Raises temperature in the material DNA with a bitwise mask — liquids get warmer and can be prepared for separation.",
        "Heater" => "Deterministic heating of energy/temperature band. Simpler than the boiler but same phase rules.",
        "Cooler" => "Deterministic cooling. Lowers temperature band — some toxic materials can block the process.",
        "Condenser" => "Converts gas to liquid by lowering the boiling-point band. Output is always liquid.",
        "Crystallizer" => "Unstable liquid crystallizes to solid form via freeze-point band. Never gas out.",
        "Melter" => "Spread-out solid melts to liquid via boiling band. Compact solid may pass through.",
        "Mixer" => "Mixes two streams. Intensity and ratio control whether DNA becomes compact or volatile.",
        "Destilator" => "Fractionates material into two outputs based on boiling point and reflux.",
        "LiquidSeparator" => "Splits liquid into two outputs at the chosen cut — faster than full distillation.",
        "Sorter" => "Routes selected base elements to ports 1-3; the rest go to the rest port.",
        "SeaportConnector" => "Bridge between factory and your seaport pool — material in/out per DNA variant.",
        _ => ""
    };
}
