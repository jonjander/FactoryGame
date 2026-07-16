# sessionStart — init local playtest log session for API/backend/UI correlation.
. "$PSScriptRoot/log-common.ps1"

try {
    $logRoot = Get-LocalLogRoot
    New-Item -ItemType Directory -Force -Path (Join-Path $logRoot 'sessions') | Out-Null

    $sessionDir = Ensure-SessionDir

    $entry = [ordered]@{
        tsUtc = (Get-Date).ToUniversalTime().ToString('o')
        event = 'sessionStart'
        sessionDir = $sessionDir
    }

    Write-NdjsonLine -FilePath (Join-Path $sessionDir 'agent-activity.ndjson') -Object $entry

    $readme = @(
        '# FactoryGame local playtest session'
        ''
        "Session: $(Split-Path $sessionDir -Leaf)"
        "Started (UTC): $($entry.tsUtc)"
        ''
        '## Files'
        '- api-recent.log — API + backend (GET /diagnostics/recent-logs)'
        '- ui-client.log — Blazor/browser (GET /diagnostics/client-logs)'
        '- shell.log — dotnet run / shell output from hooks'
        '- agent-activity.ndjson — tool timeline for correlation'
        '- snapshots.ndjson — snapshot metadata'
        ''
        '## Refresh snapshots'
        'powershell -NoProfile -ExecutionPolicy Bypass -File .cursor/hooks/local-playtest-log/snapshot-local-logs.ps1'
    ) -join "`n"

    Set-Content -Path (Join-Path $sessionDir 'README.md') -Value $readme -Encoding utf8
}
catch {
    # Fail open — logging must never block the agent.
}

exit 0
