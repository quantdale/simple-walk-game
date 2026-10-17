# Show today's earned Vitality on Home ("Today: 20 Vitality earned from your walks.")

**Status:** Approved for implementation by the executing agent (D-156), under the design authority Michael granted for this campaign. Region 1 scope; not a Region 2 item (the D-011 / ROADMAP gate is untouched).

## Why

Home says where walked Vitality went (D-151) and how far the active project is (D-145). It never says what today's walking earned. Auto-invest drains the "Unspent" balance, so a player who walked this morning has no direct answer to "did today count?".

## What Changes

- Domain: `ProcessedRecordLedgerState.VitalityOnLocalDay(int day)` returns the net applied Vitality of the retained processed-record rows dated `day` (`LocalDay`, D-103). A row with zero eligible steps (a deleted walk) counts 0. It returns null (unknown) when the day may include retired rows (`day <= RetiredThroughLocalDay`).
- Application: `GameSession.GetTodayEarnings(int utcOffsetSeconds)` returns `DailyEarningsReadModel(LocalDay, Vitality, IsKnown)`. "Today" is `LocalDayCalendar.DayOf(clock.UtcNow, utcOffsetSeconds)`: the trusted clock's instant under the device's current offset, the same day rule records use.
- Unity shell: `ShellController.LocalOffsetSeconds` (default: the device's current UTC offset; tests inject it). Home shows "Today: N Vitality earned from your walks." directly under the Unspent line, only when N > 0. There is no zero-day line.

## Save schema decision: NO bump (v3 before, v3 after)

The brief proposed a persisted per-day ledger introduced by a v3 → v4 migration through `MigrationRunner`. Design review (design.md) found that the canonical save already holds exactly that ledger. Each credited record is one `ProcessedRecordEntry` carrying its net applied Vitality (`VitalityCredited`, net of clamped reversals) and its local day (`LocalDay`). Each row is written in the same atomic commit as the reward it describes.

A second persisted copy would cost more than it gives:
- a new invariant ("daily ledger == Σ rows") the validator would have to police;
- a migration that could only seed the copy empty (wrong for a player mid-day at upgrade) or from these same rows;
- extra save bytes.

It would add no safety. Retention already bounds the rows (45 days behind trusted progress, D-066), and dedup, corrections and deletions already act on them. Deriving meets each requirement in the brief, and meets it more strongly:

| Requirement | How it is met |
|---|---|
| Old saves conservative, never invent credit | v1/v2-migrated rows have `LocalDay = null` and never count; undated sources never count. A hostile v2 save with injected local days fails closed or shows 0. |
| Bounded size | 0 bytes added. The D-098/D-139 save-growth gate is unchanged. |
| Exactly-once | The processed-record identity dedup is the only writer of rows; a replay changes nothing. |
| Reconciliation and corrections | A correction rewrites the row's net applied Vitality, and its day when the provider offset is known. A deletion makes the row count 0. Both reduce the day. |
| Timezone and day boundary | A record's day is its end under the provider offset (D-103). Today is the clock under the device offset. |
| Pacing and content unchanged | No rule, balance or content change. Pacing 7/7 and content compatibility pass. |

DECISIONS D-156 records this explicitly. If a later feature needs per-day history beyond the retention window, that change can add a pruned persisted ledger through `MigrationRunner` and seed it from this derivation.

## Non-goals

Streaks, goals, comparisons with other days, charts, step counts, any zero-day copy, and back-filling legacy rows.

## Compatibility

No save, schema, content or fingerprint change. Older builds read every v3 save exactly as before.
