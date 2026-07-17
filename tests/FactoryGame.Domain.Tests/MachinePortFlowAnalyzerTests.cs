using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class MachinePortFlowAnalyzerTests
{
    private static long BuildGasDna(int boilingPoint = 2500)
    {
        const long phaseGas = 2;
        var dna = phaseGas << DnaLayout.PhaseShift;
        dna |= (long)boilingPoint << DnaLayout.BoilingShift;
        return dna;
    }

    [Fact]
    public void Analyze_pool_out_port_shows_configured_element()
    {
        var machines = new[]
        {
            new MachineInfo("sea1", "SeaportConnector", null),
            new MachineInfo("b1", "Boiler", null)
        };
        var connections = new[] { new ConnectionInfo("sea1", "out", "b1", "in") };

        var flows = MachinePortFlowAnalyzer.Analyze(machines, connections, isRunning: false, runtime: null);
        var poolOut = Assert.Single(flows, f => f.IsPoolSource);

        Assert.Equal("sea1", poolOut.MachineId);
        Assert.Equal("out", poolOut.Port);
        Assert.Null(poolOut.OutputElementId);
        Assert.Null(poolOut.OutputElementSymbol);
        Assert.Contains("Pool", poolOut.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_boiler_out_predicts_heated_element()
    {
        var machines = new[]
        {
            new MachineInfo("sea1", "SeaportConnector", System.Text.Json.JsonSerializer.SerializeToElement(new { outElementId = 1 })),
            new MachineInfo("b1", "Boiler", null)
        };
        var connections = new[]
        {
            new ConnectionInfo("sea1", "out", "b1", "in"),
            new ConnectionInfo("b1", "out", "sea1", "in")
        };

        var flows = MachinePortFlowAnalyzer.Analyze(machines, connections, isRunning: false, runtime: null);
        var boilerOut = Assert.Single(flows, f => f.MachineId == "b1" && f.Port == "out");

        Assert.Equal("E01", boilerOut.InputElementSymbol);
        Assert.NotNull(boilerOut.OutputElementSymbol);
        Assert.Contains("heated", boilerOut.TransformNote ?? boilerOut.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_condenser_running_shows_gas_to_liquid_with_same_symbol()
    {
        var gasDna = BuildGasDna();
        var plan = new SimulationPlan(
            [
                new SimulationMachine("sea1", "SeaportConnector", """{"outElementId":4}"""),
                new SimulationMachine("c1", "Condenser", null)
            ],
            [
                new SimulationConnection("sea1", "out", "c1", "in"),
                new SimulationConnection("c1", "out", "sea1", "in")
            ]);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["c1"].GetOrCreateInput("in").TryEnqueue(new MaterialPacket
        {
            ElementId = 4,
            Dna = gasDna,
            Quantity = 1
        });

        var tick = TickHelper.Run(plan, state, 10);
        var machineInfos = plan.Machines.Select(m =>
        {
            System.Text.Json.JsonElement? settings = null;
            if (!string.IsNullOrEmpty(m.SettingsJson))
                settings = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(m.SettingsJson);
            return new MachineInfo(m.Id, m.Type, settings);
        }).ToArray();
        var flows = MachinePortFlowAnalyzer.Analyze(
            machineInfos,
            plan.Connections.Select(c => new ConnectionInfo(c.FromId, c.FromPort, c.ToId, c.ToPort)).ToArray(),
            isRunning: true,
            runtime: tick.State);

        var condenserOut = Assert.Single(flows, f => f.MachineId == "c1" && f.Port == "out");
        Assert.Equal("E04", condenserOut.InputElementSymbol);
        Assert.Equal("E04", condenserOut.OutputElementSymbol);
        Assert.Equal("Liquid", condenserOut.OutputPhase);
    }

    [Fact]
    public void Analyze_condenser_stopped_estimates_gas_to_liquid()
    {
        var machines = new[]
        {
            new MachineInfo("sea1", "SeaportConnector", System.Text.Json.JsonSerializer.SerializeToElement(new { outElementId = 4 })),
            new MachineInfo("c1", "Condenser", null)
        };
        var connections = new[]
        {
            new ConnectionInfo("sea1", "out", "c1", "in"),
            new ConnectionInfo("c1", "out", "sea1", "in")
        };

        var flows = MachinePortFlowAnalyzer.Analyze(machines, connections, isRunning: false, runtime: null);
        var condenserOut = Assert.Single(flows, f => f.MachineId == "c1" && f.Port == "out");

        Assert.Equal("E04", condenserOut.InputElementSymbol);
        Assert.Equal("E04", condenserOut.OutputElementSymbol);
        Assert.True(condenserOut.DnaChanged || condenserOut.InputPhase != condenserOut.OutputPhase);
        Assert.Equal(MaterialProcessStatus.Transformed, condenserOut.ProcessStatus);
    }
}
