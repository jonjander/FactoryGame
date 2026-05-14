namespace FactoryGame.Infrastructure.Data.Entities;

/// <summary>Unplaced machine owned by the player (purchased from the machine store).</summary>
public sealed class PlayerMachineStockEntity
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    /// <summary>Canonical machine type (e.g. Boiler, Mixer).</summary>
    public string MachineType { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}
