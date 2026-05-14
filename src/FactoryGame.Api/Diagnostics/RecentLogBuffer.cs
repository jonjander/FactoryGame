namespace FactoryGame.Api.Diagnostics;

/// <summary>
/// In-memory ring buffer of formatted log lines since process start (for GET /diagnostics/recent-logs).
/// </summary>
public sealed class RecentLogBuffer
{
    private readonly object _lock = new();
    private readonly List<string> _lines = new();
    private readonly int _maxLines;

    public RecentLogBuffer(int maxLines) =>
        _maxLines = maxLines > 0 ? maxLines : 2000;

    public void AddLine(string line)
    {
        lock (_lock)
        {
            _lines.Add(line);
            var overflow = _lines.Count - _maxLines;
            if (overflow > 0)
                _lines.RemoveRange(0, overflow);
        }
    }

    public IReadOnlyList<string> Snapshot()
    {
        lock (_lock)
        {
            return _lines.ToArray();
        }
    }
}
