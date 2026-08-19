# ObbyistMacro

> Professional Roblox obbies macro toolkit — FPS changer, Wallhop, Freeze, Align and Wall Walk macros.

[![Download latest](https://img.shields.io/badge/Download-Latest%20Release-3BFF88?style=for-the-badge)](https://github.com/orbitthegreatest/ObbyistMacro/releases/latest)
[![OrbitDen](https://img.shields.io/badge/OrbitDen-Main%20Website-3BFF88?style=for-the-badge)](https://orbitden.vercel.app/)
[![Build](https://github.com/orbitthegreatest/ObbyistMacro/actions/workflows/build.yml/badge.svg)](https://github.com/orbitthegreatest/ObbyistMacro/actions/workflows/build.yml)

A lightweight Windows macro toolkit for Roblox obbies — part of the
[OrbitDen](https://orbitden.vercel.app/) app suite. One small tray app, five
macros, global hotkeys — everything configured from a clean dark UI.

**All macros only trigger while Roblox is the focused window**, so your hotkeys
never leak into other games or apps. A **global suspend key** overrides that:
one press stops every macro instantly, even when Roblox isn't focused.

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
- Jump is optional (scan-code injected, like Spencer's original).
- Customizable hotkey.

### ❄️ Freeze Macro
Instantly **suspend the entire Roblox process** (`NtSuspendProcess`) and resume
it just as fast.

- **Toggle** mode — one key freezes, the same key resumes.
- **Hold** mode — Roblox is frozen only while the key is held.
- Customizable hotkey (keyboard or mouse button).

### ↔️ Align Macro
Re-enables Roblox's removed camera alignment keys (`,`) and (`.`) as
one-tap hotkeys — perfect for jump-in-place tricks in games that still support
alignment.

- Alignment keys are **resolved from your keyboard layout** automatically
  (US, AZERTY, QWERTZ, …), with Shift / AltGr modifiers honored.
- Separate hotkey for left and right alignment.
- The Align tab shows your detected layout and the resolved keys live.

### 🧗 Wall Walk Macro
Loop a flick-right / flick-left to stay glued to walls — ported from Spencer
Macro Utilities.

- Flick distance follows Spencer's formula from **your sensitivity**
  (`round(360 / sens × 0.13)`, 94 px at 0.5 sens).
- Flick timing follows your **FPS cap** (one frame per flick, `(1000/fps + 0.5) × 1.1` ms).
- **Toggle** or **Hold** mode; the loop stops itself when Roblox loses focus.

### ⏸️ Global Suspend Key
One key to stop everything. While suspended, every macro hotkey is ignored and
a running Wall Walk loop stops instantly — works even when Roblox is not
focused. The Home tab shows the current state with a cursor-following tooltip.

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
- **Align** — presses the layout-resolved `,` / `.` alignment key via
  `VkKeyScanExW` + `MapVirtualKeyExW` against Roblox's keyboard layout.
- **Wall Walk** — mouse flick loop on its own thread, stopped by focus loss or
  the global suspend key.

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

Pushing a tag like `v2.0.0` triggers the GitHub Actions workflow
(`.github/workflows/build.yml`), which builds the app, compiles the installer
and attaches `ObbyistMacro-Setup.exe` to the release automatically.

## Project layout

```
assets/          icon assets
installer/       Inno Setup script + output
src/             WPF application (.NET 10)
  Controls/      custom controls (toggle switch, …)
  Core/          settings, input hooks, Roblox detection, keyboard layout
  Macros/        FPS, Wallhop, Freeze, Align and Wall Walk implementations
  MainWindow.xaml  main UI
build.bat        one-shot build + installer
.github/workflows/  CI build + release workflow
```

## Disclaimer

This tool is provided for educational and personal-use purposes. Use it at your
own risk and in accordance with the terms of service of the games you play.