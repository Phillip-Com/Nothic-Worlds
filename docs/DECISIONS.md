# Decision Log

A record of key project decisions made by the owner. Newest first.

**Rule:** add an entry only after the change behind it has been committed to a PR or merged into
`main`. Decisions that are only discussed or approved are not logged here yet.

| Date | Decision | Status | PR / Commit | Notes |
|------|----------|--------|-------------|-------|
| 2026-09-30 | Pre-PR checklist adds a **repo-wide line-length check** (100 characters) | In PR | [#6](https://github.com/Phillip-Com/Nothic-Worlds/pull/6) | `dotnet format` doesn't enforce it; 9 old violations slipped through. CLAUDE.md §9 |
| 2026-09-30 | Polar map: centered on the **north** pole, **equator** at the edge; the southern hemisphere uses the fill color | In PR | [#6](https://github.com/Phillip-Com/Nothic-Worlds/pull/6) | VISION.md `MAP-04` |
| 2026-09-30 | Two hemispheres: **west left, east right**, split at 0°/180° | In PR | [#6](https://github.com/Phillip-Com/Nothic-Worlds/pull/6) | VISION.md `MAP-04` |
| 2026-09-30 | Circular map types spread the map with **even spacing** (azimuthal equidistant) | In PR | [#6](https://github.com/Phillip-Com/Nothic-Worlds/pull/6) | Rim isn't squished. VISION.md `MAP-04` |
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
