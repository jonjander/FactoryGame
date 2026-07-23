using FactoryGame.Contracts.Boards;

namespace FactoryGame.Web;

public sealed record MachineCanvasStatusDisplay(
    string Kind,
    string Icon,
    string Label,
    string? Title);

public static class MachineCanvasStatus
{
    public static MachineCanvasStatusDisplay? Resolve(
        string machineId,
        string machineType,
        bool isFactoryRunning,
        IReadOnlyDictionary<string, string> errorByMachineId,
        IReadOnlyDictionary<string, string> warningByMachineId,
        IReadOnlyList<MachinePortFlowDto> portFlows,
        IReadOnlyList<BoardIssueDto> issues)
    {
        if (errorByMachineId.TryGetValue(machineId, out var errorMsg))
            return Blocked(errorMsg, machineType);

        var flows = portFlows.Where(f => f.MachineId == machineId).ToList();

        if (isFactoryRunning)
        {
            var blockedFlow = flows.FirstOrDefault(f =>
                f.ProcessStatus.Equals("Blocked", StringComparison.OrdinalIgnoreCase));
            if (blockedFlow != null)
                return Blocked(blockedFlow.Summary, machineType);

            var active = flows.FirstOrDefault(f =>
                f.ProcessStatus is "Processing" or "Transformed"
                && (!string.IsNullOrEmpty(f.OutputElementSymbol) || !string.IsNullOrEmpty(f.Summary) || !string.IsNullOrEmpty(f.TransformNote)));
            if (active != null)
                return Active(active);

            if (flows.Any(f => f.ProcessStatus.Equals("WaitingMaterial", StringComparison.OrdinalIgnoreCase)))
                return new("waiting", "...", "Waiting for material", "No input material at this tick.");

            if (flows.Count > 0)
                return new("idle", "—", "Idle this tick", "Connected but no processing yet.");
        }
        else
        {
            var previewError = issues.FirstOrDefault(i =>
                i.Severity == "error"
                && i.MachineId != null
                && i.MachineId.Equals(machineId, StringComparison.Ordinal));
            if (previewError != null)
                return Blocked(previewError.Message, machineType);

            var previewBlocked = flows.FirstOrDefault(f =>
                f.IsEstimate && f.ProcessStatus.Equals("Blocked", StringComparison.OrdinalIgnoreCase));
            if (previewBlocked != null)
                return Blocked(previewBlocked.Summary, machineType);

            if (warningByMachineId.TryGetValue(machineId, out var warnMsg))
                return Warning(warnMsg, machineType);
        }

        return null;
    }

    private static MachineCanvasStatusDisplay Blocked(string message, string machineType) =>
        new("blocked", "!", Shorten(message, machineType), message);

    private static MachineCanvasStatusDisplay Warning(string message, string machineType) =>
        new("warning", "?", Shorten(message, machineType), message);

    private static MachineCanvasStatusDisplay Active(MachinePortFlowDto flow)
    {
        var label = BuildActiveLabel(flow);
        var title = string.IsNullOrWhiteSpace(flow.Summary) ? label : flow.Summary;
        return new("active", "OK", label, title);
    }

