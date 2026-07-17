using System.Net.Http.Json;
using System.Text.Json;
using FactoryGame.Contracts.Boards;
using FactoryGame.Contracts.Machines;
using FactoryGame.Contracts.Pool;
using FactoryGame.Contracts.Json;
using FactoryGame.Web.Models;

namespace FactoryGame.Web.Services;

public sealed class BoardCanvasSession : IAsyncDisposable
{
    public event Action? Changed;

    private void NotifyChanged() => Changed?.Invoke();

    private static readonly JsonSerializerOptions JsonRelaxed = FactoryGameJson.Api;
    private static readonly JsonSerializerOptions PlanJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly HttpClient _http;
    private readonly OfflineCommandQueue _queue;
    private readonly BrowserStorage _storage;
    private readonly WalletState _wallet;
    private readonly SnackbarService _snackbar;
    private readonly BoardIssueMuteStore _issueMutes;

    private PeriodicTimer? _pollTimer;
    private CancellationTokenSource? _pollCts;
    private TaskCompletionSource<bool>? _confirmTcs;
    private readonly Dictionary<Guid, IReadOnlyList<BoardIssueDto>> _issuesByBoardId = new();

    public bool IsInitialized { get; private set; }

    public BoardCanvasSession(
        HttpClient http,
        OfflineCommandQueue queue,
        BrowserStorage storage,
        WalletState wallet,
        SnackbarService snackbar,
        BoardIssueMuteStore issueMutes)
    {
        _http = http;
        _queue = queue;
        _storage = storage;
        _wallet = wallet;
        _snackbar = snackbar;
        _issueMutes = issueMutes;
    }


    

    public bool Busy { get; private set; }
    public string? Error { get; private set; }
    public List<BoardSummaryDto> Boards { get; private set; } = new();
    public Guid? Selected { get; private set; }
    public string PlanJson { get; set; } = DefaultPlan();
    public string Snapshot { get; private set; } = "";
    public string? SaveHint { get; private set; }
    public BoardInfoDto? BoardInfo { get; private set; }
    public BoardKeyframeDto? LatestKeyframe { get; private set; }
    
    
    public bool ConfirmVisible { get; private set; }
    public string ConfirmMessage { get; private set; } = "";
    

    public string PipeFromId { get; set; } = "";
    public string PipeFromPort { get; set; } = "";
    public string PipeToId { get; set; } = "";
    public string PipeToPort { get; set; } = "";
    public string PlaceStockId { get; set; } = "";
    public string PlaceMachineId { get; set; } = "";
    public List<MachineStoreItemDto> StoreItems { get; private set; } = new();
    public List<MachineStoreItemDto> Connectors { get; private set; } = new();
    public List<PlayerMachineStockDto> Inventory { get; private set; } = new();
    public Dictionary<string, MachineStoreItemDto> MachineMeta { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ElementContentItem> Elements { get; private set; } = [];
    public string SettingsMachineId { get; set; } = "";
    public HashSet<int> OwnedPoolElementIds { get; private set; } = [];
    public List<PoolVariantStackDto> OwnedPoolVariants { get; private set; } = [];
    public bool StoreInfoVisible { get; private set; }
    public MachineStoreItemDto? StoreInfoItem { get; private set; }
    public bool RenamingBoard { get; private set; }
    public string RenameDraft { get; set; } = "";
    public HashSet<string> MutedIssueKeys { get; private set; } = new(StringComparer.Ordinal);
    public sealed class MachineStorePayload
    {
        public List<MachineStoreItemDto> Store { get; set; } = new();
        public List<MachineStoreItemDto> Connectors { get; set; } = new();
    }

    private static string DefaultPlan() =>
        """{"machines":[{"id":"mix1","type":"Mixer"}],"connections":[]}""";

    public async Task InitializeAsync()
    {
        await LoadBoardsAsync();
        await _wallet.RefreshAsync();
        await LoadMachineStoreAsync();
        await LoadInventoryAsync();
        await EnsureElementsAsync();
        await LoadPoolOwnershipAsync();
        await _issueMutes.EnsureLoadedAsync();
        MutedIssueKeys = _issueMutes.MutedKeys.ToHashSet(StringComparer.Ordinal);
        StartPollingIfRunning();
        IsInitialized = true;
        NotifyChanged();
    }

    /// <summary>Prefetch issues for boards with server-side warnings/errors so client mutes can adjust list tiles.</summary>
    public async Task PrefetchBoardIssuesAsync()
    {
        var targets = Boards
            .Where(b => b.ErrorCount > 0 || b.WarningCount > 0
                || string.Equals(b.Health, "error", StringComparison.OrdinalIgnoreCase)
                || string.Equals(b.Health, "warning", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targets.Count == 0)
        {
            ApplyClientHealthOverrides();
            return;
        }

        const int batchSize = 4;
        for (var i = 0; i < targets.Count; i += batchSize)
        {
            var batch = targets.Skip(i).Take(batchSize);
            await Task.WhenAll(batch.Select(async board =>
            {
                if (_issuesByBoardId.ContainsKey(board.Id))
                    return;
                try
                {
                    var info = await _http.GetFromJsonAsync<BoardInfoDto>(
                        $"/v1/boards/{board.Id}/info", JsonRelaxed);
                    if (info != null)
                        CacheBoardIssues(board.Id, info.Issues);
                }
                catch
                {
                    /* offline or unauthorized */
                }
            }));
        }

        ApplyClientHealthOverrides();
    }

    public ValueTask DisposeAsync()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollTimer?.Dispose();
        return ValueTask.CompletedTask;
    }

    public bool IsSelectedBoardRunning()
    {
        if (Selected is not { } id)
            return false;
        var b = Boards.FirstOrDefault(x => x.Id == id);
        return b != null && string.Equals(b.Mode, "Running", StringComparison.OrdinalIgnoreCase);
    }

    public void StartPollingIfRunning()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        _pollTimer?.Dispose();
        _pollCts = null;
        _pollTimer = null;

        if (!IsSelectedBoardRunning() || Selected is not { } boardId)
            return;

        _pollCts = new CancellationTokenSource();
        _pollTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        _ = PollRunningBoardAsync(boardId, _pollCts.Token);
    }

