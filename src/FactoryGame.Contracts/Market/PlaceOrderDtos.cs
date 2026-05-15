namespace FactoryGame.Contracts.Market;

public sealed record PlaceOrderRequest(
    int ElementId,
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
    string Side,
    decimal LimitPrice,
    long QuantityRemaining,
    long OriginalQuantity,
    DateTimeOffset CreatedAt);
