# Standalone snapshot — run anytime during local playtest to refresh API/UI log files.
param(
    [string]$Reason = 'manual'
)

. "$PSScriptRoot/log-common.ps1"

try {
    $sessionDir = Ensure-SessionDir
    $meta = Invoke-LogSnapshot -SessionDir $sessionDir -Reason $Reason
    Write-Output "Snapshot written to $sessionDir"
    $meta | ConvertTo-Json -Depth 6 | Write-Output
    exit 0
}
catch {
    Write-Error $_
    exit 1
}
