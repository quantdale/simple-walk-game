## 1. Content bindings
- [x] 1.1 `Content/Region1WorldLayout.cs` (chain → landmark) and `Content/Region1Characters.cs` (characters, appearances, lines); `ContentText.Fields.Line` / `Name`. Tests: `tests/WalkGame.Application.Tests/WorldPresentationContentTests.cs` (every project bound exactly once, every location a real landmark, every milestone a real project, line ≤ 140, unique ids, every character reachable).
## 2. World view + dialogue
- [x] 2.1 `WorldViewReadModel`, `CharacterDialogueReadModel`, `WorldPresentationProjector`, `GameSession.GetWorldView(seen)` / `GetCharacters()`. Tests: `tests/WalkGame.Application.Tests/WorldViewReadModelTests.cs` (fresh game, mid-game, completed region, locale overlay, pending/seen, read leaves the encoded save identical, determinism).
## 3. Choice previews
- [ ] 3.1 `RestorationChoicesReadModel`, `GameSession.GetRestorationChoices()`. Tests: `tests/WalkGame.Application.Tests/RestorationChoicePreviewTests.cs`. Each preview's predicted stages, opened projects, producers, arcs, discoveries and chapters must equal what actually happens when that project completes, for every selectable project along a full playthrough.
## 4. Visual-change stream
- [ ] 4.1 `VisualChangeEvent`, `GameSession.GetVisualChangesSince`, `WorldViewDiff.Between`. Tests: `tests/WalkGame.Application.Tests/VisualChangeStreamTests.cs` (replay matches canonical state at every step, since-filtering, reload/restart determinism, legacy null timestamps, diff of a view with itself is empty, construction advance in-session).
## 5. Verify
- [ ] 5.1 `dotnet build` + `dotnet test` green; `scripts/verify-architecture.sh`; content-compatibility and pacing evidence unchanged (no content/fingerprint change); save schema v3 unchanged. Existing suites stay green, in particular `ContentValidationTests`, `Region1PlaythroughTests`, `StoryCadenceTests`, `SummaryPriorityTests`, `LettersReadModelTests` and the Unity EditMode suite (no Unity change in this change).
