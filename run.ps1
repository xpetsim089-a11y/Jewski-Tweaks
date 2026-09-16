# Jewski Free Tweaks - bootstrap launcher
# This is the script people run via:  irm https://raw.githubusercontent.com/xpetsim089-a11y/Jewski-Tweaks/main/run.ps1 | iex
# It downloads the latest JewskiFreeTweaks.exe from this repo's GitHub Releases and launches it elevated.
# Nothing here modifies your system by itself - it only fetches and starts the real app,
# which then shows its own UI and applies nothing until you click Apply in there.

$ErrorActionPreference = 'Stop'

$repo = "xpetsim089-a11y/Jewski-Tweaks"

$releaseUrl = "https://github.com/$repo/releases/latest/download/JewskiFreeTweaks.exe"
$dest = Join-Path $env:TEMP "JewskiFreeTweaks.exe"

Write-Host ""
Write-Host "  JEWSKI FREE TWEAKS - fetching latest build..." -ForegroundColor Magenta
Write-Host "  Source: $releaseUrl"
Write-Host ""

try {
    Invoke-WebRequest -Uri $releaseUrl -OutFile $dest -UseBasicParsing
} catch {
    Write-Host "Download failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Check that a GitHub Release exists with an asset named exactly 'JewskiFreeTweaks.exe'."
    exit 1
}

Write-Host "  Launching (a UAC prompt will appear - the app needs admin to apply tweaks)..." -ForegroundColor Cyan
Start-Process -FilePath $dest -Verb RunAs
