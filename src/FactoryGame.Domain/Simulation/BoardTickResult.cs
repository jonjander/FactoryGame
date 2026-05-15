namespace FactoryGame.Domain.Simulation;

public sealed class BoardTickResult
{
    public required BoardLineState State { get; init; }
    public required SeaportTickDelta SeaportDelta { get; init; }
    public required long Tick { get; init; }
    public string SummaryNote { get; init; } = "";
}
