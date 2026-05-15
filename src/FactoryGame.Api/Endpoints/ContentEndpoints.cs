using FactoryGame.Contracts.Machines;
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

        group.MapGet("/machine-store", () =>
            {
                MachinePortDto[] PortsFor(string machineType) =>
                    MachinePortCatalog.GetPorts(machineType)
                        .Select(p => new MachinePortDto(p.Name, p.Direction == PortDirection.In ? "in" : "out"))
                        .ToArray();

                var store = MachineStoreCatalog.All.Select(e =>
                    new MachineStoreItemDto(e.MachineType, e.DisplayName, e.Price, true, PortsFor(e.MachineType))).ToList();

                // Legacy plan types: port metadata only (not purchasable; use SeaportConnector in store).
                var connectors = new[]
                {
                    (type: "SeaportIn", display: "Seaport in (legacy)", price: 0m),
                    (type: "SeaportOut", display: "Seaport ut (legacy)", price: 0m)
                }.Select(x => new MachineStoreItemDto(x.type, x.display, x.price, false, PortsFor(x.type))).ToList();

                return Results.Ok(new { store, connectors });
            })
            .WithName("GetMachineStore")
            .WithOpenApi();

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
