using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class PlayerSeaportElementFlowAggregatorTests
{
    private static readonly Guid Board1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Board2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const long DnaSolid = 1L << DnaLayout.PhaseShift;
    private const long DnaGas = 2L << DnaLayout.PhaseShift;

    [Fact]
    public void Aggregate_sums_planned_ports_across_boards()
    {
        var snapshots = new[]
        {
            Snapshot(
                Board1Id,
                "Alpha",
                IsRunning: true,
                LastSeaportDelta: CreateDelta(withdraw: (3, DnaSolid, 2m)),
                IntoFactory:
                [
                    new SeaportFlowLine("sea1", "SeaportConnector", "out", "b1", "in", 0.2, "Pool → factory", null)
                ],
                OutOfFactory: [],
                SeaportPorts:
                [
                    new SeaportPortFlowDetail("sea1", "SeaportConnector", "out", "out", true, "b1", "in", 3, DnaSolid, "E03", "From pool", false)
                ]),
            Snapshot(
                Board2Id,
                "Beta",
                IsRunning: true,
                LastSeaportDelta: CreateDelta(withdraw: (3, DnaSolid, 5m), deposit: (7, DnaGas, 2m)),
                IntoFactory:
                [
                    new SeaportFlowLine("sea2", "SeaportConnector", "out", "b2", "in", 0.5, "Pool → factory", null)
                ],
                OutOfFactory:
                [
                    new SeaportFlowLine("sea2", "SeaportConnector", "in", "b2", "out", 0.1, "Factory → pool", null)
                ],
                SeaportPorts:
                [
                    new SeaportPortFlowDetail("sea2", "SeaportConnector", "out", "out", true, "b2", "in", 3, DnaSolid, "E03", "From pool", false),
                    new SeaportPortFlowDetail("sea2", "SeaportConnector", "in", "in", true, "b2", "out", 7, DnaGas, "E07", "To pool", false)
                ])
        };

        var flows = PlayerSeaportElementFlowAggregator.Aggregate(snapshots);

        var withdrawKey = new PoolStackKey(3, DnaSolid);
        var depositKey = new PoolStackKey(7, DnaGas);
        Assert.True(flows[withdrawKey].ConsumedByFactory);
        Assert.Equal(0.7, flows[withdrawKey].ConsumeUnitsPerSecond!.Value, 3);
        Assert.False(flows[withdrawKey].ProducedByFactory);
        Assert.False(flows[withdrawKey].FlowIsEstimate);
        Assert.True(flows[depositKey].ProducedByFactory);
        Assert.Equal(0.1, flows[depositKey].ProduceUnitsPerSecond!.Value, 3);
        Assert.False(flows[depositKey].ConsumedByFactory);
    }

    [Fact]
    public void Aggregate_lists_boards_and_machines_for_variant()
    {
        var snapshots = new[]
        {
            Snapshot(
                Board1Id,
                "Cool chain",
                IsRunning: false,
                IntoFactory:
                [
                    new SeaportFlowLine("sea1", "SeaportConnector", "out", "boiler1", "in", 0.5, "Pool → factory", null)
                ],
                OutOfFactory: [],
                SeaportPorts:
                [
                    new SeaportPortFlowDetail("sea1", "SeaportConnector", "out", "out", true, "boiler1", "in", 3, DnaSolid, "E03", "From pool", true)
                ],
                MachinePortFlows:
                [
                    new MachinePortFlowDetail(
                        "boiler1", "Boiler", "out", "mix1", "in",
                        3, "E03", 3, "E03", "solid", "solid", DnaSolid, DnaSolid,
                        null, "Heats material", MaterialProcessStatus.Transformed, false, true, false)
                ])
        };

        var key = new PoolStackKey(3, DnaSolid);
        var flow = PlayerSeaportElementFlowAggregator.Aggregate(snapshots)[key];

        Assert.Single(flow.Boards);
        Assert.Equal("Cool chain", flow.Boards[0].BoardName);
        Assert.Equal(Board1Id, flow.Boards[0].BoardId);
        Assert.Contains(flow.Boards[0].Machines, m => m.MachineId == "boiler1" && m.Role == "consume");
    }

    [Fact]
    public void Aggregate_same_element_different_variants_does_not_merge_directions()
    {
        var snapshots = new[]
        {
            Snapshot(
                Board1Id,
                "Split",
                IsRunning: false,
                IntoFactory:
                [
                    new SeaportFlowLine("sea1", "SeaportConnector", "out", "b1", "in", 0.5, "Pool → factory", null)
                ],
                OutOfFactory:
                [
                    new SeaportFlowLine("sea1", "SeaportConnector", "in", "b1", "out", 0.4, "Factory → pool", null)
                ],
                SeaportPorts:
                [
                    new SeaportPortFlowDetail("sea1", "SeaportConnector", "out", "out", true, "b1", "in", 3, DnaSolid, "E03", "From pool", true),
                    new SeaportPortFlowDetail("sea1", "SeaportConnector", "in", "in", true, "b1", "out", 3, DnaGas, "E03", "To pool", true)
                ])
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
            Snapshot(
                Board1Id,
                "Stopped",
                IsRunning: false,
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
    public void Aggregate_ignores_tick_delta_and_uses_planned_ports_when_running()
    {
        var snapshots = new[]
        {
            Snapshot(
                Board1Id,
                "Running",
                IsRunning: true,
                LastSeaportDelta: CreateDelta(withdraw: (3, DnaSolid, 2m)),
                IntoFactory:
                [
                    new SeaportFlowLine("sea1", "SeaportConnector", "out", "b1", "in", 0.5, "Pool → factory", null)
                ],
                OutOfFactory: [],
                SeaportPorts:
                [
                    new SeaportPortFlowDetail("sea1", "SeaportConnector", "out", "out", true, "b1", "in", 99, DnaGas, "E99", "From pool", false)
                ])
        };

        var flows = PlayerSeaportElementFlowAggregator.Aggregate(snapshots);

        Assert.False(flows.ContainsKey(new PoolStackKey(3, DnaSolid)));
        Assert.True(flows.ContainsKey(new PoolStackKey(99, DnaGas)));
        Assert.Equal(0.5, flows[new PoolStackKey(99, DnaGas)].ConsumeUnitsPerSecond);
    }

    [Fact]
    public void Aggregate_does_not_mark_internal_upstream_elements_when_stopped()
    {
        var snapshots = new[]
        {
            Snapshot(
                Board1Id,
                "Withdraw only",
                IsRunning: false,
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
        Assert.Single(flows);
    }

    [Fact]
    public void Aggregate_marks_produce_when_in_port_has_planned_deposit_variant()
    {
        var heatedDna = DnaTransforms.Heat(DnaSolid);
        var snapshots = new[]
        {
            Snapshot(
                Board1Id,
                "Heated out",
                IsRunning: false,
                IntoFactory: [],
                OutOfFactory:
                [
                    new SeaportFlowLine("sea1", "SeaportConnector", "in", "boiler1", "out", 0.4, "Factory → pool", null)
                ],
                SeaportPorts:
                [
                    new SeaportPortFlowDetail("sea1", "SeaportConnector", "in", "in", true, "boiler1", "out", 3, heatedDna, "E03", "To pool", true)
                ])
        };

        var flows = PlayerSeaportElementFlowAggregator.Aggregate(snapshots);

        var key = new PoolStackKey(3, heatedDna);
        Assert.True(flows[key].ProducedByFactory);
        Assert.False(flows[key].ConsumedByFactory);
        Assert.Equal(0.4, flows[key].ProduceUnitsPerSecond);
    }

    private static PlayerBoardSeaportFlowSnapshot Snapshot(
        Guid boardId,
        string boardName,
        bool IsRunning,
        IReadOnlyList<SeaportFlowLine>? IntoFactory = null,
        IReadOnlyList<SeaportFlowLine>? OutOfFactory = null,
        IReadOnlyList<SeaportPortFlowDetail>? SeaportPorts = null,
        IReadOnlyList<MachinePortFlowDetail>? MachinePortFlows = null,
        SeaportTickDelta? LastSeaportDelta = null) =>
        new(
            boardId,
            boardName,
            IsRunning,
            TickIntervalSeconds: 10,
            LastSeaportDelta,
            IntoFactory ?? [],
            OutOfFactory ?? [],
            SeaportPorts ?? [],
            MachinePortFlows ?? []);

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
