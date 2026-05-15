using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Simulation;

/// <summary>Deterministic bitwise DNA transforms for machine processors (v1).</summary>
public static class DnaTransforms
{
    public static long Heat(long dna, int bandDelta = 8) =>
        AdjustBand(dna, DnaLayout.BoilingShift, DnaLayout.BoilingMask, bandDelta);

    public static long Cool(long dna, int bandDelta = 8) =>
        AdjustBand(dna, DnaLayout.BoilingShift, DnaLayout.BoilingMask, -bandDelta);

    public static long Mix(long dnaA, long dnaB) =>
        unchecked(dnaA ^ (dnaB << 1) ^ (dnaB >> 3));

    private static long AdjustBand(long dna, int shift, long mask, int delta)
    {
        var u = (ulong)dna;
        var maskU = (ulong)mask;
        var raw = (int)((u >> shift) & maskU);
        var next = Math.Clamp(raw + delta, 0, (int)mask);
        u &= ~(maskU << shift);
        u |= (ulong)next << shift;
        return (long)u;
    }
}
