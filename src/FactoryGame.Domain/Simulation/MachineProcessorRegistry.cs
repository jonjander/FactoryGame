namespace FactoryGame.Domain.Simulation;

public static class MachineProcessorRegistry
{
    private static readonly IReadOnlyDictionary<string, IMachineProcessor> ByType =
        new IMachineProcessor[]
        {
            new Processors.BoilerProcessor(),
            new Processors.HeaterProcessor(),
            new Processors.CoolerProcessor(),
            new Processors.MixerProcessor(),
            new Processors.SorterProcessor(),
            new Processors.SeaportConnectorProcessor(),
            new Processors.SeaportInProcessor(),
            new Processors.SeaportOutProcessor(),
            new Processors.UnsupportedProcessor("LiquidSeparator"),
            new Processors.UnsupportedProcessor("Destilator")
        }.ToDictionary(p => p.MachineType, StringComparer.OrdinalIgnoreCase);

    public static IMachineProcessor Resolve(string machineType) =>
        ByType.TryGetValue(machineType, out var p)
            ? p
            : new Processors.UnsupportedProcessor(machineType);
}
