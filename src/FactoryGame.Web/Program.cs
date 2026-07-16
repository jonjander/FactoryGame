using System.Reflection;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FactoryGame.Web;
using FactoryGame.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Configuration.AddJsonFile("factory-config.json", optional: true, reloadOnChange: false);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var buildApiTarget = typeof(Program).Assembly
    .GetCustomAttributes<AssemblyMetadataAttribute>()
    .FirstOrDefault(a => a.Key == "FactoryGameApiTarget")?.Value;

var apiBaseUri = ApiEndpointResolver.Resolve(builder.Configuration, builder.HostEnvironment, buildApiTarget);

builder.Services.AddSingleton(new ApiEndpointInfo(apiBaseUri));
builder.Services.AddSingleton<BrowserStorage>();
builder.Services.AddSingleton<BoardIssueMuteStore>();
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddSingleton<AdminTokenStore>();
builder.Services.AddSingleton<WalletState>();
builder.Services.AddSingleton<SnackbarService>();
builder.Services.AddSingleton<AppVersionMonitor>();
builder.Services.AddScoped<AuthMessageHandler>();
builder.Services.AddSingleton<OfflineCommandQueue>();
builder.Services.AddSingleton<ViewportLayoutService>();
builder.Services.AddSingleton<GameWindowService>();
builder.Services.AddSingleton<BoardCanvasSession>();
builder.Services.AddSingleton<GameShellNavigation>();

builder.Services.AddHttpClient("api", c => { c.BaseAddress = apiBaseUri; })
    .AddHttpMessageHandler<AuthMessageHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("api"));

await builder.Build().RunAsync();
