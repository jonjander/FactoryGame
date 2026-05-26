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

    private static long BuildSpreadLiquidDna()
    {
        var dna = BuildLiquidDna();
        dna |= 200L << DnaLayout.ExplosivityShift;
        dna |= 40L << DnaLayout.FlammabilityShift;
        dna |= 220L << DnaLayout.ToxicityShift;
        dna |= 3200L << DnaLayout.BoilingShift;
        dna |= 400L << DnaLayout.FreezeShift;
        dna |= 12_345L << DnaLayout.FamilyShift;
        return dna;
    }

    private static long BuildCompactLiquidDna()
    {
        var dna = BuildLiquidDna();
        dna |= 100L << DnaLayout.ExplosivityShift;
        dna |= 102L << DnaLayout.FlammabilityShift;
        dna |= 101L << DnaLayout.ToxicityShift;
        return dna;
    }

    private static long BuildSpreadSolidDna()
    {
        var dna = 0L;
        dna |= 180L << DnaLayout.ExplosivityShift;
        dna |= 50L << DnaLayout.FlammabilityShift;
        dna |= 210L << DnaLayout.ToxicityShift;
        dna |= 2800L << DnaLayout.BoilingShift;
        dna |= 900L << DnaLayout.FreezeShift;
        dna |= 8000L << DnaLayout.FamilyShift;
        return dna;
    }

    private static long BuildCompactSolidDna()
    {
        var dna = 0L;
        dna |= 100L << DnaLayout.ExplosivityShift;
        dna |= 101L << DnaLayout.FlammabilityShift;
        dna |= 100L << DnaLayout.ToxicityShift;
        return dna;
    }

    private static long BuildGasDna(int boilingPoint = 2048)
    {
        const long phaseGas = 2;
        var dna = phaseGas << DnaLayout.PhaseShift;
        dna |= (long)boilingPoint << DnaLayout.BoilingShift;
        return dna;
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
    public void Advance_Destillator_splits_to_out1_heavy_and_out2_light()
    {
        var gasDna = BuildGasDna(boilingPoint: 2800);
        var plan = new SimulationPlan(
            [new SimulationMachine("d1", "Destilator", """{"cutBoiling":2048}""")],
            []);

        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["d1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 3,
            Dna = gasDna,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        var heavy = r1.State.Machines["d1"].OutputPorts["out1"].Peek();
        var light = r1.State.Machines["d1"].OutputPorts["out2"].Peek();
        Assert.NotNull(heavy);
        Assert.NotNull(light);
        Assert.True(heavy.Quantity > 0);
        Assert.True(light.Quantity > 0);
        Assert.Equal(1m, heavy.Quantity + light.Quantity);

        var heavyDna = DnaDecoder.Decode(heavy.Dna);
        var lightDna = DnaDecoder.Decode(light.Dna);
        Assert.Equal(MaterialPhase.Liquid, heavyDna.Phase);
        Assert.Equal(MaterialPhase.Gas, lightDna.Phase);
        Assert.True(heavyDna.BoilingPoint >= lightDna.BoilingPoint);
        Assert.Null(r1.State.Machines["d1"].BlockedReason);
    }

    [Fact]
    public void Advance_Condenser_gas_to_liquid()
    {
        var gasDna = BuildGasDna(boilingPoint: 2500);
        var plan = new SimulationPlan(
            [new SimulationMachine("c1", "Condenser", null)],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["c1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 4,
            Dna = gasDna,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        var outp = r1.State.Machines["c1"].OutputPorts["out"].Peek();
        Assert.NotNull(outp);
        Assert.Equal(MaterialPhase.Liquid, DnaDecoder.Decode(outp.Dna).Phase);
        Assert.NotEqual(gasDna, outp.Dna);
        Assert.Null(r1.State.Machines["c1"].BlockedReason);
    }

    [Fact]
    public void Advance_Condenser_blocks_liquid()
    {
        var plan = new SimulationPlan(
            [new SimulationMachine("c1", "Condenser", null)],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["c1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 1,
            Dna = BuildLiquidDna(),
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        Assert.Contains("Condenser", r1.State.Machines["c1"].BlockedReason ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void Advance_Crystallizer_spread_liquid_becomes_solid()
    {
        var spreadDna = BuildSpreadLiquidDna();
        var plan = new SimulationPlan(
            [new SimulationMachine("cr1", "Crystallizer", """{"cutFreeze":2048,"chillDelta":32}""")],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["cr1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 5,
            Dna = spreadDna,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        var outp = r1.State.Machines["cr1"].OutputPorts["out"].Peek();
        Assert.NotNull(outp);
        Assert.Equal(MaterialPhase.Solid, DnaDecoder.Decode(outp.Dna).Phase);
        Assert.NotEqual(spreadDna, outp.Dna);
    }

    [Fact]
    public void Advance_Crystallizer_compact_dna_passthrough_liquid()
    {
        var compactDna = BuildCompactLiquidDna();
        var plan = new SimulationPlan(
            [new SimulationMachine("cr1", "Crystallizer", null)],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["cr1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 2,
            Dna = compactDna,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        var outp = r1.State.Machines["cr1"].OutputPorts["out"].Peek();
        Assert.NotNull(outp);
        Assert.Equal(compactDna, outp.Dna);
        Assert.Equal(MaterialPhase.Liquid, DnaDecoder.Decode(outp.Dna).Phase);
    }

    [Fact]
    public void Advance_Crystallizer_blocks_gas()
    {
        var plan = new SimulationPlan(
            [new SimulationMachine("cr1", "Crystallizer", null)],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["cr1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 1,
            Dna = BuildGasDna(),
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        Assert.Contains("Crystallizer", r1.State.Machines["cr1"].BlockedReason ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void Advance_Cooler_keeps_liquid_while_Crystallizer_can_solidify_spread()
    {
        var spreadDna = BuildSpreadLiquidDna();
        var coolPlan = new SimulationPlan([new SimulationMachine("c1", "Cooler", null)], []);
        var crystPlan = new SimulationPlan(
            [new SimulationMachine("cr1", "Crystallizer", """{"cutFreeze":2048,"chillDelta":32}""")],
            []);

        var coolState = BoardTickEngine.CreateInitialState(coolPlan);
        coolState.Machines["c1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket { ElementId = 5, Dna = spreadDna, Quantity = 1 });
        var coolR = BoardTickEngine.Advance(coolPlan, coolState, 1, 1m, null);
        Assert.Equal(MaterialPhase.Liquid, DnaDecoder.Decode(coolR.State.Machines["c1"].OutputPorts["out"].Peek()!.Dna).Phase);

        var crystState = BoardTickEngine.CreateInitialState(crystPlan);
        crystState.Machines["cr1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket { ElementId = 5, Dna = spreadDna, Quantity = 1 });
        var crystR = BoardTickEngine.Advance(crystPlan, crystState, 1, 1m, null);
        Assert.Equal(MaterialPhase.Solid, DnaDecoder.Decode(crystR.State.Machines["cr1"].OutputPorts["out"].Peek()!.Dna).Phase);
    }

    [Fact]
    public void Advance_Melter_spread_solid_becomes_liquid()
    {
        var spreadSolid = BuildSpreadSolidDna();
        var plan = new SimulationPlan(
            [new SimulationMachine("m1", "Melter", """{"cutBoiling":1800,"heatDelta":40}""")],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["m1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 6,
            Dna = spreadSolid,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        var outp = r1.State.Machines["m1"].OutputPorts["out"].Peek();
        Assert.NotNull(outp);
        Assert.Equal(MaterialPhase.Liquid, DnaDecoder.Decode(outp.Dna).Phase);
    }

    [Fact]
    public void Advance_Melter_compact_solid_passthrough()
    {
        var compactSolid = BuildCompactSolidDna();
        var plan = new SimulationPlan(
            [new SimulationMachine("m1", "Melter", null)],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["m1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 1,
            Dna = compactSolid,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        var outp = r1.State.Machines["m1"].OutputPorts["out"].Peek();
        Assert.NotNull(outp);
        Assert.Equal(compactSolid, outp.Dna);
        Assert.Equal(MaterialPhase.Solid, DnaDecoder.Decode(outp.Dna).Phase);
    }

    [Fact]
    public void Advance_Mixer_virgin_low_intensity_is_poor_mix()
    {
        var dnaA = ElementCatalog.All[0].Dna;
        var dnaB = ElementCatalog.All[1].Dna;
        var plan = new SimulationPlan(
            [new SimulationMachine("mix1", "Mixer", """{"mixIntensityPermille":300}""")],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["mix1"].GetOrCreateInput("in1").TryEnqueue(new MaterialPacket { ElementId = 1, Dna = dnaA, Quantity = 1 });
        state.Machines["mix1"].GetOrCreateInput("in2").TryEnqueue(new MaterialPacket { ElementId = 2, Dna = dnaB, Quantity = 1 });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        var outp = r1.State.Machines["mix1"].OutputPorts["out"].Peek();
        Assert.NotNull(outp);
        Assert.Equal(MaterialQuality.Ash, outp.Quality);
        var (_, tier) = DnaTransforms.MixCombined(dnaA, dnaB, 500, 300);
        Assert.Equal(MixTier.Poor, tier);
    }

    [Fact]
    public void Advance_Mixer_high_intensity_processed_inputs_volatile_mix()
    {
        var dnaA = BuildSpreadLiquidDna();
        var dnaB = DnaTransforms.Heat(BuildSpreadLiquidDna(), 12);
        var plan = new SimulationPlan(
            [new SimulationMachine("mix1", "Mixer", """{"mixIntensityPermille":850,"ratioPermille":500}""")],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["mix1"].GetOrCreateInput("in1").TryEnqueue(new MaterialPacket { ElementId = 3, Dna = dnaA, Quantity = 1 });
        state.Machines["mix1"].GetOrCreateInput("in2").TryEnqueue(new MaterialPacket { ElementId = 4, Dna = dnaB, Quantity = 1 });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        var outp = r1.State.Machines["mix1"].OutputPorts["out"].Peek();
        Assert.NotNull(outp);
        Assert.Equal(MaterialQuality.Normal, outp.Quality);
        Assert.True(DnaTransforms.MeasureDnaSpreadPermille(outp.Dna) >= 400);
        Assert.Equal(MaterialPhase.Liquid, DnaDecoder.Decode(outp.Dna).Phase);
    }

    [Fact]
    public void Advance_LiquidSeparator_splits_spread_liquid_stays_liquid()
    {
        var spreadDna = BuildSpreadLiquidDna();
        var plan = new SimulationPlan(
            [new SimulationMachine("ls1", "LiquidSeparator", """{"cutFreeze":2048}""")],
            []);

        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["ls1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 5,
            Dna = spreadDna,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        var dense = r1.State.Machines["ls1"].OutputPorts["out1"].Peek();
        var light = r1.State.Machines["ls1"].OutputPorts["out2"].Peek();
        Assert.NotNull(dense);
        Assert.NotNull(light);
        Assert.Equal(1m, dense.Quantity + light.Quantity);
        Assert.Equal(MaterialPhase.Liquid, DnaDecoder.Decode(dense.Dna).Phase);
        Assert.Equal(MaterialPhase.Liquid, DnaDecoder.Decode(light.Dna).Phase);
        Assert.True(DnaDecoder.Decode(dense.Dna).FreezePoint >= DnaDecoder.Decode(light.Dna).FreezePoint);
    }

    [Fact]
    public void Advance_LiquidSeparator_compact_dna_passthrough_out1_only()
    {
        var compactDna = BuildCompactLiquidDna();
        var plan = new SimulationPlan(
            [new SimulationMachine("ls1", "LiquidSeparator", null)],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["ls1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 2,
            Dna = compactDna,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        Assert.False(r1.State.Machines["ls1"].OutputPorts["out1"].IsEmpty);
        Assert.True(r1.State.Machines["ls1"].OutputPorts["out2"].IsEmpty);
        var dense = r1.State.Machines["ls1"].OutputPorts["out1"].Peek();
        Assert.Equal(compactDna, dense!.Dna);
        Assert.Equal(1m, dense.Quantity);
    }

    [Fact]
    public void Advance_LiquidSeparator_blocks_non_liquid()
    {
        var gasDna = BuildGasDna();
        var plan = new SimulationPlan(
            [new SimulationMachine("ls1", "LiquidSeparator", null)],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["ls1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 1,
            Dna = gasDna,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        Assert.Contains("Liquid separator", r1.State.Machines["ls1"].BlockedReason ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Advance_Destillator_blocks_solid_phase()
    {
        var solidDna = 0L;
        var plan = new SimulationPlan(
            [new SimulationMachine("d1", "Destilator", null)],
            []);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["d1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 1,
            Dna = solidDna,
            Quantity = 1
        });

        var r1 = BoardTickEngine.Advance(plan, state, 1, 1m, null);

        Assert.Contains("Destilator", r1.State.Machines["d1"].BlockedReason ?? "", StringComparison.Ordinal);
        Assert.True(r1.State.Machines["d1"].OutputPorts["out1"].IsEmpty);
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

    [Fact]
    public void Advance_Seaport_liquid_separator_loop_withdraws_from_pool()
    {
        var pool = new TrackingPool();
        var plan = new SimulationPlan(
            [
                new SimulationMachine("sea1", "SeaportConnector", """{"outElementId":7}"""),
                new SimulationMachine("sep1", "LiquidSeparator", """{"cutFreeze":2048}""")
            ],
            [
                new SimulationConnection("sea1", "out", "sep1", "in"),
                new SimulationConnection("sep1", "out1", "sea1", "in"),
                new SimulationConnection("sep1", "out2", "sea1", "in")
            ]);

        var state = BoardTickEngine.CreateInitialState(plan);
        var result = BoardTickEngine.Advance(plan, state, 1, 1m, pool);

        Assert.Contains("withdrawn=1", result.SummaryNote);
        Assert.Contains("active=2", result.SummaryNote);
    }

    private sealed class FakePool : ISeaportPoolSink
    {
        public bool TryWithdraw(int elementId, long dna, decimal quantity) => true;
        public bool TryDeposit(int elementId, long dna, decimal quantity) => true;
    }

    private sealed class TrackingPool : ISeaportPoolSink
    {
        public bool TryWithdraw(int elementId, long dna, decimal quantity) => true;
        public bool TryDeposit(int elementId, long dna, decimal quantity) => true;
    }
}
