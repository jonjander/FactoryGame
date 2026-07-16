# afterShellExecution — append shell output (dotnet run, curl, etc.) for playtest correlation.
. "$PSScriptRoot/log-common.ps1"

try {
    $inputJson = Read-HookInput
    if ($null -eq $inputJson) {
        exit 0
    }

    $sessionDir = Ensure-SessionDir
    $shellLog = Join-Path $sessionDir 'shell.log'
    $ts = (Get-Date).ToUniversalTime().ToString('o')

    $command = $inputJson.command
    if ($null -eq $command -and $inputJson.PSObject.Properties['command_line']) {
        $command = $inputJson.command_line
    }

    $output = $inputJson.output
    $exitCode = $inputJson.exit_code
    if ($null -eq $exitCode -and $inputJson.PSObject.Properties['exitCode']) {
        $exitCode = $inputJson.exitCode
    }

    $cwd = $inputJson.cwd
    if ($null -eq $cwd -and $inputJson.PSObject.Properties['working_directory']) {
        $cwd = $inputJson.working_directory
    }

    $block = @(
        "===== $ts ====="
        "cwd: $cwd"
        "exit: $exitCode"
        "command: $command"
        '--- output ---'
        $output
        ''
    ) -join "`n"

    Add-Content -Path $shellLog -Value $block -Encoding utf8

    Write-NdjsonLine -FilePath (Join-Path $sessionDir 'agent-activity.ndjson') -Object ([ordered]@{
        tsUtc = $ts
        event = 'afterShellExecution'
        command = $command
        exitCode = $exitCode
        cwd = $cwd
    })

    $commandText = [string]$command
    if ($commandText -match 'dotnet\s+(run|watch|test)' -or $commandText -match 'curl|Invoke-WebRequest|iwr ') {
        Invoke-LogSnapshot -SessionDir $sessionDir -Reason 'after-shell' | Out-Null
    }
}
catch {
    # Fail open
}

exit 0
