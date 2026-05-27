using System.Globalization;
using System.Text.Json;

namespace FactoryGame.Domain.Simulation;

/// <summary>Shared JSON settings readers for machine processors (discrete values from UI).</summary>
internal static class MachineSettingsJson
{
    public static long ReadInt64(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => ParseInt64String(el.GetString()),
            JsonValueKind.Number => el.GetInt64(),
            _ => 0
        };

    public static long ReadInt64(string? settingsJson, params string[] names)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            var root = doc.RootElement;
            foreach (var name in names)
            {
                if (root.TryGetProperty(name, out var el))
                {
                    var v = ReadInt64(el);
                    if (v != 0)
                        return v;
                }
            }
        }
        catch
        {
            /* default */
        }

        return 0;
    }

    internal static long ParseInt64String(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0;
        return long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;
    }

    public static int ReadInt(string? settingsJson, int defaultValue, int min, int max, params string[] names)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return defaultValue;

        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            var root = doc.RootElement;
            foreach (var name in names)
            {
                if (root.TryGetProperty(name, out var el) && el.TryGetInt32(out var v))
                    return Math.Clamp(v, min, max);
            }
        }
        catch
        {
            /* default */
        }

        return defaultValue;
    }

    public static string ReadString(string? settingsJson, params string[] names)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return "";

        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            var root = doc.RootElement;
            foreach (var name in names)
            {
                if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s.Trim();
                }
            }
        }
        catch
        {
            /* default */
        }

        return "";
    }

    public static string ReadString(string? settingsJson, string defaultValue, params string[] names)
    {
        var s = ReadString(settingsJson, names);
        return string.IsNullOrEmpty(s) ? defaultValue : s;
    }
}
