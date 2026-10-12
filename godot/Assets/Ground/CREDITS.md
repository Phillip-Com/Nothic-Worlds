# Ground materials

Photo materials for the ground while standing (VISION.md `REN-06`; `Rendering/GroundMaterials.cs`).
All are **CC0 1.0** (public domain): free for any use, including commercial, with no attribution
required. They are credited here anyway. Each was downloaded at 1K (1024 × 1024), its color and
OpenGL-style normal maps only, and saved again as JPEG at quality 85.

| File | Source | Author | Link |
|------|--------|--------|------|
| `grass_*` | ambientCG Grass004 | Lennart Demes | https://ambientcg.com/view?id=Grass004 |
| `dry_grass_*` | Poly Haven grass_ground | Charlotte Baglioni | https://polyhaven.com/a/grass_ground |
| `forest_floor_*` | Poly Haven forrest_ground_01 | Rob Tuytel | https://polyhaven.com/a/forrest_ground_01 |
| `sand_*` | ambientCG Ground080 | Lennart Demes | https://ambientcg.com/view?id=Ground080 |
| `mud_*` | Poly Haven brown_mud | Rob Tuytel | https://polyhaven.com/a/brown_mud |
| `rock_*` | ambientCG Rock060 | Lennart Demes | https://ambientcg.com/view?id=Rock060 |
| `snow_*` | ambientCG Snow010A | Lennart Demes | https://ambientcg.com/view?id=Snow010A |
| `gravel_*` | ambientCG Ground110 | Lennart Demes | https://ambientcg.com/view?id=Ground110 |
| `paving_*` | ambientCG PavingStones054 | Lennart Demes | https://ambientcg.com/view?id=PavingStones054 |
| `planks_*` | ambientCG Planks039 | Lennart Demes | https://ambientcg.com/view?id=Planks039 |

The paving (the stone highways, VISION.md `BOD-13`) comes at 683 × 1024; it is laid three across
and two down, so its stones keep their shape, and scaled to 1024 × 1024.

The planks (the dirt paths' wooden bridges and boardwalks, `BOD-13`) come at 1024 × 512; they
are laid twice, one over the other, to make 1024 × 1024. The dirt paths' worn ground reuses the
mud, made drier, finer, and lighter in the shader (owner's choice, 2026-10-12).

Licenses: https://docs.ambientcg.com/license/ and https://polyhaven.com/license.

To replace one, keep its file names, size (1024 × 1024), and import settings (VRAM compressed,
mipmaps; normal maps marked as normal maps), so the texture arrays still build.
