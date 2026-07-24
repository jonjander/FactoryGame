using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class ToxicMelterTests
{
    private static long BuildDna(MaterialPhase phase, int toxicityScaled, int spreadSeed = 0)
    {
        var phaseBits = phase switch
        {
            MaterialPhase.Solid => 0L,
            MaterialPhase.Liquid => 1L,
            MaterialPhase.Gas => 2L,
            _ => 0L
        };

        var toxRaw = Math.Clamp(toxicityScaled * 255 / 100, 0, 255);
        var dna = phaseBits << DnaLayout.PhaseShift;
        dna |= (long)toxRaw << DnaLayout.ToxicityShift;
        dna |= (long)2048 << DnaLayout.BoilingShift;
        dna |= (long)1800 << DnaLayout.FreezeShift;
        if (spreadSeed != 0)
            dna ^= (long)spreadSeed << 32 ^ (long)spreadSeed << 8;
        return dna;
    }

    [Fact]
    public void Advance_ToxicMelter_splits_toxic_gas_and_clean_solid()
    {
        var toxicGas = BuildDna(MaterialPhase.Gas, toxicityScaled: 75, spreadSeed: 0x5555);
        var cleanSolid = BuildDna(MaterialPhase.Solid, toxicityScaled: 10, spreadSeed: 0x1111);

        var plan = new SimulationPlan(
            [new SimulationMachine("tm1", "ToxicMelter", """{"gasSplitPermille":400}""")],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["tm1"].GetOrCreateInput("in1").TryEnqueue(new MaterialPacket
        {
            ElementId = 5,
            Dna = toxicGas,
            Quantity = 1
        });
        state.Machines["tm1"].GetOrCreateInput("in2").TryEnqueue(new MaterialPacket
        {
            ElementId = 8,
            Dna = cleanSolid,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        var gas = r1.State.Machines["tm1"].OutputPorts["out1"].Peek();
        var liquid = r1.State.Machines["tm1"].OutputPorts["out2"].Peek();
        Assert.NotNull(gas);
        Assert.NotNull(liquid);
        Assert.Equal(MaterialPhase.Gas, DnaDecoder.Decode(gas.Dna).Phase);
        Assert.Equal(MaterialPhase.Liquid, DnaDecoder.Decode(liquid.Dna).Phase);
        Assert.True(DnaDecoder.Decode(gas.Dna).Toxicity >= 60);
        Assert.True(DnaDecoder.Decode(liquid.Dna).Toxicity <= 35);
        Assert.Equal(5, gas.ElementId);
        Assert.Equal(5, liquid.ElementId);
        Assert.Null(r1.State.Machines["tm1"].BlockedReason);
    }

    [Fact]
    public void Advance_ToxicMelter_blocks_low_toxic_fluid_on_in1()
    {
        var weakLiquid = BuildDna(MaterialPhase.Liquid, toxicityScaled: 30);
        var cleanSolid = BuildDna(MaterialPhase.Solid, toxicityScaled: 10);

        var plan = new SimulationPlan(
            [new SimulationMachine("tm1", "ToxicMelter", null)],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["tm1"].GetOrCreateInput("in1").TryEnqueue(new MaterialPacket
        {
            ElementId = 3,
            Dna = weakLiquid,
            Quantity = 1
        });
        state.Machines["tm1"].GetOrCreateInput("in2").TryEnqueue(new MaterialPacket
        {
            ElementId = 4,
            Dna = cleanSolid,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        Assert.Contains("toxic", r1.State.Machines["tm1"].BlockedReason ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static long BuildExtremeToxicGasDna()
    {
        var dna = 2L << DnaLayout.PhaseShift;
        dna |= 200L << DnaLayout.ExplosivityShift;
        dna |= 40L << DnaLayout.FlammabilityShift;
        dna |= 235L << DnaLayout.ToxicityShift;
        dna |= 3200L << DnaLayout.BoilingShift;
        dna |= 400L << DnaLayout.FreezeShift;
        dna |= 12_345L << DnaLayout.FamilyShift;
        return dna;
    }

    private static long BuildCleanCarrierSolidDna()
    {
        var dna = 0L;
        dna |= 20L << DnaLayout.ToxicityShift;
        dna |= 1800L << DnaLayout.BoilingShift;
        dna |= 2200L << DnaLayout.FreezeShift;
        return dna;
    }

    [Fact]
    public void Advance_ToxicMelter_extreme_feed_transmutes_liquid_element_to_carrier()
    {
        var toxicGas = BuildExtremeToxicGasDna();
        var cleanSolid = BuildCleanCarrierSolidDna();
        Assert.True(DnaTransforms.MeasureDnaSpreadPermille(toxicGas) >= 500);

        var plan = new SimulationPlan(
            [new SimulationMachine("tm1", "ToxicMelter", """{"heatPermille":900}""")],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["tm1"].GetOrCreateInput("in1").TryEnqueue(new MaterialPacket
        {
            ElementId = 5,
            Dna = toxicGas,
            Quantity = 1
        });
        state.Machines["tm1"].GetOrCreateInput("in2").TryEnqueue(new MaterialPacket
        {
            ElementId = 11,
            Dna = cleanSolid,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        var liquid = r1.State.Machines["tm1"].OutputPorts["out2"].Peek();
        Assert.NotNull(liquid);
        Assert.Equal(11, liquid.ElementId);
    }
}
