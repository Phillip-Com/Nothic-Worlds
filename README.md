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
| New world / Open / Save / Save As | **File** menu | Ctrl+N / Ctrl+O / Ctrl+S / Ctrl+Shift+S |

Worlds are saved as `.nworld` files (in `Documents\Nothic Worlds\` by default). The app warns
before closing with unsaved changes, and keeps a recovery copy every 5 minutes that it offers
back after a crash.

Use **Import Map…** (top-left) to wrap a map image onto the planet. Set **Map type** to match the
layout the map was drawn in:

| Map type | For |
|----------|-----|
| **Flat map** (default) | Hand-drawn and fantasy-tool maps; keeps shapes as drawn |
| **Globe map** | 2:1 equirectangular maps made for globes |
| **Robinson**, **Winkel tripel**, **Mollweide**, **Gall–Peters** | Maps drawn in those atlas layouts |
| **Polar (north)** | One circle: north pole in the center, equator at the edge |
| **Two hemispheres** | Two circles: western hemisphere left, eastern right |

Where a map doesn't cover the globe (a flat map's polar caps, a polar map's southern
hemisphere), pick the color with **Fill color**.

If a map doesn't line up exactly (most hand-drawn maps don't), use **Calibrate…**: drag each
latitude/longitude line to where it really is on your map while watching the globe update, then
**Done** (Enter) or **Cancel** (Esc). Right-click a line to remove it. Calibration is saved with
the world.

To place parts of a map by hand, open **Pieces…**. **Cut from Map…** or **Cut from Image…** opens
the Cut editor: drag a box (**Rectangle**), or click points around a region and click the first
point again (**Freeform**). Scroll to zoom and right-drag to pan. **Add Piece** puts it on the
globe: a cut from the main map starts exactly where it already shows, and one from another image
starts in the middle of the view.

While the Pieces panel is open, click a piece on the globe to select it, then:

| Action | How |
|--------|-----|
| Move | Drag the piece |
| Resize (keeps proportions) | Drag a corner square |
| Rotate | Drag the round handle above it (hold Shift to snap to 15°) |
| Deselect | Click empty space or press Esc |

The panel's fields set exact latitude, longitude, rotation, and width. Pieces higher in the list
cover lower ones. Up to 32 pieces per planet, all saved with the world.

## Repository layout

```
src/NothicWorlds.Core/          World data and simulation (plain C#, no Godot)
tests/NothicWorlds.Core.Tests/  Tests for the core library
godot/                          Godot project: rendering, UI, input
docs/                           Vision, decisions, design notes
```
