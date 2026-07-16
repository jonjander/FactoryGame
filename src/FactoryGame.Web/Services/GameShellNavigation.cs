using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using FactoryGame.Web.Views;

namespace FactoryGame.Web.Services;

public sealed class GameShellNavigation : IDisposable
{
    private readonly NavigationManager _nav;
    private readonly ViewportLayoutService _viewport;
    private readonly GameWindowService _windows;
    private readonly TokenStore _tokens;
    private bool _registered;

    public GameShellNavigation(
        NavigationManager nav,
        ViewportLayoutService viewport,
        GameWindowService windows,
        TokenStore tokens)
    {
        _nav = nav;
        _viewport = viewport;
        _windows = windows;
        _tokens = tokens;
        _nav.LocationChanged += OnLocationChanged;
    }

    public void EnsureRegistered()
    {
        if (_registered)
            return;

        _windows.Register("login", "Logga in", typeof(HomeView), 420, 320);
        _windows.Register("exchange", "Börs", typeof(ExchangeView), 720, 520);
        _windows.Register("market", "Marknad", typeof(MarketView), 640, 480);
        _windows.Register("pool", "Pool", typeof(PoolView), 720, 480);
        _windows.Register("transactions", "Konto", typeof(TransactionsView), 640, 480);
        _windows.Register("wiki", "Wiki", typeof(WikiView), 800, 560);
        _windows.Register("cli", "CLI", typeof(CliView), 560, 400);
        _windows.Register("admin", "Admin", typeof(AdminView), 640, 480);
        _windows.Register("boards-picker", "Spelplaner", typeof(BoardPickerView), 480, 400);
        _windows.Register("store", "Maskinbutik", typeof(MachineStoreView), 560, 440);
        _windows.Register("machine-settings", "Maskininställningar", typeof(MachineSettingsView), 520, 420);
        _windows.Register("board-info", "Fabrikinformation", typeof(BoardInfoView), 560, 440);
        _windows.Register("plan-json", "Plan (JSON)", typeof(PlanJsonView), 560, 480);
        _windows.Register("place-machine", "Placera maskin", typeof(PlaceMachineView), 480, 320);
        _windows.Register("pipe-form", "Koppla rör", typeof(PipeFormView), 520, 400);
        _registered = true;
    }

    public void SyncRouteToWindows()
    {
        if (!_viewport.UseGameShell)
            return;

        EnsureRegistered();
        var path = _nav.ToBaseRelativePath(_nav.Uri).TrimEnd('/');
        if (string.IsNullOrEmpty(path))
            path = "/";

        var windowId = path switch
        {
            "/" => "login",
            "exchange" => "exchange",
            "market" => "market",
            "pool" => "pool",
            "boards" => "boards-picker",
            "transactions" => "transactions",
            "wiki" => "wiki",
            "cli" => "cli",
            "admin" => "admin",
            _ => null
        };

        if (windowId == null)
            return;

        if (windowId == "login" && !string.IsNullOrEmpty(_tokens.BearerToken))
            return;

        _windows.Open(windowId);
    }

    public void NavigateAndOpen(string route, string windowId)
    {
        EnsureRegistered();
        _windows.Open(windowId);
        var relative = route.TrimStart('/');
        if (_nav.ToBaseRelativePath(_nav.Uri).TrimEnd('/') != relative)
            _nav.NavigateTo(route);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (!_viewport.UseGameShell)
            return;

        SyncRouteToWindows();
    }

    public void Dispose() => _nav.LocationChanged -= OnLocationChanged;
}
