namespace FactoryGame.Infrastructure.Data.Entities;

public class PlayerBalanceEntity
{
    public Guid PlayerId { get; set; }

    public decimal Cash { get; set; }

    public PlayerEntity Player { get; set; } = null!;
}
