namespace FactoryGame.Web.Services;

public sealed class SnackbarService
{
    private int _generation;

    public SnackbarMessage? Current { get; private set; }

    public event Action? Changed;

    public void Show(string message, SnackbarKind kind = SnackbarKind.Success)
    {
        var gen = Interlocked.Increment(ref _generation);
        Current = new SnackbarMessage(message, kind);
        Changed?.Invoke();
        _ = HideAfterDelayAsync(gen);
    }

    private async Task HideAfterDelayAsync(int generation)
    {
        await Task.Delay(3500);
        if (generation != _generation)
            return;
        Current = null;
        Changed?.Invoke();
    }
}

public enum SnackbarKind
{
    Success,
    Error,
    Info
}

public sealed record SnackbarMessage(string Text, SnackbarKind Kind);
