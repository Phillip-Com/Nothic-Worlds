# Nothic-Worlds — Vision, Goals & Ideas

This file records **what the project is meant to be** and **what each feature is meant to do**.
When it's unclear what a feature or design was meant to do, this file has the answer.
It also records **how** each feature was implemented and where, so existing systems can be
reused instead of rebuilt.

- The owner's words are the authority on intent. If the owner hasn't confirmed an entry, mark it
  as such.
- Every feature has an ID (e.g. `SIM-01`) that code, PRs, and the Decision Log can refer to.
- Update an entry's **Status** and **Implementation** once work on it lands in a PR.

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

**Milestone 1: Planet Viewer** · Complete (PR #4 merged 2026-09-30). Split into two PRs: **A** = planet,
grid, camera, and performance tools (PR #3); **B** = map import (PR #4). Saving is out of scope:
the world file format gets its own design review.
A single planet that the user can import a map image onto, with a working camera/movement system.
The goal is to get the rough idea and the movement system in place. Nothing fancy.
- One planet (sphere) rendered in 3D (`REN-01`)
- Import an image and wrap it onto the planet (`MAP-01`, in its simplest form)
- Camera: zoom in and out, pan, and move around the planet (`REN-02`, basic form)

**Milestone 2: Maps + Saving** · In Progress (owner's choice, 2026-09-30)
- **Part 1:** `MAP-03`, better wrapping for hand-drawn maps (Flat map mode) (PR #5). This grew
  into `MAP-04`, atlas and circular map types (PR #6).
- **Part 2:** the **world save format** (`SAV-01`, `SAV-02`), so work persists between sessions.
  It's foundational, because every later feature adds data to it. Split into two PRs: the Core
  format (PR #7), then the app side (File menu, unsaved changes, recovery copies) (PR #8).
- **Then:** the map fitting tools, `MAP-05` (grid calibration) and `MAP-02` (cut and place). They
  come after saving (owner's choice), so fitting work is never lost.

---

## 4. Feature Areas

Each entry uses this format:

> **ID — Name** · Status · Tier
> **Intent:** what the owner wants it to do and why
> **Notes / open questions:**
> **Implementation:** _(filled in when built: approach, key files/modules, what can be reused)_

### 4.1 Rendering & Navigation (`REN`)

**REN-01 — 3D world rendering** · In Progress (single planet done in M1) · Base
**Intent:** Render worlds and bodies in 3D. This grew from the original "3D render of a 2D world" concept.
**Implementation (M1, PR #3):**
- The planet is a `SphereMesh` (radius 1, 128×64 segments) in `godot/Scenes/main.tscn`, with one
  sun (`DirectionalLight3D`) and soft ambient light so the night side stays readable.
- `godot/Rendering/planet.gdshader` computes latitude/longitude **per pixel from the surface
  direction**, not from the mesh's texture coordinates. It works on any planet mesh, so reuse it
  (or its math) when the sphere is replaced by a sculptable mesh. With no map loaded, it draws a
  15° lat/long grid with the equator and longitude 0 highlighted. Grid lines keep a constant
  on-screen width at any zoom.
- The axis convention (+Y north, longitude 0 faces +Z, 90° east faces +X) is shared with Core's
  `SphericalCoordinates`. Keep the shader and Core in sync.

**REN-02 — Multi-scale navigation (system → planet → local region)** · In Progress (planet camera done in M1) · Base
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
**Implementation (M1, PR #3):** `godot/Controls/PlanetCamera.cs`, `godot/UI/CameraModeIndicator.cs`
- Orbit and pan are separate controls: **left-drag orbits** around the focus point at a steady
  0.25°/pixel, and **right-drag / WASD pan**. Scroll / E / Q / + / - zoom by a percentage per
  step. Home resets.
- **Pan mode** (`PanMode`) is chosen by altitude. Below `SurfacePanMaxAltitude` (1.0 radius, where
  the planet roughly fills the screen) it's **Surface**: lat/long moves at ground speed, and the
  focus point eases back to the planet's center. Above that it's **View**: the focus point moves in
  the screen plane, keeping the grabbed point under the mouse.
- The camera looks at a focus point (planet center + view-pan offset), north stays up, and
  latitude is limited to ±89° so the view can't flip at the poles. Altitude ranges from 0.05 to
  8 radii. If orbiting around an off-planet focus point would put the camera inside the planet,
  it gets pushed back out.
- The indicator reads `PlanetCamera.CurrentAction` and `PlanetCamera.PanMode`. It updates its text
  only when something changes.
**Known issue (deferred by the owner):** when the view is slid so the planet is near the screen
edge, the planet looks stretched into an oval. This comes from the camera's wide 75° field of view.
A narrower lens (~45–50°) would reduce it but changes the look of everything else, so the owner
chose to keep 75° for now and fix it later. Possible fixes: a narrower field of view with
retuned zoom limits, or rotating the camera toward the slid planet instead of sliding it.
- All movement eases toward a target (frame-rate independent). Longitude is kept unwrapped so
  easing never takes the long way around.
- Public `Orbit` / `Pan` / `Zoom` / `ResetView` methods can be reused by other code (the benchmark
  already uses them).
- Keys are defined once in `godot/Controls/InputActions.cs`, ready to be made rebindable later.
- Position math uses Core's `GeoCoordinate` / `SphericalCoordinates`
  (`src/NothicWorlds.Core/Geometry/`). Reuse these for pins, region outlines, and anything else
  placed by latitude/longitude.

**REN-03 — Performance tiers** · In Progress (measurement tools done) · —
**Intent:** Keep requirements low with level-of-detail, quality settings, and rendering only what's
visible. Heavier features go in an opt-in **Advanced** section.
**Implementation (M1, PR #3):** measurement tools only. The tiers themselves aren't built yet.
- `godot/Diagnostics/PerformanceOverlay.cs`: F3 shows FPS, video memory, app memory, and draw calls.
- `godot/Diagnostics/Benchmark.cs`: run with `-- --benchmark`. It orbits and zooms for 10 s
  with VSync off, then prints the results. Use it to catch performance regressions.
- **Baseline (2026-09-29, Vega 10 laptop, 1920×1080 fullscreen, grid planet):** average
  **207 fps**, slowest frame 10.1 ms (99 fps), 35 MB video memory, 42 MB app memory.
  Measured with no other apps using the graphics chip. With the Godot editor open in the
  background, the same build scores ~125–130 fps, so close the editor before benchmarking. The
  camera indicator added ~12 MB of video memory (font/UI).
- **Re-measured 2026-09-29 (M1 part B, nothing else running):** the laptop scored ~155 fps
  on both the part A build and the part B build, so the 207 fps figure was a good day. Treat
  ~150–160 fps as the realistic baseline, and compare builds back to back. With an **8k map**:
  ~155 fps, **137 MB video memory** (S3TC), 45 MB app memory. The slowest frames sit around
  60–70 fps in every build.

**REN-04 — Top-down local region view** · Idea · Base
**Intent:** Zoom down to a local region and see it as a top-down terrain view.
**Implementation:** —

**REN-05 — Visual styles** · Idea · Base
**Intent:** Painterly is the default style. The goal is to let users choose other styles, such
as realistic or simple.
**Implementation:** —

**REN-06 — First-person surface view** · Future · Advanced (probably)
**Intent:** View the world from the surface in first person. It's a nice-to-have if it proves possible.
**Implementation:** —

### 4.2 Interface Layout (`UI`)

**UI-01 — Main screen layout** · Idea · Base
**Intent:** The main view shows the world or map in the center. Panels along the sides of the
screen hold tools, journals, and similar content.
**Notes:** Views that need a lot of space, like large diagrams, may need their own tab or page
(see `LORE-04`).
**Implementation:** —

**UI-02 — System tree panel** · Deferred · Base
**Intent:** A compact tree view of the star system's hierarchy (e.g. Sun ▸ Planet ▸ Moon) showing
which bodies orbit which. It likely lives in a side panel and doubles as a way to select bodies for
editing. This is separate from lore relationships (`LORE-04`).
**Notes:** Tabled. Revisit when multiple bodies are introduced.
**Implementation:** —

### 4.3 Maps & Image Import (`MAP`)

**MAP-01 — Import map image in a supported layout** · In Progress (equirectangular done in M1) · Base
**Intent:** Import a flat map image and wrap it onto a globe. Supported preset layouts
(map projections) can be wrapped onto a sphere with little stretching.
**Notes:** M1 needs only the simplest case: one standard layout (probably equirectangular, a
2:1 image where lines of latitude and longitude form a straight grid). Other projections are
future work.
**Implementation (M1, PR #4):**
- **Rules (Core, tested):** `src/NothicWorlds.Core/Maps/MapImageRules.cs`. The supported layout
  is 2:1 (±1%). Images over **8192 × 4096** are shrunk, keeping their proportions (owner
  decision). Images that aren't 2:1 are still applied, **with a warning** (owner decision). Since
  `MAP-03`, the warning applies only to Globe maps, because Flat maps accept any shape. Since
  `MAP-04`, the shape check lives in `MapProjections.ShapeMatches`, with an expected shape per map
  type.
- **Loading:** `godot/Maps/MapImageLoader.cs`. It accepts PNG/JPG/WebP up to 256 MB and runs on a
  background thread. It shrinks by repeated halving and then one cubic resize (a 16k image loads
  in ~2 s; a single Lanczos resize took ~20 s). It builds mipmaps and compresses to **S3TC**
  (owner decision: ~137 MB of graphics memory for an 8k map instead of ~497 MB, +~2 s load, slight
  blockiness). If compression fails, it falls back to uncompressed. The CPU copy is freed right
  after upload.
- **Display:** `planet.gdshader` samples the map by per-pixel lat/long, and uses a mipmap
  fix so there's no visible seam at the 180° line. `godot/Rendering/PlanetSurface.cs` applies
  or clears the map. The grid is hidden when a map is loaded, and **G** toggles it (owner decision).
- **UI:** `godot/UI/MapToolbar.cs` is the top-left toolbar with **Import Map…** (native file picker)
  and **Clear Map**, plus a message line. Success messages fade after 6 s. Warnings and errors
  stay until the next action. A failed import keeps the current map.
- **Command line:** `-- --map=<path>` imports a map at startup (used for testing and
  benchmarks).
- **Verified orientation:** a generated test map with markers at 0°/0°, 45°N, 45°E, 45°W, and
  180° shows north up, east to the right, and no seam.

**MAP-02 — Manual map placement onto the globe** · Idea · Base
**Intent:** For maps that aren't in a supported layout, the user cuts the image up and places it
onto the globe themselves, adjusting for distortion and distance. (Flat maps don't map one-to-one
onto spheres: areas near the equator are close to true size, and areas near the poles are stretched.)
**Notes:** One of the most complex features. Needs its own design review.
- **Owner, 2026-09-30** (after testing `MAP-04`): the user should be able to **cut out parts of
  their image, move them around the globe, and resize them**. This is one of two ways to fit
  imprecise maps; the other is `MAP-05`.
- Best for maps of part of a world, maps with no consistent layout, and combining several
  regional maps.
**Implementation:** —

**MAP-05 — Grid calibration (adjust how the map's lines project)** · Idea · Base
**Intent:** Raised by the owner while testing `MAP-04`. The built-in map types require the image
to follow their layout exactly, but most maps aren't that precise, and even a Gall–Peters map
still looked slightly pinched at the poles. The user should be able to **adjust how the
planet's horizontal and vertical lines (latitude/longitude) project onto their map**, adding
guiding lines so the wrap follows their drawing.
**Notes:** A likely design, to be reviewed before building: show the flat image with the grid
drawn over it, and let the user drag lines (e.g. "my equator is here", "60°N is here") on top of
the closest map type. The wrap then follows those lines. Best for whole-world maps that *almost*
match a known layout. Works alongside `MAP-02`. The adjustments are user work, so they must be
**saved** (depends on the world save format).
**Implementation:** —

**MAP-03 — Better wrapping for hand-drawn (flat) maps** · Implemented (Flat map mode, M2) · Base
**Intent:** Hand-drawn and fantasy-tool maps (e.g. Inkarnate, Wonderdraft) should look right on
the globe even when they're 2:1. Found by the owner while testing `MAP-01`: such maps look
**pinched toward the poles**.
**Why it happens (not a bug):** the supported layout (equirectangular) is deliberately stretched
sideways toward the poles, and wrapping onto a globe undoes that stretch. Flat-drawn maps don't
have the stretch built in, so land near the top and bottom gets squeezed. The equator is fine.
Verified: on a true equirectangular test map, shapes come out correct.
**Candidate approaches** (the owner picks during the design review; they can be combined):
1. **Latitude coverage:** the user sets which latitudes the map covers (e.g. 60°N–60°S). Poles
   outside that range get a fill such as ice or ocean. Simplest option, and it greatly reduces
   pinching, since most fantasy maps don't include the poles.
2. **Treat the map as Mercator:** reproject as if the map were drawn in a shape-preserving
   projection. Shapes look as drawn, but it can't reach the poles.
3. **Manual placement** (`MAP-02`): cut and position pieces by hand. Most flexible, most work.
**Chosen (owner, 2026-09-30):** approach 2, as a **"Flat map"** mode. The design review found
that approach 1 only reduces pinching (at 60° land is still squeezed to half width) and squashes
the equator vertically.
- The **map type** is chosen from a toolbar dropdown (**Flat map** / **Globe map**). It can be
  changed at any time and takes effect instantly.
- **New imports default to Flat map**, which suits most DM and fantasy-tool maps.
- **Polar caps** beyond the map's coverage are filled with a **Pole color the user picks**
  (toolbar color picker, shown only for Flat maps), with a soft 3° blend where the map ends. The
  owner first chose to stretch the map's edges to the poles, then switched after seeing the
  streaks converge at the poles on their own map.
**Implementation (M2, PR #5):**
- **Math (Core, tested):** `src/NothicWorlds.Core/Maps/MapProjections.cs` provides
  `ToImagePosition` (globe position → map pixel, for either `MapProjection`) and
  `MercatorLatitudeLimit` (a 2:1 map covers ~66.5°N–66.5°S, 1:1 ~85°, 3:1 ~51°). Reuse
  `ToImagePosition` for anything that needs to find the map pixel under a globe position, such as
  pins or region outlines.
- **Shader:** `planet.gdshader` mirrors the Core math, and fades to the fill color past the
  coverage limit. The `projection`, `map_aspect`, and fill color uniforms are set by
  `PlanetSurface.Projection` / `SetMap` / `FillColor`. (In `MAP-04`, "Pole color" was renamed
  "Fill color" because more map types leave areas uncovered.) The map sampler now
  **clamps** instead of repeating, so the top edge never picks up the bottom edge's colors.
- **Reading shader settings:** `PlanetSurface` falls back to the shader's own default for any
  setting the material hasn't stored yet (`GetParameter`). This fixed a PR #4 bug where the first
  G press did nothing before a map was loaded. Use the same pattern for any future shader
  settings.
- **UI:** the Map type dropdown and Fill color picker live in `godot/UI/MapToolbar.cs`. Messages
  state the coverage for flat maps. The 2:1 warning now applies only to Globe maps, and it
  suggests Flat map.
- **Verified:** a flat-drawn test map of equal circles stays round in Flat mode (smaller toward
  the poles) and turns into teardrops in Globe mode. The cap shows the pole color with a soft
  blend, and the picker swatch matches it.

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
**Notes:** A map type only looks right if the image was drawn in that layout (e.g. traced from a
Robinson template). For freehand maps, Flat map is usually best. Layout details (e.g. which pole
the Polar map is centered on, and how Two hemispheres splits the globe) get a short owner review
before building. Build on `MapProjections` (Core) and `map_v()` in the shader. The oval and
circle types also need horizontal math, and some parts of the image are outside the map.
**Layout decisions (owner, 2026-09-30):**
- **Polar** is centered on the **north** pole, with the **equator** at the circle's edge. The
  southern hemisphere uses the fill color.
- **Two hemispheres** puts **west on the left and east on the right**, split at 0° and 180°.
- Inside the circles, the map is spread with **even spacing** (azimuthal equidistant), so the rim
  isn't squished.
**Implementation (M2, PR #6):**
- **Math (Core, tested):** `MapProjections.ToImagePosition` covers all eight types. Also
  `ExpectedAspectRatio` (Robinson ≈1.97, Winkel tripel ≈1.64, Mollweide 2, Gall–Peters ≈1.57,
  Polar 1, Two hemispheres 2), `ShapeMatches` (5% tolerance for atlas and circular types, 1% for
  Globe), and `CoversWholeGlobe` (false for Flat and Polar, which use the fill color).
  Reference values in the tests come from a separate implementation, not the code under test.
  Mismatched images are stretched to fit, with a warning.
- **Shader:** one `project_*` function per type. Three things keep edges clean:
  - **Seams (mipmap choice):** a second, continuous copy of the position picks the mipmap level at
    the 180° line. Two hemispheres computes both circles and keeps its own.
  - **Outline bleed:** `keep_inside_map` pulls samples just inside a circle's rim or the curved
    side outlines, by the filter width, so background pixels outside the map never blend in.
    Measured: a visible dark line (brightness dip of ~10/255) at the hemisphere splits is gone.
  - **Mollweide's iterative solve** runs once per pixel (`mollweide_theta`), not four times.
    That brought it from ~118 to ~165 fps, level with the other types.
- **UI:** the dropdown is grouped (Flat / Globe, **Atlas**, **Circular**). The **Fill color**
  picker shows for Flat and Polar. Messages name the type, state the coverage, and warn with
  the expected shape when the image doesn't match.
- **Verified end-to-end:** test maps drawn with Core's formulas (a 15° grid plus markers) line up
  exactly under the app's own grid in every type. All four atlas types produce identical globes
  from very different images.
**Owner feedback (2026-09-30):** tested with a Gall–Peters map. It works, but the image has to
follow the layout very precisely, which many maps won't, and the poles still looked slightly
pinched. The owner sees these types as good defaults for precise maps, with manual fitting
needed for the rest. That led to `MAP-05` (grid calibration) and the expanded `MAP-02` (cut and
place).

### 4.4 Celestial Bodies (`BOD`)

**BOD-01 — Suns, planets, and moons** · Idea · Base
**Intent:** A system can have multiple suns and moons, plus planets.
**Implementation:** —

**BOD-02 — Non-standard bodies** · Idea · Base
**Intent:** Support bodies that aren't spheres, such as flat worlds and world trees.
**Notes / open questions:** How these interact with physics, orbits, and light/shadow.
**Implementation:** —

**BOD-03 — Other astral features** · Idea · —
**Intent:** Asteroids, nebulas, and similar features.
**Implementation:** —

**BOD-04 — Body sculpting (digital clay)** · Idea · —
**Intent:** Mold bodies like digital clay with brush tools. Tools include:
- Raise and lower terrain brushes
- Basic shape tools to add and subtract terrain, which are also useful for artificial structures
- Extreme shapes: hollow planets, planets with holes through them
**Notes:** Holes and hollows rule out simple heightmap-on-a-sphere approaches and have major
architecture implications. This needs a design review before any related data format is fixed.
**Implementation:** —

**BOD-05 — Terrain/biome painting** · Idea · Base
**Intent:** Paint terrain types onto bodies, such as ocean, mountains, swamps, forests, and fields.
**Implementation:** —

**BOD-06 — Custom surface appearance** · Idea · Base
**Intent:** Custom textures/appearance for bodies beyond defaults (see also `MAP-01`).
**Implementation:** —

### 4.5 Orbits & Simulation (`SIM`)

**SIM-01 — Designed ("on-rails") orbits** · Idea · Base
**Intent:** The default. Bodies follow the paths the user sets, and they stay stable forever.
**Implementation:** —

**SIM-02 — Time simulation** · Idea · Base
**Intent:** A world clock that advances the system. It lets the user track dates and the positions
of suns and moons at any point in time.
**Implementation:** —

**SIM-03 — Physics mode (toggle)** · Idea · Advanced (probably)
**Intent:** An optional toggle that simulates the system with real-world physics, so the user can
see how their system might fall apart.
**Implementation:** —

**SIM-04 — Stable orbit guide** · Idea · —
**Intent:** For a selected body, show a guiding path for where stable orbits would be.
**Implementation:** —

### 4.6 Time & Calendar (`CAL`)

**CAL-01 — User-defined calendar** · Idea · Base
**Intent:** The default. The user defines their calendar, and it doesn't have to be
astronomically accurate. Some users won't care about the calendar at all, so it must be optional.
**Implementation:** —

**CAL-02 — Calendar accuracy mode (toggle)** · Idea · Base
**Intent:** An optional toggle that makes the world fit the calendar. The tool adjusts the
system's parameters (e.g. orbital periods, rotation speed) so the user's calendar becomes accurate.
**Implementation:** —

**CAL-03 — Solstices, equinoxes, and seasons** · Idea · Base
**Intent:** Derived from the system's configuration.
**Implementation:** —

### 4.7 Events (`EVT`)

**EVT-01 — Eclipses** · Idea · Base
**Intent:** Simulated from body positions.
**Implementation:** —

**EVT-02 — Meteor showers and asteroid events** · Idea · —
**Intent:** Simulated celestial events.
**Implementation:** —

### 4.8 Weather & Climate (`WTH`)

**WTH-01 — Weather pin** · Idea · Base
**Intent:** Drop a pin on a region to see what its weather would be, based on climate zone,
season, etc. This is the lightweight version that works on any system.
**Implementation:** —

**WTH-02 — Live weather simulation** · Idea · Advanced
**Intent:** As detailed as possible. Ideally the user can watch clouds and weather move across
the world.
**Implementation:** —

### 4.9 Lore & Journal (`LORE`)

**LORE-01 — Region outlines** · Idea · Base
**Intent:** Draw outlines around specific regions or locations on the world, with optional notes.
**Implementation:** —

**LORE-02 — Journal system** · Idea · Base
**Intent:** Start with a simple journal. Entries are sortable and can be linked to locations.
Clicking a location can pop up its journal entries to read. The exact interaction is to be worked
out later.
**Implementation:** —

**LORE-03 — Timelines** · Idea · Base
**Intent:** Timelines of events. They become relevant once orbiting bodies are introduced, and
they're key helpers for the calendar features (`CAL-01`–`CAL-03`, `SIM-02`).
**Implementation:** —

**LORE-04 — Lore relationship diagrams** · Deferred · —
**Intent:** Diagrams of connections between characters, factions, and nations (e.g. family trees,
alliances, rivalries). They probably need their own tab or page because of the space they take.
This is separate from the star system tree (`UI-02`).
**Notes:** Tabled. Revisit when the lore features are further along.
**Implementation:** —

### 4.10 Saving (`SAV`)

**SAV-01 — Save and open worlds** · Implemented (M2) · Base
**Intent:** A world persists between sessions. It's saved to and opened from a file, with
nothing lost (CLAUDE.md §4: losing user data is the worst possible bug).
**Design decisions (owner, 2026-09-30):**
- **One `.nworld` file per world**, so a world is easy to back up, move, and later share.
- The **original map image** is copied into the file unchanged, so it can always be re-prepared
  at full quality.
- The **camera view** is saved, so a world reopens where the user left off.
**Implementation (Core, PR #7):** `src/NothicWorlds.Core/Model/` (World, Body, SurfaceSettings,
SurfaceMap, RgbColor, CameraView) and `src/NothicWorlds.Core/Storage/` (`WorldPackage`). The
format is specified in [world-format.md](world-format.md): a zip with `world.json` plus
`assets/`. It's versioned (newer files are refused clearly, older ones upgraded), and uses fixed
written names for enums. Saving is atomic: write, read back, swap, with a `.bak` of the previous
save. Everything read is validated as untrusted input. Tests include round trips, a
**golden version 1 file** (guards the format forever), failure mid-save leaving the old file
untouched, and rejection of damaged, newer, or path-escaping files.
**Implementation (app, PR #8):**
- **`godot/Session/WorldSession.cs`** holds the open world. **Every edit goes through it**
  (import or clear map, map type, fill color), so the Core model always matches the screen and
  unsaved changes are always tracked. Save and open run in the background. A save works on a
  `World.Clone()` snapshot, and only clears "unsaved" if nothing changed meanwhile. After saving,
  assets are read from the saved file, so moving or deleting the original image is safe. If a
  world's map image can't be shown on open, the world still opens and the image data is kept.
  Reuse the session for any future world edit (journals, bodies, fitting adjustments).
- **`godot/UI/FileMenu.cs`**: the File menu (New / Open / Save / Save As, Ctrl+N/O/S,
  Ctrl+Shift+S, matched exactly so Ctrl+Shift+S isn't also Ctrl+S). It uses native file dialogs
  starting in `Documents\Nothic Worlds\`, and sets the window title to `Name • — Nothic Worlds`
  when there are unsaved changes.
- The **world's name follows its file name** on save. Camera movement doesn't count as an
  unsaved change, but the view is saved with every save.
- `MapImageLoader` can load from any `IAssetSource`, including an image inside a world file.
  `PlanetCamera.GetView/SetView` save and restore the view (clamped to the camera's limits).
  Camera keys are ignored while Ctrl is held, so shortcuts don't move the view.
- **Verified end to end** in the running app: import, edit, and save, with the saved image
  byte-identical to the original (SHA-256). Reopening restores the map type, fill color, and
  camera view. A save failure gives a plain-language message.

**SAV-02 — Unsaved-changes safety net** · Implemented (M2) · Base
**Intent (owner's choice, 2026-09-30): manual saving with a safety net.** The user saves with
Ctrl+S. The app warns before closing or opening another world with unsaved changes, and quietly
keeps a **recovery copy every 5 minutes**, which it offers back after a crash.
**Implementation (PR #8):**
- **Warning:** a "Save changes to “Name” before …?" dialog (Save / Don't Save / Cancel) before
  New, Open, and closing the window. Closing is intercepted (`AutoAcceptQuit = false`). Choosing
  Save on a never-saved world opens Save As, and cancelling that cancels the close.
- **Recovery:** `godot/Session/RecoveryService.cs` writes a copy in the background every 5
  minutes while there are unsaved changes, via Core `RecoveryStore` (a normal `.nworld` plus a
  note with the original location, in the app's user-data folder). On the next start after a
  crash, it offers **Recover / Discard** for the newest copy. A recovered world is marked unsaved
  and keeps its original save location. Copies are deleted when the world is saved, or when its
  changes are knowingly discarded. A copy that fails to recover is **kept**, never deleted.
- **Verified end to end:** unsaved work with a simulated crash left a recovery copy, the next
  start showed the offer, Recover restored the map, and saving then deleted the copy. The close
  warning appears with unsaved changes.

### 4.11 Sharing (`SHR`)

**SHR-01 — Share a world with players** · Future · —
**Intent:** Share the world with others in a tabletop setting, e.g. players viewing the world
the DM built. This is why viewing is treated as a first-class priority. Low priority for now.
**Notes:** It could eventually grow into a Roll20-style hosted system, where the DM hosts games
and players join. The engine-independent world format and core library (CLAUDE.md Section 9)
keep that possible without an overhaul.
**Implementation:** —

---

## 5. Open Questions

Questions raised during the project review that are still waiting on answers. When one is
answered, move the answer into the relevant entry above and remove the question from this list.

- _(none right now)_

---

## 6. Idea Inbox

New raw ideas go here first, then get sorted into a feature area once reviewed.

- **High-quality textures option (Advanced tier):** a setting to keep maps uncompressed for
  perfect quality on systems with more graphics memory (~497 MB for an 8k map vs ~137 MB
  compressed). Raised during `MAP-01`.
