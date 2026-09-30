## Context

See `proposal.md` (Why) for the evidence.

What constrains the design:

- `GameState.Rng` is **persisted**. It is serialized by `SaveCodec`, asserted
  field-by-field by `SaveCodecRoundtripTests.Encode_Decode_PreservesEveryField`,
  and printed by the CLI. Removing it is a payload change requiring an
  `ISaveMigration` and a mature-save fixture — disproportionate for dead code,
  and it would contradict the policy D-017 records.
- The generator itself is good. It is a correctly specified xoshiro256\*\*
  with SplitMix64 seeding, a documented rejection-sampling `NextInt64` with no
  modulo bias, and an all-zero-state normalization guard. Eight tests cover it
  properly. There is no defect to fix here — only a status to record.
- `SeededProgressionPropertyTests` uses `DeterministicRng` as a test-data
  generator. This is the single most misreadable reference in the repository:
  a grep for "does the game use the RNG?" finds it and appears to answer yes.
  The change must make the distinction legible, because that ambiguity is a
  recurring cost for every future reader.
- `DomainResult<T>` is genuinely inert. Its removal is a two-line change plus
  the test cases that exercise it in isolation. Its retention is also
  defensible as a ready-made result type for the first operation that returns a
  value. Both are fine; leaving it undecided is not.
- `ErrorCodes` is a flat const-string registry. Adding codes is free, and the
  presence of unused codes is therefore likely to recur. A guard is more
  valuable here than a one-time cleanup.
- The warnings-as-errors change (`pin-build-toolchain-and-centralize-packages`)
  does **not** catch any of this: C# does not warn about unused public members
  or unused const fields, and `DomainResult<T>` is public in a library
  assembly, so nothing is ever "unused" from the compiler's perspective.

## Goals / Non-Goals

**Goals**

- Give every item of dormant surface an explicit, recorded decision.
- Make the "is the RNG actually used?" question answerable correctly at a glance
  and by tooling.
- Prevent the class from recurring, with a guard rather than a cleanup.
- Preserve the determinism policy and the persisted field.

**Non-Goals**

- Adding a random mechanic. Manufacturing a consumer to justify existing code
  would be the speculative scope growth the repository's own rules forbid, and
  would put an unexercised determinism path into progression.
- Refactoring the generator.
- Changing `GameState.Rng`'s persisted representation.

## Decisions

### 1. Keep the generator and the field; change its documented status

The generator is infrastructure the platform will need (D-017 is a sound
policy), the field is already persisted and migrating it away costs more than
keeping it, and the determinism discipline the generator embodies is
load-bearing. What is wrong is not the code — it is that the code reads as
live while being inert.

So: keep both, and add an explicit statement, in the class doc comment, that no
canonical consumer exists today and that the first consumer must add an
end-to-end determinism test. Also correct D-017 to say so.

*Alternative considered:* delete the generator and the field. Rejected: it
deletes working, tested, policy-required infrastructure and requires a schema
migration to remove a field that harms nothing.

*Alternative considered:* wire the RNG into something now. Rejected as
speculative: it would put randomness into canonical progression purely to
validate the existence of a random number generator, which is the opposite of
this repository's evidence-driven discipline.

### 2. Add an honesty test that pins the current property

A test asserting that two profiles created with **different** seeds currently
reach identical canonical state. Its purpose is not to enshrine that behaviour
— it is to make the next behaviour change fail loudly. When someone adds the
first canonical consumer, this test breaks, and the break forces a deliberate
decision and a new determinism test rather than a silent semantic change to
seed semantics.

*Alternative considered:* no test. Rejected: then the first consumer is
introduced with no signal at all.

### 3. Make the distinction between test-data RNG and canonical RNG legible

`SeededProgressionPropertyTests`'s use of the generator is legitimate and should
stay. What must change is that a reader can tell the difference without
inferring. Rename the local construction to make the role explicit (for example
a clearly-named property-test data generator) and add a one-line comment saying
this is test input, not canonical randomness. This costs nothing and removes
the most likely source of a future wrong conclusion.

### 4. `DomainResult<T>`: remove it, and record why

The non-generic `DomainResult` is the type every use case actually uses. The
generic one has no consumer and no near-term consumer, and keeping a parallel
untested result type is a trap: the first operation that returns a value will
either use it (with no production experience behind it) or ignore it (leaving
two result types forever).

Remove it, remove its two test cases, and record in the decision log that if a
value-returning use case appears, the choice is made then with a real
requirement in hand.

*Alternative considered:* keep it. Rejected as above; the cost of keeping is
small but the ambiguity it creates is not zero, and there is no external
consumer to justify compatibility.

### 5. Remove the three unused error codes, and add a guard

`NotAvailable`, `QueueEmpty` and `InvalidArgument` are never returned. Removing
them is safe because error-code strings are internal to the application layer
(there is no wire protocol, no persisted error code, and no localization
lookup keyed on them), and `GameSession` constructs its messages inline.

For the guard: a test that reflects over `ErrorCodes` and asserts every declared
constant is referenced somewhere in `src/`, plus a test that
`DomainResult<T>`-style parallel types do not reappear. The error-code guard is
the valuable one — the unused-code pattern is guaranteed to recur, and each
occurrence is a small lie about what the system distinguishes.

### 6. Report rather than enforce where a guard would be brittle

The error-code guard should report **all** unused codes in one failure message
rather than stopping at the first, so a single run tells the implementer the
full list. A brittle guard that cries wolf gets disabled, and a disabled guard
is worse than none.

## Risks / Trade-offs

- **[Keeping an inert generator indefinitely]**
  → Accepted deliberately, with a bounded review cost: the honesty test, the
  corrected decision record, and the explicit doc statement make it a known,
  deliberate state rather than a latent surprise. Removing it later is a small,
  well-understood change.

- **[Removing `DomainResult<T>` could be wrong if a value-returning use case is
  imminent]**
  → The M7 platform-integration work is the plausible candidate, and it is
  externally blocked (KI-002). Reintroducing a result type when there is a real
  requirement is cheap; keeping an untested one "just in case" is the state this
  change removes.

- **[The error-code guard may flag legitimate platform-scaffold codes]**
  → Future adapters may legitimately declare codes before the operations exist.
  The guard therefore permits an explicitly listed set of reserved codes with
  reasons, which is a reviewable act.

- **[Removing error codes could break string-matching consumers]**
  → Verified none exist: the codes are returned in-process, surfaced through
  `DomainError.Message`/`Code`, and asserted only in tests. No persistence, no
  wire format, no localization table. The task list requires confirming this
  before removal rather than assuming it.

## Migration Plan

None. No persisted field is removed, no payload changes, no schema bump.

Rollback: a plain revert. The only externally visible effect is a removed unused
public type from `WalkGame.Domain`, which nothing references.

## Open Questions

None. Each dormant item has a recommended decision and the task list requires an
explicit recorded decision for every one of them, including the option to
override the recommendation.
