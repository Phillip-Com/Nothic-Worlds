# Nothic-Worlds — Vision, Goals & Ideas

This file records **what the project is meant to be** and **what each feature is meant to do**.
When it's unclear what a feature or design was meant to do, this file has the answer.
It also says **where** each feature lives and what can be reused, so existing systems are
extended instead of rebuilt.

- The owner's words are the authority on intent. If the owner hasn't confirmed an entry, mark it
  as such.
- Every feature has an ID (e.g. `SIM-01`) that code, PRs, and the Decision Log can refer to.
- Update an entry's **Status** and **Built** notes once work on it lands in a PR.
- **Keep entries short** (owner's choice, 2026-10-08, to keep this file cheap to read): the
  Intent, the owner's choices, the key files, what to reuse, and known limits. Measurements,
  test lists, and step-by-step history go in the PR description, not here. The longer notes
  written before then are in git history (`docs/VISION.md` at commit `98d697d`).

**Status values:** `Idea` (raw, not yet reviewed) · `Planned` (reviewed and approved for building) ·
`In Progress` · `Implemented` · `Future` (wanted, but not until later) · `Deferred` · `Dropped`
(entries are never deleted, so the history stays)

**Tier values:** `Base` (must run on the baseline laptop, see Section 2) · `Advanced` (opt-in; the
user enables it if they believe their system can handle it)

---

## 1. Core Premise

Nothic-Worlds began as the idea of **a 3D render of a 2D world**. It grew into a tool for designing
and simulating whole **star systems** for fictional worlds. Users can build suns, moons, planets,
and unusual bodies (flat worlds, world trees, hollow planets), then watch them move over time.
From that motion the tool derives calendars, seasons, weather, and celestial events.

**Audience:** Dungeon Masters running tabletop RPGs (TTRPGs) and world builders.

**Editing and viewing are equal priorities.** Users must be able to edit easily *and* see the
results of their changes. Viewing becomes even more important once worlds can be shared with
players at the table (see `SHR-01`).

**Scale range:** from a whole star system down to **local regions on a planet's surface**, shown
in a top-down terrain view. Going smaller than a local region is not a goal.

## 2. Guiding Goals

1. **Low system requirements.** Target the lowest hardware practical. A DM at a game table will
   most likely be on a laptop.
   - **Baseline machine:** the owner's laptop (AMD Ryzen 7 3700U, integrated Radeon Vega 10
     graphics, ~7 GB usable RAM). Every `Base` feature must run acceptably on it.
   - A mid-range PC is available for testing `Advanced` features.
   - Features that need more power go in an opt-in **Advanced** tier. Flag them to the owner
     before building.
2. **Look good and stay functional.** Good visuals, without trading away goal 1.
3. **Deep customization.** Users can shape bodies, not just pick from default textures.
4. **Simulation supports the world, but doesn't overrule the user.** The user's design is the
   default. Physical accuracy is an optional toggle, because not every fictional world is (or
   wants to be) realistic.
5. **Room to grow.** The owner is still generating ideas, so the architecture must make it easy to
   add new body types, events, and simulations later.
6. **Possible commercial release.** Single user for now, possibly sold later (see CLAUDE.md).

---

## 3. Milestones

Every milestone so far is **Complete**. The owner's choices for each are in the feature entries
below and in docs/DECISIONS.md.

| # | Milestone | Features | PRs |
|---|-----------|----------|-----|
| 1 | Planet viewer | `REN-01`, `REN-02`, `MAP-01` | #3, #4 |
| 2 | Maps and saving | `MAP-03`, `MAP-04`, `SAV-01`, `SAV-02` | #5–#8 |
| 3 | Map fitting | `MAP-05`, `MAP-02`, `UI-03` | #9–#15 |
| 4 | Star system basics | `BOD-01`, `SIM-01`, `SIM-02`, `UI-02` | #16–#18 |
| 5 | Calendars and seasons | `CAL-01`, `CAL-03` | #19–#20 |
| 6 | Eclipses | `EVT-01` | #21–#22 |
| 7 | Journals and timelines | `LORE-02`, `LORE-03` | #23–#26 |
| 8 | Region outlines | `LORE-01` | #27–#28 |
| 9 | Weather pin | `WTH-01` | #29–#30 |
| 10 | Layout tidy-up | `UI-01` | #31–#32 |
| 11 | Terrain painting | `BOD-05` | #33–#35 |
| 12 | Calendar fitting | `CAL-02` | #36 |
| 13 | Local region view | `REN-04` | #37–#38 |
| 14 | Terrain-aware weather | `WTH-03` | #39–#40 |
| 15 | Small fixes | `UI-01` | #41 |
| 16 | Body appearance | `BOD-06` | #42 |
| 17 | Leap years | `CAL-04` | #43 |
| 18 | Comets and meteor showers | `EVT-02` | #44–#45 |
| 19 | Flat worlds | `BOD-02` | #46–#48 |
| 20 | Astral features | `BOD-03`, `EVT-02` | #49–#52 |
| 21 | World tree | `BOD-02` | #53–#55 |
| 22 | Stable orbit guide | `SIM-04` | #56–#57 |
| 23 | Physics mode | `SIM-03` | #58–#59 |
| 24 | Body sculpting (heights) | `BOD-04` | #60–#64 |
| 25 | Shapes, holes, and hollow worlds | `BOD-04` | #65–#67 |
| 26 | Visual styles | `REN-05` | #69–#70 |
| 27 | Live weather | `WTH-02` | #71–#73 |
| 28 | Small fixes | `BOD-04` | #74 |
| 29 | Performance tiers | `REN-03` | #75–#76 |
| 30 | First-person surface view | `REN-06` | #77–#79 |
| 31 | Lore relationship diagrams | `LORE-04` | #80–#81 |
| 32 | Metric and imperial units | `UI-04` | #82–#83 |
| 33 | Polish and loose ends | `REN-06`, `LORE-04` | #84–#86 |
| 34 | Polish round two | `UI-05`, `UI-06`, `MAP-02`, `REN-06`, `REN-07` | #87–#90 |
| 35 | Terrain shapes the ground | `BOD-07` | #91 |
| 36 | Peaks and water | `BOD-08`, `BOD-09` | #92 |
| 37 | Flat worlds in relief | `BOD-10` | #93 |
| 38 | Clearer look | `UI-07` | #94 |
| 39 | The calendar | `CAL-05` | #95 |
| 40 | Calendars made easy | `CAL-06` | #96 |
| 41 | Map and time while standing | `REN-08` | #97 |
| 42 | Rivers and lakes | `BOD-11` | #98–#100 |
| — | Opening and editing without freezing | `REN-03`, `BOD-11` | #101–#103 |

---

## 4. Feature Areas

Each entry uses this format:

> **ID — Name** · Status · Tier
> **Intent:** what the owner wants it to do and why
> **Owner's choices:** decisions that shape it (when not already in the Intent)
> **Built:** the key files and how they fit · **Reuse:** what other work can build on ·
> **Limits:** known gaps

### 4.1 Rendering & Navigation (`REN`)

