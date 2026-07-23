namespace FactoryGame.Domain.Simulation;

/// <summary>Aggregates pool withdraw/deposit per DNA variant across a player's boards (seaport boundary only).</summary>
public static class PlayerSeaportElementFlowAggregator
{
    public static IReadOnlyDictionary<PoolStackKey, PoolElementFactoryFlow> Aggregate(
        IEnumerable<PlayerBoardSeaportFlowSnapshot> boards)
    {
        var acc = new Dictionary<PoolStackKey, Accumulator>();

        foreach (var board in boards)
        {
            var tickSec = Math.Max(1, board.TickIntervalSeconds);
            var hasDelta = board.IsRunning && board.LastSeaportDelta != null;

            if (hasDelta)
            {
                foreach (var (variant, qty) in board.LastSeaportDelta!.WithdrawVariants())
                    Get(acc, variant).AddMeasuredConsume((double)qty / tickSec);

                foreach (var (variant, qty) in board.LastSeaportDelta.DepositVariants())
                    Get(acc, variant).AddMeasuredProduce((double)qty / tickSec);
            }
            else
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
                        Get(acc, variant).NoteConsumeEstimate(rate, isEstimate);
                    else if (port.Port.Equals("in", StringComparison.OrdinalIgnoreCase))
                        Get(acc, variant).NoteProduceEstimate(rate, isEstimate);
                }
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
        private double _measuredConsume;
        private double _measuredProduce;
        private bool _hasMeasuredConsume;
        private bool _hasMeasuredProduce;
        private double _estimatedConsume;
        private double _estimatedProduce;
        private bool _estimateConsume;
        private bool _estimateProduce;

        public void AddMeasuredConsume(double rate)
        {
            _consumed = true;
            _measuredConsume += rate;
            _hasMeasuredConsume = true;
        }

        public void AddMeasuredProduce(double rate)
        {
            _produced = true;
            _measuredProduce += rate;
            _hasMeasuredProduce = true;
        }

        public void NoteConsumeEstimate(double estimatedRate, bool isEstimate)
        {
            _consumed = true;
            if (estimatedRate > 0)
                _estimatedConsume += estimatedRate;
            if (isEstimate)
                _estimateConsume = true;
        }

        public void NoteProduceEstimate(double estimatedRate, bool isEstimate)
        {
            _produced = true;
            if (estimatedRate > 0)
                _estimatedProduce += estimatedRate;
            if (isEstimate)
                _estimateProduce = true;
        }

        public PoolElementFactoryFlow ToFlow()
        {
            var consume = _hasMeasuredConsume ? _measuredConsume : _estimatedConsume;
            var produce = _hasMeasuredProduce ? _measuredProduce : _estimatedProduce;
            var isEstimate = (_consumed && _estimateConsume && !_hasMeasuredConsume)
                || (_produced && _estimateProduce && !_hasMeasuredProduce);

            return new PoolElementFactoryFlow(
                _consumed,
                _produced,
                _consumed ? consume : null,
                _produced ? produce : null,
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
