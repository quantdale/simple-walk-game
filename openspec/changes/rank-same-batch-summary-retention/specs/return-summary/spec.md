## MODIFIED Requirements

### Requirement: Items rotate per kind, and rotation preserves priority

Each summary item kind SHALL have its own retention bound. When a kind is at its bound, adding a new item of that kind SHALL retire an item **of that same kind** to make room: one from the oldest committed batch (earliest first-commit time). Within that batch, the item with the lowest authored retention rank goes first, and the incoming item competes when it belongs to that batch. For transformation items, a project's own completion line SHALL outrank chapter, expedition, count, arc and landmark-stage lines, and the region-complete line SHALL outrank all of them. Kinds without ranks SHALL retire the oldest item. Rotation SHALL NOT promote a lower-priority kind above a higher-priority kind, and SHALL NOT evict a higher-priority item to make room for a lower-priority one.

Rotation SHALL be deterministic: the same sequence of committed events SHALL produce byte-identical summary state.

#### Scenario: New transformation items displace old transformation items

- **WHEN** the transformation kind is at its retention bound and a new transformation item is committed in a later commit
- **THEN** an item from the oldest committed batch is retired
- **THEN** the new transformation item is present
- **THEN** no item of any other kind was retired

#### Scenario: A crowded commit keeps every project completion line

- **WHEN** one commit produces more transformation lines than the bound, including project completions, chapters, landmark stages, arc lines and the region completion
- **THEN** every project completion line and the region completion line are present
- **THEN** the landmark-stage and arc lines are the first retired

#### Scenario: A production item never displaces a transformation item

- **WHEN** the summary already holds the maximum number of transformation items and a new production item is committed
- **THEN** the production item is present
- **THEN** at least one transformation item is still present
- **THEN** the documented priority ordering is still respected

#### Scenario: Rotation is byte-deterministic across reruns

- **WHEN** the same sequence of committed events is replayed twice from the same starting state
- **THEN** both runs produce byte-identical pending summary state
