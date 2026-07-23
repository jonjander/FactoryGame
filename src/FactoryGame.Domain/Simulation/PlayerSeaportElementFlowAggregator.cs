namespace FactoryGame.Domain.Simulation;

/// <summary>Aggregates seaport withdraw/deposit per element across a player's boards.</summary>
public static class PlayerSeaportElementFlowAggregator
{
    public static IReadOnlyDictionary<int, PoolElementFactoryFlow> Aggregate(
        IEnumerable<PlayerBoardSeaportFlowSnapshot> boards)
    {
        var acc = new Dictionary<int, Accumulator>();

        foreach (var board in boards)
        {
            var tickSec = Math.Max(1, board.TickIntervalSeconds);
            var hasDelta = board.IsRunning && board.LastSeaportDelta != null;

            if (hasDelta)
            {
                foreach (var (elementId, qty) in board.LastSeaportDelta!.WithdrawnFromPool)
                {
                    if (qty <= 0)
                        continue;
                    Get(acc, elementId).AddMeasuredConsume((double)qty / tickSec);
                }

                foreach (var (elementId, qty) in board.LastSeaportDelta.DepositedToPool)
                {
                    if (qty <= 0)
                        continue;
                    Get(acc, elementId).AddMeasuredProduce((double)qty / tickSec);
                }
            }

            foreach (var port in board.SeaportPorts)
            {
                if (port.ElementId is not int elementId || !port.IsConnected)
                    continue;

                var rate = ResolvePortRate(port, board.IntoFactory, board.OutOfFactory);
                if (port.Port.Equals("out", StringComparison.OrdinalIgnoreCase))
                    Get(acc, elementId).NoteConsumePresence(rate, hasDelta ? null : port.IsEstimate || !board.IsRunning);
                else if (port.Port.Equals("in", StringComparison.OrdinalIgnoreCase))
                    Get(acc, elementId).NoteProducePresence(rate, hasDelta ? null : port.IsEstimate || !board.IsRunning);
            }

            foreach (var line in board.IntoFactory)
            {
                if (line.ElementId is not int elementId)
                    continue;
                Get(acc, elementId).NoteConsumePresence(line.UnitsPerSecond, hasDelta ? null : !board.IsRunning);
            }

            foreach (var line in board.OutOfFactory)
            {
                if (line.ElementId is not int elementId)
                    continue;
                Get(acc, elementId).NoteProducePresence(line.UnitsPerSecond, hasDelta ? null : !board.IsRunning);
            }
        }

        return acc.ToDictionary(kv => kv.Key, kv => kv.Value.ToFlow());
    }

    private static double ResolvePortRate(
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

    private static Accumulator Get(Dictionary<int, Accumulator> acc, int elementId)
    {
        if (!acc.TryGetValue(elementId, out var a))
        {
            a = new Accumulator();
            acc[elementId] = a;
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

        public void NoteConsumePresence(double estimatedRate, bool? isEstimate)
        {
            _consumed = true;
            if (estimatedRate > 0)
                _estimatedConsume += estimatedRate;
            if (isEstimate == true)
                _ = isEstimate;
        }

        public void NoteProducePresence(double estimatedRate, bool? isEstimate)
        {
            _produced = true;
            if (estimatedRate > 0)
                _estimatedProduce += estimatedRate;
            if (isEstimate == true)
                _ = isEstimate;
        }

        public PoolElementFactoryFlow ToFlow()
        {
            var consume = _hasMeasuredConsume ? _measuredConsume : _estimatedConsume;
            var produce = _hasMeasuredProduce ? _measuredProduce : _estimatedProduce;
            var isEstimate = (_consumed && !_hasMeasuredConsume) || (_produced && !_hasMeasuredProduce);

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
