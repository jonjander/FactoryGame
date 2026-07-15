using System.Text.Json;

namespace FactoryGame.Web.Services;

/// <summary>Persists muted board issue keys (code + machine) in browser storage.</summary>
public sealed class BoardIssueMuteStore(BrowserStorage storage)
{
    private const string StorageKey = "fg_muted_board_issues";
    private HashSet<string> _muted = new(StringComparer.Ordinal);
    private bool _loaded;

    public IReadOnlySet<string> MutedKeys => _muted;

    public static string IssueKey(string code, string? machineId) =>
        $"{code}|{machineId ?? ""}";

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
            return;
        _loaded = true;
        var raw = await storage.GetAsync(StorageKey);
        if (string.IsNullOrWhiteSpace(raw))
            return;
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(raw);
            if (list != null)
                _muted = list.ToHashSet(StringComparer.Ordinal);
        }
        catch
        {
            _muted = new HashSet<string>(StringComparer.Ordinal);
        }
    }

    public async Task<bool> IsMutedAsync(string code, string? machineId)
    {
        await EnsureLoadedAsync();
        return _muted.Contains(IssueKey(code, machineId));
    }

    public async Task ToggleMuteAsync(string code, string? machineId)
    {
        await EnsureLoadedAsync();
        var key = IssueKey(code, machineId);
        if (!_muted.Add(key))
            _muted.Remove(key);
        await storage.SetAsync(StorageKey, JsonSerializer.Serialize(_muted.OrderBy(k => k).ToList()));
    }

    public async Task UnmuteAllAsync()
    {
        _muted.Clear();
        await storage.SetAsync(StorageKey, "[]");
    }
}