    private static string BuildActiveLabel(MachinePortFlowDto flow)
    {
        if (flow.DnaChanged
            && !string.IsNullOrEmpty(flow.InputElementSymbol)
            && !string.IsNullOrEmpty(flow.OutputElementSymbol)
            && flow.InputElementSymbol == flow.OutputElementSymbol
            && !string.IsNullOrEmpty(flow.TransformNote))
        {
            return $"{flow.OutputElementSymbol} · {flow.TransformNote}";
        }

        if (flow.DnaChanged
            && !string.IsNullOrEmpty(flow.InputElementSymbol)
            && !string.IsNullOrEmpty(flow.OutputElementSymbol)
            && !string.IsNullOrEmpty(flow.InputPhase)
            && !string.IsNullOrEmpty(flow.OutputPhase))
        {
            return $"{flow.InputElementSymbol} {PhaseShort(flow.InputPhase)} -> {flow.OutputElementSymbol} {PhaseShort(flow.OutputPhase)}";
        }

        if (!string.IsNullOrEmpty(flow.OutputElementSymbol) && !string.IsNullOrEmpty(flow.OutputPhase))
            return $"Output {flow.OutputElementSymbol} ({PhaseShort(flow.OutputPhase)})";

        if (!string.IsNullOrEmpty(flow.TransformNote))
            return flow.TransformNote;

        if (flow.ProcessStatus.Equals("Processing", StringComparison.OrdinalIgnoreCase))
            return "Processing";

        if (!string.IsNullOrEmpty(flow.Summary))
            return TrimSummary(flow.Summary);

        return flow.ProcessStatus switch
        {
            "Transformed" => "Transforming",
            _ => "Processing"
        };
    }

    public static string Shorten(string message, string machineType)
    {
        var core = StripIssuePrefix(message);
        var t = machineType.Trim();

        if (core.Contains("gas phase", StringComparison.OrdinalIgnoreCase)
            && t.Equals("Condenser", StringComparison.OrdinalIgnoreCase))
            return "Needs gas to operate";

        if (core.Contains("liquid phase", StringComparison.OrdinalIgnoreCase))
        {
            if (t.Equals("Boiler", StringComparison.OrdinalIgnoreCase))
                return "Needs liquid to operate";
            if (t.Equals("LiquidSeparator", StringComparison.OrdinalIgnoreCase))
                return "Needs liquid input";
            if (t.Equals("Crystallizer", StringComparison.OrdinalIgnoreCase))
                return "Needs liquid to crystallize";
        }

        if (core.Contains("solid phase", StringComparison.OrdinalIgnoreCase))
        {
            if (t.Equals("Melter", StringComparison.OrdinalIgnoreCase))
                return "Needs solid input";
            if (t.Equals("Destilator", StringComparison.OrdinalIgnoreCase))
                return "Blocked by solid material";
        }

        if (core.Contains("explosivity", StringComparison.OrdinalIgnoreCase))
            return "Material too explosive";

        if (core.Contains("toxicity", StringComparison.OrdinalIgnoreCase))
            return "Material too toxic";

        if (core.Contains("pool volume full", StringComparison.OrdinalIgnoreCase)
            || core.Contains("pool is full", StringComparison.OrdinalIgnoreCase))
            return "Pool is full";

        if (core.Contains("pool is empty", StringComparison.OrdinalIgnoreCase)
            || core.Contains("pool has none", StringComparison.OrdinalIgnoreCase))
            return "Pool empty for this element";

        if (core.Contains("no configured variant", StringComparison.OrdinalIgnoreCase))
            return "Pick pool element in settings";

        if (core.Contains("not connected", StringComparison.OrdinalIgnoreCase))
            return "Port not connected";

        return TrimSummary(core);
    }

    private static string StripIssuePrefix(string message)
    {
        const string blocked = " is blocked:";
        var idx = message.IndexOf(blocked, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return message[(idx + blocked.Length)..].Trim();

        const string blockedBy = " is blocked by element ";
        idx = message.IndexOf(blockedBy, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var tail = message[(idx + blockedBy.Length)..];
            var colon = tail.IndexOf(':');
            return colon >= 0 ? tail[(colon + 1)..].Trim() : tail.Trim();
        }

        return message.Trim();
    }

    private static string TrimSummary(string text)
    {
        var oneLine = text.Replace('\n', ' ').Trim();
        if (oneLine.Length <= 42)
            return oneLine;

        var cut = oneLine[..39];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > 20)
            cut = cut[..lastSpace];
        return cut + "...";
    }

    private static string PhaseShort(string phase) => phase.ToLowerInvariant() switch
    {
        "gas" => "gas",
        "liquid" => "liquid",
        "solid" => "solid",
        _ => phase
    };
}
