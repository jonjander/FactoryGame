namespace FactoryGame.Domain.Simulation;

/// <summary>Pool I/O during tick (implemented in Infrastructure).</summary>
public interface ISeaportPoolSink
{
    /// <summary>Withdraw qty of element variant from player pool; returns false if insufficient.</summary>
    bool TryWithdraw(int elementId, long dna, decimal quantity);

    /// <summary>Deposit qty into player pool; returns false if volume cap exceeded.</summary>
    bool TryDeposit(int elementId, long dna, decimal quantity);
}
