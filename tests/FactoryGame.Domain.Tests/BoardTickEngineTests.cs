using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class BoardTickEngineTests
{
    [Fact]
    public void Advance_Boiler_transforms_dna_deterministically()
    {
        var liquidDna = BuildLiquidDna();
        var plan = new SimulationPlan(
            [new SimulationMachine("b1", "Boiler", null)],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["b1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 1,
            Dna = liquidDna,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);
        var r2 = BoardTickEngine.Advance(plan, r1.State, 2, 1m, null);

        var out1 = r1.State.Machines["b1"].OutputPorts["out"].Peek();
        var out2 = r2.State.Machines["b1"].OutputPorts["out"].Peek();
        Assert.NotNull(out1);
        Assert.NotNull(out2);
        Assert.Equal(out1.Dna, out2.Dna);
        Assert.NotEqual(liquidDna, out1.Dna);
    }

    private static long BuildLiquidDna()
    {
        const long phaseLiquid = 1;
        return phaseLiquid << DnaLayout.PhaseShift;
    }

    [Fact]
    public void Advance_Sorter_routes_to_configured_port()
    {
        var liquidDna = BuildLiquidDna();
        var settings = """{"port1":[1]}""";
        var plan = new SimulationPlan(
            [new SimulationMachine("s1", "Sorter", settings)],
            []);

        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["s1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 1,
            Dna = liquidDna,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        Assert.False(r1.State.Machines["s1"].OutputPorts["out1"].IsEmpty);
        Assert.True(r1.State.Machines["s1"].OutputPorts["out4"].IsEmpty);
    }

    [Fact]
    public void TopologicalOrder_detects_cycle()
    {
        var plan = new SimulationPlan(
            [
                new SimulationMachine("a", "Mixer", null),
                new SimulationMachine("b", "Mixer", null)
            ],
            [
                new SimulationConnection("a", "out", "b", "in1"),
                new SimulationConnection("b", "out", "a", "in1")
            ]);

        var order = PlanGraph.TopologicalMachineOrder(plan, out var err);
        Assert.NotNull(err);
        Assert.Empty(order);
    }

    private sealed class FakePool : ISeaportPoolSink
    {
        public bool TryWithdraw(int elementId, decimal quantity) => true;
        public bool TryDeposit(int elementId, long dna, decimal quantity) => true;
    }
}
