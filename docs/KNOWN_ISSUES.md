# Known Issues Register

The §18 "known issues by severity" artifact for the release evidence package.
Severities follow `TESTING_AND_RELEASE.md` §15. This register is the durable
home for every *accepted-open* defect, gap, and verification hole: anything a
release reviewer must not have to rediscover. New entries are added whenever a
campaign accepts something open; entries are removed only with evidence.

| ID | Sev | Status | Issue | Closure lane | Evidence |
|---|---|---|---|---|---|
| KI-001 | High | Open (external) | Hosted GitHub Actions runners execute zero steps ("recent account payments have failed or your spending limit needs to be increased"); every run since 2026-08-30 is `steps: 0`. The complete gate is locally reproducible (D-049) and a clean clone of the canonical remote passes it, but hosted-green remains unclaimed. | Account owner: fix billing/spending limit; first hosted green must be diffed against `docs/evidence/ci-local/` | `docs/evidence/ci-local/CAMPAIGN-2026-09-29.md`, runs `36488888405`, `36526131921` |
| KI-002 | High | Open (external) | No Unity editor/runtime exists (D-035), so all RUNTIME/DEVICE verification lanes are blocked: M5 screens and accessibility, M6 Visit World, M7 platform adapters, M8 lifecycle/on-device performance. Headless contracts for these areas are automated verified; their runtime halves stay UNVERIFIED and must never be claimed. | Runtime environment unblocking (external) | ROADMAP M5/M6/M7/M8 status notes; `docs/evidence/m8-h1/CAMPAIGN_OUTCOME.md` |
| KI-003 | Medium | Open (reserved decision) | The `inactivity` pacing profile (silent weekends + days 100–144 fully silent) never reaches the Region 1 closure milestone within the 400-day horizon; two chains stay incomplete. Real quitting-spiral shape (R-007), deliberately recorded rather than silently rebalanced because balance changes invalidate committed pacing evidence and D-039 targets. | Product-balance decision reserved for M5/M6 milestone work | `docs/evidence/m4/pacing-*.txt`, ROADMAP "Intermittent-engagement pacing finding" |
| KI-004 | Low | Open | Pacing targets for the §13 profiles are provisional (D-039), not ratified product numbers. | Same balance decision as KI-003 | `docs/DECISIONS.md` D-039 |
| KI-005 | Medium | Open | `docs/PRODUCT_SPEC.md` has no explicit privacy requirements section (keyword scan: zero matches for privacy/offline-first/analytics); the guarantees exist only implicitly in the product thesis and explicitly in `PRIVACY_REVIEW.md`. Spec should gain an explicit, testable privacy requirements section. | Docs/product lane: author spec section, then this review cites it | `docs/PRIVACY_REVIEW.md` §7 P-2 |
| KI-006 | Medium | Open | Save data is health-adjacent (timestamped walking counts, provider record IDs) and stored **plaintext at the application layer** (base64 payload; SHA-256 is integrity-only), including the backup generation. At-rest confidentiality currently relies on OS disk encryption. Decision needed: app-layer encryption, OS-encryption requirement documented as a system requirement, or accepted risk with rationale. | Product/security decision before M9 | `docs/PRIVACY_REVIEW.md` §3/§7 P-1 |
| KI-007 | Low | Open | `install-git-hooks.sh --check` can only pass in fixture repos without an origin when `GITHUB_REPOSITORY` is exported (identity guard needs a source of truth). Works, but a `--allow-no-origin` mode for test fixtures would remove the implicit env coupling. | Tooling hygiene | observed during C4 clean-clone campaign, `docs/evidence/ci-local/CAMPAIGN-2026-09-29.md` |
| KI-008 | Low | Open | Long guard/proof suites and lease ownership both break on this harness's short-lived shell model: (a) a foreground suite killed mid-lease-scenario strands a writer lease (two operator-authorized force-releases happened this way in one day); (b) `writer-lease.sh` only recognizes a release as same-session via `SWG_SESSION_ID` or a still-alive holder PID, and every harness command runs in a fresh bash — so a later `release` call reports the session's **own** lease as foreign. Remedies in practice: run long suites detached (`artifacts/ev-c4/launch*.ps1` pattern) and export a stable `SWG_SESSION_ID` for the whole session so acquire/release match across processes (verified working). A lease TTL or heartbeat could make stranding self-healing. | Tooling hardening decision | `docs/evidence/ci-local/CAMPAIGN-2026-09-29.md` "Lease note"; C5 close-out (same-session release refused at `9042019`, resolved via `SWG_SESSION_ID`) |

## Rules

- **No Critical or High item may remain open at release qualification**
  (TESTING_AND_RELEASE §15). KI-001/KI-002 being externally blocked is exactly
  why M9 cannot currently be claimed, and no local evidence may be presented
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