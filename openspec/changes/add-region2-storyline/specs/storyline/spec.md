## ADDED Requirements

### Requirement: A later region's storyline continues, never replaces, the earlier one
A region after the first SHALL unlock only after the previous region's closure milestone. The previous region's chapters SHALL stay unlocked and readable, and its post-completion program SHALL continue.

#### Scenario: Region 1 closure offers Region 2
- **WHEN** Region 1 reaches closure
- **THEN** Region 2's prologue unlocks and Region 1's epilogue and chapters remain readable

#### Scenario: A Region-1-only save after the upgrade
- **WHEN** a save made before Region 2 existed loads
- **THEN** it loads as Region 1 with no progress lost, and Region 2 is offered only if Region 1 closure holds
