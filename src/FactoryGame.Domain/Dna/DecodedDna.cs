namespace FactoryGame.Domain.Dna;

public readonly record struct DecodedDna(
    MaterialPhase Phase,
    int Explosivity,
    int Flammability,
    int Toxicity,
    int BoilingPoint,
    int FreezePoint,
    int FamilyId);