**REN-01 — 3D world rendering** · Implemented (M1: PR #3; star systems: PR #17) · Base
**Intent:** Render worlds and bodies in 3D. This grew from the original "3D render of a 2D world" concept.
**Built:**
- `Rendering/planet.gdshader` (the surface itself in `planet_surface.gdshaderinc`) works out
  latitude and longitude **per pixel from the surface direction**, not the mesh's texture
  coordinates, so it works on any mesh. With no map it draws a 15° grid of constant on-screen
  width. Axes: +Y north, longitude 0 faces +Z, 90° east faces +X, shared with Core's
  `SphericalCoordinates`; keep the two in sync.
- `Rendering/SystemView.cs` draws every body: planets and moons as a shared unit sphere with
  their own `PlanetSurface` (spun, tilted, and scaled by the node's transform, so maps and
  handles ride the surface); stars as glowing spheres with an unshadowed `OmniLight3D`; orbit
  lines from Core's `SystemLayout.OrbitPath`.
- **Floating origin:** positions are full precision and the scene is drawn around the focused
  body. The near plane is 2% of the distance to the nearest surface, so true-scale systems
  keep their depth precision.

**REN-02 — Multi-scale navigation (system → planet → local region)** · Implemented (planet and system views: PR #17; local region: M13, see `REN-04`) · Base
**Intent:** Move smoothly from viewing the whole star system down to a local region on a planet.
M1 covers these camera controls for a single planet:
- **Orbit:** drag to spin the globe / circle the camera around it
- **Zoom:** scroll wheel to move closer or farther
- **Pan:** switches automatically with zoom (owner's decision after trying the first version):
  - **Zoomed out:** slide the *whole view* sideways and up/down, moving the planet across the
    screen. At star-system scale, this is how the user moves around the whole area.
  - **Zoomed in close:** slide across the planet's surface.
- **Indicator:** on-screen text shows whether the camera is orbiting or panning, and which pan
  mode is active.
**Owner's choices:** readable scale by default (sizes and distances compressed) with a
true-scale toggle; data always in real units.
**Built:** `Controls/PlanetCamera.cs` (left-drag orbits, right-drag/WASD pans, the pan mode
chosen by altitude, everything easing frame-rate independently; public `Orbit`, `Pan`, `Zoom`,
`ResetView`, `FlyToSurface`), `UI/CameraModeIndicator.cs`, `UI/BodyMarkers.cs` (dots and names
for far bodies). Clicking a body flies there over 1.2 s. Core `Simulation/SystemLayout.cs`
compresses the readable view (a 0.4 power curve that keeps directions and order). Keys are
defined once in `Controls/InputActions.cs`.
**Reuse:** Core `Geometry/GeoCoordinate` and `SphericalCoordinates` for anything placed by
latitude and longitude.
**Limits (deferred by the owner):** a planet slid near the screen's edge looks oval, from the
75° field of view. A narrower lens or turning toward the planet would fix it.

**REN-03 — Performance tiers** · Implemented (M29: PRs #75–#76; background preparation: PRs #101–#103) · —
**Intent:** Keep requirements low with level-of-detail, quality settings, and rendering only what's
visible. Heavier features go in an opt-in **Advanced** section.
**Owner's choices:** a File ▸ Settings window with a Graphics Quality preset (Low, Standard,
High, or Custom) that sets each option; the starting preset from the graphics chip (Standard on
integrated, High on a dedicated card); high-quality (uncompressed) maps as the Advanced tier;
bodies **prepared in the background**, and **only when wanted** (selected, stood on, or big
enough on screen).
**Built:**
- `Session/AppSettings.cs`: settings that belong to the computer, in `user://settings.cfg`
  (a missing or damaged file gives the defaults).
- `UI/SettingsWindow.cs`, `Rendering/GraphicsOptions.cs` (the options and presets, Standing
  ground detail among them, `REN-06`), `Rendering/GraphicsSettings.cs` (puts
  them into effect).
- **Level of detail** (`PlanetSurface.ScreenRadius`, always on): under 200 px a sculpted globe
  uses the Low relief mesh; under 60 px every globe uses a coarse sphere; 15% hysteresis.
- **High-quality maps** (`Maps/MapQuality`): skips S3TC compression; switching reloads maps
  (`WorldSession.ReloadMapsAsync`).
- **Background preparation** (`Session/WorldSession.Prepare.cs`): a body's ground and globe
  images (`Rendering/SurfaceImages.cs`, handed over as a `PreparedSurface` to
  `PlanetSurface.ShowPrepared`) are made on worker threads, one body at a time, the selected
  one first; until then it's drawn plain and the toolbar says "Preparing …'s terrain…". Edits
  touching at most 96 tiles of painting stay at once (`TerrainRelief.ChangedTiles`); bigger
  ones go to the background and remake only the faces that changed. While painting, the shaped
  ground is reworked in the background too. A body is prepared once `PlanetSurface.DetailWanted`
  fires (forwarded by `SystemView.DetailWanted`).
- **Diagnostics:** `Diagnostics/PerformanceOverlay.cs` (F3: FPS, video and app memory, draw
  calls); `Diagnostics/Benchmark.cs` (`-- --benchmark`: orbits and zooms for 10 s with VSync
  off; with `--open=<world>` it first reports how long opening and preparing took and the
  slowest frame meanwhile).
**Baseline** (Vega 10 laptop, 1920 × 1080 fullscreen, nothing else using the GPU): treat
**~150–160 fps** as the realistic baseline for the standard benchmark and compare builds back
to back (an open Godot editor drops it to ~125–130). Opening a large, heavily painted world:
slowest frame ~150 ms (it was 6 s before background preparation).
**Limits:** an occasional brush move while painting is still slow (cause not yet pinned
down).

**REN-04 — Top-down local region view** · Implemented (M13: PRs #37–#38) · Base
**Intent:** Zoom down to a local region and see it as a top-down terrain view.
**Owner's choices:** a seamless deeper zoom on the same globe and camera, down to about 10 km
above an Earth-sized planet, top-down below a threshold; detail from map pieces; Zoom to
buttons for regions and pins; a scale bar, north arrow, and coordinates under the mouse.
**Built:** `PlanetCamera` below `LocalViewMaxAltitude` (0.25 radii) rides with the planet's
spin and tilt (`SurfaceFrame`, `SetLocal`), north up, left-drag pans; `SystemView` feeds it the
body's orientation every frame (`FollowSurface`) and caps the depth range (`MaxDepthRange`).
`UI/LocalViewAids.cs` (north arrow, scale bar, coordinates and regions under the mouse);
`Controls/ZoomTo.cs` (selects, flies, then `PlanetCamera.FlyToSurface` to fit a span).
**Reuse:** `ZoomTo` for any "show me this spot" button.

**REN-05 — Visual styles** · Implemented (M26: PRs #69–#70) · Base
**Intent:** Painterly is the default style. The goal is to let users choose other styles, such
as realistic or simple.
**Owner's choices:** Painterly, Realistic, and Simple; made in the surface shaders, not a
screen filter; stored per world (format v25); Painterly the default for every world.
**Built:** `Model/VisualStyle.cs`, `World.Style`. One shader per style: `planet.gdshader`
(Realistic) and `planet_painterly.gdshader` / `planet_simple.gdshader`, which add
`planet_style.gdshaderinc` with its own `light()`; `PlanetSurface.Style` swaps the shader.
Shaped worlds' rock uses toon light (`ShapedGlobe.UseStyle`). View ▸ Style
(`WorldSession.SetStyle`, one undo step).

**REN-06 — First-person surface view** · Implemented (M30: PRs #77–#79; M33: PRs #85–#86; M34: PR #90; ground tiles: PR #110) · Base (owner's choice, 2026-10-05; was Advanced, probably)
**Intent:** View the world from the surface in first person. It's a nice-to-have if it proves possible.
**Owner's choices:** walk or fly; the sky at true size and place with a magnify switch; day and
night skies (black on airless worlds), live weather overhead, a compass and readouts; on flat
worlds the same sky across the disc and walking over the rim onto the underside; clouds seen
from above; a ground-detail texture near the feet (Base tier); choosing where to stand by
clicking; true heights while standing; switches for fog, clouds, and night vision (owner's
request, 2026-10-08; night vision as brightened true colors, not green); ground that stays put
as you move, on globes and flat worlds alike, the water's surface built with it, and a
**Standing ground detail** setting (Low, Standard, High; named so by the owner, 2026-10-09).
**Built:**
- Core: `BodyOrientation.ToSystem` / `ShapeToSystem`; `Simulation/SkyView.cs` (`From`,
  `FromFlat`, `SolarTimeHours`, `BodyAt`); `Geometry/FlatWalk.cs` and `GlobeWalk` for moving
  over flat worlds and globes.
- `Controls/FirstPersonMode.cs`: View ▸ Stand Here… (a click picks the spot, through
  `PinPlacer`), its own camera, Esc back. While standing `SystemView` puts the origin at the eye
  (`StandingOn`, `StandingEye`) and hides other bodies. The rivers' water, beds, and banks
  around the eye are worked out again on workers as it moves (`BOD-11`).
- **Ground tiles** (owner's plan, 2026-10-08): Core `Geometry/GroundTile.cs`,
  `GroundTileGrid` (layout, skirt, morph pairs), `GroundTileSelection` (which tiles, finer near
  the eye, a parent standing in until all four quarters are built, but a tile just come into
  the far half of the reach left out until it's built: PR #111), `GlobeTileSurface` (the six
  cube faces) and `FlatTopTileSurface` (a flat world's top face). `Rendering/GroundTiles.cs`
  builds tiles on worker threads from a `GroundTileRecipe` (heights, and the water's over
  them), keeps 600 for reuse, and joins the drawn ones into one ground mesh and one water mesh
  (nearest first, also on a worker; 2 draw calls instead of a few hundred). Points morph onto
  the coarser tile's shape before it takes over (`ground_morph` in `planet_surface.gdshaderinc`,
  the distance in `CUSTOM1.w`); skirts hide any crack. The tiles are the same at every detail
  (finest 64 m); `StandingGroundDetail` sets 8, 16, or 32 squares a tile, in the presets too.
  The joined meshes are packed on the worker the way the engine keeps them
  (`Rendering/PackedSurface.cs`, checked against the engine's own packing at start), so the
  main thread only uploads them. The rim and underside of a flat world are still a
  `Rendering/FlatPatch.cs` of bare rock.
- `Rendering/SurfaceSky.cs` + `surface_sky.gdshader` (a copy of the environment while standing;
  sky gradient, bodies as lit discs, clouds from the live weather via
  `PlanetSurface.CopyCloudsTo` and `cloud_layer.gdshaderinc`, haze as fog), `cloud_deck.gdshader`
  (clouds seen from above), `falling_weather.gdshader` (rain and snow over the view).
- `UI/FirstPersonHud.cs`, `UI/CompassStrip.cs`. Fog (G), Clouds (K), and Night Vision (N)
  switches under the Calendar button: `SurfaceSky.ShowFog`, `SurfaceSky.ShowNightVision` (more
  ambient light and exposure, scaled by how dark it is); Clouds is the same switch as View ▸
  Clouds (`WeatherDisplay.ShowClouds`). All three are remembered on this computer
  (`AppSettings.StandingFog`, `ShowClouds`, `NightVision`; owner's choice). The view's visibility of hidden UI is noted before any is
  hidden, so the terrain brush still works after leaving.
- **Ground detail** (`eye_level_ground` in `planet_surface.gdshaderinc`): three layers of noise
  matched to the ground **by its color** (grass, sand, snow, water, rock), placed in double
  precision by `FirstPersonMode.SetGroundDetail`; bumps shade the color rather than tilt the
  normal.
**Reuse:** `SkyView` for anything about what's in a world's sky; `GroundTiles` for anything
drawn at the ground's height around the eye (give it a recipe); `FirstPersonGround.Build` for a
shell at a set height (the cloud deck).
**Limits:** the ground from eye level is only as sharp as the map (an 8k map is about 5 km a
pixel on an Earth-sized planet). On arriving, or after a big climb, the ground sharpens over about 1–2 s, coarser
ground standing in meanwhile; handing over the joined tiles takes the main thread about 5–12
ms on Standard and 15–30 ms on High (up to ~100 ms at worst), at most five times a second
while moving fast.

**REN-07 — Designed night skies** · Implemented (M34: PR #90) · Base
**Intent:** Each world has its own fixed night sky that the user designs: the stars stay put
from night to night, and the user draws and names constellations in them (owner's request,
2026-10-05; owner's choice: a star field from a seed that can be re-rolled, with named
constellations drawn by joining stars).
**Owner's choices:** stars optionally from orbit (View ▸ Stars from Orbit, off) and
constellation lines in both views (on).
**Built:** Core `Simulation/StarField.cs` (cube-face cells, 128² a face, at most one star each,
its id the cell; about 4,900 stars from the seed). **It must never change**: constellations name
stars by id; the rules are in docs/world-format.md. `Model/Constellation.cs`, `StarLink.cs`
(format v28: `starSeed`, `constellations`). `Rendering/StarSky` (images made in the
background) and `star_sky.gdshaderinc`; `UI/SkyPage.cs`, `UI/SkyCanvas.cs` (the design page);
`WorldSession.Sky.cs`. New Stars only works once there are no constellations.

**REN-08 — Map and time while standing** · Implemented (M41: PR #97) · Base
**Intent:** Standing on a world, a minimap shows where the view is on the larger map (and can
take you elsewhere with a click), and the clock and calendar can be run and read just as from
the system view (owner's request, 2026-10-08).
**Owner's choices:** a round, zoomable overhead minimap, north up, with your arrow; click to
travel; the time bar stays and the Calendar opens over the view (button or T).
**Built:** `UI/Minimap.cs` (a second orthographic camera in its own 256 px `SubViewport`,
`FirstPersonMode.BuildOverhead`, with its own even lighting); `GlobeWalk.FromOverhead` and
`FlatWalk.Walk` for clicks; `TimeControls.KeepShown`, `CalendarPanel.KeepShown`.
**Reuse:** `Minimap` takes any picture; `GlobeWalk` for moving over a globe.

### 4.2 Interface Layout (`UI`)

**UI-01 — Main screen layout** · Implemented (M10: PRs #31–#32; fixes M15: PR #41) · Base
**Intent:** The main view shows the world or map in the center. Panels along the sides of the
screen hold tools, journals, and similar content.
**Notes:** Views that need a lot of space, like large diagrams, may need their own tab or page
(see `LORE-04`).
**Owner's choices:** a menu bar plus one row of panel buttons; map tools in one Map panel on the
right; a View menu for show/hide switches; the window starts maximized and still works in small
windows. While a text or number field is edited the camera ignores the keys; Up/Down step
numbers (owner's request, PR #18).
**Built:** `UI/MapToolbar.cs` (menus, panel buttons, the message line `ShowInfo` /
`ShowWarning` / `ShowError`, the hint bar), `UI/ViewMenu.cs`, `UI/AddMenu.cs`, `UI/MapPanel.cs`
(with `UI/MapImageSection.cs`), `UI/NumberFields.cs`, `UI/Dropdown.cs` (opens when the click
ends, so a short window's list can't pick by accident), `UI/PanelStyle.cs` (solid panel
backgrounds).
**Reuse:** the message line and `PanelStyle` for any new panel.

**UI-02 — System tree panel** · Implemented (PR #18) · Base
**Intent:** A compact tree view of the star system's hierarchy (e.g. Sun ▸ Planet ▸ Moon) showing
which bodies orbit which. It likely lives in a side panel and doubles as a way to select bodies for
editing. This is separate from lore relationships (`LORE-04`).
**Owner's choices:** on the left; deleting a body deletes what orbits it (no confirmation,
Ctrl+Z restores); the selected path drawn while editing and fields applying as typed;
full-circle angles wrap; **Make Center** swaps places and keeps the motion.
**Built:** `UI/SystemPanel.cs` (now in folding sections, `UI-07`); Core
`Simulation/NewBodies.cs` (starting values), `SystemHierarchy` (`ChildrenOf`,
`DescendantsOf`, `MakeCenter`), `OrbitMath.EvenlySpacedTimes` (smooth paths at high
elongation); `NumberFields.WithLiveTyping` / `WithWrapAround`.

**UI-03 — Undo and redo** · Implemented (PR #14) · Base
**Intent (owner, 2026-09-30):** undo and redo for edits, "sooner rather than later", so mistakes
(a bad drag, a deleted piece) are easy to take back. The **Delete** key removes the selected
piece.
**Owner's choices:** covers all world edits; an Edit menu naming what will be undone, plus
Ctrl+Z and Ctrl+Y / Ctrl+Shift+Z; deleting doesn't ask first; **saved means "matches the
file"** (undoing back to it counts as saved).
**Built:** snapshots, not per-edit undo code: `WorldSession` records the state in Core
`Editing/UndoHistory<T>` before each edit (`RecordUndo("…")`); `BeginGesture` / `EndGesture`
make a whole drag one step; quick repeats within a second merge; 100 steps. Images only the
history needs are kept in `Core/Storage/AssetStash.cs`. `UI/EditMenu.cs`.
**Reuse:** every new edit gets undo by calling `RecordUndo` first; world-wide state that isn't a
body goes in `LoreState`.

**UI-04 — Metric and imperial units** · Implemented (M32: PRs #82–#83) · Base
**Intent:** A setting to switch everything between the imperial and metric systems for
measuring distance, mass, volume, area, temperature, and so on (owner's idea, 2026-10-05).
**Owner's choices:** a per-computer setting in File ▸ Settings, never saved in worlds (which
store metric); both readouts and typed fields switch; astronomical units stay; the default
follows the computer's region.
**Built:** Core `Measurement/UnitSystem.cs`, `Quantity.cs`, `Units.cs` (`ToShown`, `ToMetric`,
`Format`, `FormatDistance`, `DefaultFor`); `AppSettings.Units`; `UI/UnitText.cs` (readouts),
`UI/UnitFields.cs` (a number field that shows a measurement and hands back the exact metric
value unless the user changed it).
**Reuse:** `UnitFields.WithUnit` for every new measurement field, `UnitText` for every readout.

**UI-05 — Tool help** · Implemented (M34: PR #87) · Base
**Intent:** Someone picking up the app for the first time can use every tool: each tool says
in one line how to use it, and when a tool can't do something, it says why and what to do
instead of doing nothing (owner's request, 2026-10-05, after painting and sculpting seemed not
to work).
**Built:** `MapToolbar.SetHint(tool, text)` (the hint bar; the last tool given a hint shows);
`UI/DisabledTip.cs` (`Apply(button, tip, whyNot)`, shared reasons `Busy`, `NoMap`,
`NoSurface`); `MapToolbar.ShowBusyWarning()` for edits that must wait;
`TerrainBrush.ShowDone` ("Done: Paint Ocean. Ctrl+Z takes it back."). `UI/ToolSwitch.cs`: the
Tool On/Off switch (P) atop the Terrain, Regions, and Map panels (owner's request,
2026-10-08); while off, drags on the globe turn the view (`TerrainBrush.IsActive`,
`RegionEditor.ToolOn`, `MapPanel.IsToolOn` for `PieceHandles`), and `ToolSwitch.OffHint` says so.
**Reuse:** these for every new tool (CLAUDE.md §4, Usability); a `ToolSwitch` for any panel
whose tool takes drags on the globe.

**UI-06 — Start screen and standalone app** · Implemented (M34: PR #89) · Base
**Intent:** The app opens to a start screen (New World, Open World, Recent Worlds, Settings,
Quit) and can be launched on Windows by double-clicking it, without Godot (owner's request,
2026-10-05).
**Built:** `UI/StartScreen.cs` (shown at launch unless a command-line option says what to do);
recent worlds in `AppSettings.RecentWorldPaths` with the rule in Core
`Storage/RecentWorlds.cs` (8, newest first, no duplicates); `godot/export_presets.cfg`
("Windows Desktop" into `build/windows/`; how to build is in docs/development.md).

**UI-07 — Clearer look** · Implemented (M38: PR #94) · Base
**Intent:** Every panel, menu, and dialog is easy to read and operate: buttons and switches
look like what they are, panels are laid out alike, long panels fold into sections, menus are
grouped, and whatever can be shown is shown rather than described (owner's request,
2026-10-08).
**Owner's choices:** the look and small fixes first, then the calendar (M39), then presets and
date pickers (M40); nothing removed from the System panel.
**Built:** `UI/AppTheme.cs` (one theme merged into Godot's default: filled buttons, blue for the
chosen mode, real on/off switches, steady margins); `UI/FoldingSection.cs` (remembers open
sections, `AppSettings.IsSectionOpen`); `PanelStyle.FitHeight`; the View menu under headings;
"Day -1" rather than "Day 0" (`LocalTime.DayName`).
**Reuse:** `AppTheme.SectionHeader`, `FoldingSection`, and `PanelStyle.FitHeight` for any panel.

### 4.3 Maps & Image Import (`MAP`)

**MAP-01 — Import map image in a supported layout** · Implemented (equirectangular: M1; other layouts: M2, see `MAP-04`) · Base
**Intent:** Import a flat map image and wrap it onto a globe. Supported preset layouts
(map projections) can be wrapped onto a sphere with little stretching.
**Notes:** M1 needs only the simplest case: one standard layout (probably equirectangular, a
2:1 image where lines of latitude and longitude form a straight grid). Other projections are
future work.
**Owner's choices:** images over 8192 × 4096 are shrunk; images of the wrong shape are applied
with a warning; maps compressed to S3TC (~137 MB of graphics memory for an 8k map instead of
~497 MB); the grid hides when a map loads and G toggles it.
**Built:** Core `Maps/MapImageRules.cs`; `Maps/MapImageLoader.cs` (PNG/JPG/WebP up to 256 MB,
on a background thread, halving then one cubic resize, mipmaps, S3TC, falling back to
uncompressed); `PlanetSurface.SetMap`; `-- --map=<path>` imports at startup.
**Reuse:** `MapImageLoader` loads from any `IAssetSource`, including a world file.

**MAP-02 — Manual map placement onto the globe** · Implemented (M3; shapes and snapping: M34) · Base
**Intent:** For maps that aren't in a supported layout, the user cuts the image up and places it
onto the globe themselves, adjusting for distortion and distance. (Flat maps don't map one-to-one
onto spheres: areas near the equator are close to true size, and areas near the poles are stretched.)
**Notes:** Best for maps of part of a world, maps with no consistent layout, and combining
several regional maps. One of two ways to fit imprecise maps; the other is `MAP-05`.
**Owner's choices (2026-09-30 unless noted):**
- Cut shapes: rectangle and freeform; since M34 also ellipse (Shift for a circle) and regular
  shapes (3–12 sides).
- Pieces can come from any image, each remembering its source, all saved in the world file.
- Placed on the globe with handles like other map makers: click to select, drag to move, a
  corner resizes (keeping proportions), a handle rotates; number fields for exact placement.
- **Edit Points** (toggle, or double-click a piece): drag every point of the cut and the image
  stretches smoothly to follow.
- Up to 32 pieces a planet, drawn live.
- Since M34: **Snap to Grid** (the center or an edge snaps to a 15°, 5°, or 1° step; not saved)
  and **Center on Grid** for a piece or the main map.
**How pieces sit on the globe:** like a sticker. Distances from the piece's center are true
(azimuthal equidistant), so small pieces look exactly as drawn, and very large ones (over about a
quarter of the globe) stretch toward their edges. Pieces sit on top of the map; later pieces cover
earlier ones.
**Built:**
- Core `Model/MapPiece.cs` (in `SurfaceSettings.Pieces`, with `WarpedPoints`);
  `Maps/PieceOutline.cs` (immutable outlines, `RasterizeMask` with anti-aliased edges,
  `Ellipse`, `RegularShape`); `Maps/PieceProjection.cs` (globe ↔ box, using **atan2, not acos**;
  the shader must match); `Maps/PieceStartingPlacement.cs` (a cut from the main map starts
  exactly where it already shows); `Maps/PieceManipulation.cs` (`PieceAt`, `Move`, `Rotate`,
  `Resize`, each from the drag's start); `Maps/PieceWarp.cs` (mean value coordinates, baked to
  a `WarpLookup`); `GridSnap.SnapPiece`; `MapCalibration.CenteredOnGrid`.
- `planet.gdshader` `draw_pieces`; `Maps/PieceTextureBaker.cs` (crop, mask, compress);
  `UI/CutEditor.cs`, `UI/CutCanvas.cs`; `Controls/GlobePicker.cs` (screen ↔ surface);
  `Controls/PieceHandles.cs`; the pieces half of `UI/MapPanel.cs`.
**Reuse:** `PieceOutline.RasterizeMask` for any polygon mask; `GlobePicker` for anything clicked
on a globe.

**MAP-05 — Grid calibration (adjust how the map's lines project)** · Implemented (M3) · Base
**Intent:** Raised by the owner while testing `MAP-04`. The built-in map types require the image
to follow their layout exactly, but most maps aren't that precise, and even a Gall–Peters map
still looked slightly pinched at the poles. The user should be able to **adjust how the
planet's horizontal and vertical lines (latitude/longitude) project onto their map**, adding
guiding lines so the wrap follows their drawing.
**Owner's choices (2026-09-30):** guide lines dragged on the flat image (pin points and a mesh
grid could come later as advanced modes); a side-by-side workspace (image left, live globe
right); all map types; default guides every 30° of latitude and 60° of longitude. After trying
it: dragging the lines "feels fine for now".
**How it works:** calibration doesn't bend the image. It changes which latitude and longitude
the map type reads ("true 30° is drawn where the map type puts 33.5°"), so one method covers
every type. Each line moves as a whole; the poles stay fixed.
**Built:** Core `Maps/MapCalibration.cs` (immutable; a monotone curve so lines never cross;
`Bake…Table` for the shader) and `Maps/MapProjectionInverter.cs` (image position →
latitude/longitude for any type); `UI/CalibrationWorkspace.cs`, `UI/CalibrationCanvas.cs`;
`WorldSession.SetCalibration`; `PlanetSurface.SetCalibration` (two lookup textures).
**Reuse:** `MapProjectionInverter` for "what's under the mouse" on any flat map.

**MAP-03 — Better wrapping for hand-drawn (flat) maps** · Implemented (Flat map mode, M2) · Base
**Intent:** Hand-drawn and fantasy-tool maps (e.g. Inkarnate, Wonderdraft) should look right on
the globe even when they're 2:1. Found by the owner while testing `MAP-01`: such maps look
**pinched toward the poles**.
**Why it happens (not a bug):** the equirectangular layout is stretched sideways toward the
poles, and wrapping onto a globe undoes that stretch; flat-drawn maps don't have it built in.
**Owner's choices (2026-09-30):** a **Flat map** map type that treats the image as Mercator
(shapes look as drawn); new imports default to it; beyond its coverage the poles take a
**Fill color** the user picks, with a soft 3° blend.
**Built:** Core `Maps/MapProjections.cs` (`ToImagePosition` for every map type,
`MercatorLatitudeLimit`); the shader mirrors it. `PlanetSurface` falls back to the shader's own
default for any setting not stored yet (`GetParameter`).
**Reuse:** `MapProjections.ToImagePosition` to find the map pixel under any globe position.

**MAP-04 — Atlas map types** · Implemented (M2) · Base
**Intent:** Support popular atlas layouts as additional **Map type** options, so maps drawn in
those styles wrap onto the globe correctly. The owner chose:
- **Robinson:** classic school/wall atlas look
- **Winkel tripel:** National Geographic's world map
- **Mollweide:** full oval, equal-area
- **Polar (azimuthal):** a circle centered on a pole; also the natural layout for flat worlds
  (`BOD-02`)
- **Two hemispheres:** two side-by-side circles, old-atlas and fantasy style
- **Gall–Peters:** equal-area rectangle
**Owner's choices (2026-09-30):** Polar centered on the north pole with the equator at the rim;
Two hemispheres west on the left, split at 0° and 180°; circles spread with even spacing
(azimuthal equidistant). After testing: good defaults for precise maps, with manual fitting
(`MAP-05`, `MAP-02`) for the rest.
**Built:** `MapProjections` (`ExpectedAspectRatio`, `ShapeMatches`, `CoversWholeGlobe`); one
`project_*` function per type in the shader, with `keep_inside_map` so the background never
bleeds in at rims; the grouped Map type dropdown.
**Limits:** a map type only looks right if the image was drawn in that layout.

### 4.4 Celestial Bodies (`BOD`)

**BOD-01 — Suns, planets, and moons** · Implemented (M4) · Base
**Intent:** A system can have multiple suns and moons, plus planets.
**Owner's choices:** orbits are simple (distance, period, start), with optional elongation and
tilt; periods are set freely, not from physics.
**Built:** Core `Model/Body.cs` (`Kind`, `RadiusKm`, `DayLengthHours`, `AxialTiltDegrees`,
optional `Orbit`, its own `Surface`, `Problem()`); `Simulation/SystemHierarchy.cs` (any body can
orbit any other; loops refused). New worlds are a Sun-like star and an Earth-like planet
(`World.CreateNew`). The **selected body** (`WorldSession.SelectedBodyId`) is what the tools
edit. Only its map loads at full size; the others show a 1024-px preview (`ShowMapsAsync`).
`SystemView.Sync` rebuilds only the bodies that changed.

**BOD-02 — Non-standard bodies** · Implemented (M19: flat worlds; M21: world tree) · Base
**Intent:** Support bodies that aren't spheres, such as flat worlds and world trees.
**Owner's choices:**
- **Flat worlds:** a Shape per planet or moon (Globe or Flat world), changeable any time; the
  whole world on top (north pole at the center, the far south round the rim, like the Polar
  map), bare rock underneath; **physically flat light**: the disc tumbles like a coin, so the
  whole face is lit at once, with two summers and two winters a year and no climate zones.
- **World tree:** a new kind of body that holds **realms** (planets or moons hung on its
  branches, keeping everything of their own); grown from settings and a seed; it glows and is
  its realms' sun; each realm circles the trunk once a turn, its year.
**Built:**
- Flat worlds: `BodyShape`, `Body.Shape` (format v16); Core `Geometry/FlatDisc.cs` (the disc's
  radius is π × the globe's; distances from the center are true); `Rendering/FlatDiscMeshes.cs`;
  `flat_disc` in the shader; `Rendering/GlobeShape.cs` (places anything on either shape);
  `PlanetCamera`'s flat close-up; Core `Geometry/SurfaceDistance.cs`; flat seasons
  (`Midsummer`, `Midwinter` in `Seasons`) and weather (`ClimateYear`).
- World tree: `BodyKind.WorldTree`, `Model/WorldTreeLook.cs` (v20); Core
  `Simulation/WorldTreeShape.cs` (deterministic, from `Simulation/SeededRandom.cs`);
  `Rendering/WorldTreeVisual.cs`; `UI/WorldTreeSection.cs`. Realms: `Body.Branch`,
  `Orbit.HeightKm` (v21), Core `Simulation/Realms.cs` (locks a hung realm's orbit to its branch
  tip). Light: `Body.GivesLight` (a star or a glowing tree) is what everything that needs "the
  sun" looks for; `Seasons.SunPath` lights a realm from the trunk level with it.
**Reuse:** `SeededRandom` for any repeatable chance; `GlobeShape` for placing things on any
body shape.
**Limits:** eclipses treat a flat world as a sphere of its radius. A high realm looks lit a
little from below in the 3D view (the light shines from the tree's middle).

**BOD-03 — Other astral features** · Implemented (M20: rings, belts, nebulas) · Base
**Intent:** Asteroids, nebulas, and similar features.
**Owner's choices:** banded rings in a chosen color, with shadows both ways; a belt is one
feature on a star (its rocks drawn, not saved); nebulas are a backdrop around the whole system.
**Built:**
- Rings: `Model/PlanetRings.cs` (v17, in body radii); `Rendering/RingsVisual.cs`,
  `rings.gdshader`, bands in `ring_bands.gdshaderinc` (shared with the planet shader for the
  shadow); `UI/RingsSection.cs`.
- Belts: `Model/AsteroidBelt.cs` (v18, stars only); `Rendering/BeltVisual.cs` (up to 5,000
  rocks in one MultiMesh, moved on the GPU by `belt_rocks.gdshader`); `UI/BeltsSection.cs`.
- Nebulas: `Model/Nebula.cs` (v19, `World.Nebulas`, up to 20); Core `Simulation/NebulaSky.cs`
  paints the sky image; `Rendering/NebulaBackdrop.cs`, `nebula_sky.gdshader`;
  `UI/NebulasSection.cs`. `World.Clone` must copy them (fixed in PR #68).

**BOD-04 — Body sculpting (digital clay)** · Implemented (M24: heights, PRs #60–#64; M25: shapes, PRs #65–#67) · Base
**Intent:** Mold bodies like digital clay with brush tools. Tools include:
- Raise and lower terrain brushes
- Basic shape tools to add and subtract terrain, which are also useful for artificial structures
- Extreme shapes: hollow planets, planets with holes through them
**Owner's choices (2026-10-04):**
- Heights on the cube-sphere grid plus a list of shapes added or cut, rather than a full voxel
  model.
- **Heights:** Raise, Lower, Smooth, and Flatten brushes; ±32 km in 1 m steps, true to scale; a
  View setting exaggerates the relief, and View ▸ Relief Shading offers a map-style light from
  the northwest.
- **Shapes:** sphere, box, cylinder, and cone, each added or cut, placed by clicking and
  handled on the globe; bare rock on their faces. Built with Godot's own CSG. Shaped worlds
  carve a coarser globe (32 squares a face), and a dragged shape shows as a see-through
  preview, carved when let go.
**Built:**
- Core `Model/HeightGrid.cs` (one height per terrain cell, immutable 64 × 64 tiles; `SampleAt`,
  `SampleSteepAt`, `Sparse`), brushes along a stroke path (`StrokePath`), `CubeGridBrush`
  (shared with terrain), `Storage/GreyscalePng` (16-bit; format v23 `heights/<id>.png`).
- `Rendering/CubeSphereMesh.cs` (welded cube-sphere; squares a face from Relief Detail); heights
  as a six-layer half-float texture, lifted in the vertex stage; `PlanetSurface.SetHeights` sends
  only changed faces; `PlanetSurface.SurfaceRadiusAt` keeps pins, regions, and clicks on the
  raised ground.
- `UI/TerrainPanel.cs` (Sculpt and Shapes modes), `Controls/TerrainBrush.cs`,
  `WorldSession.SculptHeights` (a stroke is redone whole from its start, one undo step).
- Shapes: `Model/ShapeEdit.cs` (`FrameOn`; v24 `surface.shapes`, up to 64);
  `Rendering/ShapedGlobe.cs` (a hidden `CsgCombiner3D`, copied to a plain mesh once carved;
  `RayHit` so clicks reach into holes); `Controls/ShapeHandles.cs`, `UI/ShapesSection.cs`,
  `Session/WorldSession.Shapes.cs`. Lifting the globe before carving and the hit meshes after
  it run on a worker thread; the toolbar says "Carving shapes…" meanwhile
  (`ShapedGlobe.CarvingChanged`).
**Limits:** Godot only carves in the scene, on the main thread, so each change still pauses
the app (~0.17 s on the desktop); fully removing it needs carving code of our own.

**BOD-05 — Terrain/biome painting** · Implemented (M11: PRs #33–#35) · Base
**Intent:** Paint terrain types onto bodies, such as ocean, mountains, swamps, forests, and fields.
**Owner's choices:** an editable list of types per world (12 defaults); without a map the terrain
is the surface, over a map a see-through overlay (View ▸ Terrain); its own Terrain panel; a grid
of 1,024 × 1,024 cells a face (about 10 km on an Earth-sized planet); unpainted ground drawn
grey once a planet without a map has any terrain; a Tool On/Off switch (P) for painting,
sculpting, and shapes, so drags can turn the view (owner's request, 2026-10-08).
**Built:** Core `Geometry/CubeSphere.cs` (equal-angle cube faces; the shader repeats its math,
spelled out in docs/world-format.md); `Model/TerrainGrid.cs` (immutable 64 × 64 tiles shared
between versions; `Paint`, `PaintStroke`, `Replace`, `FacesChangedFrom`, `TilesChangedFrom`);
`Model/TerrainType.cs`;
`Storage/TerrainImage.cs` (format v10, `terrain/<id>.png`). Drawn as a six-layer byte texture
plus a palette, with smooth edges up close (`terrain_near`) and an averaged far copy so it never
flickers (kept per face in `PlanetSurface`, so a stroke averages only the tiles it touched); `Session/WorldSession.Terrain.cs`; `Controls/TerrainBrush.cs`; `UI/TerrainPanel.cs`.
The tool switch is a `ToolSwitch` (UI-05); Water mode has its own buttons, so it's off there
with a tooltip saying so.
**Reuse:** `TerrainGrid`'s shared-tile pattern for any per-cell data (heights use it).

**BOD-06 — Custom surface appearance** · Implemented (M16: PR #42) · Base
**Intent:** Custom textures/appearance for bodies beyond defaults (see also `MAP-01`).
**Owner's choices:** planets and moons get a color and a pattern (Plain, Rocky, Banded, Icy,
Cloudy), shown where there's no map; stars are picked by type, which sets their light; moons
default to grey and rocky.
**Built:** `Model/BodyAppearance.cs` (format v13); `surface_look` in the shader, reading a small
repeating 3D noise texture (`Rendering/PatternNoise.cs`) offset per body;
`WorldSession.SetAppearance`.
**Reuse:** `PatternNoise` for any cheap noise in a shader.

**BOD-07 — Terrain shapes the ground** · Implemented (M35: PR #91) · Base
**Intent:** Terrain painting and sculpting work together: painting a type (a mountain range,
an ocean) raises or lowers the ground to that type's default height, custom types set their
own, and where different types meet the heights merge, anything from a smooth slope to a
sheer cliff, so the world looks real without sculpting every slope by hand (owner's request,
2026-10-06).
**Owner's choices:** layered (each type's height, sculpting on top); each type has an Edge from
Gentle to Cliff and the steeper edge wins; a per-world switch, off for existing and new worlds;
standing shows true heights; erasing returns the ground to 0; flying keeps its height above the
planet's radius, not the ground.
**Built:** `TerrainType.HeightMeters`, `Edge`; `World.TerrainShapesGround` (format v29). Core
`TerrainRelief` works out the shaped ground a tile at a time (a chamfer distance transform, then
easing between heights; `Update` for a stroke, `Rework` for a changed type, `Shaped` adds the
sculpting and shares unchanged tiles); only the sculpting is saved. `HeightGrid.Flatten` and
`Smooth` work on the ground seen. Cliffs stand up close through `HeightGrid.SampleSteepAt`.
The session keeps each body's shaped ground (now prepared in the background, `REN-03`).

**BOD-08 — Variation within terrain** · Implemented (M36: PR #92) · Base
**Intent:** The ground a terrain type shapes isn't one flat height: mountains have peaks,
hills roll, and plains are a little uneven, with how high and how far apart the features are
set per type (owner's request, 2026-10-06).
**Built:** `TerrainType.VariationMeters`, `FeatureSizeKm` (format v30). Core
`Model/TerrainNoise.Offset`: seeded value noise, rolling for small variations and ridged from
400 m. **Its output must never change** (only the settings are saved; the recipe is in
docs/world-format.md). Each body and type has its own seed (`TerrainRelief.SeedFor`).
Features are never finer than a few grid cells. Worked out across the CPU's cores.
**Reuse:** `TerrainNoise` for any seeded, place-fixed bumpiness on a globe.

**BOD-09 — Water** · Implemented (M36: PR #92) · Base
**Intent:** A planet or moon can have water at a level the user sets: wherever the ground is
below it there's a water surface, seen from orbit and from the ground, and standing or flying
beneath it in first person looks like being underwater (owner's request, 2026-10-06).
**Owner's choices:** water over a Water-climate terrain type takes that type's color, darker
with depth; over land it's a standard blue-green.
**Built:** `Body.WaterLevelMeters` (format v30), `WorldSession.SetWater`. From orbit the globe is
lifted to the water in `planet_surface.gdshaderinc` (colors in `water_tint.gdshaderinc`,
`PlanetSurface.SetTerrainColors`); from the ground the water's surface on the ground tiles
(`GroundTiles`) with `water_surface.gdshader`. Underwater: murk (`SurfaceSky.ShowUnderwater`), a wavering tint
(`UnderwaterView`, `underwater.gdshader`), and light ripples (`caustics`).
A globe or disc carved by shapes (`ShapedGlobe`) is raised to the water before carving, so a
shape cut below the water is a dry pit (owner's choice, 2026-10-08); first person stands on the
ground under it (`ShapedGlobe.GroundHit`).

**BOD-10 — Flat worlds in relief** · Implemented (M37: PR #93) · Base
**Intent:** A flat world can have everything a globe has on its surface: sculpted and
terrain-shaped heights with their peaks, water up to a level (with a waterfall where it reaches
the rim), and shapes added to or cut out of it (owner's request, 2026-10-07).
**Owner's choices:** everything globes have; a waterfall at the rim (the south pole, so wet all
the way round or not at all) fading to mist below the disc; shapes too.
**Built:** no new data. `FlatDiscMeshes.TopRelief` lifted by the shader (`flat_relief_normal`),
never below `PlanetSurface.FlatDeepestLift`; `PlanetSurface.UpdateRim` and
`FlatDiscMeshes.RockLifted` keep the rim meeting the face; `RimWaterfall`,
`rim_waterfall.gdshader`; `ShapeEdit.FrameOn(radiusKm, BodyShape.FlatDisc)`.
**Limits:** the disc's map is stretched toward the rim.

**BOD-11 — Rivers and lakes** · Implemented (M42: PRs #98–#100; faster: PRs #102–#103;
following the ground: PR #112; depth: PR #113) · Base
**Intent:** Rivers you can draw, or that find their own way downhill from a source until they
reach water, and lakes standing at a height of their own that can flow out into rivers, so a
world's water looks natural or exactly as designed (owner's request, 2026-10-08).
**Owner's choices:** rivers are lines saved with the world, widening downstream; **drawn** ones
keep their clicked course, **natural** ones run downhill by the path of least resistance and
re-trace when the ground changes; lakes fill from a click to their own surface height; **Flows
Out** feeds a natural river from a lake's lowest shore. Up close: banks carved into the ground,
depth from width (a twentieth, 1–20 m), ripples flowing downstream with rapids, lakes you can
swim in, and smooth banks. The water **follows the ground** it's drawn on, its channel only as
deep as the river, even uphill, and it's **carved at every distance** (2026-10-08). Each
river's **depth** is Auto or set (at its mouth, shallower toward its source like its width), and
its bed can rise and fall by ± meters every so many km, worn smooth unless made sharp (steps
like weirs); only the bed changes, so the water, its width, and the banks above it stay as
Auto's, with steeper banks below the water (2026-10-09).
**Built:**
- Data (format v31): `Model/River.cs`, `Model/Lake.cs`, `Model/WaterRules.cs`. Only these are
  saved; the water is worked out from the ground each time (rules in docs/world-format.md).
  Depth (v32): `Model/RiverDepth.cs`; the bed's rises and falls are `RiverBedNoise` (seeded by
  the river's id; **its output must never change**, recipe in docs/world-format.md).
- Core `Simulation/`: `WaterCells` (the height grid's cells and neighbors), `LakeFill` (`Fill`,
  `HollowBelow`), `RiverCourse.Trace` (steepest descent, and out of hollows by the way that
  climbs least), `BodyWater.For` (one body's sea, lakes, lake levels, and rivers). They borrow
  `WaterScratch` (marking arrays) and use `HeightQueue`; their results are fixed by the docs, so
  any speed-up must give identical courses (`SimpleRiverCourse` in the tests checks this).
- Up close: `RiverLine`; `RiverProfile` (width, depth, heights, speed, rapids, every 200 m;
  the water a share of its depth below the lowest of the ground at its middle and its
  edges; finer steps, down to 50 m, where its bed varies; the water's half-width, which a deeper
bed doesn't change); `RiverCarving` (the channels along whole courses, the same wherever the eye is, found
  through a tree of balls around each river's segments; ground tiles sunk under the strip by
  their squares' width, and further under banks steeper than Auto's, but not for rivers
  narrower than 2% of a square); `RiverChannels`
  (the stretches near the eye); `RiverBanks` (a fine strip for the beds and banks).
- `Session/WorldSession.Water.cs` (edits with undo; works out water and lake images in the
  background); `Rendering/RiverRenderer.cs` (from orbit), `RiverWater.cs` +
  `river_water.gdshader`, `RiverBankStrip.cs` (both worked out on workers:
  `FirstPersonMode.StartChannels`, `StartBanks`; the strip meets `GroundTiles.ShownHeights`);
  `PlanetSurface.SetLakeLevels`, `WaterRadiusAt`, `GroundRadiusAt` and `GroundLiftAt` (the
  ground up close, in full precision);
  `UI/WaterSection.cs` (the Terrain panel's Water mode, with a river's depth settings).
**Limits:** courses are a height cell apart (about 10 km on an Earth-sized world); on perfectly
level ground a river's way out runs in straight lines; on a flat world, rivers away from the
north pole are stretched east–west as the disc's map is. Up close, a river runs uphill where
the ground rises along its course (owner's choice). Far off, a river narrower than about 2% of
the ground's squares there isn't cut into them (it's thinner than a pixel by then). A step in a
bed is spread over one of the profile's steps (an eighth of its spacing, at least 50 m), so the
sharpest "weirs" are short ramps.

### 4.5 Orbits & Simulation (`SIM`)

**SIM-01 — Designed ("on-rails") orbits** · Implemented (M4; edited in the System panel) · Base
**Intent:** The default. Bodies follow the paths the user sets, and they stay stable forever.
**Built:** Core `Model/Orbit.cs` (immutable: parent, distance, period, start angle, optional
eccentricity, closest-approach direction, tilt, tilt direction); `Simulation/OrbitMath.cs`
(position at any time directly, by Kepler's equation; deterministic);
`SystemPositions.At` (stacks parents' offsets in full-precision `Geometry/Vector3D`). The exact
math is in docs/world-format.md so any viewer can reproduce positions.

**SIM-02 — Time simulation** · Implemented (M4) · Base
**Intent:** A world clock that advances the system. It lets the user track dates and the positions
of suns and moons at any point in time.
**Owner's choices:** steps and jumps **glide** the clock over 0.8 s so bodies sweep along their
orbits; steps are measured on the selected body (its days, its year).
**Built:** `World.TimeDays` (saved, but not an unsaved change); Core `Simulation/BodyClock.cs`
(spin angle, local time, `YearDays`, `Describe`); `UI/TimeControls.cs` (the time bar, now the
calendar-style one of `CAL-05`); `WorldSession.SetTime`; `TimeControls.GlideTo`.

**SIM-03 — Physics mode (toggle)** · Implemented (M23) · Base (owner's choice, 2026-10-04: light enough, behind its own switch)
**Intent:** An optional toggle that simulates the system with real-world physics, so the user can
see how their system might fall apart.
**Owner's choices:** start speeds from gravity (a well-built system holds together); only the
3D view follows physics (seasons, calendars, eclipses, and weather stay designed); switching off
returns to the design; **Keep as Orbits** turns the simulated paths into designed orbits;
collisions merge the smaller body into the bigger and are listed.
**Built:** Core `Simulation/GravitySimulation.cs` (leapfrog steps of 1/200 of the quickest
orbit; deterministic, interpolating between its own steps; checkpoints to go back; collisions;
realms ride their branches), `Simulation/OrbitElements.cs` (`KeepAsOrbits`, the inverse of
`OrbitMath`), `SystemLayout.At` (draws any true positions). `Session/PhysicsMode.cs` runs it on
its own thread and publishes `PhysicsSnapshot`s; the main thread never waits.
`UI/PhysicsSection.cs`; the switch in the time bar.

**SIM-04 — Stable orbit guide** · Implemented (M22) · Base
**Intent:** For a selected body, show a guiding path for where stable orbits would be.
**Owner's choices:** mass from a **Density** per body (typical for its kind and size until set);
show the moon zone (Roche limit to half the Hill sphere), neighbors' reach, warnings, and
gravity's period with a button to use it; shaded bands in the 3D view (green steady, red not),
switched in the View menu.
**Built:** `Body.DensityGramsPerCm3` (format v22); Core `Simulation/BodyMass.cs`,
`Simulation/OrbitStability.cs` (Roche limit, Hill sphere, `MoonZone`, `Reaches`,
`NaturalPeriodDays`, `Warnings`), `Simulation/OrbitGuide.cs` (the rings);
`Rendering/OrbitGuideVisual.cs`; `UI/OrbitGuideSection.cs`; `WorldSession.SetDensity`.
**Reuse:** `BodyMass` and `OrbitStability` for anything that needs real gravity.

### 4.6 Time & Calendar (`CAL`)

**CAL-01 — User-defined calendar** · Implemented (M5) · Base
**Intent:** The default. The user defines their calendar, and it doesn't have to be
astronomically accurate. Some users won't care about the calendar at all, so it must be optional.
**Owner's choices:** calendars belong to bodies (any planet or moon); named months of any
length, named weekdays, year numbering with an optional era, and the date at time 0.
**Built:** Core `Model/Calendar.cs` (immutable, `Body.Calendar`, format v6),
`Simulation/CalendarMath.cs` (`DateOf`, `DayIndexOf`, `Format`, `AddMonths`, `AddYears`);
`UI/CalendarDialog.cs` (the editor; Save is one undo step); `WorldSession.SetCalendar` (the one
edit path).

**CAL-02 — Calendar accuracy mode (toggle)** · Implemented (M12: PR #36) · Base
**Intent:** An optional toggle that makes the world fit the calendar. The tool adjusts the
system's parameters (e.g. orbital periods, rotation speed) so the user's calendar becomes accurate.
**Owner's choices:** a lasting switch per calendar; the user picks what changes (the year's
length or the day's); optionally a moon kept to one month (the average month).
**Built:** `Calendar.Fit`, `MonthMoonId` (format v11; the math in docs/world-format.md); Core
`Simulation/CalendarFitting.cs` (`Apply`, `Problem`, `Preview`, `FittedBy`). The session
re-applies fits after every system change, in the same undo step; fitted fields are locked in
the System panel.

**CAL-03 — Solstices, equinoxes, and seasons** · Implemented (M5) · Base
**Intent:** Derived from the system's configuration.
**Owner's choices:** shown in the time bar, as a year of events in the System panel with Go to
buttons, and as markers on the orbit.
**Built:** `Body.AxialTiltDirectionDegrees`; Core `Simulation/BodyOrientation.NorthPole` (the one
rule for the axis; the renderer uses it too), `Simulation/Seasons.cs` (the star's declination;
solstices at its peaks, equinoxes at its zero crossings), `Simulation/SeasonTimeline.cs` (two
years either side, cached by `WorldSession.SelectedSeasons`); `UI/CalendarSection.cs`,
`UI/BodyMarkers.cs` (orbit markers, skipped off screen), `UI/SeasonText.cs` (wording).

**CAL-04 — Leap years** · Implemented (M17) · Base
**Intent:** Calendars can add leap days on a regular rule, so they can follow the real year
without the world being changed to fit.
**Owner's choices:** a rule "every N years, except every M, but every K"; leap days added to a
chosen month; a Suggest button.
**Built:** Core `Model/LeapRule.cs` (`Suggest`), `Calendar.Leap`, `AverageDaysPerYear` (format
v14); `CalendarMath` counts leap years arithmetically; `UI/LeapYearSection.cs`.

**CAL-05 — Calendar view** · Implemented (M39: PR #95) · Base
**Intent:** Time is shown as a calendar, as fantasy-calendar.com does: a month or year grid
for the selected world with the moons' phases, seasons, eclipses, meteor showers, and events
on each day, and a time bar that shows the date in the world's own calendar and steps by day,
month, or year (owner's request, 2026-10-08).
**Owner's choices:** a Calendar tab in place of Timeline (click a day to go there, double-click
to add an event; the timeline stays as one of its views); a calendar-style time bar replacing
the step menu and the Go to box.
**Built:** Core `Simulation/MonthPage`, `TimeSteps.Apply`, `MoonPhase.Of`; `UI/CalendarPanel.cs`
(Month, Year, and Timeline views; Next Solar/Lunar Eclipse buttons), `UI/MoonIcon`,
`UI/ClockFace`, `UI/TimeControls.cs`. The hint bar moved to the top.

**CAL-06 — Calendar presets and date pickers** · Implemented (M40: PR #96) · Base
**Intent:** A calendar can start from a ready-made one, and every date is picked from a
calendar rather than typed as a day number (owner's request, 2026-10-08).
**Owner's choices:** four presets (fitted to this world, Earth's Gregorian, thirteen months,
ten-day weeks); date pickers everywhere a date is asked.
**Built:** Core `Model/CalendarPresets.cs`; a Start From row in `CalendarDialog`;
`UI/DatePicker.cs` (used by `DateFields` in the event and relationship editors and for the
calendar's start date).
**Reuse:** `DatePicker` / `DateFields` for any new date.

### 4.7 Events (`EVT`)

**EVT-01 — Eclipses** · Implemented (M6) · Base
**Intent:** Simulated from body positions.
**Owner's choices:** the selected body's eclipses (solar seen from it, lunar of its moons; a
moon sees its planet's); shown as a list with Go to and as markers on the moon's orbit (only
each moon's previous and next); type, times, and coverage. Not yet: where on the surface an
eclipse is seen.
**Built:** Core `Simulation/Eclipses.cs` (umbra, antumbra, penumbra; finds and refines close
passes), `Simulation/EclipseTimeline.cs`, shared `Simulation/OrbitChain.cs` and
`Simulation/TimeSearch.cs`. `WorldSession.SelectedEclipses` works them out in the background
(`EclipsesReady`). `UI/EclipseSection.cs`, `UI/EclipseMarkers.cs`, `UI/EclipseText.cs`.
Eclipses are never saved.
**Reuse:** `TimeSearch` for refining any moment; the background pattern of `SelectedEclipses`
for any slow derived result.

**EVT-02 — Meteor showers and asteroid events** · Implemented (M18, M20) · Base
**Intent:** Simulated celestial events.
**Owner's choices:** **comets** are a new kind of body with a glowing tail away from the star;
**showers** happen where a planet's orbit passes near a comet's, at the same point every year,
their strength from the comet's size, shown as a list, a line in the time bar, and orbit
markers; **asteroid events** are worked out from the belts, the same every time for a world,
and impacts are only listed.
**Built:** `BodyKind.Comet` (format v15), `Body.HasSurface`, Core `Simulation/CometTail.cs`,
`Rendering/CometTailVisual.cs`, `comet_tail.gdshader`; Core `Simulation/MeteorShowerTimeline.cs`
(found once, repeating yearly; worked out in the background), `UI/MeteorShowerSection.cs`,
`UI/MeteorText.cs`; Core `Simulation/AsteroidEvents.cs` (SplitMix64 seeded by body, belt, and
year), `UI/AsteroidEventsSection.cs`.

### 4.8 Weather & Climate (`WTH`)

**WTH-01 — Weather pin** · Implemented (M9) · Base
**Intent:** Drop a pin on a region to see what its weather would be, based on climate zone,
season, etc. This is the lightweight version that works on any system.
**Owner's choices:** sun and temperature (daylight, sun height, a temperature range through
the year) from each body's average temperature; a pop-up with a year chart; pins named and
saved, placed with a click.
**Built:** Core `Simulation/ClimateYear.cs` (daylight and sun exact from the simulation;
temperatures an estimate around `Body.AverageTemperatureC`), `Model/WeatherPin.cs` (format v9);
`UI/WeatherMarkers.cs`, `UI/WeatherWindow.cs`, `UI/WeatherChart.cs`,
`Session/WorldSession.Weather.cs`.

**WTH-03 — Terrain-aware weather** · Implemented (M14: PRs #39–#40) · Base
**Intent:** Weather pins take the painted terrain around them into account (owner's choice,
2026-10-03).
**Owner's choices:** a climate kind per terrain type (Water, Open land, Forest, Desert, Wetland,
Mountains, Ice); temperature and rainfall; water counts within about 500 km, other kinds where
the pin stands; one line in the window explains the effect.
**Built:** `Model/ClimateKind.cs`, `TerrainType.Climate` (format v12); Core
`Simulation/TerrainSurroundings.cs`; `ClimateYear` takes it (water softens and delays seasons,
ground shifts the swing and mean) and gives `RainMm` (a tropical belt following the sun, a
storm belt, drizzle, scaled by cold and moisture); `UI/ClimateText.cs`.

**WTH-02 — Live weather simulation** · Implemented (M27: PRs #71–#73) · Base (owner's choice, 2026-10-04; was Advanced)
**Intent:** As detailed as possible. Ideally the user can watch clouds and weather move across
the world.
**Owner's choices:** worked out from the time, not stepped (any date's weather at once, always
the same); clouds, rain and snow, winds, and weather pins reporting today's weather; an
Atmosphere switch per body (planets on, moons off); Base tier with a Cloud Detail setting;
clouds hide while the Terrain or Map panel is open.
**Built:** `Body.HasAtmosphere` (format v26); Core `Simulation/LiveWeather.cs` (built once per
body, then `At(time)` → `WeatherMoment.SampleAt`: cloud, rain or snow, wind, temperature,
storms; `WeatherNoise`, `StormTracks`, `StormShape`). Drawn in the surface itself
(`planet_weather.gdshaderinc`) from snapshot images (`WeatherImages`, blended by
`WeatherSnapshots`); `Rendering/WeatherDisplay.cs` keeps the globes fed from another thread;
`UI/LiveWeatherText.cs`.
**Reuse:** `WeatherDisplay.WeatherOf(body)` for the weather anywhere the globe shows it.

### 4.9 Lore & Journal (`LORE`)

**LORE-01 — Region outlines** · Implemented (M8) · Base
**Intent:** Draw outlines around specific regions or locations on the world, with optional notes.
**Owner's choices:** click points around the region, then drag, add, or delete points; a colored
outline with a light fill and its name; regions are places that entries and events can be in;
deleting a body deletes its regions; a Regions toggle.
**Built:** Core `Model/Region.cs` (format v8), `Geometry/SphericalPolygon.cs` (`Contains`,
`FitsInHemisphere`, `Center`, `EdgePath`, `FillTriangles`), `LoreRules.RegionsAt` /
`PlacedIn`; `Rendering/RegionRenderer.cs`, `Controls/RegionEditor.cs` (also
`StartDrawingLine`), `UI/RegionsPanel.cs`, `UI/RegionMarkers.cs`, `UI/RegionChoice.cs`,
`Session/WorldSession.Regions.cs`.
**Reuse:** `SphericalPolygon` for any outline on a sphere; overlays should redraw only when
they have something to show.

**LORE-02 — Journal system** · Implemented (M7) · Base
**Intent:** Start with a simple journal. Entries are sortable and can be linked to locations.
Clicking a location can pop up its journal entries to read. The exact interaction is to be worked
out later.
**Owner's choices:** entries and events are separate things linked many to many; both can be
placed on a body, optionally pinned to a spot; pins placed by clicking on the globe, always
shown, with a Pins toggle; deleting a body keeps the entries placed on it, with the place
cleared.
**Built:** Core `Model/JournalEntry.cs`, `Model/LoreLocation.cs`, `Model/LoreRules.cs`
(`Problem(world)` checks all lore together; links are stored on events only; format v7);
`UI/JournalPanel.cs`, `Session/WorldSession.Lore.cs`, `Session/LoreState.cs` (lore in undo and
the saved check), `Controls/PinPlacer.cs`, `UI/PinMarkers.cs`, `UI/PlaceText.cs`.
**Reuse:** `PinPlacer` for any "click a spot on the globe" step; `LoreState` for any
world-wide state that needs undo.

**LORE-03 — Timelines** · Implemented (M7) · Base
**Intent:** Timelines of events. They become relevant once orbiting bodies are introduced, and
they're key helpers for the calendar features (`CAL-01`–`CAL-03`, `SIM-02`).
**Owner's choices:** several named timelines as colored lanes; events are a moment with an
optional end; clicking an event jumps the clock there, double-clicking edits it; deleting a
timeline deletes its events.
**Built:** Core `Model/Timeline.cs`, `Model/TimelineEvent.cs`, `Simulation/TimeRuler.cs`;
`UI/TimelineStrip.cs` and `UI/TimelineCanvas.cs` (now the Calendar tab's Timeline view),
`UI/EventDialog.cs`, `UI/DateFields.cs`, `UI/TimelinesDialog.cs`.

**LORE-04 — Lore relationship diagrams** · Implemented (M31: PRs #80–#81; extras M33: PR #84) · Base
**Intent:** Diagrams of connections between characters, factions, and nations (e.g. family trees,
alliances, rivalries). They probably need their own tab or page because of the space they take.
This is separate from the star system tree (`UI-02`).
**Owner's choices:** the boxes are journal entries (with an optional kind); set relationship
kinds plus Other in the user's own words; any number of named diagrams sharing ties; optional
start and end dates; a full-window page; ties made by dragging between boxes; Arrange lays out
a family tree, then the rest; drag entries from the list; save a diagram as a picture.
**Built:** Core `Model/LoreKind.cs`, `Relationship.cs`, `RelationshipKind.cs`, `LoreDiagram.cs`,
`DiagramPlacement.cs` (format v27), `LoreRules.ForgetEntry`, `Model/DiagramLayout.cs`;
`UI/DiagramPage.cs`, `UI/DiagramCanvas.cs` (also `ForExport` for pictures),
`UI/RelationshipDialog.cs`, `UI/LoreWords.cs`, `Session/WorldSession.Diagrams.cs`.

### 4.10 Saving (`SAV`)

**SAV-01 — Save and open worlds** · Implemented (M2) · Base
**Intent:** A world persists between sessions. It's saved to and opened from a file, with
nothing lost (CLAUDE.md §4: losing user data is the worst possible bug).
**Owner's choices (2026-09-30):** one `.nworld` file per world; the original map image copied
in unchanged; the camera view saved.
**Built:** Core `Model/` and `Storage/WorldPackage` (a zip of `world.json` plus `assets/`,
specified in docs/world-format.md; versioned, older files upgraded, newer ones refused; atomic
save: write, read back, swap, keep a `.bak`; everything read is validated).
`Session/WorldSession.cs` holds the open world: **every edit goes through it**; saves work on a `World.Clone()` in the
background. `UI/FileMenu.cs`. Golden files for every format version must keep passing.

**SAV-02 — Unsaved-changes safety net** · Implemented (M2) · Base
**Intent (owner's choice, 2026-09-30): manual saving with a safety net.** The user saves with
Ctrl+S. The app warns before closing or opening another world with unsaved changes, and quietly
keeps a **recovery copy every 5 minutes**, which it offers back after a crash.
**Built:** the Save / Don't Save / Cancel prompt before New, Open, and closing;
`Session/RecoveryService.cs` with Core `RecoveryStore` (copies in the user-data folder, deleted
once saved or knowingly discarded; a copy that fails to recover is **kept**).

### 4.11 Sharing (`SHR`)

**SHR-01 — Share a world with players** · Future · —
**Intent:** Share the world with others in a tabletop setting, e.g. players viewing the world
the DM built. This is why viewing is treated as a first-class priority. Low priority for now.
**Notes:** It could eventually grow into a Roll20-style hosted system, where the DM hosts games
and players join. The engine-independent world format and core library (CLAUDE.md §7)
keep that possible without an overhaul.

---

## 5. Open Questions

Questions raised during the project review that are still waiting on answers. When one is
answered, move the answer into the relevant entry above and remove the question from this list.

- _(none right now)_

---

## 6. Idea Inbox

New raw ideas go here first, then get sorted into a feature area once reviewed.

