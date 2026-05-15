using System.Text.Json;
using FactoryGame.Contracts.Boards;

namespace FactoryGame.Web;

internal static class PlanMachineSettings
{
    public static int GetOutElementId(MachineDto machine)
    {
        if (machine.Settings is not { ValueKind: JsonValueKind.Object } settings)
            return 1;
        if (settings.TryGetProperty("outElementId", out var el) && el.TryGetInt32(out var id) && id > 0)
            return id;
        return 1;
    }

    public static MachineDto WithOutElementId(MachineDto machine, int elementId)
    {
        var dict = CloneSettings(machine);
        dict["outElementId"] = JsonSerializer.SerializeToElement(elementId);
        return machine with { Settings = JsonSerializer.SerializeToElement(dict) };
    }

    private static Dictionary<string, JsonElement> CloneSettings(MachineDto machine)
    {
        var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (machine.Settings is { ValueKind: JsonValueKind.Object } settings)
        {
            foreach (var prop in settings.EnumerateObject())
                dict[prop.Name] = prop.Value.Clone();
        }
        return dict;
    }
}
