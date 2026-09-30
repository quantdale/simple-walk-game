# Decision Log

This file records product and architectural decisions that future agents/maintainers should treat as current defaults until superseded by a later decision.

Use the format:

- **Status:** Proposed / Accepted / Superseded / Rejected
- **Decision:** what is being chosen
- **Rationale:** why
- **Consequences:** constraints created by the decision

---

## D-001 — Real-world activity is the primary gameplay

**Status:** Accepted

**Decision:** The core game is driven by real-world movement/activity rather than foreground tapping, combat, or conventional session-based play.

**Rationale:** The target player may not be willing to dedicate substantial active gaming time. The game should attach meaning to activity the player is already doing.

**Consequences:**

- app-closed progression is first-class;
- activity ingestion is a core subsystem;
- foreground mechanics may not become mandatory maintenance;
- product success cannot be measured only by session length.

---

## D-002 — Low required screen time is a hard product constraint

**Status:** Accepted

**Decision:** Core progression must remain coherent with seconds-long check-ins and optional longer sessions.

**Rationale:** The product is specifically designed for users who may be busy or not highly active gamers.

**Consequences:**

- automation is required;
- no frequent collection loops;
- project queues should auto-advance where safe;
- multi-day absence must be a supported normal state;
- every feature should be reviewed against an attention budget.

---

## D-003 — Persistent restoration is the main progression fantasy

**Status:** Accepted

**Decision:** Real-world activity restores a damaged world over time.

**Rationale:** Restoration creates a durable and visually legible representation of cumulative physical activity.

**Consequences:**

- world state is persistent;
- major project completion should create visible environmental change;
- Region 1 must have strong degraded-to-restored contrast;
- progress cannot be represented only through account XP/numbers.

---

## D-004 — Inactivity does not destroy major progress

**Status:** Accepted

**Decision:** Missing days or taking breaks does not destroy restored assets, completed projects, or major earned progress.

**Rationale:** Punishing absence conflicts directly with the low-pressure ambient product thesis and harms re-entry.

**Consequences:**

- no destructive world decay;
- no mandatory daily maintenance;
- momentum bonuses, if introduced, must be non-destructive;
- return UX should welcome rather than shame.

---

## D-005 — Offline-first MVP

**Status:** Accepted

**Decision:** Core progression and local world state do not require a backend or mandatory account.

**Rationale:** Offline reliability reduces complexity, protects the ambient use case, and avoids making network availability part of the activity trust chain.

**Consequences:**

- canonical state is stored locally;
- future sync must be additive and conflict-aware;
- no server dependency for project progression;
- cloud features require a later architecture/privacy decision.

---

## D-006 — No mandatory precise GPS for the core loop

**Status:** Accepted

**Decision:** Core activity progression uses health/activity data and does not require continuous precise route tracking.

**Rationale:** Continuous GPS increases battery/privacy cost and is unnecessary for the primary restoration loop.

**Consequences:**

- step/activity aggregates are preferred inputs;
- route-based features, if ever added, are optional;
- permissions should remain minimal.

---

## D-007 — Exactly-once reward crediting is a non-negotiable invariant

**Status:** Accepted

**Decision:** The same logical activity record/transaction may not generate progression more than once.

**Rationale:** Duplicate credit immediately undermines trust, balance, and save integrity.

**Consequences:**

- durable dedup identities;
- reward transaction IDs;
- idempotent application;
- replay/failure tests;
- activity checkpoint sequencing cannot bypass durable reward state.

---

## D-008 — Canonical state lives outside Unity presentation

**Status:** Accepted

**Decision:** The authoritative game rules/state live in a pure C# domain layer with no Unity engine dependency.

**Rationale:** Deterministic tests, offline simulation, migration, and exactly-once accounting should not depend on scene objects or frame lifecycle.

**Consequences:**

- presentation binds to canonical state;
- scene reload cannot alter progression;
- domain tests run outside Unity where practical;
- platform bridges cannot directly mutate world state.

---

## D-009 — Unity 6 LTS is the baseline presentation/runtime direction

**Status:** Accepted *(ratified by implementation: domain targets netstandard2.1 with zero engine references, ready for Unity 6 LTS hosting)*

**Decision:** Use Unity 6 LTS + C# for mobile runtime and optional 3D Visit World presentation, while keeping the domain independent.

**Rationale:** The product benefits from a richer optional 3D restored world, and the previous design direction is compatible with a C# deterministic domain.

**Consequences:**

- exact Unity/package versions must be locked during M1/M3 implementation;
- mobile build/runtime validation is required;
- a future superseding decision may choose another presentation technology without rewriting domain rules if boundaries are preserved.

---

## D-010 — Visit World is optional depth

**Status:** Accepted

**Decision:** Real-time 3D exploration is optional and cannot be required for baseline progression.

**Rationale:** The product must work for players who do not want long sessions or heavy foreground interaction.

**Consequences:**

- lightweight Region/Projects UI supports all core decisions;
- 3D content may unlock optional flavor/discoveries but not gate core restoration;
- 3D scene loads on demand;
- 3D load failure cannot break the game.

---

## D-011 — One complete region before expansion

**Status:** Accepted

**Decision:** Region 1 must be vertically complete and release-qualified before Region 2 production becomes a default priority.

**Rationale:** Multiple shallow regions would hide weaknesses in the core product loop and multiply content/technical debt.

**Consequences:**

- Region 1 gets complete project, discovery, expedition, ecology, settlement, and visual arcs;
- post-MVP expansion happens only after a decision gate;
- Region 2 is explicitly excluded from MVP execution campaigns.

---

## D-012 — Vitality is the primary activity-derived resource

**Status:** Accepted *(implemented: `ResourceType.Vitality` is canonical; 100 steps → 1 Vitality via conversion rule v1)*

**Decision:** Use a primary resource tentatively called `Vitality` to represent validated restorative activity.

**Rationale:** One dominant resource keeps the relationship between real movement and world restoration understandable.

**Consequences:**

- conversion rules are deterministic and versioned;
- secondary resources must have distinct roles;
- avoid economy sprawl;
- naming may change without changing the underlying model.

---

## D-013 — Project queue automatically preserves progress continuity

**Status:** Accepted

**Decision:** The player can queue restoration work and eligible progress can automatically continue into subsequent queued work.

**Rationale:** Requiring the app to be opened exactly when a project completes violates the ambient design.

**Consequences:**

- project completion mid-batch/offline must be deterministic;
- remaining progress rolls according to documented rules;
- queue-empty fallback must avoid catastrophic wasted activity.

---

## D-014 — Activity conversion rules are versioned

**Status:** Accepted

**Decision:** Store the conversion-rule version associated with credited activity/reward transactions.

**Rationale:** Balance changes must not cause historical activity to be silently revalued or replayed.

**Consequences:**

- old transactions remain auditable;
- conversion changes require migration/reconciliation decisions;
- tests include multiple rule versions when such changes ship.

---

## D-015 — Platform activity APIs are adapters, not domain dependencies

**Status:** Accepted

**Decision:** Android/iOS activity sources are isolated behind platform-neutral application ports.

**Rationale:** Platform behavior changes independently and should not infect game rules.

**Consequences:**

- fixtures can test the full downstream pipeline;
- Android/iOS can have different capability details;
- exact provider API versions are implementation-time decisions;
- platform-specific limitations are documented honestly.

---

## D-016 — Raw health data retention is minimized

**Status:** Accepted

**Decision:** Persist only the normalized/provenance information needed for deduplication, reconciliation, and game progress.

**Rationale:** The game does not need broad health history and should minimize privacy risk.

**Consequences:**

- no wholesale raw platform payload storage;
- diagnostics are redacted/bounded;
- future backend cannot receive raw health data without a new explicit decision.

---

## D-017 — Deterministic RNG for canonical outcomes

**Status:** Accepted *(POLICY kept; implementation inert — corrected by D-057, 2026-09-30)*

**Decision:** Any canonical outcome that uses randomness must derive from persisted
deterministic seeds/state. Rendering-side randomness must never decide a reward.

**Rationale:** Save/reload/retry must not reroll canonical results.

**Consequences:**

- RNG is injected;
- rendering randomness remains non-authoritative;
- algorithm changes may require version consideration.

**CORRECTION (D-057).** As originally written this read as a description of a *live*
mechanism. It was not one. `DeterministicRng` is fully implemented (xoshiro256\*\* with
SplitMix64 seeding, rejection-sampled `NextInt64`), persisted in `GameState.Rng`,
round-tripped field by field by the save codec, printed by `dump`, and covered by eight
unit tests — and **no production or tool code draws from it**. Grepping `src/` and
`tools/` for `NextUInt64|NextInt64|NextDouble` returns only the definitions.

The practical consequence is that **`StartNewGame(ulong seed)` cannot currently affect
any outcome**: the seed is written to the save and read back, and that is all. Two
profiles run with different seeds produce identical canonical state.

The **policy is correct and is kept**. What was missing was a consumer, not a policy. The
mechanism is retained as persisted infrastructure for the first canonical consumer, whose
obligations are now stated explicitly: it must add an end-to-end determinism test
covering the save/load round trip, because that will be the first time `Rng` is read, the
first time the seed matters, and the first opportunity to regress determinism. The
current inert behaviour is pinned by `SeedDoesNotAffectCanonicalStateTests` so that the
first consumer must revisit seed semantics deliberately rather than silently redefining
what a seed means.

A property test's use of the generator as a **data generator** is not canonical
randomness; `SeededProgressionPropertyTests` now names its local `testDataRng` and says
so, because that file is the single most misreadable reference in the repository.

---

## D-018 — Evidence states replace vague “done” labels

**Status:** Accepted

**Decision:** Features are classified as SPECIFIED, IMPLEMENTED, AUTOMATED VERIFIED, RUNTIME VERIFIED, DEVICE VERIFIED, or RELEASE QUALIFIED.

**Rationale:** Platform/mobile projects easily overstate readiness when code exists but runtime/device behavior remains unproven.

**Consequences:**

- release docs must state evidence precisely;
- editor success is not device verification;
- documentation drift becomes a quality issue.

---

## D-019 — Critical/High defects block release

**Status:** Accepted

**Decision:** No known Critical or High severity defect remains open at release qualification.

**Rationale:** The highest-risk failures affect progression trust, data integrity, platform usability, accessibility, or severe performance.

**Consequences:**

- scope cuts are preferable to shipping a known major integrity defect;
- defect severity influences roadmap priority.

---

## D-020 — No MVP backend/social/multiplayer complexity

**Status:** Accepted

**Decision:** The MVP does not include multiplayer, guilds, PvP, social feed, competitive leaderboards, server-authoritative progression, or live-service infrastructure.

**Rationale:** These systems do not prove the ambient-restoration thesis and would materially increase scope.

**Consequences:**

- architecture should not be pre-distorted around hypothetical live service needs;
- post-MVP additions require a new decision and product rationale.

---

## D-021 — No ad-dependent or gacha economy in MVP core

**Status:** Accepted

**Decision:** Core progression is not designed around ads, gacha, or premium currency.

**Rationale:** Monetization pressure would distort attention budgets and progression before the product loop is validated.

**Consequences:**

- MVP balance assumes earned activity progression;
- monetization, if later desired, receives a separate ethical/product/economy review.

---

## D-022 — Performance budgets are release gates

**Status:** Accepted

**Decision:** Launch, responsiveness, frame pacing, memory, battery/thermal behavior, and reconciliation cost are measured against documented budgets.

**Rationale:** A passive/ambient product that feels heavy on a phone contradicts its value proposition.

**Consequences:**

- mobile device evidence required;
- optional 3D has quality tiers;
- lightweight mode avoids retaining unnecessary world cost.

---

## D-023 — Documentation-first bootstrap

**Status:** Accepted

**Decision:** The new repository begins with comprehensive product/engineering contracts before implementation.

**Rationale:** The repository started effectively empty, allowing architectural and scope decisions to be made explicitly rather than reconstructed from accidental code structure.

**Consequences:**

