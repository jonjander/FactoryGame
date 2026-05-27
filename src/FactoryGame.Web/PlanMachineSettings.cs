using System.Globalization;
using System.Text.Json;
using FactoryGame.Contracts.Boards;

namespace FactoryGame.Web;

internal static class PlanMachineSettings
{
    public static int GetInt(MachineDto machine, string key, int defaultValue)
    {
        if (machine.Settings is not { ValueKind: JsonValueKind.Object } settings)
            return defaultValue;
        if (settings.TryGetProperty(key, out var el) && el.TryGetInt32(out var v))
            return v;
        return defaultValue;
    }

    public static int GetOutElementId(MachineDto machine) =>
        GetInt(machine, "outElementId", 0);

    public static long GetOutMaterialDna(MachineDto machine) =>
        GetLong(machine, "outMaterialDna", 0);

    public static long GetLong(MachineDto machine, string key, long defaultValue)
    {
        if (machine.Settings is not { ValueKind: JsonValueKind.Object } settings)
            return defaultValue;
        if (settings.TryGetProperty(key, out var el))
            return ReadLongElement(el);
        return defaultValue;
    }

    private static long ReadLongElement(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => long.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 0,
            JsonValueKind.Number => el.GetInt64(),
            _ => 0
        };

    public static int GetSorterPortElement(MachineDto machine, string portKey)
    {
        if (machine.Settings is not { ValueKind: JsonValueKind.Object } settings)
            return 0;
        if (!settings.TryGetProperty(portKey, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return 0;
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var id) && id > 0)
                return id;
        }
        return 0;
    }

    public static MachineDto WithInt(MachineDto machine, string key, int value)
    {
        var dict = CloneSettings(machine);
        dict[key] = JsonSerializer.SerializeToElement(value);
        return machine with { Settings = JsonSerializer.SerializeToElement(dict) };
    }

    public static MachineDto WithOutElementId(MachineDto machine, int elementId) =>
        WithInt(machine, "outElementId", elementId);

    public static MachineDto WithLong(MachineDto machine, string key, long value)
    {
        var dict = CloneSettings(machine);
        if (key.Equals("outMaterialDna", StringComparison.Ordinal))
            dict[key] = JsonSerializer.SerializeToElement(value.ToString(CultureInfo.InvariantCulture));
        else
            dict[key] = JsonSerializer.SerializeToElement(value);
        return machine with { Settings = JsonSerializer.SerializeToElement(dict) };
    }

    public static MachineDto WithOutMaterialVariant(MachineDto machine, int elementId, long dna)
    {
        var updated = WithOutElementId(machine, elementId);
        return WithLong(updated, "outMaterialDna", dna);
    }

    public static MachineDto WithSorterPort(MachineDto machine, string portKey, int elementId)
    {
        var dict = CloneSettings(machine);
        if (elementId <= 0)
            dict.Remove(portKey);
        else
            dict[portKey] = JsonSerializer.SerializeToElement(new[] { elementId });
        return machine with { Settings = JsonSerializer.SerializeToElement(dict) };
    }

    public static MachineDto ApplyDefaults(MachineDto machine)
    {
        var fields = MachineSettingsUi.GetFields(machine.Type);
        if (fields.Count == 0)
            return machine;

        var updated = machine;
        foreach (var field in fields)
        {
            if (MachineSettingsUi.UsesElementPicker(field))
                continue;
            if (GetInt(updated, field.JsonKey, -1) >= 0
                && updated.Settings is { ValueKind: JsonValueKind.Object })
                continue;
            updated = WithInt(updated, field.JsonKey, field.Options[0].Value);
        }
        return updated;
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
