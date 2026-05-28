using System.Text.Json;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Simulation.Processors;

namespace FactoryGame.Domain.Simulation;

/// <summary>Static plan report: seaport flows, throughput estimate, value estimate, validation issues.</summary>
public static class BoardInfoAnalyzer
{
    private const double BaselineUnitsPerTick = 1.0;
    private const decimal ReferenceUnitValue = 10m;

    public static BoardInfoReport Analyze(BoardInfoAnalyzeRequest request)
    {
        var machines = request.Machines;
        var connections = request.Connections;
        var tickSec = Math.Max(1, request.TickIntervalSeconds);
        var isEstimate = !request.IsRunning;

        var connFrom = connections.GroupBy(c => (c.FromId, c.FromPort)).ToDictionary(g => g.Key, g => g.First());
        var connTo = connections.GroupBy(c => (c.ToId, c.ToPort)).ToDictionary(g => g.Key, g => g.First());
        var machineById = machines.ToDictionary(m => m.Id, StringComparer.Ordinal);

        var intoFactory = new List<SeaportFlowLine>();
        var outOfFactory = new List<SeaportFlowLine>();
        var issues = new List<BoardIssue>();

        foreach (var m in machines)
        {
            if (!MachinePortCatalog.IsKnownMachineType(m.Type))
            {
                issues.Add(BoardIssue.Warning("unknown_machine_type", $"Okänd maskintyp «{m.Type}» på {m.Id}.", m.Id));
                continue;
            }

            var ports = MachinePortCatalog.GetPorts(m.Type);
            var connectedPorts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in ports)
            {
                var hasIn = connTo.ContainsKey((m.Id, p.Name));
                var hasOut = connFrom.ContainsKey((m.Id, p.Name));
                if (hasIn || hasOut)
                    connectedPorts.Add(p.Name);
            }

            foreach (var p in ports)
            {
                if (IsImplicitSeaportPort(m.Type, p))
                    continue;
                if (!connectedPorts.Contains(p.Name))
                {
                    issues.Add(BoardIssue.Warning(
                        "port_unconnected",
                        $"Port «{p.Name}» ({p.Direction}) på {m.Id} ({m.Type}) är inte kopplad.",
                        m.Id));
                }
            }

            if (connectedPorts.Count == 0 && !IsSeaportOnlyType(m.Type))
            {
                issues.Add(BoardIssue.Warning(
                    "machine_isolated",
                    $"Maskin {m.Id} ({m.Type}) har inga rörkopplingar.",
                    m.Id));
            }

            CollectSeaportFlows(m, connFrom, connTo, tickSec, intoFactory, outOfFactory, issues);
            CollectSorterDnaIssues(m, machineById, connFrom, issues);
        }

        if (machines.Count > 0 && !machines.Any(m => IsSeaportType(m.Type)))
        {
            issues.Add(BoardIssue.Warning(
                "no_seaport",
                "Planen saknar seaport-anslutning — material kan inte flöda till/från poolen.",
                null));
        }

        if (connections.Count > 0)
        {
            var simPlan = ToSimulationPlan(machines, connections);
            if (PlanGraph.HasCycle(simPlan))
            {
                issues.Add(BoardIssue.Info(
                    "cycle_detected",
                    "Planen innehåller en slinga (t.ex. seaport → maskin → seaport). Det är tillåtet; simuleringen kör i stabil maskinordning.",
                    null));
            }
        }

        CollectPoolStockIssues(machines, connFrom, request.PoolQuantities, issues);
        CollectRuntimeIssues(request, issues);

        double intoUps;
        double outUps;
        if (request.LastSeaportDelta != null && request.IsRunning)
        {
            intoUps = request.LastSeaportDelta.WithdrawnFromPool.Values.Sum(v => (double)v) / tickSec;
            outUps = request.LastSeaportDelta.DepositedToPool.Values.Sum(v => (double)v) / tickSec;
            EnrichFlowsFromDelta(intoFactory, outOfFactory, request.LastSeaportDelta, tickSec);
        }
        else
        {
            intoUps = intoFactory.Sum(f => f.UnitsPerSecond);
            outUps = outOfFactory.Sum(f => f.UnitsPerSecond);
        }

        var totalUps = intoUps + outUps;
        var valuePerSec = EstimateValuePerSecond(intoUps, outUps, request.ElementPrices, machines, isEstimate);
        var assetValue = EstimateInstalledAssetValue(machines);

