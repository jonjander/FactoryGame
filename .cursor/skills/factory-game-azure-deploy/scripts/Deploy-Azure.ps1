#Requires -Version 5.1
<#
.SYNOPSIS
  Publish FactoryGame.Api (Release) and Zip Deploy to Azure App Service via PublishSettings.

.DESCRIPTION
  Credentials come from a local PublishSettings file (never committed).
  Default path: <repo>/.local/FactoryGame.PublishSettings
  Override: -PublishSettingsPath or env FACTORYGAME_PUBLISH_SETTINGS

.PARAMETER PublishSettingsPath
  Path to Azure *.PublishSettings XML.

.PARAMETER Configuration
  MSBuild configuration (default Release).

.PARAMETER SkipBuild
  Reuse existing publish output under -OutputDir (must already exist).

.PARAMETER SkipSmoke
  Do not GET /health after deploy.

.PARAMETER OutputDir
  Folder for dotnet publish output (default <repo>/_publish_azure).
#>
[CmdletBinding()]
param(
    [string] $PublishSettingsPath = "",
    [string] $Configuration = "Release",
    [switch] $SkipBuild,
    [switch] $SkipSmoke,
    [string] $OutputDir = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    $here = $PSScriptRoot
    # .../skills/factory-game-azure-deploy/scripts -> repo root is 4 levels up
    return (Resolve-Path (Join-Path $here "..\..\..\..")).Path
}

function Resolve-PublishSettingsPath {
    param([string] $Explicit, [string] $RepoRoot)
    if ($Explicit) { return (Resolve-Path $Explicit).Path }
    if ($env:FACTORYGAME_PUBLISH_SETTINGS) {
        return (Resolve-Path $env:FACTORYGAME_PUBLISH_SETTINGS).Path
    }
    $default = Join-Path $RepoRoot ".local\FactoryGame.PublishSettings"
    if (-not (Test-Path $default)) {
        throw @"
PublishSettings not found at: $default

Place Azure PublishSettings at that path (gitignored), or pass -PublishSettingsPath,
or set env FACTORYGAME_PUBLISH_SETTINGS.
Download from Azure Portal -> App Service FactoryGame -> Get publish profile.
"@
    }
    return (Resolve-Path $default).Path
}

function Get-ZipDeployProfile {
    param([string] $Path)
    [xml] $xml = Get-Content -LiteralPath $Path -Raw
    $profiles = @($xml.publishData.publishProfile)
    $zip = $profiles | Where-Object { $_.publishMethod -eq "ZipDeploy" } | Select-Object -First 1
    if (-not $zip) {
        $zip = $profiles | Where-Object { $_.publishMethod -eq "MSDeploy" } | Select-Object -First 1
    }
    if (-not $zip) {
        throw "No ZipDeploy or MSDeploy profile in $Path"
    }
    return $zip
}

function New-BasicAuthHeader {
    param([string] $UserName, [string] $Password)
    $pair = "{0}:{1}" -f $UserName, $Password
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($pair)
    $token = [Convert]::ToBase64String($bytes)
    return "Basic $token"
}

