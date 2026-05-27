using System.Text.Json.Serialization;
using FactoryGame.Contracts.Json;

namespace FactoryGame.Contracts.Market;

public sealed record MarketTradeDto(
    Guid Id,
    int ElementId,
    [property: JsonConverter(typeof(DnaJsonConverter))] long Dna,
    decimal Price,
    long Quantity,
    DateTimeOffset CreatedAt,
    string? BuyerLabel,
    string? SellerLabel,
    bool BuyerIsSponsor,
    bool SellerIsSponsor,
    Guid? BuyerSponsorCompanyId,
    Guid? SellerSponsorCompanyId);

public sealed record MarketInsightDto(
    Guid SponsorCompanyId,
    string CompanyName,
    string LogoUrl,
    int ElementId,
    [property: JsonConverter(typeof(DnaJsonConverter))] long Dna,
    string Symbol,
    string PhaseLabel,
    string DisplayName,
    decimal LimitPrice,
    long OpenQuantity,
    int ExposureTier,
    decimal AttractivenessScore);

public sealed record MarketInsightsResponse(
    IReadOnlyList<MarketInsightDto> SellOpportunities,
    IReadOnlyList<MarketInsightDto> BuyOpportunities);

public sealed record LeaderboardEntryDto(
    Guid Id,
    string Label,
    string? LogoUrl,
    string? Description,
    decimal TotalSpend,
    long TotalVolume,
    int TradeCount,
    int Rank);

public sealed record MarketLeaderboardsDto(
    IReadOnlyList<LeaderboardEntryDto> BigSpendersPlayers,
    IReadOnlyList<LeaderboardEntryDto> BigSpendersSponsors,
    IReadOnlyList<LeaderboardEntryDto> TopVolumePlayers,
    IReadOnlyList<LeaderboardEntryDto> TopVolumeSponsors,
    IReadOnlyList<LeaderboardEntryDto> MostActivePlayers,
    IReadOnlyList<LeaderboardEntryDto> MostActiveSponsors);

public sealed record SponsorProfileDto(
    Guid Id,
    string Name,
    string Description,
    string LogoUrl,
    int ExposureTier,
    SponsorCompanyPublicStatsDto Stats);

public sealed record SponsorCompanyPublicStatsDto(
    decimal TotalSpend,
    long TotalVolume,
    int TradeCount,
    int? SpendRank,
    int? VolumeRank);
