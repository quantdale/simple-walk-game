# Known Issues Register

The §18 "known issues by severity" artifact for the release evidence package.
Severities follow `TESTING_AND_RELEASE.md` §15. This register is the durable
home for every *accepted-open* defect, gap, and verification hole: anything a
release reviewer must not have to rediscover. New entries are added whenever a
campaign accepts something open; entries are removed only with evidence.

| ID | Sev | Status | Issue | Closure lane | Evidence |
|---|---|---|---|---|---|
| KI-002 | High | Open (runtime/device half) | **Install/activation half CLOSED 2026-10-16:** Unity 6000.3.26f1 is installed and the Personal entitlement resolves through the Hub licensing client. `Unity -batchmode -nographics -quit` exits 0, see `docs/evidence/m5-u1/licence-activation-check.txt`; no `Unity_lic.ulf` exists and none is required. **Still open:** no device and no GPU on the box, so all RUNTIME/DEVICE verification lanes are blocked: M5 screens and accessibility, M6 Visit World, M7 platform adapters, M8 lifecycle/on-device performance. Headless contracts for these areas are automated verified; their runtime halves stay UNVERIFIED and must never be claimed. | Runtime environment unblocking (external) | ROADMAP M5/M6/M7/M8 status notes; `docs/evidence/m8-h1/CAMPAIGN_OUTCOME.md` |
| KI-007 | Low | Open | `install-git-hooks.sh --check` can only pass in fixture repos without an origin when `GITHUB_REPOSITORY` is exported (identity guard needs a source of truth). Works, but a `--allow-no-origin` mode for test fixtures would remove the implicit env coupling. | Tooling hygiene | observed during C4 clean-clone campaign, `docs/evidence/ci-local/CAMPAIGN-2026-09-29.md` |
| KI-008 | Low | Open | Long guard/proof suites and lease ownership both break on this harness's short-lived shell model: (a) a foreground suite killed mid-lease-scenario strands a writer lease (two operator-authorized force-releases happened this way in one day); (b) `writer-lease.sh` only recognizes a release as same-session via `SWG_SESSION_ID` or a still-alive holder PID, and every harness command runs in a fresh bash — so a later `release` call reports the session's **own** lease as foreign. Remedies in practice: run long suites detached (`artifacts/ev-c4/launch*.ps1` pattern) and export a stable `SWG_SESSION_ID` for the whole session so acquire/release match across processes (verified working). **Partial remedy (2026-10-16):** `acquire` is now idempotent for the same explicit `SWG_SESSION_ID`. A harness whose shell died re-acquires its own lease without changing the file. An anonymous (PID-default) or different session is still refused (guard scenario 5). A TTL/auto-steal was deliberately **not** added: it would let a slow but live writer lose its lease, which is the exact incident the lease exists to prevent. Stranded leases from sessions without an explicit id still need an operator. | Tooling hardening decision | `docs/evidence/ci-local/CAMPAIGN-2026-09-29.md` "Lease note"; C5 close-out (same-session release refused at `9042019`, resolved via `SWG_SESSION_ID`) |

## Needs Michael (morning queue)

*(empty as of 2026-10-16: Unity activated, KI-001 and KI-006 closed)*

Operator-only actions. Agents log these and keep working on unblocked tasks (see `docs/AGENT_EXECUTION_GUIDE.md` "Blockers never stop the campaign").

| Blocker | Exact fix | Links | Unblocks |
|---|---|---|---|

## Rules

- **No Critical or High item may remain open at release qualification**
  (TESTING_AND_RELEASE §15). KI-002 being externally blocked is exactly
  why M9 cannot currently be claimed (KI-001 closed 2026-10-16), and no local evidence may be presented
  as if it changed that.
- "Open (external)" means closure requires an action outside this repository;
  it does not lower severity.
- Medium items must each carry an explicit pre-release disposition decision;
  Low items may ship with documented rationale.

## Closed

Items closed by a campaign, retained so a reviewer can see what was open and what closed
it. A closed item is never deleted: the register's value is partly the history of what
this repository got wrong and then fixed.

