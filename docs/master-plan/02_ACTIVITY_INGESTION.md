# 02 — Activity Ingestion

## 1. Goal

Reliably translate real-world movement into a canonical local ledger without double-awarding, even when:

- health data is edited or delivered late;
- the app is not opened for days;
- background execution does not occur;
- multiple devices/apps contribute records;
- permissions change;
- the phone reboots;
- the user crosses time zones;
- the same period is reconciled more than once.

## 2. Provider abstraction

Define a narrow cross-platform interface before writing native modules.

```ts
interface ActivityProvider {
  getAvailability(): Promise<ActivityProviderAvailability>;
  getPermissions(): Promise<ActivityPermissionState>;
  requestPermissions(request: ActivityPermissionRequest): Promise<ActivityPermissionState>;
  readChanges(cursor: ProviderCursor | null, window: TimeWindow): Promise<ActivityChangePage>;
  aggregate(window: TimeWindow, metrics: ActivityMetric[]): Promise<ActivityAggregate>;
}
```

The domain/application layers must not import native SDK classes.

Implementations:

1. `FakeActivityProvider` — first and fully deterministic.
2. `HealthConnectActivityProvider` — Android.
3. `HealthKitActivityProvider` — iOS.
4. Optional `PedometerPreviewProvider` — same-day live UI preview only, not canonical long-term reward source.

## 3. Health data scope

### Required core data

Request the minimum necessary data at first onboarding:

- steps;
- walking/running distance;
- workout/exercise sessions or duration where available.

### Optional progressive permissions

Ask later, only when a feature explains why:

- elevation/floors climbed;
- wheelchair pushes for accessible movement credit;
- cycling/swimming distance if broad exercise support is intentionally added.

Avoid requiring:

- heart rate;
- HRV;
- body weight;
- sleep;
- blood metrics;
- calorie estimates.

Active calories may be explored later as a secondary feature but should not be the fundamental reward source because estimates vary by device and user profile.

## 4. Android — Health Connect

Use Health Connect as the Android canonical health-store integration.

Implementation responsibilities in Kotlin/Expo Module:

- availability/version status;
- permission declaration and rationale routing;
- read permission state;
- background read permission capability/flow when supported;
- aggregation for steps/distance/elevation/exercise duration;
- change-token or bounded-window reconciliation strategy;
- data-origin metadata where available;
- resilient pagination;
- conversion to neutral DTOs returned over the Expo Module boundary.

Do not assume background reads are always available. The foreground reconciliation path must be sufficient for correctness.

Use platform aggregation APIs where they correctly handle overlapping sources/deduplication semantics. Preserve enough provenance metadata to understand the result.

## 5. iOS — HealthKit

Use HealthKit as the iOS canonical integration.

Implementation responsibilities in Swift/Expo Module:

- HealthKit availability;
- explicit authorization request for selected types;
- anchored object queries for incremental changes where appropriate;
- statistics/collection queries for aggregate steps/distance summaries where appropriate;
- observer queries plus background delivery as an optimization/trigger;
- app startup registration for observer queries;
- provider anchor persistence only after successful processing;
- neutral DTO conversion.

Critical constraint: HealthKit background observer/server-style behavior cannot be fully qualified in the Simulator. Automated fixtures must cover logic, while physical-device tests validate real background delivery.

## 6. Background execution rule

Background callbacks are **hints to reconcile**, not the source of truth.

Never implement:

`every 15 minutes -> add N idle progress`

Implement:

`OS wakes app -> ask health store what changed -> idempotently reconcile -> persist`

And always repeat reconciliation on foreground.

## 7. Canonicalization

Incoming provider records are normalized before they can award anything.

Normalize:

- provider and source app/device;
- provider record ID if available;
- timestamps as UTC instants;
- local-day key using the user's timezone at the relevant time;
- metric type;
- canonical units (steps count, meters, seconds, meters elevation);
- provenance/trust class;
- provider revision/deletion state where exposed.

Store original provider IDs/cursors where privacy allows, but avoid storing unnecessary health payload.

## 8. Deduplication

Use multiple mechanisms:

1. Unique `(provider, provider_record_id)` when stable IDs exist.
2. Canonical fingerprint fallback over source/type/time/value metadata.
3. Provider aggregate checkpoints for metrics where raw records are unsuitable.
4. Reconciliation window overlap (for example, re-read recent history) plus idempotent upsert to handle late changes.

Do not dedupe solely on timestamp/value because distinct legitimate samples can match.

## 9. Corrections and deletions

Health stores may change historical data. Design for negative correction.

Recommended strategy:

- canonical activity facts can be superseded/deleted;
- reward processing stores the exact activity basis and formula version;
- ordinary small corrections affect future calculation/reconciliation rather than destructively removing already-visible world progress;
- large suspicious corrections create a reconciliation adjustment record;
- never silently duplicate rewards when a record is updated.

A detailed economic correction policy must be finalized before competitive/social features.

## 10. Time zones and day boundaries

Persist UTC timestamps and a resolved `activityLocalDate`/offset where daily presentation requires it.

Rules:

- do not reclassify historical activity just because current timezone changed;
- weekly summaries must state which timezone policy they use;
- DST transitions must not assume all days are 24 hours;
- deterministic test fixtures cover east/west travel and midnight activity.

## 11. Reward eligibility

Activity should be processed through a versioned eligibility layer before conversion.

Possible protections:

- per-source provenance;
- impossible-speed/distance sanity checks when data is sufficiently detailed;
- reasonable daily soft thresholds with non-punitive handling;
- overlap handling between workout duration and step/distance rewards to avoid obvious double counting;
- manual entries explicitly tagged.

For a single-player/local product, avoid invasive anti-cheat. Trust signals mainly protect data quality and future social systems.

## 12. Pedometer preview

A device pedometer can make the Today screen feel live, but it must not independently create permanent rewards if the same steps will later arrive from HealthKit/Health Connect.

Pattern:

- show `todayPreviewSteps` while app is open;
- label as live estimate if necessary;
- reconcile authoritative health-store total later;
- reward only through canonical ledger.

## 13. Reconciliation transaction

Pseudo-flow:

```text
BEGIN
  load provider cursor/checkpoint
  fetch/receive page
  normalize records
  upsert canonical entries and tombstones
  determine newly rewardable deltas
  create immutable VitalityGrant rows
  advance allocation/project/expedition engines
  emit world events
  update read models
  advance provider cursor/checkpoint
COMMIT
```

If any step fails before commit, the cursor must not advance.

For provider APIs where fetching occurs outside SQLite transactions, persist a replayable batch envelope or repeat the query safely.

## 14. Fixture matrix

Build deterministic fixture packs for:

- no activity;
- 500 / 5,000 / 20,000 steps;
- long walk;
- run + steps overlap;
- gym workout with few steps;
- elevation-heavy hike;
- wheelchair movement;
- duplicate provider delivery;
- late-arriving data;
- updated record;
- deleted record;
- 7-day app absence;
- 30-day app absence;
- timezone crossing;
- permission revoked/restored;
- provider temporarily unavailable;
- debug/manual import.

## 15. Acceptance criteria

Activity ingestion is considered production-ready only when:

- re-running the same fixture batch creates zero duplicate grants;
- app absence does not lose eligible progression;
- provider cursor corruption has a bounded recovery path;
- permission revocation does not crash or erase progress;
- the app can explain the last successful reconciliation;
- native modules return typed errors, not opaque exceptions;
- Android real-device Health Connect flow passes;
- iOS physical-device HealthKit foreground and background-delivery qualification passes;
- no unnecessary health permissions are requested.
