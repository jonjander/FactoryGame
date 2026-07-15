using FactoryGame.Domain.Simulation;
using FactoryGame.Infrastructure.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class RateAndLogisticsTests
{
    private static long LiquidDna => 1L << Dna.DnaLayout.PhaseShift;

    [Fact]
    public void Tank_stores_when_output_blocked()
    {
        var plan = new SimulationPlan(
            [new SimulationMachine("t1", "Tank", """{"tankSize":0}""")],
            []);

        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["t1"].GetOrCreateOutput("out").TryEnqueue(new MaterialPacket
        {
            ElementId = 99,
            Dna = LiquidDna,
            Quantity = 1
        });
        state.Machines["t1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 1,
            Dna = LiquidDna,
            Quantity = 5
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);
        var tank = r1.State.Machines["t1"].Tank;
        Assert.NotNull(tank);
        Assert.True(tank!.StoredQuantity > 0);
        Assert.True(tank.StoredQuantity <= 8);
    }

    [Fact]
    public void RateLimiter_caps_flow_to_max_rate()
    {
        var plan = new SimulationPlan(
            [new SimulationMachine("rl1", "RateLimiter", """{"maxRatePermille":250}""")],
            []);

        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["rl1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 1,
            Dna = LiquidDna,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);
        var outPkt = r1.State.Machines["rl1"].OutputPorts["out"].Peek();
        Assert.NotNull(outPkt);
        Assert.Equal(0.25m, outPkt!.Quantity);
    }

    [Fact]
    public void Junction_alternates_between_two_outputs()
    {
        var plan = new SimulationPlan(
            [
                new SimulationMachine("j1", "Junction", null),
                new SimulationMachine("a1", "Heater", null),
                new SimulationMachine("a2", "Cooler", null)
            ],
            [
                new SimulationConnection("j1", "out1", "a1", "in"),
                new SimulationConnection("j1", "out2", "a2", "in")
            ]);

        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["j1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 1,
            Dna = LiquidDna,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);
        var out1 = !r1.State.Machines["j1"].OutputPorts["out1"].IsEmpty;
        var out2 = !r1.State.Machines["j1"].OutputPorts["out2"].IsEmpty;
        Assert.True(out1 ^ out2);
    }

    [Fact]
    public void Operation_rate_50_percent_takes_longer_than_100_percent()
    {
        var planFast = new SimulationPlan(
            [new SimulationMachine("b1", "Boiler", """{"heatDelta":16,"operationRatePermille":1000}""")],
            []);
        var planSlow = new SimulationPlan(
            [new SimulationMachine("b1", "Boiler", """{"heatDelta":16,"operationRatePermille":500}""")],
            []);

        var seed = new MaterialPacket { ElementId = 1, Dna = LiquidDna, Quantity = 1 };
        var fastState = BoardTickEngine.CreateInitialState(planFast);
        fastState.Machines["b1"].GetOrCreateInput("in").TryEnqueue(seed.Clone());
        var slowState = BoardTickEngine.CreateInitialState(planSlow);
        slowState.Machines["b1"].GetOrCreateInput("in").TryEnqueue(seed.Clone());

        var fastTicks = FirstOutputTick(planFast, fastState);
        var slowTicks = FirstOutputTick(planSlow, slowState);
        Assert.True(slowTicks > fastTicks);
    }

    private static int FirstOutputTick(SimulationPlan plan, BoardLineState state)
    {
        for (long t = 1; t <= 20; t++)
        {
            var r = BoardTickEngine.Advance(plan, state, t, 1m, null);
            state = r.State;
            if (!state.Machines.Values.First().OutputPorts["out"].IsEmpty)
                return (int)t;
        }
        return 21;
    }

    [Fact]
    public void Serializer_roundtrip_preserves_internal_state()
    {
        var plan = new SimulationPlan(
            [new SimulationMachine("t1", "Tank", """{"tankSize":1}""")],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["t1"].Tank = new TankInternalState
        {
            Capacity = 24
        };
        state.Machines["t1"].Tank!.Storage.Add(new MaterialPacket
        {
            ElementId = 2,
            Dna = LiquidDna,
            Quantity = 3
        });
        state.Machines["t1"].Junction = new JunctionInternalState { NextOutIndex = 1, Out1Debt = 0.5m };

        var json = BoardLineStateSerializer.Serialize(state);
        var restored = BoardLineStateSerializer.Deserialize(json);

        var tank = restored.Machines["t1"].Tank;
        Assert.NotNull(tank);
        Assert.Equal(24, tank!.Capacity);
        Assert.Equal(3m, tank.Storage[0].Quantity);
        Assert.Equal(1, restored.Machines["t1"].Junction!.NextOutIndex);
    }

    [Fact]
    public void Advance_deterministic_across_identical_runs()
    {
        var plan = new SimulationPlan(
            [new SimulationMachine("b1", "Boiler", """{"heatDelta":8,"operationRatePermille":1000}""")],
            []);
        var seed = BoardTickEngine.CreateInitialState(plan);
        seed.Machines["b1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 1,
            Dna = LiquidDna,
            Quantity = 1
        });

        var a = TickHelper.Run(plan, seed, 5);
        var b = TickHelper.Run(plan, seed.CloneShallow(), 5);
        Assert.Equal(
            a.State.Machines["b1"].OutputPorts["out"].Peek()?.Dna,
            b.State.Machines["b1"].OutputPorts["out"].Peek()?.Dna);
    }
}

internal static class TickHelper
{
    public static BoardTickResult Run(SimulationPlan plan, BoardLineState state, int ticks, ISeaportPoolSink? pool = null)
    {
        BoardTickResult? last = null;
        for (long t = 1; t <= ticks; t++)
        {
            last = BoardTickEngine.Advance(plan, state, t, 1m, pool);
            state = last.State;
        }
        return last!;
    }
}
