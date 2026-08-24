# Activity Pipeline

## 1. Purpose

The activity pipeline is the highest-trust subsystem in the game. It transforms external health/activity records into canonical game progression.

Its contract is stricter than ordinary gameplay code because failures directly undermine player trust.

The pipeline must guarantee, within documented platform limitations:

- eligible activity is eventually credited;
- duplicate/replayed data is not credited twice;
- late/corrected records can be reconciled;
- source failures degrade safely;
- a crash/restart cannot multiply rewards;
- the game can explain what it processed internally;
- raw health data is minimized.

---

## 2. Pipeline stages

`Platform Source → Raw Adapter Record → Normalized Activity Record → Validation → Deduplication → Eligibility → Conversion → Reward Ledger → Domain Application → Persistence → Reconciliation Checkpoint`

Every stage should have a narrow responsibility and test fixtures.

---

## 3. Platform adapters

The domain must never depend directly on Android or iOS health APIs.

Define a platform-neutral interface conceptually similar to:

```text
ActivitySource
  readChanges(checkpoint, window) -> ActivityBatch
  getPermissionState() -> PermissionState
  getSourceHealth() -> SourceHealth
```

Platform adapters are responsible for:

- querying approved source APIs;
- translating source-specific fields;
- preserving stable source identifiers when available;
- reporting permission/source state;
- returning correction/deletion information when the source supports it;
- not applying game rewards.

The Android implementation may integrate with Health Connect; the iOS implementation may integrate with HealthKit. Exact API/version choices must be validated at implementation time.

---

## 4. Normalized activity record

A normalized record should contain only what the game needs.

Suggested fields:

- normalized record ID/fingerprint;
- provider/source type;
- source record ID if available;
- activity category;
- start timestamp;
- end timestamp;
- quantity/value;
- unit;
- source revision/update marker if available;
- ingestion timestamp;
- deletion/correction marker where applicable;
- provenance flags;
- schema version.

Do not copy entire platform health objects into the save file.

---

## 5. Eligible activity

The MVP should begin with the narrowest reliable input, most likely walking/step-derived activity.

Additional activity categories should be added only after their conversion semantics and data quality are understood.

Eligibility rules must be versioned and testable.

Potential checks:

- supported category;
- non-negative values;
- sane timestamp ordering;
- supported units;
- valid source provenance;
- not marked deleted;
- not outside permitted reconciliation horizon;
- within bounded plausibility rules;
- not already committed.

Plausibility checks must protect data integrity without pretending the app can perfectly identify fraud.

---

## 6. Deduplication model

Exactly-once behavior must not depend on “we usually query only new data.”

### Preferred identity

Use a stable platform source record identifier + source/provider namespace when available.

### Fallback identity

When a source lacks durable identity, create a deterministic fingerprint from normalized stable fields such as:

`source + category + start + end + normalized quantity + source metadata subset`

The fingerprint algorithm must be versioned.

### Dedup ledger

Maintain a durable processed-record ledger or equivalent compact structure containing enough information to answer:

- have we seen this logical record?
- what version/revision was processed?
- how much game value was credited?
- was it later corrected/deleted?

Do not rely only on an in-memory set.

---

## 7. Batch processing transaction

A processing batch should conceptually behave as one recoverable transaction:

1. load canonical state;
2. load source checkpoint;
3. normalize source records;
4. validate;
5. compare against ledger;
6. calculate net eligible delta;
7. calculate Vitality conversion;
8. create deterministic reward transaction(s);
9. apply game progress;
10. update processed-record ledger;
11. update source checkpoint;
12. persist atomically or journal enough to recover;
13. publish presentation summary only after commit.

If persistence fails, the source checkpoint must not advance beyond the durable reward state.

---

## 8. Reward transaction identity

Every credit operation should have a stable transaction ID derived from the underlying activity batch/records and conversion rule version.

A reward transaction should record:

- transaction ID;
- record IDs/fingerprints or batch identity;
- conversion-rule version;
- eligible activity amount;
- Vitality amount;
- application timestamp;
- target allocation summary if immediately spent;
- resulting ledger state/checksum where practical.

Reapplying the same transaction ID must be a no-op.

---

## 9. Conversion rules

Activity-to-Vitality conversion must be deterministic and versioned.

The initial implementation should favor simple rules that can be reasoned about and simulated.

Requirements:

- integer/fixed-point authoritative math where appropriate;
- documented units;
- no hidden random multiplier;
- bounded pathological inputs;
- explicit rounding behavior;
- rule version stored with credited transactions;
- balance simulation before changing live rules.

If the game later changes conversion rates, historical activity should not silently be re-valued unless a migration explicitly does so.

---

## 10. Late-arriving data

Health/activity sources may expose records after the time period in which they occurred.

The pipeline must support a reconciliation window rather than assume strict chronological arrival.

Rules:

