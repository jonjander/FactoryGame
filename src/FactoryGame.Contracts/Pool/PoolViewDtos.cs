namespace FactoryGame.Contracts.Pool;

public sealed record PoolOverviewDto(
    long MaxVolume,
    long UsedVolume,
    decimal TotalEstimatedValue,
    IReadOnlyList<PoolStackViewDto> Stacks,
    IReadOnlyList<PoolElementGroupDto> Groups);

public sealed record PoolElementGroupDto(
    int ElementId,
    string Symbol,
    string DisplayName,
    long TotalQuantity,
    IReadOnlyList<PoolVariantStackDto> Variants);

public sealed record PoolVariantStackDto(
    int ElementId,
    string Symbol,
    long Dna,
    string Phase,
    string PhaseLabel,
    long Quantity,
    long VolumePerUnit,
    decimal LastPrice,
    decimal LineValue,
    int PriceRank,
    int CatalogSize,
    decimal? ChangePercent24h);

/// <summary>Legacy flat stack row (one per DNA variant).</summary>
public sealed record PoolStackViewDto(
    int ElementId,
    string Symbol,
    long Dna,
    string Phase,
    string PhaseLabel,
    string DisplayName,
    long Quantity,
    long VolumePerUnit,
    decimal LastPrice,
    decimal LineValue,
    int PriceRank,
    int CatalogSize,
    decimal? ChangePercent24h);
