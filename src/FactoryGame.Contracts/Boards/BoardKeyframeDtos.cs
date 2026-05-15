namespace FactoryGame.Contracts.Boards;

public sealed record BoardKeyframeDto(
    Guid BoardId,
    long Tick,
    int RevisionVersion,
    string SummaryNote,
    string Mode,
    SeaportDeltaDto SeaportDelta);

public sealed record SeaportDeltaDto(
    IReadOnlyDictionary<int, decimal> WithdrawnFromPool,
    IReadOnlyDictionary<int, decimal> DepositedToPool);

public sealed record BoardKeyframesResponseDto(
    IReadOnlyList<BoardKeyframeDto> Keyframes,
    long LatestTick);
