using FactoryGame.Domain.Simulation;

namespace FactoryGame.Domain.Tests;

public sealed class BoardTickSimulatorTests
{
    [Fact]
    public void BuildNote_IsStable_ForSameMachinesAndTick()
    {
        var machines = new (string Id, string Type)[] { ("a", "Mixer"), ("b", "Heater") };
        var a = BoardTickSimulator.BuildNote(42, machines);
        var b = BoardTickSimulator.BuildNote(42, machines);
        Assert.Equal(a, b);
    }

    [Fact]
    public void BuildNote_Is_OrderIndependent_OnIds()
    {
        var m1 = new (string, string)[] { ("a", "Mixer"), ("b", "Heater") };
        var m2 = new (string, string)[] { ("b", "Heater"), ("a", "Mixer") };
        Assert.Equal(BoardTickSimulator.BuildNote(1, m1), BoardTickSimulator.BuildNote(1, m2));
    }

    [Fact]
    public void BuildNote_Changes_WhenTickChanges()
    {
        var m = new (string, string)[] { ("a", "Mixer") };
        Assert.NotEqual(BoardTickSimulator.BuildNote(1, m), BoardTickSimulator.BuildNote(2, m));
    }
}
