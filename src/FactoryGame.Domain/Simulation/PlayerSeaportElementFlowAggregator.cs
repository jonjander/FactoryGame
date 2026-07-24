namespace FactoryGame.Domain.Simulation;

/// <summary>
/// Aggregates planned pool withdraw/deposit per DNA variant across a player's boards
/// (seaport boundary capacity from layout/sim analysis — not per-tick deltas).
/// </summary>
public static class PlayerSeaportElementFlowAggregator
{
    public static IReadOnlyDictionary<PoolStackKey, PoolElementFactoryFlow> Aggregate(
        IEnumerable<PlayerBoardSeaportFlowSnapshot> boards)
    {
        var acc = new Dictionary<PoolStackKey, Accumulator>();

        foreach (var board in boards)
        {
            var touchedVariants = new HashSet<PoolStackKey>();

            foreach (var port in board.SeaportPorts)
            {
                if (!port.IsConnected || port.ElementId is not int elementId)
                    continue;

                var dna = port.MaterialDna ?? 0;
                if (dna == 0)
                    continue;

                var variant = new PoolStackKey(elementId, dna);
                touchedVariants.Add(variant);
                var rate = ResolveSeaportPortRate(port, board.IntoFactory, board.OutOfFactory);
                var isEstimate = port.IsEstimate || !board.IsRunning;

                if (port.Port.Equals("out", StringComparison.OrdinalIgnoreCase))
                {
                    Get(acc, variant).NoteConsume(rate, isEstimate);
                    var machineId = port.LinkedMachineId ?? port.MachineId;
                    Get(acc, variant).AddMachine(
                        board, machineId, ResolveMachineType(board, machineId), "consume", rate, isEstimate,
                        port.Summary);
                }
                else if (port.Port.Equals("in", StringComparison.OrdinalIgnoreCase))
                {
                    Get(acc, variant).NoteProduce(rate, isEstimate);
                    var machineId = port.LinkedMachineId ?? port.MachineId;
                    Get(acc, variant).AddMachine(
                        board, machineId, ResolveMachineType(board, machineId), "produce", rate, isEstimate,
                        port.Summary);
                }
            }

            foreach (var variant in touchedVariants)
            {
                var elementId = variant.ElementId;
                var dna = variant.Dna;
                foreach (var flow in board.MachinePortFlows)
                {
                    if (flow.MachineType.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (flow.InputElementId == elementId && flow.InputDna == dna)
                    {
                        Get(acc, variant).AddMachine(
                            board, flow.MachineId, flow.MachineType, "consume", null,
                            flow.IsEstimate || !board.IsRunning, flow.Summary);
                    }

                    if (flow.OutputElementId == elementId && flow.OutputDna == dna)
                    {
                        Get(acc, variant).AddMachine(
                            board, flow.MachineId, flow.MachineType, "produce", null,
                            flow.IsEstimate || !board.IsRunning, flow.Summary);
                    }
                }
            }
        }

        return acc.ToDictionary(kv => kv.Key, kv => kv.Value.ToFlow());
    }

    private static string ResolveMachineType(PlayerBoardSeaportFlowSnapshot board, string machineId)
    {
        var fromFlow = board.MachinePortFlows.FirstOrDefault(f => f.MachineId == machineId);
        if (fromFlow != null)
            return fromFlow.MachineType;

        var fromPort = board.SeaportPorts.FirstOrDefault(p => p.MachineId == machineId);
        return fromPort?.MachineType ?? "Machine";
    }

    private static double ResolveSeaportPortRate(
        SeaportPortFlowDetail port,
        IReadOnlyList<SeaportFlowLine> intoFactory,
        IReadOnlyList<SeaportFlowLine> outOfFactory)
    {
        if (port.Port.Equals("out", StringComparison.OrdinalIgnoreCase))
        {
            return intoFactory
                .Where(l => l.MachineId == port.MachineId && l.Port.Equals("out", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.UnitsPerSecond)
                .DefaultIfEmpty(0)
                .Max();
        }

        return outOfFactory
            .Where(l => l.MachineId == port.MachineId && l.Port.Equals("in", StringComparison.OrdinalIgnoreCase))
            .Select(l => l.UnitsPerSecond)
            .DefaultIfEmpty(0)
            .Max();
    }

    private static Accumulator Get(Dictionary<PoolStackKey, Accumulator> acc, PoolStackKey variant)
    {
        if (!acc.TryGetValue(variant, out var a))
        {
            a = new Accumulator();
            acc[variant] = a;
        }

        return a;
    }

    private sealed class Accumulator
    {
        private bool _consumed;
        private bool _produced;
        private double _consumeRate;
        private double _produceRate;
        private bool _estimateConsume;
        private bool _estimateProduce;
        private readonly Dictionary<Guid, BoardAccumulator> _boards = new();

        public void NoteConsume(double rate, bool isEstimate)
        {
            _consumed = true;
            if (rate > 0)
                _consumeRate += rate;
            if (isEstimate)
                _estimateConsume = true;
        }

        public void NoteProduce(double rate, bool isEstimate)
        {
            _produced = true;
            if (rate > 0)
                _produceRate += rate;
            if (isEstimate)
                _estimateProduce = true;
        }

        public void AddMachine(
            PlayerBoardSeaportFlowSnapshot board,
            string machineId,
            string machineType,
            string role,
            double? rate,
            bool isEstimate,
            string? summary)
        {
            if (!_boards.TryGetValue(board.BoardId, out var boardAcc))
            {
                boardAcc = new BoardAccumulator(board.BoardId, board.BoardName, board.IsRunning);
                _boards[board.BoardId] = boardAcc;
            }

            boardAcc.AddMachine(machineId, machineType, role, rate, isEstimate, summary);
        }

        public PoolElementFactoryFlow ToFlow()
        {
            var isEstimate = (_consumed && _estimateConsume) || (_produced && _estimateProduce);
            var boards = _boards.Values
                .Select(b => b.ToBoard())
                .OrderBy(b => b.BoardName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new PoolElementFactoryFlow(
                _consumed,
                _produced,
                _consumed ? _consumeRate : null,
                _produced ? _produceRate : null,
                isEstimate,
                boards);
        }
    }

    private sealed class BoardAccumulator(Guid boardId, string boardName, bool isRunning)
    {
        private readonly Dictionary<(string MachineId, string Role), PoolElementFactoryMachine> _machines = new();

        public void AddMachine(
            string machineId,
            string machineType,
            string role,
            double? rate,
            bool isEstimate,
            string? summary)
        {
            var key = (machineId, role);
            if (_machines.TryGetValue(key, out var existing))
            {
                var mergedRate = MergeRate(existing.UnitsPerSecond, rate);
                _machines[key] = existing with
                {
                    UnitsPerSecond = mergedRate,
                    IsEstimate = existing.IsEstimate || isEstimate,
                    Summary = existing.Summary ?? summary
                };
                return;
            }

            _machines[key] = new PoolElementFactoryMachine(
                machineId, machineType, role, rate, isEstimate, summary);
        }

        public PoolElementFactoryBoard ToBoard() =>
            new(
                boardId,
                boardName,
                isRunning,
                _machines.Values
                    .OrderBy(m => m.Role, StringComparer.Ordinal)
                    .ThenBy(m => m.MachineId, StringComparer.Ordinal)
                    .ToList());

        private static double? MergeRate(double? left, double? right)
        {
            if (left is null)
                return right;
            if (right is null)
                return left;
            return Math.Max(left.Value, right.Value);
        }
    }
}

public sealed record PlayerBoardSeaportFlowSnapshot(
    Guid BoardId,
    string BoardName,
    bool IsRunning,
    int TickIntervalSeconds,
    SeaportTickDelta? LastSeaportDelta,
    IReadOnlyList<SeaportFlowLine> IntoFactory,
    IReadOnlyList<SeaportFlowLine> OutOfFactory,
    IReadOnlyList<SeaportPortFlowDetail> SeaportPorts,
    IReadOnlyList<MachinePortFlowDetail> MachinePortFlows);

public sealed record PoolElementFactoryMachine(
    string MachineId,
    string MachineType,
    string Role,
    double? UnitsPerSecond,
    bool IsEstimate,
    string? Summary);

public sealed record PoolElementFactoryBoard(
    Guid BoardId,
    string BoardName,
    bool IsRunning,
    IReadOnlyList<PoolElementFactoryMachine> Machines);

public sealed record PoolElementFactoryFlow(
    bool ConsumedByFactory,
    bool ProducedByFactory,
    double? ConsumeUnitsPerSecond,
    double? ProduceUnitsPerSecond,
    bool FlowIsEstimate,
    IReadOnlyList<PoolElementFactoryBoard> Boards);
