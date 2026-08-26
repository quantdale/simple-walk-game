# M8-H1 Mature Save & Migration Evidence (Workstream C)

Suite: `MatureSaveMigrationTests` (+ existing `MigrationV1ToV2Tests`,
`M4BackwardDecodingTests`, `SaveCodecRoundtripTests`). Payload surgery uses
checksum-correct re-wrapping (`PayloadSurgery`) so intended semantic damage — not an
accidental checksum mismatch — reaches the decode pipeline.

## C1 — Migration invariants

A mature mid-game fixture is driven through the REAL pipeline (12 app-closed days,
queue decisions, completed entry work), then downgraded into a GENUINE schema-v1
payload: sub-unit `carryMilliUnits` producer stores and every post-v1 additive field
removed while the M2-era dedup ledger survives.

`old bytes → decode/migrate → validate → encode current → reload → validate`:

* migration chain reports exactly `m1-to-v2-producer-stored-milli-units`;
* migrated state passes `GameStateValidator` with zero violations;
* processed-row count, processed vitality total and reward-ledger total survive
  byte-for-byte through the transition;
* canonical stability: `encode(decode(save))` reproduces the exact durable bytes;
  a second decode of the persisted result applies NO migrations.

Deterministic serialization remains covered by roundtrip byte-equality tests.

## C2 — Exactly-once after migration

After migrating the rich v1 save:

* replaying every already-processed day through `IngestFromSource` → all duplicates
  ignored, zero new credit;
* restarting and replaying again → identical outcome;
* final economic state equals the post-migration state exactly.

This retires R-006's headless half: old activity cannot be re-credited by the
registered migration chain.

## C3 — Unknown future schema

Both durable generations rewritten to `schemaVersion=99`: boot fails CLOSED with
the specific "newer game version" diagnostic and leaves both files BYTE-IDENTICAL.
No downgrade is attempted; no blank save is fabricated
(`SessionPersistenceHardeningTests.FutureSchemaSave_...`, codec-level
`Decode_SchemaVersionFromTheFuture_ReportsVersionTooNew`).

## C4 — Content identity durability

Adversarial payload mutations, each required to fail closed with diagnostics and
leave the damaged generation untouched:

* unknown DISCOVERY runtime added → rejected ("unknown");
* unknown COMPLETED PROJECT runtime added → rejected;
* missing producer runtime row → now rejected (validator rule V-1);
* duplicate dictionary keys / renamed content IDs → decode or validation failure →
  fail closed (existing validator coverage plus session-level detail surfacing).

Compatibility policy (documented, not invented ad hoc): additive M4 fields absent =
"nothing yet" (D-036); unknown stable IDs are corruption and reject the save;
missing producer runtime rows are corruption and reject the save (V-1). No silent
fallbacks were introduced.

## Mature v2 coverage note

The Region-1 closure save produced by `M4Region1AcceptanceTests` and reused by the
long-horizon/acceptance scenarios exercises: large ledgers, corrections/deletions,
all chains, landmark tops, producers with bounded stores, discoveries
(reviewed/unreviewed), expeditions completed, arcs at final stages, closure +
post-completion stability, pending summaries — i.e. the campaign's mature-fixture
feature list.
