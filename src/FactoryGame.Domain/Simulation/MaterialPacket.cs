namespace FactoryGame.Domain.Simulation;

/// <summary>Amount of one element flowing on a port (deterministic; no randomness).</summary>
public sealed class MaterialPacket
{
    public int ElementId { get; init; }
    public long Dna { get; set; }
    public decimal Quantity { get; set; }

    public MaterialQuality Quality { get; set; } = MaterialQuality.Normal;

    public MaterialPacket Clone() =>
        new() { ElementId = ElementId, Dna = Dna, Quantity = Quantity, Quality = Quality };
}

public enum MaterialQuality
{
    Normal,
    Ash,
    Goo
}
