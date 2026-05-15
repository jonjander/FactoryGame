using System.Text.Json;

namespace FactoryGame.Domain.Simulation;

/// <summary>Shared JSON settings readers for machine processors (discrete values from UI).</summary>
internal static class MachineSettingsJson
{
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
}
