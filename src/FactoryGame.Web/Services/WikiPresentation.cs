using FactoryGame.Web.Models;

namespace FactoryGame.Web.Services;

public static class WikiPresentation
{
    public static string MachineDisplayName(string type) => type switch
    {
        "Boiler" => "Vätskepanna",
        "LiquidSeparator" => "Vätskeavskiljare",
        "Destilator" => "Destillator",
        "Mixer" => "Blandare",
        "Heater" => "Värmare",
        "Cooler" => "Kylare",
        "Condenser" => "Kondensator",
        "Crystallizer" => "Kristallisator",
        "Melter" => "Smältugn",
        "Sorter" => "Sorterare",
        "Tank" => "Tank",
        "Junction" => "Förgrening",
        "RateLimiter" => "Flödesbroms",
        "SeaportConnector" => "Seaport-koppling",
        "SeaportIn" => "Seaport in (legacy)",
        "SeaportOut" => "Seaport ut (legacy)",
        _ => type
    };

    public static string MachineCategory(string type) => type switch
    {
        "Boiler" or "Heater" or "Cooler" or "Melter" => "heat",
        "Condenser" or "Crystallizer" => "phase",
        "Mixer" or "Destilator" or "LiquidSeparator" or "Sorter" => "separation",
        "Tank" or "Junction" or "RateLimiter" or "SeaportConnector" or "SeaportIn" or "SeaportOut" => "logistics",
        _ => "other"
    };

    public static string MachineCategoryLabel(string category) => category switch
    {
        "heat" => "Värme & smältning",
        "phase" => "Fasomvandling",
        "separation" => "Separation & sortering",
        "logistics" => "Seaport & logistik",
        _ => "Övrigt"
    };

    public static string MachineEmoji(string type) => type switch
    {
        "Boiler" or "Heater" => "🔥",
        "Cooler" => "❄️",
        "Condenser" => "💧",
        "Crystallizer" => "🧊",
        "Melter" => "🫠",
        "Mixer" => "🌀",
        "Destilator" or "LiquidSeparator" => "⚗️",
        "Sorter" => "🔀",
        "Tank" => "🛢️",
        "Junction" => "⑂",
        "RateLimiter" => "🚦",
        "SeaportConnector" or "SeaportIn" or "SeaportOut" => "🚢",
        _ => "⚙️"
    };

    public static string ExtendedSummary(string type, string apiSummary) =>
        string.IsNullOrWhiteSpace(GetExtendedSummary(type))
            ? apiSummary
            : GetExtendedSummary(type);

    public static IReadOnlyList<string> MachineTips(string type) => type switch
    {
        "Boiler" =>
        [
            "Kräver vätskefas — gas går inte igenom.",
            "Höjer temperaturband i DNA deterministiskt.",
            "Koppla seaport out → panna in för en enkel startloop."
        ],
        "Heater" =>
        [
            "Ökar energi/temperaturband stegvis.",
            "Var försiktig med högt explosiva ämnen.",
            "Bra före separation som kräver värme."
        ],
        "Cooler" =>
        [
            "Sänker energi/temperaturband.",
            "Giftiga ämnen kan blockera kylning.",
            "Använd före kondensering eller kristallisation."
        ],
        "Condenser" =>
        [
            "Kräver gasform — aldrig fast input.",
            "Symbol kan vara oförändrad; fas (gas→vätska) syns i poolen.",
            "Utgående material blir alltid vätska."
        ],
        "Crystallizer" =>
        [
            "Fryser ostabil/spretig vätska till fast form.",
            "Ger aldrig gas som output.",
            "Kompakt solid passerar oftast igenom."
        ],
        "Melter" =>
        [
            "Smälter spridd solid till vätska via kokband.",
            "Kompakta fasta ämnen kan passera oförändrade.",
            "Bra mellansteg före vätskeprocesser."
        ],
        "Mixer" =>
        [
            "Två ingångar — ratio och intensitet styr DNA.",
            "Låg intensitet → kompakt, stabil DNA.",
            "Hög intensitet → volatil DNA för destillation."
        ],
        "Destilator" =>
        [
            "Separerar i två fraktioner utifrån kokpunkt.",
            "Kräver vätske- eller gasfas.",
            "Ställ reflux och cut efter önskade fraktioner."
        ],
        "LiquidSeparator" =>
        [
            "Endast vätskor — cut styr ut1 vs ut2.",
            "Enklare än destillator när du bara vill dela flöde.",
            "Cut nära mitten ger jämnare fördelning."
        ],
        "Sorter" =>
        [
            "Konfigurera grundämnen på port 1–3.",
            "Allt omatchat hamnar på rest-port.",
            "Kontrollera att downstream tål ämnets DNA."
        ],
        "SeaportConnector" =>
        [
            "Out från pool → in till fabrik.",
            "In till pool lagrar produktion per DNA-variant.",
            "Välj rätt fas (gas vs vätska) i inställningar."
        ],
        _ => ["Placera från maskinlager.", "Spara planen efter kopplingar.", "Starta fabriken när loop är klar."]
    };

    public static IReadOnlyList<(string In, string Out)> ParsePortRatio(string ports)
    {
        var parts = ports.Split(':', 2);
        if (parts.Length != 2)
            return [(ports, "")];

        return [(parts[0].Trim(), parts[1].Trim())];
    }

    public static string PhaseCssClass(string phase) => phase.ToLowerInvariant() switch
    {
        "liquid" => "fg-phase-liquid",
        "gas" => "fg-phase-gas",
        _ => "fg-phase-solid"
    };

    public static string PhaseLabelSv(string phase) => phase switch
    {
        "Liquid" => "Vätska",
        "Gas" => "Gas",
        _ => "Fast"
    };

    public static string DailyTip(IReadOnlyList<WikiMachineItem> machines)
    {
        if (machines.Count == 0)
            return "Wiki genereras live från samma regeldata som servern — inga manuella sidor.";

        var day = DateTime.UtcNow.DayOfYear;
        var machine = machines[day % machines.Count];
        return $"Dagens maskintips ({MachineEmoji(machine.Type)} {MachineDisplayName(machine.Type)}): {MachineTips(machine.Type)[0]}";
    }

    private static string GetExtendedSummary(string type) => type switch
    {
        "Boiler" => "Höjer temperaturen i materialets DNA med bitwise mask — vätskor blir varmare och kan förberedas för separation.",
        "Heater" => "Deterministisk uppvärmning av energi/temperaturband. Enklare än pannan men samma fasregler.",
        "Cooler" => "Deterministisk kylning. Sänker temperaturband — vissa giftiga ämnen kan blockera processen.",
        "Condenser" => "Omvandlar gas till vätska genom att sänka kokpunktsbandet. Output är alltid vätska.",
        "Crystallizer" => "Ostabil vätska kristalliseras till fast form via fryspunktsband. Aldrig gas ut.",
        "Melter" => "Spridd solid smälts till vätska via kokband. Kompakt solid kan passera.",
        "Mixer" => "Blandar två strömmar. Intensitet och ratio styr om DNA blir kompakt eller volatil.",
        "Destilator" => "Fraktionerar material i två utgångar baserat på kokpunkt och reflux.",
        "LiquidSeparator" => "Delar vätska i två utgångar vid valt cut — snabbare än full destillation.",
        "Sorter" => "Dirigerar valda grundämnen till port 1–3; resten till rest-port.",
        "SeaportConnector" => "Bro mellan fabrik och din seaport-pool — material in/ut per DNA-variant.",
        _ => ""
    };
}
