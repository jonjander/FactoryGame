namespace FactoryGame.Web;

/// <summary>Compact vs full material labels for canvas hints and tooltips.</summary>
public static class MaterialLabelUi
{
    /// <summary>Short variant code from full label, e.g. E03-000652.</summary>
    public static string? Compact(string? fullLabel)
    {
        if (string.IsNullOrEmpty(fullLabel))
            return fullLabel;

        var space = fullLabel.IndexOf(' ');
        return space > 0 ? fullLabel[..space] : fullLabel;
    }
}
