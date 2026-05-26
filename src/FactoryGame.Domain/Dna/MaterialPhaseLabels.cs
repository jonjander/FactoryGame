namespace FactoryGame.Domain.Dna;

public static class MaterialPhaseLabels
{
    public static string PhaseKey(MaterialPhase phase) =>
        phase switch
        {
            MaterialPhase.Gas => "Gas",
            MaterialPhase.Liquid => "Liquid",
            MaterialPhase.Solid => "Solid",
            _ => "Solid"
        };

    public static string PhaseLabelSv(MaterialPhase phase) =>
        phase switch
        {
            MaterialPhase.Gas => "gasform",
            MaterialPhase.Liquid => "flytande",
            MaterialPhase.Solid => "fast",
            _ => "fast"
        };

    public static string PhaseLabelSv(string phaseKey) =>
        phaseKey switch
        {
            "Gas" => "gasform",
            "Liquid" => "flytande",
            "Solid" => "fast",
            _ => "fast"
        };

    public static MaterialPhase DecodePhase(long dna) => DnaDecoder.Decode(dna).Phase;

    public static int PhaseSortOrder(MaterialPhase phase) =>
        phase switch
        {
            MaterialPhase.Gas => 0,
            MaterialPhase.Liquid => 1,
            MaterialPhase.Solid => 2,
            _ => 3
        };
}
