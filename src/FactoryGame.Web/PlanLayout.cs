using System.Text.Json;
using FactoryGame.Contracts.Boards;

namespace FactoryGame.Web;

internal static class PlanLayout
{
    private const double DefaultColWidth = 150;
    private const double DefaultRowHeight = 100;

    public static (double X, double Y) GetPosition(MachineDto machine, int index)
    {
        if (machine.Settings is { ValueKind: JsonValueKind.Object } settings)
        {
            if (settings.TryGetProperty("x", out var xEl) && settings.TryGetProperty("y", out var yEl) &&
                xEl.TryGetDouble(out var x) && yEl.TryGetDouble(out var y))
                return (x, y);
        }

        var col = index % 4;
        var row = index / 4;
        return (40 + col * DefaultColWidth, 40 + row * DefaultRowHeight);
    }

    public static MachineDto WithPosition(MachineDto machine, double x, double y)
    {
        var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (machine.Settings is { ValueKind: JsonValueKind.Object } settings)
        {
            foreach (var prop in settings.EnumerateObject())
                dict[prop.Name] = prop.Value.Clone();
        }

        dict["x"] = JsonSerializer.SerializeToElement(x);
        dict["y"] = JsonSerializer.SerializeToElement(y);
        return machine with { Settings = JsonSerializer.SerializeToElement(dict) };
    }
}
