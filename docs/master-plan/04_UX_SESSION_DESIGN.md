# 04 — UX and Session Design

## 1. Information architecture

Recommended four-tab navigation:

1. **Today** — activity + away report + immediate world impact.
2. **World** — regional map/restoration visualization.
3. **Projects** — priorities, queue, automation, decisions.
4. **Journey** — chronological history, discoveries, milestones.

Profile/settings live behind a top-level avatar/settings entry rather than consuming a primary tab.

## 2. Today screen

The Today screen must answer in under five seconds:

- What did I do?
- What did it accomplish?
- What happens next?

Suggested hierarchy:

1. World-change hero card.
2. Today/weekly movement summary.
3. “Since you were away” digest if new events exist.
4. Current project progress and projected next milestone.
5. One optional decision CTA.
6. Quick link to Visit World only if desired.

Do not lead with a dense fitness dashboard.

## 3. First launch

Onboarding sequence:

1. One-screen fantasy: “Your movement restores a dying world.”
2. Explain zero-required-screen-time behavior.
3. Let user preview Ashfall Basin before permissions.
4. Explain requested activity types in plain language.
5. Request minimal health permissions.
6. Import recent bounded history only after consent.
7. Show first conversion and first visible restoration effect.
8. Default allocation policy to Balanced.

Avoid asking for notification permission at first launch. Ask after the user experiences a meaningful project completion and explain what notifications would do.

## 4. Permission UX

Permission state must be treated as a normal state, not an error modal.

States:

- not requested;
- partially granted;
- granted;
- denied/restricted;
- provider unavailable;
- needs settings action.

If steps are denied but workout duration is available, degrade gracefully rather than blocking the entire app.

## 5. Away report UX

Report principles:

- one scrollable sheet/card, not a sequence of mandatory modals;
- before/after visual when a region changed materially;
- activity -> allocation -> outcome causality visible;
- “View all changes” goes to Journey;
- report is acknowledged automatically by viewing, not by tapping “Claim.”

Example:

```text
While you were away
8,432 steps · 6.1 km · 42 active min

North River purification completed
Forest restoration +7%
Old Mill reconstruction 68% -> 91%
Expedition: Ridge Observatory discovered

Your Balanced policy used 74 Vitality:
40 Nature · 22 Rebuild · 12 Exploration
```

## 6. Projects UX

Project list should show:

- what changes visually;
- progress;
- category;
- why it is available/locked;
- expected effect;
- allocation priority.

Provide queue controls simple enough for a 30-second session:

- drag/reorder or “Prioritize” action;
- preset allocation policy;
- optional advanced percentages.

Do not expose spreadsheet-like controls by default.

## 7. World UX

World screen initially uses a fast, legible region map/diorama rather than requiring real-time 3D traversal.

Capabilities:

- pan/zoom;
- tap region/landmark;
- stage transitions;
- animated ecology/water/infrastructure state;
- before/after comparison;
- project hotspots;
- expedition fog/discovery areas;
- reduced-motion mode.

## 8. Journey UX

Journey is the durable history of what the user's activity accomplished.

Entries:

- project completion;
- region stage change;
- significant activity milestone;
- expedition discovery;
- decision result;
- rare narrative event.

Group repetitive changes by day/week.

## 9. Decision UX

Decision surfaces:

- show no more than one high-priority decision on Today;
- list all pending decisions under Projects;
- give consequences in player language;
- allow “Decide later” without penalty;
- optional policy-based auto-resolution can be added later.

## 10. Low-attention design

The app must be useful when used distractedly after a workout.

Requirements:

- large tap targets;
- no mandatory rapid interactions;
- clear hierarchy;
- concise copy;
- stable navigation;
- offline states explicit;
- skeletons only where necessary;
- reconciliation status subtle but inspectable.

## 11. Accessibility

Minimum:

- dynamic type / font scaling;
- screen-reader labels and meaningful accessibility roles;
- WCAG-informed contrast;
- no color-only state encoding;
- reduced motion;
- haptics optional;
- captions/text equivalents for audio cues;
- support wheelchair movement data where platform/provider and product logic permit;
- do not use shaming language for lower activity.

## 12. Notification design

Default categories:

- major project completed;
- expedition complete;
- weekly world digest.

Off by default or conservatively permissioned until contextual prompt.

Rules:

- quiet hours;
- digest multiple events;
- no more than one non-critical notification/day by default;
- no “You’re losing your streak!” language;
- tapping deep-links to the exact world/project report;
- notification generation must tolerate delayed background execution.

## 13. Empty/error states

Examples:

No activity yet:
> Take your day normally. When movement appears in your health data, Ashfall Basin will start to recover.

Permission missing:
> Movement access is off. Your existing world is safe; reconnect activity access when you want progression to resume.

Provider sync delayed:
> Your latest movement has not arrived yet. We’ll reconcile it the next time your health store makes it available.

## 14. Product copy rule

Prefer consequence language:

- “Your 4.2 km reopened the eastern trail.”

Over abstract economy language:

- “You earned +42 coins.”

Vitality can appear as an explanatory secondary value, but the world consequence is the emotional headline.
