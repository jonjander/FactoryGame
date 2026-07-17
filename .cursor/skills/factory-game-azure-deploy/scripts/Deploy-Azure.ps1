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
    if (Test-Path $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $SourceDir,
        $ZipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false
    )
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
    $deadline = (Get-Date).AddMinutes(3)
    do {
        try {
            $r = Invoke-WebRequest -Uri $health -UseBasicParsing -TimeoutSec 30
            if ($r.StatusCode -eq 200 -and $r.Content -match "Healthy") {
                Write-Host "Health OK."
                return
            }
        } catch {
            Write-Host "  waiting for app... $($_.Exception.Message)"
        }
        Start-Sleep -Seconds 5
    } while ((Get-Date) -lt $deadline)
    throw "Health check failed for $health"
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

$zipPath = Join-Path $repoRoot "_publish_azure.zip"
try {
    New-DeployZip -SourceDir $OutputDir -ZipPath $zipPath
    $sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Host "Zip size: ${sizeMb} MB"
    Invoke-ZipDeploy -ScmHost $scmHost -UserName $userName -Password $password -ZipPath $zipPath
} finally {
    if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue }
}

if (-not $SkipSmoke) {
    Invoke-HealthSmoke -AppUrl $appUrl
}

Write-Host "Azure deploy complete: $appUrl"
