# Decision Log

A record of key project decisions made by the owner. Newest first.

**Rule:** add an entry only after the change behind it has been committed to a PR or merged into
`main`. Decisions that are only discussed or approved are not logged here yet.

| Date | Decision | Status | PR / Commit | Notes |
|------|----------|--------|-------------|-------|
| 2026-09-30 | **Undo/redo** brought forward, before map warping; covers **all world edits** (pieces, map type, fill color, calibration, Import/Clear Map) | In PR | [#14](https://github.com/Phillip-Com/Nothic-Worlds/pull/14) | VISION.md `UI-03` |
| 2026-09-30 | Undo/redo through an **Edit menu** (names what will be undone) plus Ctrl+Z and Ctrl+Y / Ctrl+Shift+Z | In PR | [#14](https://github.com/Phillip-Com/Nothic-Worlds/pull/14) | |
| 2026-09-30 | The **Delete** key deletes the selected piece; deleting **no longer asks first** because it can be undone | In PR | [#14](https://github.com/Phillip-Com/Nothic-Worlds/pull/14) | |
| 2026-09-30 | Pieces are handled **like stamps in other map makers**: click to select, drag to move, corners resize, a handle rotates; number fields kept for exact values | Merged | [#13](https://github.com/Phillip-Com/Nothic-Worlds/pull/13) · `e79c89f` | Owner found typing numbers hard as the main control. VISION.md `MAP-02` |
| 2026-09-30 | Corner resizing **keeps proportions**; stretching one way is done by warping | Merged | [#13](https://github.com/Phillip-Com/Nothic-Worlds/pull/13) · `e79c89f` | |
| 2026-09-30 | **Warping**: drag every point of a piece's cut on the globe and the image stretches to follow | Merged | [#13](https://github.com/Phillip-Com/Nothic-Worlds/pull/13) · `e79c89f` | Recorded here; built in the next PR (world file format version 4) |
| 2026-09-30 | Map pieces are cut with a **rectangle and freeform outlines** | Merged | [#11](https://github.com/Phillip-Com/Nothic-Worlds/pull/11) · `d837931` | VISION.md `MAP-02` |
| 2026-09-30 | Pieces are moved, resized, and rotated **directly on the globe** | Merged | [#11](https://github.com/Phillip-Com/Nothic-Worlds/pull/11) · `d837931` | Built in the next PR |
| 2026-09-30 | Pieces can come from **any imported image**, all saved inside the world file | Merged | [#11](https://github.com/Phillip-Com/Nothic-Worlds/pull/11) · `d837931` | |
| 2026-09-30 | Up to a **few dozen** pieces, drawn live; limit **32 per planet** | Merged | [#11](https://github.com/Phillip-Com/Nothic-Worlds/pull/11) · `d837931` | Cost measured in the app PR |
| 2026-09-30 | World file **format version 3** adds map pieces; versions 1–2 upgrade automatically | Merged | [#11](https://github.com/Phillip-Com/Nothic-Worlds/pull/11) · `d837931` | docs/world-format.md version history |
| 2026-09-30 | Grid calibration approach: **guide lines** (drag latitude/longitude lines to where they really are) | Merged | [#9](https://github.com/Phillip-Com/Nothic-Worlds/pull/9) · `f10be33` | Chosen over pin points and a mesh grid. VISION.md `MAP-05` |
| 2026-09-30 | Calibration workspace: **side by side** (flat image + lines, live globe) | Merged | [#9](https://github.com/Phillip-Com/Nothic-Worlds/pull/9) · `f10be33` | Built in the next PR |
| 2026-09-30 | Calibration works for **all map types** | Merged | [#9](https://github.com/Phillip-Com/Nothic-Worlds/pull/9) · `f10be33` | One method: it adjusts which latitude/longitude each type reads |
| 2026-09-30 | Default guides: every **30° latitude** (60°S–60°N) and **60° longitude** | Merged | [#9](https://github.com/Phillip-Com/Nothic-Worlds/pull/9) · `f10be33` | |
| 2026-09-30 | World file **format version 2** adds map calibration; version 1 files upgrade automatically | Merged | [#9](https://github.com/Phillip-Com/Nothic-Worlds/pull/9) · `f10be33` | docs/world-format.md version history |
| 2026-09-30 | Worlds save as **one `.nworld` file** (zip: `world.json` + assets) | Merged | [#7](https://github.com/Phillip-Com/Nothic-Worlds/pull/7) · `411d7ad` | Spec: docs/world-format.md. VISION.md `SAV-01` |
| 2026-09-30 | The **original map image** is copied into the world file, unchanged | Merged | [#7](https://github.com/Phillip-Com/Nothic-Worlds/pull/7) · `411d7ad` | Can always be re-prepared at full quality |
| 2026-09-30 | The **camera view** is saved with the world | Merged | [#7](https://github.com/Phillip-Com/Nothic-Worlds/pull/7) · `411d7ad` | Reopen where you left off |
| 2026-09-30 | Saving: **manual (Ctrl+S) + safety net** (unsaved-changes warning, recovery copy every 5 min) | Merged | [#7](https://github.com/Phillip-Com/Nothic-Worlds/pull/7) · `411d7ad` | Recorded here; built in the next PR. VISION.md `SAV-02` |
| 2026-09-30 | Next after saving: map fitting tools (`MAP-05`, `MAP-02`) | Merged | [#7](https://github.com/Phillip-Com/Nothic-Worlds/pull/7) · `411d7ad` | Saving first so fitting work is never lost |
| 2026-09-30 | Pre-PR checklist adds a **repo-wide line-length check** (100 characters) | Merged | [#6](https://github.com/Phillip-Com/Nothic-Worlds/pull/6) · `8ca09ee` | `dotnet format` doesn't enforce it; 9 old violations slipped through. CLAUDE.md §9 |
| 2026-09-30 | Polar map: centered on the **north** pole, **equator** at the edge; the southern hemisphere uses the fill color | Merged | [#6](https://github.com/Phillip-Com/Nothic-Worlds/pull/6) · `8ca09ee` | VISION.md `MAP-04` |
| 2026-09-30 | Two hemispheres: **west left, east right**, split at 0°/180° | Merged | [#6](https://github.com/Phillip-Com/Nothic-Worlds/pull/6) · `8ca09ee` | VISION.md `MAP-04` |
| 2026-09-30 | Circular map types spread the map with **even spacing** (azimuthal equidistant) | Merged | [#6](https://github.com/Phillip-Com/Nothic-Worlds/pull/6) · `8ca09ee` | Rim isn't squished. VISION.md `MAP-04` |
| 2026-09-30 | Hand-drawn maps: add a **Flat map** mode (Mercator, shapes kept as drawn) alongside **Globe map** (equirectangular) | Merged | [#5](https://github.com/Phillip-Com/Nothic-Worlds/pull/5) · `fe8db9a` | Chosen over latitude coverage, which only reduces pinching. VISION.md `MAP-03` |
| 2026-09-30 | Map type is chosen with a toolbar dropdown, changeable any time; new imports default to **Flat map** | Merged | [#5](https://github.com/Phillip-Com/Nothic-Worlds/pull/5) · `fe8db9a` | VISION.md `MAP-03` |
| 2026-09-30 | Flat map polar caps: filled with a **user-picked Pole color**, softly blended at the map edge | Merged | [#5](https://github.com/Phillip-Com/Nothic-Worlds/pull/5) · `fe8db9a` | Replaced the first choice (stretch the map's edges) after the owner saw the streaks on their map. VISION.md `MAP-03` |
| 2026-09-30 | More map types (**MAP-04**): Robinson, Winkel tripel, Mollweide, Polar (azimuthal), Two hemispheres, Gall–Peters | Merged | [#5](https://github.com/Phillip-Com/Nothic-Worlds/pull/5) · `fe8db9a` | Recorded in VISION.md here; built in the next PR |
| 2026-09-30 | Milestone 2: map fixes (`MAP-03`) first, then the world save format | Merged | [#5](https://github.com/Phillip-Com/Nothic-Worlds/pull/5) · `fe8db9a` | VISION.md §3 |
| 2026-09-29 | Imported maps are compressed with **S3TC** on the GPU | Merged | [#4](https://github.com/Phillip-Com/Nothic-Worlds/pull/4) · `0f7af7c` | 8k map: ~137 MB video memory instead of ~497 MB, +~2 s load, slight blockiness. An uncompressed "high quality" option is in the Idea Inbox (Advanced tier). VISION.md `MAP-01` |
| 2026-09-29 | Map size limit **8192 × 4096**; larger images are shrunk, keeping their proportions | Merged | [#4](https://github.com/Phillip-Com/Nothic-Worlds/pull/4) · `0f7af7c` | VISION.md `MAP-01` |
| 2026-09-29 | Maps that aren't 2:1 are **applied with a warning**, not rejected | Merged | [#4](https://github.com/Phillip-Com/Nothic-Worlds/pull/4) · `0f7af7c` | VISION.md `MAP-01` |
| 2026-09-29 | Map UI: top-left toolbar with Import Map… and Clear Map; grid hidden on maps, **G** toggles it | Merged | [#4](https://github.com/Phillip-Com/Nothic-Worlds/pull/4) · `0f7af7c` | VISION.md `MAP-01` |
| 2026-09-29 | Camera: **separate controls**, with left-drag to orbit and right-drag to pan. Pan **auto-switches by zoom**: it slides the whole view when zoomed out and slides across the surface when close. An on-screen indicator shows orbiting/panning and the pan mode. | Merged | [#3](https://github.com/Phillip-Com/Nothic-Worlds/pull/3) · `1a9c203` | Revised after the owner tried the first version (pan felt the same as orbit). VISION.md `REN-02` |
| 2026-09-29 | Milestone 1 split into two PRs: A = planet + camera + performance tools, B = map import | Merged | [#3](https://github.com/Phillip-Com/Nothic-Worlds/pull/3) · `1a9c203` | VISION.md §3 |
| 2026-09-29 | Milestone 1 does not save; the world file format gets its own design review | Merged | [#3](https://github.com/Phillip-Com/Nothic-Worlds/pull/3) · `1a9c203` | VISION.md §3 |
| 2026-09-29 | Merged status is updated by the first commit of the next branch, not by a separate PR | Merged | [#2](https://github.com/Phillip-Com/Nothic-Worlds/pull/2) · `e0753c8` | CLAUDE.md §10 |
| 2026-09-29 | Godot renderer: **Mobile** | Merged | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) · `8f783ae` | Lighter than Forward+ but keeps compute shaders. Verify performance on the baseline laptop in M1. CLAUDE.md §9 |
| 2026-09-29 | Project structure: `src/` Core library, `tests/`, `godot/`; all projects target net8.0 | Merged | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) · `8f783ae` | CLAUDE.md §7, §9 |
| 2026-09-29 | Tech stack: **Godot 4.7.2 (.NET) + C#**, xUnit, `dotnet format` | Merged | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) · `8f783ae` | Chosen over a TypeScript/Three.js web stack. CLAUDE.md §9 |
| 2026-09-29 | Architecture: world data and simulation live in an engine-independent Core library, with a versioned save format | Merged | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) · `8f783ae` | Keeps a future Roll20-style hosted system (`SHR-01`) possible without an overhaul |
| 2026-09-29 | Performance tiers: `Base` must run on the baseline laptop; heavier features are opt-in `Advanced` | Merged | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) · `8f783ae` | Baseline: Ryzen 7 3700U / Vega 10 / ~7 GB RAM. VISION.md §2 |
| 2026-09-29 | Milestone 1: a single planet with map image import and orbit/zoom/pan camera | Merged | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) · `8f783ae` | VISION.md §3 |
| 2026-09-29 | Project documents: CLAUDE.md (rules), VISION.md (intent + implementation), DECISIONS.md (this log) | Merged | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) · `8f783ae` | |
| 2026-09-29 | Workflow: Evaluate → Check against the docs → Owner review → Build | Merged | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) · `8f783ae` | CLAUDE.md §2 |
| 2026-09-29 | Git: feature branches + PRs; the owner merges | Merged | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) · `8f783ae` | One-time exception: an empty initial commit on `main` so PRs have a base |
