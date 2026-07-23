using System.Text.Json;
using FactoryGame.Contracts.Boards;
using FactoryGame.Domain.Boards;
using FactoryGame.Domain.Content;
using FactoryGame.Domain.Simulation;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using FactoryGame.Infrastructure.Options;
using FactoryGame.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactoryGame.Infrastructure.Services;

public sealed class BoardService(AppDbContext db, IOptions<GameEconomyOptions> economyOptions)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly GameEconomyOptions _economy = economyOptions.Value;

    public async Task<BoardSummaryDto> CreateBoardAsync(Guid playerId, string name, CancellationToken ct)
    {
        var board = new BoardEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = name,
            Mode = BoardMode.Edit,
            RevisionVersion = 0,
            SimulationTick = 0
        };
        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);
        return ToSummary(board);
    }

    public async Task<IReadOnlyList<BoardSummaryDto>> ListBoardsAsync(Guid playerId, CancellationToken ct)
    {
        var boards = await db.Boards.AsNoTracking()
            .Where(b => b.PlayerId == playerId)
            .OrderBy(b => b.Name)
            .ToListAsync(ct);
        if (boards.Count == 0)
            return Array.Empty<BoardSummaryDto>();

        var poolQty = await LoadPoolQuantitiesAsync(playerId, ct);
        var poolVariants = await LoadPoolVariantQuantitiesAsync(playerId, ct);
        var boardIds = boards.Select(b => b.Id).ToList();
        var revisions = await db.BoardRevisions.AsNoTracking()
            .Where(r => boardIds.Contains(r.BoardId))
            .ToListAsync(ct);
        var revisionByBoard = revisions
            .GroupBy(r => r.BoardId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Version));

        var runningIds = boards
            .Where(b => b.Mode == BoardMode.Running)
            .Select(b => b.Id)
            .ToHashSet();
        var latestKeyframes = runningIds.Count == 0
            ? new Dictionary<Guid, BoardKeyframeEntity>()
            : await db.BoardKeyframes.AsNoTracking()
                .Where(k => runningIds.Contains(k.BoardId))
                .GroupBy(k => k.BoardId)
                .Select(g => g.OrderByDescending(k => k.Tick).First())
                .ToDictionaryAsync(k => k.BoardId, ct);

        return boards
            .Select(board =>
            {
                BoardPlanDto plan = new(Array.Empty<MachineDto>(), Array.Empty<ConnectionDto>());
                if (board.RevisionVersion > 0
                    && revisionByBoard.TryGetValue(board.Id, out var versions)
                    && versions.TryGetValue(board.RevisionVersion, out var revision))
                {
                    plan = JsonSerializer.Deserialize<BoardPlanDto>(revision.PlanJson, Json)
                           ?? plan;
                }

                BoardLineState? runtime = null;
                SeaportTickDelta? delta = null;
                if (board.Mode == BoardMode.Running
                    && latestKeyframes.TryGetValue(board.Id, out var kf))
                {
                    runtime = BoardLineStateSerializer.Deserialize(kf.LineStateJson);
                    delta = BoardLineStateSerializer.DeserializeDelta(kf.SeaportDeltaJson);
                }

                return BuildSummary(board, plan, poolQty, poolVariants, runtime, delta);
            })
            .ToList();
    }

    public async Task RenameBoardAsync(Guid playerId, Guid boardId, string name, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");

        var trimmed = name.Trim();
        if (trimmed.Length > 120)
            throw new InvalidOperationException("Name must be at most 120 characters.");

        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == boardId && b.PlayerId == playerId, ct)
            ?? throw new InvalidOperationException("Board not found.");

        board.Name = trimmed;
        await db.SaveChangesAsync(ct);
    }

    public async Task<BoardPlanDto?> GetLatestPlanAsync(Guid playerId, Guid boardId, CancellationToken ct)
    {
        var board = await db.Boards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId && b.PlayerId == playerId, ct);
        if (board == null)
            return null;

        if (board.RevisionVersion == 0)
            return new BoardPlanDto(Array.Empty<MachineDto>(), Array.Empty<ConnectionDto>());

        var revision = await db.BoardRevisions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.BoardId == boardId && r.Version == board.RevisionVersion, ct);
        if (revision == null)
            return new BoardPlanDto(Array.Empty<MachineDto>(), Array.Empty<ConnectionDto>());

        return JsonSerializer.Deserialize<BoardPlanDto>(revision.PlanJson, Json)
               ?? new BoardPlanDto(Array.Empty<MachineDto>(), Array.Empty<ConnectionDto>());
    }

    public async Task PlaceMachineFromStockAsync(Guid playerId, Guid boardId, Guid stockId, string machineId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(machineId))
            throw new InvalidOperationException("Machine id is required.");
        if (machineId.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("Machine id must not contain whitespace.");
        foreach (var c in machineId)
        {
            if (!char.IsLetterOrDigit(c) && c is not ('_' or '-'))
                throw new InvalidOperationException("Machine id may only contain letters, digits, '_' and '-'.");
        }

        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == boardId && b.PlayerId == playerId, ct)
            ?? throw new InvalidOperationException("Board not found.");
        if (board.Mode != BoardMode.Edit)
            throw new InvalidOperationException("Plan can only be changed in Edit mode.");

        var stock = await db.PlayerMachineStocks.FirstOrDefaultAsync(s => s.Id == stockId && s.PlayerId == playerId, ct)
            ?? throw new InvalidOperationException("Stock item not found.");

        if (!MachinePortCatalog.IsKnownMachineType(stock.MachineType))
            throw new InvalidOperationException("Unknown machine type in stock.");

        var plan = await GetLatestPlanAsync(playerId, boardId, ct)
                   ?? new BoardPlanDto(Array.Empty<MachineDto>(), Array.Empty<ConnectionDto>());
        if (plan.Machines.Any(m => m.Id.Equals(machineId, StringComparison.Ordinal)))
            throw new InvalidOperationException("Machine id already exists on plan.");

        var machines = plan.Machines.ToList();
        var index = machines.Count;
        machines.Add(PlanMachineLayout.WithDefaultPosition(new MachineDto(machineId, stock.MachineType), index));
        var newPlan = PlanMachineLayout.NormalizeLayout(new BoardPlanDto(machines, plan.Connections.ToList()));
        ValidateSorterRules(newPlan);

        db.PlayerMachineStocks.Remove(stock);

        var json = JsonSerializer.Serialize(newPlan, Json);
        board.RevisionVersion++;
        db.BoardRevisions.Add(new BoardRevisionEntity
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Version = board.RevisionVersion,
            PlanJson = json,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task ReturnMachineToStockAsync(Guid playerId, Guid boardId, string machineId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(machineId))
            throw new InvalidOperationException("Machine id is required.");

        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == boardId && b.PlayerId == playerId, ct)
            ?? throw new InvalidOperationException("Board not found.");
        if (board.Mode != BoardMode.Edit)
            throw new InvalidOperationException("Plan can only be changed in Edit mode.");

        var plan = await GetLatestPlanAsync(playerId, boardId, ct)
                   ?? new BoardPlanDto(Array.Empty<MachineDto>(), Array.Empty<ConnectionDto>());
        var machine = plan.Machines.FirstOrDefault(m => m.Id.Equals(machineId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Machine not found on plan.");

        if (!MachinePortCatalog.IsKnownMachineType(machine.Type))
            throw new InvalidOperationException("Unknown machine type on plan.");

        var machines = plan.Machines.Where(m => !m.Id.Equals(machineId, StringComparison.Ordinal)).ToList();
        var connections = plan.Connections
            .Where(c => c.FromId != machineId && c.ToId != machineId)
            .ToList();
        var newPlan = PlanMachineLayout.NormalizeLayout(new BoardPlanDto(machines, connections));
        ValidateSorterRules(newPlan);

        db.PlayerMachineStocks.Add(new PlayerMachineStockEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            MachineType = machine.Type,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var json = JsonSerializer.Serialize(newPlan, Json);
        board.RevisionVersion++;
        db.BoardRevisions.Add(new BoardRevisionEntity
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Version = board.RevisionVersion,
            PlanJson = json,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task SavePlanAsync(Guid playerId, Guid boardId, BoardPlanDto plan, CancellationToken ct)
    {
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == boardId && b.PlayerId == playerId, ct)
            ?? throw new InvalidOperationException("Board not found.");
        if (board.Mode != BoardMode.Edit)
            throw new InvalidOperationException("Plan can only be saved in Edit mode.");

        ValidateSorterRules(plan);
        ValidateConnectionEndpoints(plan);

        var normalized = PlanMachineLayout.NormalizeLayout(plan);
        var json = JsonSerializer.Serialize(normalized, Json);
        board.RevisionVersion++;
        db.BoardRevisions.Add(new BoardRevisionEntity
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Version = board.RevisionVersion,
            PlanJson = json,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task StartBoardAsync(Guid playerId, Guid boardId, CancellationToken ct)
    {
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == boardId && b.PlayerId == playerId, ct)
            ?? throw new InvalidOperationException("Board not found.");
        if (board.Mode == BoardMode.Running)
            return;

        var revision = await db.BoardRevisions.AsNoTracking()
            .Where(r => r.BoardId == boardId && r.Version == board.RevisionVersion)
            .FirstOrDefaultAsync(ct);
        if (revision == null || board.RevisionVersion == 0)
            throw new InvalidOperationException("Save a plan before start.");

        var plan = JsonSerializer.Deserialize<BoardPlanDto>(revision.PlanJson, Json)
            ?? throw new InvalidOperationException("Invalid plan JSON.");

        ValidateConnectionEndpoints(plan);

        board.Mode = BoardMode.Running;
        board.SimulationTick = await GetGlobalTickAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task StopBoardAsync(Guid playerId, Guid boardId, CancellationToken ct)
    {
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == boardId && b.PlayerId == playerId, ct)
            ?? throw new InvalidOperationException("Board not found.");
        board.Mode = BoardMode.Edit;
        await db.SaveChangesAsync(ct);
    }

    public async Task<BoardInfoDto?> GetBoardInfoAsync(Guid playerId, Guid boardId, CancellationToken ct)
    {
        var board = await db.Boards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId && b.PlayerId == playerId, ct);
        if (board == null)
            return null;

        var plan = await GetLatestPlanAsync(playerId, boardId, ct)
                   ?? new BoardPlanDto(Array.Empty<MachineDto>(), Array.Empty<ConnectionDto>());
        return BuildBoardInfo(board, plan);
    }

    public async Task<BoardInfoDto?> PreviewBoardInfoAsync(Guid playerId, Guid boardId, BoardPlanDto plan, CancellationToken ct)
    {
        var board = await db.Boards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId && b.PlayerId == playerId, ct);
        if (board == null)
            return null;

        ValidateSorterRules(plan);
        return BuildBoardInfo(board, plan);
    }

    public async Task<BoardKeyframeDto?> GetLatestKeyframeAsync(Guid playerId, Guid boardId, CancellationToken ct)
    {
        var board = await db.Boards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId && b.PlayerId == playerId, ct);
        if (board == null)
            return null;
        var kf = await db.BoardKeyframes.AsNoTracking()
            .Where(k => k.BoardId == boardId)
            .OrderByDescending(k => k.Tick)
            .FirstOrDefaultAsync(ct);
        if (kf == null)
            return null;
        return ToKeyframeDto(board, kf);
    }

    public async Task<BoardKeyframesResponseDto?> GetKeyframesAfterAsync(
        Guid playerId,
        Guid boardId,
        long? afterTick,
        CancellationToken ct)
    {
        var board = await db.Boards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId && b.PlayerId == playerId, ct);
        if (board == null)
            return null;

        var q = db.BoardKeyframes.AsNoTracking().Where(k => k.BoardId == boardId);
        if (afterTick.HasValue)
            q = q.Where(k => k.Tick > afterTick.Value);

        var list = await q.OrderBy(k => k.Tick).Take(30).ToListAsync(ct);
        var dtos = list.Select(k => ToKeyframeDto(board, k)).ToList();
        return new BoardKeyframesResponseDto(dtos, board.SimulationTick);
    }

    private BoardInfoDto BuildBoardInfo(BoardEntity board, BoardPlanDto plan)
    {
        var machines = plan.Machines.Select(m => new MachineInfo(m.Id, m.Type, m.Settings)).ToList();
        var connections = plan.Connections
            .Select(c => new ConnectionInfo(c.FromId, c.FromPort, c.ToId, c.ToPort))
            .ToList();

        BoardLineState? runtime = null;
        SeaportTickDelta? delta = null;
        if (board.Mode == BoardMode.Running)
        {
            var kf = db.BoardKeyframes.AsNoTracking()
                .Where(k => k.BoardId == board.Id)
                .OrderByDescending(k => k.Tick)
                .FirstOrDefault();
            if (kf != null)
            {
                runtime = BoardLineStateSerializer.Deserialize(kf.LineStateJson);
                delta = BoardLineStateSerializer.DeserializeDelta(kf.SeaportDeltaJson);
            }
        }

        var poolQty = db.PoolStacks.AsNoTracking()
            .Where(s => s.PlayerId == board.PlayerId)
            .GroupBy(s => s.ElementId)
            .ToDictionary(g => g.Key, g => g.Sum(s => (decimal)s.Quantity));

        var poolVariants = db.PoolStacks.AsNoTracking()
            .Where(s => s.PlayerId == board.PlayerId)
            .ToDictionary(s => new PoolStackKey(s.ElementId, s.Dna), s => (decimal)s.Quantity);

        var prices = db.MarketPriceCandles.AsNoTracking()
            .GroupBy(c => c.ElementId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.BucketStart).First().Close);

        var report = BoardInfoAnalyzer.Analyze(new BoardInfoAnalyzeRequest(
            machines,
            connections,
            board.Mode == BoardMode.Running,
            _economy.SimulationTickIntervalSeconds,
            runtime,
            delta,
            poolQty,
            prices,
            poolVariants));

        var machineProgress = MachineRuntimeProgressAnalyzer.Analyze(machines, runtime);

        var simPlan = SimulationPlanMapper.ToSimulationPlan(plan);
        return new BoardInfoDto(
            board.Id,
            board.Name,
            board.Mode.ToString(),
            board.SimulationTick,
            new SeaportFlowsDto(
                report.IntoFactory.Select(MapFlow).ToList(),
                report.OutOfFactory.Select(MapFlow).ToList()),
            report.SeaportPorts.Select(MapPortFlow).ToList(),
            report.MachinePortFlows.Select(MapMachinePortFlow).ToList(),
            machineProgress.Select(MapMachineProgress).ToList(),
            new ThroughputDto(report.TotalUnitsPerSecond, report.ThroughputIsEstimate, report.ThroughputNote),
            new ValueEstimateDto(report.EstimatedValuePerSecond, report.ValueIsEstimate, report.ValueNote),
            report.Issues.Select(i => new BoardIssueDto(i.Severity, i.Code, i.Message, i.MachineId)).ToList(),
            plan.Machines.Count,
            plan.Connections.Count,
            PlanGraph.HasCycle(simPlan));
    }

    private static SeaportFlowLineDto MapFlow(SeaportFlowLine f) =>
        new(f.MachineId, f.MachineType, f.Port, f.LinkedMachineId, f.LinkedPort, f.UnitsPerSecond, f.Description);

    private static SeaportPortFlowDto MapPortFlow(SeaportPortFlowDetail p) =>
        new(p.MachineId, p.MachineType, p.Port, p.Direction, p.IsConnected, p.LinkedMachineId, p.LinkedPort,
            p.ElementId, p.ElementSymbol, p.Summary, p.IsEstimate);

    private static MachinePortFlowDto MapMachinePortFlow(MachinePortFlowDetail p) =>
        new(p.MachineId, p.MachineType, p.Port, p.LinkedMachineId, p.LinkedPort,
            p.InputElementId, p.InputElementSymbol, p.OutputElementId, p.OutputElementSymbol,
            p.InputPhase, p.OutputPhase, p.InputDna, p.OutputDna,
            p.TransformNote, p.Summary, p.ProcessStatus, p.DnaChanged, p.IsEstimate, p.IsPoolSource);

    private static MachineRuntimeProgressDto MapMachineProgress(MachineRuntimeProgressDetail p) =>
        new(p.MachineId, p.MachineType, p.OverallProgress, p.StepProgress, p.ProcessKind, p.IsActive,
            p.InputNeeds.Select(n => new MachineInputNeedDto(n.Port, n.Ready)).ToList());

    public async Task<BoardSnapshotDto?> GetSnapshotAsync(Guid playerId, Guid boardId, long? afterTick, CancellationToken ct)
    {
        var board = await db.Boards.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == boardId && b.PlayerId == playerId, ct);
        if (board == null)
            return null;
        if (afterTick.HasValue && board.SimulationTick <= afterTick.Value)
            return null;
        return new BoardSnapshotDto(board.Id, board.SimulationTick, board.LastSnapshotNote ?? "", board.Mode.ToString());
    }

    private async Task<long> GetGlobalTickAsync(CancellationToken ct)
    {
        var clock = await db.SimulationClock.AsNoTracking().FirstOrDefaultAsync(c => c.Id == 1, ct);
        return clock?.CurrentTick ?? 0;
    }

    private static void ValidateConnectionEndpoints(BoardPlanDto plan)
    {
        var machineIds = plan.Machines.Select(m => m.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var c in plan.Connections)
        {
            if (!machineIds.Contains(c.FromId))
                throw new InvalidOperationException($"Connection from unknown machine «{c.FromId}».");
            if (!machineIds.Contains(c.ToId))
                throw new InvalidOperationException($"Connection to unknown machine «{c.ToId}».");
        }
    }

    private BoardKeyframeDto ToKeyframeDto(BoardEntity board, BoardKeyframeEntity kf)
    {
        var delta = BoardLineStateSerializer.DeserializeDelta(kf.SeaportDeltaJson);
        return new BoardKeyframeDto(
            board.Id,
            kf.Tick,
            kf.RevisionVersion,
            board.LastSnapshotNote ?? "",
            board.Mode.ToString(),
            new SeaportDeltaDto(delta.WithdrawnFromPool, delta.DepositedToPool));
    }

    private static void ValidateSorterRules(BoardPlanDto plan)
    {
        foreach (var m in plan.Machines.Where(x => x.Type.Equals("Sorter", StringComparison.OrdinalIgnoreCase)))
        {
            if (m.Settings is not { } settings)
                continue;
            var assigned = new HashSet<int>();
            foreach (var portKey in new[] { "port1", "port2", "port3" })
            {
                if (!settings.TryGetProperty(portKey, out var arr) || arr.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var el in arr.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var id))
                        throw new InvalidOperationException($"Sorter {m.Id}: port {portKey} must be array of element ids.");
                    if (!assigned.Add(id))
                        throw new InvalidOperationException($"Sorter {m.Id}: element {id} appears on more than one port.");
                }
            }
        }
    }

    private async Task<Dictionary<int, decimal>> LoadPoolQuantitiesAsync(Guid playerId, CancellationToken ct) =>
        await db.PoolStacks.AsNoTracking()
            .Where(s => s.PlayerId == playerId)
            .GroupBy(s => s.ElementId)
            .Select(g => new { ElementId = g.Key, Quantity = g.Sum(s => (decimal)s.Quantity) })
            .ToDictionaryAsync(x => x.ElementId, x => x.Quantity, ct);

    private BoardSummaryDto BuildSummary(
        BoardEntity board,
        BoardPlanDto plan,
        IReadOnlyDictionary<int, decimal> poolQty,
        IReadOnlyDictionary<PoolStackKey, decimal> poolVariants,
        BoardLineState? runtime = null,
        SeaportTickDelta? delta = null)
    {
        var machines = plan.Machines.Select(m => new MachineInfo(m.Id, m.Type, m.Settings)).ToList();
        var connections = plan.Connections
            .Select(c => new ConnectionInfo(c.FromId, c.FromPort, c.ToId, c.ToPort))
            .ToList();

        var report = BoardInfoAnalyzer.Analyze(new BoardInfoAnalyzeRequest(
            machines,
            connections,
            board.Mode == BoardMode.Running,
            _economy.SimulationTickIntervalSeconds,
            runtime,
            delta,
            poolQty,
            null,
            poolVariants));

        var errorCount = report.Issues.Count(i => i.Severity == "error");
        var warningCount = report.Issues.Count(i => i.Severity == "warning");
        var health = ComputeBoardHealth(board.Mode == BoardMode.Running, errorCount, warningCount);
        var statusHint = BuildStatusHint(board.Mode, errorCount, warningCount, plan);

        return new BoardSummaryDto(
            board.Id,
            board.Name,
            board.Mode.ToString(),
            board.RevisionVersion,
            board.SimulationTick,
            plan.Machines.Count,
            plan.Connections.Count,
            health,
            errorCount,
            warningCount,
            statusHint);
    }

    private static string ComputeBoardHealth(bool isRunning, int errorCount, int warningCount)
    {
        if (errorCount > 0)
            return "error";
        if (warningCount > 0)
            return "warning";
        return isRunning ? "running" : "ok";
    }

    private static string BuildStatusHint(
        BoardMode mode,
        int errorCount,
        int warningCount,
        BoardPlanDto plan)
    {
        var parts = new List<string>();
        parts.Add(mode == BoardMode.Running ? "Running" : "Stopped");

        if (plan.Machines.Count == 0 && plan.Connections.Count == 0)
            parts.Add("tom plan");
        else if (errorCount > 0)
            parts.Add(errorCount == 1 ? "1 blockerare" : $"{errorCount} blockerare");
        else if (warningCount > 0)
            parts.Add(warningCount == 1 ? "1 varning" : $"{warningCount} varningar");

        return string.Join(" · ", parts);
    }

    private async Task<Dictionary<PoolStackKey, decimal>> LoadPoolVariantQuantitiesAsync(Guid playerId, CancellationToken ct) =>
        await db.PoolStacks.AsNoTracking()
            .Where(s => s.PlayerId == playerId)
            .ToDictionaryAsync(s => new PoolStackKey(s.ElementId, s.Dna), s => (decimal)s.Quantity, ct);

    private BoardSummaryDto ToSummary(BoardEntity b) =>
        BuildSummary(
            b,
            new BoardPlanDto(Array.Empty<MachineDto>(), Array.Empty<ConnectionDto>()),
            new Dictionary<int, decimal>(),
            new Dictionary<PoolStackKey, decimal>());
}
