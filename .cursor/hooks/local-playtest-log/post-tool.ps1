# postToolUse — timeline of agent tool calls for bug correlation.
. "$PSScriptRoot/log-common.ps1"

try {
    $inputJson = Read-HookInput
    if ($null -eq $inputJson) {
        exit 0
    }

    $sessionDir = Ensure-SessionDir
    $ts = (Get-Date).ToUniversalTime().ToString('o')

    $toolName = $inputJson.tool_name
    if ($null -eq $toolName -and $inputJson.PSObject.Properties['toolName']) {
        $toolName = $inputJson.toolName
    }

    $entry = [ordered]@{
        tsUtc = $ts
        event = 'postToolUse'
        tool = $toolName
    }

    if ($inputJson.PSObject.Properties['tool_input']) {
        $entry.toolInput = $inputJson.tool_input
    }
    elseif ($inputJson.PSObject.Properties['toolInput']) {
        $entry.toolInput = $inputJson.toolInput
    }

    if ($inputJson.PSObject.Properties['duration_ms']) {
        $entry.durationMs = $inputJson.duration_ms
    }

    Write-NdjsonLine -FilePath (Join-Path $sessionDir 'agent-activity.ndjson') -Object $entry

    $toolText = [string]$toolName
    if ($toolText -match '^MCP:' -or $toolText -eq 'Shell') {
        Invoke-LogSnapshot -SessionDir $sessionDir -Reason 'post-tool' | Out-Null
    }
}
catch {
    # Fail open
}

exit 0
