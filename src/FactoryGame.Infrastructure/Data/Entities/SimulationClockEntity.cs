namespace FactoryGame.Infrastructure.Data.Entities;

/// <summary>Singleton row (Id fixed) driving global simulation tick index.</summary>
public class SimulationClockEntity
{
    public int Id { get; set; }

    public long CurrentTick { get; set; }

    public DateTimeOffset LastAdvancedAt { get; set; }
}
