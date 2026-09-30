# Resolve the dormant canonical-randomness and unused result surface

## Why

The repository ships, persists, round-trips and unit-tests a canonical random
number generator that **nothing in the product ever uses**.

`DeterministicRng` is a complete, well-specified xoshiro256\*\* implementation
with SplitMix64 seeding, rejection-sampled `NextInt64`, and a persisted
`RngState` struct. `GameFactory.NewGame` seeds it from the `ulong seed` argument
and stores the snapshot in `GameState.Rng`, which `SaveCodec` serializes and
`SaveCodecRoundtripTests` asserts round-trips field by field, and
`tools/simulation` prints in `dump`. Eight `DeterministicRngTests` cover
seeding, determinism across instances, snapshot/restore continuation, all-zero
state normalization, and the bounds of each draw.

Every one of those consumers is a test or a diagnostic. Grepping the production
and tool sources for any use of the generator:

```text
$ grep -rn "NextUInt64|NextInt64|NextDouble" --include=*.cs src tools
src/WalkGame.Domain/Randomness/DeterministicRng.cs:37, 63, 72, 79   (definitions only)
```

So the practical consequences:

- **`StartNewGame(ulong seed)` cannot affect any outcome.** The seed is written
  to the save and read back, and nothing else. Two profiles run with different
  seeds produce identical canonical state.
- **D-017 is satisfied vacuously.** "Expedition/discovery outcomes that use
  randomness derive from persisted deterministic seeds/state" describes a
  mechanism with no consumer. The *policy* is good and must be kept; the
  *implementation* is currently inert.
- **The first future random mechanic inherits an unexercised path.** Adding one
  would be the first time `Rng` is read, first time the seed matters, and first
  time a determinism regression could be introduced — with no end-to-end test
  proving the seed actually flows through save/load into outcomes.
- **`DomainResult<T>` is also dead.** `Results.cs` defines a full generic
  result type with `Ok(T)`, `Fail(code, message)`, `Fail(DomainError)`, a
  `Value` and an `Error`. It has **zero** production or tool references; the
  only uses are two lines in `IdsAndResultsTests` testing it in isolation.
- **Three error codes are never produced**: `ErrorCodes.NotAvailable`,
  `ErrorCodes.QueueEmpty`, `ErrorCodes.InvalidArgument`. Every other code in the
  family is returned by a real operation.

None of this is harmful at runtime. It is dormant surface area that reads as
live: a reader of `GameState.Rng` reasonably concludes randomness is in play,
and a reader of `ErrorCodes` reasonably concludes those conditions are
distinguished somewhere.

## What Changes

- The determinism **policy** is preserved and made explicit: canonical randomness
  is seeded, persisted, and never generated in presentation code. The decision
  record is corrected to state that no consumer exists today.
- The dormant surface is **resolved by an explicit decision per item**, not by
  silence: each is either given a documented, tested consumer path, or removed.
  The task list requires a decision for every item and forbids leaving any
  ambiguous.
- A guard is added so the "dead surface" class is detectable: unused error
  codes and unused public result types are reported rather than left to a
  reader's judgement.
- The seed parameter is documented for what it actually is today, and a
  determinism test asserts the current honest property (same seed and different
  seed both yield identical state) so that the moment a consumer is added, the
  test must be revisited deliberately.

**Non-goals:** adding a random mechanic. Inventing randomness to justify the
code would be exactly the speculative scope growth the repository's own scope
rules forbid. Removing the generator without a decision record is also rejected
— the policy is sound and the platform will need it.

## Impact

**Evidence from the audit**

- `src/WalkGame.Domain/Randomness/DeterministicRng.cs` — the only definitions
  of `NextUInt64` / `NextInt64` / `NextDouble`; no call sites outside the file.
- `src/WalkGame.Domain/GameState.cs` — `Rng = new DeterministicRng(seed).Snapshot()`
  in `GameFactory.NewGame`; `Rng` is otherwise only read by the codec, the
  validator round-trip test, and `tools/simulation/Out.cs` for display.
- `tests/WalkGame.Domain.Tests/DeterministicRngTests.cs` — 8 tests, all
  generator-internal.
- `tests/WalkGame.Domain.Tests/SeededProgressionPropertyTests.cs` — uses
  `DeterministicRng` as a *test-data* generator (`rateMilliPerDay = 500L +
  (long)(rng.NextUInt64() % 4_500UL)`), never as canonical game randomness. This
  is the most easily misread reference in the repository and must not be mistaken
  for a production consumer.
- `src/WalkGame.Domain/Common/Results.cs` — `DomainResult<T>` has 2 references
  in `IdsAndResultsTests` and none in `src/` or `tools/`.
- `ErrorCodes.NotAvailable`, `ErrorCodes.QueueEmpty`, `ErrorCodes.InvalidArgument`
  — 0 references outside their declarations.
- `docs/DECISIONS.md` D-017 — states the policy without noting that no
  consumer exists.

**Affected files**

- `src/WalkGame.Domain/Common/Results.cs` — `DomainResult<T>` removal or
  retention.
- `src/WalkGame.Domain/Common/Results.cs` — three unused `ErrorCodes` members.
- `src/WalkGame.Domain/Randomness/DeterministicRng.cs` and
  `src/WalkGame.Domain/GameState.cs` — documented decision.
- `tests/WalkGame.Domain.Tests/IdsAndResultsTests.cs` — adjusted to the
  decision.
- Documentation: `docs/DECISIONS.md`, `docs/AGENT_EXECUTION_GUIDE.md`.

**Note on `GameState.Rng`**

`Rng` is persisted state. Removing the field would be a payload change needing
a migration, and would contradict D-017's policy. The expected decision is to
**keep the field and the generator**, document that it is infrastructure for a
future consumer, and add the honesty test. The task list states this
expectation so a reviewer is not surprised, but leaves the call to the
implementer with an explicit requirement that removing it needs a decision
record and a migration.

**Dependencies**

- Independent of every other change.
