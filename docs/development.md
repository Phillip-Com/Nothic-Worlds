# Development Guide

How to build, check, benchmark, and ship Nothic Worlds, and how the repo is laid out. The rules
for writing code are in [CLAUDE.md](../CLAUDE.md); this file is the reference behind them, read
when it's needed rather than every session.

## Tech stack (approved by the owner)

- **Engine:** Godot **4.7.2**, .NET edition. Chosen for its free MIT license (no royalties if
  the app is sold), its lightweight renderer that runs on integrated graphics, and its built-in
  3D, UI, and networking.
- **Renderer:** Godot's **Mobile** renderer. It's lighter than Forward+ and still supports the
  compute shaders that sculpting and weather will likely need. Verified on the baseline laptop
  in Milestone 1 (`REN-03` in VISION.md).
- **Language:** C# (latest language version) with nullable reference types enabled.
- **.NET:** SDK 9 (pinned in `global.json`). All projects target **net8.0** to match Godot's
  .NET runtime. Core must never target a newer framework than the Godot project.
- **Platform:** desktop (Windows first). Godot 4 C# projects can't currently export to the web.
  That's acceptable because player sharing (`SHR-01`) is a future feature.
- **Testing:** xUnit for the core library. Godot-side (rendering/UI) changes are verified with
  manual test steps in the PR, until a Godot test tool is approved.
- **Formatting/linting:** `dotnet format` driven by an `.editorconfig` in the repo root.

## Commands

Run from the repo root.

| Task | Command |
|------|---------|
| Build everything | `dotnet build NothicWorlds.sln` |
| Run tests | `dotnet test NothicWorlds.sln` |
| Check formatting | `dotnet format NothicWorlds.sln --verify-no-changes` |
| Check line length (Git Bash; prints nothing if OK) | `awk 'length > 100 {print FILENAME":"FNR}' $(git ls-files '*.cs' '*.gdshader')` |
| Run the app headless (smoke test) | `godot --headless --path godot --quit-after 30` |
| Performance benchmark (opens fullscreen ~12 s) | `godot --path godot --fullscreen -- --benchmark` |
| Load benchmark for a world | `godot --path godot -- --open=<world> --benchmark` |
| Build the Windows app (make `build/windows/` first) | `godot --headless --path godot --export-release "Windows Desktop" ../build/windows/NothicWorlds.exe` |

On this machine Godot is installed via winget. If the `godot` command isn't on PATH, use the
full path to `Godot_v4.7.2-stable_mono_win64_console.exe` under
`%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_*`.

## Checks before a PR

The first five commands above must succeed: the build has no errors, the tests pass, the
formatting check makes no changes, the line-length check prints nothing, and the headless run
starts without errors.

- The line-length check covers the **whole repo**, not just changed files, because
  `dotnet format` doesn't enforce the 100-character limit. Include new untracked `.cs` and
  `.gdshader` files and `godot/Rendering/*.gdshaderinc`.
- Headless runs don't compile shaders. After a shader edit, look for `SHADER ERROR` in the
  output of a windowed run or the benchmark.
- **PRs that affect rendering:** also run the benchmark and report its numbers in the PR.
  Compare them with the baseline under `REN-03` in VISION.md and flag any drop. Other apps
  using the GPU (especially an open Godot editor) skew the results. If a drop shows up,
  benchmark the previous commit under the same conditions before blaming the change.

## Building the Windows app

Building needs Godot's export templates for the same version (4.7.2, .NET), in
`%APPDATA%\Godot\export_templates\4.7.2.stable.mono\`. Get them from the Godot editor
(Editor ▸ Manage Export Templates) or from the official release on Godot's GitHub, checking the
download against the release's `SHA512-SUMS.txt`. The app is built into `build/`, which git
ignores. Double-click `build/windows/NothicWorlds.exe` to run it; the `.pck` file and the
`data_NothicWorlds_windows_x86_64` folder next to it must stay with it.

## Project structure

```
NothicWorlds.sln               Solution tying all C# projects together
Directory.Build.props          Shared C# build settings (nullable, style checks, lock files)
global.json                    Pins the .NET SDK version
.editorconfig                  Formatting and naming rules
src/NothicWorlds.Core/         Plain C# library, NO Godot references
  Geometry/                    Shared math: coordinates on spheres, conversions
  Editing/                     Undo/redo history
  Maps/                        Map image rules (layout, size limits)
  Measurement/                 Units: metric and imperial conversion and formatting
  Model/                       World data: bodies, maps, journals, calendars
  Simulation/                  Orbits, time, events, weather logic
  Storage/                     Save/load, format versioning, migrations
tests/NothicWorlds.Core.Tests/ xUnit tests, mirroring Core's folders
godot/                         The Godot project (references Core)
  Scenes/                      Scene files (.tscn) and their root scripts
  Rendering/                   Shaders and drawing; reads simulation state, never owns it
  UI/                          Panels, tools, pop-ups
  Controls/                    Input actions, camera and tool controls
  Maps/                        Loading and preparing map images (background thread)
  Session/                     The open world (all edits go through WorldSession), recovery
  Diagnostics/                 Performance overlay (F3) and benchmark
  Interop/                     Conversions between Core types and Godot types
  Assets/                      CC0 art (standing view's ground textures), credited in CREDITS.md
docs/                          VISION.md, DECISIONS.md, world-format.md, this guide
```

The controls folder is named `Controls/`, not `Input/`: a namespace called
`NothicWorlds.Input` would hide Godot's `Input` class.
