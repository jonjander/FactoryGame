namespace FactoryGame.Web.Services;

public sealed class GameWindowDescriptor
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required Type ContentType { get; init; }
    public double X { get; set; } = 48;
    public double Y { get; set; } = 56;
    public double Width { get; set; } = 520;
    public double Height { get; set; } = 420;
    public bool Minimized { get; set; }
    public bool IsOpen { get; set; }
    public int ZIndex { get; set; }
}

public sealed class GameWindowService
{
    private readonly Dictionary<string, GameWindowDescriptor> _registry = new(StringComparer.OrdinalIgnoreCase);
    private int _topZ = 100;

    public IReadOnlyCollection<GameWindowDescriptor> OpenWindows =>
        _registry.Values.Where(w => w.IsOpen).OrderBy(w => w.ZIndex).ToList();

    public event Action? Changed;

    public void Register(string id, string title, Type contentType, double? defaultWidth = null, double? defaultHeight = null)
    {
        if (_registry.ContainsKey(id))
            return;

        var offset = _registry.Count * 28;
        _registry[id] = new GameWindowDescriptor
        {
            Id = id,
            Title = title,
            ContentType = contentType,
            X = 48 + offset,
            Y = 56 + offset,
            Width = defaultWidth ?? 520,
            Height = defaultHeight ?? 420
        };
    }

    public GameWindowDescriptor? Get(string id) =>
        _registry.TryGetValue(id, out var w) ? w : null;

    public bool IsOpen(string id) =>
        _registry.TryGetValue(id, out var w) && w.IsOpen && !w.Minimized;

    public void Toggle(string id)
    {
        if (!_registry.TryGetValue(id, out var window))
            return;

        if (window.IsOpen && !window.Minimized)
        {
            Close(id);
            return;
        }

        Open(id);
    }

    public void Open(string id)
    {
        if (!_registry.TryGetValue(id, out var window))
            return;

        window.IsOpen = true;
        window.Minimized = false;
        Focus(id);
    }

    public void Close(string id)
    {
        if (!_registry.TryGetValue(id, out var window))
            return;

        window.IsOpen = false;
        window.Minimized = false;
        Notify();
    }

    public void Minimize(string id)
    {
        if (!_registry.TryGetValue(id, out var window) || !window.IsOpen)
            return;

        window.Minimized = true;
        Notify();
    }

    public void Restore(string id)
    {
        if (!_registry.TryGetValue(id, out var window))
            return;

        window.IsOpen = true;
        window.Minimized = false;
        Focus(id);
    }

    public void Focus(string id)
    {
        if (!_registry.TryGetValue(id, out var window))
            return;

        window.IsOpen = true;
        window.Minimized = false;
        window.ZIndex = ++_topZ;
        Notify();
    }

    public void SetPosition(string id, double x, double y)
    {
        if (!_registry.TryGetValue(id, out var window))
            return;

        window.X = Math.Max(0, x);
        window.Y = Math.Max(0, y);
        Notify();
    }

    public void SetSize(string id, double width, double height)
    {
        if (!_registry.TryGetValue(id, out var window))
            return;

        window.Width = Math.Max(280, width);
        window.Height = Math.Max(200, height);
        Notify();
    }

    private void Notify() => Changed?.Invoke();
}
