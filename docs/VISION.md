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

**Milestone 1: Planet Viewer** · In Progress. Split into two PRs: **A** = planet, grid, camera,
and performance tools (PR #3); **B** = map import. Saving is out of scope: the world file format
gets its own design review.
A single planet that the user can import a map image onto, with a working camera/movement system.
The goal is to get the rough idea and the movement system in place. Nothing fancy.
- One planet (sphere) rendered in 3D (`REN-01`)
- Import an image and wrap it onto the planet (`MAP-01`, in its simplest form)
- Camera: zoom in and out, pan, and move around the planet (`REN-02`, basic form)

Later milestones are defined after Milestone 1 is reviewed.

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

**MAP-01 — Import map image in a supported layout** · Planned (simple form in M1) · Base
**Intent:** Import a flat map image and wrap it onto a globe. Supported preset layouts
(map projections) can be wrapped onto a sphere with little stretching.
**Notes:** M1 needs only the simplest case: one standard layout (probably equirectangular, a
2:1 image where lines of latitude and longitude form a straight grid).
**Implementation:** —

**MAP-02 — Manual map placement onto the globe** · Idea · Base
**Intent:** For maps that aren't in a supported layout, the user cuts the image up and places it
onto the globe themselves, adjusting for distortion and distance. (Flat maps don't map one-to-one
onto spheres: areas near the equator are close to true size, and areas near the poles are stretched.)
**Notes:** One of the most complex features. Needs its own design review.
**Implementation:** —

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

### 4.10 Sharing (`SHR`)

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

- _(empty)_
