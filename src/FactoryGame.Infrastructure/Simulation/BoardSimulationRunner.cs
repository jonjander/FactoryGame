using FactoryGame.Domain.Boards;
using FactoryGame.Domain.Simulation;
using FactoryGame.Infrastructure.Data;
using FactoryGame.Infrastructure.Data.Entities;
using FactoryGame.Infrastructure.Options;
using FactoryGame.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FactoryGame.Infrastructure.Simulation;

public sealed class BoardSimulationRunner(AppDbContext db, IOptions<GameEconomyOptions> economyOptions)
{
    private readonly decimal _unitsPerTick = 1m;
    private readonly int _maxKeyframes = 60;

    public async Task<BoardTickResult?> TickBoardAsync(BoardEntity board, long globalTick, CancellationToken ct)
    {
        if (board.Mode != BoardMode.Running || board.RevisionVersion == 0)
            return null;

        var revision = await db.BoardRevisions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.BoardId == board.Id && r.Version == board.RevisionVersion, ct);
        if (revision == null)
            return null;

        var planDto = System.Text.Json.JsonSerializer.Deserialize<FactoryGame.Contracts.Boards.BoardPlanDto>(
            revision.PlanJson,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });
        if (planDto == null)
            return null;

        var simPlan = SimulationPlanMapper.ToSimulationPlan(planDto);
        var lastKf = await db.BoardKeyframes.AsNoTracking()
            .Where(k => k.BoardId == board.Id)
            .OrderByDescending(k => k.Tick)
            .FirstOrDefaultAsync(ct);

        BoardLineState? prev = lastKf != null
            ? BoardLineStateSerializer.Deserialize(lastKf.LineStateJson)
            : BoardTickEngine.CreateInitialState(simPlan);

        var pool = new SeaportPoolGateway(db, board.PlayerId);
        var result = BoardTickEngine.Advance(simPlan, prev, globalTick, _unitsPerTick, pool);

        db.BoardKeyframes.Add(new BoardKeyframeEntity
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Tick = globalTick,
            RevisionVersion = board.RevisionVersion,
            LineStateJson = BoardLineStateSerializer.Serialize(result.State),
            SeaportDeltaJson = BoardLineStateSerializer.SerializeDelta(result.SeaportDelta),
            CreatedAt = DateTimeOffset.UtcNow
        });

        board.SimulationTick = globalTick;
        board.LastSnapshotNote = result.SummaryNote;

        await TrimKeyframesAsync(board.Id, ct);
        await db.SaveChangesAsync(ct);
        return result;
    }

    private async Task TrimKeyframesAsync(Guid boardId, CancellationToken ct)
    {
        var count = await db.BoardKeyframes.CountAsync(k => k.BoardId == boardId, ct);
        if (count <= _maxKeyframes)
            return;
        var toRemove = await db.BoardKeyframes
            .Where(k => k.BoardId == boardId)
            .OrderBy(k => k.Tick)
            .Take(count - _maxKeyframes)
            .ToListAsync(ct);
        db.BoardKeyframes.RemoveRange(toRemove);
    }
}
