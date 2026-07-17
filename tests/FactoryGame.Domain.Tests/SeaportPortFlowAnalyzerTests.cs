using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class SeaportPortFlowAnalyzerTests
{
    [Fact]
    public void AnalyzePorts_seaport_in_shows_upstream_and_inferred_element_in_loop()
    {
        var machines = new[]
        {
            new MachineInfo("seaportconnector1", "SeaportConnector", null),
            new MachineInfo("boiler1", "Boiler", null)
        };
        var connections = new[]
        {
            new ConnectionInfo("seaportconnector1", "out", "boiler1", "in"),
            new ConnectionInfo("boiler1", "out", "seaportconnector1", "in")
        };

        var ports = SeaportPortFlowAnalyzer.AnalyzePorts(
            machines, connections, isRunning: false, runtime: null, lastDelta: null);

        var seaportIn = Assert.Single(ports, p => p.MachineId == "seaportconnector1" && p.Port == "in");
        Assert.True(seaportIn.IsConnected);
        Assert.Equal("boiler1", seaportIn.LinkedMachineId);
        Assert.Equal("out", seaportIn.LinkedPort);
        Assert.Null(seaportIn.ElementId);
        Assert.Null(seaportIn.ElementSymbol);
        Assert.Contains("boiler1.out", seaportIn.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalyzePorts_unconnected_in_port_reports_idle_summary()
    {
        var machines = new[] { new MachineInfo("sea1", "SeaportConnector", null) };
        var ports = SeaportPortFlowAnalyzer.AnalyzePorts(
            machines, Array.Empty<ConnectionInfo>(), isRunning: false, runtime: null, lastDelta: null);

        var seaportIn = Assert.Single(ports, p => p.Port == "in");
        Assert.False(seaportIn.IsConnected);
        Assert.Contains("not connected", seaportIn.Summary, StringComparison.OrdinalIgnoreCase);
    }
}
