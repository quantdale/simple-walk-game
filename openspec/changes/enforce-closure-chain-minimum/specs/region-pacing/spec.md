## MODIFIED Requirements

### Requirement: Closure is defined by a criterion, not by a raw effort total

Region closure SHALL be decided by the full authored closure criterion on every route. Completing the closure milestone SHALL NOT close a region while fewer than the criterion's minimum chains are complete. A completion recorded under an earlier, weaker rule SHALL remain valid state and SHALL NOT be reset.

#### Scenario: Milestone funded before the last chain
- **WHEN** a player completes the valley survey while the canopy walkway is unbuilt
- **THEN** the region is not completed, no epilogue is shown, and the canopy walkway remains available to choose

#### Scenario: Region closes when the criterion holds
- **WHEN** the last project of the last incomplete chain completes after the survey
- **THEN** the region completes in that same step, and every chapter up to "Every Mile Mapped" is unlocked

#### Scenario: A save closed under the old rule
- **WHEN** a save records the region complete with the survey done and a chain unbuilt
- **THEN** it loads without a state violation, stays complete, and the unbuilt project can still be funded to unlock the remaining chapters
