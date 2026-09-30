## Purpose

Defines the state a region is in once its closure milestone has been reached:
that completion is permanent and detected exactly once, that the game asks for
nothing the player cannot act on, that activity earned after completion has a
defined and honest use, that every surface reports the completed state
truthfully, and that the completed state never regresses.

## ADDED Requirements

### Requirement: The game never asks for work it cannot offer

Once every authored work item in a region is complete, the game SHALL NOT
present any signal instructing the player to start, queue, or choose further
restoration work. No surface SHALL report that the player's attention is
required on the basis of unallocated progress while no such work exists.

The completed state SHALL be reported as an explicit, first-class
classification, so a shell can present it as a conclusion rather than as an
outstanding to-do.

#### Scenario: A completed region reports no outstanding work

- **WHEN** the region's closure milestone is complete and no further authored
  work item is available
- **THEN** the home surface reports that nothing needs attention
- **THEN** the reason given is the completed-region classification, not an
  instruction to queue work
- **THEN** no progress indicator is presented as actionable

#### Scenario: Unallocated progress after completion is not an alarm

- **WHEN** the region is complete and the player continues earning progress
- **THEN** the unallocated progress is not reported as requiring attention
- **THEN** the progress is reported through its post-completion use instead

#### Scenario: The attention signal is still honest before completion

- **WHEN** the region is **not** complete and unallocated progress sits with an
  empty queue
- **THEN** the home surface still reports that attention is required
- **THEN** the reason identifies the empty queue with banked progress
- **THIS** guarantee is not weakened by the completed-region exemption

#### Scenario: The summary offers no impossible next action after completion

- **WHEN** the region is complete and a return summary is produced
- **THEN** the summary's primary next action is not an instruction to queue a
  restoration project
- **THEN** any action it does offer is one the player can actually take

### Requirement: Progress earned after completion has a defined use

Progress earned after region completion SHALL have a defined use. The use SHALL
be authored content, validated with the rest of the region's content, and
presented so the player can see what their continued activity achieves.

If a resource is intentionally a lifetime score with no further use, that SHALL
be stated explicitly and SHALL NOT be presented as an outstanding objective.

#### Scenario: Continued activity is visibly consumed

- **WHEN** a player earns progress after region completion
- **THEN** the progress is applied to an authored post-completion use
- **THEN** the state reflects that use rather than an ever-growing unused bank
- **THEN** the surface reports the use, not an actionable deficit

#### Scenario: The post-completion use is validated as content

- **WHEN** the authored post-completion use is validated
- **THEN** it is subject to the same reference, reachability and arithmetic
  rules as every other authored node
- **THEN** an unreachable or unrepresentable post-completion use is a content
  validation failure

#### Scenario: A resource with no use is declared, not implied

- **WHEN** a resource is a lifetime score with no sink by design
- **THEN** that fact is stated in the region-surface contract
- **THEN** it is not presented as progress toward an unattainable goal

### Requirement: Every surface reports the completed state truthfully

Every surface that can present progress SHALL report the completed state's real
facts: that the region is complete, when it completed, and what remains
operating. No surface SHALL imply unfinished work, offer an unavailable
objective, or reset to a pre-completion appearance.

#### Scenario: The region surface reports completion and its remaining activity

- **WHEN** the region is complete
- **THEN** the region surface reports completion and the completion instant
- **THEN** operating producers and post-completion activity are still reported
- **THEN** no surface implies a chain is still pending

#### Scenario: The projects surface reflects an exhausted roster

- **WHEN** the region is complete
- **THEN** every project reads as complete
- **THEN** no project is presented as ready, queued, or actionable

#### Scenario: The summary does not regress to a pre-completion appearance

- **WHEN** a summary is produced in the completed state
- **THEN** it does not present pre-completion guidance
- **THEN** repeated summaries in the completed state converge rather than
  accumulating contradictory guidance

### Requirement: The completed state is permanent and stable

Region completion SHALL be detected exactly once, SHALL never reset, and SHALL
remain valid across reloads, restarts and long absences. A profile that has
completed a region SHALL continue to report that region as complete.

#### Scenario: Completion is detected once

- **WHEN** a profile reaches its closure milestone
- **THEN** completion is recorded exactly once
- **THEN** no later commit re-detects or duplicates it

#### Scenario: Completion survives reload and long absence

- **WHEN** a completed profile is reloaded, restarted, or advanced across a long
  absence
- **THEN** it still reports the region as complete
- **THEN** the state validates

#### Scenario: Continued activity does not reopen or reset anything

- **WHEN** a completed profile continues to earn and apply progress
- **THEN** no completed work is undone
- **THEN** the completed region is not reset to an earlier stage
