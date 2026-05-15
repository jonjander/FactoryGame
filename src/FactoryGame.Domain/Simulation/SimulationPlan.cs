namespace FactoryGame.Domain.Simulation;

public sealed record SimulationMachine(string Id, string Type, string? SettingsJson);

public sealed record SimulationConnection(string FromId, string FromPort, string ToId, string ToPort);

public sealed record SimulationPlan(
    IReadOnlyList<SimulationMachine> Machines,
    IReadOnlyList<SimulationConnection> Connections);