        var throughputNote = request.IsRunning && request.LastSeaportDelta != null
            ? "Mätt från senaste simuleringstick."
            : isEstimate
                ? "Uppskattning från plan (fabriken kör inte)."
                : "Baserat på planstruktur och maskinspecifika flödesrater.";

        var valueNote = request.IsRunning && request.ElementPrices != null
            ? "Värde från senaste flöde × spotpris + maskinkapital (timprorata)."
            : "Värde = flödesuppskattning + installerade maskiner (timprorata).";

        var seaportPorts = SeaportPortFlowAnalyzer.AnalyzePorts(
            machines,
            connections,
            request.IsRunning,
            request.RuntimeState,
            request.LastSeaportDelta);

        var machinePortFlows = MachinePortFlowAnalyzer.Analyze(
            machines,
            connections,
            request.IsRunning,
            request.RuntimeState);

        return new BoardInfoReport(
            intoFactory,
            outOfFactory,
            seaportPorts,
            machinePortFlows,
            totalUps,
            isEstimate,
            throughputNote,
            valuePerSec,
            isEstimate,
            valueNote,
            issues);
    }

    private static double MachineEffectiveUnitRate(MachineInfo machine, double tickSec)
    {
        var settings = machine.Settings?.GetRawText();
        var permille = MachineRateCatalog.GetEffectiveRatePermille(machine.Type, settings);
        return BaselineUnitsPerTick * permille / 1000.0 / tickSec;
    }

    private static SimulationPlan ToSimulationPlan(
        IReadOnlyList<MachineInfo> machines,
        IReadOnlyList<ConnectionInfo> connections) =>
        new(
            machines.Select(m => new SimulationMachine(m.Id, m.Type, m.Settings?.GetRawText())).ToList(),
            connections.Select(c => new SimulationConnection(c.FromId, c.FromPort, c.ToId, c.ToPort)).ToList());

    private static bool IsSeaportType(string type) =>
        type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase)
        || type.Equals("SeaportIn", StringComparison.OrdinalIgnoreCase)
        || type.Equals("SeaportOut", StringComparison.OrdinalIgnoreCase);

    private static bool IsSeaportOnlyType(string type) =>
        type.Equals("SeaportIn", StringComparison.OrdinalIgnoreCase)
        || type.Equals("SeaportOut", StringComparison.OrdinalIgnoreCase);

    private static bool IsImplicitSeaportPort(string machineType, MachinePort port)
    {
        if (machineType.Equals("SeaportIn", StringComparison.OrdinalIgnoreCase) && port.Direction == PortDirection.In)
            return true;
        if (machineType.Equals("SeaportOut", StringComparison.OrdinalIgnoreCase) && port.Direction == PortDirection.Out)
            return true;
        return false;
    }

    private static void CollectSeaportFlows(
        MachineInfo m,
        IReadOnlyDictionary<(string FromId, string FromPort), ConnectionInfo> connFrom,
        IReadOnlyDictionary<(string ToId, string ToPort), ConnectionInfo> connTo,
        double tickSec,
        List<SeaportFlowLine> intoFactory,
        List<SeaportFlowLine> outOfFactory,
        List<BoardIssue> issues)
    {
        var unitRate = MachineEffectiveUnitRate(m, tickSec);
        if (m.Type.Equals("SeaportIn", StringComparison.OrdinalIgnoreCase)
            || m.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase))
        {
            if (connFrom.TryGetValue((m.Id, "out"), out var c))
            {
                intoFactory.Add(new SeaportFlowLine(
                    m.Id, m.Type, "out", c.ToId, c.ToPort, unitRate,
                    $"Pool → fabrik via {m.Id}.out → {c.ToId}.{c.ToPort}"));
            }
            else if (m.Type.Equals("SeaportIn", StringComparison.OrdinalIgnoreCase)
                     || m.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(BoardIssue.Warning(
                    "seaport_in_idle",
                    $"Seaport «{m.Id}» matar inte in i fabriken (utgång «out» ej kopplad).",
                    m.Id));
            }
        }

        if (m.Type.Equals("SeaportOut", StringComparison.OrdinalIgnoreCase)
            || m.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase))
        {
            if (connTo.TryGetValue((m.Id, "in"), out var c))
            {
                outOfFactory.Add(new SeaportFlowLine(
                    m.Id, m.Type, "in", c.FromId, c.FromPort, unitRate,
                    $"Fabrik → pool via {c.FromId}.{c.FromPort} → {m.Id}.in"));
            }
            else if (m.Type.Equals("SeaportOut", StringComparison.OrdinalIgnoreCase)
                     || m.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(BoardIssue.Warning(
                    "seaport_out_idle",
                    $"Seaport «{m.Id}» tar inte emot från fabriken (ingång «in» ej kopplad).",
                    m.Id));
            }
        }
    }

    private static void CollectSorterDnaIssues(
        MachineInfo sorter,
        IReadOnlyDictionary<string, MachineInfo> machineById,
        IReadOnlyDictionary<(string FromId, string FromPort), ConnectionInfo> connFrom,
        List<BoardIssue> issues)
    {
        if (!sorter.Type.Equals("Sorter", StringComparison.OrdinalIgnoreCase) || sorter.Settings is not { } settings)
            return;

        var portElements = ParseSorterElementIds(settings);
        foreach (var (portKey, elementIds) in portElements)
        {
            var outPort = portKey switch
            {
                "port1" => "out1",
                "port2" => "out2",
                "port3" => "out3",
                _ => null
            };
            if (outPort == null)
                continue;

            if (!connFrom.TryGetValue((sorter.Id, outPort), out var link))
                continue;

            if (!machineById.TryGetValue(link.ToId, out var downstream))
                continue;

            foreach (var elementId in elementIds)
            {
                var element = ElementCatalog.All.FirstOrDefault(e => e.Id == elementId);
                if (element.Id != elementId)
                    continue;

                var reason = MachineDnaCompatibility.GetIncompatibilityReason(downstream.Type, element.Dna);
                if (reason == null)
                    continue;

                issues.Add(BoardIssue.Error(
                    "dna_incompatible",
                    MachineBlockedGuidance.FormatSorterIssue(
                        downstream.Id, downstream.Type, element.Symbol, elementId, sorter.Id, reason),
                    downstream.Id));
            }
        }
    }

    private static IReadOnlyDictionary<string, int[]> ParseSorterElementIds(JsonElement settings)
    {
        var result = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var portKey in new[] { "port1", "port2", "port3" })
        {
            if (!settings.TryGetProperty(portKey, out var arr) || arr.ValueKind != JsonValueKind.Array)
                continue;
            var ids = new List<int>();
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var id))
                    ids.Add(id);
            }

            if (ids.Count > 0)
                result[portKey] = ids.ToArray();
        }

        return result;
    }

    private static void CollectPoolStockIssues(
        IReadOnlyList<MachineInfo> machines,
        IReadOnlyDictionary<(string FromId, string FromPort), ConnectionInfo> connFrom,
        IReadOnlyDictionary<int, decimal>? poolQuantities,
        List<BoardIssue> issues)
    {
        foreach (var m in machines)
        {
            if (!m.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase)
                && !m.Type.Equals("SeaportIn", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!connFrom.ContainsKey((m.Id, "out")))
                continue;

            var elementId = SeaportConnectorProcessor.ParseOutElementId(m.Settings?.GetRawText());
            if (elementId <= 0)
            {
                // SeaportConnector with out port connected but no outElementId — factory will idle.
                if (m.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(BoardIssue.Warning(
                        "seaport_no_element",
                        $"Seaport «{m.Id}» saknar konfigurerad variant (outElementId) — välj ett ämne i inställningarna, annars kör fabriken idle.",
                        m.Id));
                }
                continue;
            }

            if (poolQuantities == null)
                continue;

            if (poolQuantities.GetValueOrDefault(elementId) > 0)
                continue;

            var symbol = ElementCatalog.All.FirstOrDefault(e => e.Id == elementId).Symbol;
            var label = string.IsNullOrEmpty(symbol) ? $"element {elementId}" : symbol;
            issues.Add(BoardIssue.Warning(
                "pool_empty",
                $"Seaport «{m.Id}» matar in {label} men poolen har inget kvar.",
                m.Id));
        }
    }

    private static void CollectRuntimeIssues(BoardInfoAnalyzeRequest request, List<BoardIssue> issues)
    {
        if (request.RuntimeState == null)
            return;
        foreach (var (_, m) in request.RuntimeState.Machines)
        {
            if (m.BlockedReason != null)
            {
                issues.Add(BoardIssue.Error(
                    "machine_blocked",
                    MachineBlockedGuidance.FormatBlockedIssue(m.MachineId, m.MachineType, m.BlockedReason),
                    m.MachineId));
            }
        }

        if (request.PoolQuantities != null && request.LastSeaportDelta != null)
        {
            foreach (var (elementId, withdrawn) in request.LastSeaportDelta.WithdrawnFromPool)
            {
                var available = request.PoolQuantities.GetValueOrDefault(elementId);
                if (available <= 0 && withdrawn > 0)
                {
                    issues.Add(BoardIssue.Warning(
                        "pool_empty",
                        $"Seaport kunde inte hämta element {elementId} — poolen har inget kvar.",
                        null));
                }
            }
        }
    }

    private static void EnrichFlowsFromDelta(
        List<SeaportFlowLine> intoFactory,
        List<SeaportFlowLine> outOfFactory,
        SeaportTickDelta delta,
        int tickSec)
    {
        foreach (var (elementId, qty) in delta.WithdrawnFromPool)
        {
            if (qty <= 0) continue;
            var rate = (double)qty / tickSec;
            intoFactory.Add(new SeaportFlowLine(
                "pool", "SeaportPool", "out", null, null, rate,
                $"Pool → fabrik: element {elementId}, {qty:0.##}/tick"));
        }
        foreach (var (elementId, qty) in delta.DepositedToPool)
        {
            if (qty <= 0) continue;
            var rate = (double)qty / tickSec;
            outOfFactory.Add(new SeaportFlowLine(
                "pool", "SeaportPool", "in", null, null, rate,
                $"Fabrik → pool: element {elementId}, {qty:0.##}/tick"));
        }
    }

    private static decimal EstimateValuePerSecond(
        double intoUps,
        double outUps,
        IReadOnlyDictionary<int, decimal>? prices,
        IReadOnlyList<MachineInfo> machines,
        bool isEstimate)
    {
        if (prices == null || prices.Count == 0)
        {
            var flowValue = (decimal)(intoUps + outUps) * ReferenceUnitValue * 0.5m;
            return flowValue + EstimateInstalledAssetValue(machines) / 3600m;
        }

        decimal sum = 0;
        var avgPrice = prices.Values.DefaultIfEmpty(10m).Average();
        sum += (decimal)(intoUps + outUps) * avgPrice;
        return sum + EstimateInstalledAssetValue(machines) / 3600m;
    }

    private static decimal EstimateInstalledAssetValue(IReadOnlyList<MachineInfo> machines)
    {
        decimal sum = 0;
        foreach (var m in machines)
        {
            var entry = MachineStoreCatalog.TryGetEntry(m.Type);
            if (entry != null)
                sum += entry.Price;
        }

        return sum;
    }
}

