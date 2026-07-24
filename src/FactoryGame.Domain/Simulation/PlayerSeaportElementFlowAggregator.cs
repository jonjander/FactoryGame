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
            foreach (var port in board.SeaportPorts)
            {
                if (!port.IsConnected || port.ElementId is not int elementId)
                    continue;

                var dna = port.MaterialDna ?? 0;
                if (dna == 0)
                    continue;

                var variant = new PoolStackKey(elementId, dna);
                var rate = ResolveSeaportPortRate(port, board.IntoFactory, board.OutOfFactory);
                var isEstimate = port.IsEstimate || !board.IsRunning;

                if (port.Port.Equals("out", StringComparison.OrdinalIgnoreCase))
                    Get(acc, variant).NoteConsume(rate, isEstimate);
                else if (port.Port.Equals("in", StringComparison.OrdinalIgnoreCase))
                    Get(acc, variant).NoteProduce(rate, isEstimate);
            }
        }

        return acc.ToDictionary(kv => kv.Key, kv => kv.Value.ToFlow());
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

        public PoolElementFactoryFlow ToFlow()
        {
            var isEstimate = (_consumed && _estimateConsume) || (_produced && _estimateProduce);

            return new PoolElementFactoryFlow(
                _consumed,
                _produced,
                _consumed ? _consumeRate : null,
                _produced ? _produceRate : null,
                isEstimate);
        }
    }
}

public sealed record PlayerBoardSeaportFlowSnapshot(
    bool IsRunning,
    int TickIntervalSeconds,
    SeaportTickDelta? LastSeaportDelta,
    IReadOnlyList<SeaportFlowLine> IntoFactory,
    IReadOnlyList<SeaportFlowLine> OutOfFactory,
    IReadOnlyList<SeaportPortFlowDetail> SeaportPorts);

public sealed record PoolElementFactoryFlow(
    bool ConsumedByFactory,
    bool ProducedByFactory,
    double? ConsumeUnitsPerSecond,
    double? ProduceUnitsPerSecond,
    bool FlowIsEstimate);
