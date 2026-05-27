using FactoryGame.Domain.Market;

namespace FactoryGame.Infrastructure.Data.Entities;

public class SponsorCompanyOrderEntity
{
    public Guid Id { get; set; }

    public Guid SponsorCompanyId { get; set; }

    public SponsorCompanyEntity SponsorCompany { get; set; } = null!;

    public int ElementId { get; set; }

    public long Dna { get; set; }

    public OrderSide Side { get; set; }

    public decimal LimitPrice { get; set; }

    public long TargetQuantity { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? LinkedMarketOrderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