public sealed record BoardInfoAnalyzeRequest(
    IReadOnlyList<MachineInfo> Machines,
    IReadOnlyList<ConnectionInfo> Connections,
    bool IsRunning,
    int TickIntervalSeconds,
    BoardLineState? RuntimeState = null,
    SeaportTickDelta? LastSeaportDelta = null,
    IReadOnlyDictionary<int, decimal>? PoolQuantities = null,
    IReadOnlyDictionary<int, decimal>? ElementPrices = null);

public sealed record MachineInfo(string Id, string Type, JsonElement? Settings);

public sealed record ConnectionInfo(string FromId, string FromPort, string ToId, string ToPort);

public sealed record BoardInfoReport(
    IReadOnlyList<SeaportFlowLine> IntoFactory,
    IReadOnlyList<SeaportFlowLine> OutOfFactory,
    IReadOnlyList<SeaportPortFlowDetail> SeaportPorts,
    IReadOnlyList<MachinePortFlowDetail> MachinePortFlows,
    double TotalUnitsPerSecond,
    bool ThroughputIsEstimate,
    string ThroughputNote,
    decimal EstimatedValuePerSecond,
    bool ValueIsEstimate,
    string ValueNote,
    IReadOnlyList<BoardIssue> Issues);

public sealed record SeaportFlowLine(
    string MachineId,
    string MachineType,
    string Port,
    string? LinkedMachineId,
    string? LinkedPort,
    double UnitsPerSecond,
    string Description);

public sealed record BoardIssue(string Severity, string Code, string Message, string? MachineId)
{
    public static BoardIssue Error(string code, string message, string? machineId) =>
        new("error", code, message, machineId);

    public static BoardIssue Warning(string code, string message, string? machineId) =>
        new("warning", code, message, machineId);

    public static BoardIssue Info(string code, string message, string? machineId) =>
        new("info", code, message, machineId);
}