| ID | Sev | Issue | Closed by | Evidence |
|---|---|---|---|---|
| KI-009 | Critical *(was)* | A **missing primary save slot** was reported as "no save found" and the healthy backup generation was never read. Because the store promotes a generation with a delete-then-move pair, an absent primary is reachable (a crash between those two steps). Any composition root treating "no save" as a fresh profile replaced the player's world with a new empty one. **This item was never in the register** despite being a Critical-class data-loss path in the flagship product invariant — which is itself the finding recorded in the note below. | D-052 / OpenSpec change `recover-save-from-backup-when-primary-is-missing` | 8 new application-layer regression tests (`SessionMissingPrimaryRecoveryTests`); CLI reproduction before/after in `docs/evidence/save-recovery/CAMPAIGN-2026-09-30.md` |
| KI-010 | High *(was)* | **A returning player's entire activity backlog was rejected.** The acceptance window was anchored to the raw device clock (`now − 14 days`), so a player coming back after a holiday lost every record the provider handed them — the activity that earned the most goodwill silently disappeared. A device clock merely *minutes* fast additionally made every record look future-dated and stopped crediting altogether, with no player-visible explanation. Neither scenario had any test coverage. | D-053 / OpenSpec change `anchor-activity-acceptance-window-to-trusted-time` | 17 new tests in `TrustedTimeWindowTests` (30-day and 45-day absences, fast clock, clock jump, backward clock, hostile dump, deletion-marker eligibility, boundary edges, accounting identity); `docs/evidence/trust-window/CAMPAIGN-2026-09-30.md` |
| KI-011 | High *(was)* | **The content-drift gate could not fail.** `scripts/verify-content-compatibility.sh --with-fixtures` validated `dirname "$save"` — the fixture's *parent directory* — instead of the fixture file. The CLI reads `save.json` from the directory it is given, so any corrupt fixture sitting beside a valid save reported `violations=0 ... EXACT-MATCH` and exited 0. The gate guarding unrecoverable save loss (R-018) therefore certified broken fixtures as clean. Found by review on 2026-09-30; **the default CI invocation was unaffected** (it generates its own saves and passes no fixtures), so this was latent, not active. | Fixed in `scripts/verify-content-compatibility.sh`: each fixture is staged into its own isolated directory as `save.json`; a permanent `--self-test` mode plants a corrupt fixture beside a valid one and requires the gate to reject it (and a valid one to be accepted, so the test cannot be satisfied by rejecting everything); the self-test is a CI step. | Reproduced before/after: garbage fixture → `PASS … EXACT-MATCH`, exit 0 **before** → `FAIL`, exit 1 **after**; `scripts/verify-content-compatibility.sh --self-test` passes; step 16/16 in `scripts/verify-ci-steps.sh` |
| KI-012 | Medium *(was)* | **An authored Vitality cap would have silently discarded earned walking credit.** D-054 correctly decided Vitality must not be capped, but nothing *enforced* that decision: `GameFactory` applies every authored cap to a fresh balance, and `ValidateResourceCaps` accepted a Vitality cap without complaint. A future campaign adding one would have produced a validating build that quietly clamped real activity credit — the exact data loss the exemption exists to prevent. | `ContentValidator.ValidateResourceCaps` now rejects an authored Vitality cap and names the resource and the reason (D-054) | `ContentValidationTests.Vitality_MayNotBeCapped_AndTheViolationNamesTheResource` (negative + positive control) |
| KI-013 | High *(was)* | **A finished game permanently demanded an impossible action.** With the region complete and every project finished, `GetHome()` reported `RequiresAttention = true` with the reason `QueueEmptyWithBankedVitality` — an instruction to queue restoration work that no longer existed — for as long as the player kept walking, while banked Vitality grew without bound (reproduced: 60,650 banked with 19/19 complete). No assertion of the Home surface after region completion existed anywhere in the suite, so the acceptance proof that "post-completion state is stable" covered the *state* and nothing covered the *surface*. | D-061 / OpenSpec change `define-post-completion-evergreen-state` | `PostCompletionEvergreenStateTests` (7 named scenarios driving the real trust pipeline: conclusion not alarm, continued activity applied to the authored program and never alarming, the pre-completion alarm still honest, every surface truthful, permanence across reload/restart/long absence, closure independent of the post-completion program, bank bounded to one cycle) plus `ContentValidationTests` content/state invariants; `docs/evidence/m4/pacing-*.txt` still reproduce byte-for-byte |
| KI-003 | Medium *(was)* | **The product's own target player could not finish the game.** The `inactivity` profile (silent weekends plus a fully silent day 100–144) earned 16,420 Vitality in 400 days against a 19,400-Vitality roster — short by 2,980 — so the forest and research chains never completed and the region never closed. The pacing harness reported the shortfall and exited 0, so it survived a whole milestone as "acceptable evidence". | D-062 / OpenSpec change `close-inactivity-pacing-shortfall` | `docs/evidence/m4/pacing-inactivity.txt` regenerated: `region completed: yes on day 376`, `closure criterion: met (6/6 chains)`, all 19 projects complete, validator-clean; `RegionPacingContractTests` drives the profile through the real trust pipeline; the pacing gate now exits 2 and names the unfinished chains on a shortfall |
| KI-005 | Medium *(was)* | PRODUCT_SPEC had no explicit privacy requirements section (PRIVACY_REVIEW P-2). The ID was cited by the review but **never entered in this register**, the same register-gap shape the note below warns about. | PRODUCT_SPEC §7a added (2026-10-16): eight privacy requirements, each traced to its verification in PRIVACY_REVIEW | `docs/PRODUCT_SPEC.md` §7a; PRIVACY_REVIEW §7 |
| KI-006 | Medium *(was)* | Health-adjacent save (including the backup generation) is plaintext at the application layer. | D-067 (2026-10-16): option (b). OS device encryption (Android FBE, iOS Data Protection) is a documented system requirement; primary and backup both stay in app-private storage. Device verification belongs to the runtime/M7 lane. | `docs/DECISIONS.md` D-067; PRIVACY_REVIEW §3/§7 |
| KI-001 | High *(was)* | Hosted GitHub Actions runners executed zero steps (billing/spending limit) from 2026-08-30. | Repository made public on 2026-10-16 (public repos get free standard runners). First hosted runs of the full `ci` workflow are green: runs 38798928999, 38798934912, 38798938154, 38798944450, 38798955391 (PRs #7–#10). The only hosted failure seen was KI-014 (CRLF pacing evidence), fixed on the base branch. | https://github.com/quantdale/simple-walk-game/actions/runs/38798934912 |
| KI-014 | Low *(was)* | `verify-pacing-evidence.sh` reported 4/7 pacing reports "drifted" on Linux because they were committed with CRLF line endings; the content was identical. | `.gitattributes` `*.txt text eol=lf` plus a renormalize-only commit (2026-10-16) | `verify-pacing-evidence.sh`: 7 reproduced, 0 drifted |
| KI-004 | Low *(was)* | Pacing targets for the §13 profiles were provisional (D-039), not ratified product numbers. | D-062 ratified them per profile: high ≤120, moderate ≤300, irregular ≤250, weekend-heavy ≤300, pathological/inactivity ≤400 — each met with the whole roster restored — and re-affirmed the `low` long tail as an explicit exemption | `docs/DECISIONS.md` D-062 measured table; `GAME_SYSTEMS.md` §13; `PacingContract` is the machine-checkable form of the same table |

## Note on this register

Between the M8-H1 campaign and the 2026-09-30 repository audit, this register contained
**no entry for a Critical-class data-loss defect** that was simultaneously present,
reproducible, and unrecoverable. The defect was real and the store's own comments
described the reachable state that triggered it; what was missing was any mechanism that
would have surfaced it to a release reviewer.

Two consequences, recorded so the same gap does not reopen:

1. A mitigation existing is not the same as a mitigation being *exercised*. `WriteAtomicPreservingBackup`
   (D-040) protected the healthy generation perfectly — on a code path no caller took.
   `ActivityValidationPolicy`'s 14-day reconciliation horizon bounded old history correctly
   while rejecting the product's own central case (D-053). Future audits must ask what a
   guarantee protects, not only whether it is implemented.
2. Boot-reachable filesystem states are product defects even when the filesystem layer is
   correct. The application-layer test gap is what let this survive a campaign that
   specifically covered crash-during-promotion.
3. A named acceptance scenario that deliberately exercises *none* of the behaviour it is
   named for is a coverage gap wearing a test's clothes. `Scenario05_ThirtyDayReturn`
   returned after 30 days and ingested nothing during the absence, so "the provider hands
   back the whole backlog on return" — the single most important ingest scenario for an
   offline-first game — was untested in the entire suite until D-053.
