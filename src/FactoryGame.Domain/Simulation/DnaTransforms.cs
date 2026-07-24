using System.Numerics;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Simulation;

public enum MixTier
{
    Poor,
    Refined,
    Volatile
}

public sealed record ToxicMeltSplitResult(
    long GasDna,
    long LiquidDna,
    bool ExtremeTransmute,
    int GasElementId,
    int LiquidElementId);

/// <summary>Deterministic bitwise DNA transforms for machine processors (v1).</summary>
public static class DnaTransforms
{
    public static long Heat(long dna, int bandDelta = 8) =>
        AdjustBand(dna, DnaLayout.BoilingShift, DnaLayout.BoilingMask, bandDelta);

    public static long Cool(long dna, int bandDelta = 8) =>
        AdjustBand(dna, DnaLayout.BoilingShift, DnaLayout.BoilingMask, -bandDelta);

    /// <summary>Gas condenses to liquid: lower boil band and force liquid phase.</summary>
    public static long Condense(long dna, int bandDelta = 12)
    {
        var u = (ulong)Cool(dna, bandDelta);
        u = (u & ~3UL) | 1UL;
        return (long)u;
    }

    /// <summary>
    /// Lowers freeze band (not boil). Solid phase only when freeze at/below cut; otherwise stays liquid.
    /// </summary>
    public static (long Dna, bool Crystallized) Crystallize(long dna, int cutFreeze, int chillDelta = 16)
    {
        var u = (ulong)dna;
        cutFreeze = Math.Clamp(cutFreeze, 0, (int)DnaLayout.FreezeMask);
        var frz = (int)((u >> DnaLayout.FreezeShift) & DnaLayout.FreezeMask);
        var nextFrz = Math.Clamp(frz - chillDelta, 0, (int)DnaLayout.FreezeMask);
        u = SetBand(u, DnaLayout.FreezeShift, DnaLayout.FreezeMask, nextFrz);

        var lattice = (u >> 28) ^ (u >> 12);
        var tox = (int)((u >> DnaLayout.ToxicityShift) & DnaLayout.ToxicityMask);
        u = SetBand(u, DnaLayout.ToxicityShift, DnaLayout.ToxicityMask,
            Math.Clamp(tox + (int)(lattice & 0x07), 0, (int)DnaLayout.ToxicityMask));

        var crystallized = nextFrz <= cutFreeze;
        u = (u & ~3UL) | (crystallized ? 0UL : 1UL);
        return ((long)u, crystallized);
    }

    public static long Mix(long dnaA, long dnaB) =>
        MixCombined(dnaA, dnaB, 500, 350).Dna;

    /// <summary>
    /// Combines two DNA streams. Poor = compact/fattig; Volatile = spretig/ostabil for downstream machines.
    /// </summary>
    public static (long Dna, MixTier Tier) MixCombined(long dnaA, long dnaB, int ratioPermilleA, int mixIntensityPermille)
    {
        ratioPermilleA = Math.Clamp(ratioPermilleA, 100, 900);
        mixIntensityPermille = Math.Clamp(mixIntensityPermille, 100, 1000);

        var spreadA = MeasureDnaSpreadPermille(dnaA);
        var spreadB = MeasureDnaSpreadPermille(dnaB);
        var tier = ResolveMixTier(
            mixIntensityPermille,
            (spreadA + spreadB) / 2,
            IsCatalogDna(dnaA),
            IsCatalogDna(dnaB),
            ratioPermilleA);

        var dna = tier switch
        {
            MixTier.Volatile => MixVolatile(dnaA, dnaB, ratioPermilleA, mixIntensityPermille),
            MixTier.Refined => MixRefined(dnaA, dnaB, ratioPermilleA),
            _ => MixPoor(dnaA, dnaB, ratioPermilleA)
        };

        return (dna, tier);
    }

