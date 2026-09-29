# Privacy and Permission Review (headless lane)

**Scope:** the headless product lanes as they exist at `e2ea00a` (Domain /
Application / Infrastructure / `tools/simulation`). Required by
`TESTING_AND_RELEASE.md` §18 ("privacy/permission review"). Runtime and device
lanes are **out of scope and UNVERIFIED** — Unity screens, OS permission
dialogs, and platform health adapters (M7) do not exist yet and must receive
their own review pass when they do.

Every mechanical claim below was verified by command against this checkout at
review time; the commands are recorded so any future session can re-run them.

## 1. Network surface: none

```bash
grep -rn --include=*.cs -e HttpClient -e WebRequest -e Sockets \
     -e UnityWebRequest -e Analytics src tools   # exit 1 = zero matches
```

Zero network API usage exists anywhere in product or tool sources. There is no
analytics, telemetry, advertising, account, or cloud-sync code. The
offline-first thesis (PRODUCT_SPEC) is therefore mechanically enforced at the
source level, not merely promised — any future addition of a network call is a
review-triggering change.

## 2. Dependencies

- Runtime NuGet dependency (all `src/`): **`System.Text.Json` 8.0.5**
  (`WalkGame.Infrastructure` only) — Microsoft first-party, no transitive
  third-party runtime packages observed in production assemblies.
- Test-only packages (xunit, `Microsoft.NET.Test.Sdk`, `coverlet.collector`)
  never ship: they are referenced exclusively from `tests/*` projects.
- No Unity packages exist in this checkout yet; the future Unity manifest must
  be re-reviewed for transitive network/analytics dependencies at M6/M7 time.

## 3. Data inventory (what the save actually holds)

Save envelope (`SaveEnvelopeDto`, `src/WalkGame.Infrastructure/Persistence/SaveJson.cs`):
`schemaVersion`, `savedAtUtc`, `payloadSha256Base64` (integrity), `payloadBase64`
(opaque canonical payload). The payload is the serialized canonical
`GameState` (`src/WalkGame.Domain/GameState.cs`):

| Field | Content | Privacy character |
|---|---|---|
| `CreatedAtUtc`, `LastAdvancedUtc`, `IngestionCheckpointUtc` | timestamps | presence/absence of play; coarse activity times |
| `Resources`, `Rng` | integer balances, RNG state | non-personal game state |
| `Ledger` | reward transactions: source record IDs, amounts, windows | **health-adjacent**: derived from walking data |
| `ProcessedRecords` | dedup ledger (source IDs / content fingerprints) | **health-adjacent identifiers** |
| `Region`, `Queue`, `PendingReturnSummary`, `LastIngestionOutcome` | world progress, summaries, bounded batch counters | non-personal, bounded |

No names, emails, contact data, precise location, or device identifiers are
stored. The sensitive core is **timestamped walking activity counts and
provider record identifiers** — health-adjacent by nature even though nothing
directly identifying is kept. `LocalPreferencesStore` holds UX
preferences/onboarding state only (D-042).

**At-rest confidentiality: none at the application layer.** The atomic store
writes plaintext JSON (base64 payload, SHA-256 for *integrity only*) with a
backup generation beside it. Confidentiality currently relies entirely on
OS-level disk encryption. Tracked as **KI-006** in
[`KNOWN_ISSUES.md`](KNOWN_ISSUES.md); a product decision on app-layer
encryption/redaction is required before any release qualification.

## 4. Storage location

`grep` over `src/` and `tools/` finds no `Environment.GetFolderPath`,
`SpecialFolder`, or hardcoded OS paths: the save directory is **host-injected**
(`--save` in `tools/simulation`; a host-provided path will be the Unity
contract). Headless code cannot write outside its injected directory. The real
runtime location (e.g. Unity `persistentDataPath`, its OS backup/sync behavior)
is UNVERIFIED until the runtime lane exists.

## 5. Diagnostics surface

`DiagnosticsReadModel` (D-044) is privacy-safe by construction: classified
enums, bounded counters, timestamps, and adapter-owned technical strings, with
**no activity records, no payloads, and no raw exception text**
(`src/WalkGame.Application/ReadModels/DiagnosticsReadModel.cs`). This is
test-enforced, including a planted-marker no-leak test
(`DiagnosticsReadModelTests`, `LeakMarker`). Presentation is gated by the
`DiagnosticsVisible` preference. The surface is local read-only; there is no
upload path (see §1).

## 6. Permissions (forward-looking)

The headless lanes request **no OS permissions**. M7 (Health Connect /
HealthKit) will introduce the first real permission surface; at that point this
review must be redone covering: permission purpose strings, data-type
minimization (steps/distance only — the normalized record model already
requires nothing more), read-only scope, revocation behavior (already
specified headlessly via source-failure/reconciliation paths, D-043), and
platform data-residency rules.

## 7. Findings

| # | Finding | Severity (§15) | Status |
|---|---|---|---|
| P-1 | Health-adjacent save data is plaintext at rest (app layer) | Medium | Open → KI-006 |
| P-2 | PRODUCT_SPEC contains no explicit privacy requirements section; the guarantees above live only in this review and implicit spec theses | Medium | Open → KI-005 |
| P-3 | No network/analytics/accounts in headless code | — | Verified clean (§1) |
| P-4 | Diagnostics are privacy-safe by construction, test-enforced | — | Verified clean (§5) |
| P-5 | Runtime/device permission surfaces | — | UNVERIFIED (do not claim; §4/§6) |

**Conclusion:** the headless lane is honest to its "offline, on-device, no
telemetry" thesis with mechanical evidence, with two open Medium findings
(P-1, P-2) that must be dispositioned before M9, and with all runtime/device
permission claims explicitly reserved rather than assumed.