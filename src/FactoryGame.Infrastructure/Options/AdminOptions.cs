namespace FactoryGame.Infrastructure.Options;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>Shared secret for bootstrapping API keys (set in dev/staging only).</summary>
    public string BootstrapToken { get; set; } = "";
}
