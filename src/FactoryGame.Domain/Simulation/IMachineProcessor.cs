namespace FactoryGame.Domain.Simulation;

public interface IMachineProcessor
{
    string MachineType { get; }
    void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson);
}
