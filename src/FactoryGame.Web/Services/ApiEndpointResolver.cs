using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;

namespace FactoryGame.Web.Services;

/// <summary>Resolves HttpClient API base URL for WASM (Azure same-origin, local dev API, or explicit Azure).</summary>
public static class ApiEndpointResolver
{
    public const string DefaultLocalApiHttps = "https://localhost:7145";
    public const string DefaultAzureApiHttps =
        "https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net";

    private static readonly int[] CoHostedApiPorts = [7145, 5176];

    public static Uri Resolve(IConfiguration config, IWebAssemblyHostEnvironment host, string? buildApiTarget = null)
    {
        var pageBase = NormalizeBase(host.BaseAddress);
        var target = ResolveTarget(config, buildApiTarget);
        var explicitUrl = config["ApiBaseUrl"]?.Trim() ?? "";

        // Legacy / custom URL: ignore loopback ApiBaseUrl when SPA is on a real host (Azure + dev appsettings).
        if (!string.IsNullOrEmpty(explicitUrl) && IsLoopbackUrl(explicitUrl) && !IsLoopbackUrl(pageBase))
            explicitUrl = "";

        var resolved = target switch
        {
            ApiTarget.SameOrigin => pageBase,
            ApiTarget.LocalDev => GetConfiguredOrDefault(config, "LocalApiBaseUrl", DefaultLocalApiHttps),
            ApiTarget.Azure => GetConfiguredOrDefault(config, "AzureApiBaseUrl", DefaultAzureApiHttps),
            ApiTarget.Custom => string.IsNullOrEmpty(explicitUrl) ? pageBase : explicitUrl,
            _ => ResolveAuto(config, host, pageBase, explicitUrl)
        };

        resolved = NormalizeBase(resolved);

        if (IsLoopbackUrl(resolved) && !IsLoopbackUrl(pageBase))
            resolved = pageBase;

        return new Uri(resolved);
    }

    private static ApiTarget ResolveTarget(IConfiguration config, string? buildApiTarget)
    {
        if (!string.IsNullOrWhiteSpace(buildApiTarget)
            && Enum.TryParse<ApiTarget>(buildApiTarget, ignoreCase: true, out var fromBuild)
            && fromBuild != ApiTarget.Auto)
            return fromBuild;

        var configured = config["ApiTarget"]?.Trim() ?? "";
        if (Enum.TryParse<ApiTarget>(configured, ignoreCase: true, out var fromConfig) && fromConfig != ApiTarget.Auto)
            return fromConfig;

        if (!string.IsNullOrWhiteSpace(config["ApiBaseUrl"]))
            return ApiTarget.Custom;

        return ApiTarget.Auto;
    }

    private static string ResolveAuto(
        IConfiguration config,
        IWebAssemblyHostEnvironment host,
        string pageBase,
        string explicitUrl)
    {
        if (!string.IsNullOrEmpty(explicitUrl))
            return explicitUrl;

        if (!IsLoopbackUrl(pageBase))
            return pageBase;

        if (IsCoHostedApiOrigin(pageBase))
            return pageBase;

        if (host.IsDevelopment())
            return GetConfiguredOrDefault(config, "LocalApiBaseUrl", DefaultLocalApiHttps);

        return pageBase;
    }

    private static string GetConfiguredOrDefault(IConfiguration config, string key, string fallback)
    {
        var value = config[key]?.Trim();
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private static bool IsCoHostedApiOrigin(string baseAddress)
    {
        if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var u))
            return false;
        if (!IsLoopbackHost(u.Host))
            return false;
        return CoHostedApiPorts.Contains(u.Port);
    }

    public static bool IsLoopbackUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var u))
            return false;
        return IsLoopbackHost(u.Host);
    }

    private static bool IsLoopbackHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeBase(string url)
    {
        var trimmed = url.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return "/";
        return trimmed.EndsWith('/') ? trimmed : trimmed + "/";
    }

    private enum ApiTarget
    {
        Auto,
        SameOrigin,
        LocalDev,
        Azure,
        Custom
    }
}