- M0 documentation is the initial source of truth;
- implementation must update docs as evidence replaces proposals;
- proposed decisions (such as exact runtime/tooling) may be superseded when implementation evidence warrants it.

---

# Unresolved decisions

The following should be resolved at the relevant milestone rather than guessed prematurely:

1. Exact Unity 6 LTS editor/runtime version. **(open — D-035)**
2. Exact local persistence implementation (snapshot+journal vs SQLite or another adapter). *(resolved by D-025/D-026: versioned JSON envelope with SHA-256 integrity, atomic snapshot + one-generation backup)*
3. Exact Android Health Connect SDK/API integration strategy and minimum supported OS/device policy. *(open — M7, physically blocked)*
4. Exact iOS HealthKit integration strategy and minimum supported iOS version. *(open — M7, physically blocked)*
5. Final Region 1 fiction/name/art direction. *(fiction resolved: Millbrook Valley, WORLD_AND_CONTENT; art direction still open — M6)*
6. Final Vitality conversion curve and safety bounds. *(curve resolved: conversion rule v1, 100 steps → 1 Vitality, integer floor, versioned. Safety bounds remain open — the `pathological` profile in D-046 measures ≈686 effective steps/Vitality under D-030 clamping, and no diminishing-returns design exists, so R-008 is untouched)*
7. Whether Materials and Knowledge both survive MVP balance testing. *(open — and now materially different: neither resource has any production consumer or cap, see the D-032 discharge note recorded under D-046)*
8. Notification categories and defaults after UX/device validation. *(categories/defaults resolved by D-042: opt-in master, four categories, optional reminder; "after UX/device validation" remains open — M5/M7)*
9. Asset delivery strategy for Visit World. *(open — M6)*
10. Whether cloud backup/sync enters post-MVP scope. *(open; ROADMAP post-MVP gate explicitly does not pre-authorize it)*

Resolve these through new decision entries with rationale and consequences; do not silently bake them into implementation.

*(Status annotation added 2026-09-29 during the documentation-truth campaign: this list was
presented as uniformly unresolved even though four entries had been settled by accepted
decisions, which is exactly the documentation-drift failure R-020 describes.)*

---

## D-024 — M1 implementation stack: standalone .NET, netstandard2.1 + C# 9 domain

**Status:** Accepted

**Decision:** The deterministic core is implemented as a plain .NET solution (`SimpleWalkGame.sln`) independent of Unity: `src/WalkGame.Domain` and `src/WalkGame.Application` target `netstandard2.1` with `<LangVersion>9.0</LangVersion>`; `src/WalkGame.Infrastructure` targets the same profile with a `System.Text.Json` 8.x package reference; test projects and the headless CLI target `net9.0`.

**Rationale:** ROADMAP M1 requires a headless, Unity-free domain that a clean clone can build and test with the SDK alone. netstandard2.1 + C# 9 keeps the Domain/Application sources consumable by Unity 6 either as compiled assemblies or source.

**Consequences:**
- no C# 10+ syntax in Domain/Application (no record structs, no file-scoped namespaces there);
- Infrastructure may be replaced/adapted for Unity if its JSON stack differs;
- tests/CLI are dev-time only and never ship to device.

---

## D-025 — Save envelope: versioned JSON frame with SHA-256 payload integrity

**Status:** Accepted

**Decision:** Save files are `{ schemaVersion, savedAtUtc, payloadSha256Base64, payloadBase64 }`. The payload is opaque base64 UTF-8 JSON of the canonical state graph. Integrity is verified before any migration or deserialization; schema version gates run before migration; migrations transform a cloned payload so failure preserves the original node.

**Rationale:** Opaque framing decouples envelope stability from payload evolution; checksum-first ordering means corrupt data is rejected before it can be misinterpreted; clone-based migration satisfies the "never overwrite the recoverable copy" guardrail.

**Consequences:**
- payload inspection requires base64 decoding (acceptable for tooling);
- every persisted-shape change bumps `SchemaVersions.Current` and adds a registered sequential `ISaveMigration`;
- at schema v1 the migration chain is intentionally empty; the pipeline is proven by dedicated tests.

---

## D-026 — Atomic snapshot store with one-generation backup

**Status:** Accepted

**Decision:** `AtomicFileSaveStore` writes to a temp file with write-through + flush-to-disk, rotates the previous primary into `save.backup.json`, then replaces the primary. Load order is primary → backup; recovery policy lives in the application layer, file semantics in infrastructure.

**Rationale:** Snapshot+backup is the simplest durable pattern that satisfies M1's tested-recovery exit criterion without journal complexity; crash windows leave at most one stale generation behind, which reads ignore.

**Consequences:**
- backup depth is one generation (older history is not retained);
- storage cost is ~2× save size;
- netstandard2.1 lacks atomic-overwrite `File.Move`, so replace is delete+move with the backup guaranteeing recovery across the window.

---

## D-027 — Canonical state serializes directly via STJ contract modifiers

**Status:** Accepted

**Decision:** Domain state classes keep get-only collection properties (invariant-enforcing). The Infrastructure serializer options set `IncludeFields = true` (for `RngState`) and force `JsonObjectCreationHandling.Populate` on collection-typed properties via a `DefaultJsonTypeInfoResolver` modifier. No serializer attributes leak into Domain.

**Rationale:** Default STJ handling *replaces* get-only collections on load, silently dropping all projects/balances/ledger entries — discovered by roundtrip tests during M1. Populate keeps deserialization additive while preserving the domain's encapsulation.

**Consequences:**
- adding new collection-typed canonical state requires verifying the modifier still applies (covered by roundtrip regression tests);
- RngState must remain field-based or gain properties deliberately.

---

## D-028 — Exactly-once crediting keyed by caller-supplied transaction identity

**Status:** Accepted

**Decision:** Reward transactions carry a durable, caller-supplied stable ID (GUID in M1 tooling; platform-fingerprint-derived later in M2). The ledger rejects re-application by ID; balances only move inside `Apply`.

**Rationale:** Replay/crash/retry safety requires identity that survives restarts and cannot be regenerated differently; deriving IDs from activity-source fingerprints arrives with the M2 trust pipeline.

**Consequences:**
- callers who mint fresh GUIDs per retry defeat dedup — adapters must derive IDs deterministically from source records;
- ledger growth is unbounded (years of daily transactions remain small); bounded retention would weaken exactly-once guarantees and needs explicit design first.

---

## D-029 — Correction/deletion baseline: conservative clawback with net-applied accounting

**Status:** Accepted

**Decision:** Higher-revision redeliveries of an already-trusted logical record are corrections. Positive deltas credit the difference; negative deltas (and deletions) claw back only what the unspent Vitality balance allows, durably counting any unclawed remainder in ProcessedRecordLedgerState.UnappliedReversalVitality. Processed-record rows store **net applied** vitality (what the ledger actually saw), never the theoretical target, so dedup state can never outrun reward state. Deletions for unknown records and stale revisions are counted diagnostics, not state changes. Corrections require durable source record IDs; fingerprint-identity sources treat changed content as a new logical record by construction.

**Rationale:** ACTIVITY_PIPELINE §11 prioritizes state integrity and player trust over punitive clawbacks; completed world content must never be destroyed because a source corrected or deleted a small amount. Net-applied rows keep the validator invariant (processedTotal <= ledgerTotal) true without special cases.

**Consequences:**
- reversal remainders are revenue-safe but must be visible in diagnostics forever;
- a later positive correction after a clamped reversal credits against net-applied value, converging toward earned value;
- correction transaction IDs derive from identity + revision + applied amount (
tx-corr1 namespace) and cannot collide with first-credit IDs;
- sources without durable record IDs cannot express corrections (documented adapter requirement for Health Connect/HealthKit work).

---

## D-030 — Bounded reconciliation horizon

**Status:** Accepted

**Decision:** Records ending more than 14 days before "now" (injected clock) are rejected with OutsideHorizon; records ending more than 10 minutes in the future are rejected as FutureTimestamp. The ingestion checkpoint watermark is clamped to now.

**Rationale:** ACTIVITY_PIPELINE §10 requires bounded historical reconciliation: a stale or hostile source dump must not be able to re-open arbitrarily old history, and future-dated records must not pre-mint rewards.

**Consequences:**
- genuinely older backfills need an explicit, separately-designed import path;
- the horizon constants live in domain policy and are covered by validation tests.

---

## D-031 — Fail-closed repository identity and single-writer isolation for agents

**Status:** Accepted

**Decision:** Every autonomous coding session must pass a fail-closed repository identity preflight and hold a single-writer lease before writing. Concretely: `.repo-identity.json` + `scripts/assert-repo-identity.{sh,ps1}` prove origin/CI identity equals `quantdale/simple-walk-game` (exit 86 otherwise); `scripts/writer-lease.{sh,ps1}` grants one atomic per-worktree lease (exit 87 when busy; override requires BOTH `--force` and an explicit operator env acknowledgement); `.githooks/pre-push` refuses any push whose remote tip is not contained in the pushed history (exit 88); CI re-runs the identity guard plus a proof suite over all guard behaviors; root `AGENTS.md` binds every harness and all `/goal` adapters inherit the preflight. Concurrent sessions use dedicated worktrees (`scripts/new-agent-worktree.sh`: one writer = one worktree = one branch).

**Rationale:** Two concurrent executor sessions once wrote this same work tree and interleaved/deleted each other's lineage (commits `b12f52c`, `67368e3`). Identity alone cannot prevent same-tree collisions, hooks alone can be bypassed, and nothing previously stopped an agent intended for the sibling repository `quantdale/walk-game` from operating here. The mechanisms cover each other's gaps: manifest identity (wrong repo), lease (same-tree writers), pre-push (remote races), CI (local bypass), AGENTS.md + adapters (harness-independent propagation).

**Consequences:**
- agents acquire/release the lease around write phases; second writers stop instead of racing;
- stale locks require explicit human recovery — never silent theft;
- force-push / hard-reset / clean remain forbidden conflict shortcuts without operator authorization;
- the guard tooling is under change-control like any invariant: modifying it to make it "stop complaining" is prohibited.

---

## D-032 — Producer capacity is a bounded pending-output store (schema v2)

**Status:** Accepted *(implemented and automated verified: ProducerSimulationTests, GameStateValidationTests, MigrationV1ToV2Tests)*

**Decision:** `ProducerDefinition.CapacityUnits` is the size of that producer's bounded pending-output store, in whole units. Offline production mints `min(remainingStoreRoom, rate × elapsed)` into the store (`ProducerRuntimeState.StoredMilliUnits`, integer milli-units); surplus time beyond the room creates no waste and no value. Whole units auto-deliver into canonical resource balances every tick (never claimed manually). When a downstream resource-level cap refuses delivery, undelivered units stay parked in the producer's store up to its capacity and flush automatically on any later tick call, including zero-elapsed ones. Because minting happens before delivery within a tick, one tick can never produce more than the store's free room regardless of elapsed time; combined with `OfflineAdvancer.MaxProducerInterval` this bounds long absences explicitly. Producer checkpoints are monotonic at every callable boundary: the public `TickProducers` path refuses backward clocks (emitting ClockSkewIgnored) instead of backdating `LastTickUtc`, and unlock stamps the checkpoint at the completion instant so no retroactive production exists.

**Rationale:** GAME_SYSTEMS §5 specifies `produced = min(capacityRemaining, rate × eligibleElapsedTime)`; the previous implementation ignored `CapacityUnits` entirely and tests simulated caps via resource limits. A per-producer parking store keeps the anti-busywork rule (no claiming), gives multi-producer same-resource behavior an unambiguous rule (independent stores; the resource cap arbitrates delivery), and makes "capacity reached" mean one thing everywhere including diagnostics.

