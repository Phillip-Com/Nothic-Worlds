# Nothic-Worlds — Project Guidelines

The source of truth for how code is written in this project. Check every task against it. If a
request conflicts with this file, stop and raise the conflict instead of guessing.

**Roles:** Claude writes almost all of the code and proposes; the owner (Phillip) reviews it and
makes the key design and feature decisions.

## 1. Project

A desktop worldbuilding tool (Godot 4 + C#) for designing and simulating fictional star systems
in 3D, from whole systems down to local regions on a planet. Calendars, seasons, weather, and
celestial events come from the simulation. Single user for now, possibly sold later: keep the
data model clean, no hard-coded personal paths, nothing that blocks multi-user or distribution.

| File | Purpose |
|------|---------|
| `CLAUDE.md` | Rules, workflow, standards |
| [docs/VISION.md](docs/VISION.md) | Goals; each feature's Intent (with IDs), owner's choices, key files |
| [docs/DECISIONS.md](docs/DECISIONS.md) | Key decisions, logged once committed to a PR or merged |
| [docs/world-format.md](docs/world-format.md) | The `.nworld` save format. Update it with any format change |
| [docs/development.md](docs/development.md) | Commands, pre-PR checks, benchmarks, export, repo layout |

## 2. Workflow: Evaluate → Check → Review → Build

1. **Evaluate.** Read the relevant code. Find the feature ID(s) in VISION.md and read their
   **Intent**; search its **Built** notes and the code for something to reuse or extend
   (explain in the review if reuse isn't practical). Read only the VISION sections you need.
2. **Check** the plan against this file, the feature's intent, and DECISIONS.md.
3. **Review with the owner** before significant code: the plan, any conflicts or gaps, decisions
   to make (each with options and a recommendation), and risks or anything hard to undo.
4. **Build** on a feature branch after approval, test, and open a PR.
5. **Update the docs** in the same PR: the feature's **Status** and short **Built** notes in
   VISION.md (key files, reuse, limits; measurements belong in the PR, not VISION); new owner
   ideas in its Idea Inbox. Never change an **Intent** without the owner's approval. Update this
   file when the owner makes a lasting decision; log decisions per Section 9.

**Always ask the owner before:** architecture, tech stack, or data model/file format changes;
adding a dependency; adding, removing, or changing a user-facing feature or behavior; deleting
or rewriting significant code; anything destructive or hard to reverse (migrations,
force-pushes, deleting files).

**Claude may decide:** small implementation details inside an approved plan (names, helpers,
internal refactors that don't change behavior). Mention them in the PR.

**Trivial tasks** (a typo, a one-line fix with an obvious cause) skip step 3 but still get a
branch and PR.

## 3. Git

- `main` is always working. Never commit to it directly.
- One branch per task: `feature/`, `fix/`, `docs/`, or `refactor/<short-name>`, **based on
  `main`**. Don't stack a PR on another PR's branch: it merges into that branch, not `main`.
- Small focused commits that leave the project runnable. Messages: an imperative summary line
  (≤ 72 chars), a blank line, then details if needed.
- PR descriptions: **What** and **why**, **How to test**, **Decisions made** by Claude, **Open
  questions**. Keep them short.
- The owner merges. Claude never merges its own PRs or force-pushes shared branches.
- Never commit secrets, personal data, or large generated/binary files. Exception (owner,
  2026-10-09): CC0 art for the standing view (photo textures at 1K, low-poly models) in
  `godot/Assets/`, kept small and credited in a `CREDITS.md` beside them.

## 4. Code Quality

**Readability:** clear names (`relationshipType`, not `rt`), small single-purpose functions,
comments that explain *why*, a short doc comment on every public function, class, and module.

**Design:** keep data/storage, domain logic, and UI in separate layers (UI never talks to
storage). Extract shared code on the third repeat, not before. YAGNI. Prefer simple,
well-understood solutions.

**Usability** (owner's rule, M34: anyone new can pick up every tool):
- Every tool says in one line how to use it: `MapToolbar.SetHint` while it's open.
- A tool never just does nothing: a disabled button gets its reason as a tooltip
  (`DisabledTip.Apply`); an action that must wait or is refused shows a message (e.g.
  `MapToolbar.ShowBusyWarning`).

**Data safety** (losing user data is the worst possible bug):
- Never silently discard or overwrite data. Saves are atomic (temp file, then commit).
- Handle errors explicitly with clear messages; never swallow exceptions.
- Version the save format and plan a migration for every change.
- Validate all input at the boundaries; treat imported files as untrusted.

**Performance** (a core goal: good on modest hardware):
- A design requirement from the start. 3D degrades gracefully (level of detail, quality
  settings, skipping off-screen or tiny things). Heavy work (physics, weather, sculpting) stays
  off the rendering path; don't recompute what hasn't changed.
- Every feature has a tier in VISION.md. `Base` must run on the baseline laptop (VISION.md §2);
  more demanding features go in the opt-in `Advanced` tier only with the owner's approval.
  `Base` never depends on `Advanced`. Call out choices that affect system requirements.

**Dependencies:** only with the owner's approval; mature, maintained, licenses fit for a
commercial release (MIT, Apache-2.0, BSD; no GPL-only without discussion). Pin versions (NuGet
lock files).

**Security:** no secrets in code; use environment variables or git-ignored config.

## 5. Testing

- New logic comes with tests; a bug fix comes with a test that reproduces the bug.
- Priorities: (1) data model and save/load round trips, (2) simulation and domain logic, which
  must be deterministic (same world, same time, same result), (3) key UI flows.
- Core logic must be testable without Godot (xUnit). Godot-side changes are verified with
  manual test steps in the PR.
- All checks pass before a PR is opened (docs/development.md, "Checks before a PR"); report
  the results in the PR, including any failures.

## 6. Formatting

- Run `dotnet format` before every commit. 4 spaces, no tabs; lines ≤ 100 characters; a final
  newline and no trailing whitespace; LF line endings (`.gitattributes`).
- C#: `PascalCase` for types, methods, properties, constants, public fields, and file names (a
  file is named after its main type); `camelCase` locals and parameters; `_camelCase` private
  fields; interfaces start with `I`. One public type per file; file-scoped namespaces.
- Godot scenes and resources (`.tscn`, `.tres`) use `snake_case`; folders in `docs/` use
  `kebab-case`.

## 7. Architecture

- **Keep the engine at the edge.** All world data and simulation logic lives in
  `src/NothicWorlds.Core`, a plain C# library with **no Godot references**. Godot code only
  draws that state, handles input, and shows UI. Dependencies point one way: `godot/` → Core.
- The save format is **engine-independent, documented, and versioned**, so a future web viewer
  or server for hosted games can read it without Godot.
- All edits go through `WorldSession`. Rendering reads simulation state, never owns it.
- Create subfolders only when the first file needs them. Commit Godot's `.uid` files, not the
  `.godot/` cache. The full layout is in docs/development.md.

## 8. Communication

- Plain language; define technical terms the first time. Options: 2–4, with trade-offs and a
  clear recommendation. Report outcomes honestly (untested, broken, or skipped). Link to files
  and lines.
- **Keep token use low** (owner's request, 2026-10-08): filter command output to what matters;
  read only the parts of large files that are needed; run the full checks once before a PR, and
  the benchmarks only when drawing code changed; agree up front how deep a measurement task
  should go. When a PR has merged, suggest starting a fresh session for the next task.

## 9. Decision Log

Key decisions go in [docs/DECISIONS.md](docs/DECISIONS.md), not here.

- Add an entry **only** once its change is committed to a PR or merged. Each links its PR (and
  merge commit once merged).
- New entries start as **In PR**. Don't open a PR just to mark entries merged: the **first
  commit of the next branch** marks the previous PR's entries **Merged** with the merge commit.
- Read the log before starting a task in an area with past decisions.
