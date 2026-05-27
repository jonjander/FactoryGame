using System.Net.Http.Json;
using FactoryGame.Contracts.App;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace FactoryGame.Web.Services;

/// <summary>Polls server app version; signals when a newer deploy is available than this WASM build.</summary>
public sealed class AppVersionMonitor : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    private readonly HttpClient _http;
    private readonly bool _enabled;
    private CancellationTokenSource? _cts;

    public string? ServerVersion { get; private set; }

    public bool UpdateAvailable =>
        ServerVersion is not null
        && !string.Equals(ServerVersion, AppReleaseVersion.Current, StringComparison.Ordinal);

    public event Action? Changed;

    public AppVersionMonitor(HttpClient http, IWebAssemblyHostEnvironment env)
    {
        _http = http;
        _enabled = !env.IsDevelopment();
    }

    public void Start()
    {
        if (!_enabled || _cts != null)
            return;

        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            await CheckOnceAsync(ct);
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task CheckOnceAsync(CancellationToken ct)
    {
        try
        {
            var dto = await _http.GetFromJsonAsync<AppVersionDto>("v1/app/version", ct);
            if (dto?.Version is not { Length: > 0 } version)
                return;

            if (string.Equals(version, ServerVersion, StringComparison.Ordinal))
                return;

            ServerVersion = version;
            Changed?.Invoke();
        }
        catch (HttpRequestException)
        {
            // Offline or unreachable — try again on next tick.
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