**Consequences:**
- Save schema bumped to v2; registered sequential migration `m1-to-v2-producer-stored-milli-units` promotes v1 `carryMilliUnits` (always < 1000) into `storedMilliUnits`. Representative v1 fixtures decode through the real chain; re-encode/re-decode is stable.
- With no resource cap set, delivery always succeeds and the store only ever holds sub-unit remainders — capacity binds when content defines resource caps or sinks (M4 concern). **This deferral was never discharged:** as of 2026-09-29 M4 shipped without defining any resource cap or sink, `ResourceBalances.SetCap` has no production call site (tests only), and `TryConsume` in `src/` only ever consumes Vitality. The bounded-store overflow, no-waste and parked-flush branch is therefore exercised solely by synthetic unit tests and never by the real simulation — all committed pacing reports show `producer capped-store days: 0`. Tracked as a candidate M5/M6 content campaign; see D-046.
- Validator enforces `0 <= StoredMilliUnits <= CapacityUnits × 1000`, locked producers hold nothing, and content validator rejects capacities unrepresentable in checked milli-unit math.

---

## D-033 — Return summaries are durable, typed, bounded re-entry state

**Status:** Accepted *(implemented and automated verified: ReturnSummaryDurabilityTests, M3AmbientProgressionAcceptanceTests)*

**Decision:** Canonical `GameState.PendingReturnSummary` (new additive field, schema v2 payload; absent on old payloads decodes to null which is exactly "nothing pending") stores the committed-but-not-yet-acknowledged summary. Every committing mutation composes its simulation events INTO the pending summary BEFORE persisting, via `ReturnSummaryComposer`: deterministic priority (transformation → actionable decision → production/notice → aggregate), text-level dedupe across merges, and a hard 12-item cap inside the glance budget. Items carry typed kinds (`SummaryItemKind`). A single derived `PrimaryNextAction` ("Queue the next restoration project." when actionable items exist; otherwise null = nothing needs attention). `AcknowledgeReturnSummary()` clears it idempotently and may never alter earned progression. Boot-time advancement summaries regenerate deterministically from checkpoints, so a crash before presentation loses nothing either way; ingestion/credit summaries rely on the durable copy.

**Rationale:** UX_DESIGN §4 makes the summary the central re-entry mechanism; the previous implementation aggregated events into ephemeral strings built AFTER the save, so a crash between commit and display lost the explanation of progress the player had already earned.

**Consequences:**
- Presentation renders read models (`ReturnSummaryReadModel`, Home/Projects/Region snapshots); no UI owns canonical state.
- The item cap evicts lowest-priority entries when new meaningful events arrive while stale ones are still unacknowledged; replayed activity adds notices at most, never fabricated progress claims (acceptance-tested).

---

## D-034 — One platform-neutral activity-source seam; development injector isolated behind it

**Status:** Accepted *(implemented and automated verified: IngestFromSourceTests, walk CLI replay proof)*

**Decision:** `Application.Activity.IActivityRecordSource` is the single seam for activity provenance (fixtures today; Health Connect/HealthKit adapters in M7 behind the same interface). `GameSession.IngestFromSource(source, windowStart, windowEnd)` fetches normalized records and pushes them through the unchanged `IngestActivityBatch` trust pipeline — there is no separate M3 crediting path. The deterministic `SyntheticWalkingSource` lives in `WalkGame.Application.Development`, namespaces all records `dev.synthetic-walking`, derives stable per-day source IDs (replay-safe), and enters production builds only if a composition root explicitly constructs it — which the documented roots never do. `tools/simulation walk --replay` re-feeds an identical window through fresh sessions and fails loudly if anything credits twice.

**Rationale:** Campaign gap: the simulator's multi-day loop bypassed validation/dedup/identity by calling CreditActivity directly. Keeping `credit`/`simulate` as labeled low-level diagnostics while making `walk` the acceptance harness preserves both developer convenience and trust-pipeline honesty.

**Consequences:**
- Retry/replay after restart is safe by construction (durable processed-record ledger), proven by tests rather than asserted.
- Production adapter work (M7) must implement the same narrow port; sources without durable record IDs cannot express corrections (unchanged D-029 requirement).

---

## D-035 — M3 presentation boundary delivered headless; Unity project deferred to a runtime-enabled session

**Status:** Accepted *(implementation reality of this campaign)*

**Decision:** This campaign delivers the complete presentation CONTRACT — Home/Projects/Region read models, queue/auto-advance/manual-start operations, durable summary acknowledgement, and the bootstrap-relevant seams (save store, clock, activity source, session) wired exactly as a Unity composition root will wire them — and proves the full player story through those application boundaries with automated acceptance tests. Creating `src/WalkGame.Unity` is deferred: no Unity 6 LTS editor exists in this execution environment, so a committed Unity project could not be opened, imported, compiled, or PlayMode-tested here, and shipping unverifiable editor YAML would violate the evidence rules (D-018).

**Rationale:** "Never fabricate editor/device evidence" (campaign §3, AGENTS.md honesty gates). Headless-verifiable M3 scope is complete; runtime-only gates are recorded UNVERIFIED, not silently omitted.

**Consequences:**
- The exact Unity 6 LTS version remains unresolved decision #1; the next campaign with an installed editor starts at the Unity shell + EditMode/PlayMode gates, not at domain/application rework.
- Any future presentation technology must consume the same read-model/use-case boundary; nothing in Domain/Application references Unity.

---

## D-036 — M4 canonical state is additive under save schema v2; absent means "nothing yet"

**Status:** Accepted *(implemented and automated verified: M4BackwardDecodingTests, SaveCodecRoundtripTests)*

**Decision:** Discovery runtimes (`RegionState.Discoveries`), expedition runtimes (`RegionState.Expeditions`), region progression stages (`EcologyStage`/`SettlementStage`) and the completion markers (`IsCompleted`/`RegionCompletedAtUtc`) are added as additive schema-v2 payload fields with strict absent-means-default decoding: a missing entry always means "not yet discovered / not yet available / baseline stage / not completed". Entries appear only after their first canonical transition, so pre-M4 payloads and fresh saves share identical semantics without a schema bump or registered migration. `GameStateValidator` validates every present entry against authored content (unknown IDs, review-flag/timestamp consistency, availability-before-completion ordering, arc bounds, completion/milestone consistency).

**Rationale:** The v1→v2 producer migration precedent showed migrations must change semantics; none of the M4 fields reinterpret any existing persisted value, so a bump would add risk without protecting anything.

**Consequences:**
- Backward-decoding tests strip all new properties from a real v2 payload and prove decode + validate + re-encode stability.
- If a future field ever needs non-default history interpretation, it requires a versioned migration like m1-to-v2 — this decision does not create a general exemption.

---

## D-037 — Discoveries and expeditions ship at an M4 headless boundary

**Status:** Accepted *(implemented and automated verified: M4ProgressionMechanicsTests, M4Region1AcceptanceTests)*

**Decision:** A discovery is defined by stable ID, category, title/body/provenance keys, optional location key, and exactly one deterministic trigger: completion of one designated project. Unlock is derived from canonical completion effects, fires at most once, and reviewed state is separate presentation convenience (`MarkDiscoveryReviewed`, idempotent, never gates progression). An expedition is a stable route definition (title/description keys, required projects, required landmark stages, optional one-time integer reward) with deterministic availability (all required projects completed) and completion (all required landmark stages reached) hooks evaluated inside the same canonical effects pipeline; rewards apply cap-clamped in the same state transition as the completion timestamp. Neither system has foreground interaction at M4; both continue while the app is closed.

**Rationale:** GAME_SYSTEMS §7/§8 list richer trigger models (activity thresholds, seeded rolls, visit interactions) that cannot be honestly exercised or validated without runtime presentation. The chosen boundary keeps definitions durable and validation complete while leaving those extensions open.

**Consequences:**
- Adding trigger kinds later extends `DiscoveryDefinition`; the durable unlocked/reviewed state shape is final.
- Expedition cancellation/retry rules are intentionally absent: there is nothing to cancel while routes resolve automatically.

---

## D-038 — Region progression uses two discrete arcs plus one closure project; post-completion is evergreen

**Status:** Accepted *(implemented and automated verified: M4ProgressionMechanicsTests, GameStateValidationTests)*

**Decision:** Region-level restoration state is represented by strictly-ascending discrete stage arcs — ecology and settlement — where each arc stage names the single project whose completion advances it, and by an explicit closure milestone project (`CompletionMilestoneProjectId`). Arcs advance monotonically inside canonical completion effects (never continuous simulation); reaching closure sets `IsCompleted`/`RegionCompletedAtUtc` exactly once and nothing ever resets it. Producers, discoveries and flourishing stages keep operating after closure.

**Rationale:** WORLD_AND_CONTENT §8/§9 and GAME_SYSTEMS §9/§10 demand explainable, presentation-bindable progression without city-builder micromanagement; discrete stages satisfy the attention budget and remain fully validator-checkable.

**Consequences:**
- Arc stage counts are bounded (≤10) and validated ascending with resolvable unlockers.
- `GameStateValidator` rejects completion claims inconsistent with content (flag without milestone, milestone completed without flag, timestamp without flag).

---

## D-039 — Region 1 pacing is measured by deterministic profile simulation; provisional targets documented, low-profile long tail accepted for M4

**Status:** Accepted *(automated verified: `tools/simulation profile`, reports committed under `docs/evidence/m4/`, M4Region1AcceptanceTests byte-determinism)*

**Decision:** Pacing evidence comes from the `profile` CLI verb: a deterministic auto-player (catalog-order queue choice only when fully idle; auto-advance on) drives fresh saves through the real ingestion pipeline under four fixed step patterns — low 3,000/day, moderate 8,000/day, high 20,000/day, irregular [26k,15k,2k,18k,6k,22k,9k]/week — to completion or horizon, printing a stable report (completion day, per-chain vitality/completion days, decisions, queue-empty days, capped-store days, discovery/expedition pacing, final arcs, validator cleanliness). Measured results (seed 42): high completes day 97, moderate day 242, irregular day 139; low completes 12,000 of ~19,400 vitality within 400 days (wetland chain done day 387; forest/research open). Provisional targets: high ≤120 days, irregular ≤250, moderate ≤300 — all met; low-profile completion beyond one year is recorded as an accepted characteristic of a movement-proportional economy, not hidden. Foreground pressure stays minimal everywhere: exactly one queue decision per project (19 total), zero-to-one queue-empty days, zero producer cap waste.

**Rationale:** The campaign forbids inventing thresholds just to pass tests. Publishing measured distributions with explicitly provisional targets keeps balance work honest and gives future tuning (more activity categories, cost scaling) a regression baseline.

**Consequences:**
- Content cost changes require regenerating `docs/evidence/m4/pacing-*.txt`.
 - The low-profile long tail is the top input for any future pacing campaign; it is a design characteristic, not a defect against the no-punishment rule (no progress decays).

---

## D-040 — Recovery commits preserve the last healthy generation

**Status:** Accepted *(implemented and automated verified: PersistenceFaultInjectionTests, SessionPersistenceHardeningTests, M8H1HardeningAcceptanceTests)*

**Decision:** `ISaveStore` gains `WriteAtomicPreservingBackup`: an atomic commit to the primary slot that never rotates the current primary into the backup. The boot-recovery path (`GameSession.Continue` → recovered-from-backup) uses it exclusively. A known-corrupt primary may therefore NEVER displace the last valid backup generation, and after a recovery commit at least two decodable generations exist (backup N−1, primary N′). Interruption windows during recovery leave either the exact pre-recovery state or a strictly better one.

**Rationale:** The previous recovery path reused `WriteAtomic`, whose first step promotes whatever sits in the primary slot — including bytes just proven corrupt — into the backup, destroying the last healthy copy as a safety net; a crash inside that same window could then lose every valid generation.

**Consequences:**
- normal (non-recovery) writes keep rotation semantics unchanged;
- reads classify access-denied/inaccessible paths as IoFailure with detail — "no save found" means genuinely absent; persist paths surface access failures as the documented IOException type;
- boot surfaces specific decode/validation failure reasons instead of a generic unreadable message;
- stale crash temporaries are removed at store construction.

---

## D-041 — Producer runtime rows are mandatory canonical state

**Status:** Accepted *(implemented and automated verified: GameStateValidationTests coverage via validator rule; MatureSaveMigrationTests)*

**Decision:** `GameStateValidator` requires exactly one runtime row for every content producer, mirroring the existing project-runtime rule. Missing rows are corruption and fail closed at load.

