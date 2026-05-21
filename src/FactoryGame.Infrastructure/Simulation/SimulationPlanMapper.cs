using System.Text.Json;
using FactoryGame.Contracts.Boards;
using FactoryGame.Domain.Simulation;

namespace FactoryGame.Infrastructure.Simulation;

public static class SimulationPlanMapper
{
    public static SimulationPlan ToSimulationPlan(BoardPlanDto plan) =>
        new(
            plan.Machines.Select(m => new SimulationMachine(
                m.Id,
                m.Type,
                MapSettings(m.Settings))).ToList(),
            plan.Connections.Select(c => new SimulationConnection(
                c.FromId, c.FromPort, c.ToId, c.ToPort)).ToList());

    private static string? MapSettings(JsonElement? settings) =>
        settings is { } s ? JsonSerializer.Serialize(s) : null;
}
