using System.Net.Http.Json;
using System.Text.Json;

namespace FactoryGame.Web.Services;

public sealed class WalletState(HttpClient http, TokenStore tokens)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public decimal? Cash { get; private set; }

    public event Action? Changed;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(tokens.BearerToken))
        {
            Cash = null;
            Changed?.Invoke();
            return;
        }

        try
        {
            var w = await http.GetFromJsonAsync<WalletJson>("/v1/me/wallet", Json, ct);
            Cash = w?.cash;
        }
        catch
        {
            Cash = null;
        }

        Changed?.Invoke();
    }

    private sealed class WalletJson
    {
        public decimal cash { get; set; }
    }
}