function Publish-Api {
    param([string] $RepoRoot, [string] $OutDir, [string] $Config)
    $csproj = Join-Path $RepoRoot "src\FactoryGame.Api\FactoryGame.Api.csproj"
    if (-not (Test-Path $csproj)) { throw "Missing project: $csproj" }
    if (Test-Path $OutDir) {
        Remove-Item -LiteralPath $OutDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $OutDir | Out-Null
    Write-Host "dotnet publish $csproj -c $Config -o $OutDir"
    & dotnet publish $csproj -c $Config -o $OutDir --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit $LASTEXITCODE" }
}

function New-DeployZip {
    param([string] $SourceDir, [string] $ZipPath)
    # Linux Kudu rsync fails if zip entries use Windows backslashes (EINVAL on paths like wwwroot\_framework\...).
    if (Test-Path $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $sourceFull = (Resolve-Path -LiteralPath $SourceDir).Path.TrimEnd('\', '/')
    $zip = [System.IO.Compression.ZipFile]::Open(
        $ZipPath,
        [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        Get-ChildItem -LiteralPath $sourceFull -Recurse -File -Force | ForEach-Object {
            $rel = $_.FullName.Substring($sourceFull.Length).TrimStart('\', '/')
            $entryName = $rel.Replace('\', '/')
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip,
                $_.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal)
        }
    } finally {
        $zip.Dispose()
    }
}

function Clear-AzureWwwrootForRedeploy {
    param(
        [string] $ScmHost,
        [string] $UserName,
        [string] $Password
    )
    # Wipe publish root so leftover Windows-backslash path names cannot break Linux rsync.
    $auth = New-BasicAuthHeader -UserName $UserName -Password $Password
    $body = @{
        command = 'rm -rf /home/site/wwwroot/* ; ls -la /home/site/wwwroot'
        dir     = '/home/site/wwwroot'
    } | ConvertTo-Json
    try {
        $r = Invoke-RestMethod -Uri "https://$ScmHost/api/command" `
            -Method Post `
            -Headers @{ Authorization = $auth; "Content-Type" = "application/json" } `
            -Body $body
        Write-Host "Cleared /home/site/wwwroot for clean Zip Deploy."
        if ($r.Output) { Write-Host $r.Output }
        if ($r.Error) { Write-Host $r.Error }
    } catch {
        Write-Host "wwwroot clear skipped: $($_.Exception.Message)"
    }
}

function Invoke-ZipDeploy {
    param(
        [string] $ScmHost,
        [string] $UserName,
        [string] $Password,
        [string] $ZipPath
    )
    $uri = "https://$ScmHost/api/zipdeploy?isAsync=true"
    $auth = New-BasicAuthHeader -UserName $UserName -Password $Password
    Write-Host "Zip Deploy -> $uri"
    $response = Invoke-WebRequest -Uri $uri -Method Post -InFile $ZipPath `
        -Headers @{ Authorization = $auth } `
        -ContentType "application/octet-stream" `
        -UseBasicParsing
    $location = $response.Headers["Location"]
    if ($location -is [array]) { $location = $location[0] }
    if (-not $location) {
        # Sync deploy may return 200 with no Location
        Write-Host "Zip Deploy accepted (HTTP $($response.StatusCode))."
        return
    }
    Write-Host "Polling deploy status: $location"
    $deadline = (Get-Date).AddMinutes(15)
    do {
        Start-Sleep -Seconds 5
        $status = Invoke-RestMethod -Uri $location -Headers @{ Authorization = $auth }
        $complete = [bool]$status.complete
        $progress = $status.progress
        Write-Host ("  complete={0} progress={1}" -f $complete, $progress)
        if ($complete) {
            if ($status.status -eq 4 -or $status.status -eq "4") {
                # 4 = Failed in Kudu
                throw "Zip Deploy failed: $($status.status_text) $($status.log_url)"
            }
            Write-Host "Zip Deploy finished: $($status.status_text)"
            return
        }
    } while ((Get-Date) -lt $deadline)
    throw "Zip Deploy timed out after 15 minutes. Status: $location"
}

function Invoke-HealthSmoke {
    param([string] $AppUrl)
    $health = ($AppUrl.TrimEnd("/") + "/health")
    Write-Host "Smoke: GET $health"
    # SQL migrate + cold start can exceed 3 minutes on first boot.
    $deadline = (Get-Date).AddMinutes(8)
    do {
        try {
            $r = Invoke-WebRequest -Uri $health -UseBasicParsing -TimeoutSec 45
            if ($r.StatusCode -eq 200 -and $r.Content -match "Healthy") {
                Write-Host "Health OK."
                return
            }
            Write-Host "  unexpected health body: $($r.Content)"
        } catch {
            Write-Host "  waiting for app... $($_.Exception.Message)"
        }
        Start-Sleep -Seconds 5
    } while ((Get-Date) -lt $deadline)
    throw "Health check failed for $health"
}

function Set-AzureSqlAppSettingViaKudu {
    param(
        [string] $ScmHost,
        [string] $UserName,
        [string] $Password,
        [string] $ConnectionString
    )
    if (-not $ConnectionString) { return }
    $auth = New-BasicAuthHeader -UserName $UserName -Password $Password
    $uri = "https://$ScmHost/api/settings"
    $body = @{ ConnectionStrings__DefaultConnection = $ConnectionString } | ConvertTo-Json
    try {
        Invoke-RestMethod -Uri $uri -Method Post -Headers @{ Authorization = $auth; "Content-Type" = "application/json" } -Body $body | Out-Null
        Write-Host "Posted ConnectionStrings__DefaultConnection to Kudu settings (best-effort)."
    } catch {
        Write-Host "Kudu settings post skipped: $($_.Exception.Message)"
    }
}

# --- main ---
$repoRoot = Get-RepoRoot
if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot "_publish_azure"
}
$settingsPath = Resolve-PublishSettingsPath -Explicit $PublishSettingsPath -RepoRoot $repoRoot
$profile = Get-ZipDeployProfile -Path $settingsPath
$userName = [string]$profile.userName
$password = [string]$profile.userPWD
$scmHost = ([string]$profile.publishUrl) -replace ':443$', ''
$appUrl = [string]$profile.destinationAppUrl

Write-Host "Repo: $repoRoot"
Write-Host "PublishSettings: $settingsPath"
Write-Host "App: $appUrl"
Write-Host "SCM: $scmHost"

if (-not $SkipBuild) {
    Publish-Api -RepoRoot $repoRoot -OutDir $OutputDir -Config $Configuration
} elseif (-not (Test-Path $OutputDir)) {
    throw "SkipBuild set but OutputDir missing: $OutputDir"
}

# Safety net: Oryx/Linux falls back to hostingstart when multiple runtimeconfigs exist.
$webRuntimeConfig = Join-Path $OutputDir "FactoryGame.Web.runtimeconfig.json"
if (Test-Path $webRuntimeConfig) {
    Remove-Item -LiteralPath $webRuntimeConfig -Force
    Write-Host "Removed FactoryGame.Web.runtimeconfig.json from publish output (Azure startup)."
}

# Optional Azure SQL connection (gitignored). Prefer Portal App Setting long-term.
$connPath = Join-Path $repoRoot ".local\azure-sql-connection.txt"
$connFromEnv = $env:FACTORYGAME_SQL_CONNECTION
$sqlConn = $null
if ($connFromEnv -and $connFromEnv.Trim().Length -gt 0) {
    $sqlConn = $connFromEnv.Trim()
    Write-Host "Using SQL connection from FACTORYGAME_SQL_CONNECTION."
} elseif (Test-Path $connPath) {
    $sqlConn = (Get-Content -LiteralPath $connPath -Raw).Trim()
    if ($sqlConn) { Write-Host "Using SQL connection from .local/azure-sql-connection.txt." }
}
if ($sqlConn) {
    $prodPath = Join-Path $OutputDir "appsettings.Production.json"
    $prodObj = [ordered]@{
        ConnectionStrings = [ordered]@{ DefaultConnection = $sqlConn }
    }
    $prodObj | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $prodPath -Encoding utf8
    Write-Host "Wrote appsettings.Production.json with Azure SQL connection."
}

$zipPath = Join-Path $repoRoot "_publish_azure.zip"
try {
    New-DeployZip -SourceDir $OutputDir -ZipPath $zipPath
    $sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Host "Zip size: ${sizeMb} MB"
    Clear-AzureWwwrootForRedeploy -ScmHost $scmHost -UserName $userName -Password $password
    Invoke-ZipDeploy -ScmHost $scmHost -UserName $userName -Password $password -ZipPath $zipPath
} finally {
    if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue }
}

if ($sqlConn) {
    Set-AzureSqlAppSettingViaKudu -ScmHost $scmHost -UserName $userName -Password $password -ConnectionString $sqlConn
}

if (-not $SkipSmoke) {
    Invoke-HealthSmoke -AppUrl $appUrl
}

Write-Host "Azure deploy complete: $appUrl"
