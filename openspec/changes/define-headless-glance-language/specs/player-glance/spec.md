## Purpose

Turns the facts the headless shell already has into the few calm sentences a
player needs for a glance: what changed, what is being restored, and whether
anything must be done. It does not decide progression, and it is not a screen.

## ADDED Requirements

### Requirement: A glance answers three questions in bounded sentences

A player glance SHALL answer exactly three questions in calm English: what
changed, what is being restored now, and whether the player must do anything.
It SHALL expose at most one primary next action, or an explicit statement that
nothing needs attention. The "what changed" answer SHALL contain at most one
lead sentence plus an optional count of further current changes, and SHALL NOT
require the player to read every pending summary item. A numeric activity
aggregate SHALL NOT be the lead when a world transformation is current.

Reading a glance SHALL NOT mutate canonical state, acknowledge a summary,
credit activity, or change any balance.

#### Scenario: A return with a world change leads with the transformation

- **WHEN** a glance is taken while a current transformation is pending and an
  activity aggregate was also committed
- **THEN** the lead change sentence describes the world transformation
- **THEN** the activity aggregate is not the lead sentence
- **THEN** the glance does not list every pending summary item
- **THEN** canonical balances and the pending summary are unchanged by the read

#### Scenario: Nothing current is an explicit quiet state

- **WHEN** a glance is taken and no current change is pending and no attention
  is required
- **THEN** the change sentence states that nothing meaningful changed
- **THEN** the attention sentence states that nothing needs attention
- **THEN** no primary action is offered

### Requirement: Glance attention follows the existing classification

The glance SHALL derive "must the player do anything" from the home attention
classification already produced for the shell. It SHALL NOT invent a second
attention policy. The completed-region classification SHALL remain a
conclusion: the glance SHALL NOT instruct the player to queue restoration work
that no longer exists, and SHALL NOT report that attention is required solely
because the region is complete. Before completion, an empty queue with banked
progress SHALL still be reported as requiring attention.

The sentences SHALL be:

- pending current summary: the lead change, plus the existing primary action
  when one is offered
- empty queue with banked progress, region not complete: "Restoration is
  paused. Your movement is saved until you choose a project." and the action
  "Choose a restoration project."
- no project started yet: "Choose the first thing to restore." and attention
  is not required
- region complete: "{region title}'s restoration is complete." and attention
  is not required
- otherwise, when nothing needs attention: "Nothing needs your attention right
  now."

#### Scenario: A finished valley is a conclusion, not a to-do

- **WHEN** the region is complete and a glance is taken
- **THEN** the attention sentence names the region and states that restoration
  is complete
- **THEN** the glance does not require attention
- **THEN** the glance does not offer an action to queue a restoration project

#### Scenario: Banked progress before completion still asks for a choice

- **WHEN** the region is not complete, no project is active or queued, and
  unallocated progress is banked
- **THEN** the glance requires attention
- **THEN** the attention sentence is "Restoration is paused. Your movement is
  saved until you choose a project."
- **THEN** the only primary action is "Choose a restoration project."

#### Scenario: A fresh profile is informed, not alarmed

- **WHEN** no project has ever been started and no progress is banked
- **THEN** the glance does not require attention
- **THEN** the sentence is "Choose the first thing to restore."

### Requirement: What is being restored is a title, not an identifier

When a project is active, the glance SHALL name it by its display title and
SHALL state progress as invested amount out of cost. When no project is active
and one is queued, the glance SHALL name the first queued title as what is
next. When the region is complete, the glance SHALL describe the authored
post-completion use if one exists, and SHALL NOT present an absent project as
if it were active. A stable content identifier SHALL NOT appear in a glance
sentence when a display title exists.

#### Scenario: The active project is named in player language

- **WHEN** a project with a display title is active
- **THEN** the restoring-now sentence contains that title
- **THEN** the sentence states the invested amount and the cost
- **THEN** the sentence does not contain the project's stable identifier

#### Scenario: Completion does not invent an active project

- **WHEN** the region is complete and no project is active
- **THEN** the restoring-now sentence does not name a project that is not active
- **THEN** if an authored post-completion use exists, the sentence names that use

### Requirement: Landmark stages use phrases, not stage identifiers

A glance sentence that describes a landmark stage SHALL use the landmark's
display title and exactly one of these phrases:

- still damaged: "is still damaged"
- safe to approach: "is safe to approach"
- working again: "is working again"
- brought back: "is restored"
- thriving: "is thriving"

The glance sentence SHALL NOT contain the stage identifiers `Ruined`,
`Stabilized`, `Functional`, `Restored`, or `Flourishing` as whole words, and
SHALL NOT use the form "has reached" followed by a stage identifier. The
lowercase word "restored" inside "is restored" is the brought-back phrase and
is permitted. A newly composed durable summary line for a landmark stage SHALL
use the same title-plus-phrase form. An already-stored summary line SHALL NOT
be rewritten, including a stored line whose text is "Old Trailhead has reached
Stabilized."