    public async Task PollRunningBoardAsync(Guid boardId, CancellationToken ct)
    {
        if (_pollTimer == null)
            return;
        try
        {
            while (await _pollTimer.WaitForNextTickAsync(ct))
            {
                await LoadBoardInfoAsync();
                await LoadLatestKeyframeAsync(boardId);
                await LoadPoolOwnershipAsync();
                await LoadBoardsAsync();
                NotifyChanged();
            }
        }
        catch (OperationCanceledException)
        {
            /* stopped */
        }
    }

    public async Task LoadLatestKeyframeAsync(Guid boardId)
    {
        try
        {
            LatestKeyframe = await _http.GetFromJsonAsync<BoardKeyframeDto>(
                $"/v1/boards/{boardId}/keyframes/latest", JsonRelaxed);
        }
        catch
        {
            LatestKeyframe = null;
        }
    }

    public async Task OnCanvasPlanChangedAsync(BoardPlanDto plan)
    {
        PlanJson = SerializePlan(plan);
        NotifyChanged();
    }

    public async Task OnCanvasLayoutCommittedAsync()
    {
        if (IsSelectedBoardRunning())
            return;
        await SyncBoardInfoFromEditorAsync();
        await SavePlanQuietAsync();
    }

    public IReadOnlyList<BoardIssueDto> GetVisibleBoardIssues()
    {
        if (Selected is not { } boardId)
            return Array.Empty<BoardIssueDto>();
        return FilterVisibleIssues(GetRawIssuesForBoard(boardId));
    }

    public IReadOnlyList<BoardIssueDto> FilterVisibleIssues(IReadOnlyList<BoardIssueDto>? issues)
    {
        if (issues == null || issues.Count == 0)
            return Array.Empty<BoardIssueDto>();
        return issues
            .Where(i => i.Severity != "warning"
                        || !MutedIssueKeys.Contains(BoardIssueMuteStore.IssueKey(i.Code, i.MachineId)))
            .ToList();
    }

    private IReadOnlyList<BoardIssueDto>? GetRawIssuesForBoard(Guid boardId)
    {
        if (Selected == boardId && BoardInfo?.Issues != null)
            return BoardInfo.Issues;
        return _issuesByBoardId.TryGetValue(boardId, out var cached) ? cached : null;
    }

    public void CacheBoardIssues(Guid boardId, IReadOnlyList<BoardIssueDto>? issues)
    {
        if (issues == null)
            return;
        _issuesByBoardId[boardId] = issues;
    }

    public void ApplyClientHealthOverrides()
    {
        for (var i = 0; i < Boards.Count; i++)
        {
            var board = Boards[i];
            var raw = GetRawIssuesForBoard(board.Id);
            if (raw == null)
                continue;

            var visible = FilterVisibleIssues(raw);
            var health = ComputeHealthFromVisible(board, visible);
            var hint = BuildStatusHintFromVisible(board, visible);
            var errors = visible.Count(x => x.Severity == "error");
            var warnings = visible.Count(x => x.Severity == "warning");
            Boards[i] = board with
            {
                Health = health,
                StatusHint = hint,
                ErrorCount = errors,
                WarningCount = warnings
            };
        }
    }

    private static string ComputeHealthFromVisible(BoardSummaryDto board, IReadOnlyList<BoardIssueDto> visible)
    {
        if (visible.Any(i => i.Severity == "error"))
            return "error";
        if (visible.Any(i => i.Severity == "warning"))
            return "warning";
        if (board.Mode.Equals("Running", StringComparison.OrdinalIgnoreCase))
            return "running";
        return "ok";
    }

