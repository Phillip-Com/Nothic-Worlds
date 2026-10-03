# World File Format (`.nworld`)

This is the specification for Nothic Worlds save files. It's engine-independent: anything that
can read a zip file and JSON can read a world, without Godot (CLAUDE.md §9). Code:
`src/NothicWorlds.Core/Storage/` (`WorldPackage` reads and writes it).

**Current format version: 9** (see **Version history** at the end)

## Container

A `.nworld` file is a standard **zip archive** containing:

| Entry | Contents |
|-------|----------|
| `world.json` | The world data (below), UTF-8 JSON, compressed |
| `assets/<32 hex chars>.<ext>` | The user's **original** map images, unchanged (`png`, `jpg`, `jpeg`, `webp`), stored uncompressed because images are already compressed |

- Only assets the world references are saved. Replaced maps don't pile up.
- Asset names must match `^assets/[0-9a-f]{32}\.(png|jpg|jpeg|webp)$`. Anything else is rejected,
  which also blocks names that try to escape the archive (`../`).
- Limits when reading: `world.json` up to 16 MB, each asset up to 256 MB.

## `world.json` (version 9)

```json
{
  "formatVersion": 9,
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
        "start": { "month": 1, "day": 5, "weekday": 1 }
      },
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
        "fillColor": "#112233"
      }
    }
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
| `bodies` | yes, 1 or more | Celestial bodies: suns, planets, and moons. |
| `bodies[].id` | yes | GUID, unique within the world |
| `bodies[].name` | yes | Not empty |
| `bodies[].kind` | yes | `"star"`, `"planet"`, or `"moon"` |
| `bodies[].radiusKm` | yes | Above 0, at most 10¹⁰ |
| `bodies[].dayLengthHours` | yes | Time for one spin, in standard hours. Above 0, at most 10⁷. |
| `bodies[].axialTilt` | yes | Degrees the spin axis leans, 0 to 180 |
| `bodies[].axialTiltDirection` | yes | Degrees: which way the north pole leans (see the axis rule below) |
| `bodies[].averageTemperature` | yes | °C, −270 to 2,000: the body's average surface temperature over a year (`WTH-01`; Earth about 15). Weather pins spread it by latitude and season. |
| `bodies[].calendar` | no | The body's own calendar (`CAL-01`). Omitted to count plain days. |
| `…calendar.months` | yes | 1 to 100 `{ "name", "days" }`, in order; each name not empty, days 1 to 100,000 |
| `…calendar.weekdays` | no | Weekday names, in order (at most 100, none empty). Omitted for a calendar without weeks. |
| `…calendar.firstYear` | yes | The year number at time 0 |
| `…calendar.era` | no | Words shown after the year number, e.g. "of the Third Age" |
| `…calendar.start` | yes | The date at time 0: `month` (0 is the first), `day` (1 is the first, within that month), `weekday` (0 is the first; 0 when there are no weekdays) |
| `bodies[].orbit` | no | The body's designed orbit around another body. Omitted for a body at the system's center. Every chain of parents must end at a body without an orbit (no missing parents, no loops). |
| `…orbit.parent` | yes | The `id` of the body it circles |
| `…orbit.distanceKm` | yes | The orbit's size (semi-major axis), center to center. Above 0, at most 10¹³. |
| `…orbit.periodDays` | yes | Standard days per trip around. Above 0, at most 10⁹. Set freely, not derived from physics. |
| `…orbit.startAngle` | yes | Degrees: where the body is at time 0 (see the orbit math below) |
| `…orbit.eccentricity` | no | How elongated: 0 (default, a circle) to 0.95 |
| `…orbit.closestApproach` | no | Degrees: the direction of the closest approach. Default 0. |
| `…orbit.tilt` | no | Degrees from the reference plane, 0 (default) to 180. Over 90 runs backwards. |
| `…orbit.tiltDirection` | no | Degrees: where the orbit rises north through the reference plane. Default 0. |
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
| `view` | no | Camera when saved. Omitted means the default view. |
| `view.latitude`, `longitude` | yes | Degrees; camera direction from the focus point |
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

Names written for enums (`kind`, `projection`) are fixed strings. They're not the code's enum
names, so renaming code never changes the format.

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
earlier years for negative *n*. Weekdays cycle the same way from `start.weekday`.

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
