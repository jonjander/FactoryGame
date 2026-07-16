namespace FactoryGame.Api.Diagnostics;

/// <summary>
/// In-memory ring buffer of browser/Blazor client log lines (Development diagnostics).
/// </summary>
public sealed class ClientLogBuffer(int maxLines)
{
    private readonly RecentLogBuffer _inner = new(maxLines);

    public void AddLine(string line) => _inner.AddLine(line);

    public IReadOnlyList<string> Snapshot() => _inner.Snapshot();
}
