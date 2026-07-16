using System.Text.Json;
using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class BoardInfoAnalyzerTests
{
    [Fact]
    public void Analyze_reports_seaport_in_and_out_flows()
    {
        var machines = new[]
        {
            new MachineInfo("sea1", "SeaportConnector", null),
            new MachineInfo("mix1", "Mixer", null)
        };
        var connections = new[]
        {
            new ConnectionInfo("sea1", "out", "mix1", "in1"),
            new ConnectionInfo("mix1", "out", "sea1", "in")
        };

        var report = BoardInfoAnalyzer.Analyze(new BoardInfoAnalyzeRequest(
            machines, connections, IsRunning: true, TickIntervalSeconds: 1));

        Assert.Single(report.IntoFactory);
        Assert.Single(report.OutOfFactory);
        Assert.Equal(2.0, report.TotalUnitsPerSecond, 3);
    }

    [Fact]
    public void Analyze_seaport_boiler_loop_shows_both_flows_and_connected_ports()
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

        var report = BoardInfoAnalyzer.Analyze(new BoardInfoAnalyzeRequest(
            machines, connections, IsRunning: false, TickIntervalSeconds: 10));

        Assert.Single(report.IntoFactory);
        Assert.Single(report.OutOfFactory);
        Assert.Contains(report.Issues, i => i.Code == "cycle_detected");
        Assert.DoesNotContain(report.Issues, i => i.Code == "port_unconnected" && i.MachineId == "boiler1");
        Assert.DoesNotContain(report.Issues, i => i.Code == "seaport_in_idle");
        Assert.DoesNotContain(report.Issues, i => i.Code == "seaport_out_idle");
    }

    [Fact]
    public void Analyze_warns_on_unconnected_mixer_port()
    {
        var machines = new[] { new MachineInfo("mix1", "Mixer", null) };
        var report = BoardInfoAnalyzer.Analyze(new BoardInfoAnalyzeRequest(
            machines, Array.Empty<ConnectionInfo>(), IsRunning: false, TickIntervalSeconds: 1));

        Assert.Contains(report.Issues, i => i.Code == "port_unconnected" && i.MachineId == "mix1");
    }

    [Fact]
    public void Analyze_detects_dna_incompatible_downstream_of_sorter()
    {
        var solidDna = 0x00010203_04050607L;
        var elementId = ElementCatalogElementIdForDna(solidDna);
        Assert.NotNull(elementId);

        var settings = JsonSerializer.SerializeToElement(new { port1 = new[] { elementId.Value } });
        var machines = new[]
        {
            new MachineInfo("s1", "Sorter", settings),
            new MachineInfo("b1", "Boiler", null)
        };
        var connections = new[] { new ConnectionInfo("s1", "out1", "b1", "in") };

        var report = BoardInfoAnalyzer.Analyze(new BoardInfoAnalyzeRequest(
            machines, connections, IsRunning: false, TickIntervalSeconds: 1));

        Assert.Contains(report.Issues, i => i.Code == "dna_incompatible");
    }

    [Fact]
    public void Analyze_warns_when_seaport_out_element_missing_from_pool()
    {
        var settings = JsonSerializer.SerializeToElement(new { outElementId = 7 });
        var machines = new[]
        {
            new MachineInfo("sea1", "SeaportConnector", settings),
            new MachineInfo("mix1", "Mixer", null)
        };
        var connections = new[] { new ConnectionInfo("sea1", "out", "mix1", "in1") };
        var pool = new Dictionary<int, decimal> { [7] = 0m };

        var report = BoardInfoAnalyzer.Analyze(new BoardInfoAnalyzeRequest(
            machines, connections, IsRunning: false, TickIntervalSeconds: 1, PoolQuantities: pool));

        Assert.Contains(report.Issues, i => i.Code == "pool_empty" && i.MachineId == "sea1");
    }

    [Fact]
    public void Analyze_running_with_empty_seaport_delta_uses_flow_list_throughput()
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

        var delta = new SeaportTickDelta();
        var report = BoardInfoAnalyzer.Analyze(new BoardInfoAnalyzeRequest(
            machines,
            connections,
            IsRunning: true,
            TickIntervalSeconds: 2,
            LastSeaportDelta: delta));

        Assert.True(report.TotalUnitsPerSecond > 0, "flow rows should yield non-zero throughput when delta is empty");
    }

    [Fact]
    public void Analyze_running_with_seaport_delta_and_flow_rows_keeps_positive_throughput()
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

        var delta = new SeaportTickDelta();
        delta.AddWithdraw(3, 1m);
        delta.AddDeposit(3, 1m);

        var report = BoardInfoAnalyzer.Analyze(new BoardInfoAnalyzeRequest(
            machines,
            connections,
            IsRunning: true,
            TickIntervalSeconds: 2,
            LastSeaportDelta: delta));

        Assert.True(report.TotalUnitsPerSecond > 0);
        Assert.True(report.IntoFactory.Count > 0);
        Assert.True(report.OutOfFactory.Count > 0);
    }

    private static int? ElementCatalogElementIdForDna(long dna)
    {
        foreach (var e in FactoryGame.Domain.Content.ElementCatalog.All)
        {
            if (e.Dna == dna)
                return e.Id;
        }

        return null;
    }
}
