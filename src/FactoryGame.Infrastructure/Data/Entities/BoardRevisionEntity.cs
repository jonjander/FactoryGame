namespace FactoryGame.Infrastructure.Data.Entities;

public class BoardRevisionEntity
{
    public Guid Id { get; set; }

    public Guid BoardId { get; set; }

    public int Version { get; set; }

    public string PlanJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }

    public BoardEntity Board { get; set; } = null!;
}
