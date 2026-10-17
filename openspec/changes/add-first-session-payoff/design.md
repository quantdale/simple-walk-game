# Design

## Math (integer only)
- `percent = clamp(invested * 100 / cost, 1, 99)` when `0 < invested < cost`; null otherwise.
- `stage = min(invested * S / cost, S - 1)` (identical to `BuildStageFor`).
- If `stage + 1 < S`: next threshold `ceil((stage + 1) * cost / S)`, `NextIsBuildStage = true`; else threshold `cost`.
- `StepsToNext = (threshold - invested) * 100`. "About" because the per-provider carry (< 100 steps, D-079) is ignored.

Pinned by `FirstSessionPayoffTests.Next_stage_threshold_matches_domain_build_stage_rule` (every invested value of a 3-stage project).

## Surfaces
Derived in `GlanceProjector` from `HomeReadModel`; never persisted, read-only (calling it twice yields the same text and no save write). The shell adds no literals (D-120 ratchet): it renders the glance sentence.
