using System;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FactoryGame.Web;
using FactoryGame.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Configuration.AddJsonFile("factory-config.json", optional: true, reloadOnChange: false);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var configuredApi = builder.Configuration["ApiBaseUrl"]?.Trim() ?? "";
var pageBase = builder.HostEnvironment.BaseAddress;
// If the SPA is served from a real host (e.g. Azure) but config still points at loopback
// (common when ASPNETCORE_ENVIRONMENT=Development on the server caused WASM to load dev appsettings),
// ignore that ApiBaseUrl — otherwise the browser calls the user's localhost and fails with "Load failed".
if (!string.IsNullOrEmpty(configuredApi) && IsLoopbackUrl(configuredApi) && !IsLoopbackUrl(pageBase))
    configuredApi = "";

// API default from src/FactoryGame.Api/Properties/launchSettings.json (https profile).
const string defaultLocalApiHttps = "https://localhost:7145/";
var apiBase = string.IsNullOrEmpty(configuredApi)
    ? (LooksLikeBlazorWasmDevHost(pageBase) ? defaultLocalApiHttps : pageBase)
    : configuredApi.TrimEnd('/') + "/";

static bool LooksLikeBlazorWasmDevHost(string baseAddress)
{
    if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var u))
        return false;
    if (!u.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
        !u.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase))
        return false;
    // Ports from src/FactoryGame.Web/Properties/launchSettings.json (Kestrel + IIS Express).
    return u.Port is 5130 or 7048 or 32617 or 44310;
}

static bool IsLoopbackUrl(string value)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out var u))
        return false;
    return u.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || u.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
}

builder.Services.AddSingleton<BrowserStorage>();
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddScoped<AuthMessageHandler>();
builder.Services.AddSingleton<OfflineCommandQueue>();

builder.Services.AddHttpClient("api", c => { c.BaseAddress = new Uri(apiBase); })
    .AddHttpMessageHandler<AuthMessageHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("api"));

await builder.Build().RunAsync();
