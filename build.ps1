$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'External\FiveMWantedLighting.csproj'
$out = Join-Path $PSScriptRoot 'publish'

Write-Host '=== FiveM Wanted Lighting - Build ===' -ForegroundColor Cyan
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet SDK wurde nicht gefunden. Installiere .NET 8 SDK und starte PowerShell erneut.'
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

dotnet publish $project -c Release -r win-x64 --self-contained false -o $out

Write-Host "`nFertig: $out" -ForegroundColor Green
Write-Host 'Starte danach FiveM und dann FiveMWantedLighting.exe.' -ForegroundColor Yellow