**Rationale:** Producer rows are created for the full content set at game start; a silently missing row would permanently disable that producer's production and unlock path while everything else appeared healthy. Unknown EXTRA rows were already rejected; absence was the unguarded direction.

**Consequences:** payloads produced by any factory since M1 always satisfy the rule; hand-crafted or damaged saves missing a row are rejected with a specific diagnostic instead of silently degrading.

---

## D-042 — Durable local UX preferences and onboarding progress live in a separate versioned store, never in canonical game state

**Status:** Accepted *(implemented and automated verified: LocalPreferencesStoreTests, OnboardingAndPreferencesTests, M5H1ShellAcceptanceTests, M5H1ContractHardeningTests)*

**Decision:** Onboarding stage/completion, reduced-motion/haptics/sound flags, notification opt-in + per-category toggles + optional daily reminder time, and diagnostics visibility are **durable local UX state**. They are persisted in a dedicated `IUxPreferencesStore` (file implementation `LocalPreferencesStore`: single JSON file, versioned envelope schema v1, atomic temp+flush+replace write, NO backup generations) and never enter `GameState` or the canonical save envelope.

**Rationale:** Ownership audit (M5-H1 Workstream A): none of these values affect game rules, reward math, queue behavior, or content completion, so canonical ownership would be misclassification; but they must survive restarts, so they cannot be ephemeral. A separate physically-isolated record makes preference corruption unable to touch progression and makes "preference writes leave canonical save bytes identical" provable byte-for-byte. The value of the data is low: one atomic file without backup chains is proportionate; worst case is re-setting preferences, never gameplay loss.

**Consequences:**
- load policy is explicit: absent → NotFound→defaults; malformed/JSON-invalid/wrong-typed/pre-history version → Malformed→defaults; future version → FutureVersion, payload never interpreted; v1 keys merge over documented defaults so missing keys mean default;
- preferences NEVER block boot: any damage degrades to defaults while gameplay loads normally;
- setters fail with stable code `ux.preferences-store-missing` when no store is wired instead of silently diverging;
- onboarding progression is forward-only and idempotent; reaching Complete requires a first project to exist in canonical queue/active/completed state — earned only through real project operations (`EnqueueProject`/`ActivateQueuedProject`), so denial of activity permission can never trap onboarding;
- notification QUIET HOURS are delegated to the operating system (UX_DESIGN §13 mandates respecting system settings); this store persists category toggles and an optional reminder time-of-day only;
- canonical presentation-affecting state that already exists elsewhere (queue auto-advance) is surfaced in `SettingsReadModel` marked canonical — never duplicated into preferences.

---

## D-043 — Activity connection/permission status is an adapter-owned projection behind one platform-neutral port; status never mutates progression

**Status:** Accepted *(implemented and automated verified: ActivityStatusProjectionTests, M5H1ContractHardeningTests, M5H1ShellAcceptanceTests scenarios 2/7/8)*

**Decision:** One narrow port, `IActivityConnectionPort`, reports ephemeral platform truth (permission state incl. partially-granted/revoked, availability, refresh timestamps, adapter-owned technical detail). Player-safe classification is a PURE projection (`ActivityStatusProjector`) over that snapshot plus two canonical facts: processed-record existence, and the durable last-ingestion outcome. The six standing states use UX_DESIGN §5 vocabulary with documented precedence: denied/revoked → permission-denied; not-requested → permission-needed; unsupported provider → source-unavailable; transient adapter failure or durable fetch-failure → refresh-temporarily-failed; connected with zero processed records → waiting-for-first-data; otherwise connected-current. "Data processed successfully" rides separately as a last-outcome fact so standing state and event outcome are never conflated.

**Rationale:** Before M7 delivers Health Connect/HealthKit adapters, the shell needs honest, testable player-facing source state without native SDKs or fake device claims. Future adapters implement exactly this port; conformance is pinned by table tests today.

**Consequences:**
- raw exceptions/messages can never become ordinary player copy: read models carry enums, counts, timestamps only; adapter technical detail surfaces exclusively through the bounded diagnostics projection;
- status reads are side-effect free and byte-neutral (proven against save bytes);
- external revocation/grant changes are representable at any moment by swapping snapshot values; earned progress is untouched;
- a failed adapter fetch durably records `IngestionOutcomeState` with `SourceFetchFailed` and an error category equal to the exception TYPE name, in the same atomic discipline as successful batches, then rethrows — callers keep existing semantics while the shell gains cross-restart "temporarily unable to refresh";
- no HC/HK SDK, OS permission dialog, or device verification exists in this scope; those remain UNVERIFIED runtime items.

---

## D-044 — Support diagnostics expose only privacy-safe operational facts in one bounded projection

**Status:** Accepted *(implemented and automated verified: DiagnosticsReadModelTests, M5H1ContractHardeningTests leak sweep)*

**Decision:** `DiagnosticsReadModel` is the single support-oriented surface: boot outcome classification (including recovered-from-backup and the structured codec/state-validation failure category of the last failed decode, retained monotonically so a successful backup recovery cannot erase primary-failure evidence), applied migrations, schema version, region identity, ingestion checkpoint watermark plus age in days (reported only after a real batch sets it), processed-record count, lifetime credited vitality, forever-visible unapplied-reversal vitality (D-029), the bounded last-batch counter row, the preferences-load outcome, and optionally the adapter-owned bounded technical detail string. Display gating uses the local DiagnosticsVisible preference.

**Rationale:** ACTIVITY_PIPELINE §19 and RISK_REGISTER R-022 require redacted bounded diagnostics; players and support need operational facts after crashes, which demands durability across restarts and availability even when boot FAILED.

**Consequences:**
- no raw records, payloads, stack traces, or exception messages generated by this codebase ever appear; error categories are stable type/class names;
- every text field from adapters/stores is truncated (300 chars max) and lives behind the explicit diagnostics gate;
- reading diagnostics is side-effect free and available pre-boot-success (schemaVersion 0 / empty identity until state exists);
- aggregates stay permanently bounded (single counter row) honoring PERFORMANCE_BUDGETS memory/save rules.

---

## D-045 — Systemic performance pass: cached content/validation, decode fast path, durability-preserving commit I/O

**Status:** Accepted *(automated verified: full test suites, guard proof suite 25/25, `bench` phase harness, longhaul/profile/walk outputs byte-identical to committed evidence)*

**Decision:** Four optimizations on the boot/ingest/commit hot paths, none changing canonical semantics:
1. `Region1Catalog.Create()` returns one process-wide instance. Every definition is constructor-frozen (get-only properties), so rebuilding a 20 KB immutable content graph per session — once per boot and once per simulated app-closed day — was pure duplicate work.
2. `GameSession` validates content at most once per distinct content instance (`ConditionalWeakTable`, atomic per-key `GetValue`). Invalid content still fails closed on every construction; valid graphs skip re-walking reachability/cycle analysis.
3. `SaveCodec.Decode` deserializes current-version payloads directly from payload bytes. The JsonNode DOM materialization is now paid only when a migration actually needs to transform the tree (v1→v2 path unchanged).
4. Commit I/O keeps `Flush(flushToDisk:true)` as THE durability barrier and drops only `FileOptions.WriteThrough` (redundant behind an explicit FlushFileBuffers on a single buffered write); the backup rotation copy streams instead of buffering the whole save in memory.

**Rationale:** Phase-level measurement (`tools/simulation bench`) showed decode ≈6× encode (DOM double-parse) and ~0.13 ms/session of duplicated catalog+validation work. Longhaul 365-day wall time improved ≈27% (≈6.95 s → ≈5.1 s median) with identical final state bytes, ledger sizes and pacing reports; decode −51%, session construction −58%.

**Consequences:**
- rejected: rename-chain backup rotation (would widen the primary-empty crash window and force boot-fallback semantic changes in the durability core for ~1 flush/commit);
- rejected: skipping the boot persist when no events fired (producer integer milli-unit truncation makes tick granularity observable; risk without measurable gain);
- deferred: processed-record/reward-ledger compaction for save-size bounding (PERFORMANCE_BUDGETS §14–15) — late deletions target ledger rows without horizon checks, so compaction changes deletion semantics and requires a product decision;
- deferred: STJ source generation — projected single-digit ms/boot gain does not justify serializer-mode drift risk while reflection mode remains correct;
- `bench --save <dir> [--iterations N] [--days N]` stays available as the regression-measurement entry point.

---

## D-046 — Pacing evidence covers all seven GAME_SYSTEMS §13 profiles; intermittent engagement is recorded as an open balance finding

**Status:** Accepted *(automated verified: seven reproducible reports, SHA-256 byte-identical on re-run; the four pre-existing M4 reports unchanged)*

**Decision:** `tools/simulation profile` resolves its `--profile` name through a `PacingProfile` table covering all seven profiles named by GAME_SYSTEMS §13 and repeated by R-013: `low`, `moderate`, `high`, `irregular` (pre-existing), plus `weekend-heavy`, `inactivity` and `pathological` (added). A profile resolves a day index to a synthetic walking quantity, or 0 to model a day on which the source returned **no record at all** — never a zero-quantity record, which the trust pipeline rejects by design. The four original profiles keep their exact positional rotation so the evidence committed under `docs/evidence/m4/` still reproduces byte-for-byte. `weekend-heavy` keys off the real calendar weekday rather than array position so it stays correct for any `--at` instant.

**Rationale:** Only four of the seven profiles existed, so R-013's mitigation was half-implemented and GAME_SYSTEMS §13 was unmet. Two of the new profiles changed the balance picture:

- `pathological` completes the region on **day 29** from 3,184,633 ingested steps yielding 21,844 Vitality — ≈**686 steps/Vitality** against the nominal 100. The D-030 pathological-quantity clamp is doing real work; "100 steps → 1 Vitality" is a ceiling, not a guarantee.
- `inactivity` (silent weekends plus a fully silent day 100–144) **does not reach the closure milestone within 400 days** — forest and research chains stay incomplete while both arcs reach 4/4.

**Consequences:**
- committed evidence `docs/evidence/m4/pacing-weekend-heavy.txt`, `pacing-inactivity.txt`, `pacing-pathological.txt`, each reproducible by SHA-256;
- the intermittent-engagement shortfall is **recorded, not silently re-balanced** — changing chain costs or Vitality awards would invalidate the four previously committed M4 pacing reports and the D-039 provisional targets, which is a product-balance decision belonging to the M5/M6 milestone;
- silent days still run the full canonical day loop (offline advancement, queue policy, production), so an absent player is simulated rather than skipped.

---

## D-047 — Architectural layering and the exactly-once invariant are CI-enforced, not aspirational

**Status:** Accepted *(automated verified: 6/6 architecture assertions green on the real tree; every rule proven to fail against a deliberately-broken fixture; `walk --replay` reports `vitality credited: 0` / `duplicate records ignored: 16`)*

**Decision:** Two new mechanical gates replace documented-but-unenforced intentions.

1. `scripts/verify-architecture.sh` enforces the layering: Domain references nothing and targets `netstandard2.1`; Application references only Domain; Infrastructure references only Application + Domain; no engine namespace appears anywhere in `src/`; Domain and Application perform no filesystem access. Comments are stripped before the filesystem scan, so prose such as "no separate code path." cannot produce a false positive. It is the countermeasure for R-017 and implements the mechanically-verifiable subset of TECHNICAL_ARCHITECTURE §23.
2. CI now runs `walk` then `walk --replay` and **asserts** the replayed window credits zero and reports 16 duplicates. The repository's flagship invariant previously had no CI gate whatsoever. CI also runs `longhaul --days 365` (§7 offline simulation was documented but never executed in CI) and a Release `dotnet publish` build smoke (TESTING_AND_RELEASE §16 gate 9).

**Rationale:** §16 listed ten target CI gates and implemented six; R-017's "dependency checks" mitigation bullet had no enforcement behind it, and the exactly-once property that defines this product was proven only by locally-run evidence documents.

