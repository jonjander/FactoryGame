# Shared helpers for FactoryGame local playtest log hooks.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
}

function Get-LocalLogRoot {
    return Join-Path (Get-RepoRoot) '.local-logs'
}

function Get-ConfigPath {
    return Join-Path $PSScriptRoot 'config.json'
}

function Get-PlaytestConfig {
    $path = Get-ConfigPath
    if (-not (Test-Path $path)) {
        return [pscustomobject]@{
            apiBaseUrls = @(
                'https://localhost:7145',
                'http://localhost:5176'
            )
            clientLogBaseUrls = @(
                'https://localhost:7145',
                'http://localhost:5176'
            )
        }
    }

    return Get-Content -Raw -Path $path | ConvertFrom-Json
}

function Get-CurrentSessionDir {
    $pointer = Join-Path (Get-LocalLogRoot) 'current-session.txt'
    if (-not (Test-Path $pointer)) {
        return $null
    }

    $content = Get-Content -Raw -Path $pointer -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($content)) {
        return $null
    }

    $sessionPath = $content.Trim()
    if (-not (Test-Path $sessionPath)) {
        return $null
    }

    return $sessionPath
}

function Ensure-SessionDir {
    $existing = Get-CurrentSessionDir
    if ($existing) {
        return $existing
    }

    $sessionId = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
    $sessionDir = Join-Path (Get-LocalLogRoot) "sessions/$sessionId"
    New-Item -ItemType Directory -Force -Path $sessionDir | Out-Null
    Set-Content -Path (Join-Path (Get-LocalLogRoot) 'current-session.txt') -Value $sessionDir -NoNewline

    $meta = [ordered]@{
        sessionId = $sessionId
        startedUtc = (Get-Date).ToUniversalTime().ToString('o')
        repoRoot = Get-RepoRoot
        purpose = 'local-playtest-correlation'
    }
    ($meta | ConvertTo-Json -Depth 4) | Set-Content -Path (Join-Path $sessionDir 'session.json') -Encoding utf8

    return $sessionDir
}

function Write-NdjsonLine {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][hashtable]$Object
    )

    $line = ($Object | ConvertTo-Json -Compress -Depth 8)
    Add-Content -Path $FilePath -Value $line -Encoding utf8
}

function Read-HookInput {
    if (-not [Console]::IsInputRedirected) {
        return $null
    }

    $reader = [Console]::In
    if ($reader.Peek() -lt 0) {
        return $null
    }

    $raw = $reader.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return $null
    }

    try {
        return $raw | ConvertFrom-Json
    }
    catch {
        return [pscustomobject]@{ raw = $raw }
    }
}

function Invoke-LogSnapshot {
    param(
        [string]$SessionDir = (Ensure-SessionDir),
        [string]$Reason = 'manual'
    )

    $config = Get-PlaytestConfig
    $timestamp = (Get-Date).ToUniversalTime().ToString('o')
    $snapshotMeta = [ordered]@{
        capturedUtc = $timestamp
        reason = $Reason
        api = @()
        ui = @()
    }

    foreach ($baseUrl in $config.apiBaseUrls) {
        $baseUrl = $baseUrl.TrimEnd('/')
        $target = Join-Path $SessionDir 'api-recent.log'
        try {
            $response = Invoke-WebRequest -Uri "$baseUrl/diagnostics/recent-logs" -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                $header = "# snapshot $timestamp from $baseUrl/diagnostics/recent-logs`n"
                Set-Content -Path $target -Value ($header + $response.Content) -Encoding utf8
                $snapshotMeta.api += [ordered]@{
                    baseUrl = $baseUrl
                    endpoint = 'recent-logs'
                    status = $response.StatusCode
                    bytes = $response.RawContentLength
                }
                break
            }
        }
        catch {
            $snapshotMeta.api += [ordered]@{
                baseUrl = $baseUrl
                endpoint = 'recent-logs'
                error = $_.Exception.Message
            }
        }
    }

    foreach ($baseUrl in $config.clientLogBaseUrls) {
        $baseUrl = $baseUrl.TrimEnd('/')
        $target = Join-Path $SessionDir 'ui-client.log'
        try {
            $response = Invoke-WebRequest -Uri "$baseUrl/diagnostics/client-logs" -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                $header = "# snapshot $timestamp from $baseUrl/diagnostics/client-logs`n"
                Set-Content -Path $target -Value ($header + $response.Content) -Encoding utf8
                $snapshotMeta.ui += [ordered]@{
                    baseUrl = $baseUrl
                    endpoint = 'client-logs'
                    status = $response.StatusCode
                    bytes = $response.RawContentLength
                }
                break
            }
        }
        catch {
            $snapshotMeta.ui += [ordered]@{
                baseUrl = $baseUrl
                endpoint = 'client-logs'
                error = $_.Exception.Message
            }
        }
    }

    Write-NdjsonLine -FilePath (Join-Path $SessionDir 'snapshots.ndjson') -Object $snapshotMeta
    return $snapshotMeta
}
