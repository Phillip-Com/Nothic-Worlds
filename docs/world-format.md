# World File Format (`.nworld`)

This is the specification for Nothic Worlds save files. It's engine-independent: anything that
can read a zip file and JSON can read a world, without Godot (CLAUDE.md §9). Code:
`src/NothicWorlds.Core/Storage/` (`WorldPackage` reads and writes it).

**Current format version: 28** (see **Version history** at the end)

## Container

A `.nworld` file is a standard **zip archive** containing:

| Entry | Contents |
|-------|----------|
| `world.json` | The world data (below), UTF-8 JSON, compressed |
| `assets/<32 hex chars>.<ext>` | The user's **original** map images, unchanged (`png`, `jpg`, `jpeg`, `webp`), stored uncompressed because images are already compressed |
| `terrain/<32 hex chars>.png` | A body's **painted terrain** (`BOD-05`, see **Terrain** below), named after the body's `id` without dashes. Only bodies with something painted have one. Stored uncompressed (PNG is already compressed). |
| `heights/<32 hex chars>.png` | A planet or moon's **sculpted heights** (`BOD-04`, see **Heights** below), named the same way. Only sculpted bodies have one. Stored uncompressed. |

- Only assets the world references are saved. Replaced maps don't pile up.
- Asset names must match `^assets/[0-9a-f]{32}\.(png|jpg|jpeg|webp)$`. Anything else is rejected,
  which also blocks names that try to escape the archive (`../`).
- Terrain image names must match `^terrain/[0-9a-f]{32}\.png$`, and height image names
  `^heights/[0-9a-f]{32}\.png$`.
- Limits when reading: `world.json` up to 16 MB, each asset up to 256 MB, each terrain image
  up to 16 MB, each height image up to 32 MB.

## `world.json` (an example)

An example of the file's main parts. It doesn't show every optional field: the tables below
list them all.

```json
{
  "formatVersion": 28,
  "id": "11111111-2222-3333-4444-555555555555",
  "name": "Aerth",
  "createdUtc": "2026-09-30T12:00:00+00:00",
  "modifiedUtc": "2026-09-30T13:30:00+00:00",
  "timeDays": 400.5,
  "bodies": [
    {
      "id": "51515151-5151-5151-5151-515151515151",
      "name": "Sol",
      "kind": "star",
      "radiusKm": 696000,
      "dayLengthHours": 609.5,
      "axialTilt": 0,
      "axialTiltDirection": 0,
      "appearance": { "starType": "yellow" },
      "surface": { "fillColor": "#E6EDF5" }
    },
    {
      "id": "70707070-7070-7070-7070-707070707070",
      "name": "Luna",
      "kind": "moon",
      "radiusKm": 1737.5,
      "dayLengthHours": 660,
      "axialTilt": 1.5,
      "axialTiltDirection": 0,
      "orbit": {
        "parent": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
        "distanceKm": 384400,
        "periodDays": 27.5,
        "startAngle": 0,
        "eccentricity": 0.25,
        "closestApproach": 45,
        "tilt": 5.25,
        "tiltDirection": 120
      },
      "appearance": { "color": "#8A8A8A", "pattern": "rocky" },
      "surface": { "fillColor": "#E6EDF5" }
    },
    {
      "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
      "name": "Aerth",
      "kind": "planet",
      "radiusKm": 6000,
      "dayLengthHours": 26.5,
      "axialTilt": 23.5,
      "axialTiltDirection": 45,
      "averageTemperature": 12.5,
      "orbit": { "parent": "51515151-5151-5151-5151-515151515151", "distanceKm": 149600000,
        "periodDays": 365.25, "startAngle": 90 },
      "calendar": {
        "months": [ { "name": "Frost", "days": 30 }, { "name": "Highsun", "days": 31 } ],
        "weekdays": [ "Moonday", "Starday" ],
        "firstYear": 1203,
        "era": "of the Third Age",
        "start": { "month": 1, "day": 5, "weekday": 1 },
        "fit": "year-length",
        "monthMoon": "70707070-7070-7070-7070-707070707070"
      },
      "appearance": { "color": "#336699", "pattern": "banded" },
      "surface": {
        "map": {
          "asset": "assets/0123456789abcdef0123456789abcdef.png",
          "projection": "winkel-tripel",
          "calibration": {
            "latitudes": [ { "latitude": 30, "drawnAs": 33.5 } ],
            "longitudes": [
              { "longitude": -180, "drawnAs": -185 },
              { "longitude": 0, "drawnAs": 2 }
            ]
          }
        },
        "pieces": [
          {
            "id": "99999999-8888-7777-6666-555555555555",
            "name": "Northern Isles",
            "asset": "assets/fedcba9876543210fedcba9876543210.png",
            "outline": {
              "sourceAspectRatio": 1.5,
              "points": [[0.25, 0.25], [0.75, 0.25], [0.75, 0.5], [0.25, 0.5]]
            },
            "latitude": 55,
            "longitude": -20.5,
            "rotation": 15,
            "width": 12.5,
            "warp": [[0, 0], [1.25, -0.125], [1, 1], [0, 1]]
          }
        ],
        "fillColor": "#112233",
        "terrain": "terrain/aaaaaaaabbbbccccddddeeeeeeeeeeee.png"
      }
    }
  ],
  "terrainTypes": [
    { "code": 1, "name": "Ocean", "color": "#1F4E79", "climate": "water" },
    { "code": 5, "name": "Forest", "color": "#2F6B35", "climate": "forest" },
    { "code": 13, "name": "Crystal Wastes", "color": "#B0E0E6", "climate": "desert" }
  ],
  "weatherPins": [
    { "id": "3c3c3c3c-3c3c-3c3c-3c3c-3c3c3c3c3c3c", "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
      "name": "Aster Bay", "latitude": 42.5, "longitude": -71.25 }
  ],
  "regions": [
    {
      "id": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
      "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
      "name": "The Western Coast",
      "notes": "Fishing towns.",
      "color": "#5090D0",
      "corners": [[10, -35], [10, -25], [16, -25], [16.5, -35]]
    }
  ],
  "journal": [
    {
      "id": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
      "title": "The Founding",
      "text": "First line.\nSecond line.",
      "kind": "faction",
      "location": {
        "body": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
        "region": "4e4e4e4e-4e4e-4e4e-4e4e-4e4e4e4e4e4e",
        "latitude": 12.5,
        "longitude": -30.25
      },
      "createdUtc": "2026-10-02T09:00:00+00:00",
      "editedUtc": "2026-10-02T10:15:00+00:00"
    }
  ],
  "timelines": [
    { "id": "7e7e7e7e-7e7e-7e7e-7e7e-7e7e7e7e7e7e", "name": "The Empire", "color": "#C04040" },
    { "id": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f", "name": "House Vael", "color": "#40A060",
      "hidden": true }
  ],
  "events": [
    {
      "id": "0f0f0f0f-0f0f-0f0f-0f0f-0f0f0f0f0f0f",
      "timeline": "7f7f7f7f-7f7f-7f7f-7f7f-7f7f7f7f7f7f",
      "title": "The Long War",
      "description": "Twelve years of war.",
      "start": 400,
      "end": 4783.25,
      "entries": [ "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1" ]
    }
  ],
  "relationships": [
    {
      "id": "a2a2a2a2-a2a2-a2a2-a2a2-a2a2a2a2a2a2",
      "from": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2",
      "to": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1",
      "kind": "other",
      "label": "pays tribute to",
      "start": 401,
      "end": 4783.25
    }
  ],
  "diagrams": [
    {
      "id": "d1d1d1d1-d1d1-d1d1-d1d1-d1d1d1d1d1d1",
      "name": "The Empire and Luna",
      "entries": [
        { "entry": "e1e1e1e1-e1e1-e1e1-e1e1-e1e1e1e1e1e1", "x": 0, "y": 0 },
        { "entry": "e2e2e2e2-e2e2-e2e2-e2e2-e2e2e2e2e2e2", "x": 240.5, "y": -80 }
      ]
    }
  ],
  "starSeed": 424242,
  "constellations": [
    { "id": "c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c1c1", "name": "The Kestrel",
      "lines": [[22, 121], [121, 139]] }
  ],
  "view": {
    "latitude": 20,
    "longitude": -45.5,
    "altitude": 1.25,
    "focusOffset": [0.5, 0, -0.25]
  }
}
```

