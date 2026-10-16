## 1. Design
- [x] 1.1 OpenSpec change and D-076

## 2. Domain and content
- [x] 2.1 `StoryChapterDefinition`, `RegionDefinition.Chapters`, `StoryProgress.Evaluate` (pure)
- [x] 2.2 ContentValidator rules (unique ids, contiguous numbers, projects exist, closure chapter, body budget)
- [x] 2.3 Region 1 chapters 0–9 (amended: ten, see D-076); content version 4 → 5; content-compatibility gate stays green
- [x] 2.4 Domain/application tests: order, monotonic, no-steps-only, older save gains chapters

## 3. Application
- [x] 3.1 `GameSession.GetStory()` → `StoryReadModel`
- [x] 3.2 (via `StoryChapterUnlocked` emitted from `AllocateVitality`) Newly unlocked chapters → return-summary "New chapter" items
- [x] 3.3 `StoryCadenceTests` measures and gates cadence for six profiles (replaces a separate `profile --story` CLI)

## 4. Unity shell
- [x] 4.1 Story screen (journal), reachable from Home; accessible outline
- [x] 4.2 Chapter moments in the return summary; EditMode tests
