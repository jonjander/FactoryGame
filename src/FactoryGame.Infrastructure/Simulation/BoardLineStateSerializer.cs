using System.Text.Json;
using FactoryGame.Domain.Simulation;

namespace FactoryGame.Infrastructure.Simulation;

public static class BoardLineStateSerializer
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(BoardLineState state) =>
        JsonSerializer.Serialize(ToDto(state), Json);

    public static BoardLineState Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<BoardLineStateDto>(json, Json);
        if (dto == null)
            return new BoardLineState();
        return FromDto(dto);
    }

    public static string SerializeDelta(SeaportTickDelta delta) =>
        JsonSerializer.Serialize(delta, Json);

    public static SeaportTickDelta DeserializeDelta(string json) =>
        JsonSerializer.Deserialize<SeaportTickDelta>(json, Json) ?? new SeaportTickDelta();

    private static BoardLineStateDto ToDto(BoardLineState state)
    {
        var dto = new BoardLineStateDto { RuleVersion = BoardLineState.RuleVersion };
        foreach (var (id, m) in state.Machines)
        {
            dto.Machines[id] = new MachineStateDto
            {
                MachineId = m.MachineId,
                MachineType = m.MachineType,
                BlockedReason = m.BlockedReason,
                Inputs = PortMap(m.InputPorts),
                Outputs = PortMap(m.OutputPorts),
                Tank = m.Tank == null ? null : new TankStateDto
                {
                    Capacity = m.Tank.Capacity,
                    Storage = m.Tank.Storage.Select(ToPacketDto).ToList()
                },
                Junction = m.Junction == null ? null : new JunctionStateDto
                {
                    NextOutIndex = m.Junction.NextOutIndex,
                    Out1Debt = m.Junction.Out1Debt,
                    Out2Debt = m.Junction.Out2Debt
                },
                ProcessingSlot = m.ProcessingSlot == null ? null : new ProcessingSlotDto
                {
                    Packet = m.ProcessingSlot.Packet == null ? null : ToPacketDto(m.ProcessingSlot.Packet),
                    ElapsedTicks = m.ProcessingSlot.ElapsedTicks,
                    TotalTicks = m.ProcessingSlot.TotalTicks,
                    OperationRatePermille = m.ProcessingSlot.OperationRatePermille,
                    TotalDelta = m.ProcessingSlot.TotalDelta,
                    ProcessKind = m.ProcessingSlot.ProcessKind
                }
            };
        }
        return dto;
    }

    private static Dictionary<string, List<PacketDto>> PortMap(Dictionary<string, PortBuffer> ports) =>
        ports.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Snapshot().Select(ToPacketDto).ToList(),
            StringComparer.Ordinal);

    private static BoardLineState FromDto(BoardLineStateDto dto)
    {
        var state = new BoardLineState();
        foreach (var (id, m) in dto.Machines)
        {
            var runtime = state.GetOrCreate(m.MachineId, m.MachineType);
            runtime.BlockedReason = m.BlockedReason;
            foreach (var (port, packets) in m.Inputs)
            {
                var buf = runtime.GetOrCreateInput(port);
                foreach (var p in packets)
                    buf.TryEnqueue(FromPacket(p));
            }
            foreach (var (port, packets) in m.Outputs)
            {
                var buf = runtime.GetOrCreateOutput(port);
                foreach (var p in packets)
                    buf.TryEnqueue(FromPacket(p));
            }

            if (m.Tank != null)
            {
                runtime.Tank = new TankInternalState { Capacity = m.Tank.Capacity };
                foreach (var p in m.Tank.Storage)
                    runtime.Tank.Storage.Add(FromPacket(p));
            }

            if (m.Junction != null)
            {
                runtime.Junction = new JunctionInternalState
                {
                    NextOutIndex = m.Junction.NextOutIndex,
                    Out1Debt = m.Junction.Out1Debt,
                    Out2Debt = m.Junction.Out2Debt
                };
            }

            if (m.ProcessingSlot != null)
            {
                runtime.ProcessingSlot = new ProcessingSlotState
                {
                    Packet = m.ProcessingSlot.Packet == null ? null : FromPacket(m.ProcessingSlot.Packet),
                    ElapsedTicks = m.ProcessingSlot.ElapsedTicks,
                    TotalTicks = m.ProcessingSlot.TotalTicks,
                    OperationRatePermille = m.ProcessingSlot.OperationRatePermille,
                    TotalDelta = m.ProcessingSlot.TotalDelta,
                    ProcessKind = m.ProcessingSlot.ProcessKind ?? ""
                };
            }
        }
        return state;
    }

    private static PacketDto ToPacketDto(MaterialPacket p) => new()
    {
        ElementId = p.ElementId,
        Dna = p.Dna,
        Quantity = p.Quantity,
        Quality = p.Quality.ToString()
    };

    private static MaterialPacket FromPacket(PacketDto p) => new()
    {
        ElementId = p.ElementId,
        Dna = p.Dna,
        Quantity = p.Quantity,
        Quality = Enum.TryParse<MaterialQuality>(p.Quality, out var q) ? q : MaterialQuality.Normal
    };

    private sealed class BoardLineStateDto
    {
        public int RuleVersion { get; set; } = 1;
        public Dictionary<string, MachineStateDto> Machines { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class MachineStateDto
    {
        public string MachineId { get; set; } = "";
        public string MachineType { get; set; } = "";
        public string? BlockedReason { get; set; }
        public Dictionary<string, List<PacketDto>> Inputs { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<PacketDto>> Outputs { get; set; } = new(StringComparer.Ordinal);
        public TankStateDto? Tank { get; set; }
        public JunctionStateDto? Junction { get; set; }
        public ProcessingSlotDto? ProcessingSlot { get; set; }
    }

    private sealed class TankStateDto
    {
        public int Capacity { get; set; }
        public List<PacketDto> Storage { get; set; } = [];
    }

    private sealed class JunctionStateDto
    {
        public int NextOutIndex { get; set; }
        public decimal Out1Debt { get; set; }
        public decimal Out2Debt { get; set; }
    }

    private sealed class ProcessingSlotDto
    {
        public PacketDto? Packet { get; set; }
        public int ElapsedTicks { get; set; }
        public int TotalTicks { get; set; } = 1;
        public int OperationRatePermille { get; set; } = 1000;
        public int TotalDelta { get; set; }
        public string? ProcessKind { get; set; }
    }

    private sealed class PacketDto
    {
        public int ElementId { get; set; }
        public long Dna { get; set; }
        public decimal Quantity { get; set; }
        public string Quality { get; set; } = "Normal";
    }
}