    /// <summary>Stable gas blend — averages bands and keeps gas phase (no volatile wake).</summary>
    public static long MixGas(long dnaA, long dnaB, int ratioPermilleA)
    {
        ratioPermilleA = Math.Clamp(ratioPermilleA, 100, 900);
        var ua = (ulong)dnaA;
        var ub = (ulong)dnaB;
        var weightA = ratioPermilleA / 1000.0;

        var u = (ulong)unchecked(dnaA ^ (dnaB << 2) ^ (dnaB >> 7) ^ (dnaA >> 13));

        var boil = (int)(((ua >> DnaLayout.BoilingShift) & DnaLayout.BoilingMask) * weightA
            + ((ub >> DnaLayout.BoilingShift) & DnaLayout.BoilingMask) * (1 - weightA));
        var frz = (int)(((ua >> DnaLayout.FreezeShift) & DnaLayout.FreezeMask) * weightA
            + ((ub >> DnaLayout.FreezeShift) & DnaLayout.FreezeMask) * (1 - weightA));
        var exp = (int)(((ua >> DnaLayout.ExplosivityShift) & DnaLayout.ExplosivityMask) * weightA
            + ((ub >> DnaLayout.ExplosivityShift) & DnaLayout.ExplosivityMask) * (1 - weightA));
        var fla = (int)(((ua >> DnaLayout.FlammabilityShift) & DnaLayout.FlammabilityMask) * weightA
            + ((ub >> DnaLayout.FlammabilityShift) & DnaLayout.FlammabilityMask) * (1 - weightA));
        var tox = (int)(((ua >> DnaLayout.ToxicityShift) & DnaLayout.ToxicityMask) * weightA
            + ((ub >> DnaLayout.ToxicityShift) & DnaLayout.ToxicityMask) * (1 - weightA));

        u = SetBand(u, DnaLayout.BoilingShift, DnaLayout.BoilingMask, boil);
        u = SetBand(u, DnaLayout.FreezeShift, DnaLayout.FreezeMask, frz);
        u = SetBand(u, DnaLayout.ExplosivityShift, DnaLayout.ExplosivityMask, exp);
        u = SetBand(u, DnaLayout.FlammabilityShift, DnaLayout.FlammabilityMask, fla);
        u = SetBand(u, DnaLayout.ToxicityShift, DnaLayout.ToxicityMask, tox);
        u = (u & ~3UL) | 2UL;
        return (long)u;
    }

    /// <summary>Solid to liquid via boil band (not the same as Heater/Cooler on boil alone).</summary>
    public static (long Dna, bool Melted) Melt(long dna, int cutBoil, int heatDelta = 20)
    {
        var u = (ulong)dna;
        cutBoil = Math.Clamp(cutBoil, 0, (int)DnaLayout.BoilingMask);
        var boil = (int)((u >> DnaLayout.BoilingShift) & DnaLayout.BoilingMask);
        var nextBoil = Math.Clamp(boil + heatDelta, 0, (int)DnaLayout.BoilingMask);
        u = SetBand(u, DnaLayout.BoilingShift, DnaLayout.BoilingMask, nextBoil);

        var thaw = (u >> 16) ^ (u >> 4);
        var frz = (int)((u >> DnaLayout.FreezeShift) & DnaLayout.FreezeMask);
        u = SetBand(u, DnaLayout.FreezeShift, DnaLayout.FreezeMask,
            Math.Clamp(frz + (int)(thaw & 0x1F), 0, (int)DnaLayout.FreezeMask));

        var melted = nextBoil >= cutBoil;
        u = (u & ~3UL) | (melted ? 1UL : (u & 3UL));
        return ((long)u, melted);
    }

    private static MixTier ResolveMixTier(
        int mixIntensityPermille,
        int processedScore,
        bool virginA,
        bool virginB,
        int ratioPermilleA)
    {
        var balanced = ratioPermilleA is >= 400 and <= 600;
        var virginOnly = virginA && virginB;

        if (mixIntensityPermille >= 750 && (!virginOnly || processedScore >= 280))
            return MixTier.Volatile;
        if (mixIntensityPermille >= 520 && balanced && (!virginOnly || processedScore >= 200))
            return MixTier.Refined;
        return MixTier.Poor;
    }

    private static long MixPoor(long dnaA, long dnaB, int ratioPermilleA)
    {
        var dominant = ratioPermilleA >= 500 ? dnaA : dnaB;
        var other = ratioPermilleA >= 500 ? dnaB : dnaA;
        var u = (ulong)unchecked(dominant ^ (other >> 8) ^ (other << 3));
        u &= 0x0000_03FF_FFFF_FFF3UL;
        return (long)u;
    }