**Consequences:**
- a `dotnet format --verify-no-changes` gate was **deliberately not adopted**: it fails against the existing tree and adopting it would force mass reformatting churn in files unrelated to any risk. The tree is held at 0 build warnings instead;
- each rule was verified to *fail* against a deliberately-broken throwaway fixture (injected `UnityEngine` using, `File.AppendAllText` in Application, a `net9.0` Domain, a Domain→Infrastructure reference) — a gate that has only ever passed proves nothing;
- `verify-architecture.sh` exits 2 with a clear message when run outside the checkout, rather than silently scanning the wrong tree.

---

## D-048 — Performance regression scenarios and save-size growth are measured and gated; the remainder is device-blocked

**Status:** Accepted *(automated verified: 5 of 10 PERFORMANCE_BUDGETS §19 scenarios measured headlessly, 1 by explicit surrogate, 4 deliberately not simulated; save-growth series reproduces exactly across independent runs; 0 validator violations at maximum content)*

**Decision:** `tools/simulation scenarios` implements the headless-measurable subset of PERFORMANCE_BUDGETS §19 and the §15 save-growth obligation that nothing previously performed. It emits two line classes:

- `SCENARIO-RESULT` carries wall-clock and status. **Not gated** — machine-dependent, and TESTING_AND_RELEASE §17 treats flaky timing assertions as defects.
- `SCENARIO-SIZES` carries save bytes, ledger sizes, credited totals and resource balances. Deterministic for a fixed seed and content, and therefore gated by `scripts/verify-save-growth.sh`, which runs the harness twice and requires exact reproduction.

**Rationale:** §19 named ten scenarios with none implemented, and §15 stated a CI obligation that was never met. Growth existed only as prose inside `docs/evidence/m8-h1/`.

**Consequences:**
- §19 is now **5/10 measured headlessly, 1/10 by surrogate (background/resume, which repeated boot-from-disk is *not*), 4/10 not simulated (Home idle, Visit World enter/traverse/exit)**. No figure was invented for the four device-only scenarios; a fabricated number would be worse than an acknowledged gap;
- scenario 6 is measured for its **canonical** cost only — a landmark stage transition in canonical state, never its rendered frame cost;
- save growth is measured **linear and unbounded at ~525-530 bytes per ingestion day** (day 7: 10,344 B; day 365: 202,644 B; day 730: 394,401 B), corroborating the ~554 B/day figure in `LONG_HORIZON.md`. Region completes at day 365 and stays stable through day 730, so post-completion growth is pure ledger accumulation — the shape D-045 deferred;
- the same run **quantifies the D-032 deferral**: at day 730 a save holds 2,604 Materials and 638 Knowledge with no consumer anywhere in `src/`, so producer capacity still can never bind;
- adding project secondary-resource requirements is explicitly NOT done here. It alters progression balance, and `ContentValidator`'s critical-path reachability would have to prove a project is *schedulable* given what its unlock dependency actually produces, or it can certify a reachable graph that deadlocks in play. This campaign produced the measurement series such a change must be judged against, and stopped;
- the harness also corrects a misleading claim: an incremental `dotnet build` reports 0 warnings, but a clean `--no-incremental` build reveals 8 pre-existing xUnit analyzer warnings in test code. Production code is genuinely at 0 warnings. TESTING_AND_RELEASE now records the distinction so "0 warnings" is never cited without qualification;
- evidence: `docs/evidence/performance/SCENARIOS.md`.

---

## D-049 — The complete CI gate is mechanically reproducible locally while hosted runners remain blocked

**Status:** Accepted *(full ci.yml body reproduction at `3b5d3e0`: 12 ok / 2 hosted-only SKIP / 0 failed; 295/295 tests; all four blocked runs confirmed `steps: 0` via gh)*

**Decision:** `scripts/verify-ci-steps.sh` parses `.github/workflows/ci.yml` mechanically and executes every `run:` step body locally, in checkout order, under the GitHub Actions Linux default shell (`bash --noprofile --norc -eo pipefail`), honoring `continue-on-error` as WARN, exporting `GITHUB_REPOSITORY` and a CI-style `RUNNER_TEMP`, and reporting hosted-only `uses:` steps as explicit SKIPs backed by asserted local facts. The extractor is guarded both directions: extracted run/uses counts must equal the workflow's declared occurrences, and an empty extracted body is a hard failure — parser drift can only abort loudly, never report a hollow success.

**Rationale:** no workflow run has progressed since `33293682455` (2026-08-30); the account-level billing/spending block (runs annotated "recent account payments have failed or your spending limit needs to be increased") holds C1–C3 and HEAD runs at `steps: 0`, and the repository's documented position (TESTING_AND_RELEASE §16) is that hosted CI is an external blocker awaiting an account owner while every gate remains locally reproducible. Release confidence cannot wait indefinitely on billing support, and what is needed is not the hosted *platform* but the hosted *gate* — and that gate is defined by the text of `ci.yml`.

**Consequences:**
- the reproduction tool now lives beside the workflow it reproduces; its hollow-pass guard proved necessary during its own development — the first two runs falsely reported 12/14 ok because step keys at 8-space indentation were parsed against 6-space patterns, and every "empty script" exited 0 (miss recorded in `docs/evidence/ci-local/CAMPAIGN-2026-09-29.md`);
- a local reproduction is a reproduction, not hosted CI: Linux-runner environment behavior remains unverified until the block lifts, and the first hosted green run must be diffed against this package's recorded results;
- `uses:`-step assertions and the publish-apphost existence check follow MSYS `-x` semantics locally; equivalence with the Linux ELF apphost check was verified by inspection, not execution;
- evidence: `docs/evidence/ci-local/CAMPAIGN-2026-09-29.md`.

---

## D-050 — Release-evidence registers (known issues, privacy review) are living documents, headless-scoped

**Status:** Accepted *(all mechanical claims verified by recorded commands at `e2ea00a`; runtime/device halves explicitly reserved UNVERIFIED)*

**Decision:** The two §18 release-package artifacts that can be produced without runtime, devices, or hosted CI are established as durable, versioned documents instead of one-off release attachments: `docs/KNOWN_ISSUES.md` (severity register per §15; any newly accepted-open defect, gap, or verification hole gains an entry; Critical/High entries block release qualification by rule) and `docs/PRIVACY_REVIEW.md` (every claim backed by a re-runnable command: zero network APIs in sources, single runtime NuGet dependency, save-content inventory, host-injected storage paths, test-enforced privacy-safe diagnostics). Both are scoped to the headless lanes; no runtime/device privacy or permission claim is made.

**Rationale:** M9 deliverables list both artifacts; neither requires the blocked lanes; a living register makes externally blocked items (KI-001 hosted CI, KI-002 runtime) impossible to forget and locally accepted risks (plaintext health-adjacent saves) impossible to hide. Encoding privacy mechanically (grep-verifiable absence of network surface, test-enforced diagnostics safety) matches the repository evidence culture far better than prose assurances.

**Consequences:**
- two Medium findings are now formally open and must be dispositioned before M9: no explicit privacy requirements section in PRODUCT_SPEC (KI-005) and plaintext at-rest save data with OS encryption as the only shield (KI-006);
- tooling/process items KI-007 (fixture hook env coupling) and KI-008 (stranded writer leases from killed foreground suites) join the backlog with evidence;
- when the runtime/device lanes unblock, both documents require a second pass; until then they bound every privacy claim to the headless code;
- evidence: `docs/PRIVACY_REVIEW.md`, `docs/KNOWN_ISSUES.md`.

---

## D-051 — The build toolchain is pinned, shared settings are declared once, and a warning is an error

**Status:** Accepted *(verified at the F-12 hardening pass: `dotnet build --no-incremental` 0 errors / 0 warnings; `dotnet test` 295/295 unchanged; `scripts/verify-architecture.sh` 11 passed / 0 failed with the netstandard2.1 rule proven to fail on a broken fixture; `scripts/verify-ci-steps.sh` 12 ok / 0 warned / 0 failed / 2 hosted-only SKIP)*

**Decision:** three repository-root files become the single declarations site for the
build toolchain. `global.json` pins the SDK to `9.0.100` with `rollForward: latestFeature`
and `allowPrerelease: false`; `Directory.Build.props` carries the universal settings
(`Nullable`, `TreatWarningsAsErrors=true`, `WarningLevel`, `Deterministic`) and is imported
explicitly by `src/Directory.Build.props`, which layers the production C# 9.0 language pin
on top; `Directory.Packages.props` enables central package management and declares every
package version once. `ci.yml` installs the pin with `actions/setup-dotnet`'s
`global-json-file:` instead of a hardcoded `9.0.x`, and `scripts/verify-ci-steps.sh`
asserts that the local SDK satisfies `global.json` — failing loudly if the pin file is
missing, if the workflow stops declaring it, or if no installed SDK satisfies it. The eight
pre-existing xUnit analyzer warnings were **fixed** (assertion shape only) and the
warnings-as-errors policy was then switched on for every project, with no `NoWarn` and no
exemptions.

**Rationale:** §14 requires a "documented toolchain version" and it was satisfied only by
prose ("9.x today"), which is not checkable. With hosted CI blocked (KI-001), local
reproducibility is currently the *only* reproducibility story this repository has, so an
unpinned SDK and a build where "0 warnings" depended on incremental caching were not
acceptable debt. The policy ordering matters and was deliberate: the warnings-as-errors
switch was only turned on *after* the eight known warnings were genuinely fixed, because
suppressing them to make a policy satisfiable is exactly the failure mode the
unenforced-warnings arrangement had already produced. `latestFeature` rather than an exact
pin keeps onboarding honest — a contributor without the exact patch builds fine, while a
contributor on a different major gets a self-explanatory refusal instead of a subtly
different binary.

**Consequences:**
- each shared setting now has exactly one declarations site; adding a `Version` to any `PackageReference` is an error (`NETSDK1206`), so the three test projects can no longer drift apart;
- `dotnet build` means the same thing with and without `--no-incremental` for new commits. The old caveat ("never cite 0 warnings without `--no-incremental`") remains true of **evidence recorded before this change** and was left in place there deliberately;
- **the architecture gate was rewritten, not left alone.** It previously grepped `<TargetFramework>netstandard2.1</TargetFramework>` out of each csproj, so centralising the property would have silently hollow-passed the rule on all three layers. It now reads the **effective** MSBuild property (`dotnet msbuild <proj> -getProperty:TargetFramework`), fails closed if that read is unavailable, and self-checks the reader against a project that is legitimately `net9.0`. Proven by two broken fixtures: moving `WalkGame.Domain` to `net9.0` in the csproj fails the rule, and moving it to `net9.0` via `src/Directory.Build.props` (csproj emptied of the property) fails it identically — which is the case a grep would have missed. The healthy count remains 11 passed / 0 failed;
- `TargetFramework` deliberately stays local to each production csproj. It is the one property whose declaration site is itself part of the architecture contract, and keeping it visible keeps the gate simple and honest; `LangVersion` did not survive the same reasoning because the net9.0 projects legitimately need C# 10+ syntax (file-scoped namespaces in the test suite);
- `TreatWarningsAsErrors` applies to the test projects too. An alternative that gated production only was rejected as a needless asymmetry: a warning in a test is precisely how a test rots;
- `dotnet format` remains deliberately **not** adopted as a style gate. The eleven over-length lines in `ci.yml` are pre-existing (identical count before and after this change, zero introduced) and the `run:` bodies cannot be reflowed without changing what CI executes, which is the exact drift `verify-ci-steps.sh` exists to prevent. That decision is not reopened here and is recorded rather than silently ignored;
- the eight analyzer fixes are recorded in `docs/evidence/quality/CAMPAIGN-2026-09-30.md`;
- a fully hermetic build (container-pinned toolchain) is still disproportionate for a single-developer-quality repository and stays on the deferred list.

---

## D-052 — Boot addresses both save generations before concluding no save exists, and the outcome taxonomy is exhaustive

