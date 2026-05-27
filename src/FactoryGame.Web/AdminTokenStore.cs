using FactoryGame.Web.Services;

namespace FactoryGame.Web;

public sealed class AdminTokenStore(BrowserStorage storage)
{
    private const string Key = "factorygame.adminToken";
    private string? _token;

    public string? Token => _token;

    public event Action? Changed;

    public async Task LoadAsync()
    {
        _token = await storage.GetAsync(Key);
    }

    public async Task SetAsync(string token)
    {
        _token = token.Trim();
        await storage.SetAsync(Key, _token);
        Changed?.Invoke();
    }

    public async Task ClearAsync()
    {
        _token = null;
        await storage.SetAsync(Key, "");
        Changed?.Invoke();
    }
}
