## ADDED Requirements

### Requirement: Characters are presented where they are, with a mood from progress

The Unity seam SHALL present every character to its view on each refresh with its current location (or absence) and the expression of its current line, as style-neutral sprite, mood and portrait-expression keys.

#### Scenario: A milestone changes the mood
- **WHEN** a project completes that gives a present character a new line
- **THEN** the next refresh presents that character with the new line's expression

### Requirement: Characters and inspectable props can be tapped

A tap on a present character at an open place SHALL make the character react and open that character's current line with the place's actions. A tap on a prop bound to a project SHALL open a read-only inspect card once the project is no longer locked. Unknown, absent or locked targets SHALL open nothing. Hit-testing SHALL NOT require a physics package.

#### Scenario: Tap the bridge before it can be worked on
- **WHEN** the bridge project is still locked
- **THEN** a tap on the bridge prop opens nothing

### Requirement: Dialogue actions never gate progression

Dialogue SHALL offer Inspect, Help restore (when the place's focus goal can be picked or is underway) and Maybe later. Taking an action SHALL NOT change canonical state; Help restore opens the goal's card, whose start action is the existing `EnqueueProject` command.

#### Scenario: Decline and walk
- **WHEN** the player leaves a dialogue open or declines with Maybe later and keeps walking
- **THEN** progress applies and the save equals that of a player who never talked
