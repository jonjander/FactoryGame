<#
.SYNOPSIS
  Lokal zip-deploy av FactoryGame.Api till Azure App Service (Linux) via Azure CLI.

.DESCRIPTION
  Kör dotnet publish, packar utdata till en zip och anropar `az webapp deploy` (motsvarar praktisk
  "web deploy" / Kudu zipdeploy). Kräver `az login` och behörighet till resursgruppen.

.PARAMETER ResourceGroup
  Azure-resursgrupp som innehåller Web App.

.PARAMETER WebAppName
  Web App-namn (hostname utan .azurewebsites.net).

.PARAMETER Subscription
  Valfritt: prenumerations-id eller namn. Sätter aktiv prenumeration innan deploy.

.EXAMPLE
  .\scripts\Deploy-ApiToAzureZip.ps1 -ResourceGroup "rg-factorygame-dev" -WebAppName "factorygame-h5hmbzgncnazcmgu"

.EXAMPLE
  .\scripts\Deploy-ApiToAzureZip.ps1 -ResourceGroup "rg-factorygame-dev" -WebAppName "factorygame-h5hmbzgncnazcmgu" -Subscription "Mitt MSDN-abonnemang"
#>
param(
    [Parameter(Mandatory = $true)]
    [string] $ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string] $WebAppName,

    [Parameter(Mandatory = $false)]
    [string] $Subscription = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$publishDir = Join-Path ([System.IO.Path]::GetTempPath()) ("factorygame-api-publish-" + [Guid]::NewGuid().ToString("N"))
$zipPath = Join-Path ([System.IO.Path]::GetTempPath()) ("factorygame-api-" + (Get-Date -Format "yyyyMMddHHmmss") + ".zip")

try {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI (az) saknas. Installera: https://aka.ms/installazurecliwindows"
    }

    if ($Subscription) {
        Write-Host "Sätter prenumeration: $Subscription"
        az account set --subscription $Subscription | Out-Host
    }

    Write-Host "dotnet publish -> $publishDir"
    dotnet publish (Join-Path $repoRoot "src\FactoryGame.Api\FactoryGame.Api.csproj") `
        -c Release `
        -o $publishDir `
        --no-self-contained

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Write-Host "Skapar zip: $zipPath"
    $items = Get-ChildItem -Path $publishDir -ErrorAction Stop
    if ($items.Count -eq 0) {
        throw "Publish-mappen är tom."
    }
    Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force

    Write-Host "az webapp deploy (zip) -> $WebAppName i $ResourceGroup"
    az webapp deploy `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --src-path $zipPath `
        --type zip `
        --timeout 600000 `
        --track-status true
}
finally {
    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    }
}
