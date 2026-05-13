using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FactoryGame.Web;
using FactoryGame.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Configuration.AddJsonFile("factory-config.json", optional: true, reloadOnChange: false);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = builder.Configuration["ApiBaseUrl"]?.Trim().TrimEnd('/');
if (string.IsNullOrEmpty(apiBase))
    apiBase = "http://localhost:5176";

builder.Services.AddSingleton<BrowserStorage>();
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddScoped<AuthMessageHandler>();
builder.Services.AddSingleton<OfflineCommandQueue>();

builder.Services.AddHttpClient("api", c => { c.BaseAddress = new Uri(apiBase); })
    .AddHttpMessageHandler<AuthMessageHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("api"));

await builder.Build().RunAsync();
