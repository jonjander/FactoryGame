using System.Text.Json;

namespace FactoryGame.Contracts.Boards;

public sealed record MachineDto(string Id, string Type, JsonElement? Settings = null);

public sealed record ConnectionDto(string FromId, string FromPort, string ToId, string ToPort);

public sealed record BoardPlanDto(IReadOnlyList<MachineDto> Machines, IReadOnlyList<ConnectionDto> Connections);

public sealed record CreateBoardRequest(string Name);

public sealed record SavePlanRequest(BoardPlanDto Plan);

public sealed record BoardSummaryDto(Guid Id, string Name, string Mode, int RevisionVersion, long SimulationTick);

public sealed record BoardSnapshotDto(Guid BoardId, long Tick, string Note, string Mode);