**Status:** Accepted *(verified at the F-01 pass: 8 new application-layer regression tests; CLI reproduction before/after; `dotnet test` 303/303 — Domain 105, Infrastructure 48, Application 150 — green; `verify-architecture.sh` 11/0; pacing evidence 7 reproduced / 0 drifted; save-growth verified; guard suite 25/0)*

**Decision:** `GameSession.Continue()` addresses the primary and backup generations
**independently**. `StartStatus.NoSaveFound` is produced only when **both** probes classify
as absent, and that path performs no write. A decodable backup is preferred over reporting
absence for **every** reason the primary is unavailable, including the primary file being
absent entirely. The cause is then modelled, not the symptom: a new
`SaveRecoveryCause` (`None` / `PrimaryDamaged` / `PrimaryMissing`) and a new
`DiagnosticsBootOutcome.RecoveredFromMissingPrimary` distinguish a crash-window primary
loss from a damaged primary, while `StartStatus` deliberately gains no new value — to a
caller, recovering a world is recovering a world, and the finer distinction belongs in the
support projection. In the same pass `StartStatus.StateInvalid` and
`DiagnosticsBootOutcome.StateInvalid` were wired up for the decoded-but-invariant-violating
case, making every declared outcome value reachable and the taxonomy non-overlapping;
`tools/simulation` now returns exit 2 (documented "domain/validation failure") for that
case instead of 3 ("save missing/unreadable"). Recovery commits continue to route through
the existing `ISaveStore.WriteAtomicPreservingBackup`; no store method was added. The
recovery notice is parameterised on the cause, because "your latest save was damaged" is
untrue when the file was simply missing, and an inaccurate notice on a recovery is how a
player concludes their world was corrupted and starts over.

**Rationale:** the store had been correct all along — `AtomicFileSaveStore.ReplaceFile`
promotes a generation with a delete-then-move pair and its own comment documents that a
crash between the two steps leaves the previous generation in backup, leaving the primary
**transiently absent**. That is a reachable state, and the boot flow classified it as "no
save exists" without reading the healthy backup. Any composition root treating "no save" as
a fresh profile then overwrote the only surviving copy: total loss of the player's world,
in the flagship product invariant. The deepest lesson is not the missing `if`; it is that
**a mitigation existing is not a mitigation being exercised**. `WriteAtomicPreservingBackup`
(D-040) protected the last healthy generation perfectly — on a code path no caller took,
and a store-layer test asserted the exact on-disk state while the application layer had
none. That is why the new tests are application-layer, driving `Continue()`.

**Consequences:**
- the behaviour change is visible and deliberate: an invalid-state save now exits 2, not 3. `MatureSaveMigrationTests` asserted the old misclassification for two payload-surgery cases (an unknown content id decodes cleanly, then fails validation) and was updated to the precise class — the fail-closed and untouched-bytes assertions were kept unchanged;
- a *deliberately* deleted primary now presents to a player as a recovery rather than an error. That is the correct default for a game whose promise is "your world is never destroyed", and the distinction is available to support via `BootRecoveryCause` / `RecoveredFromMissingPrimary`;
- the player-facing no-leak sweep is now exhaustive by construction — `GetDiscoveries`, `GetExpeditions` and `GetPendingReturnSummary` were unwatched, i.e. the one test whose purpose is preventing a leak class had three unwatched surfaces (F-17);
- boot-outcome evidence stays in the session object rather than being persisted: a second write during a recovering boot is the wrong trade, and no other session would ever read it;
- no store write sequence, no save schema, no envelope framing, no migration chain was touched. Rollback is a plain revert;
- multi-generation history and a write-ahead journal remain **deferred**, not open: they reduce the probability of the state, whereas this change makes the state recoverable. Recorded in the master plan's deferred list so the next agent does not re-investigate;
- `docs/KNOWN_ISSUES.md` gains a "Closed" section and an explicit note that the register held **no entry** for this Critical-class data-loss path despite it being present, reproducible and unrecoverable. A register that cannot hold a Critical defect is itself the finding.

---

## D-053 — The activity acceptance window is anchored to trusted time, and deletion markers obey the same eligibility rules

