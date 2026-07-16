using Microsoft.JSInterop;

namespace FactoryGame.Web.Services;

public sealed class ViewportLayoutService : IAsyncDisposable
{
    public const int DesktopMinWidth = 900;
    public const string MediaQuery = "(min-width: 900px)";

    private IJSObjectReference? _module;
    private DotNetObjectReference<ViewportLayoutService>? _dotNetRef;
    private bool _initialized;

    public bool UseGameShell { get; private set; }

    public event Action? Changed;

    public async Task InitializeAsync(IJSRuntime js)
    {
        if (_initialized)
            return;

        _module = await js.InvokeAsync<IJSObjectReference>("import", "./js/viewport-layout.js");
        _dotNetRef = DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("attach", _dotNetRef, MediaQuery);
        _initialized = true;
    }

    [JSInvokable]
    public void OnViewportChanged(bool matches)
    {
        if (UseGameShell == matches)
            return;

        UseGameShell = matches;
        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            try
            {
                await _module.InvokeVoidAsync("stop");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                /* app shutting down */
            }
        }

        _dotNetRef?.Dispose();
        _module = null;
        _dotNetRef = null;
        _initialized = false;
    }
}