    private static long MixRefined(long dnaA, long dnaB, int ratioPermilleA)
    {
        var u = (ulong)unchecked(dnaA ^ (dnaB << 1) ^ (dnaB >> 3) ^ (dnaA >> 11));
        var weightA = ratioPermilleA / 1000.0;
        var boil = (int)(((u >> DnaLayout.BoilingShift) & DnaLayout.BoilingMask) * weightA
            + (((ulong)dnaB >> DnaLayout.BoilingShift) & DnaLayout.BoilingMask) * (1 - weightA));
        u = SetBand(u, DnaLayout.BoilingShift, DnaLayout.BoilingMask, boil);
        u = (u & ~3UL) | 1UL;
        return (long)u;
    }

    private static long MixVolatile(long dnaA, long dnaB, int ratioPermilleA, int mixIntensityPermille)
    {
        var u = (ulong)unchecked(
            dnaA ^ dnaB
            ^ (dnaA << 17) ^ (dnaB >> 11)
            ^ (dnaA >> 9) ^ (dnaB << 19)
            ^ ((long)mixIntensityPermille << 21));

        var ua = (ulong)dnaA;
        var ub = (ulong)dnaB;
        var boilA = (int)((ua >> DnaLayout.BoilingShift) & DnaLayout.BoilingMask);
        var boilB = (int)((ub >> DnaLayout.BoilingShift) & DnaLayout.BoilingMask);
        var frzA = (int)((ua >> DnaLayout.FreezeShift) & DnaLayout.FreezeMask);
        var frzB = (int)((ub >> DnaLayout.FreezeShift) & DnaLayout.FreezeMask);

        var skew = mixIntensityPermille / 40;
        var boil = Math.Clamp((boilA + boilB) / 2 + skew, 0, (int)DnaLayout.BoilingMask);
        var frz = Math.Clamp(Math.Abs(frzA - frzB) + skew / 2, 0, (int)DnaLayout.FreezeMask);

        u = SetBand(u, DnaLayout.BoilingShift, DnaLayout.BoilingMask, boil);
        u = SetBand(u, DnaLayout.FreezeShift, DnaLayout.FreezeMask, frz);
        u ^= (ua ^ ub) & 0x00FF_00FF_00UL;
        u = (u & ~3UL) | 1UL;
        return (long)u;
    }

    private static bool IsCatalogDna(long dna) =>
        ElementCatalog.All.Any(e => e.Dna == dna);

    /// <summary>
    /// Distillation split: heavy fraction condenses high-boil / upper-bit signature; light keeps volatile lower bands.
    /// </summary>
    public static (long Heavy, long Light) DistillFractions(long dna, int cutBoiling)
    {
        var u = (ulong)dna;
        var boil = (int)((u >> DnaLayout.BoilingShift) & DnaLayout.BoilingMask);
        cutBoiling = Math.Clamp(cutBoiling, 0, (int)DnaLayout.BoilingMask);

        var heavyBoil = Math.Clamp((boil + cutBoiling + (int)DnaLayout.BoilingMask) / 2, cutBoiling, (int)DnaLayout.BoilingMask);
        var lightBoil = Math.Clamp((boil + cutBoiling) / 2 - 1, 0, Math.Min(cutBoiling, (int)DnaLayout.BoilingMask));

        var heavyMask =
            Band(DnaLayout.PhaseShift, DnaLayout.PhaseMask)
            | Band(DnaLayout.FlammabilityShift, DnaLayout.FlammabilityMask)
            | Band(DnaLayout.BoilingShift, DnaLayout.BoilingMask)
            | Band(DnaLayout.FreezeShift, DnaLayout.FreezeMask)
            | Band(DnaLayout.FamilyShift, DnaLayout.FamilyMask);

        var lightMask =
            Band(DnaLayout.PhaseShift, DnaLayout.PhaseMask)
            | Band(DnaLayout.ExplosivityShift, DnaLayout.ExplosivityMask)
            | Band(DnaLayout.FlammabilityShift, DnaLayout.FlammabilityMask)
            | Band(DnaLayout.ToxicityShift, DnaLayout.ToxicityMask)
            | Band(DnaLayout.BoilingShift, DnaLayout.BoilingMask);

        var heavy = u & heavyMask;
        heavy = SetBand(heavy, DnaLayout.BoilingShift, DnaLayout.BoilingMask, heavyBoil);

        var massFold = (u >> 32) ^ (u >> 20);
        var frz = (int)((heavy >> DnaLayout.FreezeShift) & DnaLayout.FreezeMask);
        heavy = SetBand(heavy, DnaLayout.FreezeShift, DnaLayout.FreezeMask,
            Math.Clamp(frz + (int)(massFold & 0x1F), 0, (int)DnaLayout.FreezeMask));

        heavy = (heavy & ~3UL) | 1UL;

        var light = u & lightMask;
        light ^= (u << 9) & Band(DnaLayout.FlammabilityShift, DnaLayout.FlammabilityMask);
        light = SetBand(light, DnaLayout.BoilingShift, DnaLayout.BoilingMask, lightBoil);
        light = (light & ~3UL) | (lightBoil < cutBoiling ? 2UL : (u & 3UL));

        return ((long)heavy, (long)light);
    }

