# 05 — Data, Privacy, Sync and Trust

## 1. Privacy posture

Health data is sensitive. The product should collect the minimum required information and keep core progression local by default.

Principles:

- health/fitness purpose must be explicit;
- request only data types needed by implemented features;
- do not use health data for advertising;
- do not sell health data;
- do not introduce analytics events containing raw health metrics;
- local-only mode must be fully functional;
- account creation is optional;
- export and deletion are product requirements before public launch.

## 2. Proposed SQLite schema

Initial tables:

### Metadata/config

- `app_meta(key, value)`
- `schema_migrations(version, applied_at)`
- `profile(id, timezone_policy, created_at, updated_at)`
- `balance_config(version, content_hash, activated_at)`

### Health/provider

- `activity_provider_state(provider, availability, permission_json, updated_at)`
- `activity_cursor(provider, cursor_blob, last_success_at, recovery_window_start)`
- `activity_ledger(id, provider, provider_record_id, fingerprint, kind, start_at, end_at, local_date, quantity, unit, provenance, source_app_id, observed_at, deleted_at)`

Unique indices on provider IDs/fingerprint as appropriate.

### Rewards/progression

- `vitality_grant(id, activity_basis_hash, formula_version, amount, breakdown_json, created_at)`
- `vitality_allocation(id, grant_id, policy_version, target_type, target_id, amount, created_at)`
- `allocation_policy(id, version, policy_json, active_from, active_to)`

### Projects/world

- `project_instance(id, definition_id, definition_version, region_id, state, progress, activated_at, completed_at)`
- `project_progress_event(id, project_id, allocation_id, delta, created_at)`
- `world_event(sequence, id, type, schema_version, region_id, payload_json, created_at)`
- `world_region_state(region_id, snapshot_sequence, state_json, updated_at)`
- `world_snapshot(id, through_sequence, snapshot_json, created_at)`

### Decisions/expeditions

- `decision_instance(id, definition_id, state, triggered_by_event, resolved_choice, triggered_at, resolved_at)`
- `expedition(id, definition_id, state, effort_progress, started_at, completed_at, revealed_at)`
- `expedition_progress_event(id, expedition_id, grant_id, delta, created_at)`

### Presentation/read models

- `away_report_checkpoint(profile_id, through_world_sequence, acknowledged_at)`
- `journey_entry(id, source_event_id, kind, occurred_at, payload_json)`
- `notification_log(id, category, source_event_id, scheduled_at, delivered_or_unknown_at, state)`

### Sync

- `sync_outbox(id, entity_type, entity_id, operation, payload_json, created_at, attempt_count, last_error)`
- `sync_cursor(scope, token, updated_at)`
- `sync_conflict(id, entity_type, entity_id, local_json, remote_json, status, created_at)`

## 3. Data minimization

Prefer storing normalized activity facts needed to prove/recompute rewards rather than entire native health samples.

Do not persist:

- heart-rate samples if no heart-rate feature exists;
- precise GPS routes unless an explicit route feature is built and permissioned;
- medical/clinical data;
- unrelated HealthKit/Health Connect fields.

If source app/device identifiers are stored for dedupe, document why and consider hashing/pseudonymizing where practical.

## 4. Encryption

Baseline:

- rely on OS app sandbox/device encryption for ordinary local DB protection;
- store auth tokens/keys in SecureStore/Keychain/Keystore, not SQLite plaintext;
- evaluate SQLCipher only if threat model/product requirements justify the operational complexity;
- all remote transport TLS;
- secrets never bundled in client code.

## 5. Optional account and cloud sync

Sync should follow a local-outbox model.

User actions commit locally first, then enqueue sync mutations. The user never waits on network to complete a project or resolve a decision.

Suggested conflict model:

- immutable events: union by globally unique ID;
- allocation policy/settings: last-writer-wins with server timestamp/version or explicit merge;
- project/world state: derive from synced canonical events rather than syncing mutable counters independently;
- health ledger: consider keeping device-local unless cross-device continuity requires server storage; if synced, minimize fields and obtain explicit consent.

## 6. Multi-device concerns

Multi-device progression creates a hard dedupe problem because both devices may read the same health-store-originated activity.

Do not ship naive multi-device cloud rewards.

Before enabling multi-device:

- establish stable activity basis identifiers;
- define server canonicalization/deduplication;
- ensure grants are unique by activity basis + formula version;
- test two devices processing overlapping periods offline then syncing.

Until then, cloud sync can be limited to world/settings or designate one activity-awarding device.

## 7. Trust tiers

Suggested provenance classes:

- **store_verified** — HealthKit/Health Connect data from granted APIs;
- **sensor_preview** — device sensor preview, not permanent reward authority;
- **manual** — user-entered/imported activity;
- **fixture/debug** — test data.

In single-player, all legitimate user experiences can remain generous. Trust tiers become important for leaderboards, shared worlds, or rewards with external value.

## 8. Anti-cheat philosophy

Do not turn the app into surveillance software.

Allowed lightweight checks:

- duplicate detection;
- impossible negative values;
- obviously corrupted magnitudes;
- source provenance;
- consistency checks among overlapping aggregate windows;
- debug-mode watermarking.

Do not require continuous GPS or heart rate merely to prove walking.

## 9. Export

User export should include:

- profile/settings;
- normalized activity summaries used for gameplay;
- Vitality grants and allocations;
- project/world state/events;
- decisions;
- expeditions;
- Journey history;
- sync metadata where useful.

Offer machine-readable JSON and optionally user-friendly CSV summaries.

Do not export OS health-store records beyond what the app itself retains unless explicitly requested and lawful.

## 10. Deletion

Deletion flows:

- reset game world only;
- delete local app data;
- delete cloud account/data;
- disconnect health access (with OS settings guidance where required).

Deletion must not claim to delete data from HealthKit/Health Connect that the app does not own/control.

## 11. Analytics

Public release can operate with zero third-party analytics initially.

If analytics are later added:

- explicit event schema review;
- no raw health values;
- no provider record IDs;
- no GPS routes;
- prefer aggregate product behavior events;
- configurable telemetry opt-out where appropriate.

## 12. Privacy documentation gate

Before store release:

- privacy policy exists;
- permissions rationale matches actual code;
- App Store/Play data disclosures reviewed;
- no unused health entitlement/permission;
- retention/export/delete behavior documented;
- third-party SDK data collection audited.
