# Design notes (proposal stage)

Reuse, do not duplicate:
- `RegionDefinition`, `ContentValidator`, `StoryChapterDefinition`, `StoryProgress.Evaluate`, `GameSession.GetStory()`, `ShellController` Story screen.
- New: `WorldDefinition` (ordered regions + unlock rule) in Domain; `GameState.CurrentRegionId` and per-region progress in canonical state (schema change, migration from v-current: every existing save is Region 1).
- Layering per `scripts/verify-architecture.sh`: catalogs and rules in Domain, orchestration in Application, no Infrastructure or Unity knowledge of region ids beyond read models.

Open questions for the decision gate:
1. Is Region 2 the right next campaign at all, versus deeper ecology, richer discoveries or cloud backup (ROADMAP list)?
2. Does closure of Region 1 "move" the player, or does Region 2 run alongside Region 1 care?
3. Art budget: the procedural valley picture is authored for Region 1; Region 2 needs its own landmark silhouettes.