    /// <summary>
    /// How willing liquid DNA is to split: high = bands/bits spretiga (ostabilt); low = kompakt kedja.
    /// </summary>
    public static int MeasureDnaSpreadPermille(long dna)
    {
        var d = DnaDecoder.Decode(dna);
        var u = (ulong)(dna & ~3L);

        var bandSpan = ActiveBandSpan(u, d);

        if (u == 0)
            return 0;

        var pop = BitOperations.PopCount(u);
        var hi = 63 - BitOperations.LeadingZeroCount(u);
        var lo = BitOperations.TrailingZeroCount(u);
        var width = Math.Max(hi - lo + 1, 1);
        var density = (int)((long)pop * 1000 / width);

        var xorHalves = BitOperations.PopCount((u >> 32) ^ (u & 0xFFFFFFFFUL));
        var spanScore = Math.Min(bandSpan * 10, 450);
        var scatterScore = Math.Clamp(450 - density / 2, 0, 450);
        var xorScore = Math.Min(xorHalves * 70, 350);

        var spread = spanScore + scatterScore + xorScore;
        if (IsCompactDnaChain(bandSpan, density, width))
            spread = Math.Min(spread, 180);

        return Math.Clamp(spread, 0, 1000);
    }

    private static bool IsCompactDnaChain(int bandSpan, int density, int width) =>
        bandSpan <= 12 && width <= 36 && density >= 280;

    private static int ActiveBandSpan(ulong u, DecodedDna d)
    {
        var values = new List<int>(5);
        if ((u & Band(DnaLayout.ExplosivityShift, DnaLayout.ExplosivityMask)) != 0)
            values.Add(d.Explosivity);
        if ((u & Band(DnaLayout.FlammabilityShift, DnaLayout.FlammabilityMask)) != 0)
            values.Add(d.Flammability);
        if ((u & Band(DnaLayout.ToxicityShift, DnaLayout.ToxicityMask)) != 0)
            values.Add(d.Toxicity);
        if ((u & Band(DnaLayout.BoilingShift, DnaLayout.BoilingMask)) != 0)
            values.Add(d.BoilingPoint * 100 / 4095);
        if ((u & Band(DnaLayout.FreezeShift, DnaLayout.FreezeMask)) != 0)
            values.Add(d.FreezePoint * 100 / 4095);

        if (values.Count < 2)
            return 0;
        return values.Max() - values.Min();
    }

