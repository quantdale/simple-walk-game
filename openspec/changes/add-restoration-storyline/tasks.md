## 1. Design
- [x] 1.1 OpenSpec change and D-076

## 2. Domain and content
- [ ] 2.1 `StoryChapterDefinition`, `RegionDefinition.Chapters`, `StoryProgress.Evaluate` (pure)
- [ ] 2.2 ContentValidator rules (unique ids, contiguous numbers, projects exist, closure chapter, body budget)
- [ ] 2.3 Region 1 chapters 0–8; content version 4 → 5; content-compatibility gate stays green
- [ ] 2.4 Domain/application tests: order, monotonic, no-steps-only, older save gains chapters

## 3. Application
- [ ] 3.1 `GameSession.GetStory()` → `StoryReadModel`
- [ ] 3.2 Newly unlocked chapters → return-summary "New chapter" items
- [ ] 3.3 `profile --story` cadence output and `StoryCadenceTests`

## 4. Unity shell
- [ ] 4.1 Story screen (journal), reachable from Home; accessible outline
- [ ] 4.2 Chapter moments in the return summary; EditMode tests
