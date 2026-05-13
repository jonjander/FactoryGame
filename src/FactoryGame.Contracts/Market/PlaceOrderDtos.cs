namespace FactoryGame.Contracts.Market;

public sealed record PlaceOrderRequest(
    int ElementId,
    string Side,
    decimal LimitPrice,
    long Quantity,
    string? IdempotencyKey = null);

public sealed record PlaceOrderResponse(Guid OrderId, long QuantityRemaining, string Status);
