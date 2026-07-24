using FactoryGame.Domain.Dna;

namespace FactoryGame.Domain.Simulation.Processors;

internal sealed class ToxicMelterProcessor : IMachineProcessor
{
    private const int MinToxicFluid = 60;
    private const int MaxCarrierToxic = 35;

    public string MachineType => "ToxicMelter";

    public void Process(MachineRuntimeState machine, TickContext ctx, string? settingsJson)
    {
        if (machine.IsBlocked)
            return;

        var in1Budget = ctx.GetPortInputBudget(MachineType, "in1", settingsJson);
        var in2Budget = ctx.GetPortInputBudget(MachineType, "in2", settingsJson);
        var pullBudget = Math.Min(in1Budget, in2Budget);

        var toxic = FlowHelper.PullFromInputBudget(machine, "in1", pullBudget);
        var carrier = FlowHelper.PullFromInputBudget(machine, "in2", pullBudget);
        if (toxic == null || carrier == null)
        {
            if (toxic != null) machine.GetOrCreateInput("in1").TryEnqueue(toxic);
            if (carrier != null) machine.GetOrCreateInput("in2").TryEnqueue(carrier);
            return;
        }

        var toxicBlock = ValidateToxicFluid(toxic);
        if (toxicBlock != null)
        {
            machine.BlockedReason = toxicBlock;
            machine.GetOrCreateInput("in1").TryEnqueue(toxic);
            machine.GetOrCreateInput("in2").TryEnqueue(carrier);
            return;
        }

        var carrierBlock = ValidateCarrierSolid(carrier);
        if (carrierBlock != null)
        {
            machine.BlockedReason = carrierBlock;
            machine.GetOrCreateInput("in1").TryEnqueue(toxic);
            machine.GetOrCreateInput("in2").TryEnqueue(carrier);
            return;
        }

        machine.BlockedReason = null;

        var consumed = Math.Min(toxic.Quantity, carrier.Quantity);
        if (consumed <= 0)
        {
            machine.GetOrCreateInput("in1").TryEnqueue(toxic);
            machine.GetOrCreateInput("in2").TryEnqueue(carrier);
            return;
        }

        toxic.Quantity = consumed;
        carrier.Quantity = consumed;

        var cut = ResolveCutBoiling(settingsJson);
        var heat = ResolveHeatPermille(settingsJson);
        var split = DnaTransforms.ToxicMeltSplit(
            toxic.Dna, toxic.ElementId, carrier.Dna, carrier.ElementId, cut, heat);

        var gasPermille = ResolveGasSplitPermille(settingsJson);
        var gasQty = consumed * gasPermille / 1000m;
        gasQty = ClampSplit(gasQty, consumed);
        var liquidQty = consumed - gasQty;

        var gasBudget = ctx.GetPortOutputBudget(MachineType, "out1", settingsJson);
        var liquidBudget = ctx.GetPortOutputBudget(MachineType, "out2", settingsJson);
        gasQty = Math.Min(gasQty, gasBudget);
        liquidQty = Math.Min(liquidQty, liquidBudget);

        var gasPkt = new MaterialPacket
        {
            ElementId = split.GasElementId,
            Dna = split.GasDna,
            Quantity = gasQty,
            Quality = split.ExtremeTransmute ? MaterialQuality.Ash : toxic.Quality
        };
        var liquidPkt = new MaterialPacket
        {
            ElementId = split.LiquidElementId,
            Dna = split.LiquidDna,
            Quantity = liquidQty,
            Quality = MaterialQuality.Normal
        };

        var outGas = machine.GetOrCreateOutput("out1");
        var outLiquid = machine.GetOrCreateOutput("out2");

        if (gasQty > 0 && !outGas.TryEnqueue(gasPkt))
        {
            machine.GetOrCreateInput("in1").TryEnqueue(toxic);
            machine.GetOrCreateInput("in2").TryEnqueue(carrier);
            return;
        }

        if (liquidQty > 0 && !outLiquid.TryEnqueue(liquidPkt))
        {
            if (gasQty > 0)
                _ = outGas.TryDequeue();
            machine.GetOrCreateInput("in1").TryEnqueue(toxic);
            machine.GetOrCreateInput("in2").TryEnqueue(carrier);
        }
    }

    private static string? ValidateToxicFluid(MaterialPacket pkt)
    {
        var d = DnaDecoder.Decode(pkt.Dna);
        if (d.Phase == MaterialPhase.Solid)
            return "Toxic feed must be liquid or gas — not solid.";
        if (d.Toxicity < MinToxicFluid)
            return "Toxic feed toxicity too low — need a hazardous fluid on in1.";
        return null;
    }

    private static string? ValidateCarrierSolid(MaterialPacket pkt)
    {
        var d = DnaDecoder.Decode(pkt.Dna);
        if (d.Phase != MaterialPhase.Solid)
            return "Carrier bed must be solid with low toxicity on in2.";
        if (d.Toxicity > MaxCarrierToxic)
            return "Carrier solid is too toxic — use a cleaner absorbent on in2.";
        return null;
    }

    private static decimal ClampSplit(decimal gasQty, decimal total)
    {
        if (total <= 0)
            return 0;
        var minSide = total * 0.15m;
        var maxGas = total - minSide;
        return Math.Clamp(gasQty, minSide, maxGas);
    }

    private static int ResolveCutBoiling(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 2048, 0, (int)DnaLayout.BoilingMask,
            "cutBoiling", "cutPoint", "cut", "targetBoil");

    private static int ResolveHeatPermille(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 650, 200, 1000, "heatPermille", "heat", "power");

    private static int ResolveGasSplitPermille(string? settingsJson) =>
        MachineSettingsJson.ReadInt(settingsJson, 380, 150, 650, "gasSplitPermille", "gasSplit");
}
