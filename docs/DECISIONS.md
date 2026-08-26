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

**Status:** Accepted

**Decision:** Expedition/discovery outcomes that use randomness derive from persisted deterministic seeds/state.

**Rationale:** Save/reload/retry must not reroll canonical results.

**Consequences:**

- RNG is injected;
- rendering randomness remains non-authoritative;
- algorithm changes may require version consideration.

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

1. Exact Unity 6 LTS editor/runtime version.
2. Exact local persistence implementation (snapshot+journal vs SQLite or another adapter).
3. Exact Android Health Connect SDK/API integration strategy and minimum supported OS/device policy.
4. Exact iOS HealthKit integration strategy and minimum supported iOS version.
5. Final Region 1 fiction/name/art direction.
6. Final Vitality conversion curve and safety bounds.
7. Whether Materials and Knowledge both survive MVP balance testing.
8. Notification categories and defaults after UX/device validation.
9. Asset delivery strategy for Visit World.
10. Whether cloud backup/sync enters post-MVP scope.

Resolve these through new decision entries with rationale and consequences; do not silently bake them into implementation.

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
- correction transaction IDs derive from identity + revision + applied amount (tx-corr1 namespace) and cannot collide with first-credit IDs;
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
- With no resource cap set, delivery always succeeds and the store only ever holds sub-unit remainders — capacity binds when content defines resource caps or sinks (M4 concern).
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
