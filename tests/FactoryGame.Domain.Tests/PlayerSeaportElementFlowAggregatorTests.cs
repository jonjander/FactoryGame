using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class PlayerSeaportElementFlowAggregatorTests
{
    [Fact]
    public void Aggregate_sums_withdraw_and_deposit_across_boards()
    {
        var snapshots = new[]
        {
            new PlayerBoardSeaportFlowSnapshot(
                IsRunning: true,
                TickIntervalSeconds: 10,
                LastSeaportDelta: CreateDelta(withdraw: (3, 2m)),
                IntoFactory: [],
                OutOfFactory: [],
                SeaportPorts: []),
            new PlayerBoardSeaportFlowSnapshot(
                IsRunning: true,
                TickIntervalSeconds: 10,
                LastSeaportDelta: CreateDelta(withdraw: (3, 5m), deposit: (7, 2m)),
                IntoFactory: [],
                OutOfFactory: [],
                SeaportPorts: [])
        };

        var flows = PlayerSeaportElementFlowAggregator.Aggregate(snapshots);

        Assert.True(flows[3].ConsumedByFactory);
        Assert.Equal(0.7, flows[3].ConsumeUnitsPerSecond!.Value, 3);
        Assert.False(flows[3].FlowIsEstimate);
        Assert.True(flows[7].ProducedByFactory);
        Assert.Equal(0.2, flows[7].ProduceUnitsPerSecond!.Value, 3);
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
                    new SeaportPortFlowDetail("sea1", "SeaportConnector", "out", "out", true, "b1", "in", 5, "E05", "From pool", true)
                ])
        };

        var flows = PlayerSeaportElementFlowAggregator.Aggregate(snapshots);

        Assert.True(flows[5].ConsumedByFactory);
        Assert.Equal(0.5, flows[5].ConsumeUnitsPerSecond);
        Assert.True(flows[5].FlowIsEstimate);
    }

    private static SeaportTickDelta CreateDelta(
        (int ElementId, decimal Qty)? withdraw = null,
        (int ElementId, decimal Qty)? deposit = null)
    {
        var delta = new SeaportTickDelta();
        if (withdraw is { } w)
            delta.AddWithdraw(w.ElementId, w.Qty);
        if (deposit is { } d)
            delta.AddDeposit(d.ElementId, d.Qty);
        return delta;
    }
}
