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

    public static string PhaseLabel(MaterialPhase phase) =>
        phase switch
        {
            MaterialPhase.Gas => "Gas",
            MaterialPhase.Liquid => "Liquid",
            MaterialPhase.Solid => "Solid",
            _ => "Solid"
        };

    public static string PhaseLabel(string phaseKey) =>
        phaseKey switch
        {
            "Gas" => "Gas",
            "Liquid" => "Liquid",
            "Solid" => "Solid",
            _ => "Solid"
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
