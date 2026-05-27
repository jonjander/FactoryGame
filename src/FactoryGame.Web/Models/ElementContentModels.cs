using System.Text.Json.Serialization;

namespace FactoryGame.Web.Models;

public sealed class ElementContentItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = "";

    [JsonPropertyName("dna")]
    public long Dna { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("decoded")]
    public ElementDecodedProperties Decoded { get; set; } = new();
}

public sealed class ElementDecodedProperties
{
    [JsonPropertyName("phase")]
    public string Phase { get; set; } = "";

    [JsonPropertyName("explosivity")]
    public int Explosivity { get; set; }

    [JsonPropertyName("flammability")]
    public int Flammability { get; set; }

    [JsonPropertyName("toxicity")]
    public int Toxicity { get; set; }

    [JsonPropertyName("boilingPoint")]
    public int BoilingPoint { get; set; }

    [JsonPropertyName("freezePoint")]
    public int FreezePoint { get; set; }

    [JsonPropertyName("familyId")]
    public int FamilyId { get; set; }
}

public sealed class WikiContentResponse
{
    [JsonPropertyName("locale")]
    public string Locale { get; set; } = "";

    [JsonPropertyName("nameGeneratorVersion")]
    public int NameGeneratorVersion { get; set; }

    [JsonPropertyName("machines")]
    public List<WikiMachineItem> Machines { get; set; } = [];

    [JsonPropertyName("elements")]
    public List<WikiElementItem> Elements { get; set; } = [];
}

public sealed class WikiMachineItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("ports")]
    public string Ports { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";
}

public sealed class WikiElementItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    [JsonPropertyName("dna")]
    public long Dna { get; set; }
}
