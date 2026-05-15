using FactoryGame.Domain.Market;

namespace FactoryGame.Infrastructure.Data.Entities;

public class MarketOrderEntity
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public int ElementId { get; set; }

    public OrderSide Side { get; set; }

    /// <summary>Limit price per unit; null treated as market (match best opposite).</summary>
    public decimal? LimitPrice { get; set; }

    public long QuantityRemaining { get; set; }

    public long OriginalQuantity { get; set; }

    public OrderStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Optional idempotency for PlaceOrder (KRAVSPEC).</summary>
    public string? IdempotencyKey { get; set; }

    public bool IsSynthetic { get; set; }
}
