## 1. Seam
- [x] 1.1 `Assets/Shell/Presentation/PresentationContracts.cs`: presenter interfaces, memory, value types.
- [x] 1.2 `Assets/Shell/Presentation/PresentationAssetResolver.cs`: catalog interface, dictionary/empty catalogs, resolver with fallback chain and missing-key list.
- [x] 1.3 `Assets/Shell/Presentation/WorldPresentationController.cs` and `NullPresentation.cs`.
## 2. Tests (`Assets/Tests/EditMode/WorldPresentationSeamTests.cs`)
- [x] 2.1 A tap routes to that place's dialogue and choices; locked or unknown places open nothing; landmark views receive their variant keys; a missing view is skipped.
- [x] 2.2 Dialogue can be skipped at once, stays revisitable and clears pending; an open dialogue never blocks choosing or walking progress; the save is byte-identical to a player who never opened dialogue.
- [x] 2.3 Choosing a goal gives the same save as a direct `EnqueueProject`; re-choosing is a no-op; an unknown id fails with `UnknownProject`.
- [x] 2.4 Changes made while away are replayed once on resume, in stream order, and not again (including after a reload with the same memory); reduced motion is passed through.
- [x] 2.5 Asset fallback chain; with no art, every world, portrait and choice key resolves to Missing without throwing.
## 3. Verify
- [x] 3.1 Unity 6000.3.26f1 batch EditMode suite green (new and existing tests, text shell unchanged); `dotnet build` + `dotnet test` green; `scripts/verify-architecture.sh` green (rule 7). `ProjectSettings.asset` reverted before commit.
