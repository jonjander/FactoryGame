using System.Text.Json.Serialization;
using FactoryGame.Contracts.Json;

namespace FactoryGame.Contracts.Market;

public sealed record PlaceOrderRequest(
    int ElementId,
    [property: JsonConverter(typeof(DnaJsonConverter))] long Dna,
    string Side,
    decimal LimitPrice,
    long Quantity,
    string? IdempotencyKey = null);

public sealed record PlaceOrderResponse(
    Guid OrderId,
    long QuantityRemaining,
    string Status,
    long QuantityFilled = 0,
    decimal? AverageFillPrice = null,
    long OriginalQuantity = 0);

public sealed record MyOpenOrderDto(
    Guid Id,
    int ElementId,
    [property: JsonConverter(typeof(DnaJsonConverter))] long Dna,
    string Side,
    decimal LimitPrice,
    long QuantityRemaining,
    long OriginalQuantity,
    DateTimeOffset CreatedAt);

public sealed record AmendOrderRequest(decimal LimitPrice);

public sealed record OrderActionResponse(
    Guid OrderId,
    string Status,
    long QuantityRemaining,
    long QuantityFilled = 0,
    decimal? AverageFillPrice = null,
    decimal? LimitPrice = null,
    long OriginalQuantity = 0);
