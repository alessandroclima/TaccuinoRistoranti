param(
    [switch]$UseDocker
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

if ($UseDocker) {
    Write-Host 'Avvio stack Docker (backend + frontend)...' -ForegroundColor Cyan
    Set-Location $repoRoot
    docker compose up --build
    exit $LASTEXITCODE
}

Write-Host 'Ripristino dipendenze backend/frontend/mobile...' -ForegroundColor Cyan
Set-Location (Join-Path $repoRoot 'backend/TaccuinoRistoranti.Api')
dotnet restore | Out-Host

Set-Location (Join-Path $repoRoot 'frontend')
npm install | Out-Host

Set-Location (Join-Path $repoRoot 'mobile')
flutter pub get | Out-Host

$backendCmd = "Set-Location '$repoRoot\backend\TaccuinoRistoranti.Api'; dotnet run"
$frontendCmd = "Set-Location '$repoRoot\frontend'; npm run dev"

Write-Host 'Apro backend in nuova finestra...' -ForegroundColor Green
Start-Process powershell -ArgumentList '-NoExit', '-Command', $backendCmd

Write-Host 'Apro frontend in nuova finestra...' -ForegroundColor Green
Start-Process powershell -ArgumentList '-NoExit', '-Command', $frontendCmd

Write-Host ''
Write-Host 'Backend e frontend avviati.' -ForegroundColor Green
Write-Host 'Per mobile (emulatore Android):' -ForegroundColor Yellow
Write-Host "  1) flutter emulators --launch <id_emulatore>"
Write-Host "  2) Set-Location '$repoRoot\mobile'"
Write-Host '  3) flutter run'
