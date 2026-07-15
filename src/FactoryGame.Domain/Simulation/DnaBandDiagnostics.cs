using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Simulation;

internal static class DnaBandDiagnostics
{
    public static int ReadFreezeBand(long dna) =>
        (int)(((ulong)dna >> DnaLayout.FreezeShift) & DnaLayout.FreezeMask);

    public static int ReadBoilBand(long dna) =>
        (int)(((ulong)dna >> DnaLayout.BoilingShift) & DnaLayout.BoilingMask);

    public static string FormatCrystallizerPending(long dna, int cutFreeze, int chillDelta)
    {
        cutFreeze = Math.Clamp(cutFreeze, 0, (int)DnaLayout.FreezeMask);
        chillDelta = Math.Max(1, chillDelta);
        var freeze = ReadFreezeBand(dna);
        var afterOnePass = Math.Clamp(freeze - chillDelta, 0, (int)DnaLayout.FreezeMask);
        var need = Math.Max(0, freeze - cutFreeze);
        var passes = need == 0 ? 0 : (int)Math.Ceiling(need / (double)chillDelta);
        return passes <= 1
            ? $"kyls — fryspunkt {freeze}, cut {cutFreeze}: efter en pass ~{afterOnePass} (behöver ≤{cutFreeze}, saknas {need})"
            : $"kyls — fryspunkt {freeze}, cut {cutFreeze}: behöver sänka fryspunkt med {need} (~{passes} pass à −{chillDelta})";
    }

    public static string FormatMelterPending(long dna, int cutBoil, int heatDelta)
    {
        cutBoil = Math.Clamp(cutBoil, 0, (int)DnaLayout.BoilingMask);
        heatDelta = Math.Max(1, heatDelta);
        var boil = ReadBoilBand(dna);
        var afterOnePass = Math.Clamp(boil + heatDelta, 0, (int)DnaLayout.BoilingMask);
        var need = Math.Max(0, cutBoil - boil);
        return $"värms — kokpunkt {boil}, cut {cutBoil}: efter en pass ~{afterOnePass} (behöver ≥{cutBoil}, saknas {need})";
    }

    public static string FormatCoolerStep(long dnaBefore, long dnaAfter, int coolDelta) =>
        $"kyls i Cooler (kokband {ReadBoilBand(dnaBefore)}→{ReadBoilBand(dnaAfter)}, Δ−{coolDelta})";
}
