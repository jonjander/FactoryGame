using System.Text.Json;
using FactoryGame.Contracts.Boards;
using FactoryGame.Domain.Boards;
using FactoryGame.Domain.Content;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using FactoryGame.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactoryGame.Infrastructure.Services;

public sealed class BoardService(AppDbContext db, IOptions<GameEconomyOptions> economyOptions)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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
        return boards.Select(ToSummary).ToList();
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
        machines.Add(new MachineDto(machineId, stock.MachineType));
        var newPlan = new BoardPlanDto(machines, plan.Connections.ToList());
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

    public async Task SavePlanAsync(Guid playerId, Guid boardId, BoardPlanDto plan, CancellationToken ct)
    {
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == boardId && b.PlayerId == playerId, ct)
            ?? throw new InvalidOperationException("Board not found.");
        if (board.Mode != BoardMode.Edit)
            throw new InvalidOperationException("Plan can only be saved in Edit mode.");

        ValidateSorterRules(plan);

        var json = JsonSerializer.Serialize(plan, Json);
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

        var cost = plan.Machines.Count * _economy.MachinePlacementCost;
        var balance = await db.PlayerBalances.FirstAsync(b => b.PlayerId == playerId, ct);
        if (balance.Cash < cost)
            throw new InvalidOperationException("Insufficient cash for economic audit at start.");

        balance.Cash -= cost;
        db.EconomyTransactions.Add(new EconomyTransactionEntity
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = "BoardStartAudit",
            CashDelta = -cost,
            CreatedAt = DateTimeOffset.UtcNow,
            Metadata = boardId.ToString()
        });

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

    private static BoardSummaryDto ToSummary(BoardEntity b) =>
        new(b.Id, b.Name, b.Mode.ToString(), b.RevisionVersion, b.SimulationTick);
}
