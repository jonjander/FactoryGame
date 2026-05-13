namespace FactoryGame.Infrastructure.Data.Entities;

public class InventoryPoolEntity
{
    public Guid PlayerId { get; set; }

    public long MaxVolume { get; set; }

    public long UsedVolume { get; set; }

    public PlayerEntity Player { get; set; } = null!;

    public ICollection<PoolStackEntity> Stacks { get; set; } = new List<PoolStackEntity>();
}
