namespace FactoryGame.Domain.Simulation;

/// <summary>Pool withdrawals/deposits during one board tick.</summary>
public sealed class SeaportTickDelta
{
    public Dictionary<int, decimal> WithdrawnFromPool { get; } = new();
    public Dictionary<int, decimal> DepositedToPool { get; } = new();

    public void AddWithdraw(int elementId, decimal qty)
    {
        WithdrawnFromPool[elementId] = WithdrawnFromPool.GetValueOrDefault(elementId) + qty;
    }

    public void AddDeposit(int elementId, decimal qty)
    {
        DepositedToPool[elementId] = DepositedToPool.GetValueOrDefault(elementId) + qty;
    }
}