- query incremental changes where the platform supports it;
- periodically reconcile a bounded recent historical window;
- deduplication must make overlapping queries safe;
- late valid data should receive credit once;
- return summaries should distinguish newly processed historical activity from “activity performed since last launch” when confusion would result.

---

## 11. Corrections and deletions

Some platforms/sources may correct or remove records.

The MVP must define an explicit policy.

Recommended baseline:

- positive late corrections can add net eligible credit;
- negative corrections should adjust unspent/current progression conservatively when feasible;
- never destroy completed major world content because a source corrected a small amount;
- suspicious large reversals should be diagnosed and bounded;
- source deletions must not silently create duplicate replacement credit.

The exact correction policy should prioritize state integrity and player trust over punitive clawbacks.

---

## 12. Clock and time-zone anomalies

The pipeline must not assume device time is monotonic.

Test:

- time-zone change;
- daylight-saving transitions;
- manual clock moved forward;
- manual clock moved backward;
- device reboot;
- app restored from backup;
- records with future timestamps;
- records spanning midnight;
- long inactivity periods.

Canonical source timestamps should be normalized consistently. UI may render local time separately.

---

## 13. Permission states

Permission handling is part of the product experience.

Canonical permission states should include enough distinction for the UI to explain:

- not requested;
- granted;
- partially granted where applicable;
- denied;
- revoked;
- unavailable/unsupported;
- temporarily failing.

The app must remain navigable if permission is denied. It should explain which functionality is unavailable without entering a broken loop.

---

## 14. Source health

The adapter should expose diagnostic source health such as:

- last successful query;
- last record timestamp;
- permission status;
- last error category;
- checkpoint age;
- whether reconciliation is pending.

Player-facing UI should remain simple; detailed source health belongs in diagnostics.

---

## 15. Privacy and retention

Minimize retained sensitive data.

Preferred retention model:

- canonical credited aggregates;
- processed identity/fingerprint data needed for dedup;
- compact provenance;
- source checkpoint;
- optional bounded diagnostic history.

Avoid retaining:

- unrelated health metrics;
- detailed routes;
- raw records beyond what is necessary for reconciliation;
- platform payloads copied wholesale.

A future backend must not automatically receive raw health records.

---

## 16. Development fixture system

The activity pipeline must be testable without a real device source.

Create fixture sources for:

- ordinary daily steps;
- duplicate batches;
- overlapping query windows;
- late records;
- corrected records;
- deleted records;
- malformed records;
- huge values;
- out-of-order records;
- partial failures;
- crash between reward application and checkpoint persistence;
- repeated app restart;
- permission transitions.

Fixtures must use the same application/domain ingestion path as production adapters after the platform boundary.

---

## 17. Property/invariant tests

High-value invariants:

- processing the same batch twice produces the same final canonical state as processing once;
- input ordering does not change final credited value when records are independent;
- replay after save/load does not duplicate reward;
- negative/invalid quantities never create positive credit;
- ledger balance equals sum of committed reward transactions under the defined accounting model;
- source checkpoint never advances beyond durable processed state;
- adding one new valid record changes credited state by exactly its defined net value;
- presentation summary generation cannot mutate reward state accidentally.

---

## 18. Failure injection

The test harness should inject failures at each critical persistence boundary:

- before normalization completion;
- after dedup calculation;
- after reward transaction creation;
- after domain mutation but before save;
- during save write;
- after save but before checkpoint acknowledgement;
- during restart/recovery.

The acceptable outcome is either:

- the transaction is durably committed exactly once; or
- it is not committed and is safely retried.

There must not be a third state where reward is multiplied or silently lost with no recovery path.

---

## 19. Diagnostics

Internal diagnostics should make investigations possible without exposing sensitive raw data.

Useful fields:

- source adapter/version;
- reconciliation window;
- batch count;
- accepted/rejected/duplicate counts;
- credited aggregate;
- conversion version;
- checkpoint before/after;
- transaction IDs;
- error categories;
- save/recovery outcome.

Diagnostics should use redaction and bounded retention.

---

## 20. Device verification matrix

Before release, verify at minimum:

- clean install + permission grant;
- permission denial;
- later permission grant;
- permission revoke after prior use;
- foreground ingestion;
- app closed then reopened after activity;
- device reboot;
- offline/no network;
- duplicate reconciliation queries;
- long absence;
- app update with existing ledger/save;
- low-storage or interrupted persistence where practical;
- source unavailable/degraded state;
- daylight/time-zone change scenario.

Platform-specific behavior must be documented as device-verified only after physical-device evidence exists.

---

## 21. Exit criteria

The pipeline is not ready for content-scale development until:

1. deterministic fixture ingestion works end to end;
2. duplicate and overlapping records are proven idempotent;
3. save/restart replay is proven safe;
4. checkpoints and reward commits recover correctly from injected interruption;
5. late data is credited once;
6. correction policy is implemented and tested;
7. permission/source errors have explicit UI states;
8. diagnostics can explain processed totals;
9. privacy retention matches this document;
10. platform adapters have at least one real-device verification path before release qualification.
