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

# WPF assemblies (for the launch-video splash screen) live in the GAC, not next to csc.exe.
function Find-GacAssembly($name) {
    $hit = Get-ChildItem "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\$name","C:\Windows\Microsoft.NET\assembly\GAC_64\$name","C:\Windows\Microsoft.NET\assembly\GAC_32\$name" -Filter "$name.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $hit) { throw "Could not find $name.dll in the GAC." }
    return $hit.FullName
}
$presentationCore = Find-GacAssembly "PresentationCore"
$presentationFramework = Find-GacAssembly "PresentationFramework"
$windowsBase = Find-GacAssembly "WindowsBase"
$systemXaml = Find-GacAssembly "System.Xaml"
$windowsFormsIntegration = Find-GacAssembly "WindowsFormsIntegration"

$refs = @(
    "System.dll", "System.Windows.Forms.dll", "System.Drawing.dll", "System.Core.dll",
    "System.Data.dll", "System.Management.dll",
    $presentationCore, $presentationFramework, $windowsBase, $systemXaml, $windowsFormsIntegration
) -join ","

Write-Host "Compiling JewskiFreeTweaks.exe..." -ForegroundColor Cyan
Push-Location $buildDir
& $csc /target:winexe /out:JewskiFreeTweaks.exe /win32manifest:app.manifest /win32icon:jewski.ico `
    /resource:logo.png,JewskiTweaksGUI.Resources.logo.png `
    /resource:launch.mp3,JewskiTweaksGUI.Resources.launch.mp3 `
    /resource:done.mp3,JewskiTweaksGUI.Resources.done.mp3 `
    /resource:launch.mp4,JewskiTweaksGUI.Resources.launch.mp4 `
    /platform:x64 /optimize+ /reference:$refs Program.cs
Pop-Location

if (Test-Path (Join-Path $buildDir "JewskiFreeTweaks.exe")) {
    Write-Host ""
    Write-Host "DONE. Built $(Join-Path $buildDir 'JewskiFreeTweaks.exe')" -ForegroundColor Green
} else {
    Write-Host "Build failed - check the compiler output above for errors." -ForegroundColor Red
    exit 1
}
