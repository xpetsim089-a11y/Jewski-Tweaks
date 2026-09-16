# Jewski Free Tweaks - GUI Edition

A checkbox-driven Windows tweak tool: pick individual performance/privacy/network tweaks (or use "Recommended" / "Select All"), apply them, and revert any of them individually later. Requires admin (it self-elevates via UAC).

## Run it

Paste into PowerShell:

```powershell
irm https://raw.githubusercontent.com/xpetsim089-a11y/Jewski-Tweaks/main/run.ps1 | iex
```

That downloads the latest `JewskiFreeTweaks.exe` from this repo's [Releases](../../releases) page and launches it. You'll get a UAC prompt - the app needs admin rights to change services, the registry, and power settings.

## Or just download it directly

Grab `JewskiFreeTweaks.exe` from the [latest release](../../releases/latest) and run it yourself - no PowerShell one-liner needed.

## What it does

- 150+ individual tweaks across Power, Network/Ethernet, Audio, Input, Visual Effects, Privacy, Gaming/GPU, Startup, Debloat, Services, Scheduled Tasks, Storage, and RAM/Boot tuning
- Every tweak shows its real effect, risk level (Safe / Moderate / Advanced), and has its own individual revert
- Each category page has its own "Apply Recommended" / "Apply All" - no global batch button, so you always know exactly what you're about to change
- A live Startup Apps manager (real registry scan, publisher pulled from the actual exe) and a few one-click Fixes (restore mic access, restart audio, restart Explorer, rebuild icon cache)
- Nothing is applied until you click Apply on a page - creating a System Restore Point first is one click on the Home tab
- A short launch video/animation plays on startup, plus a sound on launch and another when a tweak batch finishes applying
- A busy overlay with live progress ("Applying 3/9: ...") shows during any Apply/Revert/Fix so nothing looks frozen

## Building from source

Clone the repo and run `build_jewski.ps1` from PowerShell - it compiles `Program.cs` with the plain C# compiler that ships with .NET Framework (no Visual Studio, no SDK, no NuGet). Output is `JewskiFreeTweaks.exe` in the repo folder.

## Safety notes

- Nothing here bypasses Windows security features by design; a few "Advanced"-tagged tweaks intentionally trade off things like driver-update protection or SYN-flood defenses, and are unchecked by default with the tradeoff explained in the app
- Source is plain C#/WinForms (`Program.cs` in this repo), compiled with the standard .NET Framework compiler that ships with Windows - no external runtime, no telemetry, no network calls except the one-time download above
