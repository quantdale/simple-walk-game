## 1. Phrase table and read model

- [x] 1.1 Add `GlancePhrases.FormatLandmark` in `src/WalkGame.Application/ReadModels/GlanceProjector.cs`, mapping `RestorationStage` to the five phrases in `specs/player-glance/spec.md`. No Domain edit.
- [x] 1.2 Add `GlanceReadModel` in `src/WalkGame.Application/ReadModels/GlanceReadModel.cs` with the three answers, optional further-change count, one nullable primary action, the activity sentence, the nullable onboarding sentence, and the copied `RequiresAttention` flag.
- [x] 1.3 Implement `GlanceProjector.Project` as a pure function of `HomeReadModel`, the pending summary, onboarding, activity status, and content titles. Switch on `HomeAttentionReason`. Do not recompute attention. Use the restoring-now and lead-selection rules in `design.md`.

## 2. Session entry that cannot throw for a missing port

- [x] 2.1 Add parameterless `GameSession.GetGlance()`. When `_connectionPort` is null, pass a null snapshot to `ActivityStatusProjector.Project` instead of calling `GetActivityStatus()`.
- [x] 2.2 `GetGlance()` must not save, acknowledge, ingest, or otherwise mutate. Reading it twice returns the same sentences.

## 3. Newly composed landmark lines

- [x] 3.1 In `ReturnSummaryComposer`, change only the `LandmarkStageReached` arm to `GlancePhrases.FormatLandmark`. Leave every other summary line, the bounds, the rotation, and `DerivePrimaryNextAction` alone.
- [x] 3.2 Do not migrate stored summary text. `SaveCodecRoundtripTests` must still round-trip `Old Trailhead has reached Stabilized.`

## 4. Tests

- [x] 4.1 Add `tests/WalkGame.Application.Tests/GlanceLanguageTests.cs`. Pin each stage phrase, including `Old Trailhead is safe to approach.`
- [x] 4.2 In that file, cover a current transformation beating an activity aggregate, the further-change count, and a quiet glance when nothing is current.
- [x] 4.3 In that file, cover all five `HomeAttentionReason` values, including the finished-valley conclusion and the pre-completion banked-progress alarm. `M5H1ShellAcceptanceTests.Scenario06_QueueEmptyWhileAway_ExplicitReasonAndNextAction` must still pass unchanged.
- [x] 4.4 In that file, cover an active project named by title with invested/cost and no stable id, a queued title, and `Evergreen Tending` after completion.
- [x] 4.5 In that file, cover all six activity sentences, permission denial leaving the three answers readable, and a port-less `GameSession` returning the source-unavailable sentence without throwing.
- [x] 4.6 In that file, cover each incomplete onboarding step, denial on the connection step, and no onboarding sentence once complete.
- [x] 4.7 In that file, load a save whose summary text is `Old Trailhead has reached Stabilized.`, read the glance, and assert the stored text is unchanged while the glance sentence does not contain `Stabilized` or `has reached`.
- [x] 4.8 In that file, assert a hostile exception message and provider identifier never appear in `GetGlance()`, and that a second read is identical and does not change vitality.
- [x] 4.9 Add `GetGlance()` to the hand list in `M5H1ContractHardeningTests.PlayerFacingSurfaces_NeverLeakRawExceptionTextOrHealthPayloads`. Leave the reflection sweep's rule intact so the new parameterless surface is included.

## 5. Headless inspection verb

- [x] 5.1 Add `glance` to `tools/simulation/Program.cs`, `Cli.Glance` in `Commands.cs`, and the usage text. It loads, prints `GetGlance()`, and does not write the save.
- [x] 5.2 Do not edit `Out.HomeLine` or `Out.HomeBlock`.

## 6. Decision record

- [x] 6.1 Add D-065 to `docs/DECISIONS.md` stating the phrase table, the no-migration rule, and that Unity rendering remains UNVERIFIED.
- [x] 6.2 Add a binding note to the M5-H1 section of `docs/UX_DESIGN.md`. Do not check off ROADMAP M5 exit criteria.

## 7. Verification

- [x] 7.1 Run `dotnet test --filter GlanceLanguageTests` and the existing tests that must keep passing: `SaveCodecRoundtripTests`, `PostCompletionEvergreenStateTests`, `M5H1ShellAcceptanceTests`, `M5H1ContractHardeningTests`, and `ReturnSummaryDurabilityTests`.
- [x] 7.2 Run `dotnet test` for the solution. Do not delete a failing assertion to get green.
- [x] 7.3 Run `scripts/verify-architecture.sh` and confirm Domain gained no copy dependency.
- [x] 7.4 Run the simulation `glance` verb against a fresh save and against a save with a pending summary. Confirm the save bytes are unchanged and that `new` still prints the previous `HomeLine` contract.
- [x] 7.5 Run `openspec validate --change define-headless-glance-language` if the CLI supports it, and record the result in the implementation notes. Do not claim M5 or device verification.
