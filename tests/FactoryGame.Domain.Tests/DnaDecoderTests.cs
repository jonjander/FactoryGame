using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Tests;

public class DnaDecoderTests
{
    [Fact]
    public void Decode_Is_Deterministic_And_Scales_Risk_Bands()
    {
        const long dna = 0x0102_0304_0506_0708L;
        var a = DnaDecoder.Decode(dna);
        var b = DnaDecoder.Decode(dna);
        Assert.Equal(a, b);
        Assert.InRange(a.Explosivity, 0, 100);
        Assert.InRange(a.Flammability, 0, 100);
        Assert.InRange(a.Toxicity, 0, 100);
    }
}
