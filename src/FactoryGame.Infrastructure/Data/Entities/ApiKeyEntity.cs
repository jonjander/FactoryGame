namespace FactoryGame.Infrastructure.Data.Entities;

public class ApiKeyEntity
{
    public Guid Id { get; set; }

    /// <summary>SHA-256 (hex, lowercase) of the raw API key.</summary>
    public string KeyHash { get; set; } = "";

    public Guid PlayerId { get; set; }

    /// <summary>Comma-separated scopes, e.g. "market,boards,admin".</summary>
    public string Scopes { get; set; } = "";

    public string Name { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}