    private static string BuildStatusHintFromVisible(BoardSummaryDto board, IReadOnlyList<BoardIssueDto> visible)
    {
        var errors = visible.Count(i => i.Severity == "error");
        var warnings = visible.Count(i => i.Severity == "warning");
        var parts = new List<string>
        {
            board.Mode.Equals("Running", StringComparison.OrdinalIgnoreCase) ? "Running" : "Stopped"
        };
        if (board.PlanMachineCount == 0 && board.PlanConnectionCount == 0)
            parts.Add("empty plan");
        else if (errors > 0)
            parts.Add(errors == 1 ? "1 blocker" : $"{errors} blockers");
        else if (warnings > 0)
            parts.Add(warnings == 1 ? "1 warning" : $"{warnings} warnings");
        return string.Join(" · ", parts);
    }

    public async Task ToggleIssueMuteAsync(BoardIssueDto issue)
    {
        await _issueMutes.ToggleMuteAsync(issue.Code, issue.MachineId);
        MutedIssueKeys = _issueMutes.MutedKeys.ToHashSet(StringComparer.Ordinal);
        ApplyClientHealthOverrides();
        NotifyChanged();
    }

    public async Task OnCanvasConnectionAddedAsync((string FromId, string FromPort, string ToId, string ToPort) pipe) =>
        await TryApplyConnectionAsync(pipe.FromId, pipe.FromPort, pipe.ToId, pipe.ToPort);

    public Task OnCanvasConnectionRemovedAsync(int index) => RemoveConnectionAtAsync(index);

    public async Task OnCanvasMachineRemovedAsync(string machineId)
    {
        if (IsSelectedBoardRunning() || string.IsNullOrWhiteSpace(machineId))
            return;
        if (!TryDeserializePlan(out var plan))
            return;

        var machines = plan.Machines.Where(m => !m.Id.Equals(machineId, StringComparison.Ordinal)).ToList();
        if (machines.Count == plan.Machines.Count)
            return;

        var connections = plan.Connections
            .Where(c => c.FromId != machineId && c.ToId != machineId)
            .ToList();
        PlanJson = SerializePlan(new BoardPlanDto(machines, connections));
        SettingsMachineId = "";
        SaveHint = $"Machine {machineId} removed (not saved).";
        _snackbar.Show($"Machine {machineId} removed — save the plan.", SnackbarKind.Info);
        await SyncBoardInfoFromEditorAsync();
        NotifyChanged();
    }

    public void ShowStoreInfo(MachineStoreItemDto item)
    {
        StoreInfoItem = item;
        StoreInfoVisible = true;
    }

    public void CloseStoreInfo()
    {
        StoreInfoVisible = false;
        StoreInfoItem = null;
    }

    public string GetBoardModeCss() =>
        IsSelectedBoardRunning() ? "fg-board-mode-running" : "fg-board-mode-stopped";

    public string GetEffectiveBoardHealth(BoardSummaryDto board)
    {
        var raw = GetRawIssuesForBoard(board.Id);
        if (raw != null)
            return ComputeHealthFromVisible(board, FilterVisibleIssues(raw));
        return board.Health;
    }

    public string GetDisplayStatusHint(BoardSummaryDto board)
    {
        var raw = GetRawIssuesForBoard(board.Id);
        if (raw != null)
            return BuildStatusHintFromVisible(board, FilterVisibleIssues(raw));
        return board.StatusHint ?? "";
    }

    public string GetBoardTileCss(BoardSummaryDto board) => GetEffectiveBoardHealth(board) switch
    {
        "running" => "fg-board-tile-running",
        "error" => "fg-board-tile-error",
        "warning" => "fg-board-tile-warning",
        _ => "fg-board-tile-stopped"
    };

    public string GetBoardStatusPillCss(BoardSummaryDto board) => GetEffectiveBoardHealth(board) switch
    {
        "running" => "fg-status-running",
        "error" => "fg-status-error",
        "warning" => "fg-status-warning",
        _ => "fg-status-stopped"
    };

    public string GetBoardStatusLabel(BoardSummaryDto board) => GetEffectiveBoardHealth(board) switch
    {
        "running" => "Running",
        "error" => "Error",
        "warning" => "Warning",
        _ => "Stopped"
    };

    public string GetSelectedBoardName()
    {
        if (Selected is not { } id)
            return "Plan";
        return Boards.FirstOrDefault(b => b.Id == id)?.Name
               ?? BoardInfo?.Name
               ?? "Plan";
    }

    public void BeginRename()
    {
        RenameDraft = GetSelectedBoardName();
        RenamingBoard = true;
    }

    public void CancelRename()
    {
        RenamingBoard = false;
        RenameDraft = "";
    }

