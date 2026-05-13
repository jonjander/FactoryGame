using System.Reflection;

namespace FactoryGame.Web;

public static class AppReleaseVersion
{
    private static readonly string? Informational =
        typeof(AppReleaseVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    public static string Current =>
        Informational
        ?? typeof(AppReleaseVersion).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";
}
