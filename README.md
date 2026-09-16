# Jewski Tweaks - GUI Edition

A checkbox-driven Windows tweak tool: pick individual performance/privacy/network tweaks (or use "Recommended" / "Select All"), apply them, and revert any of them individually later. Requires admin (it self-elevates via UAC).

## Run it

Paste into PowerShell:

```powershell
irm https://raw.githubusercontent.com/xpetsim089-a11y/Jewski-Tweaks/main/run.ps1 | iex
```

That downloads the latest `JewskiTweaks.exe` from this repo's [Releases](../../releases) page and launches it. You'll get a UAC prompt - the app needs admin rights to change services, the registry, and power settings.

## Or just download it directly

Grab `JewskiTweaks.exe` from the [latest release](../../releases/latest) and run it yourself - no PowerShell one-liner needed.

## What it does

- ~150 individual tweaks across Power, Network/Ethernet, Audio, Input, Visual Effects, Privacy, Gaming/GPU, Startup, Debloat, Services, Scheduled Tasks, Storage, and RAM/Boot tuning
- Every tweak shows its real effect, risk level (Safe / Moderate / Advanced), and has its own individual revert
- A live Startup Apps manager and a few one-click fixes (restore mic access, restart audio, restart Explorer, rebuild icon cache)
- Nothing is applied until you check boxes and click Apply - creating a System Restore Point first is one click

## Safety notes

- Nothing here bypasses Windows security features by design; a few "Advanced"-tagged tweaks intentionally trade off things like driver-update protection or SYN-flood defenses, and are unchecked by default with the tradeoff explained in the app
- Source is plain C#/WinForms, compiled with the standard .NET Framework compiler that ships with Windows - no external runtime, no telemetry, no network calls except the one-time download above
