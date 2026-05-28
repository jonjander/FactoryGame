namespace FactoryGame.Infrastructure.Data.Entities;

public class PlayerEntity
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 hex of guest device key; null when linked to OIDC later.</summary>
    public string? GuestDeviceKeyHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>UTC ticks mirror of <see cref="CreatedAt"/> for indexed server-side sort.</summary>
    public long CreatedAtUtcTicks { get; set; }

    /// <summary>Backing account for a sponsor company; not a human player.</summary>
    public bool IsSponsorAccount { get; set; }

    public PlayerBalanceEntity Balance { get; set; } = null!;

    public InventoryPoolEntity Pool { get; set; } = null!;
}
