using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class PlayerSeaportElementFlowAggregatorTests
{
    private const long DnaSolid = 1L << DnaLayout.PhaseShift;
    private const long DnaGas = 2L << DnaLayout.PhaseShift;

    [Fact]
    public void Aggregate_sums_withdraw_and_deposit_across_boards()
    {
        var snapshots = new[]
        {
            new PlayerBoardSeaportFlowSnapshot(
                IsRunning: true,
                TickIntervalSeconds: 10,
                LastSeaportDelta: CreateDelta(withdraw: (3, DnaSolid, 2m)),
                IntoFactory: [],
                OutOfFactory: [],
                SeaportPorts: []),
            new PlayerBoardSeaportFlowSnapshot(
                IsRunning: true,
                TickIntervalSeconds: 10,
                LastSeaportDelta: CreateDelta(withdraw: (3, DnaSolid, 5m), deposit: (7, DnaGas, 2m)),
                IntoFactory: [],
                OutOfFactory: [],
                SeaportPorts: [])
        };

        var flows = PlayerSeaportElementFlowAggregator.Aggregate(snapshots);

        var withdrawKey = new PoolStackKey(3, DnaSolid);
        var depositKey = new PoolStackKey(7, DnaGas);
        Assert.True(flows[withdrawKey].ConsumedByFactory);
        Assert.Equal(0.7, flows[withdrawKey].ConsumeUnitsPerSecond!.Value, 3);
        Assert.False(flows[withdrawKey].ProducedByFactory);
        Assert.False(flows[withdrawKey].FlowIsEstimate);
        Assert.True(flows[depositKey].ProducedByFactory);
        Assert.Equal(0.2, flows[depositKey].ProduceUnitsPerSecond!.Value, 3);
        Assert.False(flows[depositKey].ConsumedByFactory);
    }

    [Fact]
    public void Aggregate_same_element_different_variants_does_not_merge_directions()
    {
        var snapshots = new[]
        {
            new PlayerBoardSeaportFlowSnapshot(
                IsRunning: true,
                TickIntervalSeconds: 10,
                LastSeaportDelta: CreateDelta(withdraw: (3, DnaSolid, 1m), deposit: (3, DnaGas, 1m)),
                IntoFactory: [],
                OutOfFactory: [],
                SeaportPorts: [])
        };

        var flows = PlayerSeaportElementFlowAggregator.Aggregate(snapshots);

        var solid = new PoolStackKey(3, DnaSolid);
        var gas = new PoolStackKey(3, DnaGas);
        Assert.True(flows[solid].ConsumedByFactory);
        Assert.False(flows[solid].ProducedByFactory);
        Assert.True(flows[gas].ProducedByFactory);
        Assert.False(flows[gas].ConsumedByFactory);
    }

    [Fact]
    public void Aggregate_marks_connected_ports_when_factory_stopped()
    {
        var snapshots = new[]
        {
            new PlayerBoardSeaportFlowSnapshot(
                IsRunning: false,
                TickIntervalSeconds: 10,
                LastSeaportDelta: null,
                IntoFactory:
                [
                    new SeaportFlowLine("sea1", "SeaportConnector", "out", "b1", "in", 0.5, "Pool → factory", null)
                ],
                OutOfFactory: [],
                SeaportPorts:
                [
                    new SeaportPortFlowDetail("sea1", "SeaportConnector", "out", "out", true, "b1", "in", 5, DnaSolid, "E05", "From pool", true)
                ])
        };

        var flows = PlayerSeaportElementFlowAggregator.Aggregate(snapshots);

        var key = new PoolStackKey(5, DnaSolid);
        Assert.True(flows[key].ConsumedByFactory);
        Assert.Equal(0.5, flows[key].ConsumeUnitsPerSecond);
        Assert.True(flows[key].FlowIsEstimate);
    }

    [Fact]
    public void Aggregate_uses_measured_delta_only_when_running()
    {
        var snapshots = new[]
        {
            new PlayerBoardSeaportFlowSnapshot(
                IsRunning: true,
                TickIntervalSeconds: 10,
                LastSeaportDelta: CreateDelta(withdraw: (3, DnaSolid, 2m)),
                IntoFactory: [],
                OutOfFactory: [],
                SeaportPorts:
                [
                    new SeaportPortFlowDetail("sea1", "SeaportConnector", "out", "out", true, "b1", "in", 99, DnaGas, "E99", "From pool", true)
                ])
        };

        var flows = PlayerSeaportElementFlowAggregator.Aggregate(snapshots);

        Assert.True(flows.ContainsKey(new PoolStackKey(3, DnaSolid)));
        Assert.False(flows.ContainsKey(new PoolStackKey(99, DnaGas)));
    }

    [Fact]
    public void Aggregate_does_not_mark_internal_upstream_elements_when_stopped()
    {
        var snapshots = new[]
        {
            new PlayerBoardSeaportFlowSnapshot(
                IsRunning: false,
                TickIntervalSeconds: 10,
                LastSeaportDelta: null,
                IntoFactory:
                [
                    new SeaportFlowLine("sea1", "SeaportConnector", "out", "b1", "in", 0.5, "Pool → factory", null)
                ],
                OutOfFactory: [],
                SeaportPorts:
                [
                    new SeaportPortFlowDetail("sea1", "SeaportConnector", "out", "out", true, "b1", "in", 5, DnaSolid, "E05", "From pool", true),
                    new SeaportPortFlowDetail("sea1", "SeaportConnector", "in", "in", true, "b1", "out", null, null, null, "To pool", true)
                ])
        };

        var flows = PlayerSeaportElementFlowAggregator.Aggregate(snapshots);

        Assert.True(flows.ContainsKey(new PoolStackKey(5, DnaSolid)));
        Assert.Equal(1, flows.Count);
    }

    private static SeaportTickDelta CreateDelta(
        (int ElementId, long Dna, decimal Qty)? withdraw = null,
        (int ElementId, long Dna, decimal Qty)? deposit = null)
    {
        var delta = new SeaportTickDelta();
        if (withdraw is { } w)
            delta.AddWithdraw(w.ElementId, w.Dna, w.Qty);
        if (deposit is { } d)
            delta.AddDeposit(d.ElementId, d.Dna, d.Qty);
        return delta;
    }
}
