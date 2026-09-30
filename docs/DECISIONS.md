# Decision Log

A record of key project decisions made by the owner. Newest first.

**Rule:** add an entry only after the change behind it has been committed to a PR or merged into
`main`. Decisions that are only discussed or approved are not logged here yet.

| Date | Decision | Status | PR / Commit | Notes |
|------|----------|--------|-------------|-------|
| 2026-09-29 | Godot renderer: **Mobile** | In PR | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) | Lighter than Forward+ but keeps compute shaders. Verify performance on the baseline laptop in M1. CLAUDE.md §9 |
| 2026-09-29 | Project structure: `src/` Core library, `tests/`, `godot/`; all projects target net8.0 | In PR | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) | CLAUDE.md §7, §9 |
| 2026-09-29 | Tech stack: **Godot 4.7.2 (.NET) + C#**, xUnit, `dotnet format` | In PR | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) | Chosen over a TypeScript/Three.js web stack. CLAUDE.md §9 |
| 2026-09-29 | Architecture: world data and simulation live in an engine-independent Core library, with a versioned save format | In PR | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) | Keeps a future Roll20-style hosted system (`SHR-01`) possible without an overhaul |
| 2026-09-29 | Performance tiers: `Base` must run on the baseline laptop; heavier features are opt-in `Advanced` | In PR | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) | Baseline: Ryzen 7 3700U / Vega 10 / ~7 GB RAM. VISION.md §2 |
| 2026-09-29 | Milestone 1: a single planet with map image import and orbit/zoom/pan camera | In PR | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) | VISION.md §3 |
| 2026-09-29 | Project documents: CLAUDE.md (rules), VISION.md (intent + implementation), DECISIONS.md (this log) | In PR | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) | |
| 2026-09-29 | Workflow: Evaluate → Check against the docs → Owner review → Build | In PR | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) | CLAUDE.md §2 |
| 2026-09-29 | Git: feature branches + PRs; the owner merges | In PR | [#1](https://github.com/Phillip-Com/Nothic-Worlds/pull/1) | One-time exception: an empty initial commit on `main` so PRs have a base |
