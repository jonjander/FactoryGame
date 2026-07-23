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
            ? $"cooling — freeze point {freeze}, cut {cutFreeze}: after one pass ~{afterOnePass} (need ≤{cutFreeze}, short by {need})"
            : $"cooling — freeze point {freeze}, cut {cutFreeze}: need to lower freeze point by {need} (~{passes} passes at −{chillDelta})";
    }

    public static string FormatMelterPending(long dna, int cutBoil, int heatDelta)
    {
        cutBoil = Math.Clamp(cutBoil, 0, (int)DnaLayout.BoilingMask);
        heatDelta = Math.Max(1, heatDelta);
        var boil = ReadBoilBand(dna);
        var afterOnePass = Math.Clamp(boil + heatDelta, 0, (int)DnaLayout.BoilingMask);
        var need = Math.Max(0, cutBoil - boil);
        return $"heating — boiling point {boil}, cut {cutBoil}: after one pass ~{afterOnePass} (need ≥{cutBoil}, short by {need})";
    }

    public static string FormatCoolerStep(long dnaBefore, long dnaAfter) =>
        $"cooled in Cooler (boiling point {ReadBoilBand(dnaBefore)}→{ReadBoilBand(dnaAfter)})";
}
