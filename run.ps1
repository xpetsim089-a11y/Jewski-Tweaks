# Jewski Tweaks - bootstrap launcher
# This is the script people run via:  irm https://raw.githubusercontent.com/USERNAME/REPO/main/run.ps1 | iex
# It downloads the latest JewskiTweaks.exe from this repo's GitHub Releases and launches it elevated.
# Nothing here modifies your system by itself - it only fetches and starts the real app,
# which then shows its own UI and applies nothing until you click Apply in there.

$ErrorActionPreference = 'Stop'

# ---- EDIT THIS: replace with your GitHub username/repo, e.g. "jewski/jewski-tweaks" ----
$repo = "USERNAME/REPO"

$releaseUrl = "https://github.com/$repo/releases/latest/download/JewskiTweaks.exe"
$dest = Join-Path $env:TEMP "JewskiTweaks.exe"

Write-Host ""
Write-Host "  JEWSKI TWEAKS - fetching latest build..." -ForegroundColor Magenta
Write-Host "  Source: $releaseUrl"
Write-Host ""

try {
    Invoke-WebRequest -Uri $releaseUrl -OutFile $dest -UseBasicParsing
} catch {
    Write-Host "Download failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Check that a GitHub Release exists with an asset named exactly 'JewskiTweaks.exe'."
    exit 1
}

Write-Host "  Launching (a UAC prompt will appear - the app needs admin to apply tweaks)..." -ForegroundColor Cyan
Start-Process -FilePath $dest -Verb RunAs
