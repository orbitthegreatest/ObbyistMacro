# ObbyistMacro

> Professional Roblox obbies macro toolkit — FPS changer, Wallhop and Freeze macros.

[![Download latest](https://img.shields.io/badge/Download-Latest%20Release-3BFF88?style=for-the-badge)](https://github.com/orbitthegreatest/ObbyistMacro/releases/latest)
[![OrbitDen](https://img.shields.io/badge/OrbitDen-Main%20Website-3BFF88?style=for-the-badge)](https://orbitden.vercel.app/)

A lightweight Windows macro toolkit for Roblox obbies — part of the
[OrbitDen](https://orbitden.vercel.app/) app suite. One small tray app, three
macros, global hotkeys — everything configured from a clean dark UI.

**All macros only trigger while Roblox is the focused window**, so your hotkeys
never leak into other games or apps.

---

## Features

### ⚡ FPS Macro
Toggle the in-game FPS cap between **60 and 240** with a single hotkey. The full
keyboard navigation (open menu → focus it → highlight FPS → pick 60/240 → close)
is automated for you.

- Guided 3-step **calibration** — the app walks you through the menu once, and
  remembers exactly how many `Down` presses your Roblox menu needs.
- Customizable hotkey (keyboard or mouse button).
- The status bar always shows the current cap.

### 🧱 Wallhop Macro
One-tap wallhop: a **150° flick**, a jump, and a flick back — in ~100 ms.

- Pixel distance is computed from **your Roblox sensitivity**, so the flick is
  correct on every setup.
- Customizable hotkey.

### ❄️ Freeze Macro
Instantly **suspend the entire Roblox process** (`NtSuspendProcess`) and resume
it just as fast.

- **Toggle** mode — one key freezes, the same key resumes.
- **Hold** mode — Roblox is frozen only while the key is held.
- Customizable hotkey (keyboard or mouse button).

---

## Installation

1. Download the latest installer from the
   [Releases page](https://github.com/orbitthegreatest/ObbyistMacro/releases/latest).
2. Run `ObbyistMacro-Setup.exe` and follow the installer.
3. Launch **ObbyistMacro** — it runs in the system tray (the window closes to
   the tray, it keeps running).

> Requires **Windows 10 or later** and **.NET 10 runtime** (bundled in the release
> build — no manual install needed).

## Quick start

1. Open the app, go to the **FPS** tab.
2. Click **Start Calibration**, then in Roblox follow the steps shown (open the
   menu with `Esc`, press `Tab`, arrow `Down` until the FPS option is highlighted,
   press `Enter`).
3. Bind your hotkeys (click a keybox, press the key you want, `Esc` cancels).
4. Enable the macros you want and **keep Roblox focused** — your hotkeys now work
   in-game.

## How it works

ObbyistMacro installs low-level global keyboard/mouse hooks and watches for your
hotkeys. When a hotkey is pressed **and Roblox is the foreground window**, the
macro executes:

- **FPS** — simulates the exact key sequence Roblox uses to switch the graphics
  FPS cap (ported from `roblox-fps-toggle.ahk`).
- **Wallhop** — mouse flick + jump with a 19 ms wallhop length, flicked back to
  where you started (Spencer Macro Utilities defaults).
- **Freeze** — suspends/resumes the Roblox process via `NtSuspendProcess`.

Settings are saved automatically to
`%LOCALAPPDATA%\ObbyistMacro\settings.json`.

## Building from source

Prerequisites:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (only for the installer)

```bat
:: build the app + installer
build.bat

:: or just the app
dotnet publish src\ObbyistMacro.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o src\publish
```

Output:

- App: `src\publish\ObbyistMacro.exe`
- Installer: `installer\output\ObbyistMacro-Setup.exe`

## Project layout

```
assets/          icon assets
installer/       Inno Setup script + output
src/             WPF application (.NET 10)
  Controls/      custom controls (toggle switch, …)
  Core/          settings, input hooks, Roblox detection
  Macros/        FPS, Wallhop and Freeze implementations
  MainWindow.xaml  main UI
build.bat        one-shot build + installer
```

## Disclaimer

This tool is provided for educational and personal-use purposes. Use it at your
own risk and in accordance with the terms of service of the games you play.