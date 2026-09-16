# Build script for Jewski Free Tweaks - builds JewskiFreeTweaks.exe from Program.cs using
# only the C# compiler that ships with the .NET Framework (no Visual Studio / SDK needed).
# Run from anywhere: powershell -ExecutionPolicy Bypass -File build_jewski.ps1

$ErrorActionPreference = 'Stop'
$buildDir = $PSScriptRoot
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) {
    Write-Host "Could not find csc.exe at $csc - this needs .NET Framework 4.x (present by default on Windows 10/11)." -ForegroundColor Red
    exit 1
}

$refs = "System.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Core.dll,System.Data.dll,System.Management.dll"

Write-Host "Compiling JewskiFreeTweaks.exe..." -ForegroundColor Cyan
Push-Location $buildDir
& $csc /target:winexe /out:JewskiFreeTweaks.exe /win32manifest:app.manifest /win32icon:jewski.ico `
    /resource:logo.png,JewskiTweaksGUI.Resources.logo.png `
    /resource:launch.mp3,JewskiTweaksGUI.Resources.launch.mp3 `
    /resource:done.mp3,JewskiTweaksGUI.Resources.done.mp3 `
    /platform:x64 /optimize+ /reference:$refs Program.cs
Pop-Location

if (Test-Path (Join-Path $buildDir "JewskiFreeTweaks.exe")) {
    Write-Host ""
    Write-Host "DONE. Built $(Join-Path $buildDir 'JewskiFreeTweaks.exe')" -ForegroundColor Green
} else {
    Write-Host "Build failed - check the compiler output above for errors." -ForegroundColor Red
    exit 1
}
