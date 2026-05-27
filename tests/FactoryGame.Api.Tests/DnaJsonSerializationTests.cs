using System.Text.Json;
using FactoryGame.Contracts.Json;
using FactoryGame.Contracts.Market;

namespace FactoryGame.Api.Tests;

public sealed class DnaJsonSerializationTests
{
    [Fact]
    public void PlaceOrderRequest_serializes_dna_as_string()
    {
        const long dna = 217304205466536202L;
        var req = new PlaceOrderRequest(4, dna, "buy", 12.5m, 3, "key-1");
        var json = JsonSerializer.Serialize(req, FactoryGameJson.Api);
        Assert.Contains("\"217304205466536202\"", json);

        var back = JsonSerializer.Deserialize<PlaceOrderRequest>(json, FactoryGameJson.Api);
        Assert.NotNull(back);
        Assert.Equal(dna, back.Dna);
    }

    [Fact]
    public void PlaceOrderRequest_accepts_legacy_numeric_dna()
    {
        const long dna = 217304205466536202L;
        var json = $$"""{"elementId":4,"dna":{{dna}},"side":"buy","limitPrice":12.5,"quantity":3}""";
        var req = JsonSerializer.Deserialize<PlaceOrderRequest>(json, FactoryGameJson.Api);
        Assert.NotNull(req);
        Assert.Equal(dna, req.Dna);
    }
}
