namespace FactoryGame.Domain.Dna;

public static class DnaDecoder
{
    public static DecodedDna Decode(long dna)
    {
        var u = (ulong)dna;
        var phaseBits = (int)(u >> DnaLayout.PhaseShift) & DnaLayout.PhaseMask;
        var phase = phaseBits switch
        {
            0 => MaterialPhase.Solid,
            1 => MaterialPhase.Liquid,
            2 => MaterialPhase.Gas,
            _ => MaterialPhase.Solid
        };

        var exp = ScaleByte((int)((u >> DnaLayout.ExplosivityShift) & DnaLayout.ExplosivityMask));
        var fla = ScaleByte((int)((u >> DnaLayout.FlammabilityShift) & DnaLayout.FlammabilityMask));
        var tox = ScaleByte((int)((u >> DnaLayout.ToxicityShift) & DnaLayout.ToxicityMask));
        var boil = (int)((u >> DnaLayout.BoilingShift) & DnaLayout.BoilingMask);
        var frz = (int)((u >> DnaLayout.FreezeShift) & DnaLayout.FreezeMask);
        var family = (int)((u >> DnaLayout.FamilyShift) & DnaLayout.FamilyMask);

        return new DecodedDna(phase, exp, fla, tox, boil, frz, family);
    }

    private static int ScaleByte(int raw) => raw * 100 / 255;
}
