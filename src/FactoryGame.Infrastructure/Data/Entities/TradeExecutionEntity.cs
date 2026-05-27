namespace FactoryGame.Infrastructure.Data.Entities;

public class TradeExecutionEntity
{
    public Guid Id { get; set; }

    public int ElementId { get; set; }

    public long Dna { get; set; }

    public decimal Price { get; set; }

    public long Quantity { get; set; }

    public Guid BuyerPlayerId { get; set; }

    public Guid SellerPlayerId { get; set; }

    public Guid BuyOrderId { get; set; }

    public Guid SellOrderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsSynthetic { get; set; }

    public Guid? BuyerSponsorCompanyId { get; set; }

    public Guid? SellerSponsorCompanyId { get; set; }
}
