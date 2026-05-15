namespace FactoryGame.Contracts.Pool;

public sealed record PoolOverviewDto(
    long MaxVolume,
    long UsedVolume,
    decimal TotalEstimatedValue,
    IReadOnlyList<PoolStackViewDto> Stacks);

public sealed record PoolStackViewDto(
    int ElementId,
    string Symbol,
    string DisplayName,
    long Quantity,
    long VolumePerUnit,
    decimal LastPrice,
    decimal LineValue,
    int PriceRank,
    int CatalogSize,
    decimal? ChangePercent24h);