**Status:** Accepted *(supersedes D-030's constants and reference point; verified at the F-02/F-03/F-09 pass: 17 new tests in `TrustedTimeWindowTests`, `dotnet test` 322/322 — Domain 105, Infrastructure 48, Application 169 — green; all seven `docs/evidence/m4/pacing-*.txt` reports still reproduce **byte-for-byte**; `verify-architecture.sh` 11/0; save-growth verified; guard suite 25/0)*

**Decision:** `ActivityValidationPolicy.Validate` now takes a `TrustedTime` reference
instead of a bare `nowUtc`, and stays `static`, side-effect free and **O(1) per record**
with no dependency on `GameState` — required, because `verify-architecture.sh` rule 5
keeps Domain free of ambient state. `TrustedTime` is computed **once per batch** in
`IngestActivityBatch` and carries the injected clock, the latest durably trusted record
end, and the reconciliation watermark. The window is:

| Bound | Value |
|---|---|
| lower | `min(watermark, latest trusted end)` − **`TrustedWindowFloor` = 45 days**, falling back to `now` when a profile has no trusted progress at all |
| upper | `max(now, watermark, latest trusted end)` + `MaxFutureSkew` = 10 minutes |

The eligibility-first restructure also applies the **same eligibility rules to deletion
markers**, which previously bypassed validation entirely and could push the watermark
arbitrarily far forward. `DeletionsIgnored` / `StaleRevisionsIgnored` are retained as
*policy* outcomes (unknown logical record, stale revision); eligibility failures are now
*rejections* with a named reason.

**Rationale:** D-030's window was anchored to raw device wall clock, and both of its
anchors were wrong for the situations this product exists for. A player returning after a
holiday had their **entire** provider backlog rejected as `OutsideHorizon` (F-02) — the
14-day bound was introduced to protect the offline-first thesis (D-003/D-004) and instead
destroyed it. A device clock **minutes** fast made every record future-dated and stopped
crediting altogether, with no player-visible explanation (F-03); `GAME_SYSTEMS.md` §15
already required not to rely solely on device wall clock. The problem was never the width
of the constants: a wider fixed window is still wrong for a fast clock and still too narrow
for a 45-day absence. **The anchor was the defect.**

**The 45-day floor — how it was chosen.** It answers "how far *before* the last thing we
trusted will we still believe a record?". It comfortably exceeds the longest absence the
product treats as normal (a 30-day return is the acceptance case, `MASTER_PLAN` §10) and
stays far below unbounded device history. It does **not** bound the length of an absence:
records *after* the last trusted end are inside the window regardless of how far back that
trusted end is, which is precisely what makes a 30- or 45-day absence creditable.

**Why both bounds use `min`/`max` rather than a single anchor.** The lower bound takes the
*earlier* durable marker so a watermark pushed forward by a late deletion cannot silently
expose older history; the upper bound takes the *later* so a fast clock degrades the
anti-future bound instead of disabling crediting, and so a backward clock jump can never
pull the bound below the already-trusted end. The watermark itself remains monotonic and
never regresses.

**Consequences:**
- **`ReconciliationHorizon` (14 days) is retained as a name only** and is no longer used by
  validation, so the historical decision stays locatable in review while the value it
  described is not silently resurrected. `TrustedWindowFloor` is the live constant.
- **Two re-scoped tests, neither deleted.** `RecordBeyondReconciliationHorizon_IsRejectedWithoutSideEffects`
  now uses a 60-day-old record, because a 15-day-old record is *correctly accepted* under
  the new window — keeping the old fixture would have turned an anti-abuse requirement into
  a dead test. A companion test asserts the other half: a 30-day-old record **is** accepted.
  `ActivityRedTeamTests`' `junk-stale` fixture moved from −40 to −120 days for the same
  reason. `TemporalAnomalyTests.HorizonAndSkewBoundaries` now re-derives both edges from
  the policy constants and uses a fresh profile per case, because accepting an edge record
  advances the watermark and would otherwise move the window mid-test.
- **An already-trusted out-of-window record is reported as a duplicate, not a rejection**,
  guarded by a revision check. Without it, replaying a long horizon would report the
  oldest days as `OutsideHorizon` and the flagship exactly-once evidence
  (`M4Region1AcceptanceTests`) would read as refused activity. The revision guard is what
  makes it incapable of minting value: an out-of-window record with a **higher** revision
  is still rejected, so it can never reach the correction path.
- **A deletion marker skips exactly one eligibility rule: the quantity checks.** A marker
  carries no activity by definition and every well-formed marker in this repository has
  `Quantity == 0`; applying `ZeroQuantity` would reject every deletion ever issued and
  silently disable the correction policy — the opposite of "the same eligibility rules".
  Provenance, unit, temporal extent and both window bounds all still apply.
- **This departs from the change's own `design.md` in one place.** The design suggested a
  deletion marker with an empty provider namespace would remain an "ignored" *policy*
  outcome; the change's **spec** says a marker with "a missing provider namespace" is
  "rejected under the corresponding eligibility reason". The spec is normative, so the spec
  wins. Both classifications are equally safe (neither reverses value nor moves the
  watermark) and the spec's is strictly more informative. Recorded here rather than
  diverging silently, per the master plan's rule 11.
- **The batch accounting identity is now asserted in a test** with every counter exercised
  at once: `accepted + rejected + duplicates + corrections + deletionsApplied +
  deletionsIgnored + staleRevisions == total received`. Per-reason counts remain **on the
  result object only**; persisting them is the follow-up diagnostics change and is
  explicitly out of scope here.
- `DateTimeOffset` arithmetic is not saturating, and `MinValue − 45d` throws. `TrustedTime`
  guards both operations against the distance available to the representable limit, so a
  profile whose trusted progress sits at the minimum degrades to "reject everything"
  instead of throwing during validation. (Found by the new tests, not by inspection.)
- **No conversion, clamp, dedup identity, correction policy or save schema changed.** All
  seven byte-reproducible pacing reports are unchanged, which is the strongest available
  evidence that the window change did not perturb simulation behaviour.
- Still **open and not claimed**: real Health Connect / HealthKit backlog-on-reconnect
  behaviour and backfill limits are the M7 adapter lane and remain device-blocked
  (`KI-002`). The reconciliation watermark is still *evidence*, not a driver of scheduled
  query windows; making it a driver is the adapter's job and stays deferred.

---

## D-054 — Secondary resources carry authored caps and a real consumption decision

**Status:** Accepted *(verified at the F-06 pass: 29 new tests — 17 in `SecondaryResourceEconomyTests` (Domain), 12 in `SecondaryResourceEconomyIntegrationTests` (Application); `dotnet test` 351/351 — Domain 122, Infrastructure 48, Application 181 — green; all seven `docs/evidence/m4/pacing-*.txt` reports still reproduce **byte-for-byte**; `verify-architecture.sh` 11/0; save-growth verified; guard suite 25/0)*

**Decision:** Region content gains two pieces of economy contract. (1) A per-resource
**cap** map on `RegionDefinition`, applied by `GameFactory.NewGame`, with
`ContentValidator` rejecting (a) any resource a producer can emit that has no authored cap,
(b) a non-positive cap, and (c) a cap whose derived store capacity is unrepresentable.
`GameStateValidator` reports a balance above its content cap and **never clamps it on
load** — the same fail-closed posture every other invariant takes. (2) An optional
per-resource **requirement** on `ProjectDefinition`, consumed all-or-nothing via
`TryConsume` when the project is *activated*, before any Vitality is streamed. Activation
with an unmet requirement returns a new stable code
`project.resource-requirement-not-met` naming the resource and the shortfall, and consumes
nothing. Auto-advance checks the head of the queue and **stops** rather than promoting a
later project past it, so Vitality keeps banking.

**Authored Region 1 values:** caps **Materials 400 / Knowledge 300**; requirements
**Restore Market Hall 40 Materials** (settlement chain) and **Calibrate Survey Rig 20
Knowledge** (research chain) — two distinct chains, two distinct currencies.

**Why 400/300 and 40/20 — measured, not guessed.** Both caps were run to a 730-day
`longhaul`: Materials settles at exactly 400 and Knowledge at exactly 300, i.e. **both
caps bind**, because production outruns consumption. A cap a real profile never reaches
would be decoration. The requirement sizes were chosen empirically against the committed
pacing evidence: 60 Materials / 40 Knowledge pushed the settlement chain out by up to 6
days and the research chain by up to 9 (high 97→101, pathological 29→38) and drifted
**five of seven** profiles. At **40 / 20 all seven profiles reproduce byte-for-byte**, so
the mechanic is real (a player must wait for production) and the balance cost is zero.

**Vitality is deliberately NOT capped — a documented departure from the task list.**
Task 1.5 asked for a Vitality cap "at or above the maximum balances the committed evidence
produces". Implementing it would have been a **data-loss bug**, not a balance:
`RewardLedger.Apply` *ignores* the value `balances.Add` returns and records the **full**
transaction amount in the ledger, and Vitality has no producer store to park refused units
in. A Vitality cap would therefore silently discard real walking credit — the exact defect
class this campaign exists to remove. The cap requirement is scoped to resources a producer
can emit, and no producer emits Vitality. Recorded here rather than implemented.

**The real defect, restated.** The cap/parking machinery was fully implemented, fully
tested (`ProducerSimulationTests` proved parking and zero-elapsed flush both work), and
**completely unreachable in production**: `SetCap` had zero call sites outside tests, so
`GetCap` always returned `long.MaxValue`, so the `hitResourceCap` branch never fired and
two currencies grew forever with nothing to spend them on. The `ProducerProduced` event
also conflated "the producer's own store filled" with "the resource is at its cap"; the two
are now separate flags, because only the second is relieved by the player spending
something, and only the second needs telling about — otherwise "nothing happened" and
"your output is waiting" look identical.

**Consequences:**
- **No pacing evidence was regenerated.** The change anticipated it, but sizing the
  requirements empirically made it unnecessary. All seven reports are byte-identical,
  which is strong evidence the mechanic was added without perturbing balance.
- `EnsureActiveSlot` now removes the queue entry only after the affordability check, so a
  stalled project is not silently dequeued.
- `ProjectRow.RequirementsAffordable` and `ProducerRow.CapacityLimited` are computed from
  canonical state at read time and are pure projections; the shell renders, the domain
  decides.
- The requirement is a gate the player *chooses* to satisfy. Vitality is not: a Vitality
  shortfall is not an error, it banks until affordable. Keeping those two visibly
  different is deliberate.
- Content validation now requires that a requirement be satisfiable from the region's own
  production within the 400-day design horizon, so a project cannot be authored
  permanently unactivatable. The check uses production *rate*, not store size: parked units
  flush repeatedly, so total deliverable is bounded by rate × horizon.
- `ProducerSimulationTests.Tick_ResourceCapExhausted_ParksSurplus_InBoundedStore` now
  asserts `HitResourceCap` rather than the old conflated `HitCapacity`; the parking and
  flush assertions are unchanged.
- `docs/RISK_REGISTER.md` R-014 and `docs/GAME_SYSTEMS.md` §3/§6 updated. No new
  `KNOWN_ISSUES` entry: nothing open was invalidated.
- **Still open and not claimed:** whether a cap value is the right *number* for a real
  player is a balance judgement no headless test can settle; the seven profiles are
  unchanged, but a cap that binds at day ~300 tells us nothing about how it feels at day
  30. A UX pass on the capped-storage message is the M5 shell's call.

---

## D-055 — The durable return summary rotates per kind, items carry currency, and attention expires

**Status:** Accepted *(verified at the F-05 pass: 13 new tests in `ReturnSummaryRotationTests`; `dotnet test` 364/364 — Domain 122, Infrastructure 48, Application 194 — green; all seven `docs/evidence/m4/pacing-*.txt` reports still reproduce **byte-for-byte**; `verify-architecture.sh` 11/0; save-growth verified; guard suite 25/0)*

**Decision:** `ReturnSummaryComposer` retains items under a **per-kind bound table**
(Transformation 5, ActionableDecision 3, Production 2, Notice 1, Aggregate 1 — summing to
exactly the existing 12-item hard bound, which is unchanged). When a kind is full its own
oldest item is retired, so newer facts of the same kind can appear. A lower-priority item is
**never** promoted above a higher-priority one to make room. `PendingSummaryItemState`
gains an additive `FirstCommittedAtUtc`; items carried over keep their **original** commit
time. `GetHome()` claims attention only while the summary holds at least one **current**
item, where current means committed within `ReturnSummaryReadModel.CurrencyThreshold` =
**7 days**; `HasPendingSummary` stays true regardless, so a historical summary is still
readable.

**Rationale.** The summary only ever grew. Every batch merged new items into the pending
ones, the merged list was sorted by kind, and the first 12 survived; nothing was ever
removed except by an explicit acknowledgement. So for a player who does not tap "dismiss" —
precisely the busy player this product is designed for — the summary saturated within
roughly the first dozen region events, after which every new Transformation item was
dropped in favour of the oldest ones and **no Production, Notice or Aggregate item was ever
shown again**. The player who engaged least got the least useful information, and Home
compounded it by claiming `RequiresAttention` whenever any summary existed.

**The 7-day threshold.** Long enough that a player who opens the app most days still sees
"something is new"; short enough that a summary left unacknowledged for a week stops
shouting. The queue-idle-with-banked-Vitality branch is deliberately **not** gated by
currency, so that signal's semantics are unchanged.

**Consequences — three real defects found and fixed while implementing:**

1. **The sort was never stable.** `Finalize` called `List<T>.Sort` with a kind-only
   comparator and then claimed "insertion order within a kind" in a comment. `List.Sort` is
   not stable, so presentation order within a kind was arbitrary while the code asserted
   otherwise. The ordering is now explicit: kind ordinal, then input index.

2. **An unsound validator rule was removed, not added.** Task 2.4 asked for a check that an
   item's commit timestamp is not after the summary's generation timestamp. That comparison
   **assumes a monotonic clock**, and this repository explicitly does not assume one
   (`ACTIVITY_PIPELINE` §12; the backward-clock handling in D-053). Adding the check made
   three acceptance suites fail closed on a perfectly valid state — booting a save with a
   clock earlier than the save's own generation is a supported, tested scenario. This is
   exactly the "unsound invariant that bricks mature saves" shape the validator exists to
   prevent, so any comparison between two persisted timestamps is now treated as unsound and
   item currency is carried for **presentation only**.

3. **Currency had to be measured against *now*, not against the summary.** The first
   implementation computed `IsCurrent` from the summary's own `GeneratedAtUtc`. A summary
   that is not recomposed keeps its original generation time, so a month-old item reported
   as brand new forever and the attention signal could never expire — the change would have
   been inert. `FromState` now takes the reading instant and `GameSession` passes the
   injected clock. Found by the Home attention test, not by inspection.

- **No schema bump and no migration.** The new item field is additive and an absent value
  decodes to "as current as the summary itself", which is correct: those items genuinely are
  the most recent thing that happened to the player. A pre-D-055 save is never invalidated.
- **No item text, event mapping, acknowledgement operation or canonical progression
  changed.** All seven pacing reports — whose text includes summary output — are
  byte-identical, which is direct evidence that nothing about what is *said* moved.
- **Still open and not claimed:** whether 7 days is the right currency threshold, and
  whether rendering historical items dimmed rather than absent is the right presentation,
  are UX judgements. The mechanical properties are tested; the feel is the M5 shell's call.

---

## D-056 — Every public use case is covered, and an enumeration guard stops the next gap shipping

**Status:** Accepted *(verified at the F-11/F-17 pass: `dotnet test` 383/383 — Domain 122, Infrastructure 48, Application 213 — green; `git diff --stat -- src tools` **empty** (no production file modified by this change); `verify-architecture.sh` 11/0; guard suite 25/0)*

**Decision:** Two of `GameSession`'s public mutating operations — `ReorderQueue` and
`DequeueProject` — had **zero** test references anywhere in the repository. Both mutate and
persist durable queue state, and they are exactly the two operations a drag-to-reorder
queue UI and an "un-queue" button would wire up first. Both are now covered, and a
**reflection-based enumeration guard** fails the build if any public, parameterless,
non-void, non-mutating operation on `GameSession` has no reference anywhere under
`tests/`. `ReturnSummaryComposer.WithNotice` gained direct tests that assert its output as
*content* rather than as a count.

**Rationale.** Writing tests for the two known gaps fixes those two and nothing else. A
gap nobody looks for is a gap that grows, and this repository has now hit the same class of
gap twice — a store-level guarantee no application-layer caller exercised (D-052), and a
mechanism that was tested in isolation but unreachable in the product (D-054). The guard
is the part that generalises. It reads only names and source text: it never **invokes** an
operation, so it cannot mutate durable state or throw for the wrong reason.

**The guard was proven to fail, and its first failure mode was found by doing that.**

| Step | Result |
|---|---|
| Healthy tree | guard green |
| A public parameterless non-void use case added to `GameSession`, untested | **guard FAILED**: `- UntestedPublicUseCaseProbe (HomeReadModel UntestedPublicUseCaseProbe()) — expected a test in tests/WalkGame.Application.Tests/` |
| Probe removed | guard green again |

**The self-reference hole.** The first proof attempt *passed* — vacuously — because the
guard test's own XML doc comment named the probe, and the guard scans the test sources.
A guard that can be satisfied by a comment about the guard is not a guard. The probe's
name was removed from the test file and the proof was re-run, and the transcript is
recorded in this decision log rather than beside the code precisely so that it cannot
reintroduce the hole.

**Two real behaviours pinned that the task list had wrong.** Task 2.3 described
`DequeueProject` on an *Active* project as "entry removed from the queue; status
unchanged". The actual contract is that it is **refused** with `NotQueued`, because
activation has already removed the project from the queue. That is the better contract —
an Active project holds invested Vitality, and refusing the call is what stops that money
being orphaned — so the test pins the real behaviour and the discrepancy is recorded here
rather than "fixed" to match the prose. Task 2.4's "returns to the unallocated bank" is
similarly only true once the queue is idle; while a project is Active the Vitality is
correctly held as that project's progress, and the test asserts that conservation
explicitly (120 invested + 200 more = 320 in, 300 consumed, 20 banked, nothing stranded).

**Consequences:**
- **No production code was modified.** `git diff --stat -- src tools` is empty, which is
  itself the evidence that the tests describe the system as it is rather than reshaping it
  to fit the tests.
- The Region 1 content graph has exactly **one** entry project, so three projects cannot be
  enqueued through the public API without completing a chain. The queue tests therefore seed
  canonical state directly. That is a property of the shipped content, not a workaround
  for a defect, and it is worth knowing before a second chain is authored.
- The guard's exclusion rules are prefix-based (`Set*`, `Enqueue*`, `Dequeue*`, `Activate*`,
  `Reorder*`, `Mark*`, `Advance*`, `Acknowledge*`, plus the boot/ingest entry points) plus
  an explicit, reason-carrying `AllowList`. A consciously untested operation is therefore a
  reviewable edit rather than a silent exemption.
- The durable, reflection-driven no-leak sweep (every read model walked
  automatically) remains the job of `surface-ingestion-rejection-reasons` task 5.1, as that
  change specifies. This change only completed the explicit list, which the D-052 work had
  already extended to the three previously-unwatched surfaces.

---

## D-057 — Dormant surface is recorded, not wished away: the seed, the generic result, and three dead error codes

**Status:** Accepted *(verified at the F-15/F-21 pass: `dotnet test` 386/386 — Domain 124, Infrastructure 48, Application 214 — green; all seven `docs/evidence/m4/pacing-*.txt` reports byte-identical; save-growth verified; `verify-architecture.sh` 11/0; guard suite 25/0)*

**Decision:** Four pieces of surface that read as live but were not are resolved, and one
guard is added so the class cannot regrow. The guiding rule: **dormant surface is
recorded honestly, not wished away, and not justified by inventing a use for it.**

