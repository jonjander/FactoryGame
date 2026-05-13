<#
.SYNOPSIS
  Minimal smoke-test mot FactoryGame API (default: Azure dev).

.EXAMPLE
  pwsh .cursor/skills/factory-game-azure-test/scripts/Smoke-AzureApi.ps1
#>
param(
    [string] $BaseUrl = "https://factorygame-h5hmbzgncnazcmgu.swedencentral-01.azurewebsites.net"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$BaseUrl = $BaseUrl.TrimEnd("/")

Write-Host "GET $BaseUrl/health"
$h = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get
if ($h -ne "Healthy") { throw "Unexpected health body: $h" }
Write-Host "  OK: $h"

Write-Host "GET $BaseUrl/swagger/v1/swagger.json (first 80 chars of title)"
$doc = Invoke-RestMethod -Uri "$BaseUrl/swagger/v1/swagger.json" -Method Get
if ($doc.info.title -ne "FactoryGame API") { throw "Unexpected OpenAPI title: $($doc.info.title)" }
Write-Host "  OK: $($doc.info.title) $($doc.info.version)"

Write-Host "POST $BaseUrl/v1/auth/guest (optional; requires working DB)"
try {
    $guest = Invoke-RestMethod -Uri "$BaseUrl/v1/auth/guest" -Method Post -ContentType "application/json" `
        -Body '{"deviceKey":"smoke-azure-api"}'
    $token = $guest.sessionToken
    if (-not $token) { throw "No sessionToken in guest response" }
    Write-Host "  OK: playerId=$($guest.playerId)"

    Write-Host "GET $BaseUrl/v1/me/wallet"
    $headers = @{ Authorization = "Bearer $token" }
    $wallet = Invoke-RestMethod -Uri "$BaseUrl/v1/me/wallet" -Method Get -Headers $headers
    Write-Host "  OK: wallet response received ($($wallet | ConvertTo-Json -Compress))"
}
catch {
    Write-Warning "Guest/wallet step failed: $($_.Exception.Message)"
}

Write-Host "Smoke finished (health + OpenAPI required; guest flow optional)."
