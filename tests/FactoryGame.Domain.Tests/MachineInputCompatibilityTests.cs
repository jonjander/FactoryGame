using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class MachineInputCompatibilityTests
{
    private static long BuildCompactSolidDna()
    {
        var dna = 0L;
        dna |= 100L << DnaLayout.ExplosivityShift;
        dna |= 101L << DnaLayout.FlammabilityShift;
        dna |= 100L << DnaLayout.ToxicityShift;
        return dna;
    }

    [Fact]
    public void Catalog_liquid_blocks_melter_but_allows_boiler()
    {
        var element = ElementCatalog.All.First(e => e.Id == 7);
        Assert.Equal(MaterialPhase.Liquid, DnaDecoder.Decode(element.Dna).Phase);

        Assert.Equal(MachineInputFit.Good, MachineInputCompatibility.Assess("Boiler", element.Dna, element.Id));
        Assert.Equal(MachineInputFit.Blocked, MachineInputCompatibility.Assess("Melter", element.Dna, element.Id));
    }

    [Fact]
    public void Compact_solid_is_limited_in_melter()
    {
        var dna = BuildCompactSolidDna();
        Assert.Equal(MaterialPhase.Solid, DnaDecoder.Decode(dna).Phase);
        Assert.True(DnaTransforms.MeasureDnaSpreadPermille(dna) < 220);

        Assert.Equal(MachineInputFit.Limited, MachineInputCompatibility.Assess("Melter", dna, 1));
        var note = MachineInputCompatibility.AssessElementInput(dna, 1)
            .First(r => r.MachineType == "Melter").Note;
        Assert.Contains("Compact solid", note!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void High_explosivity_blocks_heater_even_when_phase_ok()
    {
        var dna = ElementCatalog.All.First(e => e.Id == 7).Dna;
        dna &= ~(DnaLayout.ExplosivityMask << DnaLayout.ExplosivityShift);
        dna |= 255L << DnaLayout.ExplosivityShift;
        Assert.True(DnaDecoder.Decode(dna).Explosivity > 85);

        Assert.Equal(MachineInputFit.Blocked, MachineInputCompatibility.Assess("Heater", dna, 7));
        Assert.Contains("Explosivity", MachineInputCompatibility.GetPlayerBlockReason("Heater", dna)!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AssessElementInput_groups_good_limited_and_blocked()
    {
        var element = ElementCatalog.All.First(e => e.Id == 7);
        var rows = MachineInputCompatibility.AssessElementInput(element.Dna, element.Id);

        Assert.Contains(rows, r => r.MachineType == "Boiler" && r.Fit == MachineInputFit.Good);
        Assert.Contains(rows, r => r.MachineType == "Melter" && r.Fit == MachineInputFit.Blocked);
        Assert.Contains(rows, r => r.MachineType == "Sorter" && r.Fit == MachineInputFit.Limited);
    }
}
