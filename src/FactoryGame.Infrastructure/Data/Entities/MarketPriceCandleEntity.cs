namespace FactoryGame.Infrastructure.Data.Entities;

public class MarketPriceCandleEntity
{
    public long Id { get; set; }

    public int ElementId { get; set; }

    public long Dna { get; set; }

    public DateTimeOffset BucketStart { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public long Volume { get; set; }
}
