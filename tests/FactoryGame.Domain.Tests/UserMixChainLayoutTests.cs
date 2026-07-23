using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Names;
using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class UserMixChainLayoutTests
{
    private const long DnaE03 = 144964032628459529L;
    private const long DnaE05 = 289768180736920073L;

    [Fact]
    public void User_layout_mixer_boiler_destillator_change_dna()
    {
        var dnaA = DnaE03;
        var dnaB = DnaE05;

        var mixPlan = new SimulationPlan(
            [new SimulationMachine("mix1", "Mixer", """{"ratioPermille":500,"mixIntensityPermille":850}""")],
            []);
        var mixState = BoardTickEngine.CreateInitialState(mixPlan);
        mixState.Machines["mix1"].GetOrCreateInput("in1").TryEnqueue(new MaterialPacket { ElementId = 3, Dna = dnaA, Quantity = 1 });
        mixState.Machines["mix1"].GetOrCreateInput("in2").TryEnqueue(new MaterialPacket { ElementId = 5, Dna = dnaB, Quantity = 1 });

        var mixResult = BoardTickEngine.Advance(mixPlan, mixState, 1, 1m, null);
        var mixed = mixResult.State.Machines["mix1"].OutputPorts["out"].Peek();
        Assert.NotNull(mixed);
        Assert.NotEqual(dnaA, mixed!.Dna);
        Assert.NotEqual(dnaB, mixed.Dna);
        Assert.Equal(3, mixed.ElementId);

        var boilerPlan = new SimulationPlan([new SimulationMachine("boiler1", "Boiler", null)], []);
        var boilerState = BoardTickEngine.CreateInitialState(boilerPlan);
        boilerState.Machines["boiler1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = mixed.ElementId,
            Dna = mixed.Dna,
            Quantity = mixed.Quantity
        });

        var boilerResult = TickHelper.Run(boilerPlan, boilerState, 20);
        var boiled = boilerResult.State.Machines["boiler1"].OutputPorts["out"].Peek()
                     ?? boilerResult.State.Machines["boiler1"].ProcessingSlot?.Packet;
        Assert.NotNull(boiled);
        Assert.NotEqual(mixed.Dna, boiled!.Dna);

        var destPlan = new SimulationPlan(
            [new SimulationMachine("dest1", "Destilator", """{"cutBoiling":3072,"refluxPermille":150}""")],
            []);
        var destState = BoardTickEngine.CreateInitialState(destPlan);
        destState.Machines["dest1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = boiled.ElementId,
            Dna = boiled.Dna,
            Quantity = boiled.Quantity
        });

        var destResult = BoardTickEngine.Advance(destPlan, destState, 1, 1m, null);
        Assert.Null(destResult.State.Machines["dest1"].BlockedReason);
        var heavy = destResult.State.Machines["dest1"].OutputPorts["out1"].Peek();
        var light = destResult.State.Machines["dest1"].OutputPorts["out2"].Peek();
        Assert.NotNull(heavy);
        Assert.NotNull(light);
        Assert.NotEqual(boiled.Dna, heavy!.Dna);
        Assert.NotEqual(boiled.Dna, light!.Dna);
    }

    [Fact]
    public void User_pool_dna_e03_matches_catalog()
    {
        Assert.Equal(ElementCatalog.All[2].Dna, DnaE03);
        Assert.NotEqual(DnaE03, DnaE05);
    }

    [Fact]
    public void Mixer_preview_shows_mixed_symbols_for_user_layout()
    {
        var machines = new[]
        {
            new MachineInfo("sea1", "SeaportConnector",
                System.Text.Json.JsonSerializer.SerializeToElement(new { outElementId = 3, outMaterialDna = DnaE03.ToString() })),
            new MachineInfo("sea2", "SeaportConnector",
                System.Text.Json.JsonSerializer.SerializeToElement(new { outElementId = 5, outMaterialDna = DnaE05.ToString() })),
            new MachineInfo("mix1", "Mixer",
                System.Text.Json.JsonSerializer.SerializeToElement(new { ratioPermille = 500, mixIntensityPermille = 850 }))
        };
        var connections = new[]
        {
            new ConnectionInfo("sea1", "out", "mix1", "in1"),
            new ConnectionInfo("sea2", "out", "mix1", "in2")
        };

        var flows = MachinePortFlowAnalyzer.Analyze(machines, connections, isRunning: false, runtime: null);
        var mixOut = Assert.Single(flows, f => f.MachineId == "mix1" && f.Port == "out");

        var (mixedDna, _) = DnaTransforms.MixCombined(DnaE03, DnaE05, 500, 850);
        Assert.True(mixOut.DnaChanged);
        Assert.Equal(MaterialLabelFormatter.Format(3, mixedDna), mixOut.OutputElementSymbol);
        Assert.Contains("mixed", mixOut.TransformNote ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static bool DnaTransforms_IsCatalog(long dna) =>
        FactoryGame.Domain.Content.ElementCatalog.All.Any(e => e.Dna == dna);
}
