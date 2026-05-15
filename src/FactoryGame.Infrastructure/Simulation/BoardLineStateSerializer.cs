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
        var dto = new BoardLineStateDto();
        foreach (var (id, m) in state.Machines)
        {
            dto.Machines[id] = new MachineStateDto
            {
                MachineId = m.MachineId,
                MachineType = m.MachineType,
                BlockedReason = m.BlockedReason,
                Inputs = m.InputPorts.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.Snapshot().Select(p => new PacketDto
                    {
                        ElementId = p.ElementId,
                        Dna = p.Dna,
                        Quantity = p.Quantity,
                        Quality = p.Quality.ToString()
                    }).ToList()),
                Outputs = m.OutputPorts.ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value.Snapshot().Select(p => new PacketDto
                    {
                        ElementId = p.ElementId,
                        Dna = p.Dna,
                        Quantity = p.Quantity,
                        Quality = p.Quality.ToString()
                    }).ToList())
            };
        }
        return dto;
    }

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
        }
        return state;
    }

    private static MaterialPacket FromPacket(PacketDto p) => new()
    {
        ElementId = p.ElementId,
        Dna = p.Dna,
        Quantity = p.Quantity,
        Quality = Enum.TryParse<MaterialQuality>(p.Quality, out var q) ? q : MaterialQuality.Normal
    };

    private sealed class BoardLineStateDto
    {
        public Dictionary<string, MachineStateDto> Machines { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class MachineStateDto
    {
        public string MachineId { get; set; } = "";
        public string MachineType { get; set; } = "";
        public string? BlockedReason { get; set; }
        public Dictionary<string, List<PacketDto>> Inputs { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<PacketDto>> Outputs { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class PacketDto
    {
        public int ElementId { get; set; }
        public long Dna { get; set; }
        public decimal Quantity { get; set; }
        public string Quality { get; set; } = "Normal";
    }
}
