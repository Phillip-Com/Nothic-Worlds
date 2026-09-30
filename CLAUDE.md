# Nothic-Worlds — Project Guidelines

This file is the source of truth for how code is written in this project. Claude reads it at the
start of every session and must check every task against it. If a request conflicts with this
file, stop and raise the conflict instead of guessing.

**Roles:** Claude writes almost all of the code. The owner (Phillip) reviews it and makes the key
design and feature decisions. Claude proposes and the owner decides.

---

## 1. Project Overview

- **What it is:** A worldbuilding tool centered on designing and simulating fictional star
  systems in 3D. It covers suns, moons, planets, and unusual bodies, from system scale down to local
  regions on a planet. Calendars, seasons, weather, and celestial events come from the simulation.
  The full vision and feature list is in [docs/VISION.md](docs/VISION.md).
- **Users:** Single user (the owner) for now. It may later be released publicly or sold, so
  write code that can grow into that: clean data model, no hard-coded personal paths,
  no shortcuts that block multi-user or distribution later.
- **Platform:** Desktop app built with Godot 4 + C# (see Section 9).

### Project Documents

| File | Purpose |
|------|---------|
| `CLAUDE.md` (this file) | How code is written: rules, workflow, standards |
| [docs/VISION.md](docs/VISION.md) | What we're building: goals, feature intent (with IDs), and how each feature was implemented |
| [docs/DECISIONS.md](docs/DECISIONS.md) | Key decisions, logged once committed to a PR or merged |

---

## 2. Workflow: Evaluate → Check → Review → Build

Every non-trivial task follows these steps, in order:

1. **Evaluate.** Understand the request and read the relevant existing code. Identify which files,
   data structures, and features it touches.
   - Find the matching feature ID(s) in [docs/VISION.md](docs/VISION.md) and read their
     **Intent**. That is the definition of what the feature is meant to do.
   - **Check for reuse.** Search VISION.md's **Implementation** notes and the codebase for
     anything that already does something similar. Prefer extending or reusing it over building
     something new. If reuse isn't practical, explain why in the review.
2. **Check against this file.** Compare the planned work to the restrictions, requirements, and
   formatting rules below. Note any conflicts, and any decisions this file doesn't cover.
   Also check the work against the feature's intent in VISION.md and past decisions in
   DECISIONS.md.
3. **Bring it to the owner for review.** Before writing significant code, present:
   - A short summary of the plan (what changes and why)
   - Any conflicts with this file, or gaps in it
   - Decisions the owner needs to make, each with options and a recommendation
   - Risks and anything that would be hard to undo
4. **Build.** After approval, implement on a feature branch (Section 3), test it, and open a PR.
5. **Update the project documents.**
   - **VISION.md:** in the same PR, update the feature's **Status** and fill in its
     **Implementation** notes: the approach, the key files/modules, and what can be reused. New ideas
     from the owner go in the Idea Inbox. Never change an entry's **Intent** without the owner's
     approval.
   - **This file:** if the owner makes a decision that should apply going forward, update the
     relevant section.
   - **DECISIONS.md:** log it under the rules in Section 10.

**Always ask the owner before:**
- Choosing or changing architecture, the tech stack, or the data model/file format
- Adding any third-party dependency
- Adding, removing, or changing a user-facing feature or behavior
- Deleting or rewriting significant existing code
- Anything destructive or hard to reverse (data migrations, force-pushes, deleting files)

**Claude may decide on its own:** small implementation details inside an approved plan, such as
local variable names, helper functions, and internal refactors that don't change behavior. Mention
them in the PR description.

**Trivial tasks** (typo fixes, a one-line bug fix with an obvious cause) can skip step 3, but still
go through a branch and PR.

---

## 3. Git Workflow

- `main` is always working and stable. Never commit directly to `main`.
- Each task gets its own branch: `feature/<short-name>`, `fix/<short-name>`, `docs/<short-name>`,
  or `refactor/<short-name>`.
- Keep commits small and focused. Each commit should do one thing and leave the project runnable.
- Commit messages: a short imperative summary line (≤ 72 chars, e.g. "Add timeline event model"),
  then a blank line, then details if needed.
- Every PR description includes:
  - **What** changed and **why**
  - **How to test it** (steps the owner can follow)
  - **Decisions made** by Claude that the owner should know about
  - **Open questions** or follow-ups
- The owner reviews and merges. Claude never merges its own PRs or force-pushes shared branches.
- Never commit secrets, API keys, personal data, or large generated/binary files.

---

## 4. Code Quality: General Best Practices

