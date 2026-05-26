namespace FactoryGame.Domain.Content;

public static class ElementCatalogLookup
{
    public static long CatalogDnaFor(int elementId)
    {
        var element = ElementCatalog.All.FirstOrDefault(e => e.Id == elementId);
        return element.Id == elementId ? element.Dna : 0;
    }

    public static bool IsKnownElementId(int elementId) =>
        ElementCatalog.All.Any(e => e.Id == elementId);
}
