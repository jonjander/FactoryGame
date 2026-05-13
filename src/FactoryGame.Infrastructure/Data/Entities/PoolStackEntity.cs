namespace FactoryGame.Infrastructure.Data.Entities;

/// <summary>Amount of one element in the shared seaport pool (abstract units).</summary>
public class PoolStackEntity
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public int ElementId { get; set; }

    public long Quantity { get; set; }

    public long VolumePerUnit { get; set; }

    public InventoryPoolEntity Pool { get; set; } = null!;
}