    /// <summary>Liquid–liquid split by freeze/cut; both fractions stay liquid (never gas).</summary>
    public static (long Dense, long Light) LiquidSeparateFractions(long dna, int cutFreeze)
    {
        var u = (ulong)dna;
        var frz = (int)((u >> DnaLayout.FreezeShift) & DnaLayout.FreezeMask);
        cutFreeze = Math.Clamp(cutFreeze, 0, (int)DnaLayout.FreezeMask);

        var denseFrz = Math.Clamp((frz + cutFreeze + (int)DnaLayout.FreezeMask) / 2, cutFreeze, (int)DnaLayout.FreezeMask);
        var lightFrz = Math.Clamp((frz + cutFreeze) / 2 - 1, 0, Math.Min(cutFreeze, (int)DnaLayout.FreezeMask));

        var denseMask =
            Band(DnaLayout.PhaseShift, DnaLayout.PhaseMask)
            | Band(DnaLayout.ToxicityShift, DnaLayout.ToxicityMask)
            | Band(DnaLayout.FreezeShift, DnaLayout.FreezeMask)
            | Band(DnaLayout.BoilingShift, DnaLayout.BoilingMask)
            | Band(DnaLayout.FamilyShift, DnaLayout.FamilyMask);

        var lightMask =
            Band(DnaLayout.PhaseShift, DnaLayout.PhaseMask)
            | Band(DnaLayout.ExplosivityShift, DnaLayout.ExplosivityMask)
            | Band(DnaLayout.FlammabilityShift, DnaLayout.FlammabilityMask)
            | Band(DnaLayout.BoilingShift, DnaLayout.BoilingMask)
            | Band(DnaLayout.FreezeShift, DnaLayout.FreezeMask);

        var dense = u & denseMask;
        dense = SetBand(dense, DnaLayout.FreezeShift, DnaLayout.FreezeMask, denseFrz);
        var chainFold = (u >> 24) ^ (u >> 8);
        var tox = (int)((dense >> DnaLayout.ToxicityShift) & DnaLayout.ToxicityMask);
        dense = SetBand(dense, DnaLayout.ToxicityShift, DnaLayout.ToxicityMask,
            Math.Clamp(tox + (int)(chainFold & 0x0F), 0, (int)DnaLayout.ToxicityMask));
        dense = (dense & ~3UL) | 1UL;

        var light = u & lightMask;
        light ^= (u << 11) & Band(DnaLayout.FlammabilityShift, DnaLayout.FlammabilityMask);
        light = SetBand(light, DnaLayout.FreezeShift, DnaLayout.FreezeMask, lightFrz);
        light = (light & ~3UL) | 1UL;

        return ((long)dense, (long)light);
    }

    /// <summary>
    /// Toxic melter: volatile toxic fluid + low-toxic solid carrier → vent gas (out1) and purified liquid (out2).
    /// Extreme feed can transmute the liquid to the carrier element id.
    /// </summary>
    public static ToxicMeltSplitResult ToxicMeltSplit(
        long toxicDna,
        int toxicElementId,
        long carrierDna,
        int carrierElementId,
        int cutBoiling,
        int heatPermille)
    {
        cutBoiling = Math.Clamp(cutBoiling, 0, (int)DnaLayout.BoilingMask);
        heatPermille = Math.Clamp(heatPermille, 100, 1000);

        var toxic = DnaDecoder.Decode(toxicDna);
        var carrier = DnaDecoder.Decode(carrierDna);
        var spread = MeasureDnaSpreadPermille(toxicDna);

        var (_, lightGas) = DistillFractions(toxicDna, cutBoiling);
        var gasU = (ulong)lightGas;
        var gasTox = (int)((gasU >> DnaLayout.ToxicityShift) & DnaLayout.ToxicityMask);
        gasTox = Math.Clamp(gasTox + 28 + heatPermille / 40, 0, (int)DnaLayout.ToxicityMask);
        gasU = SetBand(gasU, DnaLayout.ToxicityShift, DnaLayout.ToxicityMask, gasTox);
        gasU = (gasU & ~3UL) | 2UL;

        var (meltedCarrier, _) = Melt(carrierDna, cutBoiling, Math.Max(heatPermille / 45, 14));
        var (heavyToxic, _) = DistillFractions(toxicDna, cutBoiling);
        var liquidU = (ulong)meltedCarrier;
        liquidU ^= ((ulong)heavyToxic >> 12) & Band(DnaLayout.FamilyShift, DnaLayout.FamilyMask);
        var carrierToxRaw = carrier.Toxicity * (int)DnaLayout.ToxicityMask / 100;
        var scrubbed = Math.Clamp(carrierToxRaw + 8, 0, (int)(DnaLayout.ToxicityMask * 0.35));
        liquidU = SetBand(liquidU, DnaLayout.ToxicityShift, DnaLayout.ToxicityMask, scrubbed);
        liquidU = (liquidU & ~3UL) | 1UL;

        var extreme = toxic.Toxicity >= 88 && carrier.Toxicity <= 30 && spread >= 500;
        var gasElement = toxicElementId;
        var liquidElement = toxicElementId;
        if (extreme)
            liquidElement = carrierElementId;

        return new ToxicMeltSplitResult((long)gasU, (long)liquidU, extreme, gasElement, liquidElement);
    }

    private static ulong Band(int shift, long mask) => (ulong)mask << shift;

    private static ulong SetBand(ulong dna, int shift, long mask, int value)
    {
        var maskU = (ulong)mask;
        value = Math.Clamp(value, 0, (int)mask);
        dna &= ~(maskU << shift);
        dna |= (ulong)value << shift;
        return dna;
    }

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