**Readability first.** The owner reviews all code and is comfortable in several languages, but
code should be easy to follow for anyone.
- Clear, descriptive names (`relationshipType`, not `rt`). Avoid clever tricks when plain code works.
- Keep functions small, with one job each. If a function needs a comment explaining *what* each
  section does, split it up.
- Comments explain *why*, not *what*. Document non-obvious decisions, workarounds, and domain rules.
- Every public function, class, and module gets a short doc comment that says what it does and
  what it expects.

**Design**
- Separate concerns. Keep the data model and storage, the business/domain logic, and the UI
  in separate layers, and don't let UI code talk to storage directly.
- Don't Repeat Yourself, but don't abstract too early either. Extract shared code when a pattern
  appears for the third time.
- YAGNI: build what is needed now. No speculative features or options that nobody asked for.
- Prefer simple, well-understood solutions over clever or trendy ones.

**Errors and data safety**
- Worlds are the user's creative work, and **losing user data is the worst possible bug.**
  - Never silently discard or overwrite data.
  - Saves must be atomic: write to a temp file or transaction, then commit.
  - Handle errors explicitly and show clear messages. Never swallow exceptions.
  - Plan migrations for any change to the saved data format, and version the format.
- Validate all input at the boundaries (user input, file loading, imports).

**Performance** (a core goal: look good on modest hardware)
- Treat performance as a design requirement from the start, not something to fix later.
- 3D features must degrade gracefully, using level-of-detail, quality settings, and skipping
  work for things that are off-screen or too small to see.
- Keep heavy computation (physics, weather, sculpting) off the rendering path, and don't
  recompute what hasn't changed.
- When a design choice affects system requirements, call it out in the review.
- Every feature has a tier in VISION.md. `Base` features must run on the baseline laptop
  (VISION.md Section 2). A feature that needs more power goes in the opt-in `Advanced` tier, and
  only with the owner's approval. `Base` features must never depend on `Advanced` ones.

**Dependencies**
- Add a dependency only with the owner's approval. Prefer mature, well-maintained libraries with
  licenses compatible with a future commercial release (MIT, Apache-2.0, BSD). No GPL-only
  libraries without discussing it first.
- Pin versions (NuGet lock files).

**Security**
- No secrets in code. Use environment variables or config files that are excluded from git.
- Treat all imported files as untrusted input.

---

## 5. Testing

- New logic comes with tests. Bug fixes come with a test that reproduces the bug.
- Test priorities, from highest to lowest:
  1. Data model and save/load (round-trip tests: save → load → identical)
  2. Simulation and domain logic (orbits, time/calendar math, event detection). These must be
     deterministic: the same world at the same time always gives the same result.
  3. UI (key flows only)
- All tests must pass before a PR is opened. Run the test suite and report the results in the PR,
  including any failures.
- Tools: see Section 9. Core logic must be testable without launching Godot.

---

## 6. Formatting and Style

- Run the formatter and linter (`dotnet format`, see Section 9) before every commit.
- Indentation: 4 spaces, no tabs.
- Line length: 100 characters max.
- Naming follows the C# conventions in Section 9. Folders in `docs/` use `kebab-case`.
- End every file with a newline. No trailing whitespace.
- Line endings: LF in the repo (enforced with `.gitattributes`), even on Windows.

---

## 7. Project Structure

```
NothicWorlds.sln               Solution tying all C# projects together
Directory.Build.props          Shared C# build settings (nullable, style checks, lock files)
global.json                    Pins the .NET SDK version
.editorconfig                  Formatting and naming rules
src/NothicWorlds.Core/         Plain C# library, NO Godot references
  Geometry/                    Shared math: coordinates on spheres, conversions
  Maps/                        Map image rules (layout, size limits)
  Model/                       World data: bodies, maps, journals, calendars
  Simulation/                  Orbits, time, events, weather logic
  Storage/                     Save/load, format versioning, migrations
tests/NothicWorlds.Core.Tests/ xUnit tests, mirroring Core's folders
godot/                         The Godot project (references Core)
  Scenes/                      Scene files (.tscn) and their root scripts
  Rendering/                   Shaders and drawing; reads simulation state, never owns it
  UI/                          Panels, tools, pop-ups
  Controls/                    Input actions, camera and tool controls
  Maps/                        Loading and preparing map images (background thread)
  Diagnostics/                 Performance overlay (F3) and benchmark
  Interop/                     Conversions between Core types and Godot types
docs/                          VISION.md, DECISIONS.md, design notes
```

