using System.Net.Http.Json;
using System.Text.Json;
using FactoryGame.Contracts.Json;

namespace FactoryGame.Web.Services;

public sealed class WalletState(IHttpClientFactory httpFactory, TokenStore tokens)
{
    private static readonly JsonSerializerOptions Json = FactoryGameJson.Api;

    public decimal? Cash { get; private set; }

    public decimal? BaseIncomeAmount { get; private set; }

    public int? BaseIncomeIntervalMinutes { get; private set; }

    public DateTimeOffset? LastBaseIncomeAt { get; private set; }

    public string? BaseIncomeHint => FormatBaseIncomeHint();

    public event Action? Changed;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(tokens.BearerToken))
        {
            Cash = null;
            BaseIncomeAmount = null;
            BaseIncomeIntervalMinutes = null;
            LastBaseIncomeAt = null;
            Changed?.Invoke();
            return;
        }

        try
        {
            var w = await httpFactory.CreateClient("api").GetFromJsonAsync<WalletJson>("/v1/me/wallet", Json, ct);
            Cash = w?.cash;
            BaseIncomeAmount = w?.baseIncomeAmount;
            BaseIncomeIntervalMinutes = w?.baseIncomeIntervalMinutes;
            LastBaseIncomeAt = w?.lastBaseIncomeAt;
        }
        catch
        {
            Cash = null;
            BaseIncomeAmount = null;
            BaseIncomeIntervalMinutes = null;
            LastBaseIncomeAt = null;
        }

        Changed?.Invoke();
    }

    private string? FormatBaseIncomeHint()
    {
        if (BaseIncomeAmount is not { } amount || amount <= 0)
            return null;
        if (BaseIncomeIntervalMinutes is not { } mins || mins <= 0)
            return null;

        if (LastBaseIncomeAt is not { } last)
            return $"Basinkomst +{amount:0.##} om ≤{mins} min";

        var next = last + TimeSpan.FromMinutes(mins);
        var remaining = next - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return $"Basinkomst +{amount:0.##} snart";

        var remMins = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        return $"Basinkomst +{amount:0.##} om ~{remMins} min";
    }

    private sealed class WalletJson
    {
        public decimal cash { get; set; }
        public decimal baseIncomeAmount { get; set; }
        public int baseIncomeIntervalMinutes { get; set; }
        public DateTimeOffset? lastBaseIncomeAt { get; set; }
    }
}
