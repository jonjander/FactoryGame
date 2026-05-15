using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class MachinePortFlowAnalyzerTests
{
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
        Assert.Equal(1, poolOut.OutputElementId);
        Assert.Equal("E01", poolOut.OutputElementSymbol);
        Assert.Contains("Pool", poolOut.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_boiler_out_predicts_heated_element()
    {
        var machines = new[]
        {
            new MachineInfo("sea1", "SeaportConnector", null),
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
        Assert.Contains("värms", boilerOut.TransformNote ?? boilerOut.Summary, StringComparison.OrdinalIgnoreCase);
    }
}
