using FactoryGame.Contracts.Boards;
using FactoryGame.Contracts.Machines;
using FactoryGame.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace FactoryGame.Api.Endpoints;

public static class BoardEndpoints
{
    public static void MapBoardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/boards").WithTags("Boards");

        group.MapGet("/{boardId:guid}/info", async Task<IResult> (HttpContext http, Guid boardId, BoardService boards, CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                var info = await boards.GetBoardInfoAsync(playerId, boardId, ct);
                return info == null ? Results.NotFound() : Results.Ok(info);
            })
            .WithName("GetBoardInfo")
            .WithOpenApi();

        group.MapPost("/{boardId:guid}/info/preview", async Task<IResult> (
                HttpContext http,
                Guid boardId,
                [FromBody] SavePlanRequest? body,
                BoardService boards,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                if (body?.Plan == null)
                    return Results.BadRequest(new { error = "Plan is required." });
                try
                {
                    var info = await boards.PreviewBoardInfoAsync(playerId, boardId, body.Plan, ct);
                    return info == null ? Results.NotFound() : Results.Ok(info);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("PreviewBoardInfo")
            .WithOpenApi();

        group.MapGet("/{boardId:guid}/plan", async Task<IResult> (HttpContext http, Guid boardId, BoardService boards, CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                var plan = await boards.GetLatestPlanAsync(playerId, boardId, ct);
                return plan == null ? Results.NotFound() : Results.Ok(plan);
            })
            .WithName("GetBoardPlan")
            .WithOpenApi();

        group.MapPost("/{boardId:guid}/place-from-stock", async Task<IResult> (
                HttpContext http,
                Guid boardId,
                [FromBody] PlaceMachineFromStockRequest? body,
                BoardService boards,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                if (body == null || body.StockId == Guid.Empty || string.IsNullOrWhiteSpace(body.MachineId))
                    return Results.BadRequest(new { error = "StockId and MachineId are required." });
                try
                {
                    await boards.PlaceMachineFromStockAsync(playerId, boardId, body.StockId, body.MachineId.Trim(), ct);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("PlaceMachineFromStock")
            .WithOpenApi();

        group.MapPost("/{boardId:guid}/return-to-stock", async Task<IResult> (
                HttpContext http,
                Guid boardId,
                [FromBody] ReturnMachineToStockRequest? body,
                BoardService boards,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                if (body == null || string.IsNullOrWhiteSpace(body.MachineId))
                    return Results.BadRequest(new { error = "MachineId is required." });
                try
                {
                    await boards.ReturnMachineToStockAsync(playerId, boardId, body.MachineId.Trim(), ct);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("ReturnMachineToStock")
            .WithOpenApi();

        group.MapPost("/", async Task<IResult> (HttpContext http, [FromBody] CreateBoardRequest? body, BoardService boards, CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                var boardName = string.IsNullOrWhiteSpace(body?.Name) ? "Factory" : body!.Name.Trim();
                try
                {
                    var created = await boards.CreateBoardAsync(playerId, boardName, ct);
                    return Results.Ok(created);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("CreateBoard")
            .WithOpenApi();

        group.MapGet("/", async Task<IResult> (HttpContext http, BoardService boards, CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                var list = await boards.ListBoardsAsync(playerId, ct);
                return Results.Ok(list);
            })
            .WithName("ListBoards")
            .WithOpenApi();

        group.MapPatch("/{boardId:guid}", async Task<IResult> (
                HttpContext http,
                Guid boardId,
                [FromBody] RenameBoardRequest? body,
                BoardService boards,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                if (body == null || string.IsNullOrWhiteSpace(body.Name))
                    return Results.BadRequest(new { error = "Name is required." });
                try
                {
                    await boards.RenameBoardAsync(playerId, boardId, body.Name, ct);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("RenameBoard")
            .WithOpenApi();

        group.MapPut("/{boardId:guid}/plan", async Task<IResult> (
                HttpContext http,
                Guid boardId,
                [FromBody] SavePlanRequest body,
                BoardService boards,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                try
                {
                    await boards.SavePlanAsync(playerId, boardId, body.Plan, ct);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("SaveBoardPlan")
            .WithOpenApi();

        group.MapPost("/{boardId:guid}/start", async Task<IResult> (HttpContext http, Guid boardId, BoardService boards, CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                try
                {
                    await boards.StartBoardAsync(playerId, boardId, ct);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("StartBoard")
            .WithOpenApi();

        group.MapPost("/{boardId:guid}/stop", async Task<IResult> (HttpContext http, Guid boardId, BoardService boards, CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                try
                {
                    await boards.StopBoardAsync(playerId, boardId, ct);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
            })
            .WithName("StopBoard")
            .WithOpenApi();

        group.MapGet("/{boardId:guid}/keyframes/latest", async Task<IResult> (
                HttpContext http,
                Guid boardId,
                BoardService boards,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                var kf = await boards.GetLatestKeyframeAsync(playerId, boardId, ct);
                return kf == null ? Results.NotFound() : Results.Ok(kf);
            })
            .WithName("GetLatestBoardKeyframe")
            .WithOpenApi();

        group.MapGet("/{boardId:guid}/keyframes", async Task<IResult> (
                HttpContext http,
                Guid boardId,
                long? afterTick,
                BoardService boards,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                var resp = await boards.GetKeyframesAfterAsync(playerId, boardId, afterTick, ct);
                return resp == null ? Results.NotFound() : Results.Ok(resp);
            })
            .WithName("GetBoardKeyframes")
            .WithOpenApi();

        group.MapGet("/{boardId:guid}/snapshot", async Task<IResult> (
                HttpContext http,
                Guid boardId,
                long? afterTick,
                BoardService boards,
                CancellationToken ct) =>
            {
                if (http.Items["PlayerId"] is not Guid playerId)
                    return Results.Unauthorized();
                var snap = await boards.GetSnapshotAsync(playerId, boardId, afterTick, ct);
                return snap == null ? Results.NotFound() : Results.Ok(snap);
            })
            .WithName("GetBoardSnapshot")
            .WithOpenApi();
    }
}
