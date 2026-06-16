<p align="center">
  <b>OpenNCL v4.0</b><br>
  <i>A hybrid terminal launcher — WinUI 3 (C#) shell + Python kernel backend</i>
</p>

<p align="center">
  ( English | <a href="README-zh.md">简体中文</a> )
</p>

<p align="center">
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-512BD4.svg"></a>
  <a href="https://learn.microsoft.com/windows/apps/windows-app-sdk/"><img src="https://img.shields.io/badge/Windows%20App%20SDK-1.8-2563EB.svg"></a>
  <a href="https://www.python.org/"><img src="https://img.shields.io/badge/Python-3.11-3776AB.svg"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-green.svg"></a>
  <a href="https://github.com/chenTom2016/OpenNCL/stargazers"><img src="https://img.shields.io/github/stars/chenTom2016/OpenNCL.svg?style=social"></a>
  <a href="https://github.com/chenTom2016/OpenNCL/issues"><img src="https://img.shields.io/github/issues/chenTom2016/OpenNCL.svg"></a>
</p>

---

This is the **WinUI 3** rewrite of **OpenNCL** (_Open New Command Line OS_), a multifunctional terminal launcher built with a native **C#** front end and a **Python** kernel backend, communicating over a stdin/stdout protocol.

Author: **ChenTom2016**

Environment: **Windows + .NET 10 + Python**

Last Updated: June 7, 2026

This project is licensed under the **MIT** License.

> [!TIP]
> The C# shell runs ~50 commands locally for speed and forwards advanced commands to the Python kernel. A Flask WebUI, a WebView2 AI sidebar, and a zero-build standalone HTML terminal are also included.

---

## Navigation
- [New](#What's the new?)
- [Architecture](#architecture)
- [Features](#features)
- [Quick Start](#quick-start)
- [Command Reference](#command-reference)
- [Project Structure](#project-structure)
- [Author](#author)

---


## What's the new?

Plugin System – Add Commands Without Recompiling
Drop a Python script into the plugins/ folder and it becomes a new command instantly. No C# recompilation, no restart of the whole application – just write a function and start using it. An example hello plugin is included.

Command Palette (Ctrl+Shift+P)
Inspired by VS Code, the new command palette lists ~50 built‑in actions. Type to filter, select with arrow keys, and press Enter to insert the command into your terminal line – you can still add arguments before running it.

AI Sidebar (Tab Key)
A dedicated chat panel powered by WebView2. Ask questions, get code suggestions, or discuss problems with an AI assistant while keeping your terminal session in focus. The sidebar can be toggled on/off with the Tab key, and your API configuration is saved between sessions.

Smoother Animations & Better Performance
The rainbow background orbs now use zero memory allocations per frame – no more stutter or garbage collection pauses. The terminal input also handles Ctrl+V correctly, so pasting from the clipboard finally works as expected.

Build & Stability Improvements
Resolved several build warnings (NETSDK1198, PRI249) – cleaner compilation logs.

Improved error messages when the Python kernel is offline.

Fixed a double‑execution bug that could run forwarded commands twice.

Core Terminal Enhancements
Customizable Prompts
Use config prompt <text> and config pro_prompt <text> to set your own prompt strings. They are saved to brand.json and restored on startup.

Mode Pro – Visual Boost
Enter mode pro to switch to an orange accent theme that makes the prompt stand out. Your custom "pro" prompt will be used automatically.

Terminfo Command
Type terminfo to see terminal status: mode, prompt, current working directory, kernel state, number of available commands, and more.

Real Color Themes
The color(fg,bg) command now genuinely changes the terminal's foreground and background colors at runtime – no restart needed.

Standalone HTML Terminal
The webui/standalone.html file now includes all the new commands (terminfo, config prompt, qrcode, translate, pwd) plus theme switching and a polished top/bottom bar.

Full Command List (50+)
Category	Commands
Basic	help, about, date, time, version, ver, dir, ls, sysinfo, system, modules, clear, cls, exit, quit, cd, pwd, ip, terminfo
Tools	calc, calculate, echo, encrypt, decrypt, color(), install, search, kill
Web	google, bing, youtube, open, URLs auto‑detected and opened
System	cmd, powershell, explorer, notepad, control, taskmgr, mspaint, regedit, python, node
Advanced	mode pro, x++, logo, sandbox, bridge, edit, translate, linux, wsl
GUI	calculator, screenshot, qrcode
Config	config about, edit about, config prompt, config pro_prompt
Commands marked as "advanced" are forwarded to the Python kernel; everything else runs natively in C# for speed.

Notable Fixes
Paste (Ctrl+V) now works in the main terminal area (previously it only worked in the AI sidebar and command palette search box).

Command forwarding no longer sends a command twice when both C# and Python can handle it.

Startup handshake between C# and the Python kernel is now reliable (5‑second timeout with a __READY__ signal).

Path resolution for python/launcher.py works even when building to deep output folders like bin/Debug/net8.0-windows/win-x64/.

How to Get Started
Desktop (WinUI 3)

Build: dotnet build -c Release -r win-x64

Run: dotnet run -c Release -r win-x64

Requires: .NET 8, Windows App SDK 1.8+, Python 3.x on PATH

Standalone HTML Terminal

Open webui/standalone.html in any modern browser – no backend needed for 30+ commands.

WebUI Mode (Flask)

cd python && python ..\server\openncl_server.py

Visit http://127.0.0.1:7878



## Architecture

```
WinUI 3 (C#)  --stdin/stdout-->  Python (launcher.py)
     |                                  |
built-in commands                kernel/openncl_kernel.py
return directly                         |
     |                            Flask WebUI (optional, :7878)
     +-- WebView2 AI sidebar (ai-sidebar.html, SSE streaming)
```

---

## Features

- **CLI Shell**
  - Fullscreen terminal, inline prompt (RichTextBlock, no input box), keyboard-only operation
  - Path display with `cd` and `~` collapse; command history via up/down arrows
  - Built-in commands: `help`, `dir`, `date`, `ip`, `sysinfo`, `exit`
  - System commands: `python`, `node`, `cmd`, `powershell`, `notepad`, `explorer`

- **Dual Engine**
  - 50+ commands: C# local execution + Python kernel forwarding
  - `[ERROR] Did you mean?` Levenshtein fuzzy matching, `[x.xms]` per-command timing

- **Plugin Module**
  - Hot-pluggable Python scripts — drop a `.py` into `plugins/`, no recompile
  - Contract: `COMMAND` / `DESCRIPTION` / `run(args)`

- **Command Palette**
  - `Ctrl+Shift+P` VSCode-style fuzzy palette over a ~50-command catalog
  - Fills the input line instead of auto-running, so args can be appended

- **AI Sidebar**
  - `Tab`-toggle WebView2 panel with SSE streaming chat
  - Self-contained HTML/TS; C#-TS bridge via `ExecuteScriptAsync` / `postMessage`

- **Colorful Output**
  - Runtime `color(fg, bg)` terminal theme switching
  - Custom prompts (`config prompt`), Mode Pro orange accent

- **Search and Open**
  - `search <name>` via Everything (`es.exe`) for instant file lookup
  - `{search:Google}: OpenAI` or `{open:www.python.org}`

- **Advanced Tools**
  - QR code, screenshot, calculator, file encryption/decryption, translator
  - X++ lightweight interpreter, `mode pro`, sandbox/bridge commands

---

## Quick Start

### Requirements

- .NET 10 SDK
- Windows App SDK Runtime 1.8+
- `python` on `PATH`

### Build and Run

```bash
cd "OpenNCL Lancher"

# WinUI 3 desktop
dotnet build -c Debug -r win-x64
dotnet run   -c Debug -r win-x64
```

### Other Modes

```bash
# WebUI mode
cd python && python ..\server\openncl_server.py   # http://127.0.0.1:7878

# CLI mode
cd python && python ..\cli\openncl_cli.py

# Standalone HTML terminal (no backend)
start webui\standalone.html
```

---

## Command Reference

| Group | Commands |
|-------|----------|
| **Basic** | `help` `about` `date` `time` `version` `dir` `ls` `sysinfo` `clear` `cd` `pwd` `ip` `terminfo` `exit` |
| **Tools** | `calc` `echo` `encrypt` `decrypt` `color()` `install` `search` `kill` |
| **Web** | `google` `bing` `youtube` `open` `{search:}` `{open:}` · direct URLs auto-detected |
| **System** | `cmd` `powershell` `explorer` `notepad` `control` `taskmgr` `regedit` `python` `node` |
| **Advanced** | `mode pro` `x++` `logo` `sandbox` `bridge` `edit` `translate` `linux` `wsl` |
| **GUI** | `calculator` `screenshot` `qrcode` |
| **Config** | `config about <k> <v>` `edit about` `config prompt <text>` `config pro_prompt <text>` |
| **Plugins** | `plugins` · plus any command exported by a `plugins/*.py` script |

---

## Project Structure

```
 OpenNCL Lancher/
  ├─ .gitignore
  ├─ Development Log.log
  ├─ introduction.log
  ├─ launch.cmd
  ├─ OpenNCL Lancher.slnx
  ├─ README.md
  ├─ diagrams/
  │   ├─ diagram.txt
  │   └─ 2026-06-14T174353/
  │       └─ diagram.svg
  │
  └─ OpenNCL Lancher/
     ├─ App.xaml
     ├─ App.xaml.cs
     ├─ MainWindow.xaml
     ├─ MainWindow.xaml.cs
     ├─ AiSidebar.xaml
     ├─ AiSidebar.xaml.cs
     ├─ DebugWindow.xaml
     ├─ DebugWindow.xaml.cs
     ├─ app.manifest
     ├─ Package.appxmanifest
     ├─ OpenNCL Lancher.csproj
     ├─ OpenNCL Lancher.csproj.user
     ├─ Change request.md
     ├─ requirements.txt
     │
     ├─ .vscode/
     │   ├─ launch.json
     │   └─ tasks.json
     │
     ├─ Properties/
     │   ├─ launchSettings.json
     │   └─ PublishProfiles/
     │       ├─ win-arm64.pubxml
     │       ├─ win-x64.pubxml
     │       └─ win-x86.pubxml
     │
     ├─ Assets/
     │   ├─ LockScreenLogo.scale-200.png
     │   ├─ OpenNCLLogo.svg
     │   ├─ SplashScreen.scale-200.png
     │   ├─ Square150x150Logo.scale-200.png
     │   ├─ Square44x44Logo.scale-200.png
     │   ├─ Square44x44Logo.targetsize-24_altform-unplated.png
     │   ├─ StoreLogo.png
     │   └─ Wide310x150Logo.scale-200.png
     │
     ├─ Runtime/
     │   ├─ AgentLoop.cs
     │   ├─ BackendDebugHub.cs
     │   ├─ OpenNclNative.cs
     │   └─ PythonLauncher.cs
     │
     ├─ native/
     │   └─ OpenNclNative/
     │       ├─ README.md
     │       ├─ OpenNclNative.vcxproj
     │       ├─ openncl_native.cpp
     │       ├─ OpenNclNative/
     │       └─ x64/
     │           └─ Release/
     │
     ├─ kernel/
     │   ├─ __init__.py
     │   ├─ openncl_kernel.py
     │   └─ __pycache__/
     │       ├─ __init__.cpython-311.pyc
     │       └─ openncl_kernel.cpython-311.pyc
     │
     ├─ plugins/
     │   ├─ __init__.py
     │   ├─ example_hello.py
     │   └─ __pycache__/
     │       └─ example_hello.cpython-311.pyc
     │
     ├─ python/
     │   ├─ launcher.py
     │   ├─ OpenNCL.py
     │   ├─ config.json
     │   ├─ ascii_logo_color.txt
     │   └─ tools/
     │       └─ __pycache__/
     │
     ├─ server/
     │   └─ openncl_server.py
     │
     ├─ cli/
     │   └─ openncl_cli.py
     │
     ├─ webui/
     │   ├─ ai-sidebar.html
     │   ├─ app.js
     │   ├─ index.html
     │   ├─ standalone.html
     │   └─ style.css
     │
     ├─ config/
     │   └─ brand.json
     │
     ├─ bin/
     │   ├─ Debug/
     │   │   └─ net8.0-windows10.0.19041.0/
     │   │       └─ win-x64/
     │   └─ x64/
     │       └─ Debug/
     │           └─ net8.0-windows10.0.19041.0/
     │
     └─ obj/
         ├─ OpenNCL Lancher.csproj.nuget.dgspec.json
         ├─ OpenNCL Lancher.csproj.nuget.g.props
         ├─ OpenNCL Lancher.csproj.nuget.g.targets
         ├─ project.assets.json
         ├─ project.nuget.cache
         ├─ Debug/
         │   └─ net8.0-windows10.0.19041.0/
         │       ├─ .NETCoreApp,Version=v8.0.AssemblyAttributes.cs
         │       ├─ OpenNCL Lancher.AssemblyInfo.cs
         │       ├─ OpenNCL Lancher.AssemblyInfoInputs.cache
         │       ├─ OpenNCL Lancher.GeneratedMSBuildEditorConfig.editorconfig
         │       ├─ OpenNCL Lancher.assets.cache
         │       ├─ OpenNCL Lancher.csproj.AssemblyReference.cache
         │       ├─ ref/
         │       ├─ refint/
         │       └─ win-x64/
         └─ x64/
             └─ Debug/
                 └─ net8.0-windows10.0.19041.0/
```

**Totals:** ~3,500 source lines across 30+ files — C# ~700 · Python kernel 2,200+ · WebUI 500+

---

## Contributing

Contributions are welcome. If you have feature suggestions, bug reports, or code improvements, please submit a Pull Request or Issue.

---

## Author

- Author / Maintainer: **Tom (chenTom2016)**
- GitHub: [chenTom2016](https://github.com/chenTom2016)
- Contact: **OpenNCL@outlook.com**

---

# Enjoy the new terminal experience.
– The OpenNCL

<sub>OpenNCL v4.0 · WinUI 3 + Python hybrid terminal · last updated June 7, 2026</sub>
