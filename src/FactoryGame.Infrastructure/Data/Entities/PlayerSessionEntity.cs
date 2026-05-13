namespace FactoryGame.Infrastructure.Data.Entities;

public class PlayerSessionEntity
{
    public string Token { get; set; } = null!;

    public Guid PlayerId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public PlayerEntity Player { get; set; } = null!;
}
