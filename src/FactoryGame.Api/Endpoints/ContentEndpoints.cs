using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Names;
using FactoryGame.Domain.Wiki;

namespace FactoryGame.Api.Endpoints;

public static class ContentEndpoints
{
    public static void MapContentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/content").WithTags("Content");

        group.MapGet("/elements", (string? locale) =>
            {
                var loc = string.IsNullOrWhiteSpace(locale) ? "en" : locale!;
                var items = ElementCatalog.All.Select(e =>
                {
                    var decoded = DnaDecoder.Decode(e.Dna);
                    return new
                    {
                        e.Id,
                        e.Symbol,
                        e.Dna,
                        name = ElementNameGenerator.Generate(e.Dna, loc),
                        nameVersion = ElementNameGenerator.Version,
                        decoded = new
                        {
                            phase = decoded.Phase.ToString(),
                            decoded.Explosivity,
                            flammability = decoded.Flammability,
                            toxicity = decoded.Toxicity,
                            boilingPoint = decoded.BoilingPoint,
                            freezePoint = decoded.FreezePoint,
                            decoded.FamilyId
                        }
                    };
                });
                return Results.Ok(items);
            })
            .WithName("ListElements")
            .WithOpenApi();

        group.MapGet("/wiki", (string? locale) =>
            {
                var loc = string.IsNullOrWhiteSpace(locale) ? "en" : locale!;
                return Results.Ok(new
                {
                    locale = loc,
                    nameGeneratorVersion = ElementNameGenerator.Version,
                    machines = MachineWikiCatalog.All,
                    elements = ElementCatalog.All.Select(e => new
                    {
                        e.Id,
                        e.Symbol,
                        displayName = ElementNameGenerator.Generate(e.Dna, loc),
                        dna = e.Dna
                    })
                });
            })
            .WithName("WikiSnapshot")
            .WithOpenApi();
    }
}
