using System.Text.Json;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
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
        delta.AddWithdraw(3, ElementCatalogLookup.CatalogDnaFor(3), 1m);
        delta.AddDeposit(3, ElementCatalogLookup.CatalogDnaFor(3), 1m);

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

    [Fact]
    public void Analyze_deposit_only_seaport_does_not_warn_seaport_in_idle()
    {
        var settings = JsonSerializer.SerializeToElement(new { outElementId = 0 });
        var machines = new[]
        {
            new MachineInfo("seaOut", "SeaportConnector", settings),
            new MachineInfo("melter1", "Melter", null)
        };
        var connections = new[] { new ConnectionInfo("melter1", "out", "seaOut", "in") };

        var report = BoardInfoAnalyzer.Analyze(new BoardInfoAnalyzeRequest(
            machines, connections, IsRunning: false, TickIntervalSeconds: 1));

        Assert.DoesNotContain(report.Issues, i => i.Code == "seaport_in_idle");
        Assert.DoesNotContain(report.Issues, i => i.Code == "seaport_out_idle" && i.MachineId == "seaOut");
    }

    [Fact]
    public void Analyze_warns_when_melter_needs_many_heat_steps()
    {
        var spreadSolid = BuildLowBoilSpreadSolidDna();
        var seaSettings = JsonSerializer.SerializeToElement(new { outElementId = 2, outMaterialDna = spreadSolid.ToString() });
        var melterSettings = JsonSerializer.SerializeToElement(new { cutBoiling = 2048, heatDelta = 32 });
        var machines = new[]
        {
            new MachineInfo("seaIn", "SeaportConnector", seaSettings),
            new MachineInfo("melter1", "Melter", melterSettings),
            new MachineInfo("seaOut", "SeaportConnector", JsonSerializer.SerializeToElement(new { outElementId = 0 }))
        };
        var connections = new[]
        {
            new ConnectionInfo("seaIn", "out", "melter1", "in"),
            new ConnectionInfo("melter1", "out", "seaOut", "in")
        };

        var report = BoardInfoAnalyzer.Analyze(new BoardInfoAnalyzeRequest(
            machines, connections, IsRunning: false, TickIntervalSeconds: 1));

        Assert.Contains(report.Issues, i => i.Code == "melter_slow_melt" && i.MachineId == "melter1");
    }

    private static long BuildLowBoilSpreadSolidDna()
    {
        const long phaseSolid = 1;
        var dna = phaseSolid << DnaLayout.PhaseShift;
        dna |= 101L << DnaLayout.FlammabilityShift;
        dna |= 100L << DnaLayout.ToxicityShift;
        dna |= 1046L << DnaLayout.BoilingShift;
        return dna;
    }

    [Fact]
    public void Analyze_melter_with_processing_slot_shows_processing_not_waiting()
    {
        const long e05Dna = 289644378304612875L;
        var plan = new SimulationPlan(
            [
                new SimulationMachine("seaIn", "SeaportConnector",
                    $$"""{"outElementId":5,"outMaterialDna":"{{e05Dna}}"}"""),
                new SimulationMachine("m1", "Melter", """{"cutBoiling":2048,"heatDelta":32}"""),
                new SimulationMachine("seaOut", "SeaportConnector", """{"outElementId":0}""")
            ],
            [
                new SimulationConnection("seaIn", "out", "m1", "in"),
                new SimulationConnection("m1", "out", "seaOut", "in")
            ]);
        var state = BoardTickEngine.CreateInitialState(plan);
        state.Machines["m1"].ProcessingSlot = new ProcessingSlotState
        {
            Packet = new MaterialPacket { ElementId = 5, Dna = e05Dna, Quantity = 1 },
            ElapsedTicks = 2,
            TotalTicks = 8,
            ProcessKind = "melt"
        };

        var tick = new BoardTickResult { State = state, SeaportDelta = new SeaportTickDelta(), Tick = 1, SummaryNote = "" };
        var machines = plan.Machines.Select(m =>
        {
            System.Text.Json.JsonElement? settings = null;
            if (!string.IsNullOrEmpty(m.SettingsJson))
                settings = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(m.SettingsJson);
            return new MachineInfo(m.Id, m.Type, settings);
        }).ToArray();

        var flows = MachinePortFlowAnalyzer.Analyze(
            machines,
            plan.Connections.Select(c => new ConnectionInfo(c.FromId, c.FromPort, c.ToId, c.ToPort)).ToArray(),
            isRunning: true,
            runtime: tick.State);

        var melterOut = Assert.Single(flows, f => f.MachineId == "m1" && f.Port == "out");
        Assert.Equal(MaterialProcessStatus.Processing, melterOut.ProcessStatus);
        Assert.Contains("melt", melterOut.TransformNote ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_warns_when_seaport_dna_variant_missing_from_pool()
    {
        var settings = System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            outElementId = 5,
            outMaterialDna = "289644378304612875"
        });
        var machines = new[]
        {
            new MachineInfo("sea1", "SeaportConnector", settings),
            new MachineInfo("m1", "Melter", null)
        };
        var connections = new[] { new ConnectionInfo("sea1", "out", "m1", "in") };
        var poolVariants = new Dictionary<PoolStackKey, decimal>
        {
            [new PoolStackKey(5, 999L)] = 23m
        };
        var poolQty = new Dictionary<int, decimal> { [5] = 23m };

        var report = BoardInfoAnalyzer.Analyze(new BoardInfoAnalyzeRequest(
            machines,
            connections,
            IsRunning: false,
            TickIntervalSeconds: 1,
            PoolQuantities: poolQty,
            PoolVariantQuantities: poolVariants));

        Assert.Contains(report.Issues, i => i.Code == "pool_variant_empty" && i.MachineId == "sea1");
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