1. **Canonical randomness is kept, and D-017 is corrected.** `DeterministicRng` remains —
   the *policy* is right and the mechanism is genuinely needed by the first canonical
   consumer. What changed is the honesty: D-017 no longer describes a live mechanism, the
   type and the `GameState.Rng` field both state that no consumer exists today, and the
   inert behaviour is pinned by a test. Task 1.4's prohibition was respected: the RNG was
   **not** wired into any progression, content or simulation path. Adding a random
   mechanic to justify existing code would have been inventing a feature to excuse a
   dependency, which is strictly worse than a documented one.

2. **`DomainResult<T>` is removed.** Zero production and tool references; the only uses were
   two lines in its own unit test. It advertised a supported shape for a value-returning
   use case that does not exist. When such a use case arrives it will choose its result
   type against a real requirement, not inherit a guess. Removal (rather than retention)
   was chosen because a type nobody uses is a claim the codebase cannot keep.

3. **`ErrorCodes.NotAvailable`, `.QueueEmpty` and `.InvalidArgument` are removed.** Each was
   declared and returned by nothing, so a reader of `Results.cs` reasonably concluded the
   system distinguished those three conditions somewhere. Before removal it was confirmed
   that **no persisted value, wire format or localisation key keys on any of the three
   strings** — grepped across `docs/`, `scripts/` and the save/JSON paths. The
   `DomainResult.From` test that used two of them as arbitrary codes now uses live codes;
   the behaviour it pins is unchanged.

4. **`ErrorCodeCoverageTests` prevents recurrence.** It reflects over `ErrorCodes` and fails
   if any declared constant is referenced by no production path, reporting **all** unused
   codes in one message so a single run lists everything to fix. An empty, reason-carrying
   `ReservedAllowList` lets a platform adapter land a code ahead of the operation that
   returns it without the guard blocking legitimate in-flight work — a conscious,
   reviewable edit rather than a silent exemption.

**The guards were proven to fail, and doing so is how their own failure modes were found.**

| Guard | Healthy | Broken fixture | Result |
|---|---|---|---|
| `ErrorCodeCoverageTests` | green | an unused code added to `ErrorCodes` | **FAIL**, naming `ScratchProbeNeverReturned ("scratch.never-returned")` |
| `UseCaseCoverageGuardTests` (D-056) | green | an untested public use case added to `GameSession` | **FAIL**, naming the member and the expected test directory |

The D-056 guard's **first** proof attempt passed *vacuously*, because the guard test's own
doc comment named the temporary probe and the guard scans the test sources — a guard
satisfiable by a comment about the guard is not a guard. That self-reference is now
documented in the guard itself, and both proof transcripts are recorded in this log
rather than beside the code, precisely so they cannot reopen the hole.

**Consequences:**
- **No save schema change and no migration.** The removed types and codes were never
  persisted, so no mature-save fixture was needed and no rollback consideration arises.
  This is the direct payoff of confirming persistence-independence *before* removing.
- All seven pacing reports and the save-growth series are unchanged, which is the expected
  result of touching declarations that no code path reads — and is the evidence that the
  removals were genuinely inert rather than subtly load-bearing.
- `SeededProgressionPropertyTests` names its generator `testDataRng` and says in a
  one-line comment that it is property-test INPUT, never canonical game randomness. The
  rename is identifier-only; every assertion is unchanged and the suite is green.
- The seed honesty test drives a 24-day profile through the **real** ingestion pipeline for
  two different seeds and compares canonical state deeply with the RNG field removed, so
  it asserts the player-visible claim rather than one incidental field.
- **Still unclaimed:** whether a seed *should* eventually affect outcomes is a product
  decision for the first mechanic that needs randomness, not a code fix. Nothing here
  forecloses it; it only stops the repository from implying the mechanism is live today.

---

## D-058 — Content validation derives its numeric bounds from the simulation's real arithmetic, and checks itself

**Status:** Accepted *(verified at the F-08 pass: 11 new cases in `ContentValidationTests`; `dotnet test` 397/397 — Domain 135, Infrastructure 48, Application 214 — green; all seven `docs/evidence/m4/pacing-*.txt` reports **byte-identical**; save-growth verified; `verify-architecture.sh` 11/0; guard suite 25/0)*

**Decision:** `ContentValidator.MaxMilliUnitsPerDay` is no longer a hand-picked
`1_000_000_000_000`. It is now **derived** from the production tick's formula by
`ProducerArithmetic` (Domain, next to `ProducerDefinition`), and `MaxProducerInterval`
itself moved into that helper so the simulation and the validator read ONE source.
`ContentValidator.CheckNumericBoundsAgainstSimulation` re-derives the bound on every
validation and reports a named violation if the published constant ever drifts from it.

**The defect, restated.** `OfflineAdvancer.TickProducers` computes:

```
(cappedTicks / TicksPerDay) * rate  +  (cappedTicks % TicksPerDay) * rate / TicksPerDay
```

The sub-day remainder is strictly less than `TimeSpan.TicksPerDay` (8.64e11), so
representability requires `rate ≤ long.MaxValue / (TicksPerDay − 1)` ≈ **1.07e7**. The old
bound of 1e12 was therefore wrong by roughly **five orders of magnitude**: any rate
between ~1.07e7 and 1e12 passed validation and then overflowed in production. C# arithmetic
there is unchecked, so the product **wrapped** rather than threw, a wrapped value drove
`StoredMilliUnits` **negative**, and `GameStateValidator` then failed the save closed on the
next boot. A player's world became unloadable from a number content validation had
explicitly approved. The old comment stated the correct intent — "keep products
representable" — and simply asserted a number that did not achieve it.

**Why a self-check, and why it is a real check.** A bound that merely *looks* right is not a
bound. If someone hand-edits the constant, or the simulation's maximum interval changes,
the two drift silently and validation starts approving content the simulation cannot
represent — which is precisely the failure mode being fixed. The self-check also asserts
**which term binds** (currently the sub-day remainder, by five orders of magnitude), so a
future change to `MaxInterval` that flipped the binding term would be reported as a stale
derivation rather than passing unnoticed.

The check is exercised by a test that passes a deliberately perturbed published bound and
asserts the violation names **both** values. `MaxMilliUnitsPerDay` is `static readonly`
rather than `const` precisely because a compile-time constant cannot reference the
simulation's interval, and hardcoding the number is the failure being removed.

**Consequences:**
- **The production tick formula was not touched.** The seven byte-identical pacing reports
  are the proof, since every profile is driven day-by-day through the real pipeline. Task
  3.3 is satisfied by evidence, not by assertion.
- **No Region 1 value changed.** Shipped rates remain 1200 / 1500 / 2500 milli-units/day —
  three orders of magnitude below the corrected bound, and roughly 2500× *below* the old
  one. This was latent authoring risk, not a live defect, and the new bound does not change
  any existing world's behaviour.
- **Two validator rules are now demonstrably unreachable through the constructors.**
  `ProducerDefinition` already rejected a non-positive capacity and `RegionDefinition` a
  non-positive cap, so `ContentValidator`'s own `capacityUnits <= 0` and cap-positivity
  checks can only fire on a definition that bypasses its constructor. They are retained as
  defence in depth, and the tests now prove the CONSTRUCTOR guard that actually runs rather
  than pretending the validator rule is reachable.
- **Authoring bounds are now documented where a content author will find them**
  (`WORLD_AND_CONTENT.md`), and `TECHNICAL_ARCHITECTURE.md` §23 records that the content
  validator is the release gate for numeric representability, not only for references.
- **Still unclaimed:** whether a producer rate near the corrected bound produces *sensible*
  game behaviour is a balance question no representability test can answer. The bound is
  "cannot overflow", not "is a good number".

---

## D-059 — Saves are gated against the authored content version they were created for, and retired IDs resolve to authored tombstones

**Status:** Accepted *(verified at the F-04 pass: 21 new cases (19 `ContentVersionGateTests` + 2 codec round-trip); `dotnet test` 417/417 — Domain 135, Infrastructure 49, Application 233 — green; all seven pacing reports byte-identical; save-growth verified; `verify-architecture.sh` **12**/0 (rule 6 discovered the new CI-invoked script); `verify-ci-steps.sh` **13 ok / 0 failed / 2 hosted-only SKIP** over 15 steps; guard suite 25/0)*

**Decision:** Three things, none of which is a schema migration.

1. **`GameState.AuthoredContentVersion`** records the content version the profile was
   **created** against, set once by `GameFactory.NewGame` and never rewritten. Additive:
   absence decodes to `null` and is evaluated as the OLDEST supported version, so no
   `SchemaVersions` bump and no `ISaveMigration` were needed.
2. **Boot classifies the save↔content relationship** as `ExactMatch`, `CompatibleOlder`,
   or `NewerThanBuild`, reported on every boot. A save **newer** than the build fails
   closed with a distinct `StartStatus.ContentIncompatible` /
   `DiagnosticsBootOutcome.ContentIncompatible` and is **never rewritten**.
3. **Authored tombstones** (`ContentTombstone`) let a retired identifier stay
   RESOLVABLE instead of becoming an unresolvable reference. Region 1's set is
   deliberately **empty** — the mechanism and its guards are introduced, and nothing is
   retired.

**The two version axes are different things.** `SchemaVersions` is the **payload wire
format**, migrated by the `ISaveMigration` chain. `AuthoredContentVersion` is the
**authored content graph the payload refers to**, which is never migrated. They are
reported independently and a test pins that: a save on a *current* schema but authored
against a *newer* content version is rejected with `ContentIncompatible` and carries
**no** codec failure and **no** migrations. Conflating them would misreport a content
break as a schema problem, and the corrective action — "migrate the payload" — is the
wrong one.

**Why this was urgent.** `RegionDefinition.ContentVersion` existed, was set to 2, and was
read by **nothing**. Canonical state fails CLOSED on unknown content — correct behaviour,
and precisely what makes a content rename dangerous. Rename one project or drop one
discovery, and every existing save fails validation on boot, both generations are
unreadable, and the one-generation backup holds the same invalid content so it fails too.
R-018 listed four mitigations; two were enforced, the tombstone policy did not exist, and
**nothing detected the drift before it destroyed a profile.**

**A tombstone grants NOTHING.** "Resolves" must not mean "still works". A tombstoned
project may not sit Active, a tombstoned producer may not unlock or hold output, and a
tombstoned expedition may not be recorded as completed — each is a distinct violation, and
each is covered. An identifier that is neither live nor tombstoned **still fails closed**,
so tombstones cannot become a general escape hatch for unknown content.

**Consequences:**
- **A content-drift gate now exists and runs in CI.** `scripts/verify-content-compatibility.sh`
  proves the shipped content validates and that mature saves generated at five different
  ages all decode and resolve against it, reporting each save's authored content version.
  It is wired into `ci.yml` deliberately: `verify-ci-steps.sh` executes the workflow
  verbatim (D-049), so a script CI does not call is not a gate. `verify-architecture.sh`
  rule 6 automatically picked it up and raised the architecture count from 11 to 12 by
  checking its exec bit and LF line endings — the rule working as designed on a new script.
- **The gate was proven to fail on a deliberately drifted fixture** (a save referencing
  `proj.renamed-away`), and its diagnostics were IMPROVED as a result: the first proof
  reported only "no verdict", which would have sent a reader hunting for a harness bug
  instead of a content break. It now surfaces the actual validator line.
- `validate` reports `contentVersion=<save>/<build> content=<verdict>` alongside the schema
  version and violation count, and prints the shipped tombstone set; `dump` prints the
  recorded authored content version. Reporting only the schema made a retired-identifier
  failure look like a schema problem.
- `Region1Catalog.Create(IEnumerable<ContentTombstone>?)` was added so tests can prove the
  mechanism against the real content graph. The parameterless overload production uses is
  untouched, and passing no tombstones is exactly the shipped content.
- **Still unmitigated and honestly recorded:** a drift that shipped through an
  *intermediate* build — a profile created against a version that was then retired without
  a tombstone — is detected at boot but cannot be recovered, only refused. And detecting
  drift is not the same as preventing it; the tombstone discipline is a human review
  obligation, which is why the gate exists and why `AGENT_EXECUTION_GUIDE.md` now states
  the rule.