    public async Task SaveRenameAsync()
    {
        if (Selected is not { } boardId)
            return;
        if (string.IsNullOrWhiteSpace(RenameDraft))
        {
            Error = "Enter a name.";
            return;
        }

        Busy = true;
        Error = null;
        try
        {
            var res = await _http.PatchAsJsonAsync($"/v1/boards/{boardId}", new RenameBoardRequest(RenameDraft.Trim()));
            if (!res.IsSuccessStatusCode)
            {
                Error = await res.Content.ReadAsStringAsync();
                return;
            }

            RenamingBoard = false;
            RenameDraft = "";
            await LoadBoardsAsync();
            if (BoardInfo != null)
                BoardInfo = BoardInfo with { Name = GetSelectedBoardName() };
            _snackbar.Show("Board name updated.");
        }
        catch (Exception ex)
        {
            Error = ApiConnectionErrors.Format(ex);
        }
        finally
        {
            Busy = false;
        }
    }

    public double GetEstimatedUnitsPerSecond() =>
        BoardInfo?.Throughput.TotalUnitsPerSecond ?? 0;

    public async Task LoadPoolOwnershipAsync()
    {
        try
        {
            var view = await _http.GetFromJsonAsync<PoolOverviewDto>("/v1/me/pool/view", JsonRelaxed);
            OwnedPoolVariants = view?.Groups
                .SelectMany(g => g.Variants)
                .Where(v => v.Quantity > 0)
                .ToList() ?? [];
            if (OwnedPoolVariants.Count == 0 && view?.Stacks is { Count: > 0 } stacks)
            {
                OwnedPoolVariants = stacks
                    .Where(s => s.Quantity > 0)
                    .Select(s => new PoolVariantStackDto(
                        s.ElementId, s.Symbol, s.Dna, s.Phase, s.PhaseLabel,
                        s.Quantity, s.VolumePerUnit, s.LastPrice, s.LineValue,
                        s.PriceRank, s.CatalogSize, s.ChangePercent24h))
                    .ToList();
            }
            OwnedPoolElementIds = OwnedPoolVariants
                .Select(v => v.ElementId)
                .ToHashSet();
        }
        catch
        {
            OwnedPoolElementIds = [];
            OwnedPoolVariants = [];
        }
    }

    public async Task RemoveConnectionAtAsync(int index)
    {
        if (!TryDeserializePlan(out var plan))
            return;
        if (index < 0 || index >= plan.Connections.Count)
            return;

        var connections = plan.Connections.ToList();
        var removed = connections[index];
        connections.RemoveAt(index);
        PlanJson = SerializePlan(new BoardPlanDto(plan.Machines, connections));
        Error = null;
        _snackbar.Show(
            $"Pipe removed: {removed.FromId}.{removed.FromPort} → {removed.ToId}.{removed.ToPort}",
            SnackbarKind.Info);
        await SyncBoardInfoFromEditorAsync();
        if (!IsSelectedBoardRunning())
            await SavePlanQuietAsync();
        else
            SaveHint = "Pipe removed (not saved).";
        NotifyChanged();
    }

    public void RebuildMachineMeta()
    {
        MachineMeta.Clear();
        foreach (var x in StoreItems)
            MachineMeta[x.Type] = x;
        foreach (var x in Connectors)
            MachineMeta[x.Type] = x;
    }

    public async Task LoadMachineStoreAsync()
    {
        try
        {
            var res = await _http.GetFromJsonAsync<MachineStorePayload>("/v1/content/machine-store", JsonRelaxed);
            StoreItems = res?.Store ?? new List<MachineStoreItemDto>();
            Connectors = res?.Connectors ?? new List<MachineStoreItemDto>();
            RebuildMachineMeta();
        }
        catch
        {
            /* silent: store is optional for listing */
        }
    }

    public async Task LoadInventoryAsync()
    {
        try
        {
            var rows = await _http.GetFromJsonAsync<List<PlayerMachineStockDto>>("/v1/me/machine-inventory", JsonRelaxed);
            Inventory = rows ?? new List<PlayerMachineStockDto>();
        }
        catch
        {
            Inventory = new List<PlayerMachineStockDto>();
        }
    }

