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
| KI-008 | Low | Open | A long guard/proof suite executed in a bounded foreground shell can be killed mid-lease-scenario and strand a writer lease in the real checkout (two operator-authorized force-releases happened this way in one day). Mitigation in practice: run long suites detached (`artifacts/ev-c4/launch*.ps1` pattern). A lease TTL or heartbeat could make stranding self-healing. | Tooling hardening decision | `docs/evidence/ci-local/CAMPAIGN-2026-09-29.md` "Lease note" |

## Rules

- **No Critical or High item may remain open at release qualification**
  (TESTING_AND_RELEASE §15). KI-001/KI-002 being externally blocked is exactly
  why M9 cannot currently be claimed, and no local evidence may be presented
  as if it changed that.
- "Open (external)" means closure requires an action outside this repository;
  it does not lower severity.
- Medium items must each carry an explicit pre-release disposition decision;
  Low items may ship with documented rationale.