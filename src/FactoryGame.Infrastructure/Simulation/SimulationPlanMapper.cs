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
                m.Settings.HasValue ? m.Settings.Value.GetRawText() : null)).ToList(),
            plan.Connections.Select(c => new SimulationConnection(
                c.FromId, c.FromPort, c.ToId, c.ToPort)).ToList());
}
