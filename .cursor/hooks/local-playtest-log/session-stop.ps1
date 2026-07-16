# stop — final snapshot of API/backend and UI client logs for the session.
. "$PSScriptRoot/log-common.ps1"

try {
    $sessionDir = Get-CurrentSessionDir
    if (-not $sessionDir) {
        exit 0
    }

    $meta = Invoke-LogSnapshot -SessionDir $sessionDir -Reason 'session-stop'

    Write-NdjsonLine -FilePath (Join-Path $sessionDir 'agent-activity.ndjson') -Object ([ordered]@{
        tsUtc = (Get-Date).ToUniversalTime().ToString('o')
        event = 'sessionStop'
        snapshot = $meta
    })

    $sessionId = Split-Path $sessionDir -Leaf
    $followup = @(
        "Local playtest logs saved under .local-logs/sessions/$sessionId."
        'Read api-recent.log (API/backend), ui-client.log (UI), shell.log, and agent-activity.ndjson to correlate with reported bugs.'
        'Refresh: powershell -NoProfile -ExecutionPolicy Bypass -File .cursor/hooks/local-playtest-log/snapshot-local-logs.ps1'
    ) -join ' '

    @{ followup_message = $followup } | ConvertTo-Json -Compress | Write-Output
}
catch {
    # Fail open — no follow-up if snapshot fails.
}

exit 0
