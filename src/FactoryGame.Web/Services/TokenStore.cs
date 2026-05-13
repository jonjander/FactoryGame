namespace FactoryGame.Web.Services;

public sealed class TokenStore(BrowserStorage storage)
{
    public const string StorageKey = "fg_session_token";

    public string? BearerToken { get; private set; }

    public event Action? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        BearerToken = await storage.GetAsync(StorageKey, ct);
        Changed?.Invoke();
    }

    public async Task SetTokenAsync(string? token, CancellationToken ct = default)
    {
        BearerToken = token;
        if (string.IsNullOrEmpty(token))
            await storage.SetAsync(StorageKey, "", ct);
        else
            await storage.SetAsync(StorageKey, token, ct);
        Changed?.Invoke();
    }
}
