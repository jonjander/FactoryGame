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
        "GasMixer" => "Gas mixer",
        "Burner" => "Burner",
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
        _ => type
    };

    public static string MachineCategory(string type) => type switch
    {
        "Boiler" or "Heater" or "Cooler" or "Melter" => "heat",
        "Condenser" or "Crystallizer" => "phase",
        "Mixer" or "GasMixer" or "Destilator" or "LiquidSeparator" or "Sorter" => "separation",
        "Burner" => "heat",
        "Tank" or "Junction" or "RateLimiter" or "SeaportConnector" => "logistics",
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
        "Boiler" or "Heater" or "Burner" => "🔥",
        "Cooler" => "❄️",
        "Condenser" => "💧",
        "Crystallizer" => "🧊",
        "Melter" => "🫠",
        "Mixer" => "🌀",
        "GasMixer" => "💨",
        "Destilator" or "LiquidSeparator" => "⚗️",
        "Sorter" => "🔀",
        "Tank" => "🛢️",
        "Junction" => "⑂",
        "RateLimiter" => "🚦",
        "SeaportConnector" => "🚢",
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
            "Circulates steam through the coil until the batch is hot enough for separation.",
            "Connect seaport out -> boiler in for a simple starter loop."
        ],
        "Heater" =>
        [
            "Adds heat in steady steps through direct coils.",
            "Be careful with highly explosive materials.",
            "Good before separation that requires heat."
        ],
        "Cooler" =>
        [
            "Pulls heat out of the stream through a heat exchanger.",
            "Toxic materials can block cooling.",
            "Use before condensation or crystallization."
        ],
        "Condenser" =>
        [
            "Requires gas phase — never solid or liquid input.",
            "Chilled coil converts vapour to liquid for pool or downstream machines.",
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
            "Spread-out solid needs sustained furnace heat before it pours.",
            "Compact solids may pass through unchanged.",
            "Good intermediate step before liquid processes."
        ],
        "Mixer" =>
        [
            "Two inputs — ratio and mixing intensity shape how stable the blend is.",
            "Gentle mix -> stable, compact blend.",
            "Hard mix -> volatile blend ready for distillation."
        ],
        "GasMixer" =>
        [
            "Both inputs must be gas.",
            "Blends vapours without waking volatile fractions — output stays gas.",
            "Good after Destilator out2 or before Condenser on mixed gas."
        ],
        "Burner" =>
        [
            "Gas only — consumes the stream completely (no output).",
            "Needs moderate flammability; too inert or too explosive will block.",
            "Use as a sink for surplus or hazardous vapours you cannot store."
        ],
        "Destilator" =>
        [
            "Separates into two fractions based on boiling point.",
            "Accepts gas or liquid — gas yields light gas on out2 and heavy liquid on out1.",
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
            "Check that downstream tolerates the incoming material."
        ],
        "SeaportConnector" =>
        [
            "Out from pool -> in to factory.",
            "In to pool stores production per material variant.",
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
            return "Search for a machine, element symbol, or phase to get started.";

        var day = DateTime.UtcNow.DayOfYear;
        var machine = machines[day % machines.Count];
        return $"Machine tip of the day ({MachineEmoji(machine.Type)} {MachineDisplayName(machine.Type)}): {MachineTips(machine.Type)[0]}";
    }

    private static string GetExtendedSummary(string type) => type switch
    {
        "Boiler" => "Pressurized liquid boiler — circulates heat until liquids are warm enough for separation and refining.",
        "Heater" => "Direct heat coils for steady warming. Simpler than the boiler but same phase rules.",
        "Cooler" => "Heat exchanger that strips energy from the stream. Some toxic materials can foul the coils.",
        "Condenser" => "Chilled coil converts gas to liquid. Output is always liquid.",
        "Crystallizer" => "Supercooled bath locks unruly liquid into solid crystal. Never gas out.",
        "Melter" => "Induction furnace melts spread-out solid into pourable liquid. Compact solid may pass through.",
        "Mixer" => "Blends two streams. Mix intensity and ratio decide whether the blend stays stable or turns volatile.",
        "GasMixer" => "Blends two gas streams into one stable vapour — always gas out.",
        "Burner" => "Controlled flare — burns moderately flammable gas completely with no residue.",
        "Destilator" => "Fractionates gas or liquid into heavy liquid (out1) and light gas (out2).",
        "LiquidSeparator" => "Splits liquid into two fractions at the chosen cut — faster than full distillation.",
        "Sorter" => "Routes selected base elements to ports 1-3; the rest go to the rest port.",
        "SeaportConnector" => "Bridge between factory and your seaport pool — material in/out per variant.",
        _ => ""
    };
}
