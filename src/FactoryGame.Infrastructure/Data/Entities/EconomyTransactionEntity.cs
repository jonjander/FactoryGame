namespace FactoryGame.Infrastructure.Data.Entities;

public class EconomyTransactionEntity
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public string Type { get; set; } = null!;

    public decimal CashDelta { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? Metadata { get; set; }
}
