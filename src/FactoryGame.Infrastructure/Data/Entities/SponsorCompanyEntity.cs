using FactoryGame.Domain.Market;

namespace FactoryGame.Infrastructure.Data.Entities;

public class SponsorCompanyEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public string LogoUrl { get; set; } = "";

    public Guid PlayerId { get; set; }

    public bool IsActive { get; set; } = true;

    public SponsorFundingMode FundingMode { get; set; } = SponsorFundingMode.Budget;

    public decimal? BudgetRemaining { get; set; }

    public decimal? TotalBudget { get; set; }

    /// <summary>Tracked spend in utopia mode for leaderboards.</summary>
    public decimal VirtualSpend { get; set; }

    /// <summary>1–5; controls lot size and trade rate.</summary>
    public int ExposureTier { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<SponsorCompanyOrderEntity> StandingOrders { get; set; } = new List<SponsorCompanyOrderEntity>();
}
