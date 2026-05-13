using Microsoft.JSInterop;

namespace FactoryGame.Web.Services;

public sealed class BrowserStorage(IJSRuntime js)
{
    public ValueTask<string?> GetAsync(string key, CancellationToken ct = default) =>
        js.InvokeAsync<string?>("factoryGame.storageGet", ct, key);

    public ValueTask SetAsync(string key, string value, CancellationToken ct = default) =>
        js.InvokeVoidAsync("factoryGame.storageSet", ct, key, value);

    public ValueTask<bool> IsOnlineAsync(CancellationToken ct = default) =>
        js.InvokeAsync<bool>("factoryGame.isOnline", ct);
}
