namespace FactoryGame.Contracts.Player;

public sealed record PlayerTransactionDto(
    Guid Id,
    string Type,
    decimal CashDelta,
    DateTimeOffset CreatedAt,
    string? Metadata);

public sealed record PlayerTransactionsPageDto(
    IReadOnlyList<PlayerTransactionDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
