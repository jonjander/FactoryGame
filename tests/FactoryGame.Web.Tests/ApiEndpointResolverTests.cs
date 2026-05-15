using FactoryGame.Web.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;

namespace FactoryGame.Web.Tests;

public sealed class ApiEndpointResolverTests
{
    [Fact]
    public void Auto_on_azure_host_uses_same_origin()
    {
        var config = Config(("ApiTarget", "Auto"));
        var host = Host("https://factorygame.example.com/");

        var uri = ApiEndpointResolver.Resolve(config, host, null);

        Assert.Equal("https://factorygame.example.com/", uri.ToString());
    }

    [Fact]
    public void Auto_on_wasm_dev_host_uses_local_api()
    {
        var config = Config(("ApiTarget", "Auto"));
        var host = Host("https://localhost:7048/", development: true);

        var uri = ApiEndpointResolver.Resolve(config, host, null);

        Assert.Equal("https://localhost:7145/", uri.ToString());
    }

    [Fact]
    public void Auto_on_co_hosted_api_uses_same_origin()
    {
        var config = Config(("ApiTarget", "Auto"));
        var host = Host("https://localhost:7145/", development: true);

        var uri = ApiEndpointResolver.Resolve(config, host, null);

        Assert.Equal("https://localhost:7145/", uri.ToString());
    }

    [Fact]
    public void Build_target_Azure_overrides_local_dev_defaults()
    {
        var config = Config(
            ("ApiTarget", "LocalDev"),
            ("AzureApiBaseUrl", "https://factorygame.example.com"));
        var host = Host("https://localhost:7048/", development: true);

        var uri = ApiEndpointResolver.Resolve(config, host, "Azure");

        Assert.Equal("https://factorygame.example.com/", uri.ToString());
    }

    [Fact]
    public void Loopback_api_base_ignored_when_page_on_azure()
    {
        var config = Config(("ApiBaseUrl", "https://localhost:7145"));
        var host = Host("https://factorygame.example.com/");

        var uri = ApiEndpointResolver.Resolve(config, host, null);

        Assert.Equal("https://factorygame.example.com/", uri.ToString());
    }

    private static IConfiguration Config(params (string Key, string Value)[] pairs)
    {
        var dict = pairs.ToDictionary(p => p.Key, p => (string?)p.Value, StringComparer.OrdinalIgnoreCase);
        return new ConfigurationBuilder().AddInMemoryCollection(dict!).Build();
    }

    private static IWebAssemblyHostEnvironment Host(string baseAddress, bool development = false) =>
        new FakeWasmHost(baseAddress, development);

    private sealed class FakeWasmHost(string baseAddress, bool development) : IWebAssemblyHostEnvironment
    {
        public string Environment => development ? "Development" : "Production";
        public string BaseAddress { get; } = baseAddress;
        public bool IsDevelopment() => development;
        public bool IsProduction() => !development;
        public bool IsStaging() => false;
    }
}
