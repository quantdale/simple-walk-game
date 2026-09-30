## Purpose

Defines the durable, bounded re-entry contract: what a return summary is, how
its items are prioritised, how it stays inside the glance budget while still
reporting newly earned progress, how it survives a crash between committing and
presenting, how it is acknowledged, and how the Home surface derives its
"does this need my attention" signal from it.

## ADDED Requirements

### Requirement: The pending summary stays inside a hard total bound

The pending return summary SHALL never contain more items than its documented
hard total bound, under any sequence of committed progress, restarts or
replays. A summary that has reached the bound SHALL still report newly earned
progress of every kind by retiring older items, and SHALL never exceed the bound
while doing so.

#### Scenario: A long single session cannot overflow the bound

- **WHEN** a single committed batch produces more transformation, discovery,
  expedition and progression events than the total bound allows
- **THEN** the resulting summary contains exactly the bound or fewer items
- **THEN** the retained items are the most recent ones
- **THEN** the resulting summary still validates against canonical state

#### Scenario: The bound survives repeated restarts without acknowledgement

- **WHEN** a profile is repeatedly advanced, restarted and advanced again
  without ever acknowledging the pending summary
- **THEN** the summary never exceeds the bound
- **THEN** the summary continues to contain recent items rather than becoming
  frozen at the first saturation point

### Requirement: Items rotate per kind, and rotation preserves priority

Each summary item kind SHALL have its own retention bound. When a kind is at
its bound, adding a new item of that kind SHALL retire the oldest item **of that
same kind** to make room. Rotation SHALL NOT promote a lower-priority kind above
a higher-priority kind, and SHALL NOT evict a higher-priority item to make room
for a lower-priority one.

Rotation SHALL be deterministic: the same sequence of committed events SHALL
produce byte-identical summary state.

#### Scenario: New transformation items displace old transformation items

- **WHEN** the transformation kind is at its retention bound and a new
  transformation item is committed
- **THEN** the oldest transformation item is retired
- **THEN** the new transformation item is present
- **THEN** no item of any other kind was retired

#### Scenario: A production item never displaces a transformation item

- **WHEN** the summary already holds the maximum number of transformation items
  and a new production item is committed
- **THEN** the production item is present
- **THEN** at least one transformation item is still present
- **THEN** the documented priority ordering is still respected

#### Scenario: Rotation is byte-deterministic across reruns

- **WHEN** the same sequence of committed events is replayed twice from the same
  starting state
- **THEN** both runs produce byte-identical pending summary state

### Requirement: Item ordering within a kind is deterministic and insertion-ordered

Items SHALL be presented in descending priority order by kind. Within a single
kind, items SHALL be presented in the order in which they were first committed.
The ordering SHALL NOT depend on an unstable sort implementation.

#### Scenario: Within-kind order matches commit order

- **WHEN** several items of the same kind are committed in a known order
- **THEN** they are presented in that same order

#### Scenario: Priority order is stable across repeated composition

- **WHEN** the same set of items is composed repeatedly
- **THEN** the presented order is identical every time

### Requirement: Retired and current items are distinguishable

A summary item SHALL carry the information a presentation surface needs to tell
a newly earned fact from a still-relevant historical one, and the summary SHALL
record when it was generated. A retired item SHALL NOT reappear.

This SHALL be achieved additively: a summary item written by an older build,
which carries no such information, SHALL decode with the documented default
meaning "current".

#### Scenario: An item from an older build reads as current

- **WHEN** a pending summary written before this behaviour existed is loaded
- **THEN** every item it contains is treated as current
- **THEN** the summary is not invalidated and no progress is lost

#### Scenario: An item retired in one session does not return in the next

- **WHEN** an item is retired to make room for a newer item of the same kind
- **THEN** the retired item is absent from every subsequent summary until new
  progress of that kind retires it again

### Requirement: Acknowledgement is idempotent and never alters progression

Acknowledging the pending summary SHALL be a no-op when nothing is pending, and
SHALL NOT alter any earned progression, balance, queue position or world state
under any circumstances. New progress committed after acknowledgement SHALL
produce a new pending summary.

#### Scenario: Acknowledging nothing is a no-op

- **WHEN** acknowledgement is invoked while no summary is pending
- **THEN** the operation succeeds
- **THEN** no canonical state changes

#### Scenario: Acknowledgement is repeatable

- **WHEN** acknowledgement is invoked repeatedly
- **THEN** every invocation succeeds
- **THEN** canonical state is byte-identical before and after the first
  successful acknowledgement

#### Scenario: Progress after acknowledgement is still reported

- **WHEN** new progress is committed after a summary was acknowledged
- **THEN** a new pending summary is produced describing that progress

### Requirement: The pending summary survives a crash between committing and presenting

The summary SHALL be composed from already-committed state and persisted in the
same atomic commit as the progress it describes. A crash after that commit and
before presentation SHALL leave the summary readable at the next boot, and the
underlying progress SHALL never depend on the summary being read or
acknowledged.

#### Scenario: Commit then crash still presents the summary

- **WHEN** progress is committed and the process terminates before the summary is
  presented or acknowledged
- **THEN** the next boot exposes a pending summary describing that progress

#### Scenario: An idle operation leaves the summary untouched

- **WHEN** an operation commits no new events
- **THEN** the existing pending summary is unchanged, including its generation
  timestamp

### Requirement: Home attention is derived from summary currency, not existence

The Home surface SHALL report that the player's attention is required because a
summary is pending only when that summary contains at least one item that is
current rather than purely historical. When the summary exists but is entirely
historical, Home SHALL report that nothing needs attention, while still making
the summary available to read.

A pending summary containing a genuinely new actionable decision SHALL always
report that attention is required, regardless of what else the summary holds.

#### Scenario: A purely historical summary does not demand attention

- **WHEN** the pending summary contains only items that are historical
- **THEN** Home reports that nothing needs attention
- **THEN** the summary is still readable

#### Scenario: A summary with a current actionable decision demands attention

- **WHEN** the pending summary contains at least one current actionable-decision
  item
- **THEN** Home reports that attention is required
- **THEN** the reported reason identifies the pending summary

#### Scenario: Queue-idle banked Vitality is still reported independently

- **WHEN** no project is active or queued and unallocated Vitality is banked, and
  no current summary item exists
- **THEN** Home reports that attention is required
- **THEN** the reported reason identifies banked Vitality with an empty queue
