using FactoryGame.Domain.Boards;

namespace FactoryGame.Infrastructure.Data.Entities;

public class BoardEntity
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public string Name { get; set; } = "";

    public BoardMode Mode { get; set; }

    public int RevisionVersion { get; set; }

    public long SimulationTick { get; set; }

    public string? LastSnapshotNote { get; set; }
}
