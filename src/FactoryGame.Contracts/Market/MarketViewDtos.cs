namespace FactoryGame.Contracts.Market;

public sealed record MarketElementSummaryDto(
    int ElementId,
    long Dna,
    string Symbol,
    string Phase,
    string PhaseLabel,
    string DisplayName,
    long PoolQuantity,
    decimal LastPrice,
    decimal? ChangePercent24h,
    decimal? BestBid,
    decimal? BestAsk);

public sealed record MarketDepthLevelDto(decimal Price, long BidQuantity, long AskQuantity);

public sealed record MarketDepthDto(
    int ElementId,
    long Dna,
    decimal? BestBid,
    decimal? BestAsk,
    IReadOnlyList<MarketDepthLevelDto> Levels);

public sealed record MarketCandleDto(
    DateTimeOffset BucketStart,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

public sealed record MarketTradeDto(
    Guid Id,
    int ElementId,
    long Dna,
    decimal Price,
    long Quantity,
    DateTimeOffset CreatedAt);
