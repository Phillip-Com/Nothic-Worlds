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
| Undo / Redo | **Edit** menu | Ctrl+Z / Ctrl+Y (or Ctrl+Shift+Z) |
| Fly to another body (sun, planet, moon) | Click it, its dot, or its name | — |

A new world is a small star system: a sun with one planet orbiting it. Zoom out to see the whole
system, and click any body to fly to it. The map tools work on the selected body; stars have no
map. The time bar (bottom right) plays the world clock at the chosen speed, steps it back or forward
with **−** / **+** (by an hour, day, week, 30 days, or year of the selected body), shows the date
on the selected body (in its calendar, or in its own days without one), jumps to a date with
**Go to…**, and switches between a readable view and **True scale**. Steps and jumps glide
smoothly, so you can watch the bodies move into place. Above the buttons, a second line shows the
body's seasons in each hemisphere and when the next solstice or equinox comes.

In number fields, Up/Down change the value (Shift for bigger steps). Click the view to give the
arrow keys back to the camera.

**System…** opens the system panel on the left. It shows the tree of bodies (click one to fly
there), lets you **Add Planet**, **Add Moon**, or **Add Star**, and **Delete** the selected body
along with everything orbiting it (Ctrl+Z brings it back). **Make Center** puts the selected
body at the center of its system, with the bodies it orbited now circling it on the same paths
(for example, a planet-centered system with the sun going around it). It also edits the selected body's
name, kind, radius, day length, axial tilt, and orbit: what it orbits, distance (km or AU),
period, and starting position. Switch on "Elongated or tilted orbit" for more. While the panel is
open, the selected body's path is drawn in yellow, and every change shows live as you type.
**Axis direction** sets which way the tilted axis leans, which decides when in the year the
solstices fall.

Planets and moons can have their own calendar: **Calendar · Edit…** in the panel sets its months,
weekdays, year numbering (with an optional era), and the date the clock starts on. Below it, the
panel lists the year's solstices and equinoxes with **Go to** buttons, and they are marked on the
orbit too (equinoxes as dots, solstices as diamonds).

The panel also lists the selected body's eclipses in the coming year: solar eclipses (one of its
moons in front of the star) and lunar ones (a moon in its shadow), each with its type, date, how
deep and how long, and a **Go to** button (a moon shows the same eclipses as its planet). The
time bar's **Go to…** dialog always offers a jump to the next solar and the next lunar
eclipse. Each moon's previous and next eclipse are also marked on its orbit (when they're within
one orbit of now): gold-ringed dark disks for solar eclipses, dark red disks for lunar ones.

**Journal…** opens the journal on the right (in place of the Pieces panel). Search and sort your
entries, add one with **New Entry**, and write: each entry has a title, an optional place (a
planet, moon, or star), and text. Changes go into the world as you type, and Ctrl+Z undoes them.
Deleting a body keeps the entries about it, just without their place.

**Timeline…** shows your world's history in a strip above the time bar: a lane for each timeline,
with events as dots (a moment) or bars (something that lasts). Scroll the wheel to zoom from
hours to millennia, drag to move through time, click an event to go there, and double-click to
edit it: title, timeline, dates, place, description, and the journal entries it links to.
**New Event** adds one at the current time; **Timelines…** adds, renames, recolors, hides,
reorders, and deletes timelines (deleting one deletes its events; Ctrl+Z brings them back).

Entries and events placed on a planet or moon can also be pinned to a spot: **Pin on Globe…** in
their editor flies there, and you click the spot (Esc cancels). Pins show on the globe; click
one to see what's there and open it. The **Pins** toggle in the toolbar hides them.

**Regions…** opens the regions panel (in place of the Journal and Pieces panels). **New Region**
lets you click corners around an area on the planet; press Enter or click the first corner to
finish. Name it, pick its color, and write notes; **Edit Points** lets you drag corners, drag the
small middle handles to add corners, and right-click a corner to delete it. Regions show as a
colored outline and light fill with their name, and clicking one shows its notes and everything
placed in it. Journal entries and events can be placed in a region with the **Region** choice in
their editor. The **Regions** toggle hides them all.

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
| Delete | Delete key or the panel's Delete button (Ctrl+Z brings it back) |
| Stretch (Edit Points) | Double-click the piece (or **Edit Points** in the panel), then drag any point of the cut; Esc when done. **Reset Points** undoes all stretching. |

The panel's fields set exact latitude, longitude, rotation, and width. Pieces higher in the list
cover lower ones. Up to 32 pieces per planet, all saved with the world.

## Repository layout

```
src/NothicWorlds.Core/          World data and simulation (plain C#, no Godot)
tests/NothicWorlds.Core.Tests/  Tests for the core library
godot/                          Godot project: rendering, UI, input
docs/                           Vision, decisions, design notes
```
