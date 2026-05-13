namespace FactoryGame.Domain.Dna;

/// <summary>Bit layout v1 for element DNA (signed 64-bit; use only lower bits for portability).</summary>
public static class DnaLayout
{
    public const int PhaseMask = 0b11;
    public const int PhaseShift = 0;

    public const long ExplosivityMask = 0xFF;
    public const int ExplosivityShift = 2;

    public const long FlammabilityMask = 0xFF;
    public const int FlammabilityShift = 10;

    public const long ToxicityMask = 0xFF;
    public const int ToxicityShift = 18;

    /// <summary>Boiling point bucket 0–4095 (game scale, not Celsius).</summary>
    public const long BoilingMask = 0xFFF;
    public const int BoilingShift = 26;

    /// <summary>Freeze point bucket 0–4095 (game scale).</summary>
    public const long FreezeMask = 0xFFF;
    public const int FreezeShift = 38;

    public const long FamilyMask = 0x3FFFF;
    public const int FamilyShift = 50;
}
