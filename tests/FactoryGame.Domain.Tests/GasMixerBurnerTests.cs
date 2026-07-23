using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class GasMixerBurnerTests
{
    private static long BuildGasDna(int boilingPoint = 2500, int flammabilityRaw = 180)
    {
        var dna = 2L << DnaLayout.PhaseShift;
        dna |= (long)boilingPoint << DnaLayout.BoilingShift;
        dna |= (long)flammabilityRaw << DnaLayout.FlammabilityShift;
        return dna;
    }

    [Fact]
    public void Advance_GasMixer_blends_two_gases_to_gas_out()
    {
        var gasA = BuildGasDna(2400, 160);
        var gasB = BuildGasDna(2600, 200);
        var plan = new SimulationPlan(
            [new SimulationMachine("gm1", "GasMixer", """{"ratioPermille":500}""")],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["gm1"].GetOrCreateInput("in1").TryEnqueue(new MaterialPacket
        {
            ElementId = 2,
            Dna = gasA,
            Quantity = 1
        });
        state.Machines["gm1"].GetOrCreateInput("in2").TryEnqueue(new MaterialPacket
        {
            ElementId = 3,
            Dna = gasB,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        var outp = r1.State.Machines["gm1"].OutputPorts["out"].Peek();
        Assert.NotNull(outp);
        Assert.Equal(MaterialPhase.Gas, DnaDecoder.Decode(outp.Dna).Phase);
        Assert.Null(r1.State.Machines["gm1"].BlockedReason);
    }

    [Fact]
    public void Advance_GasMixer_blocks_liquid()
    {
        var plan = new SimulationPlan(
            [new SimulationMachine("gm1", "GasMixer", null)],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["gm1"].GetOrCreateInput("in1").TryEnqueue(new MaterialPacket
        {
            ElementId = 1,
            Dna = BuildGasDna(),
            Quantity = 1
        });
        state.Machines["gm1"].GetOrCreateInput("in2").TryEnqueue(new MaterialPacket
        {
            ElementId = 1,
            Dna = 1L << DnaLayout.PhaseShift,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        Assert.Contains("gas", r1.State.Machines["gm1"].BlockedReason ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Advance_Burner_consumes_gas_with_no_output()
    {
        var gas = BuildGasDna(flammabilityRaw: 200);
        Assert.True(DnaDecoder.Decode(gas).Flammability >= 40);

        var plan = new SimulationPlan(
            [new SimulationMachine("b1", "Burner", null)],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["b1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 4,
            Dna = gas,
            Quantity = 3
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        Assert.Null(r1.State.Machines["b1"].BlockedReason);
        var remaining = r1.State.Machines["b1"].GetOrCreateInput("in").Peek()?.Quantity ?? 0m;
        Assert.True(remaining < 3m);
    }

    [Fact]
    public void Advance_Burner_blocks_inert_gas()
    {
        var inertGas = BuildGasDna(flammabilityRaw: 20);
        Assert.True(DnaDecoder.Decode(inertGas).Flammability < 40);

        var plan = new SimulationPlan(
            [new SimulationMachine("b1", "Burner", null)],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["b1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 4,
            Dna = inertGas,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        Assert.Contains("inert", r1.State.Machines["b1"].BlockedReason ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.False(r1.State.Machines["b1"].GetOrCreateInput("in").IsEmpty);
    }

    [Fact]
    public void AssessElementInput_lists_gas_machines_for_gas()
    {
        var dna = BuildGasDna(boilingPoint: 2048);
        var rows = MachineInputCompatibility.AssessElementInput(dna, 4);

        Assert.Contains(rows, r => r.MachineType == "GasMixer" && r.Fit == MachineInputFit.Good);
        Assert.Contains(rows, r => r.MachineType == "Burner" && r.Fit == MachineInputFit.Good);
        Assert.Contains(rows, r => r.MachineType == "Condenser" && r.Fit == MachineInputFit.Good);
        Assert.Contains(rows, r => r.MachineType == "Destilator" && r.Fit == MachineInputFit.Good);
    }
}
