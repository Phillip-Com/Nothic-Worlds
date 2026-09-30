# Nothic Worlds

A worldbuilding tool for designing and simulating fictional star systems in 3D, from whole
systems down to local regions on a planet. Built for Dungeon Masters and world builders.

- What we're building: [docs/VISION.md](docs/VISION.md)
- How the code is written: [CLAUDE.md](CLAUDE.md)
- Key decisions: [docs/DECISIONS.md](docs/DECISIONS.md)

## Requirements

- [Godot 4.7.2, .NET edition](https://godotengine.org/download/archive/4.7.2-stable/)
  (`winget install GodotEngine.GodotEngine.Mono --version 4.7.2`)
- [.NET SDK 9](https://dotnet.microsoft.com/download) (pinned in `global.json`)

## Build, run, and test

| Task | How |
|------|-----|
| Build everything | `dotnet build NothicWorlds.sln` |
| Run tests | `dotnet test NothicWorlds.sln` |
| Check formatting | `dotnet format NothicWorlds.sln --verify-no-changes` |
| Fix formatting | `dotnet format NothicWorlds.sln` |
| Open in Godot | Start Godot → **Import** → select `godot/project.godot` |
| Run the app | In the Godot editor, press **Play** (F5) |

## Controls

| Action | Mouse | Keyboard |
|--------|-------|----------|
| Orbit | Left-drag | — |
| Pan (slides the view when zoomed out, the surface when close) | Right-drag | WASD / arrow keys |
| Zoom | Scroll wheel | E / Q or + / - |
| Reset view | — | Home |
| Toggle lat/long grid | — | G |
| Performance overlay | — | F3 |

Use **Import Map…** (top-left) to wrap a map image onto the planet. Set **Map type** to match it:
**Flat map** (the default) for hand-drawn and fantasy-tool maps, or **Globe map** for 2:1
equirectangular maps made for globes.

## Repository layout

```
src/NothicWorlds.Core/          World data and simulation (plain C#, no Godot)
tests/NothicWorlds.Core.Tests/  Tests for the core library
godot/                          Godot project: rendering, UI, input
docs/                           Vision, decisions, design notes
```
