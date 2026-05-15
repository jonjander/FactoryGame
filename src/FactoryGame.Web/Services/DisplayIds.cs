namespace FactoryGame.Web.Services;

public static class DisplayIds
{
    /// <summary>Short, human-readable id (first 8 hex chars, no dashes).</summary>
    public static string ShortGuid(Guid id) => id.ToString("N")[..8];

    public static string ShortGuid(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "";
        return Guid.TryParse(id, out var g) ? ShortGuid(g) : id.Length <= 10 ? id : id[..8] + "…";
    }
}
