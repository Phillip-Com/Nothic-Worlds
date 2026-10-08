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

**Milestone 2: Maps + Saving** · Complete (PR #8 merged 2026-09-30; owner's choice, 2026-09-30)
- **Part 1:** `MAP-03`, better wrapping for hand-drawn maps (Flat map mode) (PR #5). This grew
  into `MAP-04`, atlas and circular map types (PR #6).
- **Part 2:** the **world save format** (`SAV-01`, `SAV-02`), so work persists between sessions.
  It's foundational, because every later feature adds data to it. Split into two PRs: the Core
  format (PR #7), then the app side (File menu, unsaved changes, recovery copies) (PR #8).

**Milestone 3: Map Fitting** · Complete (PR #15 merged 2026-09-30)
- **Part 1:** `MAP-05` grid calibration. Core first (PR #9), then the Calibrate… workspace (PR #10).
- **Part 2:** `MAP-02` cut and place. Core first (PR #11), then the Cut editor, Pieces panel,
  and live drawing (PR #12), then drag handles on the globe (PR #13), then undo/redo (`UI-03`,
  PR #14, brought forward by the owner so warping has it from the start), then point warping
  (PR #15).

**Milestone 4: Star System Basics** · Complete (PR #18 merged 2026-10-01; owner's choice, 2026-09-30)
Suns, planets, and moons on designed orbits, a world clock, and zooming out to the whole
system (`BOD-01`, `SIM-01`, `SIM-02`, `REN-02`, `UI-02`). Calendars, seasons, and eclipses build
on it. Owner's decisions:
- **Scale:** a readable view by default, with distances compressed and bodies enlarged so everything is
  visible and clickable, plus a **true-scale toggle**. Data is always in real units (km, days).
- **Orbits:** simple (distance, period, start position; circles by default), with optional
  elongation and tilt. Periods are set freely, not derived from physics.
- **Time display** until calendars exist: "Day N, hh:mm" in the selected planet's own days.
- **Three PRs:** Core (PR #16), then the app's system view and time controls (PR #17), then
  editing (add/remove bodies, system tree, body properties) (PR #18).
- **Editing (owner, 2026-09-30):** deleting a body also deletes everything orbiting it (Ctrl+Z
  brings it all back). The system tree and properties live in a **left panel**, opened by a
  **System…** toolbar button.

**Milestone 5: Calendars and Seasons** · Complete (PR #20 merged 2026-10-01; owner's choice, 2026-10-01)
Your own calendars (`CAL-01`), and solstices, equinoxes, and seasons from the simulation
(`CAL-03`). The accuracy toggle (`CAL-02`) comes later. Owner's decisions:
- **Calendars belong to bodies:** any planet or moon can have its own. The time bar shows the
  selected body's calendar, or "Day N" without one.
- **Calendar features:** named months of any length, named weekdays, year numbering with an
  optional era, and the date at time 0. No leap days for now.
- **Seasons shown** in the time bar (the current season in each hemisphere and the next solstice
  or equinox), as a year overview in the body's panel with Go to buttons, and **as markers on
  the orbit** where each solstice and equinox happens.
- **Two PRs:** Core (calendar model, dates, season math, format v6) (PR #19), then the app
  (PR #20).

**Milestone 38: Clearer Look** · Complete (PR #94 merged 2026-10-08; owner's request, 2026-10-08)
The first of three milestones making the app easier to read and use (owner's rules: if it can
be shown, show it; simplify where nothing is lost; follow apps people already know, such as
fantasy-calendar.com). From a run-through of every panel, menu, and dialog (`UI-07`). Owner's
decisions:
- **Look and small fixes first** (this milestone), then the calendar (M39), then calendar
  presets and date pickers (M40).
- **Buttons and switches that look like what they are**, panels laid out the same way, the
  View menu grouped, the one-line hint on every tool (Diagrams and Timeline had none), and
  clearer wording (no "Day 0", no "Planet (planet)").
- **The System panel in folding sections** (the body, its look, orbit, calendar and seasons,
  events, and the system's night sky), remembering which are open, with a legend for the star's
  zones it draws. Nothing is removed.
- **One PR** (PR #94).

**Milestone 39: The Calendar** · Complete (PR #95 merged 2026-10-08; owner's request, 2026-10-08)
Time shown as a calendar, like fantasy-calendar.com (`CAL-05`). Owner's decisions:
- **A Calendar tab** in place of Timeline: the selected world's month (or whole year) as a
  grid, each day with the moons' phases, seasons, eclipses, meteor showers, and events; click
  a day to go there, double-click to add an event. The timeline stays as one of its views.
- **A calendar-style time bar:** the date in the world's calendar (weekday, day, month, year,
  time) with a small clock and the moons' phases; clicking it opens the calendar; buttons to
  step a day, month, or year either way; Play with its speed. It replaces the step menu and
  the Go to box.

**Milestone 40: Calendars Made Easy** · In progress (owner's request, 2026-10-08)
Ready-made calendars to start from, and dates picked from a calendar everywhere a date is asked
for (`CAL-06`). Owner's decisions:
- **Four starting points** in the calendar editor: fitted to this world (today's starter),
  Earth (Gregorian), thirteen lunar months, and ten-day weeks.
- **Date pickers everywhere a date is asked:** events' start and end, and the calendar's own
  start date: a small month you click, plus the hour.

**Milestone 37: Flat Worlds in Relief** · Complete (PR #93 merged 2026-10-08; owner's request, 2026-10-07)
Flat worlds get what globes have: heights, water, and shapes (`BOD-10`). Owner's decisions:
- **Everything globes have:** sculpting brushes, terrain-shaped ground with its peaks, and
  water, with the water's colors and the underwater look.
- **A waterfall at the rim:** the rim is the south pole, so the edge is wet all the way round
  or not at all. When it's wet, water pours off the edge and falls away into space, fading to
  mist below the disc, from orbit and from the ground.
- **Shapes too:** spheres, boxes, cylinders, and cones added to or cut out of the disc.
- **One PR** (PR #93). No save format change: heights, terrain, water, and shapes are saved
  whatever a body's shape.

**Milestone 36: Peaks and Water** · Complete (PR #92 merged 2026-10-07; owner's request, 2026-10-06)
The ground a terrain type shapes gets features of its own, and planets and moons get water
(`BOD-08`, `BOD-09`). Owner's decisions:
- **Variation and Size per type:** how far peaks rise and dips sink around the type's
  height, and how far apart they are; mountains get sharp ridged peaks, hills rolling bumps,
  plains a little unevenness. Fixed per world.
- **Water per body:** a Has Water switch and a Water Level (default 0 m). Wherever the
  ground is below it there's a water surface, from orbit and from the ground, with any
  terrain. With the eye below it in first person, an underwater look: a blue-green tint, a
  short murky view, and wavy light.
- **One PR** (PR #92), save format version 30.

**Milestone 35: Terrain Shapes the Ground** · Complete (PR #91 merged 2026-10-07; owner's request, 2026-10-06)
Painting a terrain type raises or lowers the ground to that type's height, and where types
meet, the heights merge from gentle slopes to sheer cliffs (`BOD-07`). Owner's decisions:
- **Layered:** each terrain type gives the ground a base height, and sculpting adds on top;
  changing a type's height later re-shapes everything painted with it, keeping the sculpting.
- **Edges:** each type has an Edge setting from Gentle through Steep to Cliff; where two
  types meet, the steeper edge wins.
- **A per-world setting** in the Terrain panel, off for existing worlds; each type gets a
  Height and an Edge. Save format version 29.

**Milestone 34: Polish Round Two** · Complete (PR #90 merged 2026-10-06; owner's request, 2026-10-05; PRs #87–#90)
Owner's requests after trying the app: ground detail up close, designed night skies, more map
cutting shapes, grid snapping, a start screen and a way to launch without Godot, and tools that
are easier to pick up. A run-through of every tool (2026-10-05) found them all working.
Painting and sculpting only looked broken: the default Ocean paint barely shows on an unmapped
planet, a 500 m sculpt is too small to see from orbit, and no tool said why it did nothing when
it couldn't act. Owner's decisions:
- **Tool help** (`UI-05`): every action a tool can't take explains why and what to do, and
  each tool shows a one-line hint on how to use it.
- **More cutting shapes** (`MAP-02`): ellipse (Shift for a circle) and regular shapes
  (hexagon and other set numbers of sides).
- **Grid snapping** (`MAP-02`): a Snap toggle that snaps dragged pieces to the latitude and
  longitude grid, and a Center on Grid button for a piece or the map.
- **Start screen and a standalone app** (`UI-06`): New World, Open World, Recent Worlds,
  Settings, and Quit, plus a Windows export that builds a double-clickable .exe.
- **Designed night skies** (`REN-07`): a fixed star field from a seed that can be re-rolled,
  and named constellations drawn by joining stars (format v28).
- **Ground detail** (`REN-06`): in first person, a fine texture and small bumps that match
  the terrain type near the feet, fading with distance (Base tier).
- **Four PRs:** tool help, then cutting shapes and snapping, then the start screen and
  export, then skies and ground.

**Milestone 33: Polish and Loose Ends** · Complete (PR #86 merged 2026-10-05; owner's choice, 2026-10-05)
Owner's decisions:
- **Standing on flat worlds** (`REN-06`): the **same sky across the disc** (the sun's height is
  its angle above the disc, so day and night come everywhere at once, as flat worlds are
  already lit and weathered; north points to the disc's center), and **walking over the rim
  onto the underside**.
- **Clouds from above:** flying above the cloud layer in first person shows the clouds below.
- **Diagram extras** (`LORE-04`): drag entries from the list onto a diagram, and **save a
  diagram as a picture**: the whole diagram, fitted with a margin, at twice normal detail, on
  the page's dark background, titled with its name.
- **Tidy the docs:** close `REN-01`'s old status, fix the format example's heading.
- **Three PRs:** the docs and diagram extras (PR #84), clouds from above (PR #85), then flat
  worlds (PR #86).

**Milestone 32: Metric and Imperial Units** · Complete (PR #83 merged 2026-10-05; owner's idea and choice, 2026-10-05)
A switch between metric and imperial for every measurement (`UI-04`). Owner's decisions:
- **A setting in File ▸ Settings, for this computer**, not saved in worlds (which always store
  metric, so they read right on anyone's machine).
- **Both what's shown and what's typed** switch: fields show and take the chosen units.
- **Everyday units switch** (km and miles, meters and feet, km/h and mph, °C and °F, mm and
  inches, km² and square miles, liters and gallons, kg and pounds, g/cm³ and lb/ft³);
  **astronomical ones stay** in both (AU, Earth and Sun masses, light-years).
- **The default follows the computer's region** (imperial in the United States, Liberia,
  and Myanmar) until it's changed.
- **Two PRs:** conversion and formatting in Core (PR #82), then the setting and every readout
  and field in the app (PR #83).

**Milestone 31: Lore Relationship Diagrams** · Complete (PR #81 merged 2026-10-05; owner's choice, 2026-10-05)
Diagrams of how characters, factions, and nations are tied together (`LORE-04`). Owner's
decisions:
- **The boxes are journal entries,** which gain an optional kind (character, faction, nation,
  place, other) for their look; clicking a box opens the entry.
- **Relationship kinds:** parent of, married to, sibling of, ally of, rival of, at war with,
  member of, rules, serves, plus **other** in the user's own words. Family ties can be laid
  out as a family tree.
- **Named diagrams, shared ties:** any number of diagrams ("House Arren", "The Northern
  Alliance"), entries placed on each by hand with an Arrange button; a tie is stored once and
  shows in every diagram that holds both ends.
- **Optional start and end dates** on ties, so a diagram shows them as they stand at the world
  clock's time (past ones faded).
- **Two PRs:** the model and file format in Core (PR #80), then the diagram page (PR #81).
- **The page's choices:** a **full-window page** from a Diagrams button in the toolbar (the 3D
  view pauses); ties made by **dragging from one box to another**, and also **listed and
  edited in the Journal panel**; **Arrange** lays out a **family tree, then the rest**.

**Milestone 30: First-Person Surface View** · Complete (PR #79 merged 2026-10-05; owner's choice, 2026-10-05)
Stand on a world and look up (`REN-06`). Owner's decisions:
- **Walk or fly:** move over the surface, following the ground, or fly freely.
- **The sky at true size and place,** with a **magnify** switch that enlarges small bodies;
  moon phases and eclipses as they really fall.
- **Included:** day and night sky colors (black skies on airless worlds), the live weather
  overhead, the ground and horizon, and a compass with readouts (names and heights on hover).
- **Base tier** (measured on the baseline laptop; `REN-06` was "Advanced, probably").
- **Three PRs:** the sky in Core (PR #77), the first-person view with walking, flying, and the
  sky (PR #78), then the ground, weather overhead, and readouts (PR #79).
- **Part 3's choices:** clouds overhead from the **live weather** (matching the globe's);
  rain and snow as **light streaks and flakes** over the view; a **compass, readouts on
  hover, and a panel** with the time and local weather; **haze toward the horizon** on worlds
  with air.
- Known limit: the ground from eye level is only as sharp as the map (an 8k map is about 5 km
  a pixel on an Earth-sized planet).

**Milestone 29: Performance Tiers** · Complete (PR #76 merged 2026-10-05; owner's choice, 2026-10-05)
Keep the app light on modest computers and let stronger ones look their best (`REN-03`).
Owner's decisions:
- **A File ▸ Settings window** with a **Graphics Quality** preset (Low, Standard, High, or
  Custom) that sets everything at once, each option below it; Relief Detail and Cloud Detail
  move there from the View menu (View keeps show and hide).
- **The options:** anti-aliasing, the 3D view's resolution, a frame-rate limit, relief and
  cloud detail; plus **automatic level of detail** (always on).
- **The Advanced tier** holds **high-quality map textures** (uncompressed), behind a switch
  with a memory warning.
- **The starting preset comes from the graphics chip:** Standard on integrated graphics, High
  on a dedicated card (owner's choice after measuring the baseline laptop: it runs even 4×
  anti-aliasing at ~140 fps, so Low would cost looks for nothing).
- **Two PRs:** the Settings window and presets (PR #75), then level of detail and the
  Advanced tier (PR #76).

**Milestone 28: Small Fixes** · Complete (PR #74 merged 2026-10-05; owner's choice, 2026-10-05)
One tidy-up PR for issues found along the way (PR #74): the dotted line the grid drew along the
date line, which also bent the wind's streaks there, and clicks that ignored holes carved by
shapes (see `BOD-04`).

**Milestone 27: Live Weather** · Complete (PR #73 merged 2026-10-05; owner's choice, 2026-10-04)
Watch the weather move across a world (`WTH-02`). Owner's decisions:
- **Worked out from the time,** not stepped forward: any date's weather is ready at once and
  always the same (CLAUDE.md §5), built on the weather pins' climate (`WTH-01`, `WTH-03`).
- **Shown:** clouds on the globe, rain and snow, the winds, and weather pins reporting today's
  weather.
- **An Atmosphere switch per body:** planets have air by default, moons don't (existing worlds
  too); only bodies with air have live weather. Format version 26.
- **Base tier, with a cloud-detail quality setting** (`WTH-02` was Advanced; owner's choice).
- **Three PRs:** the weather in Core (PR #71), drawing it on the globe (PR #72), then today's
  weather in weather pins (PR #73).

**Milestone 26: Visual Styles** · Complete (PR #70 merged 2026-10-04; owner's choice, 2026-10-04)
Let each world choose how it's drawn (`REN-05`). Owner's decisions:
- **Three styles:** **Painterly** (soft bands of light, brushy surfaces, gentle outlines),
  **Realistic** (today's lighting, unchanged), and **Simple** (flat colors, a crisp day and night,
  clean outlines).
- **Made in the surface shaders,** not a whole-screen filter: cheap on the baseline laptop
  (Base tier), and labels, grid lines, and handles stay sharp.
- **Stored per world,** in the file (format version 25), so a world keeps its look when it's
  saved or shared.
- **Painterly is the default for every world,** new and existing; Realistic is one click away.
- **Two PRs:** the style in Core and the file (PR #69), then the looks and the picker
  (PR #70).

**Milestone 25: Shapes, Holes, and Hollow Worlds** · Complete (PR #67 merged 2026-10-04; owner's choice, 2026-10-04)
The rest of body sculpting (`BOD-04`): shapes added to or cut out of a world. Owner's decisions:
- **Sphere, box, cylinder, and cone,** each **added** or **cut**: domes and craters, walls and
  quarries, towers, pits, and holes right through a world, and (a sphere cut from inside)
  hollow worlds.
- **Placing:** click the globe to place a shape, then drag handles on the globe to move,
  resize, and turn it, with a list of the body's shapes.
- **Bare rock** on the faces a shape makes; the original surface keeps its map and terrain.
- **Built with Godot's own CSG** (its Manifold library) from the sculpted globe's mesh, only for
  bodies that have shapes. No new dependency.
- **Base tier, measured first:** Godot's CSG re-carves the whole globe on every change, on the
  main thread: 1.5 s at the Standard mesh. So (owner's choice) shaped worlds carve a coarser
  globe, 32 squares a face (about 0.25 s), and while a handle is dragged the shape shows as a
  see-through preview, carved when it's let go.
- **Three PRs:** shapes in Core (PR #65), drawing worlds with shapes (PR #66), then the tools
  (PR #67).

**Milestone 24: Body Sculpting (Heights)** · Complete (PR #63 merged 2026-10-04; owner's choice, 2026-10-04)
Mold bodies like clay (`BOD-04`), starting with heights. Owner's decisions (the design review
`BOD-04` asked for):
- **Heights + shape edits:** a height per cell on the cube-sphere grid painted terrain uses,
  plus (next milestone) a list of shapes added or cut away, which makes holes through a world
  and hollow worlds possible without a full 3D voxel model.
- **Heights first:** this milestone sculpts heights on globes, with **Raise**, **Lower**,
  **Smooth**, and **Flatten** brushes; shapes, holes, and hollows come next.
- **±32 km in 1 m steps,** true to scale; a **View** setting exaggerates the relief so it shows
  from orbit, and shading always shows slopes.
- **Tools in the Terrain panel:** a Sculpt mode beside Paint/Erase, with the same brush plus a
  strength.
- **Three PRs:** heights in Core (PR #60), drawing the relief (PR #61, with its quality setting
  in PR #62), then the Sculpt tools (PR #63).

**Milestone 23: Physics Mode** · Complete (PR #59 merged 2026-10-04; owner's choice, 2026-10-04)
An optional switch that moves the system by real gravity (`SIM-03`), from the masses of
Milestone 22. Owner's decisions:
- **Start speeds from gravity:** each body starts where its design puts it, moving at the speed
  gravity would give that orbit, so a well-built system holds together and an unstable one
  slowly comes apart.
- **Only the 3D view follows physics:** seasons, calendars, eclipses, and weather stay on the
  designed orbits.
- **Switching off returns to the design;** a **Keep as orbits** button turns the simulated paths
  into new designed orbits (one undo step).
- **Collisions merge** the smaller body into the bigger (in the simulation only) and are listed.
- **Base tier**, behind its own switch (off by default): a few dozen bodies' gravity is light.
- **Two PRs:** the simulation in Core (PR #58), then the switch, drawing, collision list, and
  Keep button in the app (PR #59).

**Milestone 22: Stable Orbit Guide** · Complete (PR #57 merged 2026-10-04; owner's choice, 2026-10-04)
Where gravity would keep orbits steady (`SIM-04`). Orbits stay designed and free; this only
guides. Owner's decisions:
- **Mass from density:** each body has a **Density** (g/cm³), typical for its kind and size
  until you set it, and the panel shows the mass it gives.
- **Shows all four:** the zone where the selected body's moons could circle steadily (from its
  Roche limit out to about half its Hill sphere), how far its neighbors' pull reaches (the gaps
  where another body fits), warnings on orbits gravity wouldn't keep, and the period gravity
  would give at an orbit's size, with a button to use it.
- **Bands and numbers:** shaded bands in the 3D system view around the selected body (green
  where steady, red too close), switched in the View menu, and the distances in the System panel.
- **Two PRs:** density and the math (PR #56), then the guide in the app (PR #57).

**Milestone 21: World Tree** · Complete (PR #55 merged 2026-10-04; owner's choice, 2026-10-04)
More world shapes (`BOD-02`): a world tree. Owner's decisions:
- **A tree that holds worlds**, like Yggdrasil: a new kind of body standing in the system (at
  its center, or circling a star), whose great branches hold **realms**: any planet or moon
  (globe or flat world) hung on a branch, keeping its own maps, terrain, calendar, and weather.
- **Grown from settings:** height, how many branches and how far they spread, bark and leaf
  colors, and a seed for its shape.
- **It glows** (a color and strength) and lights its realms; stars light them too, but the tree
  counts as their sun.
- **It turns:** each realm circles the trunk once per turn, its year, so seasons work as for a
  planet around a star.
- **Three PRs:** the tree itself (PR #53), realms on its branches (PR #54), and its glow as
  their sun (PR #55).

**Milestone 20: Astral Features** · Complete (PR #52 merged 2026-10-04; owner's choice, 2026-10-03)
Rings, asteroid belts, and nebulas (`BOD-03`), and asteroid events (the rest of `EVT-02`).
Owner's decisions:
- **Planetary rings:** banded, in a color you pick, with the planet's shadow on the rings and
  theirs on the planet.
- **Asteroid belts:** a belt is one feature on a star (inner and outer distance, thickness,
  density, color); its rocks are drawn, not saved or edited one by one.
- **Nebulas:** a backdrop on the sky around the whole system, the same from every planet.
- **Asteroid events:** worked out from the belts (planets in or near dense belts get more close
  passes, and rarely an impact), the same every time for a given world; impacts are listed
  only, with no change to the map.
- **Four PRs:** rings (PR #49), belts (PR #50), nebulas (PR #51), asteroid events (PR #52).

**Milestone 19: Flat Worlds** · Complete (PR #48 merged 2026-10-03; owner's choice, 2026-10-03)
Non-standard bodies (`BOD-02`), starting with flat worlds. Owner's decisions:
- Planets and moons get a **Shape**: Globe or **Flat world**, changeable any time; maps,
  terrain, regions, and pins are kept.
- **The whole world on top**: the north pole at the center and the far south around the rim
  (like the Polar map); bare rock underneath.
- **Physically flat light**: the disc tumbles like a spinning coin, so the whole face is lit at
  once and every place shares the same sun height; two summers and two winters a year, and
  no climate zones by latitude.
- **Three PRs:** flat worlds you can make, see, and edit (PR #46); the close-up view
  (PR #47); the flat-world sky (day and night, seasons, weather) (PR #48).

**Milestone 18: Comets and Meteor Showers** · Complete (PR #45 merged 2026-10-03; owner's choice, 2026-10-03)
Meteor showers (`EVT-02`), simulated from comets. Owner's decisions:
- **Comets** are a new kind of body: they circle a star on an elongated orbit, have a color and
  pattern like moons, and are drawn with a **glowing tail** pointing away from the star, longer
  near it.
- **Showers** happen where a planet's orbit passes close to a comet's orbit, at the same point
  of the year every year. Their strength comes from the **comet's size**.
- Shown as a **list in the System panel**, a **line in the time bar** while one is under way,
  and **markers on the orbit**. Asteroid events come later (with `BOD-03`).
- **Two PRs:** comets (PR #44), then meteor showers (PR #45).

**Milestone 17: Leap Years** · Complete (PR #43 merged 2026-10-03; owner's choice, 2026-10-03)
Calendars can add leap days on a rule (new feature `CAL-04`). Owner's decisions:
- **Rule:** "every N years, except every M, but every K" (up to three tiers, like ours).
- **Leap days** are added to a chosen month.
- A **Suggest** button works out a rule that keeps the calendar with the real year.
- One PR (PR #43), with format version 14.

**Milestone 16: Body Appearance** · Complete (PR #42 merged 2026-10-03; owner's choice, 2026-10-03)
Give bodies their own look (`BOD-06`). Owner's decisions:
- **Planets and moons:** a color and a surface pattern (Plain, Rocky, Banded, Icy, Cloudy),
  shown where there's no map; terrain and maps still draw on top.
- **Stars:** picked by type (Red dwarf, Orange, Yellow, White, Blue), which sets their light too.
- **Defaults:** planets keep today's ocean blue; moons become grey and rocky; stars yellow.
- **No visual styles yet** (`REN-05` stays for later). One PR (PR #42).

**Milestone 15: Small Fixes** · Complete (PR #41 merged 2026-10-03; owner's choice, 2026-10-03)
One tidy-up PR for issues found along the way (PR #41): dropdowns that could pick an item by
accident in short windows, and markers showing through the side panels (see `UI-01`).

**Milestone 14: Terrain-Aware Weather** · Complete (PR #40 merged 2026-10-03; owner's choice, 2026-10-03)
Weather pins take the painted terrain around them into account (new feature `WTH-03`). Owner's
decisions:
- **A climate kind per terrain type**, from a short list (Water, Open land, Forest, Desert,
  Wetland, Mountains, Ice), picked in the Terrain panel; the 12 defaults come pre-set.
- **Temperature and rainfall.** **Two PRs:** temperature (PR #39), then rainfall (PR #40).
- **The area around counts** for water (about 500 km; smaller on small bodies); the other
  kinds count where the pin stands. Unpainted ground changes nothing.
- **One line** in the weather window explains the terrain's effect.

**Milestone 13: Local Region View** · Complete (PR #38 merged 2026-10-03; owner's choice, 2026-10-03)
Zoom down to a local region of a planet and see it as a top-down map (`REN-04`). Owner's
decisions:
- **A seamless deeper zoom** on the same globe and camera (not a separate 2D view), down to
  about 10 km above the ground of an Earth-sized planet, top-down below a threshold.
- **Detail from map pieces** for now: a detailed local map imported as a piece shows sharply.
- **Getting there:** by zooming, plus **Zoom to** buttons for regions and pins.
- **On screen in the local view:** a scale bar, a north arrow, and the coordinates (and region)
  under the mouse. Day and night stay as they are.
- Claude's plan: in the local view the camera rides with the planet (the same ground stays in
  view as time passes, north up). **Two PRs:** the camera (PR #37), then the on-screen aids and
  Zoom to buttons.

**Milestone 12: Calendar Fitting** · Complete (PR #36 merged 2026-10-03; owner's choice, 2026-10-03)
Keep the world fitted to a calendar, so its dates never drift against the seasons (`CAL-02`).
Owner's decisions:
- **A lasting switch** per calendar: while it's on, the world stays fitted, and the fitted
  fields are locked in the System panel. Format version 11 (older worlds: off).
- **The user picks what changes:** the year's length (the orbit) or the day's length.
  Defaults: planets change their orbit; moons their day (a moon's year is its planet's orbit).
- **Moon months, optionally:** a chosen moon's orbit is kept so new moon to new moon is one
  month (Claude: the average month, so any calendar works).
- **One PR** (PR #36).

**Milestone 11: Terrain Painting** · Complete (PR #35 merged 2026-10-03; owner's choice, 2026-10-03)
Paint terrain types onto planets and moons with a brush (`BOD-05`). Owner's decisions:
- **Terrain types:** an editable list per world. New worlds start with 12 defaults (Ocean,
  Shallow Water, Plains, Fields, Forest, Jungle, Hills, Mountains, Desert, Swamp, Tundra, Ice)
  that can be renamed, recolored, added to, and deleted.
- **With a map:** without a map, the terrain is the planet's surface. Over a map it's a
  see-through overlay, so a map can be traced; **View ▸ Terrain** turns it on and off.
- **Tools:** an own **Terrain** panel (panel row button, right side, one at a time with Map,
  Journal, and Regions). Painting is on while it's open.
- **Detail:** a cube-sphere grid of 1,024 × 1,024 cells per face (about 10 km across on an
  Earth-sized planet; 6 MB per fully painted body).
- **Three PRs:** Core (grid, brush, types, saving; PR #33), then drawing and the Terrain panel,
  then polish (soft edges) and the benchmark.
- Not in this milestone: sculpting heights (`BOD-04`), a fill bucket, terrain-aware weather.
- **Unpainted ground** (owner's choice, PR #35): once a planet without a map has any terrain,
  what's not painted is drawn neutral grey, so it's easy to tell from painted Ocean.

**Milestone 10: Layout Tidy-Up** · Complete (PR #32 merged 2026-10-03; owner's choice, 2026-10-02)
Settle the main screen before adding more (`UI-01`): the top bar had overflowed small windows and
had look-alike controls (Regions vs Regions…). Owner's decisions:
- **A menu bar plus one row of panel buttons** (System, Map, Journal, Regions, Timeline).
- **Map tools in one Map panel** on the right (image, type, fill color, Calibrate…, pieces),
  replacing the Pieces panel.
- **A View menu** for the show/hide switches, each clearly named.
- **The window starts maximized**; the layout still has to work in small windows.
- **Two PRs:** the menus and panel row (PR #31), then the Map panel (PR #32).

**Milestone 9: Weather Pin** · Complete (PR #30 merged 2026-10-02; owner's choice, 2026-10-02)
The weather at named spots on a planet or moon (`WTH-01`). Owner's decisions:
- **Content:** sun and temperature: daylight hours, how high the sun gets, and a temperature
  range through the year, with the season. No rain or climate types yet.
- **Temperatures** come from each planet or moon's **average temperature**, set by the user
  (Earth: 15 °C); the sunlight math spreads it by latitude and season. No star brightness.
- **Shown** as a pop-up with a **year chart** (temperature range and daylight per month, in
  the body's calendar) with a marker at today, plus today's numbers.
- **Weather pins are named and saved** with the world, with their own icon on the globe; added
  with a **Weather Pin** toolbar button, then a click on the spot.
- **Two PRs:** Core (the model, average temperature, pins, format v9) (PR #29), then the app
  (PR #30).

**Milestone 8: Region Outlines** · Complete (PR #28 merged 2026-10-02; owner's choice, 2026-10-02)
Named areas outlined on planets and moons (`LORE-01`). Owner's decisions:
- **Drawing:** click points around the region on the globe, then drag points to adjust, add
  points on an edge, or delete them (like map pieces' Edit Points).
- **Look:** a colored outline with a light fill, and the region's name in the middle when close
  enough.
- **Regions are places:** besides their own notes, journal entries and events can be placed in
  a region; clicking a region pops up its notes and everything placed in it.
- **Two PRs:** Core (regions on the sphere, format v8) (PR #27), then the app (PR #28).
- **Deleting a body deletes its regions** in the same undo step, and regions have their **own
  Regions toggle** (owner's choices, 2026-10-02).
- Working assumptions (Claude's, stated in the plan): regions belong to a planet or moon,
  overlap freely, and have a name, color, and notes; a Regions panel shares the right side
  with the Journal and Pieces panels.

**Milestone 7: Journals and Timelines** · Complete (PR #26 merged 2026-10-02; owner's choice, 2026-10-02)
Writing the world's history and lore (`LORE-02`, `LORE-03`). Owner's decisions:
- **Journal entries and timeline events are separate things**, linked **many to many**: an
  event can link to any number of entries and the other way round, and each side shows its
  links.
- **Places:** entries and events can both be placed on a body, optionally pinned to a spot on
  its surface. Pins show on the globe; clicking one pops up what's there.
- **Several named timelines**, shown as colored **lanes**, each of which can be hidden; every
  event belongs to one.
- **The timeline is a strip along the bottom** (above the time bar), toggled on and off, with
  scrolling, zooming, a "now" line, and clicking to jump.
- **Event dates:** a moment, with an optional end for things that last (drawn as bars). Stored
  as world time and shown in the selected body's calendar.
- **Four PRs:** Core (model, links, places, format v7) (PR #23); the journal panel (PR #24);
  the timeline strip (PR #25); pins on the globe (PR #26).
- **Pins (owner's choices, 2026-10-02):** placed by clicking on the globe (coordinates are shown,
  not typed); always shown, with a **Pins** toggle to hide them.
- **The strip (owner's choices, 2026-10-02):** clicking an event jumps the clock there and
  double-clicking edits it; deleting a timeline deletes its events (one undo step); events
  aren't dragged to new dates yet; the strip shows only the user's events, not seasons or
  eclipses.
- **Deleting a body** keeps the entries and events placed on it, with their place cleared, in
  the same undo step (owner's choice, 2026-10-02).
- **The Journal panel shares the right side with the Pieces panel**, one at a time (owner's
  choice, 2026-10-02).

**Milestone 6: Eclipses** · Complete (PR #22 merged 2026-10-02; owner's choice, 2026-10-01)
Eclipses found from where the bodies are (`EVT-01`). Owner's decisions:
- **The selected body's eclipses:** solar eclipses seen from it (its moons passing in front of
  the star), and lunar eclipses of its moons. A selected moon has the same ones its planet sees
  with it: lunar (in the planet's shadow) and solar (in front of the star, seen from the
  planet; added 2026-10-02 at the owner's request). Not the whole system, and no transits of
  planets across the star.
- **Shown** as a list in the System panel (the coming year, with dates and Go to buttons) and
  **as markers on the moon's orbit**. Not in the time bar.
- **Detail:** the type (total, annular, partial, or penumbral), start, peak, and end, and how
  much is covered.
- **Two PRs:** Core (eclipse search and tests) (PR #21), then the app (PR #22).
- Not included for now: where on the surface an eclipse is seen (its path across the map), and
  drawing the shadow in 3D. Eclipses are worked out, never saved, so the file format is
  unchanged.

---

## 4. Feature Areas

Each entry uses this format:

> **ID — Name** · Status · Tier
> **Intent:** what the owner wants it to do and why
> **Notes / open questions:**
> **Implementation:** _(filled in when built: approach, key files/modules, what can be reused)_

### 4.1 Rendering & Navigation (`REN`)

**REN-01 — 3D world rendering** · Implemented (M1: PR #3; star systems: PR #17; built on by later milestones) · Base
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
**Implementation (system view, PR #17):** `godot/Rendering/SystemView.cs` draws every body.
- Planets and moons are a shared unit sphere with their own `PlanetSurface` and a copy of the
  planet material. The node's transform spins the body (its day length), tilts its axis, and
  scales it to its display size. Because the shader works in the body's own space, maps,
  pieces, and handles stay attached to the turning surface.
- Stars are glowing spheres with an `OmniLight3D`, with no fall-off and no shadows, so suns
  light the planets around them. The old fixed `DirectionalLight3D` is only used when a world
  has no stars.
- Faint orbit lines come from Core's `SystemLayout.OrbitPath`. A body's own orbit line hides
  when you're close to it, so it doesn't cut through the globe.
- **Floating origin:** positions are worked out in full precision. The scene is drawn around
  the focused body, so true-scale systems millions of units across stay precise near the camera.
- **Near clipping follows the nearest surface (bug fix, PR #18):** the camera's near plane is 2%
  of the distance to the closest body's surface (never under 0.001 of the focused body's radius).
  It used to be fixed at that tiny minimum. Zoomed far out at true scale (about 59,000 units),
  the depth buffer then lacked the precision to draw distant things, and orbit lines vanished.
- Benchmark (8k map, fullscreen, back to back with `main`): ~154 → ~147 fps, video memory
  144 → 151 MB, for the added sun, its light, and orbit lines.

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
**Implementation (system view, PR #17):**
- Zooming out goes from the selected body to the whole system: the camera's reach (in the focused
  body's radii) follows the system's size. The same camera, controls, and pan modes work at
  every scale.
- **Click any other body** (its globe, its dot, or its label) to select it and **fly there**.
  Over 1.2 s the scene's center glides to the new body and the camera rescales to its size.
  From close up the framing is kept (the new body fills the screen the same way); from far out
  the real distance is kept, so flying to a big star doesn't zoom out. A click only counts if
  the mouse barely moved, so dragging to orbit never changes the selection.
- `godot/UI/BodyMarkers.cs`: bodies too small to see get a colored dot (stars yellow, planets
  blue, moons gray), and other bodies get their name when seen from afar.
- **Readable / true scale** (owner's choice): the toggle in the time bar. Core's
  `Simulation/SystemLayout.cs` (tested) compresses sizes and distances with a power curve (0.4).
  It keeps every direction and the order of distances, and keeps orbits clear of the bodies at
  each end. An Earth-sized planet is 1 unit in both modes.
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

**REN-03 — Performance tiers** · Implemented (M29: PRs #75–#76) · —
**Intent:** Keep requirements low with level-of-detail, quality settings, and rendering only what's
visible. Heavier features go in an opt-in **Advanced** section.
**Implementation (M1, PR #3):** measurement tools only. The tiers themselves aren't built yet.
**Quality settings (M24, PR #62):** the first one, Relief Detail (Low, Standard, High: how
finely sculpted globes' shapes are drawn, `BOD-04`; now in File ▸ Settings). Settings that
belong to the computer, not the world, live in `godot/Session/AppSettings.cs`, kept in Godot's
per-user data folder (`user://settings.cfg`); a missing or damaged file gives the defaults.
**The Settings window and presets (M29, PR #75):**
- **`UI/SettingsWindow.cs`** (File ▸ Settings…, opens even while a save runs): Graphics Quality
  (Low, Standard, High; Custom shows when the options match none) and each option: Relief
  detail, Cloud detail, Smooth edges (Off, Quick FXAA, Finest 4× MSAA), 3D resolution (100%,
  75%, 50%), Frame rate (Match the screen, 60, 30 a second); with tooltips, and the computer's
  graphics named. Changes apply at once.
- **`Rendering/GraphicsOptions.cs`:** the options as one record, the presets (Low: Low relief
  and clouds, no smoothing, 75%, 30 fps; Standard: Standard relief, High clouds, FXAA, 100%,
  60 fps; High: High relief and clouds, 4× MSAA, 100%, the screen's rate), which preset a set
  matches, and the starting one (`DefaultFor`: High on a discrete GPU, else Standard).
- **`Rendering/GraphicsSettings.cs`** (a node in the scene) puts them into effect: the
  viewport's MSAA, FXAA, and 3D scale, `Engine.MaxFps`, `SystemView.ReliefDetail`, and
  `WeatherDisplay.Detail`. `AppSettings.LoadGraphics` takes any option never saved from the
  starting preset (so earlier Relief and Cloud Detail choices carry over) and nothing is
  written until the user changes something.
- **Benchmark** (`-- --benchmark`): lifts the frame-rate limit and prints the smoothing and 3D
  resolution it ran at, so results compare across settings.
- **Measured** (baseline laptop, Vega 10, fullscreen): no smoothing ~147–171 fps (runs vary),
  FXAA ~159–161, 4× MSAA ~139 (+76 MB video memory, a ~100 ms hitch on the first frame), 75%
  resolution ~158, 50% ~206. Against `main`, the default Standard preset (FXAA) costs ~7%: 159/159
  vs 171/171 fps.
- **Verified in the running app** at 1152 × 648: File ▸ Settings… opened from the menu; Low, High,
  and Standard each setting every option (and the viewport, frame cap, relief, and cloud
  detail following), one option changed by itself showing Custom, and the choices saved (the
  test's settings file removed afterwards).
**Level of detail and the Advanced tier (M29, PR #76):**
- **Level of detail** (`PlanetSurface.ScreenRadius`, always on): `SystemView` tells each globe
  its radius on screen every frame. Under 200 pixels a sculpted globe uses the Low relief mesh
  (whatever Relief Detail says); under 60, every globe uses a coarse 32 × 16 sphere instead of
  the 128 × 64 one (a sculpted one's relief is still lifted and shaded by the shader). Leaving
  a level takes 15% more than entering it, so a globe at the edge doesn't flicker. Carved and
  flat worlds keep their own meshes. Measured with a sculpted planet and eight sculpted moons
  on the baseline laptop: from far out 203 fps with 34,000 triangles, against 107 fps with 1.6
  million without it; 223 against 161 nearer in. The standard benchmark is unchanged
  (161/161 vs 161/161 fps).
- **High-quality maps** (the Advanced tier; File ▸ Settings ▸ Advanced, with its memory cost
  spelled out; in no preset): `Maps/MapQuality.Uncompressed` skips the S3TC compression of
  imported maps and map pieces (`MapImageLoader`, `PieceTextureBaker`); far bodies' small
  previews stay compressed. Switching it reloads the selected map and re-bakes the pieces
  (`WorldSession.ReloadMapsAsync`); it's set before anything loads at start (`GraphicsSettings`
  in `_EnterTree`) and remembered (`AppSettings.HighQualityMaps`). Measured with a 4096 ×
  2048 map: DXT1 and 107 MB of video memory, uncompressed (RGB8) 206 MB; the old copy is freed
  when .NET next collects garbage.
- **Verified in the running app:** the meshes stepping down and back up as the camera moves out
  and in (radius 359 → 161 → 72 → 21 px and back), and a generated map switching between
  compressed and uncompressed; the Settings window's Advanced section at 1152 × 648.
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

**REN-04 — Top-down local region view** · Implemented (M13: PR #37, PR #38) · Base
**Intent:** Zoom down to a local region and see it as a top-down terrain view.
**Implementation (camera, PR #37):**
- `godot/Controls/PlanetCamera.cs`: below `LocalViewMaxAltitude` (0.25 radii, a view about
  2,500 km across on an Earth-sized planet) the camera is in the **local view**. Its latitude and
  longitude are then measured in the planet's own frame (`SurfaceFrame`, the spin and tilt), so
  it stays over the same ground as time passes and the planet's north is up on screen. Crossing
  the threshold re-expresses where the camera is in the other frame (`SetLocal`), so nothing
  moves; only "up" eases round (by up to the axial tilt). In the local view a left-drag pans the
  map instead of orbiting (`IsLocalView`; the action indicator says Panning).
- `godot/Rendering/SystemView.cs`: gives the camera the focused body's orientation every frame
  and has it follow straight away (`FollowSurface`), so it never lags the spinning ground. The
  local view is off while flying between bodies. The camera may come within **10 km** of the
  ground (`ClosestApproachKm`; at least 1e-5 radii for huge stars). The near clipping distance
  can now be tiny, and the far one is capped at a million times it (`MaxDepthRange`): beyond that
  the engine couldn't build the view (frustum errors), and nothing that far shows when looking
  straight down.
- Season and eclipse markers and the other bodies' dots and labels are hidden in the local view
  (they mark places out in space, which only clutter the map).
- The camera text reads "Local view: the ground, north up (zoom out for the globe)".
- Saved views: below the threshold, the latitude and longitude are the body's own (see
  world-format.md, `view`).
- **Verified in the running app** with real scroll and drag input: zooming in switches to the
  local view (north exactly up), time passing leaves the ground under the camera still (0.0 km),
  left-drag moves the map with the mouse, the closest approach is 10 km with the ground still
  drawn, zooming back out crosses over without a jump, save/reopen keeps the view, and flying to
  the Sun and back from close up works without errors. Benchmark unchanged (~195 fps both).
**Implementation (aids and Zoom to, PR #38):**
- `godot/UI/LocalViewAids.cs`: in the local view only, at the bottom left (moved aside while
  the System panel is open): a **north arrow** (pointing to the body's north from the middle of
  the view), a **scale bar** (the largest 1, 2, or 5 × 10ⁿ km up to 160 px, measured across the
  middle of the view on the body's real radius), and the **coordinates under the mouse**, with
  the names of any regions there (`LoreRules.RegionsAt`). Redrawn as the view moves.
- **Zoom to** (`godot/Controls/ZoomTo.cs`): selects the body if needed (flying there), then
  `PlanetCamera.FlyToSurface` glides down into the local view over the spot, low enough that a
  given span fits top to bottom (it waits until the flight between bodies is over). A region is
  shown whole (centered on `SphericalPolygon.Center`, at least 50 km across); a pin with 300 km
  of ground around it. Buttons: the Regions panel's editor (beside Edit Points), the journal and
  event pin pop-up, and the weather pin window. `SystemView.FlyTo` now turns the local view off
  straight away, so a Zoom to queued during a flight uses the right body.
- **Verified in the running app** with real clicks, maximized and at 1152 × 648: Zoom to from
  the Regions panel (centered to 0.0 km, all corners on screen), from a journal pin's pop-up and
  a weather pin's window (both 0.0 km off), and to a pin on a moon (selected, flown to, 0.0 km
  off); the aids readable, the region name under the mouse, and the aids beside the open System
  panel. Benchmark unchanged (~197 fps both).

**REN-05 — Visual styles** · Implemented (M26: PRs #69–#70) · Base
**Intent:** Painterly is the default style. The goal is to let users choose other styles, such
as realistic or simple.
**Implementation (the style in Core, M26, PR #69):**
- **`Model/VisualStyle.cs`:** `Painterly` (the default), `Realistic`, `Simple`; `World.Style`
  (copied by `World.Clone`).
- **Saving:** format **version 25**, a top-level `style` (world-format.md), always written;
  older files have none and load as Painterly (owner's choice: painterly for all). Tested: the
  golden file, each style written by name and read back, version 24 files, and refusing an
  unknown style.
**Implementation (the looks and the picker, M26, PR #70):**
- **One shader per style:** the surface moved into `Rendering/planet_surface.gdshaderinc`.
  `planet.gdshader` (Realistic) is just that, so it draws exactly as before; and
  `planet_painterly.gdshader` and `planet_simple.gdshader` add `planet_style.gdshaderinc`, with
  its own `light()` (a shader with one always replaces Godot's lighting, so a switch inside one
  shader couldn't keep Realistic unchanged). `PlanetSurface.Style` swaps the material's shader;
  its settings carry over.
- **Painterly:** three bands of light with soft steps whose edges wander with brush streaks
  (noise stretched along the latitudes, faded out where finer than a few pixels), a warm glow
  where day turns to night, and a soft outline in a darker shade of the surface. **Simple:**
  even daylight with a crisp line to night, and a clean dark outline. Outlines are a constant
  width in pixels (from how fast the surface turns away per pixel), following the round globe
  rather than the mesh, whose carved slivers made spikes. Bands come from the angle to the
  light only, with shadow applied after: mixed in, the globe's speckled self-shadow where light
  grazes it showed.
- **Rock faces** of shaped worlds (`ShapedGlobe.UseStyle`): Godot's two-tone "toon" light in
  Painterly and Simple. Stars, rings, belts, comets, and nebulas are unlit, so every style
  draws them the same.
- **Picking it:** **View ▸ Style** (`UI/ViewMenu.cs`), a radio list with tooltips;
  `WorldSession.SetStyle` is one undo step and an unsaved change (the style is part of
  `LoreState`); opening a world or undoing shows its style (`SystemView.Style`).
- **Verified in the running app** with real clicks: View ▸ Style switching, its check marks,
  undo; each style close up and from afar on a rocky planet, its outline, and a carved pit and
  an added block. Benchmark (Painterly against `main`, back to back): 193/193 vs 192/200 fps,
  same memory.

**REN-06 — First-person surface view** · Implemented (M30: PRs #77–#79; M33: PRs #85–#86) · Base (owner's choice, 2026-10-05; was Advanced, probably)
**Intent:** View the world from the surface in first person. It's a nice-to-have if it proves possible.
**Implementation (the sky in Core, M30, PR #77):**
- **`BodyOrientation.ToSystem(body, time, direction)`:** turns a direction on the body (its map's
  frame) into the system's at a moment: spun by the day, then leaned by the tilt (Rodrigues'
  rotation). The same turn `SystemView` gives the globe, now in Core so the sky and the drawing
  agree.
- **`Simulation/SkyView.cs`:** `SkyView.From(bodies, observer, spot, heightKm, time)` (null for
  stars and flat worlds, whose tumbling sky isn't worked out yet): every other body as a
  **`SkyBody`**: altitude above the horizon, compass azimuth (clockwise from north), true
  angular diameter, distance, lit fraction ((1 + cos phase angle) / 2; stars and bodies without
  a star fully lit), and its direction in the spot's east/north/up frame; nearest first; and
  the body's own star, whose height will set the sky's colors. Light bending at the horizon
  isn't modeled.
- **Tests** (`SkyViewTests`, 15), each against code that already worked it out another way: the
  body's north turns to the seasons' north pole (no tilt, Earth's, tipped over); the sun's
  highest in a day is the climate's noon height (equator, 45° N, 30° S, both seasons; within
  0.6°); the time it's up is the climate's daylight (within 15 minutes); it rises in the east
  and sets in the west; it's about half a degree across; a moon goes from new to full and is
  full opposite the sun; only globes get a sky; and the same moment gives the same sky.

**Implementation (standing on a world, M30, PR #78):**
- **`Controls/FirstPersonMode.cs`:** **View ▸ Stand Here** stands on the spot at the screen's
  middle (`GlobePicker.PointAt`; flat worlds and stars refused). It makes its own camera
  current, pauses the globe camera (its W/A/S/D panning would otherwise run underneath), hides
  the editing panels and markers listed in `HideWhileStanding`, and puts everything back on
  **Esc**. Moves along great circles at a pace the wheel steps through (walking pace to
  300 km/s); walking keeps the eye 1.7 m above the drawn ground (a carved hole's floor
  included), flying holds its height. Look by dragging; **F** flies, **M** magnifies.
- **Precision:** an Earth-sized globe in floats only resolves to about half a meter, so while
  standing `SystemView` puts the scene's origin at the eye (`StandingOn`, `StandingEye`) and
  hides every other body, orbit line, tail, and belt (the lights stay, to light the ground).
  The camera sits at the origin with its near plane scaled to the eye's height.
- **`Rendering/FirstPersonGround.cs`:** rings of mesh around the spot (96 × 72, fine underfoot
  and spreading out geometrically to beyond the horizon), kept relative to the spot so they
  stay sharp, drawn with the globe's own material; each point's direction from the globe's
  middle rides in `CUSTOM0`, which `planet_surface.gdshaderinc` reads in place of the vertex
  for this mesh. Rebuilt only after moving a quarter of the eye's height, rising or sinking
  by half (it reaches past the horizon, which moves out as the eye rises; since PR #79), or
  when the relief changes.
- **`Rendering/surface_sky.gdshader`** (a sky shader, filled each frame from Core's `SkyView`):
  a day gradient with a sunset glow by the sun's height (full a little after sunrise, a
  quarter as it touches the horizon, gone 3° below), then space (the nebula backdrop through
  `NebulaBackdrop.SkyTexture`, or the background, plus stars fixed to the system) once the sun
  is 2° down; up to 12 bodies as discs at true size, lit on the side toward their star so
  moons show phases (magnified to at least 0.8° across, 3° since PR #79); the sun's disc and
  its glare.
- **Performance** (this machine, fullscreen, uncapped): 94–106 fps standing, against
  121–186 fps on the globe; the regular benchmark is unchanged.

**Implementation (weather overhead and readouts, M30, PR #79):**
- **`Rendering/SurfaceSky.cs`:** the sky's side of `FirstPersonMode`, moved out of it. While
  standing it swaps in a **copy of the scene's environment** (put back on leaving, so nothing
  of the globe's look is restored piece by piece) with the sky as background, sky reflections
  off (they'd have the changing sky filtered into a lighting map every frame), and fog for
  the haze. `ShowBodies` fills the sky from `SkyView`; `ShowClouds` and `ShowHaze` below.
- **Clouds overhead** (`surface_sky.gdshader`): each line of sight is followed up to a layer
  of cloud 5 km above the ground, and the live weather's snapshots are read where it meets
  it: the same images `planet_weather.gdshaderinc` draws on the globe, copied across by
  `PlanetSurface.CopyCloudsTo`, so the clouds overhead are the globe's. The eye's distance and
  the layer's are passed as their difference (a float can't hold both apart). Fine detail
  comes from a tiling noise image (Godot's `NoiseTexture2D`, made once) laid along the ground,
  fading out beyond 20–120 km; it's skipped when the Cloud detail setting is low. Thick cloud
  hides the stars; clouds darken by night, warm toward a low sun, and grey where it rains.
  From above the layer the sky leaves them to the cloud deck (since PR #85).
  `WeatherDisplay` keeps the weather of the body stood on, whose globe its camera no longer
  sees; the ground around the eye leaves the clouds and grid out (`eye_level_ground` in
  `planet_surface.gdshaderinc`), as from below they aren't on the ground.
- **Haze:** exponential fog, half gone at 40 km in clear air, shorter in rain and shorter
  still in snow (down to 1.5 km), colored like the sky at the horizon (pale blue, greyer
  under cloud, dark by night).
- **`Rendering/falling_weather.gdshader`:** rain as three layers of streaks, snow as three of
  drifting flakes, over the whole view, with a grey veil in heavy falls; as heavy as the live
  weather at the spot says (`WeatherMoment.SampleAt`, looked up twice a second) and slanted
  by the wind across the view. None above the cloud layer.
- **`UI/FirstPersonHud.cs`** with **`UI/CompassStrip.cs`:** the controls, a compass along the
  top (a tick every 5°, the points of the compass, the heading under the middle mark), the
  readout for the body under the mouse (name, height, compass direction, distance, how much
  is lit), and a panel with the date (`BodyClock.Describe`), the time by the sun and its
  height, the weather (`LiveWeatherText`) with the temperature, and the height when flying.
- **Core:** `SkyView.SolarTimeHours` (the star's hour angle as a 24-hour time: 12 at its
  highest; none at a pole) and `SkyView.BodyAt` (the body whose disc, at least a set size,
  lies at a direction; where discs overlap, the nearest), with 7 more tests in
  `SkyViewTests`: noon at the highest sun (three latitudes), 6 and 18 at sunrise and sunset,
  none at a pole, picking a disc and missing it, and a moon in front of the sun.
- **Performance** (this machine, fullscreen, uncapped, looking up into partial cloud): about
  72 fps with clouds against 88 without (about 61 with the noise worked out per pixel;
  Godot's half-resolution sky pass would be cheaper still, but doesn't work in the Mobile
  renderer); the regular benchmark is unchanged.

**Implementation (clouds from above, M33, PR #85):**
- **The cloud deck:** while the eye is above the cloud layer (5 km up), a second
  `FirstPersonGround` is built at the layer's radius over the ground here (its `Build` now takes
  any radius for each direction) and drawn with `cloud_deck.gdshader`: unshaded, see-through
  where the cloud is thin, so the ground shows between the clouds, lit for the sun's height
  (`SurfaceSky.DeckMaterial`, filled by `ShowClouds`, which now returns whether there are any).
  It's rebuilt and placed with the ground.
- **Shared clouds:** the cover moved from the sky shader into `cloud_layer.gdshaderinc`
  (`cloud_at`: the weather snapshots plus the fine noise image, fading with distance), which the
  sky and the deck both include, with the same noise texture and ground frame, so climbing
  through the layer doesn't change the clouds. (Painting the globe's clouds on the ground was
  tried first: the snapshots are hundreds of km a pixel, so from a few km up they were one flat
  grey.)
- **Performance** (this machine, fullscreen, uncapped, looking down from 8 km): 41–62 fps with
  the deck against 52 without (noisy: most of the cost is the view itself); 93 fps at eye level;
  the regular benchmark is unchanged.

**Implementation (standing on flat worlds, M33, PR #86):**
- **Core `Geometry/FlatWalk.cs`** (with `FlatSpot` and `FlatFace`): a spot on a flat world's
  top face, rim, or underside; `Point` (with a height off the face), `Frame` (east, north,
  and up: north toward the top's center, up the rim, out toward the rim underneath, so a
  heading carries over the edges), `MapDirection` (the map point under the spot; none off the
  top), and `Walk`, in short steps, each in the frame where it starts, crossing from face to
  face (owner's choice: walking over the rim goes on down the edge and onto the underside).
  Tests (`FlatWalkTests`, 14): south from the center over the rim, down it, and to the
  underside's center; walks undone by walking back (over the edge too); square frames; north
  to the center; map points; heights off each face.
- **Core `BodyOrientation.ShapeToSystem`:** the space a body is drawn in, to the system's: a
  globe's own frame, or a flat world's disc space, which tumbles like a coin (its spin axis
  across the disc) as the 3D view draws it. `SkyView` now works from any point and frame on
  a body through it, and **`SkyView.FromFlat`** gives a flat world's sky: the same across the
  disc (owner's choice), so day and night come to the whole face at once as it tumbles, and
  the underside sees it the other way up; no sun time. Tests (4 more in `SkyViewTests`).
- **The app:** View ▸ Stand Here on a flat world stands on the top face at the screen's middle
  (`GlobePicker` already found it). `Rendering/FlatPatch.cs` is the flat ground around the eye
  (rings on the top or bottom face, a strip of the rim; each triangle turned to face out),
  the top drawn with the globe's material (each point's disc place in `CUSTOM0`, which the
  surface shader already maps for flat worlds) and the rim and underside with the bare rock
  (`PlanetSurface.RockMaterial`); it sits 1 millionth of a radius off the disc's own face so
  the two don't flicker against each other. A second patch at the cloud layer is the flat
  cloud deck; over the top face `surface_sky.gdshader`'s flat layer meets each line of sight
  with a plane over the disc (`SurfaceSky.ShowFlatClouds`). Only the top face has weather.
- **Fixed on the way:** while standing, `SystemView` draws the body stood on at full detail
  (its level of detail had been measured from the set-aside globe camera), and places the
  eye through `ShapeToSystem`.

**Implementation (ground detail, M34, PR #90):**
- **Owner's choice:** a fine texture and small bumps near the feet that match the kind of
  ground, fading with distance, in the Base tier.
- **Where it's drawn:** only on the ground patch around the eye (`eye_level_ground` in
  `planet_surface.gdshaderinc`). Three layers of the shared pattern noise, about 4 m, 1 m, and
  25 cm across, each fading out once a pixel covers more than it (so it's sharp underfoot and
  never shimmers far off).
- **Precision:** the noise is read at the patch's own positions (relative to the point it's
  built around, so precise to well under a millimeter) plus that point's place in the noise,
  which `FirstPersonMode.SetGroundDetail` works out in double precision and wraps to the
  noise's 32-unit repeat. The pattern stays put as the patch is rebuilt while walking. Reading
  the globe's own directions instead would only resolve about 0.4 m on an Earth-sized world.
- **Matched to the ground by its color** (`ground_kinds`): green → grass (tufts and blades),
  sandy → sand (fine grain), white → snow (soft drifts), blue → water (almost nothing), and
  the rest rock and soil (mottling and the strongest bumps). By color rather than terrain type,
  so it works the same on painted terrain and on map images.
- **Bumps** are a height in meters whose slopes (from screen derivatives of the precise
  positions) light and shade the ground's color by how they face the sun, rather than tilting
  the normal: the painterly style lights in bands, and small tilts came out as stripes.
- Checked in the app on painted plains, desert, and mountains at midday and at night. The
  benchmark (orbit view) is unchanged: 154, 140, 153, 154 fps against `main`'s 157, 155, 151,
  156, back to back.
- **After the owner's review (2026-10-06):**
  - About a third stronger, as asked.
  - Up close, the texture unit's coarse blending (about 1/256 steps) showed as rings and bands,
    so the detail's noise is blended in the shader at full precision (`fine_noise`, eight
    texel reads and a smooth curve).
  - Its finer layers are turned (by three times a rotation, whose whole-number entries keep
    the noise's repeat that the stay-put trick needs) so the lattice doesn't show as a grid.
  - Each layer fades by how much ground a pixel covers, worked out from distance and angle,
    so it's smooth across the mesh's triangles.
- **Choosing where to stand** (owner's request, 2026-10-06): View ▸ Stand Here… now asks for a
  click on the selected planet or moon (`FirstPersonMode.ChooseWhereToStand`, through
  `PinPlacer` with its own prompt; Esc cancels), instead of standing at the middle of the
  screen. On a flat world the click's map point becomes the spot on the top face.
- **Moving fast** (owner's report: it seemed to lock up while flicking through speeds):
  - At the fastest speeds the ground was rebuilt every frame, and each build took 33–50 ms.
    The cause was `HeightGrid.IsEmpty`, which scanned the whole grid once for every one of the
    patch's ~6,900 points; it's now worked out once per grid.
  - The ground is also rebuilt at most ten times a second unless the eye gets well out
    across it.
  - The cloud deck is built only once the eye is above the clouds.
  - Top speed went from about 12–21 frames a second to about 40.

**REN-07 — Designed night skies** · Implemented (M34: PR #90) · Base
**Intent:** Each world has its own fixed night sky that the user designs: the stars stay put
from night to night, and the user draws and names constellations in them (owner's request,
2026-10-05; owner's choice: a star field from a seed that can be re-rolled, with named
constellations drawn by joining stars).
**Implementation (M34, PR #90):**
- **The stars** (`Core/Simulation/StarField.cs`): the sky is split into the cells of a
  cube's six faces, 128 × 128 each, and each cell holds at most one star, which has the cell
  as its id. Whether a cell has a star, and the star's place, brightness, and color, come from
  a random stream seeded by the world's seed and the cell, so a seed always gives the same
  stars (about 4,900). A cell's chance follows how much sky it covers, so they're spread
  evenly. **This must never change**, as constellations name stars by id; the exact rules are
  in docs/world-format.md.
- **Constellations** (`Model/Constellation.cs`, `StarLink.cs`): a name and lines joining two
  stars each (up to 200 lines, 200 constellations). Format version 28 saves `starSeed` and
  `constellations`. Older worlds get a seed made from their id, so each keeps one fixed sky.
  Loading refuses a line to a star that isn't on the sky.
- **Drawing them:** `StarSky` (Rendering) has Core make two images in the background
  whenever the sky changes. One has a texel per cell (where the star is, its brightness and
  color); the other has the constellation lines (`ConstellationLines`, 512 × 512 a face).
  `star_sky.gdshaderinc` finds a direction's cell the same way Core does and draws its star
  about a pixel or two across, plus the lines. It's used by the sky seen from a world's surface
  (in place of the old made-up stars) and the sky behind the system.
- **Owner's choice, "optionally from orbit":** View ▸ **Stars from Orbit** (off at first)
  shows them behind the system, and View ▸ **Constellation Lines** (on) shows or hides the
  lines in both views.
- **Designing them:** System panel ▸ Night Sky ▸ **Design Night Sky…** opens a full-window
  page (`UI/SkyPage.cs`, `UI/SkyCanvas.cs`). It shows the whole sky laid flat like a world
  map, every star sized by brightness, with wheel zoom and right-drag pan. Clicking a star and
  then another joins them in the chosen constellation, and clicking on keeps drawing from the
  last star. Clicking one of its lines erases it, and Esc stops drawing, then closes. Next to
  it: New, Delete, and the name. **New Stars** re-rolls the seed, but only once there are no
  constellations (its tooltip says why), since they're drawn on these stars. Every change is
  undoable (`WorldSession.Sky.cs`; the sky is part of `LoreState`).
- Tests: 13 for the star field and lines (the same stars forever, evenly spread, ids matching
  cells, cube faces round-tripping), and the version 28 golden file with damaged-sky refusals.
- Checked in the app: drawing a 3-line constellation by clicking stars and renaming it, the
  stars and line behind the system, and the stars by night (gone by day) from the ground.

### 4.2 Interface Layout (`UI`)

**UI-01 — Main screen layout** · Implemented (M10: PR #31, PR #32) · Base
**Keyboard and number fields (owner's request, PR #18):** while a text or number field is being
edited, the camera ignores WASD and the arrow keys. In number fields, Up/Down change the value
by one step (ten with Shift; `godot/UI/NumberFields.cs`). Clicking the view (not a panel)
finishes editing, so the keys move the camera again.
**Intent:** The main view shows the world or map in the center. Panels along the sides of the
screen hold tools, journals, and similar content.
**Notes:** Views that need a lot of space, like large diagrams, may need their own tab or page
(see `LORE-04`).
**Implementation (menus, PR #31):**
- The top bar (`godot/UI/MapToolbar.cs`): the **File**, **Edit**, **View**, and **Add** menus,
  then the panel buttons **System**, **Map**, **Journal**, **Regions**, **Timeline** (no "…":
  they open panels). Map, Journal, and Regions share the right side, one at a time. The bar
  also owns the message line other parts of the app use (`ShowInfo`, `ShowWarning`,
  `ShowError`).
- `godot/UI/ViewMenu.cs`: checkable **Pins**, **Weather Pins**, **Regions**, **Season Markers**,
  **Eclipse Markers**, **Grid (G)**, and **True Scale** (moved here from the time bar). The checks
  are refreshed each time it opens. Behind it: `SystemView.ShowGrid` (shared with the G key),
  `BodyMarkers.ShowSeasonMarkers`, `EclipseMarkers.ShowMarkers`, and the existing switches.
- `godot/UI/AddMenu.cs` (Claude's addition): **Planet**, **Moon**, **Star**, **Region**,
  **Weather Pin**, **Journal Entry**, **Timeline Event**, each opening its panel and starting
  there (Moon, Region, and Weather Pin are greyed out while a star is selected). It replaces
  the Weather Pin button.
- `godot/project.godot`: the window starts maximized (`window/size/mode=2`).

**Implementation (Map panel, PR #32):**
- `godot/UI/MapPanel.cs` (was `PiecesPanel.cs`): one panel for the planet's map, replacing the
  Pieces panel. Like the Journal and Regions panels, it runs from below the top bar to above
  the time bar and scrolls when the window is short, so a selected piece's fields stay
  reachable at 1152 × 648.
- `godot/UI/MapImageSection.cs`: its top half, moved out of the top bar: **Import Map…**,
  **Clear Map**, **Calibrate…**, **Map type**, and **Fill color** (shown only for map types
  that don't cover the globe), with the messages describing how a map is shown. With a star
  selected it says stars have no map. `MapPanel.ImportAsync` (used by `--map=`) passes through
  to it.
- The lower half is the old pieces list and fields, unchanged apart from a shorter list.
- **Verified in the running app** with real clicks, maximized (1920 × 991) and at 1152 × 648:
  every control is on screen and sized; Calibrate… opens (the panel hides) and Esc closes it;
  the Map type list opens and Robinson hides Fill color; Clear Map clears and disables
  Calibrate…; with a piece selected at the small size the panel scrolls and Delete works; a
  star shows the note and disables the buttons; clicking Journal closes the Map panel.

**Fixes (M15, PR #41):**
- **Dropdowns** (`godot/UI/Dropdown.cs`, used for every dropdown): open when the click ends.
  In a short window a list can open over its own button, and the end of the same click picked
  the item under the mouse: at 1152 × 648 one click on the time bar's speed set it to "1 year /
  second" (found with realistic 150 ms clicks, and confirmed on `main`).
- **Side panels** (`godot/UI/PanelStyle.cs`): one solid background for System, Map, Terrain,
  Journal, and Regions. The markers and labels drawn over the globe sit beneath the panels, but
  showed through their slightly see-through backgrounds.

**UI-02 — System tree panel** · Implemented (PR #18) · Base
**Intent:** A compact tree view of the star system's hierarchy (e.g. Sun ▸ Planet ▸ Moon) showing
which bodies orbit which. It likely lives in a side panel and doubles as a way to select bodies for
editing. This is separate from lore relationships (`LORE-04`).
**Notes:** Was tabled until multiple bodies existed (M4).
**Implementation (PR #18):** `godot/UI/SystemPanel.cs`, opened by **System…** in the toolbar,
on the left (owner's choice).
- **Tree:** built from `SystemHierarchy.ChildrenOf`, starting from bodies without an orbit. Picking
  a body selects it and flies there. The tree is rebuilt only when names, kinds, or parents change.
- **Add Planet / Add Moon / Add Star / Delete.** Starting values come from Core's
  `Simulation/NewBodies.cs` (tested): Earth-, Moon-, and Sun-like sizes. Each new orbit goes 1.6×
  farther out than the widest around the same parent, siblings are spread by the golden angle,
  and starting periods grow as real ones do (period² ∝ distance³). Delete removes the body and
  everything orbiting it (`SystemHierarchy.DescendantsOf`), with no confirmation because Ctrl+Z
  restores it. The last body can't be deleted.
- **Properties:** name, kind (planet ↔ moon; stars stay stars), radius, day length, axial tilt,
  and the orbit. The orbit fields are the parent (only bodies that wouldn't make a loop), the
  distance in km or AU, the period, and the start angle. A switch shows the extras: elongation,
  closest point, tilt, and tilt direction. Values that aren't allowed are refused with the
  reason, and the fields go back to the real values.
- **Path and live updates while editing (owner's request):** while the panel is open, the
  selected body's path is always drawn, even close up, in the selection yellow
  (`SystemView.HighlightedOrbit`). Number fields apply as you type (`SpinBox.UpdateOnTextChanged`,
  via `NumberFields.WithLiveTyping`), so the path, position, and size update on every keystroke.
  Updates from the world never overwrite a field being typed in (`ShowValue`), and half-typed
  numbers don't flash error messages. Rapid typing merges into one undo step. Same for the
  Pieces panel's fields.
- **Full-circle angles wrap (owner's request):** start angle, closest point, and tilt direction
  (0–360°), and a piece's rotation (0–360°) and longitude (−180–180°), wrap around when stepped or
  typed past their ends: 359 + 1 is 0, and 0 − 1 is 359 (`NumberFields.WithWrapAround`). Angles
  with real limits (axial and orbit tilt 0–180°, latitude ±90°) stop at their limits.
- **Smooth paths at high elongation (bug fix):** orbit lines used to be sampled at even time
  steps, so a very elongated orbit had only a few points where the body rushes past its parent,
  drawn as a sharp-cornered path the body didn't follow. Points are now spread evenly along the
  curve (`OrbitMath.EvenlySpacedTimes`, steps in eccentric anomaly), and each one is still exactly
  where the body is at that moment. The test that reproduces the bug failed before the fix, with
  steps turning over 16°.
- **Make Center (owner's request and decision, 2026-09-30: "swap places, keep motion"):** the
  selected body becomes the center of its system. `SystemHierarchy.MakeCenter` flips the chain
  between it and the old center: each body it orbited now circles the one below it on the same
  path, mirrored (start and closest-approach directions +180°, everything else unchanged).
  Other orbits stay as they were. A test proves every body keeps the same position relative to
  the others at any time, even with elongated, tilted orbits. Make a planet the center and the
  sun circles it once a year; the time bar's "1 year" then follows the sun's trip.

**UI-03 — Undo and redo** · Implemented (PR #14) · Base
**Intent (owner, 2026-09-30):** undo and redo for edits, "sooner rather than later", so mistakes
(a bad drag, a deleted piece) are easy to take back. The **Delete** key removes the selected
piece.
**Design decisions (owner, 2026-09-30):**
- **Covers all world edits:** pieces, map type, fill color, calibration, and Import/Clear Map.
- **Edit menu** beside File, naming what will be undone ("Undo Move Piece 1"), plus **Ctrl+Z**
  and **Ctrl+Y / Ctrl+Shift+Z**.
- **Deleting doesn't ask first**, since it can be undone.
- **Saved means "matches the file"** (owner, 2026-09-30): if the world is the same as its saved
  version (after undoing back, or moving something back by hand), it counts as saved again.
**Implementation (PR #14):**
- **Snapshots, not per-edit undo code.** Before each edit, `WorldSession` records a copy of the
  planet's `SurfaceSettings` (small: no images) in a Core `Editing/UndoHistory<T>`, which is
  generic and tested. Undo swaps the copy back (`SurfaceSettings.RestoreFrom`). Any new edit
  gets undo by calling `RecordUndo("…")` first. **Reuse this for every future edit.**
- **One step per action:** `BeginGesture` / `EndGesture` turn a whole drag (move, rotate,
  resize) or a whole Calibrate session into one step. Rapid edits of the same thing merge into
  one step when they come within a second (the fill color picker, number fields). The history
  keeps 100 steps, clears on New/Open, and survives saving. Undo waits while a save, open, drag,
  calibration, or cut is in progress.
- **Images:** undoing Import/Clear Map reloads the other map image. Piece textures stay in memory
  while the history still has the piece, so undoing a delete is instant. If an image only the
  history needs lives in the file being saved over, it's copied to a temporary folder first
  (`Core/Storage/AssetStash.cs`), so undo can still bring it back and later saves still include it.
  The folder is deleted when the world closes.
- **Saved state:** `WorldSession` keeps the surface as saved and compares with it after every
  edit (`SurfaceSettings.HasSameContent`, tested for every kind of edit), so the • and the
  close warning disappear when the world matches its file again. A recovery copy is deleted then
  too. A recovered world stays unsaved until it's saved.
- **UI:** `godot/UI/EditMenu.cs`. The Pieces panel's Delete button and the **Delete** key
  (`InputActions.DeleteSelection`) delete right away and say "Ctrl+Z to undo". Text fields keep
  their own Ctrl+Z and Delete while being edited.
- **Verified in the running app** (real keyboard and mouse input): drag → Ctrl+Z → Ctrl+Y;
  Delete → Ctrl+Z (texture back instantly); five fill color changes undone in one step; Clear
  Map → save over the file → Ctrl+Z reloaded the map from the temporary copy, and saving again
  wrote it back into the file.

**UI-04 — Metric and imperial units** · Implemented (M32: PRs #82–#83) · Base
**Intent:** A setting to switch everything between the imperial and metric systems for
measuring distance, mass, volume, area, temperature, and so on (owner's idea, 2026-10-05).
**Implementation (Core, M32, PR #82):**
- `Measurement/UnitSystem.cs` (Metric, Imperial) and `Measurement/Quantity.cs`: the kinds of
  measurement that switch (distance, length, speed, temperature, temperature change,
  precipitation, area, volume, mass, density), each stored in its metric unit.
- `Measurement/Units.cs`: `ToShown` and `ToMetric` (exact both ways: US customary factors,
  °F with its offset; a temperature change without it), `Symbol`, `Format` (in the computer's
  number style), `FormatDistance` (AU from a tenth of one in both systems, then millions of km
  or miles, then whole ones: the rule `OrbitGuideSection` and `FirstPersonMode` each wrote
  out, for them to share), `KmPerAu` (now also behind `CometTail.KmPerAu`), and `DefaultFor`
  (a locale's region: imperial for US, LR, MM; metric otherwise or when there's no region).
- Tests (`UnitsTests`, 34): known values for every quantity, exact round trips in both
  systems, symbols, formatting, distances, and the default by region.

**Implementation (the app, M32, PR #83):**
- **The setting:** `AppSettings.Units` (in `user://settings.cfg`; until chosen,
  `Units.DefaultFor(OS.GetLocale())`), read once and kept, with a `UnitsChanged` event.
  **Units** is the first row of File ▸ Settings.
- **`UI/UnitText.cs`:** `Format`, `Distance`, `Symbol`, and `Shown` with the setting filled in,
  for readouts.
- **`UI/UnitFields.cs`:** turns a number field into a measurement: `WithUnit(quantity, metric
  limits, steps)`, `ShowMetric(value)`, and `MetricValue()`. A field remembers the metric
  value it was given and hands it back exactly unless the user changed the number (so a
  radius stored in km never drifts through a field in miles), and relabels itself when the
  units change. Two lessons from testing, written into it: signals are held while it
  relabels (new limits make Godot round the value again, which reached the world as an edit
  and stored a radius in miles as km), and its limits sit on whole steps (Godot rounds to
  steps counted from the lowest value, which turned a typed 4,000 miles into 3,999.62).
- **Fields:** the System panel's radius, average temperature, and density; the Shapes
  section's sizes; the terrain brush's size (by hand, its limits following the body); the
  orbit distance (by hand beside its AU choice, with the same untouched-stays-exact rule).
- **Readouts:** mass for small bodies, the radius and disc tooltips, the weather window
  (temperatures, rain by month and year, its notes) and its chart's scales, live weather
  (rain rates, wind), climate tooltips (how much colder, as a temperature change),
  asteroids, the orbit guide's zones and limits and first person's sky readouts (both now
  using `Units.FormatDistance`), the map's scale bar (a round number of km or miles), the
  first-person panel (temperature, height, speed), the sculpt height, and the temperature
  limit message. Panels showing measurements refresh when the units change.
- Tested in the app (behind the owner's windows): the default followed this computer's US
  region; switching to imperial showed 3,959 mi, 59 °F, and 344 lb/ft³; another edit left
  the radius at exactly 6,371 km; typing 4,000 mi stored 6,437.376 km; 50 °F stored 10 °C;
  and switching back to metric kept everything as stored.

**UI-05 — Tool help** · Implemented (M34: PR #87) · Base
**Intent:** Someone picking up the app for the first time can use every tool: each tool says
in one line how to use it, and when a tool can't do something, it says why and what to do
instead of doing nothing (owner's request, 2026-10-05, after painting and sculpting seemed not
to work).
**Implementation (M34, PR #87):**
- **What the run-through found** (every tool clicked through in the app, 2026-10-05): all of
  them worked. Painting and sculpting only looked broken. The default Ocean barely shows on
  an unmapped, ocean-blue planet. A 500 m sculpt is far too small to see from orbit, even at
  10× relief. And nothing said why a tool did nothing when it couldn't act.
- **The hint bar:** `MapToolbar.SetHint(tool, text)` shows one line on how to use the open
  tool, in a dark strip centered along the bottom (between the camera text and the time
  bar). The tool given a hint last is shown; closing it brings back the one before. The
  System, Map, Terrain, Regions, and Journal panels set theirs when they open, and change it
  as they need: the Map panel's says the next step (import a map, then calibrate or cut, then
  drag pieces); the Terrain panel's follows its mode, and for sculpting explains true scale
  and how to see small heights (zoom in, View ▸ Relief, View ▸ Stand Here). The Terrain and
  Shapes how-to paragraphs moved into it. The timeline strip, diagrams page, and cut editor
  keep the hint lines they already had.
- **Disabled buttons say why** (`UI/DisabledTip.cs`: `Apply(button, tip, whyNot)` with shared
  reasons `Busy`, `NoMap`, `NoSurface`): the map's Import, Clear, Calibrate, Cut from Map,
  Cut from Image, and Reset Points; the System panel's Add Moon, Delete, and Make Center;
  New Region; Add Relationship; the event editor's Pin on Globe; Add Nebula; and the diagram
  page's Delete, Add to Diagram, Arrange, and Save as Image.
- **Edits that wait say so:** `MapToolbar.ShowBusyWarning()` when the world is saving,
  opening, or loading a map and the user paints, sculpts, places or drags a shape, drags or
  deletes a piece, imports a map, opens the cut editor, or uses the File menu.
- **Each brush stroke says what it did:** "Done: Paint Ocean. Ctrl+Z takes it back.", with
  the height for Raise and Lower, or "Nothing changed" when the ground was already like that
  (`TerrainBrush.ShowDone`).

**UI-06 — Start screen and standalone app** · Implemented (M34: PR #89) · Base
**Intent:** The app opens to a start screen (New World, Open World, Recent Worlds, Settings,
Quit) and can be launched on Windows by double-clicking it, without Godot (owner's request,
2026-10-05).
**Implementation (M34, PR #89):**
- **Start screen** (`UI/StartScreen.cs`): a full-window page over everything (the 3D view
  stops drawing behind it) with the name, New World, Open World…, Settings…, Quit, and the
  recent worlds. `Main` shows it at launch unless a command-line option says what to do
  (`--open`, `--map`, `--benchmark`). New World just closes it (a fresh world is already open);
  Open and Settings use the File menu's own (`FileMenu.ShowOpenDialog`, `ShowSettings`); Quit
  goes through the same unsaved-changes check as closing the window. It closes whenever the
  world is replaced, so opening, starting, or recovering a world (whose prompt shows on top
  of it) all leave it.
- **Recent worlds:** up to 8, newest first, saved per computer in `user://settings.cfg`
  (`AppSettings.RecentWorldPaths`, `RememberWorld` after every open and save). The rule is in
  Core (`Storage/RecentWorlds.cs`: moving a world to the top, no duplicates even if the letter
  case or slashes differ, at most 8; 6 tests). A world file that's gone is shown greyed, with
  "Not found: … (it may have been moved, renamed, or deleted)" as its tooltip.
- **The Windows app:** `godot/export_presets.cfg` has a "Windows Desktop" preset (64-bit,
  release, no resource editing so no extra tools are needed) that builds into
  `build/windows/` (git-ignored); `project.godot` points Godot at the solution in the repo's
  root (`dotnet/project/solution_directory`). The build is about 183 MB: the .exe, its `.pck`,
  and the .NET runtime folder. How to build it is in CLAUDE.md §9. Export templates come from
  Godot's official release, checked against its SHA-512 sums.
- **Verified:** in the app (behind the owner's windows), the start screen showed at launch, a
  saved world appeared under Recent Worlds and opened from it, a missing one was greyed with its
  reason, New World and Settings worked; the exported .exe started on its own without errors.

**UI-07 — Clearer look** · Implemented (M38: PR #94) · Base
**Intent:** Every panel, menu, and dialog is easy to read and operate: buttons and switches
look like what they are, panels are laid out alike, long panels fold into sections, menus are
grouped, and whatever can be shown is shown rather than described (owner's request,
2026-10-08).
**Implementation (PR #94):**
- **The audit:** a scripted run through every menu, panel (scrolled through), dialog,
  setting, and first person, with screenshots. It found:
  - buttons that read as plain text, and on/off switches drawn as a grey dot;
  - a System panel 1,900 px long, and a 20-item View menu;
  - panels at full height however little they held;
  - no hint on Diagrams or Timeline;
  - "Day 0" and "Planet (planet)";
  - an unexplained green and red disc (the Orbit Guide).
- **One look** (`UI/AppTheme.cs`), merged into Godot's default theme at startup, so every
  control on every layer and in every dialog takes it:
  - buttons are filled, outlined boxes;
  - the chosen mode (the top toolbar, Paint/Sculpt, Raise/Lower) is blue;
  - buttons that can't be used are dimmed;
  - on/off switches are drawn as switches, blue when on;
  - every look of a button keeps the same margins, so nothing shifts or clips when it's
    pointed at.
- **Folding sections** (`UI/FoldingSection.cs`): a heading that folds what's under it, and
  remembers on this computer whether it was open (`AppSettings.IsSectionOpen`). The System
  panel is now:
  - **Star System**, the list, and the Add buttons;
  - the body's name;
  - **Body** (open at first) and **Appearance** (with rings, belts, and the world tree);
  - **Orbit** (open), with the stable-orbit guide, its new color key, and physics;
  - **Calendar & Seasons** (open) and **Eclipses & Showers**;
  - **Night Sky & Nebulas**, below a line, since it belongs to the whole system.
- **Panels as tall as what's in them** (`PanelStyle.FitHeight`): Map, Terrain, and System
  shrink to their contents and scroll only when the window is too short. The Map panel went
  from full height to 415 px.
- **The View menu** keeps every item, under the headings On the Globe, Weather, Sky, and
  Orbits.
- **Hints** for Diagrams and Timeline, as every other tool has (`UI-05`).
- **Wording:**
  - no "Day 0": the day before Day 1 is "Day -1", as years count (`LocalTime.DayName`);
  - a body named like its kind shows once ("Planet", not "Planet (planet)").
- **Reuse:** `AppTheme.SectionHeader` and `FoldingSection` for any panel's parts;
  `PanelStyle.FitHeight` for any side panel.

### 4.3 Maps & Image Import (`MAP`)

**MAP-01 — Import map image in a supported layout** · Implemented (equirectangular: M1; other layouts: M2, see `MAP-04`) · Base
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

**MAP-02 — Manual map placement onto the globe** · Implemented (M3) · Base
**Intent:** For maps that aren't in a supported layout, the user cuts the image up and places it
onto the globe themselves, adjusting for distortion and distance. (Flat maps don't map one-to-one
onto spheres: areas near the equator are close to true size, and areas near the poles are stretched.)
**Notes:** One of the most complex features. Needs its own design review.
- **Owner, 2026-09-30** (after testing `MAP-04`): the user should be able to **cut out parts of
  their image, move them around the globe, and resize them**. This is one of two ways to fit
  imprecise maps; the other is `MAP-05`.
- Best for maps of part of a world, maps with no consistent layout, and combining several
  regional maps.
**Design decisions (owner, 2026-09-30):**
- **Cut shapes:** a rectangle (drag a box) **and** freeform (click points around a region).
- **Placing:** directly **on the globe**. Drag to move, corner handles resize, a handle rotates.
- **Sources:** pieces can come from **any image** (e.g. extra regional maps). Each piece
  remembers its source, and all are saved inside the world file.
- **Handles work like other map makers (owner, 2026-09-30, after trying PR #12):** typing
  numbers was hard to use as the main control. Click a piece to select it, drag it to move,
  drag a corner to resize, drag a handle to rotate. The number fields stay for exact placement.
- **Resizing keeps proportions** (owner, 2026-09-30). Stretching one way is done by warping.
- **Warping (owner, 2026-09-30):** an Edit Points mode lets the user drag **every point of the
  cut** on the globe (4 corners for a rectangle, each clicked point for a freeform cut), and the
  image stretches smoothly to follow. What was cut out doesn't change. Needs world file format
  version 4 (where each point was dragged to). Its own PR, after the handles.
- **Entering Edit Points (owner, 2026-09-30):** the panel's **Edit Points** toggle, or
  **double-clicking** a piece. Esc (or clicking empty space) leaves it.
- **Count:** up to a few dozen, drawn **live** for instant feedback. The limit is **32 per
  planet**, to keep the Base tier fast; the cost is measured in the app PR.
**How pieces sit on the globe:** like a sticker. Distances from the piece's center are true
(azimuthal equidistant), so small pieces look exactly as drawn, and very large ones (over about a
quarter of the globe) stretch toward their edges. Pieces sit on top of the map; later pieces cover
earlier ones.
**Implementation (Core, PR #11):**
- `src/NothicWorlds.Core/Model/MapPiece.cs` holds the source asset, outline, center, rotation,
  and width in degrees (the height follows from the cut's true shape). It lives in
  `SurfaceSettings.Pieces`, and `Clone()` copies pieces independently.
- `Maps/PieceOutline.cs` is **immutable**: rectangle or freeform (3–1000 points on the source
  image), validated (inside the image, encloses an area). `RasterizeMask` draws the cut-out mask
  with smooth, anti-aliased edges (a 4×4 samples-per-pixel scanline fill, fast even for long
  outlines). Reuse it for any polygon mask.
- `Maps/PieceProjection.cs` covers globe → box position (for drawing) and box → globe (for
  handles), plus the center/east/north vectors for the shader. It uses **full precision, and
  atan2 rather than acos** for distances. The acos version misplaced tiny pieces by up to ~5%
  (caught by a test); the shader must use the same atan2 form.
- **World file format version 3:** optional `surface.pieces`. Piece source images are saved and
  loaded like the main map. Versions 1 and 2 upgrade automatically; golden tests for all three
  versions pass.
**Implementation (editor, PR #12):**
- **Drawing:** `planet.gdshader` draws up to 32 pieces in a loop after the map and before the
  grid (`draw_pieces`), using the same atan2 sticker math as `PieceProjection`. Each piece is
  skipped cheaply unless the pixel is within reach of its corners. Mip levels are chosen from the
  pixel's size on the globe, so the loop has no seams. `PlanetSurface.SetPieces` fills the
  uniform arrays; it's cheap enough to call on every edit.
- **Textures:** `godot/Maps/PieceTextureBaker.cs` crops the piece's box from the **original**
  image, applies the anti-aliased mask as transparency, shrinks it to at most 4096 px, and
  compresses it (DXT5), like the main map. `WorldSession` bakes each piece when it's added, and
  bakes all pieces when a world opens (decoding each source image once). A piece whose image
  can't be read is kept (so saving doesn't lose it) and reported.
- **Session edits** (`WorldSession`): `AddPieceSource` (registers an image; it's saved only once
  a piece uses it), `AddPieceAsync`, `PlacePiece`, `RenamePiece`, `RemovePiece`,
  `MovePieceInOrder`. The most recently decoded source image is kept in memory while cutting
  and freed when the editor closes. After a save, only the images actually saved are repointed
  to the world file (`WorldPackage.ReferencedAssetNames`, now public).
- **Cut editor** (`godot/UI/CutEditor.cs`, `CutCanvas.cs`): full screen, with the 3D view
  paused. Rectangle (drag a box) or freeform (click points; click the first point or press Enter
  to close; Backspace removes a point). Scroll zooms around the mouse, right/middle-drag pans,
  Fit to View resets. `IsCutting` blocks File → New/Open meanwhile. Esc cancels.
- **Starting placement** (`Core/Maps/PieceStartingPlacement.cs`): a cut from the main map starts
  **exactly where that part already shows**, including calibration (undone with the new
  `MapCalibration.TrueLatitude` / `TrueLongitude`), so nothing appears to jump. Its width comes
  from the arcs from its center to its left and right edges. Other images, and cuts outside a
  map's outline, start at the middle of the view, 30° wide.
- **Pieces panel** (now the lower half of the Map panel, `godot/UI/MapPanel.cs`, since `UI-01`): the
  list (top = drawn on top), Cut from Map… / Cut from Image…, name, exact
  latitude/longitude/rotation/width fields, Move Up / Move Down, and Delete… (confirmed). It hides
  whenever the toolbar does.
- **Verified in the running app** (simulated mouse input): a rectangle cut from the main map
  landed seamlessly over its source area; moving, rotating, and resizing it, plus a freeform
  triangle cut from a second image, drew correctly with smooth edges and the right layering. Save
  and reopen restored both pieces and stored both images.
- **Benchmark (32 pieces):** 32 large pieces (each about a third of an 8k map) cost about 20% of
  the frame rate: ~147 → ~118 fps, video memory 143 → 255 MB. This is well within Base.
**Implementation (handles, PR #13):**
- **Core math** (`Core/Maps/PieceManipulation.cs`, tested without Godot): `PieceAt` finds the
  topmost piece whose **cut shape** (not just its box) is under a point, so clicks go through the
  empty part of a freeform piece's box (`PieceOutline.Contains`). `Move` rolls the piece along
  the globe so the grabbed spot stays under the mouse without twisting. `Rotate` follows the
  mouse's bearing around the center, and `Resize` scales with its distance from the center.
  Each works from the drag's starting state, so long drags don't drift.
  `SphericalCoordinates` gained `ArcDegrees` and `BearingDegrees`.
- **`godot/Controls/GlobePicker.cs`:** screen ↔ planet surface (ray–sphere). Past the planet's
  edge a drag uses the nearest edge point, so it keeps working. The Cut editor uses it too.
- **`godot/Controls/PieceHandles.cs`:** draws the selected piece's box, curved to follow the
  globe, with corner squares and a rotate handle a fixed screen distance above the top edge.
  Parts on the far side are hidden. It works only while the Pieces panel shows, so left-drag
  orbits as usual otherwise, and clicks that miss every piece still orbit. Shift snaps rotation
  to 15°, Esc deselects, and the cursor shows what a drag will do. The node comes after the
  camera in the scene, so it gets clicks first.
- **Selection** lives in `MapPanel` (`SelectedPieceId`, `Select`, `SelectionChanged`), shared
  by the list and the globe. The list is only rebuilt when names or order change, not on every
  drag step.
- **Verified in the running app** (simulated mouse input through the real input path): a click
  selected a piece, and dragging the body, rotate handle, and corner moved, turned, and resized
  it. Clicking empty space deselected it, and left-drag then orbited the camera.
**Implementation (Edit Points, PR #15):**
- **Data:** `MapPiece.WarpedPoints`, one position per outline point in the piece's box, so a
  warp moves, turns, and resizes with its piece. World file format version 4 (optional
  `warp`); version 3 files upgrade with nothing to change.
- **The stretch** (`Core/Maps/PieceWarp.cs`): mean value coordinates over the outline. Every
  position is a smooth blend of the outline points, exact at each point, straight along each
  edge, with no creases, for any outline shape (tested with a concave L). It's fast enough for
  hundreds of points.
- **Drawing:** `BakeLookup` maps a 48 × 48 mesh (one extra cell past each side) through the
  stretch and rasterizes the **reverse** into a 64 × 64 `WarpLookup`: for each spot of the
  stretched piece, which spot of the image belongs there. All lookups share one 512 × 256 float
  atlas, rewritten only per changed tile. The shader blends the four nearest cells exactly like
  `WarpLookup.Sample`, and cuts the edge exactly where the image ends (not along the cells), so
  edges stay as clean as unwarped pieces. The quick distance check uses the stretched reach.
- **Clicking** uses the same lookup (`PieceManipulation.PieceAt` with
  `WorldSession.WarpLookupFor`), so what's drawn and what's clickable agree.
- **UI:** `PieceHandles` shows a handle on every point in Edit Points, and the outline follows
  the stretched shape. The normal frame (corners and rotate handle) wraps the stretched shape.
  `MapPanel` has **Edit Points** (toggle) and **Reset Points**. Each point drag is one undo
  step ("Edit Points of Piece 1"), and so is Reset.
- **Verified in the running app** (real input): double-click entered Edit Points, dragging the
  top-right point stretched the image smoothly, Esc left the mode, clicking the stretched area
  selected the piece, Ctrl+Z/Ctrl+Y removed and restored the warp, and save → reopen kept it.
- **Benchmark (32 large pieces, all warped):** ~120 → ~92 fps, video memory unchanged. Still
  Base.

**Implementation (cutting shapes and grid snapping, M34, PR #88):**
- **More cutting shapes** (owner's choice): **Ellipse** (drag a box; Shift for a circle) and
  **Regular Shape** (drag from the middle out to a corner; 3 to 12 sides, a hexagon to start;
  Shift turns it in 15° steps). Shift on **Rectangle** makes a square. In Core:
  `PieceOutline.Ellipse` (96 points), `RegularShape` (worked out in the image's true
  proportions, corners past the edge pulled onto it), and `SquaredCorner`. They're ordinary
  outlines, so the file format doesn't change. `CutCanvas` shares one drag path for the
  three box-like tools.
- **Snap to Grid** (owner's choice: the center *or* an edge snaps, to a chosen step): a toggle
  and a step (15°, the drawn grid; 5°; or 1°) under the cut buttons. While dragging a piece,
  `GridSnap.SnapPiece` moves its center so the center or the middle of one of its edges lies on
  a latitude line, and likewise for longitude, by whichever is nearest. On a large or turned
  piece, moving the center doesn't move the edges by exactly as much, so the move is refined
  until the chosen points are on their lines. With Snap on, turning snaps to 15° as Shift does.
  The setting isn't saved (it's how the user works, not part of the world).
- **Center on Grid:** for a piece, `WorldSession.CenterPieceOnGrid` moves its center to the
  nearest crossing. For the main map (owner's choice: its middle on a crossing), the map has
  no position of its own, so `MapCalibration.CenteredOnGrid` moves every calibration guide
  instead: longitude guides all shift (wrapping past 180°), and latitude guides shift with a
  guide pinned where the image's middle is drawn, so the middle lands exactly (guides pushed
  next to a pole are dropped). An uncalibrated map's middle is already at 0°, 0°, so the button
  says so. Each is one undo step.
- Tests (20 new): shapes in true proportions, clamping, sides limits, snapping by center or edge,
  exactness on an 80° turned piece, and centering a calibrated map (including the wrap).
- **Verified in the running app** (behind the owner's windows): an ellipse (96 points) and a
  hexagon cut from a test map and added as pieces; with Snap at 5°, dragging a piece put its
  center on 20°W, and then its west edge exactly on 55°W; Center on Grid moved it to 15°N
  15°W; and the map's Center on Grid moved a calibrated map's middle from about 6°S 9°E to 5°S
  10°E.

**MAP-05 — Grid calibration (adjust how the map's lines project)** · Implemented (M3) · Base
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
**Design decisions (owner, 2026-09-30):**
- **Approach: guide lines.** Drag latitude and longitude lines on the flat image to where they
  really are, and add lines at specific values. Pin points and a mesh grid were the alternatives,
  possible later "advanced" modes.
- **Workspace: side by side.** The flat image and lines on the left, the live globe on the right.
- **All map types** can be calibrated.
- **Default guides every 30°** of latitude (60°S–60°N) and every 60° of longitude.
**How it works for every map type:** calibration doesn't bend the image. It changes which
latitude/longitude the map type reads: "true latitude 30° is drawn where the map type puts
33.5°". So one method covers all types: straight lines on rectangular types, curved meridians on
Robinson/Mollweide/Winkel, and rings and spokes on Polar. Each line moves as a whole (it can't be
bent locally; that would be the mesh-grid mode). The poles stay fixed.
**Implementation (Core, PR #9):**
- `src/NothicWorlds.Core/Maps/MapCalibration.cs` is **immutable** (every edit returns a new one,
  ready for undo). It evaluates `DrawnLatitude` / `DrawnLongitude` through a smooth monotone curve
  (`MonotoneCurve`, Fritsch–Carlson) that never reverses, so lines can't cross. Longitude wraps
  smoothly across 180°. Edits: `WithLatitudeDrawnAs` / `WithLongitudeDrawnAs` (kept between
  neighbors), `AddLatitude` / `AddLongitude` (start where the value is currently drawn),
  `Remove…`. `Bake…Table` produces lookup tables for the shader.
- `MapProjectionInverter.cs`: the image position → latitude/longitude, for any map type
  (numerical: a cached coarse lattice, then Newton refinement). Returns null outside the map's
  outline. Used to turn mouse drags into values. Reuse it for anything that needs "what's under
  the mouse" on a flat map.
- **World file format version 2** (first format change): optional `map.calibration`. Version 1
  files upgrade automatically. Golden tests for **both** versions must pass forever.
- Also: points exactly on a coverage edge (a polar map's rim) now count as on the map, despite
  rounding.
**Implementation (workspace, PR #10):**
- **`godot/UI/CalibrationWorkspace.cs`:** the toolbar's **Calibrate…** button (enabled when a
  map is loaded) opens it. The left half is a solid panel with Add Latitude… / Add Longitude… /
  Reset / Cancel / Done (buttons wrap on narrow windows). The **right half is a separate view of
  the same 3D world**, centered on the planet. (Shifting the main view sideways stretched the
  globe into an oval, the same wide-lens effect as the deferred `REN-02` issue.) The main 3D view
  is paused meanwhile, so nothing renders twice. The preview camera mirrors the planet camera,
  and mouse input passes through, so orbit and zoom still work. The true lat/long grid shows while
  calibrating, and the toolbar is hidden (its map type and fill controls mustn't change
  mid-calibration). **Esc** = Cancel, **Enter** = Done.
- **`godot/UI/CalibrationCanvas.cs`:** draws the image with each guide line where the
  calibration puts that true latitude/longitude: straight lines, curved meridians, or rings and
  spokes, broken where they leave the map or jump between circles. Labels sit at 150°W / 45°S,
  where lines are spread out. Hovering highlights a line. **Drag** converts the mouse position to
  the map type's own latitude/longitude with `MapProjectionInverter` (exactly the new "drawn
  as" value). **Right-click** removes a line.
- **Session:** `WorldSession.SetCalibration` (tracked as an unsaved change), plus
  `BeginCalibration` / `CancelCalibration`, which restores both the calibration and the prior
  unsaved state. `IsCalibrating` blocks File → New/Open until Done or Cancel. A newly imported
  map starts uncalibrated.
- **Rendering:** `PlanetSurface.SetCalibration` bakes two 2048-sample lookup tables into
  one-pixel-tall float textures, updated in place while dragging. The shader converts the true
  latitude/longitude to drawn values first, and everything else (projection, seams, fill) is
  unchanged.
- **Verified in the running app** (simulated mouse input through the real input path): on a
  Gall–Peters test map, unmoved guide lines sat exactly on the map's own drawn grid. Dragging 30°N
  down recorded it drawn at 19.2° and updated the globe live. Save and reopen restored it (format
  version 2). Esc restored everything. Robinson curves and Polar rings/spokes drew correctly.
  Benchmark: ~208 fps with or without calibration.
- **Owner feedback, 2026-09-30:** after trying it hands-on, dragging the lines "feels fine for
  now".

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

**BOD-01 — Suns, planets, and moons** · Implemented (M4) · Base
**Intent:** A system can have multiple suns and moons, plus planets.
**Implementation (Core, PR #16):**
- `Body` gains `Kind` (star, planet, moon), `RadiusKm`, `DayLengthHours`, `AxialTiltDegrees`,
  and an optional `Orbit`, plus `Problem()` validation. Every body keeps its own `Surface` (map,
  pieces, calibration).
- `Simulation/SystemHierarchy.cs`: any body can orbit any other (including a sun around a sun).
  Chains of parents must end at a body without an orbit; missing parents and loops are refused.
  `ChildrenOf` is ready for the system tree (`UI-02`).
- World file **format version 5**. Older worlds' single planet gets Earth's size, a 24-hour day,
  and no tilt.
**Implementation (app, PR #17):**
- **New worlds** start as a Sun-like star with one Earth-like planet circling it once a year
  (`World.CreateNew`). Older worlds keep their single planet, lit as before.
- **Selected body** (`WorldSession.SelectedBodyId`, `SelectBodyAsync`): the map tools (import,
  map type, calibrate, pieces) work on it. They're disabled for stars, which have no map. The
  first planet or moon is selected when a world opens.
- **Maps per body:** only the selected body's map is loaded at full size. The others show a
  1024-pixel preview (`MapImageLoader.LoadPreviewAsync`, loaded in the background after a world
  opens), so a system of mapped planets fits the baseline laptop's graphics memory. Selecting a
  body swaps the two.
- **Undo across bodies:** each step remembers its body (`SurfaceSnapshot`). Undoing selects that
  body, so the change is visible. "Matches the saved file" compares every body.
**Implementation (editing, PR #18):** bodies are added, deleted, and edited in the System panel
(`UI-02`). `WorldSession` has `AddBodyAsync`, `RemoveBodyAsync`, `RenameBody`, `SetBodyKind`,
`SetBodyPhysical`, `SetOrbit`, and `PossibleParents`.
- **Undo snapshots** now copy every body (`EditSnapshot`), so adding, deleting, and editing
  bodies are undoable like everything else. Undo selects the body that was being edited.
  `Body.HasSameContent` makes "matches the saved file" cover body properties and orbits too.
- **Maps** are handled in one place (`ShowMapsAsync`): it makes every globe show the right image
  (full size for the selected body, previews for the others, loaded in the background). It runs
  after every change, so adds, deletes, undo, and selection all keep the maps right.
- `SystemView.Sync` updates the scene in place: it creates, removes, or rebuilds only the changed
  bodies, so loaded maps aren't reloaded.
- **Verified in the running app** (real panel buttons): added a moon, a planet, and a companion
  star; edited a radius; moved a moon to another planet; deleted a planet with what orbits it,
  then undid it; undid and redid the whole session (back to "saved" at the start); and save →
  reopen kept the system. Benchmark: same as `main` back to back (~141 fps).

**BOD-02 — Non-standard bodies** · Implemented (M19: flat worlds; M21: world tree) · Base
**Intent:** Support bodies that aren't spheres, such as flat worlds and world trees.
**Notes / open questions:** How these interact with physics, orbits, and light/shadow. Flat
worlds (M19): orbits are unchanged; light is physical (the disc tumbles); eclipses still treat
the body as a sphere of its radius. World trees (M21) hold realms on their branches.
**Implementation (flat worlds you can make, see, and edit, PR #46):**
- **Model:** `BodyShape` (`Sphere`, `FlatDisc`) and `Body.Shape`, for planets and moons only.
  Format **version 16** (optional `shape`, `"flat-disc"`).
- **Layout** (`Geometry/FlatDisc.cs`, Core): the top face holds every latitude and longitude,
  with the north pole at the center; distance from the center is the angle from the pole, so
  the disc's radius is π times the matching globe's and every distance from the center is true
  (azimuthal equidistant, like the Polar map). `Body.RadiusKm` stays the matching globe's
  radius, so everything measured in km on the map works unchanged.
- **Drawing:** `godot/Rendering/FlatDiscMeshes.cs` (the top face, and the rock rim and
  underside); `planet.gdshader`'s `flat_disc` maps each pixel back to the globe direction it
  stands for, so maps, terrain, patterns, pieces, and the grid all carry over. The disc is lit
  by its flat normal. It **tumbles like a coin**: its spin axis lies across the disc (toward
  longitude 90° east), leaning by the axial tilt as a globe's would.
- **Surface points:** `godot/Rendering/GlobeShape.cs` places anything on either shape
  (regions, pins, the brush, markers); `GlobePicker` finds the point under the mouse on the
  top face (seen only from above).
- **App:** a **Shape** field (Globe / Flat world) in the System panel, one undo step
  (`WorldSession.SetBodyShape`); its tooltip gives the disc's own size. The camera frames a
  flat world by its full width; the close-up local view stays off for flat worlds until the
  next PR.
- **Verified in the running app** with real clicks, maximized and at 1152 × 648: switching to
  a flat world and back (undo), regions in the north and far south in the right places, the
  point under the mouse and back on screen to the pixel, the tumbling face and lit rock over a
  day. Benchmark: no change.

**Implementation (the close-up view, PR #47):**
- `PlanetCamera` has a flat world's own close-up (it knows the focused body's `Shape`):
  zooming in below the local view's altitude while above the face hovers over a spot on the
  top face, looking straight down, with the disc's center (its north pole) up on screen. It
  starts above the spot the camera was looking at, as far away as it was, so what's in the
  middle stays there; the switch blends the camera from its old pose over 0.4 s.
- Dragging and the pan keys slide across the face (what's under the mouse follows it, apart
  from turning to keep the center up). It rides with the tumbling disc, as the globe's local
  view rides with its spin. Zooming out above 1.5 disc radii goes back to orbiting (well
  above where the close-up starts, so the two don't flip back and forth). From below the disc
  there's no close-up; you see the rock.
- **Zoom to** (regions, pins, weather pins) glides down to the spot on the face, at a height
  that fits it. A close-up view is saved as the orbiting view above it.
- The closest approach (10 km) and the near clipping distance measure from the disc itself
  (`SystemView.DistanceToDisc`), so the face isn't cut away close up.
- **Scale bar:** `Geometry/SurfaceDistance.cs` (Core) measures ground distance along a globe
  or straight across a flat world's face, its real ground (around the disc, distances are
  longer than on the globe the farther south they are).
- **Verified in the running app** with real scrolling and dragging, maximized and at
  1152 × 648: zooming in keeps the pole in the middle, dragging slides across the face, it
  stays over the same ground as the disc tumbles, zooming out orbits again, and Zoom to on a
  far-south region lands on it with the scale bar, north arrow, and coordinates right.
  Benchmark: no change.

**Implementation (the flat-world sky, PR #48):**
- **One sky for the whole face** (owner's choice: physically flat): the disc tumbles like a
  coin, so every place has sunrise at the same moment, the star is up for half of each day,
  and the noon sun stands at 90° minus the star's declination (its angle from the plane the
  disc spins in). The clock was already the same everywhere (`BodyClock` counts days).
- **Seasons** (`Seasons`, Core): the same moments as a globe's equinoxes and solstices, named
  `Midsummer` (noon sun overhead) and `Midwinter` (lowest); summer follows midsummer and
  winter midwinter, twice a year, with no hemispheres. With little tilt there are no seasons,
  as on a globe.
- **Weather** (`ClimateYear`): every place gets the same sunlight (a day's average of
  cos(declination) ÷ π), so each place's year averages the body's temperature (plus its
  terrain), with seasons following the noon height. Rain has no belts: about 2.7 mm a day
  (Earth's average), scaled by moisture and the cold. `TerrainSurroundings` looks for water
  straight across the face (VISION.md `WTH-03`).
- **App:** the time bar shows "Winter · Midsummer in 91 days" (its tooltip explains the one
  season); the System panel's season list and the orbit markers say Midsummer (dots) and
  Midwinter (diamonds); the weather window explains a flat world's weather.
- Tested: two summers and two winters a year at the globe's equinox and solstice times; one
  sky everywhere (daylight, noon height, temperature, rain); the noon sun from 90° to 90°
  minus the tilt; a year averaging the body's temperature with two warm spells; water
  measured across the face. Verified in the running app at both sizes: the time bar, season
  list, and a weather pin's window on a flat world.

**Implementation (the world tree itself, M21, PR #53):**
- **Model:** `BodyKind.WorldTree`; `Model/WorldTreeLook.cs` (great branches 3–16, spread,
  shape seed, bark, leaf, and glow colors, glow strength 0–4) as `Body.Tree`, required for trees
  and refused for anything else. A tree's radius is half its height and its day is how long it
  takes to turn; it has no surface or calendar. Format **version 20** (kind `"world-tree"`,
  `tree`). `NewBodies.WorldTree`: 300,000 km tall, turning once a year, nine branches, glowing
  gold, circling the selected body's star.
- **Shape** (`Simulation/WorldTreeShape.cs`, Core): grows a trunk, great branches (spread round
  the trunk by the golden angle, rising as they reach out, each bending once and ending in a
  **tip that can hold a realm**), two twigs per branch, and three to five roots, all as tapered
  straight pieces within the tree's radius. Deterministic: chance comes from
  `Simulation/SeededRandom.cs`, the repeatable recipe now shared with asteroid events (which
  keep exactly the dates they had).
- **Drawing** (`godot/Rendering/WorldTreeVisual.cs`): bark tubes and foliage clumps at branch
  and twig ends that glow faintly in the glow color, regrown only when the shape changes;
  turning with the tree (its spin and tilt). A light like a star's, in the glow color and
  strength, lights the system around it.
- **App:** **Add World Tree** in the System panel and **Add ▸ World Tree**; a **World Tree**
  section for its look (`godot/UI/WorldTreeSection.cs`, `WorldSession.SetTreeLook`, one undo step
  per drag). Its kind shows as "World tree", the planet look fields hide, and the Radius and Day
  length tooltips explain what they mean for a tree.
- **Verified in the running app** with real clicks, maximized: Add World Tree, the panel and its
  fields, the tree from the side and above and in the system, a changed look (more branches,
  blue glow), and undo. Benchmark: no change.

**Implementation (realms on its branches, M21, PR #54):**
- **Model:** `Body.Branch` (which great branch a planet or moon hangs on); `Orbit.HeightKm`, an
  optional lift of the whole orbit along its own axis. Format **version 21** (`branch`, orbit
  `height`).
- **Locked orbits** (`Simulation/Realms.cs`, Core): a hung realm's orbit is worked out from its
  branch tip: round the trunk at the tip's distance, lifted to its height, tilted with the tree
  (tilt direction 90° past its lean, the same rotation it's drawn with), starting where the tip
  is and taking one of the tree's turns to go round. So the realm rides exactly on the drawn tip
  (tested against the drawing's spin-then-tilt rotations, tilted trees included), and positions,
  seasons, eclipses, and events work for it unchanged. `Apply` re-places realms after every
  change to the system (before calendar fitting), and on loading. One realm to a branch; only
  planets and moons; a hung realm can't be made the center, and Make Center keeps lifted orbits
  right.
- **Drawing:** a realm is drawn at its branch tip scaled with its tree (whose drawn size the
  readable view squeezes), nudged out by its own radius; its orbit line is left out.
- **App:** a **Hangs on** choice in the System panel's orbit section for planets and moons when
  a world tree exists (free branches only); while hung, the orbit fields are locked and say
  why. `WorldSession.HangOnBranch` (null lets it go: it keeps circling the tree, freely).
  Changing the tree moves its realms; a change that would remove a branch holding a realm is
  refused ("a realm hangs on branch 9: move it first").
- **Verified in the running app** with real clicks, maximized and at 1152 × 648: hanging the
  planet on branch 1, the locked fields, the realm on the tip and carried round a quarter turn
  later, a reshaped tree moving it, the refused branch removal, letting it go, and undo.
  Benchmark: no change.

**Implementation (its glow as the realms' sun, M21, PR #55):**
- **Model:** `Body.GivesLight`: a star, or a world tree whose glow is above zero. Everything that
  looked for "the star" (seasons, years, weather, eclipses, meteor showers, asteroid events)
  now looks for a body that gives light, so a glowing tree is its realms' sun and a dark one
  leaves them to the star the tree circles. No format change.
- **Where the light comes from** (`Seasons.SunPath`, Core): a tree glows all over, so a realm is
  lit by the trunk level with it, not the tree's middle (which, for a high branch, would put
  the sun far below and give odd seasons). The realm circles that point once a turn, as a
  planet circles a star, so its day, seasons (four a turn with axial tilt), weather, and
  eclipses work out the same way. Moons of a realm share its sun.
- **Calendars:** a realm's year is held to one turn, so fitting the year to a realm's calendar
  is refused with a reason (change the tree's turn, or fit the day instead); fitting the day
  still works. A tree's own time reads "Turn 3, day 41" (`BodyClock.Describe`).
- **App:** a realm's orbit line is drawn again (its path round the trunk), with its season
  markers on it (`SystemLayout.OrbitPoint` follows the drawn branch). The seasons note mentions
  a glowing tree. The tree's light in the 3D view still shines from its middle, so a high
  realm looks lit a little from below; the simulation uses the trunk beside it.
- **Verified in the running app,** maximized and at 1152 × 648: a planet hung on a glowing tree
  shows its season and next equinox in the time bar, four season markers round the trunk, and
  the season list in the calendar section; the tree selected reads "Turn 1, day 1". Weather,
  the refused year fit, moons, and a dark tree are covered by tests. Benchmark: no change.

**BOD-03 — Other astral features** · Implemented (M20: rings, belts, nebulas) · Base
**Intent:** Asteroids, nebulas, and similar features.
**Implementation (planetary rings, PR #49):**
- **Model:** `Model/PlanetRings.cs` (`InnerRadii`, `OuterRadii`, `Color`), `Body.Rings` for
  planets and moons, measured in the body's radii so resizing keeps their shape. Format
  **version 17** (optional `rings`).
- **Drawing:** `godot/Rendering/RingsVisual.cs` puts a shared flat ring (radius 1 to 2) in the
  planet's equatorial plane, as a child of the planet's node; `rings.gdshader` stretches it to
  the rings' radii and draws the bands, with a gap a little past halfway out like Saturn's
  Cassini Division. The bands come from `ring_bands.gdshaderinc`, shared with
  `planet.gdshader`, so the rings and their shadow always agree.
- **Shadows:** the rings are darkened where the line toward the star passes through the planet;
  the planet is darkened where that line crosses the rings, by how dense they are there (the
  winter hemisphere gets the shadow, as on Saturn). Lit from the selected body's star each
  frame. A flat world's rings circle its spin axis, measured in its disc's radius so they
  clear the rim; its face isn't shadowed by them (as for eclipses).
- **App:** a **Rings** section in the System panel (`godot/UI/RingsSection.cs`): an on/off box,
  **From** and **To** (× radius), and **Color**; `WorldSession.SetRings`, one undo step per
  drag. Saturn's main rings (1.25 to 2.3 radii, pale tan) to start.
- **Verified in the running app** with real clicks, maximized and at 1152 × 648: switching
  rings on, the bands and gap, the planet's shadow across them, their shadow on the winter
  hemisphere, edge-on at an equinox, a flat world's rings, and undo. Benchmark: no change.

**Implementation (asteroid belts, PR #50):**
- **Model:** `Model/AsteroidBelt.cs` (name, inner and outer distance in km, thickness: how far
  the rocks' orbits tilt, density 1–100%, color); `Body.Belts`, stars only, several allowed,
  deleted with their star and covered by undo like the rest of the body. Format **version 18**
  (optional `belts`). `NewBodies.Belt` makes one like our main belt (2.2 to 3.3 AU, 10°, 50%),
  or just beyond the outermost; `NewBodies.NaturalPeriodDays` (period² ∝ distance³) is now
  public for things that aren't designed one by one.
- **Drawing:** `godot/Rendering/BeltVisual.cs` makes a belt's rocks once from its own seed
  (up to 5,000 at full density, spread evenly over its area, most tilted a little and a few a
  lot, lumpy and of varied size) and draws them in one batch (a MultiMesh) centered on the
  star. `belt_rocks.gdshader` moves every rock on the GPU at its natural speed (inner rocks
  overtake outer ones) and applies the view's distance compression, so nothing is updated
  rock by rock. Rocks are drawn far bigger than real ones (1.2% of the belt's drawn width), so
  the belt reads as a band from afar; up close none may look bigger than about 4 pixels (0.004
  radians from the camera, owner's request in PR #52), so a planet inside a belt isn't swarmed
  by moon-sized rocks.
- **App:** stars get an **Asteroid Belts** section in the System panel
  (`godot/UI/BeltsSection.cs`): each belt's name, **From** and **To** (AU), **Thickness**,
  **Density**, **Color**, and **Delete**, plus **Add Belt**; `WorldSession.AddBelt`,
  `SetBelt` (one undo step per drag), `RemoveBelt`.
- **Verified in the running app** with real clicks, maximized and at 1152 × 648: Add Belt on
  the Sun, its fields, the belt from above and edge-on, the rocks drifting as time runs,
  and Delete and undo. Benchmark with a 2,500-rock belt: no slowdown; video memory +3 MB.

**Implementation (nebulas, PR #51):**
- **Model:** `Model/Nebula.cs` (name; where it is on the sky, as a latitude and longitude around
  the system; size 2°–120° of radius; brightness; a main color and a wisp color);
  `World.Nebulas`, up to 20. Format **version 19** (optional `nebulas`). Undo covers them with
  the world's other non-body state (`LoreState`). `NewBodies.Nebula` turns each new one away
  from the others by the golden angle.
- **Painting** (`Simulation/NebulaSky.cs`, Core): an image of every direction (around ×
  up/down), each nebula a soft glow whose edge and wisps are shaped by noise seeded from its
  ID, blending between its colors; nebulas add their light together. Deterministic and tested
  without Godot. Rows are painted in parallel.
- **Drawing** (`godot/Rendering/NebulaBackdrop.cs`, `nebula_sky.gdshader`): when the nebulas
  change, the sky is painted in the background at 1024 × 512 (soft clouds don't need more;
  about a second for two large ones), then shown as the environment's sky with one lookup per
  background pixel, converted to linear light so the background stays as dark as before. A
  change made while painting is painted next. Without nebulas the background stays the plain
  color it always was. Lighting and reflections don't come from the sky, so the planets look
  the same.
- **App:** a **Nebulas** section at the bottom of the System panel
  (`godot/UI/NebulasSection.cs`): each nebula's name, **Up/down**, **Around**, **Size**,
  **Brightness**, **Color**, **Wisps**, and **Delete**, plus **Add Nebula**; also **Add ▸
  Nebula**. `WorldSession.AddNebula`, `SetNebula` (one undo step per drag), `RemoveNebula`.
- **Verified in the running app** with real clicks: Add Nebula, its fields, the nebula behind
  the planet when looking toward its direction and absent looking away, a bigger and brighter
  one, the background unchanged without nebulas, and undo. Benchmark: no change.
- **Fixed (PR #68):** `World.Clone` left nebulas out, and saving (and the recovery copy) works
  on a clone, so nebulas were lost when a world was saved. Tested by `Clone_CopiesNebulas`.

**BOD-04 — Body sculpting (digital clay)** · Implemented (M24: heights, PRs #60–#63; M25: shapes, PRs #65–#67) · Base
**Intent:** Mold bodies like digital clay with brush tools. Tools include:
- Raise and lower terrain brushes
- Basic shape tools to add and subtract terrain, which are also useful for artificial structures
- Extreme shapes: hollow planets, planets with holes through them
**Notes:** Holes and hollows rule out simple heightmap-on-a-sphere approaches and have major
architecture implications. This needs a design review before any related data format is fixed.
**Design (owner's choice, 2026-10-04):** heights on the cube-sphere grid plus a list of shape
edits (added or cut spheres, boxes, cylinders) for holes, hollows, and structures; the surface is
built from both. Heights come first (M24), shapes next.
**Implementation (heights in Core, M24, PR #60):**
- **`Model/HeightGrid.cs`:** one height per cell of the terrain grid (1,024² a face, about 10 km
  on an Earth-sized world), in whole meters, ±32,767. Immutable, in 64 × 64 tiles shared with
  the grid it came from (flat tiles take no memory), like `TerrainGrid`.
- **Brushes,** each along a stroke with a soft edge (a cosine bump: full strength at the middle,
  nothing at the radius); along a stroke the strongest stamp counts, so a stroke has the same
  effect however its stamps fall, and strokes add up. **Raise** (negative lowers) by up to so
  many meters; **Flatten** toward a target height (e.g. where the stroke began) by an amount;
  **Smooth** toward the average of the cell and 8 points round it an eighth of the radius away
  (at least the next cell; across face edges too), from the heights before the stroke. Heights
  are rounded to whole meters and clamped. Deterministic (tested). In a release build a brush a
  few degrees across takes a few milliseconds; a hemisphere about 0.1 s to raise and 0.5 s to
  smooth.
- **Shared with painted terrain:** `CubeGridBrush` (the grid's tiles, cell positions, and
  stroke stamps, moved out of `TerrainGrid`) and `Storage/GreyscalePng` (the PNG reader and
  writer, moved out of `TerrainImage` and extended to 16-bit).
- **Saving:** format **version 23**: `surface.heights` names a 16-bit greyscale PNG
  (`heights/<body id>.png`, laid out like the terrain image, height + 32,768). Planets and moons
  only. Tested: golden file, round trip, every PNG row filter from an independent writer, and
  refusing an 8-bit image, an out-of-range height, and heights on a star.
**Implementation (drawing the relief, M24, PR #61):**
- **Even strokes** (Core): brushes now ease out from the stroke's path itself (`StrokePath`:
  the distance to the nearest point of the shortest way between its ends, tested), not from
  round stamps along it, whose 4% dips between stamps showed as ripples once exaggerated.
  `HeightGrid.SampleAt` blends between the four nearest cells' middles (tested smooth, as the
  shader draws it) and `Highest` gives the tallest cell.
- **Mesh** (`Rendering/CubeSphereMesh.cs`): a sculpted globe is drawn as a cube-sphere, its
  vertices on the height grid's spacing and welded along the faces' edges so the surface can't
  crack. Unsculpted globes keep the plain sphere. How many squares a face is a quality setting
  (owner's choice, PR #62), **View ▸ Relief Detail**: Low 64 (about 158 fps with a sculpted planet
  filling the screen), Standard 96 (the default, about 148), High 128 (about 140); see `REN-03`.
- **Shader** (`planet.gdshader`): the heights are a six-layer texture of half-precision floats
  in meters with smaller copies (about 16 MB; drawn heights round by at most 16 m at 32 km,
  saved ones are exact), blended by the graphics card. The vertex stage lifts each vertex by
  its height × `relief_scale` (the exaggeration over the radius in meters); each pixel is
  shaded by the slope from the heights either side of it, a cell apart (or a pixel, with a
  smaller copy, once cells are smaller than pixels, so distant ground doesn't shimmer).
  `PlanetSurface.SetHeights` sends only the faces a stroke changed, and grows the mesh's bounds
  to the highest peak so it isn't culled.
- **View ▸ Relief:** True Scale (1×), 5×, 10× (the default), 20×, or 50× (`SystemView.
  ReliefExaggeration`; not saved).
- **On the ground:** pins, weather pins, markers, the brush circle, and clicks follow the
  raised surface (`PlanetSurface.SurfaceRadiusAt`; `GlobePicker` finds where a ray meets it by
  a few rounds of meeting a sphere of the height found); region outlines and fills are lifted
  onto it and rebuilt when the relief changes; the camera's closest approach includes the
  highest peak, so it never ends up inside a hill.
- **Verified in the running app** (heights set directly, as the tools come next), maximized and
  at 1152 × 648: a mountain range and a basin, from orbit at 1×, 10×, and 50× (the range shows on
  the planet's edge), where the light grazes it (smooth, no ripples), a region and a weather
  pin sitting on it, and the local view over a slope. No shader errors.
- **Benchmark** (fullscreen, against `main`): unsculpted worlds unchanged (198/202 vs 200/200
  fps). A sculpted planet filling the screen: about 148 fps (from 200; 1.7 ms a frame) and 21 MB
  more video memory. The first version took it to 130 fps; reading heights through the card's
  own blending and using 96 rather than 128 squares a face won back the rest.
**Implementation (the Sculpt tools, M24, PR #63):**
- **Panel** (`UI/TerrainPanel.cs`): **Sculpt** beside Paint and Erase, with **Raise**,
  **Lower**, **Smooth**, and **Flatten**. Raise and Lower take a **Height** (10 m to 10 km a
  stroke, 500 m to start); Smooth and Flatten an **Amount** (5% to 100%, 50% to start). The
  brush size is shared with painting. Flat worlds say they can't be sculpted yet.
- **Strokes** (`Controls/TerrainBrush.cs`, `WorldSession.SculptHeights`): a stroke keeps the
  heights it began with and its spots so far, and every update redoes the whole stroke from
  those (Core's brushes now take a path of points, tested: no bumps where the bits join), so it's
  one even shape and one undo step ("Raise Ground", ...). A spot is added once the mouse has
  moved a quarter of the brush's radius (and at the end), so big brushes, slowest to redo, are
  redone least often. Flatten levels to the height under the stroke's first spot.
- **Verified in the running app** with real clicks and drags, maximized and at 1152 × 648:
  Sculpt, a Raise stroke (3,000 m), Lower, Smooth (the peak wears to 2,976 m), Flatten, undoing
  each stroke as one step back to flat, and the flat-world note. In full sunlight relief shows
  only faintly (it's shaded by real light, and the ambient light softens it), strongest near
  sunrise and sunset or exaggerated further.
**Implementation (map-style shading, PR #64; owner's choice, a View option):**
- **View ▸ Relief Shading:** **Sunlight** (true lighting, the default) or **Map-style**: relief
  lit from the northwest, 45° up, as on printed maps, so it shows at any time of day; the star
  still makes day and night. The shader multiplies the surface color by how much each slope
  faces that light, with the slopes steepened 5× for shading and the result softened (between
  40% and 150% brightness) so steep sides darken without going black. Remembered on this
  computer (`AppSettings`). Verified in the running app at midday: ridges and a basin that
  sunlight barely shows stand out. Benchmark: no change.
**Implementation (shapes in Core, M25, PR #65):**
- **`Model/ShapeEdit.cs`:** a shape's `Kind` (`ShapeKind`: sphere, box, cylinder, cone),
  `Operation` (`ShapeOperation`: add, cut), `Spot` on the surface, `DepthKm` (its middle above or
  below the radius there), `WidthKm`, `HeightKm`, `LengthKm`, and `TurnDegrees`. A sphere uses
  the width as its diameter; a cylinder and a cone (point up) the width and height; a box all
  three; unused sizes are kept, so changing the kind keeps them. `Problem` checks sizes (above
  0, at most 4 radii) and depth (within 4 radii, enough to reach through the world).
- **`FrameOn(radiusKm)`** (`ShapeFrame`): where a shape sits in the body's own space, in radii:
  its middle and its across / up / along axes (up from the spot; along north turned clockwise
  by the turn). Tested: at the equator, turned, at a pole, a cylinder right through the world,
  and a sphere hollowing the center.
- **Model:** `SurfaceSettings.Shapes` (in order; copied with the surface, compared for "unsaved
  changes"); `Body.Problem` allows them on planets and moons only, up to 64, each with its own
  ID. Saving: format **version 24**, `surface.shapes` (world-format.md **Shapes**). Tested:
  golden file, older files have none, and refusing an unknown kind or operation, a size of 0, a
  middle too far away, and a latitude out of range.
**Implementation (drawing worlds with shapes, M25, PR #66):**
- **`Rendering/ShapedGlobe.cs`** (under the body's `PlanetSurface`, which hides its own mesh while
  there are shapes): a hidden `CsgCombiner3D` holds the sculpted globe as a `CsgMesh3D` (the
  cube-sphere lifted on the CPU by `HeightGrid.SampleAt` × the relief scale, wearing the planet
  material) and each shape in order as a `CsgSphere3D`, `CsgBox3D`, or `CsgCylinder3D` (a cone
  is a cylinder with `Cone`), added (union) or cut (subtraction), in bare-rock material, placed
  by `ShapeEdit.FrameOn`. A shape's middle rises with the drawn ground under its spot (the
  relief is exaggerated, the shapes aren't), so one sitting on a hill still sits on it. Once
  Godot has carved (the next frame), the result is copied into a plain mesh and drawn, so the
  last carving stays on show meanwhile. Rebuilt only when the shapes, heights, radius, or
  relief exaggeration change.
- **Measured** (baseline laptop): carving the whole globe takes 0.87 / 2.0 / 3.7 s at 64 / 96 /
  128 squares a face (1.5 s to re-carve at 96), so the carved globe uses 32 (owner's choice):
  about 0.25 s to re-carve. Slopes are still shaded per height cell (the shader reads the
  heights; `lifted_on_cpu` tells it the mesh is already lifted and to shade the round globe).
- **Found in testing:** east, up, and north make a mirror-image frame, which turned shapes
  inside out for the CSG (cuts added volume). The width axis is flipped (every shape is the
  same either side of it); checked by the carved volume (a hole through the world removes
  exactly its cylinder).
- **Verified in the running app,** maximized and at 1152 × 648: a hole right through a hollow
  world (its rock walls, and the hollow seen through it), a crater bowl, a fortress block, and
  a cone, on a sculpted planet. The camera keeps above the tallest added shape.
- **Benchmark:** unshaped worlds unchanged against `main` (197/201 vs 201/200 fps); a shaped,
  sculpted planet filling the screen: 168 fps, 18 MB more video memory.
**Implementation (shape tools, M25, PR #67):**
- **Terrain panel ▸ Shapes** (a fourth mode, `UI/TerrainPanel.cs`): `UI/ShapesSection.cs` picks
  the kind and Add/Cut for new shapes, lists the body's shapes, and edits the selected one's
  kind, operation, depth, width, height (not for a sphere), length (a box only), and turn, with
  **Delete Shape**. The brush is off in this mode; flat worlds get the same note as Sculpt.
- **`Controls/ShapeHandles.cs`** (a `CanvasLayer` after the camera, like the brush): a click on
  the globe places a shape and selects it; the selected one has three handles: its middle
  (move; it keeps its height above the ground), a square at the edge of its width (resize; it
  keeps its proportions and how it sits on the ground), and a knob past the end of its length
  (turn, clockwise from north; Shift snaps to 15°). Esc deselects. While a handle is dragged,
  `PlanetSurface.SetShapePreview` shows the shape see-through (`ShapedGlobe.ShowPreview`, using
  the same placement as the carving); letting go makes one undo step and carves. Drags off
  the handles still turn the camera. On a shape that's small on screen the resize and turn
  handles are kept 28 px from the middle so each can be grabbed, so resizing scales by how far
  the mouse moves out from the middle compared with where the handle was grabbed.
- **`Session/WorldSession.Shapes.cs`:** `AddShape` (a new shape is 5% of the radius across; a
  sphere as tall as it's wide, the others 30%; an added box, cylinder, or cone sits on the
  ground there, everything else is centered on it), `UpdateShape` (typing in a field merges
  into one undo step; each handle drag is its own), and `DeleteShape`.
- **Fixed on the way:** live number fields (`UI/NumberFields.WithLiveTyping`) turned a lone "-"
  into 0, so negative numbers (a depth below ground, a latitude south) couldn't be typed. They
  now apply only text that is already a whole number.
- **Verified in the running app** with real clicks and key presses, maximized and at 1152 × 648:
  placing a cut cylinder and an added box, moving, resizing, and turning them by their handles
  (the preview while dragging, carved on release), typing a negative depth, Delete, and undo.
  Benchmark: not run; nothing is drawn differently outside Shapes mode, and the handles redraw
  only while it's on.
**Fixed (M28, PR #74):**
- **Clicks reach into holes:** on a carved globe, `GlobePicker.PointAt` finds where the click
  meets the carving itself (`ShapedGlobe.RayHit`, Godot's `TriangleMesh` of the drawn mesh),
  so pins, the brush, and the shape tools pick the spot actually under the mouse. A shape
  clicked into a hole or hollow goes at that depth (`PlanetSurface.TrueHeightKmOf` takes the
  relief's exaggeration back out; `WorldSession.AddShape`'s `groundKm`): an added box sits on a
  pit's floor. Clicks within 0.1% of the radius of the sculpted ground count as the ground,
  since the coarse carving's flat facets sit a little inside it.
- **The date line:** longitude jumps from 180 to −180 there, which the GPU took for a huge step
  per pixel: the grid drew a dotted line along it, and live weather's wind streaks and snow
  dots (laid out by longitude × cos latitude, which also sheared them far from longitude 0)
  came out wide and slanted. The grid now measures longitude's step both ways round and takes
  the smaller (`longitude_width`); the weather lays its streaks and dots out in square cells
  along each row of latitude (`globe_cell`), sized by the globe's own angle per pixel.
- **Verified in the running app:** the night side across the date line (no dotted line, even
  streaks), and a box placed by a real click onto the floor of a 600 km-deep pit (its bottom
  at −600 km). Benchmark: unchanged against `main` (177/177 vs 177/179 fps).

**BOD-05 — Terrain/biome painting** · Implemented (M11: PR #33, #34, #35) · Base
**Intent:** Paint terrain types onto bodies, such as ocean, mountains, swamps, forests, and fields.
**Implementation (Core, PR #33):**
- `Geometry/CubeSphere.cs`: divides a sphere into six square faces of cells (an "equal-angle"
  cube, so cells stay within about 1.5× of the same size, with no crowding at the poles).
  `CellAt(direction)` and `CellCenter(cell)`. The shader in the next PR must repeat its math
  (spelled out in world-format.md, **Terrain**).
- `Model/TerrainGrid.cs`: one terrain code per cell, 1,024 × 1,024 per face (0 = unpainted).
  **Immutable**, stored in 64 × 64 tiles: painting returns a new grid that shares untouched
  tiles, so undo snapshots and world copies stay cheap; unpainted tiles take no memory.
  `Paint` (a round stamp, radius in degrees of arc, 0.01° to 90°), `PaintStroke` (stamps a
  quarter radius apart along the shortest path), `Replace` (e.g. clear a deleted type),
  `CodeAt`, `FacesChangedFrom` (which faces to re-upload to the graphics card), `CopyFace` /
  `FromCells`. Tiles wholly inside a stamp are filled at once. In a release build, a normal
  brush stamp takes well under a millisecond; the largest (a hemisphere) about 0.1 s.
- `Model/TerrainType.cs`: `(Code, Name, Color)`, the 12 `Defaults`, `Problem` (codes 1–255
  and unique, names 1–60 characters), and `FreeCode`. `World.TerrainTypes` holds the list
  (`World.CreateNew` adds the defaults); `SurfaceSettings.Terrain` holds each body's grid and
  counts in "matches the saved file".
- **Saving:** format **version 10**. Each painted body's grid is a standard 8-bit greyscale PNG
  (`terrain/<body id>.png`, faces stacked) written and read by `Storage/TerrainImage.cs`, a
  small self-contained PNG codec (Core has no image library). It reads every PNG row filter, so
  an image re-saved by a paint program still loads. Older worlds get the 12 defaults (a frozen
  copy in the migration, so changing the defaults later can't change old upgrades).
- **Tests:** `CubeSphereTests`, `TerrainGridTests`, `TerrainTypeTests`, and golden-file and
  damaged-file tests for version 10 in `WorldPackageTests` (with `TestPng`, an independent PNG
  writer, to check images the app didn't write).
**Implementation (painting, PR #34):**
- **Drawing** (`godot/Rendering/planet.gdshader`, `PlanetSurface.cs`): each painted body's grid
  is a six-layer one-byte texture (`Texture2DArray`, about 6 MB, only while something is
  painted) plus a 256-color palette. The shader repeats `CubeSphere`'s math with each face's
  axes written out (looking up arrays per pixel was measurably slower). Without a map (or
  pieces), terrain is the surface; over one it's mixed in at 60% so the map shows through.
  `SetTerrain` re-uploads only the faces a stroke changed (`FacesChangedFrom`).
- **View ▸ Terrain** (`SystemView.ShowTerrain`, `ViewMenu.cs`): one switch for every globe,
  including ones added later.
- **Session** (`godot/Session/WorldSession.Terrain.cs`): `AddTerrainType`,
  `UpdateTerrainType` (rapid edits merge into one undo step), `DeleteTerrainType` (clears its
  cells on every body; one step), `PaintTerrain` (a stroke segment on the selected body), and
  `TerrainAt`. The terrain types joined `LoreState`, so they're in undo and "unsaved changes".
- **Brush** (`godot/Controls/TerrainBrush.cs`, last in the scene so it gets clicks first):
  while the Terrain panel shows, left-drag on the selected globe paints from the last spot to
  the mouse (one undo step per stroke, through `BeginGesture`); a drag that starts off the
  globe turns the camera as usual. A circle on the globe shows the brush.
- **Panel** (`godot/UI/TerrainPanel.cs`): Paint/Erase, the brush radius (a slider spanning
  0.05° to 45°, and the same in km for the selected body), the type list with color swatches,
  New Type, Delete, and the selected type's name and color. The **Terrain** button joined the
  right-side panel group in `MapToolbar`; region labels ignore clicks while it's open.
- **Verified in the running app** with real clicks and drags, maximized and at 1152 × 648:
  painting, choosing a type from the list, Erase, undo, a drag off the globe (turns the view,
  paints nothing), the overlay on an imported map, View ▸ Terrain, New Type / rename / Delete,
  save and reopen (painted, nothing unsaved), a star (note shown, brush off), and Journal
  closing the panel. A check on all six faces compared the drawn colors with Core's cells at
  about 5,000 points per view: 99% agree, the rest on circle edges.
- **Benchmark** (fullscreen, back to back with `main`): the default world is unchanged (about
  190 fps both); a world with a painted planet on screen drops from about 189 to 177 fps (6%,
  0.35 ms a frame), and uses 6 MB more video memory.
**Implementation (polish, PR #35):**
- **Smooth edges up close** (`terrain_near` in `planet.gdshader`): each pixel looks at the four
  cells around it. Each terrain's share of them (by distance) decides, and the edge runs where
  the two biggest shares are equal, so coastlines come out as rounded curves (slightly wavy at
  the scale of a cell) instead of square steps. They're anti-aliased to about a pixel, never
  softer than 0.15 of a cell. Pixels among four cells of one terrain take a shortcut. Along a
  cube-face edge, neighbors on the next face are found by direction, so there are no seams.
- **No flicker far away**: `PlanetSurface` keeps a second copy of each face, averaged over
  4 × 4 cells into colors (256 × 256, with mipmaps, about 2 MB per painted body), rebuilt for
  the faces a stroke touches and whenever the colors change. As cells shrink below about a pixel
  the shader blends over to it.
- **Unpainted grey** (`unpainted_color`): used instead of the default blue when a planet has
  terrain and no map or pieces.
- **Colors**: the palette is now read as sRGB, like map images, so painted colors match the
  swatches in the Terrain panel (before, they came out paler).
- **Checks** on the baseline laptop (Radeon Vega 10), back to back with `main`: the default world
  is unchanged; a painted planet at the usual distance 177 → 170 fps; filling the screen close
  up, about 5% slower (e.g. 160 → 152 fps); 3 MB more video memory. The six-face check still
  agrees at 99%.

**BOD-06 — Custom surface appearance** · Implemented (M16, PR #42) · Base
**Intent:** Custom textures/appearance for bodies beyond defaults (see also `MAP-01`).
**Implementation (PR #42):**
- `Model/BodyAppearance.cs` on `Body.Appearance` (immutable): `Color` and `Pattern`
  (`SurfacePattern`) for planets and moons, `StarType` for stars; `DefaultFor(kind)` and
  `StarColor(type)`. New moons (`NewBodies.Moon`) start grey and rocky; switching a body between
  planet and moon keeps its look. Format **version 13** (`appearance`; older worlds get the
  defaults, so only moons change).
- **Drawing** (`planet.gdshader`, `surface_look`): Rocky (mottled, with craters at two sizes),
  Banded (bands along the body's own latitudes, wobbling), Icy (pale with dark cracks), Cloudy
  (swirls toward white). The noise is read from a small repeating 3D texture
  (`godot/Rendering/PatternNoise.cs`, 32³, built once from a fixed seed) rather than worked out
  per pixel: the first version cost about 20% with a patterned planet filling the screen; this
  one about 3%. Each body's pattern is offset by its ID, so two rocky moons differ. Craters
  each fit inside one cell, so only one cell is checked.
- **Stars:** `SystemView` colors each star and its light from its type, updating only when it
  changes; `BodyMarkers` uses the same color for star dots.
- **App:** the System panel's **Color** and **Pattern** (planets and moons) or **Star type**
  (stars), through `WorldSession.SetAppearance` (rapid changes are one undo step).
- **Verified in the running app** with real clicks, maximized and at 1152 × 648: every pattern
  through the dropdown, a color change and its undo, a new moon grey and rocky, the Sun as a red
  dwarf (its color and light), and save/reopen. Benchmark (baseline laptop): the default world
  unchanged against `main` (~194 fps); the same world plain ~179 fps against patterned ~174.

**BOD-07 — Terrain shapes the ground** · Implemented (M35: PR #91) · Base
**Intent:** Terrain painting and sculpting work together: painting a type (a mountain range,
an ocean) raises or lowers the ground to that type's default height, custom types set their
own, and where different types meet the heights merge, anything from a smooth slope to a
sheer cliff, so the world looks real without sculpting every slope by hand (owner's request,
2026-10-06).
**Implementation (M35, PR #91):**
- **Model:**
  - Each `TerrainType` gains `HeightMeters` (−12,000 to 12,000) and `Edge` (0 gentle to 1
    cliff). The defaults: Ocean −3,000 m, Shallow Water −150, Plains and Fields 150, Forest
    300, Jungle 200, Hills 800, Mountains 2,500 (steep), Desert 400, Swamp 20, Tundra 300, Ice
    1,000.
  - `World.TerrainShapesGround` (off by default, for new worlds too).
  - Format **version 29**. Older worlds keep it off, and their types named like the defaults
    get the default heights.
- **`TerrainRelief`** (Core) works out the ground the painting shapes, a 64-cell tile at a
  time, reading the codes 13 cells around it (across a face's edge, from the next face):
  - A two-sweep chamfer distance transform finds each cell's distance to the nearest other
    type, through cells of its own type, and which type that is.
  - Each cell eases from halfway between the two heights (at the line between them) to its
    own height over half the transition width, `24 × (1 − e)²` cells for the steeper edge
    `e`. A cliff (`e` = 1) jumps.
  - Tiles with nothing painted near them are skipped.
  - A paint stroke re-works only the tiles near it (`Update`): about 10–20 ms. A whole
    planet takes 2–700 ms, depending on how much is painted.
  - `Shaped` adds the sculpting on top, sharing tiles where one side is flat. Only the
    sculpting is saved.
- **Sculpting on shaped ground:** Raise and Lower are unchanged. `HeightGrid.Flatten` and
  `Smooth` take the ground underneath, so they level and smooth what's seen. `HeightAt` (the
  Flatten target and shapes' depth) is the ground seen.
- **App:**
  - `WorldSession` keeps each body's shaped ground, keyed by its painting and the types'
    codes, heights, and edges (a new name or color re-works nothing).
  - `ShowTerrain` sends the globe the shaped ground with the sculpting on top. First person,
    shapes, and carving use it.
  - The switch is part of undo (`LoreState`).
  - **Terrain panel:** a **Terrain shapes the ground** switch over the types, and each type's
    **Height** (in m or ft) and **Edge** (a slider, named Gentle, Moderate, Steep, Very
    steep, or Cliff, applied when the drag ends).
  - The paint hint says the ground follows the type when it's on. A note under Height and
    Edge says they apply once the switch is on.
- Tests (26 new): uniform areas, cliffs that jump in one cell, gentle slopes rising smoothly
  through halfway, the steeper edge winning, continuity over a cube face's edge, stroke
  updates matching a full re-work, sculpting added and clamped, Flatten levelling the ground
  seen, and the version 29 golden file with its upgrade and refusals.
- **Verified in the app** (behind the owner's windows):
  - switched on by a click; painting plains, then mountains, gave 2,500 m in the middle;
  - a cliff edge gave 150, 150, 150, 2,058 (between two cells), 2,500 across the line;
  - Mountains made a raised plateau with its walls seen from orbit at 50× relief;
  - four undos turned it off and flattened the ground, and four redos brought it back;
  - save and reload kept the switch and the edge.
- **After the owner's review (2026-10-06):**
  - **Standing shows true heights** (owner's choice). First person sets View ▸ Relief to
    1× while standing and puts the chosen exaggeration back on leaving.
  - **Erase returns the ground to 0** (owner's choice). That's what unpainted ground already
    was, so nothing changed.
  - **Cliffs stand up close.** Blending heights evenly between grid cells made even a
    one-cell cliff a slope a whole cell wide (about 10 km, ~14°, on an Earth-sized world).
    The ground around a first-person eye now reads `HeightGrid.SampleSteepAt`
    (`PlanetSurface.GroundRadiusAt`). Where two cells differ by more than a natural slope
    (about 7°), the change is squeezed toward the line between them, by up to 40 times as it
    grows: a 2,350 m step becomes a sheer wall, and ordinary slopes are unchanged. Seen from
    3 km, a 2,500 m cliff stands as a wall with a ragged top.
- **Second review (2026-10-06):**
  - **The brush "stopped working" after editing a type.** With the switch on, every edit to
    a type (each keystroke of its name, each step of a color being picked) made the ground
    seen afresh as all-new tiles, so the globe re-sent all six faces of heights.
    - `TerrainRelief.Shaped` now takes what it made before and shares every tile where
      neither side changed; the session keeps each body's last one.
    - A type's height or edge re-works only the tiles near ground painted with it
      (`TerrainRelief.Rework`).
    - Recoloring also re-made the averaged terrain copy of all six faces every step, which
      was there before this milestone. Now it's done at most four times a second and only
      on faces with paint (`PlanetSurface.RedoFarColors`).
    - While a stroke paints, the ground goes to the globe at most twice a second and when
      the stroke ends (`ShowPainting`).
    - Ten recolors went from 2.4 s to 16 ms, and a paint step on a sculpted world from
      276 ms to 62 ms (outside a stroke).
  - **Flying keeps its height above the planet's radius**, not the ground (owner's
    request), so a cliff passing underneath doesn't drop the eye; the ground only pushes it
    up where it rises higher. Descending stops at the ground. In the app, flying north at
    1,000 m kept 1,000 m: 600 m over the plains, then lifted over the 2,650 m plateau.
    Flat worlds, being flat, keep their height above the face.
  - **No more seeing through the ground.**
    - The eye is kept above the highest corner of the ground mesh's cell under it
      (`FirstPersonGround.HighestAround`): the mesh is flat between its points, so on curves
      and steps it stood above the ground's own height.
    - The ground is rebuilt before the eye is placed, not after, so the eye is never
      measured against an old mesh.
    - The near clipping distance is a tenth of the eye's height (17 cm standing), not
      0.3, so a wall in front isn't cut away; the view still reaches 170 km.
    - Walking into a cliff for 240 frames, the eye never came within 1.7 m of the drawn
      ground.

**BOD-08 — Variation within terrain** · Implemented (M36: PR #92) · Base
**Intent:** The ground a terrain type shapes isn't one flat height: mountains have peaks,
hills roll, and plains are a little uneven, with how high and how far apart the features are
set per type (owner's request, 2026-10-06).
**Implementation (PR #92):**
- **Model:** `TerrainType` gains `VariationMeters` (0–8,000) and `FeatureSizeKm` (1–5,000,
  default 50). The defaults: Mountains 1,500 m / 40 km, Hills 350 / 30, Ocean 800 / 200,
  Plains 40 / 80, and so on (`TerrainType.Defaults`).
- **Noise:** `Model/TerrainNoise.Offset` is seeded value noise in up to six layers. Small
  variations roll smoothly; from 400 m they turn ridged, fully by 1,500 m, so big
  variations make peaks and ridges. Its exact recipe is in docs/world-format.md: only the
  settings are saved, so it must never change without a format change.
- **Seeds:** each body has its own (`TerrainRelief.SeedFor`, from its id), and each type
  within it, so two planets with the same paint get different mountains.
- **Grid limit:** features are never drawn finer than a few cells of the terrain grid (about
  10 km on an Earth-sized world), so they don't turn into spikes from cell to cell. Finer
  detail underfoot comes from the ground detail (`REN-06`).
- **Joins:** `TerrainRelief` adds each type's features to its height before the two types
  blend (`HeightAt`), so features run on to the line between them without a seam.
  `Rework` re-does only the ground near a type whose variation or size changed. Working out
  the ground is spread over the CPU's cores (`Parallel.For` over tiles).
- **App:** the session passes the body's radius and seed (`WorldSession.TerrainGround`); the
  Terrain panel's type editor has Variation and Size fields in the chosen units.
- **Format v30:** `terrainTypes[].variation`, `featureSize`. Version 29 types named like the
  defaults get the defaults' values; others stay level.
- **Reuse:** `TerrainNoise` works for any seeded, place-fixed bumpiness on a globe.

**BOD-09 — Water** · Implemented (M36: PR #92) · Base
**Intent:** A planet or moon can have water at a level the user sets: wherever the ground is
below it there's a water surface, seen from orbit and from the ground, and standing or flying
beneath it in first person looks like being underwater (owner's request, 2026-10-06).
**Implementation (PR #92):**
- **Model:** `Body.WaterLevelMeters` (null for none, −12,000 to 12,000 m), planets and
  moons only; format v30 `bodies[].waterLevel`. `WorldSession.SetWater` changes it as one
  undo step (typing a level merges into one).
- **System panel:** a Water row with Has water and the level, under Has air. Flat worlds
  can't turn it on yet: they don't draw heights. The disabled switch says so.
- **Color** (owner's choice): water over a terrain type with the Water climate (Ocean,
  Shallow Water, or the user's own) takes that type's color, darker with depth; water over
  land or unpainted ground is a standard blue-green. Where kinds of water meet, the colors
  blend over a cell (`water_tint.gdshaderinc`, shared by the globe and the water surface;
  `PlanetSurface.SetTerrainColors` makes the water palette). Changing a type's color or
  climate recolors its water. The murk and tint under water follow the water underfoot.
- **From orbit** (`planet_surface.gdshaderinc`): the globe is lifted to the water wherever
  the ground is lower, and drawn as water there: flat, darker with depth, and catching the
  sun. Its shore is smoothed over a pixel.
  `PlanetSurface.WaterLevelMeters` / `WaterRadius`; `SurfaceRadiusAt` (pins, regions,
  picking) sits on the water.
- **From the ground:** a water surface is built around the eye like the cloud deck (a
  `FirstPersonGround` at the water's radius, `water_surface.gdshader`). It darkens with the
  depth behind it (from the depth buffer), so the shallows show the bottom. Small waves are
  fixed to the place, and the sun shines off it. The ground under it is the true bottom.
  The camera's near clipping distance follows the nearer of the ground and the water's
  surface, so over deep water the surface below isn't cut away.
- **Underwater:** with the eye below the level:
  - the haze becomes a murk in the water's color, half gone at 9 m, over the sky too, darker deeper
    down and at night (`SurfaceSky.ShowUnderwater`);
  - a full-screen layer tints the view and makes it waver (`UnderwaterView`,
    `underwater.gdshader`);
  - ripples of light play over the bottom, fixed to it and fading with depth (`caustics` in
    `planet_surface.gdshaderinc`);
  - the surface overhead glows with the light coming through;
  - rain and snow stop.
- **Limits:** a globe carved by shapes isn't raised to the water (its mesh is built on the
  CPU), though it's still colored as water; standing on one shows no water surface.

**BOD-10 — Flat worlds in relief** · Implemented (M37: PR #93) · Base
**Intent:** A flat world can have everything a globe has on its surface: sculpted and
terrain-shaped heights with their peaks, water up to a level (with a waterfall where it reaches
the rim), and shapes added to or cut out of it (owner's request, 2026-10-07).
**Implementation (PR #93):**
- **No new data:** the heights, terrain shaping, water level, and shapes a body already has
  are drawn on its disc. Switching a planet between globe and flat world keeps them all.
- **The top face** is a finer disc (`FlatDiscMeshes.TopRelief`, by View ▸ Relief Detail),
  lifted point by point by the planet shader (`planet_surface.gdshaderinc`). Its slopes are
  shaded across the disc (`flat_relief_normal`).
- **Depth limit:** the ground never sinks below `PlanetSurface.FlatDeepestLift`, just above
  the underside, so a deep trench at 50× relief can't break through the 0.08-radius-thick
  disc.
- **The rim** is the south pole all the way round. The ground or water there lifts the face's
  outer ring and the rim wall's top edge alike (`PlanetSurface.UpdateRim`,
  `FlatDiscMeshes.RockLifted`), so they always meet.
- **Overlays:** `SurfaceRadiusAt` and `GroundRadiusAt` now give 1 plus the face's lift on a
  flat world, so pins, regions, brushes, and picking follow the ground with no changes of
  their own. `GlobePicker` settles a click onto raised ground in a few steps.
- **The waterfall** (`RimWaterfall`, `rim_waterfall.gdshader`) is a curtain hung all round
  from the rim's top edge:
  - it's shown when the water at the south pole stands above the ground there;
  - it's thrown out a little as it falls, streams downward, and turns to mist that fades
    well below the disc;
  - it's the color of the water at the rim.
- **First person:**
  - you stand on the true heights and fly at a set altitude, as on globes;
  - the ground patch (`FlatPatch`) is lifted point by point, and the rim wall is stretched
    to meet the edge;
  - a water patch carries the water's surface, and the underwater look works as on globes.
- **Near the eye** (globes too): standing, the world's own mesh is no longer raised to the
  water within the ground built around you (`PlanetSurface.SetNearEye`). Raised, it hid the
  bottom and fought the drawn water's surface, and showed grid lines on the sea.
- **Shapes:**
  - placed on the face, straight up, with north toward the center
    (`ShapeEdit.FrameOn(radiusKm, BodyShape.FlatDisc)`);
  - `ShapedGlobe` carves a closed disc whose rim and underside the planet shader draws as
    bare rock;
  - clicks land on the carving, and in first person you stand on it.
- **Limits:** as on globes, a carved disc isn't raised to the water. The disc's map is
  stretched toward the rim, so features painted near the south pole are stretched around it.

### 4.5 Orbits & Simulation (`SIM`)

**SIM-01 — Designed ("on-rails") orbits** · Implemented (M4; edited in the System panel) · Base
**Intent:** The default. Bodies follow the paths the user sets, and they stay stable forever.
**Implementation (Core, PR #16):**
- `Model/Orbit.cs` is an **immutable** record: parent, distance, period, start angle, plus
  optional eccentricity, closest-approach direction, tilt, and tilt direction.
- `Simulation/OrbitMath.cs` computes the offset from the parent at any time directly (Kepler's
  equation, so elongated orbits speed up near the parent). It's deterministic, and jumping to
  any date costs the same. `SystemPositions.At` stacks offsets up the chain of parents, in
  full-precision `Geometry/Vector3D` (km across a whole system).
- The exact math is in docs/world-format.md, so any viewer can reproduce positions.

**SIM-02 — Time simulation** · Implemented (M4) · Base
**Intent:** A world clock that advances the system. It lets the user track dates and the positions
of suns and moons at any point in time.
**Implementation (Core, PR #16):** `World.TimeDays` is the clock (standard days, saved with the
world). `Simulation/BodyClock.cs` gives each body's spin angle and its local "Day N, hh:mm"
(`LocalTime`), counted in that body's own day length.
**Implementation (time bar, PR #17):** `godot/UI/TimeControls.cs`, in the bottom-right corner:
Play/Pause, speed (1 hour to 1 year per second), the selected body's date, **Go to…** a day and
hour, and the **True scale** switch. `WorldSession.SetTime` moves the clock. Like the camera, the
clock is saved but isn't an unsaved change.
**Steps and gliding (owner's request, PR #18):** **−** / **+** step the clock by a chosen amount:
1 hour, 1 day, 1 week, 30 days, or 1 year. Steps are measured on the selected body: days in its
own day length, and a year is one trip around its star, or its planet's trip for a moon
(`BodyClock.YearDays`, tested). Steps and **Go to…** **glide**: the clock runs to the new time
over 0.8 s with a smooth start and stop, so bodies sweep along their real orbits instead of
popping into place. Clicking again mid-glide adds to where it's heading. Stepping pauses Play.

**SIM-03 — Physics mode (toggle)** · Implemented (M23) · Base (owner's choice, 2026-10-04: light enough, behind its own switch)
**Intent:** An optional toggle that simulates the system with real-world physics, so the user can
see how their system might fall apart.
**Implementation (the simulation, M23, PR #58):**
- **`Simulation/GravitySimulation.cs`** (Core), started at a time with `Start`:
  - **Start state:** the designed positions; each body's velocity is its parent's plus its
    designed motion sped up or slowed to gravity's period (`OrbitStability.NaturalPeriodDays`):
    the same ellipse, so the same place. The system's overall drift is taken away so its
    balance point stays put.
  - **Stepping:** every body pulls on every other (`BodyMass` masses), by the leapfrog method
    (kick, drift, kick), which keeps orbits from gaining or losing energy over time. Each step
    is 1/200 of the quickest orbit or close pass at that moment (at least 10⁻⁷ days), so a
    system with our Moon takes about 2,700 steps a year. Ten years of Sun, Earth, and Moon take
    well under a second; the Earth stays within 0.5% of 1 AU and the Moon with it (tested).
  - **Deterministic:** the simulation takes its own steps whatever times it's asked for, and
    positions between steps are interpolated (cubic Hermite from both ends' positions and
    velocities), so a time gives the same answer however the clock got there (tested: one jump,
    many small steps, and going back). A checkpoint every 256 steps lets the clock go back.
    `TryPositionsAt` takes at most a given number of steps, so the app can spread a long jump
    over several frames and draw where it has got to meanwhile.
  - **Collisions:** two bodies touch when their closest approach during a step (moving straight
    across it) is under their radii added (a flat world's rim counts). The smaller merges into
    the bigger, keeping the total momentum, and the meeting is logged once (`Collision`).
  - **Realms** ride their tree's branches as designed: they pull on others but aren't pulled,
    and go with their tree if it's swallowed. Two realms, or a realm and its tree, never meet.
- **Keep as orbits** (`KeepAsOrbits`, `Simulation/OrbitElements.cs`): turns each body's
  simulated position and velocity relative to its designed parent into the designed orbit that
  matches, the exact inverse of `OrbitMath` (tested round trip for round, elongated, tilted,
  and backwards orbits). A round or flat orbit keeps its designed directions. Bodies that merged
  away or are escaping (or too elongated to keep) are listed with the reason and keep their
  design.
- **`SystemLayout.At(bodies, truePositions, scale)`** draws any true positions as the designed
  ones are (tested identical); a body whose parent merged away is drawn from its nearest
  remaining parent.

**Implementation (the app, M23, PR #59):**
- **`Session/PhysicsMode.cs`** (`WorldSession.Physics`): starts a `GravitySimulation` from copies
  of the bodies (the world can be edited meanwhile) and runs it on its own thread, 2,000 steps
  at a time toward the clock's latest time, publishing each answer (`PhysicsSnapshot`:
  positions, whether it has caught up, collisions). The main thread never waits on it: it reads
  the latest answer, and only **Keep** waits for the simulation. Any change to the system's
  design (kinds, shapes, sizes, densities, orbits, branches, trees) restarts it from the new
  design at the current time; painting, journals, and the like don't. Closing or opening a
  world switches it off.
- **Drawing** (`Rendering/SystemView.cs`): while it's on, the layout comes from the latest
  positions (`SystemLayout.At` with true positions); a body that merged away is hidden, and the
  view follows the body that swallowed the focused one. The designed orbit lines are hidden,
  and so are the season, meteor shower, and eclipse markers on them (`FollowsPhysics`); the
  orbit guide's rings stay, around the bodies where they are.
- **Switch:** **Physics** in the time bar, an on/off switch (`TimeControls`).
- **Panel** (`UI/PhysicsSection.cs`, shown while it's on): "Real gravity since" the start, "Working
  out the jump…" while catching up, the collisions (the latest 12) with **Go to**, and **Keep as
  Orbits** (`WorldSession.KeepPhysicsOrbits`): the current paths become the designed orbits in
  one undo step and physics switches off, so the view carries on from the same places; bodies
  that merged away or are escaping keep their designs, named on the message line.
- **Verified in the running app** with real clicks, maximized and at 1152 × 648: the switch on,
  the planet holding its orbit and the Moon with it after 60 days, a giant planet placed head
  on swallowing the Moon on day 91 and flinging the planet out, the collision listed, **Keep as
  Orbits** (the giant's new path kept; the planet "escaping from Sun" and the Moon "merged into
  Giant" named), undo, and the switch off and on. Identical results on every run.
- **Benchmark:** physics off, against `main`: no change (196–202 fps). With 10 bodies, the
  clock moving 2 days a frame (about 400 days a second), physics off 188–189 fps, on 200–210
  fps (the orbit lines are hidden), and the simulation caught up on every frame.

**SIM-04 — Stable orbit guide** · Implemented (M22) · Base
**Intent:** For a selected body, show a guiding path for where stable orbits would be.
**Implementation (density and the math, M22, PR #56):**
- **Model:** `Body.DensityGramsPerCm3`, optional (null is typical); format **version 22**
  (`density`). Allowed from 0.000001 (thinner than giant stars) to 10,000,000 (a white dwarf).
- **Mass** (`Simulation/BodyMass.cs`, Core): density times the volume of a globe the body's
  size. Typical densities: planets as dense as Earth (5.513) up to 1.5 Earth radii, as dense as
  Jupiter (1.33) from 4 Earth radii, sliding smoothly between; moons 3.3; comets 0.6; world
  trees 0.5 (as if their whole size were wood); stars the mass a main-sequence star of their
  size has (0.08 to 100 Suns). Within 5% of the real Sun, Earth, Jupiter, and Moon (tested).
  A flat world weighs as much as a globe its radius, but its rim (π radii out) counts for
  collisions.
- **Stability** (`Simulation/OrbitStability.cs`, Core), all tested against real values:
  - **Roche limit** (fluid, 2.44 R ∛(ρ/ρ moon)): Earth's for the Moon is about 18,400 km.
  - **Hill sphere**: d (1 − e) ∛(m / 3M) at closest approach, against what the body circles or
    anything heavier circling it (so a planet at the center of its system is limited by its
    star just as if it circled it); infinite for the main star. Earth's is about 1.5 million km.
  - **Moon zone** (`MoonZone`): from the Roche limit (for a moon of rock and ice, 3 g/cm³) out to
    half the Hill sphere; none when there's no room.
  - **Neighbors' reach** (`Reaches`): each body circling a parent pulls on orbits within 2√3 of
    its Hill radii of its own path (the classic two-planet Hill stability spacing). Bodies
    heavier than the parent (a star circling a center planet) are left out.
  - **Natural period** (`NaturalPeriodDays`, Kepler's third law): a year at Earth's distance from
    the Sun. Not for realms or the center.
  - **Warnings** (`Warnings`): orbits that run into their parent, pass inside the Roche limit,
    stray past half the parent's Hill sphere (0.7 for orbits tilted past 90°, which go
    backwards), or come within 2√3 mutual Hill radii of a neighbor (or overlap it). Our own
    Sun, Earth, Moon, Mars, Jupiter, and Saturn give none. Two realms are never warned about
    each other.
- **App:** a **Density** field in the System panel's body properties with a **Typical** button
  (back to the typical density, a separate undo step), and the **Mass** it gives (Sun masses for
  stars, Earth masses, or kg for tiny bodies), marked "(typical)".
  `WorldSession.SetDensity`.
- **Verified in the running app** with real typing and clicks, maximized and at 1152 × 648:
  typing 2.5 g/cm³ (0.453 Earth masses), Typical (back to 5.51, 1 Earth mass), undo (2.5
  again), and the Sun (1.41 g/cm³, 1 Sun mass).

**Implementation (the guide in the app, M22, PR #57):**
- **Rings** (`Simulation/OrbitGuide.cs`, Core): for the selected body, the zone around it
  (where its moons could circle) and, unless it's a realm or circles something lighter, the
  zone around its parent (where it could), each split edge to edge into steady and unsteady
  rings: inside the Roche limit, or within a neighbor's reach (overlapping reaches joined; the
  body's own reach is left out around its parent, since it's the one that would move). A zone
  nothing limits (the main star's) is drawn to half again past its farthest orbit. A body with
  no room for moons is unsteady all round. `SystemLayout.DisplayDistance` maps a true distance
  to the drawn one (shared with `DisplayOffset`, tested equal).
- **Drawing** (`Rendering/OrbitGuideVisual.cs`): see-through rings flat in the orbit plane,
  green (steady) and red (not), one mesh per body they're around, rebuilt only when the world,
  the body, or the scale changes and moved with the bodies each frame. Around the body they line
  up with its moons' centers; around its parent, with where its own center would be; the
  innermost ring starts at the drawn edge. Shown for the body the System panel highlights,
  so only while the panel is open; **View ▸ Orbit Guide** switches it (on by default).
- **Panel** (`UI/OrbitGuideSection.cs`): a **Stable Orbits** section: where moons (or, around a
  star or tree, orbits) stay steady; the Roche limit and Hill sphere (km, millions of km, or
  AU); the body's warnings in amber, or "Its orbit would stay steady"; and **Gravity's period**
  with **Use** (disabled when it already matches, or when a calendar sets the period). Refused
  edits show on the message line.
- **Verified in the running app,** maximized and at 1152 × 648: the planet's moon zone and its
  place round the Sun, a new moon splitting the zone, the moon pulled in to 12,000 km (no room,
  the Roche warning), **Use** setting the Moon's period to gravity's 27.29 days, the Sun's
  zone with the planet's reach, and the guide switched off. Benchmark: no change (it doesn't
  open the panel, so the guide isn't drawn).

### 4.6 Time & Calendar (`CAL`)

**CAL-01 — User-defined calendar** · Implemented (M5) · Base
**Intent:** The default. The user defines their calendar, and it doesn't have to be
astronomically accurate. Some users won't care about the calendar at all, so it must be optional.
**Implementation (Core, PR #19):**
- `Model/Calendar.cs` is an **immutable** record on `Body.Calendar` (optional, owner's choice: per
  body). It holds months (`CalendarMonth`: name, days), weekday names, the first year, an optional
  era, and the start date and weekday at time 0. `Problem()` validates it, and equality compares
  the lists.
- `Simulation/CalendarMath.cs`: `DateOf` (day index → date, including negative days),
  `DayIndexOf` (the reverse, for Go to), and `Format` ("14 Highsun 1203 of the Third Age,
  Moonday"). `BodyClock.Describe` gives the time bar's text, with the calendar date if the body
  has one and otherwise "Day N". `BodyClock.TimeAt` converts a day and hour back to world time.
- World file **format version 6** (optional `calendar`).

**Implementation (app, PR #20):**
- `godot/UI/CalendarDialog.cs`: the editor, opened from **Calendar · Edit…** in the System panel
  (planets and moons only). Months (name, days) and weekdays are lists with ↑ ↓ ✕ and Add;
  first year, era, and start month/day/weekday below. A summary compares the calendar year with
  the body's real year. Save applies everything as **one undo step**; problems (from
  `Calendar.Problem()`) show in the dialog, which stays open. **Remove Calendar** goes back to
  counting days. A body without a calendar starts from twelve months that share its year's days
  and a seven-day week, ready to rename (Claude's choice).
- `WorldSession.SetCalendar(bodyId, calendar?)` is the one edit path (undoable).
- The time bar shows `BodyClock.Describe` (the calendar date), and **Go to…** asks for year,
  month, day, and hour when the body has a calendar (`CalendarMath.DayIndexOf` +
  `BodyClock.TimeAt`), or day and hour without one. `TimeControls.GlideTo` is public so other UI
  can glide the clock.
- The date and season lines sit in their own small panel above the time bar's buttons, so long
  calendar dates never push the buttons into the camera text.

**CAL-02 — Calendar accuracy mode (toggle)** · Implemented (M12, PR #36) · Base
**Intent:** An optional toggle that makes the world fit the calendar. The tool adjusts the
system's parameters (e.g. orbital periods, rotation speed) so the user's calendar becomes accurate.
**Implementation (PR #36):**
- **Model:** `Calendar.Fit` (`CalendarFit`: `None`, `YearLength`, `DayLength`) and
  `Calendar.MonthMoonId`. Format **version 11** (`fit`, `monthMoon`); see world-format.md,
  **Calendar fitting**, for the exact math.
- `Simulation/CalendarFitting.cs` (Core): `Apply` sets every fitted value (years, then days,
  then month moons, in body order: deterministic) and skips any that can't be met; `Problem`
  explains why a calendar's fit can't be met (no star, another calendar already sets that
  orbit, the month moon isn't a moon of the body, no moon cycle can last that long, or a value
  out of range); `Preview` lists what a calendar would change; `FittedBy` says which calendar
  sets a body's day length and orbit period. `BodyClock.YearOrbitOf` names the body whose orbit
  makes a year (`YearDays` uses it).
- **Month moons** use the synodic month: seen from the planet the star drifts once around the
  sky a year, so the moon's orbit is set to 1 / (1/month + 1/year), or 1 / (1/month − 1/year)
  if it circles the other way round. Tested geometrically: the moon's phase comes back exactly
  after a month, and is opposite halfway.
- **Session:** after every change to the system (`MarkChanged`), the fits are applied again, in
  the same edit and undo step, so the world stays fitted (e.g. changing a fitted planet's day
  length re-fits its orbit and its month moon). `SetCalendar` refuses a fit that can't be met,
  with the reason. Deleting a month moon clears it from the calendar.
- **App:** the calendar editor's **Fit the World** section (under the months and their year
  comparison): **Keep each year exactly one calendar year**, **By** (the orbit or the day), and
  **Month moon** (the body's moons), with a live preview ("Planet's orbit: 365.25 → 365 days")
  or the reason it can't fit. In the System panel, fitted **Day length** and **Period** fields
  are locked, and their tooltips name the calendar that sets them.
- **Verified in the running app** with real clicks, maximized and at 1152 × 648: turning it on
  and picking a month moon (preview shown), Save (one undo step: the orbit becomes 365 days and
  Luna's 28.077), the locked fields and their tooltips, a day-length change re-fitting the
  orbit and moon live, undo, switching to the day length, deleting the moon, and save/reopen.

**CAL-03 — Solstices, equinoxes, and seasons** · Implemented (M5) · Base
**Intent:** Derived from the system's configuration.
**Implementation (Core, PR #19):**
- Bodies gain **`AxialTiltDirectionDegrees`** (which way the north pole leans).
  `Simulation/BodyOrientation.NorthPole` is the one rule for the axis. The renderer orients globes
  with it too (checked in the app: the drawn pole matches Core exactly), so what's shown always
  matches the seasons.
- `Simulation/Seasons.cs`: the star's declination (how far north or south of the body's equator
  it stands) over time. Solstices are its peaks and lows; equinoxes are its zero crossings.
  Each is found by sampling 720 times a year, then refined exactly (bisection or golden-section
  search). `EventsBetween`, `NextEvent`, and `SeasonAt` (northern and southern; opposite in the
  south). `StarFor` finds the star, including for moons and planet-centered systems.
- Tested: four events a quarter year apart on an Earth-like orbit; the star exactly at the tilt
  at a solstice and on the equator at an equinox; uneven seasons on an elongated orbit (Kepler);
  identical seasons after Make Center; moons get seasons from the star; no tilt or no star means
  no seasons; deterministic.

**Implementation (app, PR #20):**
- **Axis direction** field (0–360°, wraps) next to Axial tilt in the System panel;
  `WorldSession.SetBodyPhysical` takes it too.
- `Simulation/SeasonTimeline.cs` (Core): a body's events for two years either side of a time,
  worked out once. `SeasonAt`, `NextEvent`, `LatestEvent`, and `YearAround` (the event that began
  the current season plus the next three) answer from it. `WorldSession.SelectedSeasons` caches
  one for the selected body and redoes it only after an edit, a new selection, or a year of
  clock time. The search adds up just the two chains of orbits it needs, so a timeline takes
  ~4 ms on the baseline laptop (instead of 30+ ms).
- **Time bar:** a line under the date, e.g. "Northern spring, southern autumn · Northern summer
  solstice in 83 days"; its tooltip gives the full date.
- **System panel** (`godot/UI/CalendarSection.cs`): the year of events with their dates and
  **Go to** buttons (they glide the clock there).
- **Orbit markers** (`godot/UI/BodyMarkers.cs`): the same four events drawn on the orbit that
  shows the body's year (`Seasons.OrbitShowingYear`: its own orbit, its planet's for a moon, or
  the star's in a planet-centered system), placed with `SystemLayout.OrbitPoint`. Equinoxes are
  dots and solstices are diamonds, colored by the northern season they begin. Markers off the
  screen are skipped: a point nearly beside the camera projects millions of pixels away, and
  the engine stalled 30–50 ms drawing shapes there (caught by the benchmark).
- Wording in one place: `godot/UI/SeasonText.cs`. Events are named for the north ("Northern
  summer solstice"); the south's season shows alongside.

**CAL-04 — Leap years** · Implemented (M17) · Base
**Intent:** Calendars can add leap days on a regular rule, so they can follow the real year
without the world being changed to fit.
**Implementation (PR #43):**
- **Model:** `Model/LeapRule.cs` (`Every`, optional `Except` and `ExceptAgain`, `Month`, `Days`):
  a year is a leap year if its number divides by `Every`, unless it divides by `Except`, unless
  it divides by `ExceptAgain` (each a larger multiple of the one before). `Calendar.Leap`,
  `AverageDaysPerYear`, and `DaysInMonth(year, month)`. Format **version 14** (`leap`).
- **Dates:** `Simulation/CalendarMath.cs` counts leap years arithmetically (no loops), so dates
  far from the start are as fast as near ones: it estimates the year from the average year, then
  corrects it. Tested against our own calendar (146,097 days in 400 years) and round trips.
- **Average year:** fitting (`CAL-02`) and the time ruler use `AverageDaysPerYear`, so a fitted
  calendar with leap years keeps the orbit at the true average (365.2425 days for ours).
- **Suggest:** `LeapRule.Suggest` tries one tier, then two, then three, taking a further tier only
  when it halves the drift, and stops once drift is under a day per 2,000 years.
- **App:** `godot/UI/LeapYearSection.cs`, the calendar editor's **Leap Years** section under the
  months: an on/off box, the rule's fields (0 = no exception), the leap days and their month,
  **Suggest**, and a line with the average year and how often it drifts a day from the real one.
- **Verified in the running app** with real clicks, maximized and at 1152 × 648: turning it on,
  Suggest, Save (the leap month gains its day in leap years), and save/reopen.

**CAL-05 — Calendar view** · Implemented (M39: PR #95) · Base
**Intent:** Time is shown as a calendar, as fantasy-calendar.com does: a month or year grid
for the selected world with the moons' phases, seasons, eclipses, meteor showers, and events
on each day, and a time bar that shows the date in the world's own calendar and steps by day,
month, or year (owner's request, 2026-10-08).
**Implementation (PR #95):**
- **Core** (`Simulation/`):
  - `MonthPage` lays a month out as a wall calendar's page (its days, its first day's
    weekday column) and turns to the next or previous month across years;
  - `CalendarMath.AddMonths` / `AddYears` keep the day of the month, or the month's last day
    if it's shorter (31 January + 1 month = the end of February);
  - `TimeSteps.Apply` steps the clock by an hour, a day, a month, or a year on a body, keeping
    the time of day; without a calendar there are no months and a year is one orbit;
  - `MoonPhase.Of` gives how lit each of a body's moons is from it, whether it's waxing, and
    the phase's name.
- **The Calendar tab** (`UI/CalendarPanel.cs`) replaces the Timeline button. It runs along
  the bottom, above the time bar, with the globe still in view, and is one of the right-hand
  panels (opening it closes the others); it leaves room for the System panel.
  - **Month:** a cell per day under the weekday names (rows of ten for a calendar without
    weeks), each with its number, its moons as drawn phases (`MoonIcon`), and what happens
    that day: the world's events in their timeline's color, solstices and equinoxes,
    eclipses, and meteor showers' peaks. Today is outlined in blue.
  - **Year:** every month small; a day's number takes the color of what happens on it.
    Clicking a month's name opens it.
  - **Timeline:** the timeline of events (`TimelineStrip`'s content, moved into the panel).
  - Click a day to run the clock there (same time of day); double-click to add an event that
    day. ‹ › turn months, « » years, Today comes back; the page follows the clock until it's
    turned by hand.
  - **Next Solar Eclipse** and **Next Lunar Eclipse** buttons keep the owner's "one click
    away" from the old Go to box; **Edit Calendar…** opens the calendar editor. A body without
    a calendar shows why there's no grid, with **Make a Calendar…**.
- **The time bar** (`UI/TimeControls.cs`):
  - the date in the body's calendar, with a clock face for the time of day (`ClockFace`:
    noon at the top, any day length) and its moons' phases; clicking the date opens the
    Calendar tab;
  - ‹ Y ‹ M ‹ D ‹ H, Play and its speed, H › D › M › Y › (steps glide, as before); the Month
    steps are disabled, with the reason, on a body without a calendar;
  - it replaces the step menu and the Go to box.
- **The hint bar** moved to the top, under the toolbar: the bottom is the time bar's and the
  calendar's.
- **Cost:** with the clock playing at a month a second, 122 fps with the calendar open and
  154 closed, on the baseline laptop.

**CAL-06 — Calendar presets and date pickers** · Planned (M40) · Base
**Intent:** A calendar can start from a ready-made one, and every date is picked from a
calendar rather than typed as a day number (owner's request, 2026-10-08).

### 4.7 Events (`EVT`)

**EVT-01 — Eclipses** · Implemented (M6) · Base
**Intent:** Simulated from body positions.
**Implementation (Core, PR #21):**
- `Simulation/Eclipses.cs`: every eclipse is a body passing through another's shadow. The
  star's light past the blocker makes the **umbra** (star fully hidden; past its point, the
  **antumbra**, where a ring of light stays) and the wider **penumbra** (star partly hidden).
  A moon's eclipses are its planet's eclipses with it, solar and lunar (PR #22).
  `Between(bodies, body, from, to)` steps through time measuring how far the shadowed body is
  from the shadow's center (60 points per moon orbit), finds each close pass, and refines it:
  roughly first, since most passes miss, then exactly (`TimeSearch`). Each `Eclipse` has its
  kind (`Solar`/`Lunar`, from the selected body's view), type (`Total`, `Annular`, `Partial`,
  `Penumbral`), the blocker and shadowed body, start/peak/end (the penumbra's first and last
  touch), and coverage (solar: share of the star hidden at the best spot; lunar: share of the
  moon's width in the umbra). A solar eclipse counts if the shadow touches the body anywhere,
  as on Earth.
- `Simulation/EclipseTimeline.cs`: the eclipses from half a year before a time to a year and a
  half after, with `Covers` and `Upcoming` (not yet over, up to a year ahead), like
  `SeasonTimeline`.
- Shared with the season search: `Simulation/OrbitChain.cs` (a body's position from just its
  chain of orbits) and `Simulation/TimeSearch.cs` (refining a moment; now stops at ~0.1 ms of
  precision instead of a fixed number of steps).
- Speed: a two-year timeline for an Earth-and-Moon system takes ~7 ms on the baseline laptop
  (almost all of it working out orbit positions). The app should work it out off the rendering
  path or only when needed.
- Tested with the real Sun, Earth, and Moon: with the Moon's orbit flat, a solar and a lunar
  eclipse every synodic month (29.53 days), half a month apart; annular solar eclipses at the
  Moon's average distance (~94% covered), total when it's closer; a central lunar eclipse is
  total and lasts 5–6.5 hours, symmetric about its peak; with the real 5.1° tilt, 2–5 of each a
  year; shallow passes are partial or penumbral; grazing passes are found and near misses
  aren't; a moon sees its own lunar eclipses; the same after Make Center; none for stars or
  moonless planets; deterministic.

**Implementation (app, PR #22):**
- `WorldSession.SelectedEclipses` returns the selected body's `EclipseTimeline`, or null while
  it's being worked out. It's worked out **in the background** (`Task.Run`, on a copy of the
  bodies), one at a time, only when something asks, and again after an edit, a new selection,
  or half a year of clock time (the last one stays meanwhile). `EclipsesReady` fires when it's
  in. A failure (which edits can't cause) is logged once, not retried every frame.
- **System panel** (`godot/UI/EclipseSection.cs`, under Seasons; hidden for stars): the coming
  year's eclipses, including one under way, at most 12, each with its title ("Total solar
  eclipse (Moon)", "Partial lunar eclipse of Moon", or "… (in Earth's shadow)" for the selected
  moon), date, depth and length ("94% of the star hidden · 5.1 h"), start and end in the
  tooltip, and **Go to** (glides to the peak). "Working them out…" shows meanwhile. It only
  updates while the panel shows.
- **Orbit markers** (`godot/UI/EclipseMarkers.cs`, its own overlay): where the moon is at an
  eclipse's peak, on its orbit around the planet's current position. **Only each moon's previous
  and next eclipse**, and only while within one trip of the moon around its planet from now
  (owner's request, after a first version marked every eclipse of the year in a ring). Solar: a
  dark disk ringed in gold; lunar: a dark red disk; labelled "Previous: Total lunar", "Next:
  Annular solar", and so on. Cached until the timeline changes or an eclipse peaks; off-screen
  markers are skipped. `EclipseTimeline.Covers` looks back a quarter year so the previous one
  is always known.
- **Go to… dialog** (`godot/UI/NextEclipseJumps.cs`): below the date fields, the selected
  body's next solar and next lunar eclipse with their dates and type, each with a **Go** button
  that closes the dialog and glides to the peak (owner's request). "None in the coming year" or
  "working it out…" otherwise; hidden for stars.
- Wording in one place: `godot/UI/EclipseText.cs`.

**EVT-02 — Meteor showers and asteroid events** · Implemented (M18, M20) · Base
**Intent:** Simulated celestial events.
**Implementation (comets, PR #44):**
- **Model:** `BodyKind.Comet`. `Body.HasSurface` (planets and moons) now gates everything that
  needs a surface: maps, terrain, regions, pins, weather, calendars, and eclipses. Rules in
  `SystemHierarchy`: a comet circles only a star, nothing circles a comet (`CanOrbit`), and a
  comet can't be made the center. Format **version 15** (kind `"comet"`).
- `Simulation/NewBodies.Comet`: 5 km across, on a flat orbit with elongation 0.8 that comes in
  to 60% of the innermost planet's distance, so it crosses that orbit twice. It starts a little
  before its closest approach, so its tail shows.
- **Tail rule** (`Simulation/CometTail.cs`): 20 million km long at 1 AU from the star, growing
  with the inverse square of the distance (capped at 150 million km); full brightness within
  3 AU, fading to nothing at 5 AU. It ignores the star's type for now.
- **App:** **Add Comet** in the System panel and the Add menu. For a comet, the panel shows
  "Comet" as its kind and hides the temperature, calendar, and eclipses; **Make Center** and
  **Add Moon** are disabled, and **Orbits** lists only stars. The grid never shows on comets.
- **Drawing** (`godot/Rendering/CometTailVisual.cs`, `comet_tail.gdshader`): one cone per
  comet, from the comet's head away from its star, compressed like every other distance in the
  view. It's added on top of what's behind it (no sorting, never hides anything) and fades
  along its length and toward its edges.
- **Verified in the running app** with real clicks, maximized and at 1152 × 648: Add Comet,
  the panel's fields, the tail at the start and at closest approach, and save/reopen. The
  benchmark showed no change (195–196 fps against 179–192 on `main`).

**Implementation (meteor showers, PR #45):**
- `Simulation/MeteorShowerTimeline.cs` (Core): a comet's dust lies all along its orbit, so a
  planet meets it wherever its own orbit passes within **0.1 × its orbit's size** of the
  comet's. The planet's year is sampled 720 times against the comet's orbit as 2,048 straight
  pieces (only those near the planet's distance from the star are kept); each close pass is
  refined (`TimeSearch`) to its peak, start, and end. Orbits never change on their own, so each
  shower repeats exactly once a year: they're found once, and every year's dates follow.
  `Between`, `ActiveAt`. A moon sees its planet's showers; planet-centered systems work too.
- **Strength** (owner's choice: from the comet's size): **10 meteors an hour per km** of the
  comet's radius at the peak, less the farther apart the orbits pass (none at the dust's edge).
  `MeteorShower.PerHourAt` rises steadily to the peak and falls to the end.
- **Speed:** about 2 ms per planet in a release build, 35 ms in a debug build, so the session
  works it out **in the background** like eclipses (`WorldSession.SelectedMeteorShowers`,
  `MeteorShowersReady`), after an edit or a new selection. While a new result is worked out the
  body's last one stays, so nothing flickers during a drag.
- **System panel** (`godot/UI/MeteorShowerSection.cs`, under Eclipses): the coming year's
  showers with their comet, peak date, strength, and length in the body's days ("Up to 50
  meteors an hour · 20 days"), start and end in the tooltip, and **Go to**.
- **Time bar:** a third line while a shower is under way: "Meteor shower from Comet 1: about 50
  an hour, peaking today".
- **Orbit markers** (`BodyMarkers`): a small streak where each shower peaks, on the orbit that
  shows the body's year, labelled "Comet 1 meteors". They have their own switch, **View ▸
  Meteor Shower Markers** (like eclipse markers).
- Wording in one place: `godot/UI/MeteorText.cs`.
- Tested: two showers a year from a crossing comet, at full strength; the same day every year;
  the peak where the orbits meet; a near miss at half strength; none from a far comet; moons
  see their planet's; the same after Make Center; none for stars and comets; deterministic.
  Verified in the running app with real clicks, maximized and at 1152 × 648: the list, Go to,
  the time bar line, the markers, and the View menu switch. Benchmark: no change.

**Implementation (asteroid events, M20, PR #52):**
- `Simulation/AsteroidEvents.cs` (Core; owner's choice: worked out from the belts, the same
  every time for a given world, impacts only listed). A belt's rocks aren't tracked, so its
  events are drawn by chance at a rate the system sets: **24 close passes a year** for a body
  whose year's orbit lies inside a full-density belt, scaled by the belt's density and by
  `Exposure` (the time-weighted share of the orbit inside the belt; stretches outside count
  less, fading over a quarter of the belt's width). **0.4% of passes are impacts** for an
  Earth-sized body, scaling with its area: about one every ten years deep in a dense belt. A
  moon shares its planet's orbit.
- Each year's events come from a fixed recipe (SplitMix64, not `System.Random`, whose numbers
  may change between .NET versions) seeded by the body, belt, and year, so the same world
  always has the same events and only edits change them. Sizes follow a power law (mostly
  small, a few up to kilometers); passes come from about 3 to 1,000 of the body's radii;
  impacts land anywhere, evenly.
- **App:** an **Asteroid Events** section in the System panel (`godot/UI/AsteroidEventsSection.cs`,
  under Meteor Showers): the coming year's close passes and impacts (up to 12), each with its
  date, the asteroid's size, how close it comes (three significant figures) or where it hits,
  the belt in the tooltip, **Go to**, and **Zoom to** for an impact's spot.
- Tested: about 24 passes a year inside a dense belt, none far from any belt, half the
  density giving about half the passes, impacts rare and placed, sizes and distances in range,
  the same events however the time is asked for, a moon's events, and the exposure inside and
  outside a belt. Verified in the running app with real clicks, maximized and at 1152 × 648:
  the list, Go to, and Zoom to landing on an impact's spot.

### 4.8 Weather & Climate (`WTH`)

**WTH-01 — Weather pin** · Implemented (M9) · Base
**Intent:** Drop a pin on a region to see what its weather would be, based on climate zone,
season, etc. This is the lightweight version that works on any system.
**Implementation (Core, PR #29):**
- `Simulation/ClimateYear.cs`: a year of days at a spot (360 per year). **Exact from the
  simulation:** the star's declination and distance each day give the daylight hours (polar day
  and night included), the noon sun height, and the day's sunlight relative to the body's
  average (1 is average; the distance factor is 1/r² against its yearly mean).
  **Temperatures, an estimate** (owner's choice: around `Body.AverageTemperatureC`):
  average + 62.5 °C × (the spot's yearly sunlight − 1) + 20 °C × (the day's sunlight felt
  through a 30-day heat lag − the spot's yearly sunlight), with a day/night swing of
  10 °C × √(day length ÷ 24 h), capped at 60. On an Earth-like planet: about 29 °C at the
  equator, 9 °C at 45° (−1 to 19 through the year, 8.6–15.4 hours of daylight), −16 °C at the
  poles, with the warmest day about 27 days after midsummer. `DayAt` (wrapped into the year)
  and `Average(from, to)` (e.g. a month) answer the chart; a body without a star has no
  weather.
- `Model/WeatherPin.cs` (in `World.WeatherPins`): a named spot on a planet or moon;
  `WeatherPin.Problem(world)` checks them together.
- `Body.AverageTemperatureC` (−270 to 2,000 °C; default 15).
- World file **format version 9** (bodies' `averageTemperature`, optional `weatherPins`).
- Tested: daylight against Earth's, polar day and night, noon heights, no tilt → 12 hours,
  Earth-like temperatures falling toward the poles, the month-long lag, opposite hemispheres,
  the average shifting everything, an elongated orbit's brighter closest approach, longer days
  swinging more, the year repeating, no weather without a star, determinism, and a golden
  version 9 file plus damaged-file cases.

**Implementation (app, PR #30):**
- **Weather Pin** in the toolbar (`godot/UI/WeatherMarkers.cs`): reuses `PinPlacer` for the
  click on the selected planet or moon, then asks for a name ("Weather pin N" to start), adds
  the pin, and opens its weather.
- Pins show as small suns with their names (`WeatherMarkers`, after the journal pins in the
  scene so a click on one is taken first), on globes at least 24 pixels across; the **Pins**
  toggle hides them along with the journal pins. Clicking one opens its weather.
- `godot/UI/WeatherWindow.cs` (doesn't block the app, so the clock can run): the pin's name
  (editable), place, today's temperature (low/high), daylight, noon sun height, and season for
  the pin's hemisphere; the year chart; a note that the temperatures are estimates around the
  body's average; **Delete Pin**. The year shown is the calendar year containing today, month
  by month (or the body's own year in twelfths, without a calendar); its weather is worked out
  again only when that year or the world changes.
- `godot/UI/WeatherChart.cs`: per month, a bar from the average low to the average high
  (colored cold blue to hot red) with the mean as a dot; daylight as a line on its own scale
  (6-hour steps); a dashed line at today.
- **Avg. temperature** (°C) in the System panel for planets and moons
  (`WorldSession.SetAverageTemperature`, one undo step while holding the arrow).
- Session (`WorldSession.Weather.cs`): `AddWeatherPin`, `RenameWeatherPin`,
  `DeleteWeatherPin`; weather pins are in undo snapshots and the saved-state check, and go
  with their body when it's deleted.

**WTH-03 — Terrain-aware weather** · Implemented (M14: PR #39, PR #40) · Base
**Intent:** Weather pins take the painted terrain around them into account (owner's choice,
2026-10-03).
**Implementation (temperature, PR #39):**
- `Model/ClimateKind.cs`: Water, Open land (the default), Forest, Desert, Wetland, Mountains,
  Ice. `TerrainType.Climate` holds each type's; the defaults: Ocean and Shallow Water are
  water, Forest and Jungle forest, Mountains, Desert, Swamp (wetland), Ice; the rest open land.
  Format **version 12** (`climate`; older types get it by name).
- `Simulation/TerrainSurroundings.cs`: the climate at the spot and the share of water around it
  (the spot plus 64 points on four rings out to 500 km, or 0.3 of a small body's radius;
  unpainted ground counts as not water). `Maritime` is 1 on water, else the water share.
- `ClimateYear.At` takes it (optional): **water** softens the seasons by up to 60% and the
  day/night swing by up to 70%, and doubles the heat lag (seasons come later); the **ground**
  scales the swing (desert ×2, mountains ×1.2, forest ×0.8, wetland ×0.7) and shifts the mean
  (ice −8 °C, mountains −6 °C, desert +2 °C). The yearly average otherwise stays. Without
  terrain the weather is exactly as before.
- **App:** a **Climate** choice for the selected type in the Terrain panel (one undo step, like
  renaming); the weather window allows for the terrain and adds a line such as "Terrain: Forest
  here, 31% water within 500 km: milder, later seasons and a smaller day/night swing; a
  slightly smaller day/night swing." (`godot/UI/ClimateText.cs`).
- **Tests:** `TerrainWeatherTests` (13: sampling on an unpainted planet, open sea, a coast,
  far inland, a small moon, unknown codes; the weather unchanged without terrain, the sea's
  softer and later seasons, each ground kind's swing and mean), and golden version 12, upgrade,
  and damaged-file tests.
- **Verified in the running app** with real clicks, maximized and at 1152 × 648: choosing a
  type and its Climate, the window's line for a forest pin by the sea, and Desert widening its
  day/night swing from 18–24 °C to 15–31 °C, undone with Ctrl+Z. Found and fixed there: in a
  short window the Climate list opened over its own button, and the end of the click picked
  the item under the mouse; it now opens when the click ends.
**Implementation (rainfall, PR #40):**
- `ClimateDay.RainMm` (mm per standard day) from `ClimateYear`: a **tropical rain belt** (9 mm
  a day at its peak, 11° wide) sitting at 0.6 of the star's latitude felt a month late, so wet
  seasons follow the sun with dry belts beside them; a **storm belt** (2.6 mm, centred at 48°,
  moving 0.35 of the star's latitude poleward in summer); and **drizzle** (0.25 mm) anywhere.
  It's scaled by the **cold** (full at 10 °C, fading to 15% at −25 °C) and by the terrain's
  **moisture** (`ClimateYear.Moisture`: 0.4 + 0.9 × the maritime share, about 1 for Earth's
  mix, times desert 0.15, ice 0.5, forest 1.2, wetland and mountains 1.3; 1 without painted
  terrain). An Earth-like planet gets about 1,750 mm a year at the equator, 250 at 30°, 1,000
  at 50°, and under 50 at the poles.
- **App:** the weather chart has a strip of monthly rain bars (mm, own scale) under the
  temperatures; the window gives the year's total in its heading, this month's rain with "wet
  season" (over 1.6× the average month) or "dry season" (under 0.4×), and the terrain line says
  how much wetter or drier it is. The window now scrolls when the screen is too short (it
  didn't fit at 1152 × 648 with the rain added), and temperatures never read "-0".
- **Tests:** `RainfallTests` (16: Earth-like totals by latitude, the wet season following the
  sun in both hemispheres, moisture from the sea and each kind of ground, a desert far drier,
  monthly averages, determinism).
- **Verified in the running app**, maximized and at 1152 × 648: a monsoon pin at 15° N (dry
  winters, wet June to September, 1,215 mm a year), a forest pin by the sea at 48° N (643 mm),
  and the same pin as desert (84 mm); the window fits and scrolls at the small size.

**WTH-02 — Live weather simulation** · Implemented (M27: PRs #71–#73) · Base (owner's choice, 2026-10-04; was Advanced)
**Intent:** As detailed as possible. Ideally the user can watch clouds and weather move across
the world.
**Implementation (the weather in Core, M27, PR #71):**
- **`Body.HasAtmosphere`** (owner's choice: a switch; planets on, moons off by default, also
  for older files): only bodies with air have live weather. Format **version 26**,
  `bodies[].atmosphere`.
- **`Simulation/LiveWeather.cs`:** `LiveWeather.For(bodies, body, terrainTypes)` (null without
  air or a star) builds, once per body: a `ClimateYear` every 5° of latitude (each moment's mean
  temperature and expected rain, as the weather pins have it), the star's latitude felt a month
  late through the year (where the tropical rain belt and the storm belts are), and coarse
  copies of the painted terrain (moisture every 5°, the kind of ground every 1°), so it never
  reads the terrain afterwards and moments can be worked out on other threads. About 50 ms.
- **`WeatherMoment`** (`LiveWeather.At(time)`): `SampleAt(spot)` gives a `WeatherSample`: cloud
  cover, rain or snow (mm an hour; snow at or below 0.5 °C), wind east and north, the day's
  mean temperature (with the ground's offset), and the storm the spot is in. About 6 µs a
  sample, so a whole globe at 64 squares a face is about 40 ms across the cores.
  - **Winds:** three belts each side of the equator (trade winds, westerlies, polar
    easterlies), shifted with the season; storms add their own, counterclockwise in the north.
  - **Clouds:** noise from `WeatherNoise` (hashed, the same on every machine) carried by the
    wind; two layers take turns starting afresh every 2 days so the winds never shear it into
    streaks. Wetter climates are cloudier; features about 900 km across.
  - **Storms** (`StormTracks`, `StormShape`): made up from the body's ID and fixed slots of
    time, so any moment finds its storms without stepping through the days before it.
    **Cyclones** form near the storm belts, ride the westerlies east and toward the poles for
    3.5–7 days, and are wide spirals with a trailing front; **tropical storms** form over warm
    water (or unpainted ground) when the tropics are 20 °C or warmer, likeliest late in summer,
    drift west then curve away from the equator, weaken over land, and have a clear eye.
  - **Rain** falls from the thickest half of the usual cloud and from storms; a year of it
    comes to about the climate's (measured: equator ~1,940 mm against 2,017; 45° ~860 against
    773; the dry belt at 30° a little drier, ~220 against 340).
- **Flat worlds:** no belts, a gentle wind, cyclones anywhere, no tropical storms.
- **Tests** (`LiveWeatherTests`, 25): which bodies have weather, the same weather every time,
  smooth change, sensible values at the poles, a year's rain against the climate's at five
  latitudes, the dry belt, a desert world, snow on a frozen world, the wind belts, cyclones'
  places and tracks, storms turning the right way in each hemisphere, tropical storms only on
  warm worlds, a cyclone's cloudy middle and a tropical storm's clear eye, and flat worlds; plus
  the version 26 golden file, older files' atmospheres, and new moons being airless.
**Implementation (drawing the weather, M27, PR #72):**
- **Drawn in the planet's own surface** (`Rendering/planet_weather.gdshaderinc`, included by
  `planet_surface.gdshaderinc` before the grid): clouds, rain and snow, and the wind over the
  ground, lit like it in every style (`REN-05`), on carved and flat worlds too. A first try drew
  a see-through shell over each globe; it cost about 18% of the frame rate on the baseline
  laptop (192 → 158 fps), because every pixel of the globe was drawn twice. In the surface:
  about 6% (192 → 180), and nothing at all with the weather hidden.
  - **Snapshots:** `WeatherImages` turns a `WeatherMoment` into two images (cloud, rain, snow;
    wind east and north), 256 × 128 spots (High) or 128 × 64 (Low), worked out across the
    processor's cores. Each globe keeps an older and a newer one (`WeatherSnapshots`) and
    blends between them as the clock moves.
  - **Clouds:** soft and wandering at the edges in Painterly, soft in Realistic, crisp in
    Simple; thin cloud is see-through; at High detail fine noise roughens the edges. Rain
    clouds are darker. By night clouds mostly fade (`SystemView` now tells every globe which way
    its star is, not only ringed ones), since the scene's ambient light would make them glow.
  - **Rain** shows as fine slanting lines and **snow** as dots, about 7 and 10 pixels apart
    whatever the zoom; the dry part of the globe skips them.
  - **Wind** (View ▸ Wind): short streaks on a grid about 28 pixels apart, each pointing with
    the wind at its middle, longer where it's stronger, brightest at the downwind end with a
    pulse running along it; they glow faintly, so they show by night too. (Streaks along the
    flow itself broke up wherever the wind turned.)
- **`Rendering/WeatherDisplay.cs`:** works each body's `LiveWeather` out again on another
  thread a moment after the world changes (from a copy of the bodies), and keeps the globes
  fed: one snapshot at a time, only for globes at least 20 pixels across, made a little ahead
  of the running clock (by how long the last one took and how fast the clock runs) so the
  blend never waits; a jump of the clock starts afresh. At 1 day a second the newest snapshot
  stays within about 0.01 day of the clock.
- **Settings:** View ▸ Clouds (on) and View ▸ Wind (off); Cloud Detail, Low or High (the
  default), remembered on this computer (`CloudDetail`; in File ▸ Settings since M29).
  Clouds hide while the Terrain or Map panel is open, so the ground being edited shows
  (owner's choice; `WeatherDisplay.SetSurfaceEditing`); the wind stays if it's on. The cost
  with the weather showing, about 6%, is fine for Base (owner's choice).
- **Atmosphere** in the System panel ("Has air", planets and moons):
  `WorldSession.SetAtmosphere`, one undo step.
- **Verified in the running app** with real clicks, maximized and at 1152 × 648: clouds in each
  style by day and night, moving smoothly with the clock running, the wind, Low detail, a
  cyclone and a tropical storm close up, the Atmosphere box (and undo) taking the weather away
  and back, and the View menu's items. Benchmark: 180/179 fps against `main`'s 192/191 with
  the weather showing; the same as `main` with View ▸ Clouds off.
**Implementation (today's weather in weather pins, M27, PR #73):**
- **Right now** in a weather pin's window (`UI/WeatherWindow.cs`), under today's figures: the
  live weather at the pin, from `WeatherDisplay.WeatherOf(body)` (the same weather the globe
  shows), worked out for the clock's moment whenever the window refreshes, and again when the
  weather is rebuilt (`WeatherDisplay.WeatherRebuilt`). Without air it says so, and how to give
  the body some.
- **`UI/LiveWeatherText.cs`:** the sky (Clear, Partly cloudy, Mostly cloudy, Overcast; Stormy
  in a cyclone, In a tropical storm), what's falling (dry, a trace, light, plain, or heavy rain
  or snow, with mm an hour), and the wind (calm, or from one of eight compass points, in
  km/h), such as "Stormy · rain, 1.1 mm an hour · wind from the west at 43 km/h". The window's
  note no longer says winds aren't modeled.
- **Verified in the running app** with real clicks on pins, maximized and at 1152 × 648: a pin
  in clear trade winds, one in a cyclone, the line following the clock, and "no air" with the
  Atmosphere box unticked (and undo).

### 4.9 Lore & Journal (`LORE`)

**LORE-01 — Region outlines** · Implemented (M8) · Base
**Intent:** Draw outlines around specific regions or locations on the world, with optional notes.
**Implementation (Core, PR #27):**
- `Model/Region.cs`: an immutable record in `World.Regions` (drawing order): the body (a
  planet or moon), name, notes, color, and corners (3 to 1,000 `GeoCoordinate`s). `Problem()`
  checks the name, notes, corner count, and that it fits within about half the globe;
  `Contains(spot)`.
- `Geometry/SphericalPolygon.cs`: double-precision outlines on a sphere. `Contains` projects
  the outline and the spot onto the plane touching the sphere at the outline's center
  (gnomonic: great-circle edges stay straight, so the test is exact) and counts crossings
  (even-odd). `FitsInHemisphere` (every corner within 85° of the center), `Center`, and
  `EdgePath` (points along the great-circle edges, for drawing).
- `LoreLocation` gains an optional `RegionId` (a region on its body): regions are places.
  `LoreRules` checks regions (on a planet or moon that exists, unique IDs, limit 5,000) and
  that a place's region is on its body; `RegionsAt(world, body, spot)` and `PlacedIn(world,
  region)` answer the pop-up's questions.
- World file **format version 8** (optional `regions`; places' optional `region`).
- Tested: inside/outside, either winding, across the date line, around a pole, the far side,
  great-circle edges, too-large outlines, the rules above, and a golden version 8 file plus
  damaged-file cases.

**Implementation (app, PR #28):**
- `godot/Rendering/RegionRenderer.cs`: one mesh per globe, a child of its surface (so it
  turns and tilts with the body), holding every region on it: a fill at 18% opacity, cut by
  `SphericalPolygon.FillTriangles` (ear clipping on the inside test's projection, then split
  until edges are 1.5° or less so it hugs the sphere), and the outline along `EdgePath`. Both
  are unshaded, lifted just above the map. A body's mesh is rebuilt only when its regions
  change; the region selected in the panel is outlined in white.
- `godot/Controls/RegionEditor.cs` (last in the scene, so it gets clicks first): **New Region**
  takes clicks on the selected globe as corners (Enter or the first corner finishes, Backspace
  removes the last, Esc cancels). **Edit Points** shows a handle on every corner and the middle
  of every edge: drag a corner to move it, drag a middle handle to add a corner, right-click a
  corner to delete it (at least three stay). Each drag is one undo step (a session gesture).
  Clicks off the globe still turn the camera.
- `godot/UI/RegionsPanel.cs`, opened by **Regions…** (one at a time with Journal and Pieces;
  `MapToolbar` unpresses the others): the selected body's regions, **New Region**, **Delete**,
  and the selected region's name, color, notes, corner count, **Edit Points**, and the entries
  and events placed in it (each opens). Everything applies as it's changed and is undoable.
- `godot/UI/RegionMarkers.cs`: each region's name at its center on globes at least 60 pixels
  across; a short click on a region of the selected body pops up its name, notes, what's placed
  in it, and **Edit Region…** (overlapping regions all show). Ignored while drawing, editing,
  placing a pin, or with the Pieces panel open; pins take their clicks first.
- `godot/UI/RegionChoice.cs`: the **Region** dropdown in the journal and event editors
  ("Anywhere on the body", or one of its regions), shown when the place's body has regions.
  Placing or removing a pin keeps the region.
- Session (`WorldSession.Regions.cs`): `AddRegion` (default name and the next of six colors),
  `UpdateRegion` (typing and color picking merge into one step), `DeleteRegion` (places in it
  keep their body). Regions are in undo snapshots and the saved-state check (`LoreState`), and
  deleting a body deletes its regions (owner's choice).
- The overlays redraw only when they have something to show (caught by the benchmark: the
  slowest frames had crept up while they redrew empty every frame).

**LORE-02 — Journal system** · Implemented (M7) · Base
**Intent:** Start with a simple journal. Entries are sortable and can be linked to locations.
Clicking a location can pop up its journal entries to read. The exact interaction is to be worked
out later.
**Implementation (Core, PR #23):**
- `Model/JournalEntry.cs`: an immutable record (ID, title, plain text, optional
  `LoreLocation`, created and edited times for sorting) in `World.Journal`. `Problem()` checks
  the title and length limits.
- `Model/LoreLocation.cs`: a body, plus an optional `GeoCoordinate` pin on its surface. Shared
  with timeline events.
- `Model/LoreRules.cs`: `Problem(world)` checks the whole lore together (limits, unique IDs,
  every event on an existing timeline, every link to an existing entry, every place on an
  existing body); `EventsLinkedTo(world, entryId)` gives an entry's events (links are stored
  on events only). The app must keep these true when it deletes things (e.g. a body).
- World file **format version 7** (optional `journal`, `timelines`, `events`). Loading checks
  everything; saving reads the new file back, so a broken link can never be written (tested).

**Implementation (panel, PR #24):**
- `godot/UI/JournalPanel.cs`, opened by **Journal…** in the toolbar, on the right (one at a
  time with the Pieces panel; `MapToolbar` unpresses the other button). A search box (titles
  and text), a sort menu (recently edited, title, date written, place), the list, **New
  Entry** and **Delete** (no confirmation; Ctrl+Z brings it back). Below: the selected entry's
  title, place (any body, or nowhere), text, the timeline events that link to it, and when it
  was written and edited. An existing pin is shown; setting pins comes with the globe pins.
  The panel is nearly opaque, for reading, and scrolls on short windows.
- Edits apply as they're typed through `WorldSession.UpdateJournalEntry` (merged into one undo
  step per entry), `AddJournalEntry`, and `DeleteJournalEntry` (also removes the entry's
  links from events), in `godot/Session/WorldSession.Lore.cs`.
- **Undo and saved state cover the lore:** `godot/Session/LoreState.cs` copies the three lists
  (their items are immutable, so the copies share them) into every undo snapshot and the
  saved-file comparison. Undoing back to the saved journal counts as saved again.
- **Deleting a body** clears the places on it in the same undo step (owner's choice).
- Journal writing no longer makes the seasons and eclipses work themselves out again:
  `WorldSession` keeps a separate system version for those caches.

**Implementation (pins, PR #26):**
- `godot/Controls/PinPlacer.cs`: **Pin on Globe…** (in the journal editor and the event editor)
  flies to the place's body and waits for a click on its surface (`GlobePicker.CoordinateAt`);
  Esc cancels, and clicks off the globe still turn the camera. The event editor steps aside
  meanwhile and comes back with its unsaved changes and the new pin (saved with Save).
  **Remove Pin** keeps the place without a spot. Stars can't be pinned (no surface).
- `godot/UI/PinMarkers.cs` (its own overlay, after the body markers so it gets clicks first):
  a pin for each pinned entry (parchment color) and event (its timeline's color; hidden
  timelines have none), on planets and moons drawn at least 24 pixels across, on the side
  facing the camera only. Pins within 14 pixels merge into one with a count. Clicking one pops
  up the place's coordinates and what's there; choosing an entry opens it in the Journal panel
  (`MapToolbar.ShowJournal`, `JournalPanel.SelectEntry`), choosing an event opens its editor
  (`TimelineStrip.EditEvent`). A **Pins** toggle in the toolbar hides them all.
- `godot/UI/PlaceText.cs`: coordinates as words ("18.29° N, 8.23° W").

**LORE-03 — Timelines** · Implemented (M7) · Base
**Intent:** Timelines of events. They become relevant once orbiting bodies are introduced, and
they're key helpers for the calendar features (`CAL-01`–`CAL-03`, `SIM-02`).
**Implementation (Core, PR #23):**
- `Model/Timeline.cs`: a named timeline (ID, name, color, hidden) in `World.Timelines`, in
  lane order.
- `Model/TimelineEvent.cs`: an immutable record in `World.Events`: its timeline, title,
  description, start (world time, standard days), optional end (not before the start), optional
  place, and the journal entries it links to (`EntryIds`, each once). Equality compares the
  links too.
- Validation, file format, and tests: see `LORE-02`.

**Implementation (strip, PR #25):**
- `godot/UI/TimelineStrip.cs`, shown by **Timeline…** in the toolbar: a band above the time bar
  with **New Event** (at the current time, then its editor), **Timelines…**, **Now**, and zoom
  buttons. `godot/UI/TimelineCanvas.cs` draws it: a ruler, a lane per shown timeline (its name
  and color), events as dots (moments) or bars (spans), and a line at the current time. The
  wheel zooms around the pointer (a minute to ~27,000 years per pixel), dragging scrolls,
  clicking an event glides the clock to its start and highlights it, double-clicking opens its
  editor; tooltips give the title, timeline, and dates. Each lane's events are drawn in time
  order, and a label is skipped where it would run into the one before.
- `Simulation/TimeRuler.cs` (Core, tested): the ruler's ticks. It picks the finest unit that
  fits (hours, days, months, or years in the selected body's calendar; round day numbers
  without one) and labels each tick in the body's own terms.
- `godot/UI/EventDialog.cs`: title, timeline, start and optional end ("Lasts until…"), place,
  description, and the linked journal entries as checkboxes. Save is one undo step; problems
  show in the dialog; **Delete Event** and **Go to** are there too. The date fields are
  `godot/UI/DateFields.cs`, now shared with the time bar's **Go to…** dialog.
- `godot/UI/TimelinesDialog.cs`: add, rename, recolor, show/hide, reorder, and delete timelines;
  each change applies at once and is undoable.
- Session (`WorldSession.Lore.cs`): `AddTimeline` (with the next of six lane colors),
  `UpdateTimeline`, `MoveTimeline`, `DeleteTimeline` (with its events), `AddEvent` (on the first
  shown timeline, making a "History" timeline if there's none), `UpdateEvent` (checks the
  timeline, links, and place still exist), `DeleteEvent`.

**LORE-04 — Lore relationship diagrams** · Implemented (M31: PRs #80–#81) · Base
**Intent:** Diagrams of connections between characters, factions, and nations (e.g. family trees,
alliances, rivalries). They probably need their own tab or page because of the space they take.
This is separate from the star system tree (`UI-02`).
**Notes:** Tabled until the lore features were further along; taken up in Milestone 31 (owner's
choice, 2026-10-05).
**Implementation (Core, M31, PR #80):**
- `Model/LoreKind.cs`: what an entry is about (character, faction, nation, place, other),
  as `JournalEntry.Kind` (null for plain writing). The journal's edits copy entries with
  changes, so they keep it.
- `Model/Relationship.cs` and `RelationshipKind.cs`: a tie from one entry to another (the
  parent, member, ruler, or servant is "from"), with a kind, a label (required for Other,
  up to 100 characters), and optional `StartDays` and `EndDays` on the world clock.
  `IsMutual` (married, siblings, allies, rivals, at war) and `StandsAt(time)` (inclusive at
  both ends) are for the diagrams. In `World.Relationships`, up to 20,000.
- `Model/LoreDiagram.cs` and `DiagramPlacement.cs`: a named diagram (up to 100 characters)
  and where each entry's box sits on it (diagram units of about a pixel, within ±1,000,000;
  each entry once; up to 1,000). In `World.Diagrams`, up to 200. Equal when every placement
  matches.
- `LoreRules`: checks both (own problems, unique IDs, every end and box an existing entry);
  `RelationshipsOf(world, entry)`; and `ForgetEntry(world, entry)`, which removes an entry's
  event links, ties, and boxes, now used by `WorldSession.DeleteJournalEntry` (so a deleted
  entry can't leave a tie that would stop the world saving). `LoreState` copies both lists,
  so undo and the saved-file check cover them.
- World file **format version 27**: optional `kind` on journal entries, optional
  `relationships` and `diagrams` (docs/world-format.md); older files have none.
- **Tests:** `RelationshipTests` (28: the rules, dates, mutual kinds, forgetting an entry,
  equality, cloning) and 26 more in `WorldPackageTests` (the version 27 golden file written
  and read, version 26 files, ten kinds of damage refused, every kind written by name).

**Implementation (the diagram page, M31, PR #81):**
- **`UI/DiagramPage.cs`** (a full-window layer just under the toolbar, which stays on top with
  the clock): the diagram list (New, Delete, a name field renamed as typed), a searchable list
  of the journal entries not yet on the diagram (Add to Diagram puts the chosen ones in a row
  in the middle of the view; double-clicking adds one), the selected box's kind, **Open in
  Journal**, and **Take off Diagram**, then **Arrange**, **Show every tie**, and a line of help.
  While open, the 3D view stops drawing (`Viewport.Disable3D`), the globe camera pauses, and
  the globe's overlays (`HideWhileOpen`) hide, each put back as it was. **Diagrams** is one of
  the toolbar's right-hand buttons (`MapToolbar.Diagrams`, `HideDiagrams`), so opening the
  Journal closes the page: that's how double-clicking a box opens its entry. Esc goes back.
- **`UI/DiagramCanvas.cs`** draws it: boxes colored and marked by kind (`UI/LoreWords.cs` holds
  the words and colors), an elbow from parents down to a child, a double line for a marriage,
  dashes for rivals and siblings, a heavy red line for a war, arrows for one-way ties, and the
  user's own words on the line. Several ties between one pair sit side by side. Ties are
  shown as of the clock: ended ones at 30%, those not begun hidden. Drag a box to move it (one
  undo step per drag, through the session's gestures); drag from its dot onto another box to
  tie them; click a line to edit it (hovering shows it as a sentence, with its dates); the
  wheel zooms around the mouse; the background pans. Shown diagrams fit the view.
- **`UI/RelationshipDialog.cs`**: from, kind, to, own words (needed for Other), and optional
  begin and end dates in the selected body's calendar (`DateFields`, like events'); Save is
  one undo step; Delete for an existing tie. Used by the page and by the **Journal panel**,
  which gains a **Kind** choice and a list of the entry's relationships (each opens the
  editor) with **Add Relationship…**.
- **`Session/WorldSession.Diagrams.cs`**: add, edit, and delete relationships (checked
  against the world: both entries must exist); add, rename, and delete diagrams; place,
  move, and take entries off a diagram; and Arrange, each undoable.
- **`Model/DiagramLayout.cs`** (Core): the Arrange layout. Family members (parent of, married
  to, sibling of) go in rows by generation, parents above children, spouses level and side
  by side, siblings level; each row is placed as near as it can be under the parents above
  it, at least 200 units apart, rows 140 apart. A loop of parents still gets a layout. The
  rest go in a grid below, walked along their ties so tied entries end up near each other.
  Dates don't matter to it (a parent stays a parent). Tests (`DiagramLayoutTests`, 9).

**Implementation (extras, M33, PR #84):**
- **Drag from the list:** the page's entry list forwards drags (`SetDragForwarding`): the
  chosen entries (or the one under the mouse) travel with their titles shown, and
  `DiagramCanvas._DropData` places them in a row where they land, as one undo step
  (`DiagramPage.AddEntries`, shared with the Add to Diagram button).
- **Save as Image…:** a native save dialog (Pictures folder, the diagram's name as the file
  name), then `SaveImageAsync` draws the diagram into an off-screen `SubViewport` with a
  second `DiagramCanvas` (`ForExport`: no link dots or selection, the name as a title, the
  same "every tie" choice) sized by `DiagramCanvas.PictureOf` at twice normal detail with a
  40-pixel margin (smaller if the picture would pass 8,192 pixels), waits for it to draw,
  and saves a PNG. A canvas now stops listening to the session when it leaves the tree.

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

