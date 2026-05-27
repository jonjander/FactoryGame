using System.Text.Json;

namespace FactoryGame.Contracts.Json;

public static class FactoryGameJson
{
    public static JsonSerializerOptions Api { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
