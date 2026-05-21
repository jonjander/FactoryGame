using FactoryGame.Domain.Content;
using FactoryGame.Domain.Dna;
using FactoryGame.Domain.Simulation;
var starters = new HashSet<int>{1,2,3,4,5};
foreach (var e in ElementCatalog.All) {
  if (starters.Contains(e.Id)) continue;
  var p = DnaDecoder.Decode(e.Dna).Phase;
  var s = DnaTransforms.MeasureDnaSpreadPermille(e.Dna);
  if (p == MaterialPhase.Liquid && s >= 220)
    Console.WriteLine($"id={e.Id} spread={s}");
}
