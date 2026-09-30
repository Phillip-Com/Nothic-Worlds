# World File Format (`.nworld`)

This is the specification for Nothic Worlds save files. It's engine-independent: anything that
can read a zip file and JSON can read a world, without Godot (CLAUDE.md §9). Code:
`src/NothicWorlds.Core/Storage/` (`WorldPackage` reads and writes it).

**Current format version: 1**

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

## `world.json` (version 1)

```json
{
  "formatVersion": 1,
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
          "projection": "winkel-tripel"
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
  and adding a golden-file test for the new version. The version 1 golden test
  (`WorldPackageTests`) must keep passing forever.

## Safe saving

1. Write the complete new file next to the target as `<name>.nworld.saving`, and flush it to disk.
2. **Read it back** fully with the normal loader. If that fails, delete it and report the error.
3. Swap it in atomically. The previous save is kept as `<name>.nworld.bak`.

If anything fails, the existing world file is left untouched.
