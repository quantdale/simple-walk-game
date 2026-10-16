# M5-U3 results (2026-10-16): onboarding polish, PlayMode in batchmode, reversion investigation

Label: **Unity-editor-tested** (Unity 6000.3.26f1, batchmode `-nographics`, box). Device runtime: **UNVERIFIED** (KI-002, no KVM).

| Suite | Command | Result |
|---|---|---|
| EditMode | `EVIDENCE_DIR=docs/evidence/m5-u3 tools/unity/run-editmode-tests.sh` | 23/23 passed (`editmode-results.xml`) |
| PlayMode | `EVIDENCE_DIR=docs/evidence/m5-u3 tools/unity/run-playmode-tests.sh` | 1/1 passed (`playmode-results.xml`) |

PlayMode works headless: `-nographics` still runs the player loop, so `[UnityTest]` coroutines advance frames. Nothing is rendered, so this proves no visuals.

## Onboarding polish (UX_DESIGN §11, §12)
- Every guide screen leads with "Step n of 5" (monotonic, ends at 5).
- The world-baseline step renders the landmark through `GlancePhrases.FormatLandmark` instead of a raw key with "(stage X)".
- The activity step uses `PlayerCopy.ActivityStatus` instead of the enum name.
- First-project choices use `PlayerCopy.Title`.
- New tests: `Guide_screens_never_show_identifiers_keys_or_stage_names`, `Guide_shows_monotonic_step_progress_ending_at_last_step`, `Guide_progress_maps_every_stage_within_bounds`, and PlayMode `Shell_boots_new_game_and_survives_frames`.

## Reversion investigation (KI-015)
Controlled run in a fresh linked worktree: SHA-1 of all tracked files before and after an EditMode batchmode run.
Diff: only `docs/evidence/m5-u1/editmode-results.xml` and `editmode-run.log` (written by the script) and one doc deliberately edited mid-run, which survived.
Unity import did not modify tracked sources or `.meta` files. See KI-015 for the causes and remedies.
