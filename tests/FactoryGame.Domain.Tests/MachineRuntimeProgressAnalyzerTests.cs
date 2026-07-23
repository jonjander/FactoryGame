using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Simulation;
using System.Text.Json;

namespace FactoryGame.Domain.Tests;

public sealed class MachineRuntimeProgressAnalyzerTests
{
    private static long BuildSolidDna(int boilingPoint = 512, int freezePoint = 512)
    {
        const long phaseSolid = 0;
        var dna = phaseSolid << DnaLayout.PhaseShift;
        dna |= (long)boilingPoint << DnaLayout.BoilingShift;
        dna |= (long)freezePoint << DnaLayout.FreezeShift;
        return dna;
    }

    [Fact]
    public void Analyze_melter_processing_emits_dual_progress_when_macro_differs_from_step()
    {
        var machines = new[] { new MachineInfo("m1", "Melter", JsonSerializer.SerializeToElement(new { cutBoiling = 2048, heatDelta = 32 })) };
        var runtime = new BoardLineState();
        var state = runtime.GetOrCreate("m1", "Melter");
        state.ProcessingSlot = new ProcessingSlotState
        {
            Packet = new MaterialPacket { ElementId = 5, Dna = BuildSolidDna(boilingPoint: 1024), Quantity = 1 },
            ElapsedTicks = 10,
            TotalTicks = 50,
            ProcessKind = "melt"
        };

        var results = MachineRuntimeProgressAnalyzer.Analyze(machines, runtime);
        var progress = Assert.Single(results);

        Assert.Equal("m1", progress.MachineId);
        Assert.True(progress.IsActive);
        Assert.Equal("melt", progress.ProcessKind);
        Assert.Equal(0.2, progress.StepProgress!.Value, 2);
        Assert.True(progress.OverallProgress > 0.4);
        Assert.True(progress.OverallProgress > progress.StepProgress);
    }

    [Fact]
    public void Analyze_mixer_waiting_for_second_input_shows_partial_readiness()
    {
        var machines = new[] { new MachineInfo("mix1", "Mixer", null) };
        var runtime = new BoardLineState();
        var state = runtime.GetOrCreate("mix1", "Mixer");
        state.GetOrCreateInput("in1").TryEnqueue(new MaterialPacket { ElementId = 1, Dna = 1, Quantity = 1 });

        var results = MachineRuntimeProgressAnalyzer.Analyze(machines, runtime);
        var progress = Assert.Single(results);

        Assert.Equal("waiting_inputs", progress.ProcessKind);
        Assert.Equal(0.5, progress.OverallProgress!.Value, 2);
        Assert.Null(progress.StepProgress);
        Assert.Equal(2, progress.InputNeeds.Count);
        Assert.True(progress.InputNeeds.Single(n => n.Port == "in1").Ready);
        Assert.False(progress.InputNeeds.Single(n => n.Port == "in2").Ready);
    }

    [Fact]
    public void Analyze_single_input_waiting_shows_zero_progress()
    {
        var machines = new[] { new MachineInfo("m1", "Melter", null) };
        var runtime = new BoardLineState();
        runtime.GetOrCreate("m1", "Melter");

        var results = MachineRuntimeProgressAnalyzer.Analyze(machines, runtime);
        var progress = Assert.Single(results);

        Assert.Equal("waiting_material", progress.ProcessKind);
        Assert.Equal(0, progress.OverallProgress);
        Assert.False(progress.IsActive);
    }
}
