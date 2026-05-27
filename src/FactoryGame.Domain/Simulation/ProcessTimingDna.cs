using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Simulation;

/// <summary>Multi-tick DNA processing with speed-dependent quality (v2).</summary>
internal static class ProcessTimingDna
{
    public static int ResolveTotalTicks(int totalDelta, string? settingsJson)
    {
        var opRate = MachineRateCatalog.GetOperationRatePermille(settingsJson);
        var baseTicks = Math.Max(1, (totalDelta + 3) / 4);
        return Math.Max(1, (int)Math.Ceiling(baseTicks * 1000m / opRate));
    }

    public static int ResolvePartialDelta(int totalDelta, int totalTicks, int elapsedTicks)
    {
        if (totalTicks <= 0)
            return totalDelta;
        if (elapsedTicks >= totalTicks)
            return totalDelta;
        var applied = totalDelta * elapsedTicks / totalTicks;
        var prev = totalDelta * (elapsedTicks - 1) / totalTicks;
        return Math.Max(1, applied - prev);
    }

    public static MaterialQuality ResolveCompletionQuality(
        int operationRatePermille,
        int totalDelta,
        long dnaBefore,
        long dnaAfter,
        string processKind)
    {
        if (operationRatePermille <= 800)
            return MaterialQuality.Normal;

        if (processKind is "heat" or "cool" or "condense")
        {
            var before = DnaDecoder.Decode(dnaBefore);
            var after = DnaDecoder.Decode(dnaAfter);
            var bandShift = Math.Abs(after.BoilingPoint - before.BoilingPoint);
            if (operationRatePermille >= 1000 && totalDelta >= 16 && bandShift > totalDelta + 4)
                return MaterialQuality.Ash;
        }

        return MaterialQuality.Normal;
    }

    public static ProcessingSlotState EnsureSlot(MachineRuntimeState machine) =>
        machine.ProcessingSlot ??= new ProcessingSlotState();

    public static long ApplyPartialTransform(
        string processKind,
        long dna,
        int partialDelta,
        string? settingsJson)
    {
        return processKind switch
        {
            "heat" => DnaTransforms.Heat(dna, partialDelta),
            "cool" => DnaTransforms.Cool(dna, partialDelta),
            "condense" => DnaTransforms.Condense(dna, partialDelta),
            _ => dna
        };
    }
}