#### Scenario: A new landmark advance is stored as a phrase

- **WHEN** a landmark whose title is "Old Trailhead" newly reaches the stage
  that means safe to approach, and a summary line is composed
- **THEN** the composed line is "Old Trailhead is safe to approach."
- **THEN** the composed line does not contain "Stabilized" or "has reached"

#### Scenario: A historical enum sentence survives a glance

- **WHEN** a save already contains the summary text "Old Trailhead has reached
  Stabilized." and a glance is read
- **THEN** the stored summary text is still "Old Trailhead has reached
  Stabilized."
- **THEN** the glance sentence for that landmark does not contain
  "Stabilized" or "has reached"

### Requirement: Activity status is a player sentence, not a fault dump

The glance SHALL include one activity sentence for the current player-facing
activity status, and that sentence SHALL match the status:

- connected and current: "Your movement is connected."
- permission needed: "Connect movement to restore this world. Only walking
  activity is needed, and you can decide later."
- permission denied or revoked: "Movement access is off. Your progress is
  still here, and you can keep looking around."
- source unavailable: "Movement cannot be refreshed on this device. Your
  progress is still here."
- waiting for first data: "Connected. The world is waiting for your first
  walk."
- refresh temporarily failed: "Movement could not be refreshed just now.
  Nothing was lost."

The activity sentence SHALL NOT contain raw exception text, exception type
names, provider record identifiers, or health payloads. A denied permission
SHALL NOT remove the ability to read the rest of the glance.

#### Scenario: Denial explains the impact and does not trap the glance

- **WHEN** activity permission is denied or revoked
- **THEN** the activity sentence is "Movement access is off. Your progress is
  still here, and you can keep looking around."
- **THEN** the three glance questions are still answered
- **THEN** the sentence does not contain an exception message

#### Scenario: A failed refresh does not sound like lost progress

- **WHEN** the last refresh failed temporarily and earlier progress exists
- **THEN** the activity sentence is "Movement could not be refreshed just now.
  Nothing was lost."
- **THEN** the sentence does not name an exception type

### Requirement: Onboarding copy follows the existing stage, including denial

When onboarding is incomplete, the glance SHALL include the sentence for the
current onboarding step and no later step:

- premise: "Your movement restores this world."
- world baseline: the display title of one damaged landmark, plus "is still
  damaged"
- activity connection: "Connect movement to restore this world. Only walking
  activity is needed, and you can decide later."
- first project: "Choose the first thing to restore."
- away progress: "Progress can continue while you are away."
- exit: "You do not need to keep this open."

A denied permission SHALL NOT skip, block, or repeat the activity-connection
explanation as a trap, and SHALL leave the remaining glance readable. When
onboarding is complete, the glance SHALL NOT include an onboarding sentence.

#### Scenario: Denial leaves the rest of the glance readable

- **WHEN** onboarding is on the activity-connection step and permission is denied
- **THEN** the onboarding sentence is the activity-connection sentence
- **THEN** the glance still answers what is being restored and whether
  attention is required
- **THEN** the glance does not claim onboarding is complete

#### Scenario: A completed onboarding adds no tour sentence

- **WHEN** onboarding is complete
- **THEN** the glance contains no premise, baseline, or exit sentence

### Requirement: Glance copy does not shame or expose private data

Glance sentences SHALL NOT contain streak warnings, missed-goal language,
weight or medical claims, raw exception text, stack traces, provider record
identifiers, or health payloads. They SHALL NOT contain a stable content
identifier when a display title exists for that subject. The same glance
inputs SHALL produce the same sentences.

#### Scenario: A hostile error string cannot become player copy

- **WHEN** an activity refresh fails with an exception whose message contains
  a provider record identifier and a health-like payload
- **THEN** no glance sentence contains that message, that identifier, or that
  payload
- **THEN** repeating the read produces the same sentences

### Requirement: A headless inspection surface prints the glance without touching evidence output

A headless inspection command SHALL print the three glance answers, the
activity sentence, and the onboarding sentence when one is required. Existing
evidence commands that print home facts, save dumps, pacing reports, or
validation verdicts SHALL keep their existing output for the same save. The
inspection command SHALL NOT write the save.

#### Scenario: Inspecting a glance does not rewrite evidence lines

- **WHEN** the glance inspection command is run against a save, and the
  existing home-fact command is run against the same save
- **THEN** the inspection output contains the three glance answers
- **THEN** the home-fact output is unchanged from its previous contract
- **THEN** the save bytes are unchanged
