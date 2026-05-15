namespace FactoryGame.Contracts.Boards;

public sealed record BoardInfoDto(
    Guid BoardId,
    string Name,
    string Mode,
    long SimulationTick,
    SeaportFlowsDto Seaport,
    IReadOnlyList<SeaportPortFlowDto> SeaportPorts,
    ThroughputDto Throughput,
    ValueEstimateDto Value,
    IReadOnlyList<BoardIssueDto> Issues,
    int PlanMachineCount = 0,
    int PlanConnectionCount = 0,
    bool PlanHasCycle = false);

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

public sealed record SeaportPortFlowDto(
    string MachineId,
    string MachineType,
    string Port,
    string Direction,
    bool IsConnected,
    string? LinkedMachineId,
    string? LinkedPort,
    int? ElementId,
    string? ElementSymbol,
    string Summary,
    bool IsEstimate);

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
