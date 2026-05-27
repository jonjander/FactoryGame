namespace FactoryGame.Contracts.Admin;

public sealed record SponsorCompanyDto(
    Guid Id,
    string Name,
    string Description,
    string LogoUrl,
    Guid PlayerId,
    bool IsActive,
    string FundingMode,
    decimal? BudgetRemaining,
    decimal? TotalBudget,
    decimal VirtualSpend,
    int ExposureTier,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    SponsorCompanyStatsDto Stats);

public sealed record SponsorCompanyStatsDto(
    decimal TotalSpend,
    long TotalVolume,
    int TradeCount,
    DateTimeOffset? LastTradeAt);

public sealed record CreateSponsorCompanyRequest(
    string Name,
    string? Description,
    string? LogoUrl,
    string FundingMode,
    decimal? TotalBudget,
    int ExposureTier,
    bool IsActive = true);

public sealed record UpdateSponsorCompanyRequest(
    string? Name,
    string? Description,
    string? LogoUrl,
    string? FundingMode,
    decimal? TotalBudget,
    decimal? BudgetRemaining,
    int? ExposureTier,
    bool? IsActive);

public sealed record SponsorCompanyOrderDto(
    Guid Id,
    Guid SponsorCompanyId,
    int ElementId,
    long Dna,
    string Side,
    decimal LimitPrice,
    long TargetQuantity,
    bool IsActive,
    Guid? LinkedMarketOrderId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateSponsorCompanyOrderRequest(
    int ElementId,
    long Dna,
    string Side,
    decimal LimitPrice,
    long TargetQuantity,
    bool IsActive = true);

public sealed record UpdateSponsorCompanyOrderRequest(
    int? ElementId,
    long? Dna,
    string? Side,
    decimal? LimitPrice,
    long? TargetQuantity,
    bool? IsActive);