| Field | Required | Meaning |
|-------|----------|---------|
| `formatVersion` | yes | Integer. See **Versioning**. |
| `id` | yes | GUID; stays the same across saves |
| `name` | yes | World name, not empty |
| `createdUtc`, `modifiedUtc` | yes | ISO 8601 timestamps |
| `timeDays` | no | The world clock: standard (24-hour) days since time 0. Default 0. |
| `style` | no | How the world is drawn (`REN-05`): `"painterly"`, `"realistic"`, or `"simple"`. Always written; omitted (files before version 25) means `"painterly"`. Only the look changes, never the world. |
| `bodies` | yes, 1 or more | Celestial bodies: suns, planets, and moons. |
| `bodies[].id` | yes | GUID, unique within the world |
| `bodies[].name` | yes | Not empty |
| `bodies[].kind` | yes | `"star"`, `"planet"`, `"moon"`, `"comet"`, or `"world-tree"` (see `tree`). A comet must circle a star, nothing may circle a comet, and a comet has no `calendar`. Its `appearance` is a `color` and `pattern`, like a moon's. |
| `bodies[].shape` | no | `"sphere"` or `"flat-disc"` (`BOD-02`); omitted for a sphere. Only planets and moons can be flat. A flat world is a disc with every latitude and longitude on its top face: the north pole at the center, and a point's distance from the center equal to its angle from the pole, so the disc's radius is π × `radiusKm` (`radiusKm` stays the matching globe's). |
| `bodies[].rings` | no | Rings (`BOD-03`), planets and moons only: `inner` and `outer` (in the body's radii; a flat world's disc radius), at least 1.05 and at most 50, `outer` beyond `inner`; `color` (`#RRGGBB`). They lie in the body's equatorial plane. Omitted for none. |
| `bodies[].belts` | no | Asteroid belts (`BOD-03`), stars only; omitted for none. Each: `id` (GUID, unique within the star), `name` (1–100 characters), `innerKm` and `outerKm` (above 0, `outerKm` beyond `innerKm`, at most 10¹³), `thickness` (how far the rocks' orbits tilt, 0–45°), `density` (0.01–1), `color` (`#RRGGBB`). Only the belt is saved; its rocks are drawn from it. |
| `bodies[].tree` | for world trees | How a world tree grows and looks (`BOD-02`); required for kind `"world-tree"` and not allowed otherwise: `branches` (3–16 great branches), `spread` (0.3–1.5), `seed` (0 or more; the same seed always grows the same tree), `bark`, `leaves`, `glow` (`#RRGGBB`), `glowStrength` (0–4). A tree's `radiusKm` is half its height and its `dayLengthHours` is how long it takes to turn. |
| `bodies[].branch` | no | For a realm: which great branch (counting from 0) of the world tree it orbits it hangs on (`BOD-02`); planets and moons only, one realm to a branch. Its `orbit` is then set by the branch when the world loads: round the trunk at the tip's distance, lifted to the tip's height, tilted with the tree, and one turn of the tree long. |
| `bodies[].radiusKm` | yes | Above 0, at most 10¹⁰ |
| `bodies[].dayLengthHours` | yes | Time for one spin, in standard hours. Above 0, at most 10⁷. |
| `bodies[].axialTilt` | yes | Degrees the spin axis leans, 0 to 180 |
| `bodies[].axialTiltDirection` | yes | Degrees: which way the north pole leans (see the axis rule below) |
| `bodies[].appearance` | yes | How the body looks (`BOD-06`). Stars: `starType`, one of `"red-dwarf"`, `"orange"`, `"yellow"`, `"white"`, `"blue"` (it sets the star's color and its light's). Planets and moons: `color` (`#RRGGBB`) and `pattern`, one of `"plain"`, `"rocky"`, `"banded"`, `"icy"`, `"cloudy"`, shown where there's no map. |
| `bodies[].atmosphere` | no | Planets and moons only: `true` if the body has air, and so live weather (`WTH-02`). Always written for them; omitted (files before version 26) means `true` for a planet and `false` for a moon. Refused on other kinds of body. |
| `bodies[].averageTemperature` | yes | °C, −270 to 2,000: the body's average surface temperature over a year (`WTH-01`; Earth about 15). Weather pins spread it by latitude and season. |
| `bodies[].density` | no | g/cm³, 0.000001 to 10,000,000: how dense the body is (`SIM-04`; Earth 5.5), which with its radius gives its mass, as if it were a globe. Omitted for the typical density of its kind and size (see `BodyMass` in the code). |
| `bodies[].calendar` | no | The body's own calendar (`CAL-01`). Omitted to count plain days. |
| `…calendar.months` | yes | 1 to 100 `{ "name", "days" }`, in order; each name not empty, days 1 to 100,000 |
| `…calendar.weekdays` | no | Weekday names, in order (at most 100, none empty). Omitted for a calendar without weeks. |
| `…calendar.firstYear` | yes | The year number at time 0 |
| `…calendar.era` | no | Words shown after the year number, e.g. "of the Third Age" |
| `…calendar.start` | yes | The date at time 0: `month` (0 is the first), `day` (1 is the first, within that month), `weekday` (0 is the first; 0 when there are no weekdays) |
| `…calendar.fit` | no | Keeps the world fitted to the calendar (`CAL-02`, see **Calendar fitting** below): `"year-length"` (the year's orbit changes) or `"day-length"` (the body's spin changes). Omitted when not fitted. |
| `…calendar.monthMoon` | no | The `id` of a body (it must exist) whose orbit is kept so new moon to new moon lasts one average month. It only takes effect while that body is a moon circling this one. |
| `…calendar.leap` | no | Leap years (`CAL-04`). Omitted for none. `every` (years, 1 to 1,000,000): a year whose number divides by it is a leap year; `except` (optional, a larger multiple of `every`): unless it divides by this; `exceptAgain` (optional, only with `except`, a larger multiple of it): unless it divides by this too. `month` (0 is the first) gains `days` (1 to 1,000) in leap years; that month's days plus `days` stay within 100,000. |
| `bodies[].orbit` | no | The body's designed orbit around another body. Omitted for a body at the system's center. Every chain of parents must end at a body without an orbit (no missing parents, no loops). |
| `…orbit.parent` | yes | The `id` of the body it circles |
| `…orbit.distanceKm` | yes | The orbit's size (semi-major axis), center to center. Above 0, at most 10¹³. |
| `…orbit.periodDays` | yes | Standard days per trip around. Above 0, at most 10⁹. Set freely, not derived from physics. |
| `…orbit.startAngle` | yes | Degrees: where the body is at time 0 (see the orbit math below) |
| `…orbit.eccentricity` | no | How elongated: 0 (default, a circle) to 0.95 |
| `…orbit.closestApproach` | no | Degrees: the direction of the closest approach. Default 0. |
| `…orbit.tilt` | no | Degrees from the reference plane, 0 (default) to 180. Over 90 runs backwards. |
| `…orbit.tiltDirection` | no | Degrees: where the orbit rises north through the reference plane. Default 0. |
| `…orbit.height` | no | Km the whole orbit is lifted along its own axis (the way its tilt points up); omitted for 0. Set for realms hung high or low on a world tree (`BOD-02`). |
| `bodies[].surface.map` | no | Omitted when the planet has no map |
| `…map.asset` | yes | Asset entry name (see Container) |
| `…map.projection` | yes | Map type: `equirectangular` (Globe map), `mercator` (Flat map), `robinson`, `winkel-tripel`, `mollweide`, `gall-peters`, `polar`, `two-hemispheres` |
| `…map.calibration` | no | Grid calibration (`MAP-05`). Omitted means the map is read exactly as its type says. |
| `…calibration.latitudes` | yes | Guides, south to north: `latitude` (true, between -90 and 90) is drawn where the map type puts `drawnAs`. Both increase strictly; the poles are fixed and not listed. |
| `…calibration.longitudes` | yes | Guides, west to east: `longitude` in [-180, 180) is drawn at `drawnAs`, less than half a turn away. `drawnAs` increases strictly, and may run past ±180 so the order holds around the globe (the first + 360 must exceed the last). |
| `bodies[].surface.pieces` | no | Pieces cut out of images and laid on the globe (`MAP-02`), bottom to top: later pieces cover earlier ones. Omitted when there are none. The app places up to 32 per planet. |
| `…pieces[].id`, `name` | yes | GUID; display name (not empty) |
| `…pieces[].asset` | yes | The source image's asset entry name (see Container). Saved with the world like the main map. |
| `…pieces[].outline.points` | yes | 3 to 1000 `[u, v]` points on the source image (0–1 from its top-left), forming a closed outline. A rectangle cut is 4 points. Must enclose an area. |
| `…pieces[].outline.sourceAspectRatio` | yes | The source image's width ÷ height, so the cut's true shape is known |
| `…pieces[].latitude`, `longitude` | yes | Where the center of the outline's bounding box sits on the globe |
| `…pieces[].rotation` | yes | Degrees, clockwise from "up = north" |
| `…pieces[].width` | yes | Degrees of arc the bounding box spans left to right (0.1 to 180). The height follows from the box's true shape. |
| `…pieces[].warp` | no | Edit Points (`MAP-02`): where each outline point has been dragged to, as `[u, v]` in the piece's box (0–1 from its top-left before warping; may go beyond). Exactly one per `outline.points`, in the same order. Omitted when the piece isn't warped. |
| `bodies[].surface.fillColor` | yes | `#RRGGBB`. Color where the map doesn't cover the globe |
| `bodies[].surface.terrain` | no | The body's terrain image entry name (see Container and **Terrain**). Omitted when nothing is painted. |
| `bodies[].surface.shapes` | no | Shapes added to or cut out of the body (`BOD-04`, see **Shapes**), applied in order. Planets and moons only, up to 64; omitted when there are none. |
| `bodies[].surface.shapes[].id` | yes | Stable identity (GUID), unique on the body. |
| `bodies[].surface.shapes[].kind` | yes | `"sphere"`, `"box"`, `"cylinder"`, or `"cone"`. |
| `bodies[].surface.shapes[].operation` | yes | `"add"` or `"cut"`. |
| `bodies[].surface.shapes[].latitude`, `.longitude` | yes | Degrees: the spot on the surface it's placed at (latitude −90 to 90). |
| `bodies[].surface.shapes[].depthKm` | yes | km: how far its middle is above the body's radius at that spot (negative: below), at most 4 radii either way. |
| `bodies[].surface.shapes[].widthKm`, `.heightKm`, `.lengthKm` | yes | km, above 0 and at most 4 radii: see **Shapes** for which each kind uses. All three are kept. |
| `bodies[].surface.shapes[].turn` | no | Degrees: how far it's turned about its height, clockwise from north seen from above. Omitted when 0. |
| `bodies[].surface.heights` | no | The body's height image entry name (see Container and **Heights**). Planets and moons only; omitted when nothing is sculpted. |
| `terrainTypes` | no | The kinds of terrain that can be painted (`BOD-05`), in list order. Omitted when there are none. Up to 255. New worlds start with 12 defaults. |
| `…terrainTypes[].code` | yes | 1 to 255, unique among terrain types: the value painted cells store. 0 means unpainted. |
| `…terrainTypes[].name` | yes | Not empty, up to 60 characters |
| `…terrainTypes[].color` | yes | `#RRGGBB`: how the terrain is drawn |
| `…terrainTypes[].climate` | yes | How it affects weather pins (`WTH-03`): `"open-land"`, `"water"`, `"forest"`, `"desert"`, `"wetland"`, `"mountains"`, or `"ice"` |
| `weatherPins` | no | Named spots whose weather is shown (`WTH-01`), in the order added. Omitted when there are none. Up to 1,000. |
| `…weatherPins[].id`, `name` | yes | GUID, unique among weather pins; name not empty, up to 100 characters |
| `…weatherPins[].body` | yes | The `id` of the planet or moon it's on (not a star) |
| `…weatherPins[].latitude`, `longitude` | yes | The spot: −90 to 90, −180 to 180 |
| `regions` | no | Areas outlined on planets and moons (`LORE-01`), in drawing order (later on top). Omitted when there are none. Up to 5,000. They may overlap. |
| `…regions[].id` | yes | GUID, unique among regions |
| `…regions[].body` | yes | The `id` of the planet or moon it's on (not a star) |
| `…regions[].name` | yes | Not empty, up to 100 characters |
| `…regions[].notes` | no | Plain text, up to 20,000 characters. Omitted when empty. |
| `…regions[].color` | yes | `#RRGGBB`: its outline and fill color |
| `…regions[].corners` | yes | 3 to 1,000 `[latitude, longitude]` corners in order around the outline (at least 3 different), joined by great-circle arcs. Every corner must be within 85° of the outline's center (see **Regions** below). |
| `journal` | no | Journal entries (`LORE-02`), in the order they were added. Omitted when there are none. Up to 10,000. |
| `…journal[].id` | yes | GUID, unique among entries |
| `…journal[].title` | yes | Not empty, up to 200 characters |
| `…journal[].text` | no | Plain text with `\n` line breaks, up to 200,000 characters. Omitted when empty. |
| `…journal[].kind` | no | What it's about, for its box in relationship diagrams (`LORE-04`): `character`, `faction`, `nation`, `place`, or `other`. Omitted for plain writing (and in files before version 27). |
| `…journal[].location` | no | Where it's about (see **Locations** below). Omitted for nowhere in particular. |
| `…journal[].createdUtc`, `editedUtc` | yes | Real-world times it was written and last changed (for sorting) |
| `timelines` | no | Named timelines (`LORE-03`), in lane order. Omitted when there are none. Up to 100. |
| `…timelines[].id`, `name` | yes | GUID, unique among timelines; name not empty, up to 100 characters |
| `…timelines[].color` | yes | `#RRGGBB`: its lane's color |
| `…timelines[].hidden` | no | `true` if its lane is hidden from the timeline strip. Omitted when shown. |
| `events` | no | Timeline events (`LORE-03`). Omitted when there are none. Up to 10,000. |
| `…events[].id` | yes | GUID, unique among events |
| `…events[].timeline` | yes | The `id` of the timeline it's on (must exist) |
| `…events[].title` | yes | Not empty, up to 200 characters |
| `…events[].description` | no | Plain text, up to 20,000 characters. Omitted when empty. |
| `…events[].start` | yes | When it happens (or begins), in standard days, like `timeDays` |
| `…events[].end` | no | When it ends, not before `start`. Omitted for a moment. |
| `…events[].location` | no | Where it happens (see **Locations**) |
| `…events[].entries` | no | The `id`s of journal entries it links to, each once (every one must exist). Links are many to many and stored only here; an entry's events are the ones listing it. Omitted when none. |
| `relationships` | no | Ties between journal entries (`LORE-04`), shown in every diagram that holds both ends. Omitted when there are none. Up to 20,000. |
| `…relationships[].id` | yes | GUID, unique among relationships |
| `…relationships[].from`, `to` | yes | The `id`s of two different journal entries (both must exist). For one-way kinds, `from` is the parent, the member, the ruler, or the servant. |
| `…relationships[].kind` | yes | `parent-of`, `married-to`, `sibling-of`, `ally-of`, `rival-of`, `at-war-with`, `member-of`, `rules`, `serves`, or `other`. `married-to`, `sibling-of`, `ally-of`, `rival-of`, and `at-war-with` are mutual. |
| `…relationships[].label` | no | The user's words for it, up to 100 characters. Required for `other`; omitted when empty. |
| `…relationships[].start`, `end` | no | When it began and ended, in standard days like `timeDays` (`end` not before `start`). Omitted for always and never. |
| `diagrams` | no | Named relationship diagrams (`LORE-04`), in list order. Omitted when there are none. Up to 200. |
| `…diagrams[].id`, `name` | yes | GUID, unique among diagrams; name not empty, up to 100 characters |
| `…diagrams[].entries` | no | The journal entries it shows, in drawing order, each once (every one must exist), up to 1,000: `entry` (its `id`) and `x`, `y` (where its box's middle sits, in diagram units of about a pixel, rightward and down, each within ±1,000,000). Omitted when empty. |
| `nebulas` | no | Nebulas on the sky around the system (`BOD-03`), at most 20; omitted for none. Each: `id` (GUID, unique), `name` (1–100 characters), `latitude` (−90 to 90°, above or below the system's reference plane) and `longitude` (any, measured like orbit angles), `size` (its radius on the sky, 2–120°), `brightness` (0.05–1), `color` and `secondColor` (`#RRGGBB`). |
| `starSeed` | yes | Where the night sky's stars come from (`REN-07`): a whole number from 0 to 2,147,483,647. The same seed always gives the same stars (see below). |
| `constellations` | no | Named star patterns (`REN-07`), at most 200, in list order; omitted for none. Each: `id` (GUID, unique), `name` (1–100 characters), and `lines`: up to 200 pairs `[a, b]` of star ids, each joining two different stars of this sky, no pair twice (in either order). |
| `view` | no | Camera when saved. Omitted means the default view. |
| `view.latitude`, `longitude` | yes | Degrees; camera direction from the focus point. Below an altitude of 0.25 (the local view, `REN-04`) they're the focused body's own coordinates, since the camera rides with its spin; otherwise they're fixed in space. |
| `view.altitude` | yes | In planet radii above the surface |
| `view.focusOffset` | no | `[x, y, z]` view-pan offset from the planet's center; default `[0, 0, 0]` |

**Locations** (journal entries and events): `body` is the `id` of a body that exists. An
optional `region` is the `id` of a region on that same body. An optional pin on its surface is
given by `latitude` (−90 to 90) and `longitude` (−180 to 180), both or neither; without a region
or pin it means the body as a whole.

**Regions** (`SphericalPolygon` in Core). An outline's **center** is the direction of the sum
of its corners' unit vectors (with the axes of the **Orbits** section's body frame: latitude φ,
longitude λ give (cos φ sin λ, sin φ, cos φ cos λ)). A spot is **inside** if, projected with
the corners onto the plane touching the sphere at the center (from the sphere's center, which
keeps great-circle edges straight), it's inside the projected polygon by the even-odd rule.
Spots on the far half of the sphere are never inside.

**Terrain** (`CubeSphere`, `TerrainGrid`, and `TerrainImage` in Core). A body's surface is
divided into cells by blowing a cube up into a sphere: six faces, each 1,024 × 1,024 cells.
The faces, in order, face +x, −x, +y (north), −y, +z, −z (the body frame's axes, as in
**Regions**). Each face has an outward direction *n*, a "right" axis *r*, and an "up" axis *u*:

| Face | *n* | *r* | *u* |
|------|-----|-----|-----|
| 0 | (1, 0, 0) | (0, 0, −1) | (0, 1, 0) |
| 1 | (−1, 0, 0) | (0, 0, 1) | (0, 1, 0) |
| 2 | (0, 1, 0) | (1, 0, 0) | (0, 0, −1) |
| 3 | (0, −1, 0) | (1, 0, 0) | (0, 0, 1) |
| 4 | (0, 0, 1) | (1, 0, 0) | (0, 1, 0) |
| 5 | (0, 0, −1) | (−1, 0, 0) | (0, 1, 0) |

A direction *d* belongs to the face whose *n* is closest (its largest component; ties go to the
earlier face). On that face, with *a* = (*d*·*r*)/(*d*·*n*) and *b* = (*d*·*u*)/(*d*·*n*), the
cells are spaced by equal angles: column = ⌊(4/π · atan *a* + 1)/2 × 1024⌋ and
row = ⌊(1 − 4/π · atan *b*)/2 × 1024⌋, each clamped to 0–1023. Row 0 is the top (toward *u*).

The terrain image is an ordinary **8-bit greyscale PNG**, 1,024 pixels wide and 6,144 tall: the
six faces stacked top to bottom in face order, each row by row. Each pixel's value is its
cell's terrain `code`, or 0 for unpainted. Codes not in `terrainTypes` are kept, and drawn as
unpainted. Readers accept any PNG row filter, but not interlacing, other bit depths, or colors.

**Heights** (`HeightGrid` and `HeightImage` in Core). Sculpted heights use the same cells as
**Terrain**: one height per cell, in whole meters up from the body's radius (negative is
below it), from −32,767 to 32,767. The height image is an ordinary **16-bit greyscale PNG**,
1,024 × 6,144 pixels laid out exactly as the terrain image, each pixel holding its cell's height
plus 32,768 (so 0 m is 32,768; a stored 0 is refused). Readers accept any PNG row filter, but
not interlacing, other bit depths, or colors. Heights are true to scale; how much the view
exaggerates them isn't stored.

**Shapes** (`ShapeEdit` in Core). Each shape has its own frame, in the body's radii (the globe
is a unit sphere, as in **Terrain**): with *u* the unit direction to its spot (from `latitude`
and `longitude`, as for pins), its middle is at *u* · (1 + `depthKm` / `radiusKm`); its height
runs along *u*; its length runs north along the surface (*n*, the direction toward +y with *u*'s
part taken out; at a pole, +z) turned by `turn` toward east (*e* = *n* × *u*); its width runs at
right angles, east turned the same way. A **sphere** is `widthKm` across; a **box** is `widthKm`
× `heightKm` × `lengthKm`; a **cylinder** and a **cone** (point up) are `widthKm` across at the
base and `heightKm` tall. The body is the sculpted globe, then each shape in turn added to it or
cut out of it; the faces a shape makes are bare rock. A cylinder 2 radii tall with its middle a
radius down goes right through the world; a sphere with its middle a radius down hollows it.

Names written for enums (`kind`, `projection`) are fixed strings. They're not the code's enum
names, so renaming code never changes the format.

## The star field (`starSeed`)

Constellations name stars by id, so every reader must make exactly the same stars from a seed
(`Simulation/StarField.cs` does it; this never changes):

- **Cells.** The sky is split into the cells of a cube's six faces, 128 × 128 each. A direction
  `(x, y, z)` meets the face of its largest component, at `(a, b)` from −1 to 1: face 0 (+X)
  `a = −z/|x|, b = y/|x|`; face 1 (−X) `a = z/|x|, b = y/|x|`; face 2 (+Y) `a = x/|y|, b = −z/|y|`;
  face 3 (−Y) `a = x/|y|, b = z/|y|`; face 4 (+Z) `a = x/|z|, b = y/|z|`; face 5 (−Z)
  `a = −x/|z|, b = y/|z|` (ties go to X, then Y). The cell is `col = floor((a + 1) / 2 × 128)`,
  `row = floor((b + 1) / 2 × 128)` (each at most 127), and its id
  `(face × 128 + row) × 128 + col`. +Y is the system's north, as for nebulas.
- **Random numbers.** Each cell has its own SplitMix64 stream: the state starts at
  `(seed << 32) | id` (both as unsigned 32-bit), and each number adds `0x9E3779B97F4A7C15` to
  the state, mixes it (`z ^= z >> 30; z *= 0xBF58476D1CE4E5B9; z ^= z >> 27;
  z *= 0x94D049BB133111EB; z ^= z >> 31`), and takes `(z >> 11) / 2^53`.
- **A star or not.** With `a`, `b` at the cell's middle, the cell holds a star if the first
  number is below `0.05 × 6 / (π × (1 + a² + b²)^1.5)` (so stars spread evenly).
- **The star.** The next two numbers place it across and down its cell, each
  `0.2 + 0.6 × n` of the way; its brightness is `0.08 + 0.92 × n⁷`; its temperature (color, 0
  red, 0.5 white, 1 blue) is `0.5 + 0.5 × (n₁ − n₂)` from the next two.

## Versioning

- Every file records `formatVersion`. A reader **refuses newer versions** with a clear message
  ("saved by a newer version of Nothic Worlds…"), instead of misreading them.
- Older versions are **upgraded on load**, one version at a time (`WorldFormat` migrations), and
  are written back in the current version on the next save.
- Changing the format requires raising the version, adding a migration, updating this document,
  and adding a golden-file test for the new version. Every older version's golden test
  (`WorldPackageTests`) must keep passing forever.

Between calibration guides, readers must interpolate with a monotone cubic curve (Fritsch–Carlson)
through the guides. For latitude, fixed anchors at (-90, -90) and (90, 90) are added. For
longitude, the guides are repeated one turn west and east. Straight lines extend beyond the ends.
(`MapCalibration` in Core.)

Pieces are laid on the globe with an **azimuthal equidistant** projection around their center:
a point at arc distance *d* and compass bearing *b* (clockwise from north) from the center sits at
(*d*·sin *b*, *d*·cos *b*) in the piece's frame. That frame is then turned by `rotation`, and scaled
so the bounding box spans `width` by `width` ÷ box aspect ratio. Box position (0, 0) is the
top-left. (`PieceProjection` in Core.)

A warped piece is stretched before it's laid on the globe: each outline point moves from its
place in the box to its `warp` position, and every other position follows by **mean value
coordinates** over the outline (Floater; Hormann & Floater for any polygon shape), measured at the
box's true proportions. Points on the outline move in a straight line between their two ends.
(`PieceWarp` in Core.)

**Orbits** (`OrbitMath` in Core). All angles are measured in the reference plane (y = 0) from
+x, counterclockwise seen from the north (+y): the direction at angle θ is
(cos θ, 0, −sin θ). With distance *a*, eccentricity *e*, period *P*, start angle *L*, closest
approach *ϖ*, tilt *i*, and tilt direction *Ω*, the position relative to the parent at time
*t* (days) is found like this:
1. Mean anomaly *M* = (*L* − *ϖ*) + 360° × *t* / *P*. Solve Kepler's equation
   *M* = *E* − *e*·sin *E* for *E*.
2. In the orbit's own plane: *x* = *a*(cos *E* − *e*), *y* = *a*√(1 − *e*²)·sin *E*. This is the
   vector (*x*, 0, −*y*).
3. Turn it by (*ϖ* − *Ω*) about +y, then tilt it by *i* about +x (right-handed: +y toward +z),
   then turn it by *Ω* about +y. "Turning by φ about +y" maps (x, y, z) to
   (x cos φ + z sin φ, y, −x sin φ + z cos φ).

A body's position is its parent's position plus this offset. Bodies without an orbit sit at
(0, 0, 0). A body turns once on its axis every `dayLengthHours`, eastward.

**Spin axis** (`BodyOrientation` in Core). With tilt *i* and tilt direction *θ*, the north pole
points along (0, cos *i*, 0) + sin *i* × (cos *θ*, 0, −sin *θ*): it starts straight up (+y)
and leans by *i* toward direction *θ*, measured like orbit angles.

**Calendars** count the body's own days (`CalendarMath` in Core). Day 0 is the day at time 0
(each day lasts `dayLengthHours`), which is the calendar's `start` date. Day *n* is *n* days on
through the months in order, wrapping into the next year after the last month, and back into
earlier years for negative *n*. Weekdays cycle the same way from `start.weekday`. In a leap
year (by its year number, see `leap`) the leap month has `leap.days` more days. The **average
year** is the months' days plus `days` · (1/`every` − 1/`except` + 1/`exceptAgain`), leaving out
the terms that are missing.

**Calendar fitting** (`CalendarFitting` in Core). The values a fit sets are saved like any
other (the file holds the fitted periods and day lengths); the app re-applies the fits after
every change to the system. With *N* = the calendar's average year in days and *h* = the body's
`dayLengthHours`: the **year's orbit** is the body's own orbit if it circles a star, else its
planet's (and so on up), or the orbit of a star circling them. `"year-length"` sets that
orbit's `periodDays` to *N* · *h* / 24; `"day-length"` sets *h* to (that orbit's `periodDays`)
· 24 / *N*. Year fits are applied first, then day fits, then month moons, each in body order;
two calendars can't fit the same orbit. A **month moon** of period *T* (standard days) makes the
star drift once around the sky per year *Y* (the year orbit's period), so new moon to new moon
lasts *S* = 1 / (1/*T* − 1/*Y*) when the moon circles the same way round as the year (both
orbits' `tilt` at most 90°, or both over), else 1 / (1/*T* + 1/*Y*). It's set so *S* equals
the average month, *N* / (number of months) · *h* / 24.

**Seasons** aren't stored; they're worked out (`Seasons` in Core). The body's star is the
nearest star up its chain of parents, or a star orbiting it or its planet. The star's
declination is the angle between the direction to the star and the body's equatorial plane.
Northern solstices are where it peaks and bottoms out; equinoxes are where it crosses zero.

**Weather** isn't stored either; it's worked out for each weather pin (`ClimateYear` in Core).
Daylight and sunlight are exact from the star's declination δ and the spot's latitude φ: the
star sets at hour angle ω₀ = arccos(−tan φ tan δ) (0 or π beyond the polar circles), the day is
lit for ω₀/π of the body's day, and a level spot gets (ω₀ sin φ sin δ + cos φ cos δ sin ω₀)/π
of the light facing the star, times (mean 1/r² over the year) ÷ (1/r² today) for the distance
r. Temperatures are an estimate around `averageTemperature`; the constants are in the code.
Painted terrain adjusts them (`TerrainSurroundings` in Core): the climate at the spot, and the
share of water among the spot and 64 points on four rings out to 500 km (or 0.3 of the body's
radius, if less); unpainted ground and unknown codes count as not water. Rain is estimated the
same way (from the latitude, the star's lagged latitude, the temperature, and the terrain); it
isn't stored either.

## Safe saving

1. Write the complete new file next to the target as `<name>.nworld.saving`, and flush it to disk.
2. **Read it back** fully with the normal loader. If that fails, delete it and report the error.
3. Swap it in atomically. The previous save is kept as `<name>.nworld.bak`.

If anything fails, the existing world file is left untouched.

## Version history

| Version | Changes | Upgrade from the previous version |
|---------|---------|------------------------------------|
| 1 | First release: world, one planet, map image + map type, fill color, camera view | — |
| 2 | Added optional `map.calibration` (grid calibration, `MAP-05`) | Nothing to change: version 1 maps have no calibration |
| 3 | Added optional `surface.pieces` (cut and place, `MAP-02`) | Nothing to change: version 2 surfaces have no pieces |
| 4 | Added optional `pieces[].warp` (Edit Points, `MAP-02`) | Nothing to change: version 3 pieces aren't warped |
| 5 | Star systems (M4): `timeDays`; bodies gain `radiusKm`, `dayLengthHours`, `axialTilt`, optional `orbit`; kinds `star` and `moon` | Each body gets `radiusKm` 6371, `dayLengthHours` 24, `axialTilt` 0, and no orbit |
| 6 | Calendars and seasons (M5): bodies gain `axialTiltDirection` and an optional `calendar` | Each body gets `axialTiltDirection` 0 and no calendar |
| 7 | Journals and timelines (M7): optional `journal`, `timelines`, and `events` | Nothing to change: version 6 worlds have none |
| 8 | Region outlines (M8): optional `regions`; places gain an optional `region` | Nothing to change: version 7 worlds have none |
| 9 | Weather pins (M9): bodies gain `averageTemperature`; optional `weatherPins` | Each body gets `averageTemperature` 15; worlds have no weather pins |
| 28 | Designed night skies (M34): `starSeed`; optional `constellations` | Each world gets the seed made from its `id` (its first four bytes, as a little-endian whole number, without the sign bit), so it keeps one fixed sky; no constellations |
| 27 | Lore diagrams (M31): journal entries gain an optional `kind`; optional `relationships` and `diagrams` | Nothing to change: version 26 worlds have none |
| 26 | Live weather (M27): planets and moons gain `atmosphere` | Nothing to change: a missing `atmosphere` means planets have air and moons don't |
| 25 | Visual styles (M26): worlds gain a `style` | Nothing to change: version 24 worlds are painterly, which a missing `style` means |
| 24 | Shapes (M25): surfaces gain optional `shapes` added or cut | Nothing to change: version 23 bodies have none |
| 23 | Body sculpting (M24): surfaces gain optional `heights` (a 16-bit height image) | Nothing to change: version 22 bodies are unsculpted |
| 22 | Stable orbit guide (M22): bodies gain an optional `density` | Nothing to change: version 21 bodies have the typical density for their kind and size |
| 21 | Realms (M21): bodies gain an optional `branch`, orbits an optional `height` | Nothing to change: version 20 bodies hang on nothing, and their orbits aren't lifted |
| 20 | World trees (M21): bodies can be of kind `world-tree`, with a `tree` | Nothing to change: version 19 worlds have none |
| 19 | Nebulas (M20): the world gains optional `nebulas` | Nothing to change: version 18 skies are empty |
| 18 | Asteroid belts (M20): stars gain optional `belts` | Nothing to change: version 17 stars have no belts |
| 17 | Rings (M20): planets and moons gain optional `rings` | Nothing to change: version 16 bodies have no rings |
| 16 | Flat worlds (M19): planets and moons gain an optional `shape` | Nothing to change: version 15 bodies are all spheres |
| 15 | Comets (M18): bodies can be of kind `comet` | Nothing to change: version 14 worlds have no comets |
| 14 | Leap years (M17): calendars gain optional `leap` | Nothing to change: version 13 calendars have no leap years |
| 13 | Body appearance (M16): bodies gain `appearance` | Stars get `starType` `yellow`; moons `color` `#8A8A8A` and `pattern` `rocky`; planets `color` `#214573` and `pattern` `plain` (the look they always had) |
| 12 | Terrain-aware weather (M14): terrain types gain `climate` | Types named (ignoring case) Ocean or Shallow Water get `water`, Forest or Jungle `forest`, Mountains `mountains`, Desert `desert`, Swamp `wetland`, Ice `ice`; all others `open-land` |
| 11 | Calendar fitting (M12): calendars gain optional `fit` and `monthMoon` | Nothing to change: version 10 calendars aren't fitted |
| 10 | Terrain painting (M11): `terrainTypes`; surfaces gain an optional `terrain` image | The world gets the 12 default terrain types (codes 1–12: Ocean, Shallow Water, Plains, Fields, Forest, Jungle, Hills, Mountains, Desert, Swamp, Tundra, Ice); nothing is painted |
