namespace FactoryGame.Infrastructure.Data.Entities;

public class BoardKeyframeEntity
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public long Tick { get; set; }
    public int RevisionVersion { get; set; }
    public string LineStateJson { get; set; } = "";
    public string SeaportDeltaJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }

    public BoardEntity Board { get; set; } = null!;
}
