using System.Text.Json;

namespace FactoryGame.Contracts.Boards;

public static class PlanMachineLayout
{
    private const double DefaultColWidth = 150;
    private const double DefaultRowHeight = 100;

    public static (double X, double Y) GetDefaultPosition(int machineIndex)
    {
        var col = machineIndex % 4;
        var row = machineIndex / 4;
        return (40 + col * DefaultColWidth, 40 + row * DefaultRowHeight);
    }

    public static (double X, double Y) GetPosition(MachineDto machine, int index)
    {
        if (machine.Settings is { ValueKind: JsonValueKind.Object } settings
            && settings.TryGetProperty("x", out var xEl)
            && settings.TryGetProperty("y", out var yEl)
            && xEl.TryGetDouble(out var x)
            && yEl.TryGetDouble(out var y))
            return (x, y);

        return GetDefaultPosition(index);
    }

    public static MachineDto WithPosition(MachineDto machine, double x, double y)
    {
        var dict = CloneSettings(machine.Settings);
        dict["x"] = JsonSerializer.SerializeToElement(x);
        dict["y"] = JsonSerializer.SerializeToElement(y);
        return machine with { Settings = JsonSerializer.SerializeToElement(dict) };
    }

    public static MachineDto WithDefaultPosition(MachineDto machine, int index)
    {
        if (HasStoredPosition(machine))
            return machine;

        var (x, y) = GetDefaultPosition(index);
        return WithPosition(machine, x, y);
    }

    public static BoardPlanDto NormalizeLayout(BoardPlanDto plan)
    {
        var machines = new List<MachineDto>();
        for (var i = 0; i < plan.Machines.Count; i++)
            machines.Add(WithDefaultPosition(plan.Machines[i], i));
        return new BoardPlanDto(machines, plan.Connections);
    }

    private static bool HasStoredPosition(MachineDto machine) =>
        machine.Settings is { ValueKind: JsonValueKind.Object } settings
        && settings.TryGetProperty("x", out var xEl)
        && settings.TryGetProperty("y", out var yEl)
        && xEl.TryGetDouble(out _)
        && yEl.TryGetDouble(out _);

    private static Dictionary<string, JsonElement> CloneSettings(JsonElement? settings)
    {
        var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (settings is { ValueKind: JsonValueKind.Object } obj)
        {
            foreach (var prop in obj.EnumerateObject())
                dict[prop.Name] = prop.Value.Clone();
        }
        return dict;
    }
}
