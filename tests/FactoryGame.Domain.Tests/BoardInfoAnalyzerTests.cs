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