    public async Task PurchaseAsync(string machineType)
    {
        Busy = true;
        Error = null;
        try
        {
            var res = await _http.PostAsJsonAsync("/v1/me/machine-inventory/purchase", new PurchaseMachineRequest(machineType));
            if (!res.IsSuccessStatusCode)
                Error = await res.Content.ReadAsStringAsync();
            else
            {
                await _wallet.RefreshAsync();
                await LoadInventoryAsync();
                var label = MachineMeta.TryGetValue(machineType, out var meta)
                    ? meta.DisplayName
                    : machineType;
                _snackbar.Show($"Purchased {label}.");
            }
        }
        catch (Exception ex)
        {
            Error = ApiConnectionErrors.Format(ex);
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task PlaceFromStockAsync()
    {
        if (Selected is not { } boardId)
        {
            Error = "Select a board first.";
            return;
        }

        if (!Guid.TryParse(PlaceStockId, out var stockId) || stockId == Guid.Empty)
        {
            Error = "Select an inventory item.";
            return;
        }

        if (string.IsNullOrWhiteSpace(PlaceMachineId))
        {
            Error = "Enter a machine id.";
            return;
        }

        Busy = true;
        Error = null;
        try
        {
            if (TryDeserializePlan(out _))
                await SavePlanQuietAsync();

            var res = await _http.PostAsJsonAsync($"/v1/boards/{boardId}/place-from-stock",
                new PlaceMachineFromStockRequest(stockId, PlaceMachineId.Trim()));
            if (!res.IsSuccessStatusCode)
                Error = await res.Content.ReadAsStringAsync();
            else
            {
                SaveHint = "Machine placed and plan saved.";
                ResetPipeSelection();
                PlaceStockId = "";
                PlaceMachineId = "";
                await LoadInventoryAsync();
                await LoadBoardsAsync();
                await ReloadPlanUiAsync();
                await LoadBoardInfoAsync();
                _snackbar.Show("Machine placed on the board.");
            }
        }
        catch (Exception ex)
        {
            Error = ApiConnectionErrors.Format(ex);
        }
        finally
        {
            Busy = false;
            NotifyChanged();
        }
    }

    public async Task LoadBoardsAsync()
    {
        Busy = true;
        Error = null;
        try
        {
            var res = await _http.GetFromJsonAsync<List<BoardSummaryDto>>("/v1/boards");
            Boards = res ?? new List<BoardSummaryDto>();
            await PrefetchBoardIssuesAsync();
        }
        catch (Exception ex)
        {
            Error = ApiConnectionErrors.Format(ex);
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task CreateBoardAsync()
    {
        Busy = true;
        Error = null;
        try
        {
            var res = await _http.PostAsJsonAsync("/v1/boards", new CreateBoardRequest("New factory"));
            if (!res.IsSuccessStatusCode)
            {
                Error = await res.Content.ReadAsStringAsync();
                return;
            }

            var created = await res.Content.ReadFromJsonAsync<BoardSummaryDto>();
            if (created != null)
                await SelectBoardAsync(created.Id);
            await LoadBoardsAsync();
        }
        catch (Exception ex)
        {
            Error = ApiConnectionErrors.Format(ex);
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task TryRestoreLastBoardAsync()
    {
        var saved = await _storage.GetAsync("fg_last_board_id");
        if (Guid.TryParse(saved, out var id) && Boards.Any(b => b.Id == id))
            await SelectBoardAsync(id);
        else if (Boards.Count > 0 && Selected is null)
            await SelectBoardAsync(Boards[0].Id);
    }

    public async Task SelectBoardAsync(Guid id)
    {
        Selected = id;
        await _storage.SetAsync("fg_last_board_id", id.ToString());
        RenamingBoard = false;
        RenameDraft = "";
        Snapshot = "";
        SaveHint = null;
        if (BoardInfo?.BoardId != id)
        {
            BoardInfo = null;
            LatestKeyframe = null;
        }
        ResetPipeSelection();
        await ReloadPlanUiAsync();
        await LoadBoardInfoAsync();
        await LoadLatestKeyframeAsync(id);
        await LoadPoolOwnershipAsync();
        StartPollingIfRunning();
    }

    public async Task RefreshBoardInfoAsync()
    {
        if (Selected is not { } boardId)
            return;
        if (!TryDeserializePlan(out _))
        {
            Error = "Invalid plan JSON — cannot analyze info.";
            return;
        }

        Busy = true;
        Error = null;
        try
        {
            await SyncBoardInfoFromEditorAsync();
        }
        catch (Exception ex)
        {
            Error = ApiConnectionErrors.Format(ex);
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task LoadBoardInfoAsync()
    {
        if (Selected is not { } boardId)
        {
            BoardInfo = null;
            return;
        }

        if (IsSelectedBoardRunning())
        {
            try
            {
                BoardInfo = await _http.GetFromJsonAsync<BoardInfoDto>($"/v1/boards/{boardId}/info", JsonRelaxed);
                if (BoardInfo != null)
                {
                    CacheBoardIssues(boardId, BoardInfo.Issues);
                    ApplyClientHealthOverrides();
                }
            }
            catch
            {
                BoardInfo = null;
            }
            return;
        }

        await SyncBoardInfoFromEditorAsync();
    }

    /// <summary>In Edit mode, analyze the plan JSON in the editor (matches canvas). Running uses server keyframe.</summary>
    public async Task SyncBoardInfoFromEditorAsync()
    {
        if (Selected is not { } boardId || !TryDeserializePlan(out var plan))
            return;

        try
        {
            var res = await _http.PostAsJsonAsync($"/v1/boards/{boardId}/info/preview", new SavePlanRequest(plan));
            if (res.IsSuccessStatusCode)
            {
                BoardInfo = await res.Content.ReadFromJsonAsync<BoardInfoDto>(JsonRelaxed);
                if (BoardInfo != null)
                {
                    CacheBoardIssues(boardId, BoardInfo.Issues);
                    ApplyClientHealthOverrides();
                }
            }
        }
        catch
        {
            /* keep previous info */
        }
    }

    public void ResetPipeSelection()
    {
        PipeFromId = "";
        PipeFromPort = "";
        PipeToId = "";
        PipeToPort = "";
    }

    public async Task ReloadPlanUiAsync()
    {
        if (Selected is not { } id)
            return;
        Busy = true;
        Error = null;
        try
        {
            var res = await _http.GetAsync($"/v1/boards/{id}/plan");
            if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                PlanJson = DefaultPlan();
                return;
            }

            if (!res.IsSuccessStatusCode)
            {
                Error = await res.Content.ReadAsStringAsync();
                return;
            }

            var plan = await res.Content.ReadFromJsonAsync<BoardPlanDto>(JsonRelaxed);
            if (plan != null)
                PlanJson = SerializePlan(PlanMachineLayout.NormalizeLayout(plan));
        }
        catch (Exception ex)
        {
            Error = ApiConnectionErrors.Format(ex);
        }
        finally
        {
            Busy = false;
            OnPlaceStockChanged();
            await LoadBoardInfoAsync();
            NotifyChanged();
        }
    }

    public void OnPipeEndpointChanged()
    {
        if (!GetOutputPortChoices(PipeFromId).Any(p => p.Name == PipeFromPort))
            PipeFromPort = "";
        if (!GetInputPortChoices(PipeToId).Any(p => p.Name == PipeToPort))
            PipeToPort = "";
        NotifyChanged();
    }

    public Task OnConfirmDialogYesAsync()
    {
        ConfirmVisible = false;
        _confirmTcs?.TrySetResult(true);
        _confirmTcs = null;
        NotifyChanged();
        return Task.CompletedTask;
    }

    public Task OnConfirmDialogNoAsync()
    {
        ConfirmVisible = false;
        _confirmTcs?.TrySetResult(false);
        _confirmTcs = null;
        NotifyChanged();
        return Task.CompletedTask;
    }

    public async Task<bool> AskReplaceConnectionAsync(string message)
    {
        ConfirmMessage = message;
        ConfirmVisible = true;
        _confirmTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        NotifyChanged();
        return await _confirmTcs.Task;
    }

    private static string FormatConnection(ConnectionDto c) =>
        $"{c.FromId}.{c.FromPort} → {c.ToId}.{c.ToPort}";

    private static string BuildReplaceConfirmMessage(
        string fromId,
        string fromPort,
        string toId,
        string toPort,
        ConnectionDto? fromConflict,
        ConnectionDto? toConflict)
    {
        var removals = new List<string>();
        if (fromConflict is { } fc)
            removals.Add(FormatConnection(fc));
        if (toConflict is { } tc && !removals.Any(r => r == FormatConnection(tc)))
            removals.Add(FormatConnection(tc));

        var removedText = removals.Count switch
        {
            0 => "",
            1 => removals[0],
            _ => string.Join(" and ", removals)
        };

        return
            $"Remove connection {removedText} to connect {fromId}.{fromPort} to {toId}.{toPort} instead?";
    }

    public async Task<bool> TryApplyConnectionAsync(string fromId, string fromPort, string toId, string toPort)
    {
        if (!TryDeserializePlan(out var plan))
            return false;

        if (string.Equals(fromId, toId, StringComparison.Ordinal))
        {
            _snackbar.Show("Cannot connect a machine to itself.", SnackbarKind.Info);
            return false;
        }

        var proposed = new ConnectionDto(fromId, fromPort, toId, toPort);
        if (plan.Connections.Any(c =>
                c.FromId == proposed.FromId && c.FromPort == proposed.FromPort &&
                c.ToId == proposed.ToId && c.ToPort == proposed.ToPort))
        {
            _snackbar.Show("Pipe is already connected.", SnackbarKind.Info);
            return false;
        }

        var connections = plan.Connections.ToList();
        var fromConflict = connections.FirstOrDefault(c => c.FromId == fromId && c.FromPort == fromPort);
        var toConflict = connections.FirstOrDefault(c => c.ToId == toId && c.ToPort == toPort);

        if (fromConflict is not null || toConflict is not null)
        {
            var message = BuildReplaceConfirmMessage(fromId, fromPort, toId, toPort, fromConflict, toConflict);
            if (!await AskReplaceConnectionAsync(message))
                return false;
        }

        connections.RemoveAll(c =>
            (c.FromId == fromId && c.FromPort == fromPort) ||
            (c.ToId == toId && c.ToPort == toPort));
        connections.Add(proposed);

        PlanJson = SerializePlan(new BoardPlanDto(plan.Machines, connections));
        Error = null;
        if (!IsSelectedBoardRunning())
            await SavePlanQuietAsync();
        else
            SaveHint = "Pipe changed (not saved).";
        _snackbar.Show(
            IsSelectedBoardRunning() ? "Pipe connected — stop the factory and save the plan." : "Pipe connected.",
            SnackbarKind.Info);
        await SyncBoardInfoFromEditorAsync();
        NotifyChanged();
        return true;
    }

    public void OnPlaceStockChanged()
    {
        if (!Guid.TryParse(PlaceStockId, out var stockId) || stockId == Guid.Empty)
        {
            PlaceMachineId = "";
            return;
        }

        var row = Inventory.FirstOrDefault(r => r.Id == stockId);
        PlaceMachineId = row == null ? "" : SuggestNextMachineId(row.MachineType);
    }

    /// <summary>Next free id: lowercase type + number, e.g. mixer1, mixer2.</summary>
    public string SuggestNextMachineId(string machineType)
    {
        var prefix = machineType.Trim().ToLowerInvariant();
        if (prefix.Length == 0)
            return "";

        var max = 0;
        foreach (var m in GetPlanMachines())
        {
            if (!m.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var suffix = m.Id[prefix.Length..];
            if (suffix.Length > 0 && int.TryParse(suffix, out var n))
                max = Math.Max(max, n);
        }

        return $"{prefix}{max + 1}";
    }

    public bool TryDeserializePlan(out BoardPlanDto plan)
    {
        try
        {
            plan = JsonSerializer.Deserialize<BoardPlanDto>(PlanJson, PlanJsonOptions)
                   ?? new BoardPlanDto(Array.Empty<MachineDto>(), Array.Empty<ConnectionDto>());
            return true;
        }
        catch
        {
            plan = new BoardPlanDto(Array.Empty<MachineDto>(), Array.Empty<ConnectionDto>());
            return false;
        }
    }

    private static string SerializePlan(BoardPlanDto plan) =>
        JsonSerializer.Serialize(plan, PlanJsonOptions);

    public IReadOnlyList<MachineDto> GetPlanMachines() =>
        TryDeserializePlan(out var plan) ? plan.Machines : Array.Empty<MachineDto>();

    public IReadOnlyList<MachineDto> GetSeaportOutMachines() =>
        GetPlanMachines().Where(m =>
            m.Type.Equals("SeaportConnector", StringComparison.OrdinalIgnoreCase)
            || m.Type.Equals("SeaportIn", StringComparison.OrdinalIgnoreCase)).ToList();

    public MachineDto? GetSettingsMachine()
    {
        if (string.IsNullOrWhiteSpace(SettingsMachineId))
            return null;
        return GetPlanMachines().FirstOrDefault(m =>
            m.Id.Equals(SettingsMachineId, StringComparison.Ordinal));
    }

    public Task OnCanvasMachineSelectedAsync(string? machineId)
    {
        SettingsMachineId = machineId ?? "";
        NotifyChanged();
        return Task.CompletedTask;
    }

    public async Task OnMachineSettingsChangedAsync(MachineDto updated)
    {
        if (!TryDeserializePlan(out var plan))
            return;

        var machines = plan.Machines
            .Select(m => m.Id == updated.Id ? updated : m)
            .ToList();
        PlanJson = SerializePlan(new BoardPlanDto(machines, plan.Connections));
        SettingsMachineId = updated.Id;
        SaveHint = $"Settings for {updated.Id} updated (not saved).";
        await SyncBoardInfoFromEditorAsync();
        NotifyChanged();
    }

    public async Task EnsureElementsAsync()
    {
        if (Elements.Count > 0)
            return;
        try
        {
            var items = await _http.GetFromJsonAsync<List<ElementContentItem>>("/v1/content/elements?locale=en", JsonRelaxed);
            Elements = items ?? [];
        }
        catch
        {
            Elements = [];
        }
    }

    public IReadOnlyList<ConnectionDto> GetPlanConnections() =>
        TryDeserializePlan(out var plan) ? plan.Connections : Array.Empty<ConnectionDto>();

    public readonly record struct PortChoice(string Name, string Label);

    public IReadOnlyList<PortChoice> GetOutputPortChoices(string machineId)
    {
        if (string.IsNullOrEmpty(machineId))
            return Array.Empty<PortChoice>();
        var m = GetPlanMachines().FirstOrDefault(x => x.Id == machineId);
        if (m is null)
            return Array.Empty<PortChoice>();
        if (!MachineMeta.TryGetValue(m.Type, out var meta))
            return Array.Empty<PortChoice>();

        return meta.Ports
            .Where(p => p.Direction == "out")
            .Select(p =>
            {
                var conn = GetPlanConnections()
                    .FirstOrDefault(c => c.FromId == machineId && c.FromPort == p.Name);
                var label = conn is null
                    ? p.Name
                    : $"{p.Name} (→ {conn.ToId}.{conn.ToPort})";
                return new PortChoice(p.Name, label);
            })
            .ToList();
    }

    public IReadOnlyList<PortChoice> GetInputPortChoices(string machineId)
    {
        if (string.IsNullOrEmpty(machineId))
            return Array.Empty<PortChoice>();
        var m = GetPlanMachines().FirstOrDefault(x => x.Id == machineId);
        if (m is null)
            return Array.Empty<PortChoice>();
        if (!MachineMeta.TryGetValue(m.Type, out var meta))
            return Array.Empty<PortChoice>();

        return meta.Ports
            .Where(p => p.Direction == "in")
            .Select(p =>
            {
                var conn = GetPlanConnections()
                    .FirstOrDefault(c => c.ToId == machineId && c.ToPort == p.Name);
                var label = conn is null
                    ? p.Name
                    : $"{p.Name} (← {conn.FromId}.{conn.FromPort})";
                return new PortChoice(p.Name, label);
            })
            .ToList();
    }

    public async Task AddPipeToPlanAsync()
    {
        if (string.IsNullOrEmpty(PipeFromId) || string.IsNullOrEmpty(PipeToId) ||
            string.IsNullOrEmpty(PipeFromPort) || string.IsNullOrEmpty(PipeToPort))
        {
            Error = "Select machines and ports for the full pipe.";
            return;
        }

        if (!await TryApplyConnectionAsync(PipeFromId, PipeFromPort, PipeToId, PipeToPort))
            return;

        Error = null;
    }

    public async Task SavePlanAsync()
    {
        if (Selected is not { } id)
            return;
        Busy = true;
        SaveHint = null;
        Error = null;
        try
        {
            if (!TryDeserializePlan(out var plan))
            {
                Error = "Invalid plan JSON.";
                return;
            }

            await SavePlanCoreAsync(id, plan, showConflictHint: true);
            await LoadBoardsAsync();
        }
        catch (Exception ex)
        {
            Error = ApiConnectionErrors.Format(ex);
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task SavePlanQuietAsync()
    {
        if (Selected is not { } id || !TryDeserializePlan(out var plan))
            return;

        Error = null;
        try
        {
            await SavePlanCoreAsync(id, plan, showConflictHint: false);
        }
        catch (Exception ex)
        {
            Error = ApiConnectionErrors.Format(ex);
        }
    }

    public async Task SavePlanCoreAsync(Guid boardId, BoardPlanDto plan, bool showConflictHint)
    {
        var normalized = PlanMachineLayout.NormalizeLayout(plan);
        PlanJson = SerializePlan(normalized);
        var body = new SavePlanRequest(normalized);

        if (await _storage.IsOnlineAsync())
        {
            var res = await _http.PutAsJsonAsync($"/v1/boards/{boardId}/plan", body);
            if (res.StatusCode == System.Net.HttpStatusCode.Conflict && showConflictHint)
            {
                SaveHint = "Conflict with server (409). Open CLI and use merge flow: compare revision manually or overwrite via a new save.";
            }
            else if (!res.IsSuccessStatusCode)
            {
                Error = await res.Content.ReadAsStringAsync();
            }
            else
            {
                SaveHint = showConflictHint ? "Saved on server." : "Layout saved.";
                await LoadBoardInfoAsync();
            }
        }
        else
        {
            await _queue.EnqueueAsync("PUT", $"/v1/boards/{boardId}/plan", body);
            SaveHint = showConflictHint
                ? "Offline: save queued. Sync via CLI when online."
                : "Layout queued (offline).";
        }
    }

    public async Task StartAsync()
    {
        if (Selected is not { } id)
            return;
        Busy = true;
        try
        {
            var res = await _http.PostAsync($"/v1/boards/{id}/start", null);
            if (!res.IsSuccessStatusCode)
                Error = await res.Content.ReadAsStringAsync();
            else
            {
                _snackbar.Show("Factory starting...");
                await LoadBoardsAsync();
                await LoadBoardInfoAsync();
                await LoadLatestKeyframeAsync(id);
                StartPollingIfRunning();
            }
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task StopAsync()
    {
        if (Selected is not { } id)
            return;
        Busy = true;
        try
        {
            var res = await _http.PostAsync($"/v1/boards/{id}/stop", null);
            if (!res.IsSuccessStatusCode)
                Error = await res.Content.ReadAsStringAsync();
            else
            {
                _snackbar.Show("Factory stopped — you can edit the plan.");
                await LoadBoardsAsync();
                StartPollingIfRunning();
            }
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task SnapshotAsync()
    {
        if (Selected is not { } id)
            return;
        Busy = true;
        try
        {
            var res = await _http.GetAsync($"/v1/boards/{id}/snapshot");
            Snapshot = await res.Content.ReadAsStringAsync();
        }
        finally
        {
            Busy = false;
        }
    }

}