(The controls folder is named `Controls/`, not `Input/`. A namespace called `NothicWorlds.Input`
would hide Godot's `Input` class.)

- Subfolders are created when the first file needs them. Don't add empty placeholder folders.
- Dependencies only point one way: `godot/` → `Core`, never the reverse.
- Godot's generated `.uid` files are committed. The `.godot/` cache folder is not.

---

## 8. Communication with the Owner

- Explain in plain language. Define technical terms the first time they come up.
- When presenting options, give 2–4 choices, note the trade-offs of each, and give a clear
  recommendation.
- Report outcomes honestly. If something is untested, broken, or skipped, say so.
- Link to specific files and lines when referring to code.

---

## 9. Tech Stack

**Status:** Approved by the owner.

- **Engine:** Godot **4.7.2**, .NET edition. Chosen for its free MIT license (no royalties
  if the app is sold), its lightweight renderer that runs on integrated graphics, and its
  built-in 3D, UI, and networking.
- **Renderer:** Godot's **Mobile** renderer. It's lighter than Forward+ and still supports the compute
  shaders that sculpting and weather will likely need. Verified on the baseline laptop in
  Milestone 1 (see `REN-03` in VISION.md).
- **Language:** C# (latest language version) with nullable reference types enabled.
- **.NET:** SDK 9 (pinned in `global.json`). All projects target **net8.0** to match Godot's
  .NET runtime. Core must never target a newer framework than the Godot project.
- **Platform:** desktop (Windows first). Note: Godot 4 C# projects can't currently export to the
  web. That's acceptable because player sharing (`SHR-01`) is a future feature. See the
  architecture rule below.
- **Testing:** xUnit for the core library. Godot-side (rendering/UI) changes are verified with
  manual test steps in the PR, until a Godot test tool is approved.
- **Formatting/linting:** `dotnet format` driven by an `.editorconfig` in the repo root.

**Architecture rule: keep the engine at the edge.**
- All world data and simulation logic (bodies, orbits, time, calendar, events, journals) lives in
  a **plain C# library with no Godot references**. Godot code only draws that state, handles
  input, and shows UI.
- The saved world format is **engine-independent, documented, and versioned**. Any future
  viewer (a Godot app, a web viewer, or a server for hosted games) must be able to read it
  without Godot.
- This keeps a future Roll20-style hosted system possible without an overhaul. A server can reuse
  the core library, and at worst only a player-facing viewer would need to be written.

**C# naming conventions**
- `PascalCase`: types, methods, properties, constants, public fields, file names (a file is named
  after the main type it contains, e.g. `OrbitCalculator.cs`)
- `camelCase`: local variables and parameters
- `_camelCase`: private fields
- Interfaces start with `I` (`IWorldStore`)
- Godot scenes and resources (`.tscn`, `.tres`) use `snake_case` file names, following Godot convention
- One public type per file. Use file-scoped namespaces.

**How to build, run, and test** (run from the repo root):

| Task | Command |
|------|---------|
| Build everything | `dotnet build NothicWorlds.sln` |
| Run tests | `dotnet test NothicWorlds.sln` |
| Check formatting | `dotnet format NothicWorlds.sln --verify-no-changes` |
| Run the app headless (smoke test) | `godot --headless --path godot --quit-after 30` |
| Performance benchmark (opens fullscreen ~12 s) | `godot --path godot --fullscreen -- --benchmark` |

Before opening a PR, the first four must succeed: the build has no errors, the tests pass, the
formatting check makes no changes, and the headless run starts without errors. For PRs that
affect rendering, also run the benchmark and report its numbers in the PR. Compare them with the
baseline recorded under `REN-03` in VISION.md, and flag any drop. Other apps using the GPU
(especially an open Godot editor) skew the results. If a drop shows up, benchmark the previous
commit under the same conditions before blaming the change. On this machine
Godot is installed via winget. If the `godot` command isn't on PATH, use the full path to
`Godot_v4.7.2-stable_mono_win64_console.exe` under
`%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_*`.

---

## 10. Decision Log

Key decisions are recorded in [docs/DECISIONS.md](docs/DECISIONS.md), not in this file.

- Add an entry **only** after the change behind it has been committed to a PR or merged into
  `main`. Decisions that are only discussed or approved are not logged yet.
- Each entry links the PR (and the commit, if merged) that carries the decision.
- New entries start with status **In PR**. Don't open a separate PR just to mark entries as
  merged. Instead, the **first commit of the next branch** changes the previous PR's entries to
  **Merged** and adds the merge commit.
- Read the log before starting any task that touches an area with past decisions.
