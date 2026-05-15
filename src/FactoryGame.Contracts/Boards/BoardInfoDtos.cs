namespace FactoryGame.Contracts.Boards;

public sealed record BoardInfoDto(
    Guid BoardId,
    string Name,
    string Mode,
    long SimulationTick,
    SeaportFlowsDto Seaport,
    ThroughputDto Throughput,
    ValueEstimateDto Value,
    IReadOnlyList<BoardIssueDto> Issues);

public sealed record SeaportFlowsDto(
    IReadOnlyList<SeaportFlowLineDto> IntoFactory,
    IReadOnlyList<SeaportFlowLineDto> OutOfFactory);

public sealed record SeaportFlowLineDto(
    string MachineId,
    string MachineType,
    string Port,
    string? LinkedMachineId,
    string? LinkedPort,
    double UnitsPerSecond,
    string Description);

public sealed record ThroughputDto(
    double TotalUnitsPerSecond,
    bool IsEstimate,
    string Note);

public sealed record ValueEstimateDto(
    decimal EstimatedValuePerSecond,
    bool IsEstimate,
    string Note);

public sealed record BoardIssueDto(
    string Severity,
    string Code,
    string Message,
    string? MachineId);
