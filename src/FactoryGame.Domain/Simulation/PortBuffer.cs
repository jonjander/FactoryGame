namespace FactoryGame.Domain.Simulation;

/// <summary>FIFO buffer on one machine port (MVP depth 1).</summary>
public sealed class PortBuffer
{
    public const int DefaultCapacity = 1;

    private readonly Queue<MaterialPacket> _queue = new();

    public int Capacity { get; }

    public PortBuffer(int capacity = DefaultCapacity) => Capacity = Math.Max(1, capacity);

    public bool IsEmpty => _queue.Count == 0;

    public bool IsFull => _queue.Count >= Capacity;

    public MaterialPacket? Peek() => _queue.Count > 0 ? _queue.Peek() : null;

    public bool TryEnqueue(MaterialPacket packet)
    {
        if (IsFull)
            return false;
        _queue.Enqueue(packet);
        return true;
    }

    public MaterialPacket? TryDequeue() =>
        _queue.Count > 0 ? _queue.Dequeue() : null;

    public IReadOnlyList<MaterialPacket> Snapshot() => _queue.ToList();
}
