# World File Format (`.nworld`)

This is the specification for Nothic Worlds save files. It's engine-independent: anything that
can read a zip file and JSON can read a world, without Godot (CLAUDE.md §9). Code:
`src/NothicWorlds.Core/Storage/` (`WorldPackage` reads and writes it).

**Current format version: 2** (see **Version history** at the end)

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

## `world.json` (version 2)

```json
{
  "formatVersion": 2,
  "id": "11111111-2222-3333-4444-555555555555",
  "name": "Aerth",
  "createdUtc": "2026-09-30T12:00:00+00:00",
  "modifiedUtc": "2026-09-30T13:30:00+00:00",
  "bodies": [
    {
      "id": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
      "name": "Aerth",
      "kind": "planet",
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
        "fillColor": "#112233"
      }
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
| `bodies` | yes, 1 or more | Celestial bodies. Version 1 apps create exactly one planet. |
| `bodies[].id` | yes | GUID |
| `bodies[].name` | yes | Not empty |
| `bodies[].kind` | yes | `"planet"` |
| `bodies[].surface.map` | no | Omitted when the planet has no map |
| `…map.asset` | yes | Asset entry name (see Container) |
| `…map.projection` | yes | Map type: `equirectangular` (Globe map), `mercator` (Flat map), `robinson`, `winkel-tripel`, `mollweide`, `gall-peters`, `polar`, `two-hemispheres` |
| `…map.calibration` | no | Grid calibration (`MAP-05`). Omitted means the map is read exactly as its type says. |
| `…calibration.latitudes` | yes | Guides, south to north: `latitude` (true, between -90 and 90) is drawn where the map type puts `drawnAs`. Both increase strictly; the poles are fixed and not listed. |
| `…calibration.longitudes` | yes | Guides, west to east: `longitude` in [-180, 180) is drawn at `drawnAs`, less than half a turn away. `drawnAs` increases strictly, and may run past ±180 so the order holds around the globe (the first + 360 must exceed the last). |
| `bodies[].surface.fillColor` | yes | `#RRGGBB`. Color where the map doesn't cover the globe |
| `view` | no | Camera when saved. Omitted means the default view. |
| `view.latitude`, `longitude` | yes | Degrees; camera direction from the focus point |
| `view.altitude` | yes | In planet radii above the surface |
| `view.focusOffset` | no | `[x, y, z]` view-pan offset from the planet's center; default `[0, 0, 0]` |

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
