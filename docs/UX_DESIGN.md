# UX Design Specification

## 1. UX objective

The UX must prove that a persistent progression game can be satisfying **without demanding persistent attention**.

The interface should optimize for:

- immediate comprehension;
- short interactions;
- visible change;
- low maintenance burden;
- graceful return after absence;
- optional depth;
- accessibility;
- trust around activity and permissions.

The UX is not trying to maximize taps, daily opens, session length, or notification response rate.

---

## 2. Attention budget

Every feature should declare its expected interaction cost.

### Tier A — Glance
Target: 5–15 seconds.

A player should understand:

- what major thing changed;
- what is progressing now;
- whether a decision is required.

### Tier B — Check-in
Target: 20–60 seconds.

A player should be able to:

- review return summary;
- see recent activity impact;
- choose/confirm next project;
- leave.

### Tier C — Management
Target: 2–5 minutes.

A player may:

- reorder project priorities;
- configure automation;
- inspect production;
- start expeditions;
- review discoveries.

### Tier D — Visit World
Target: optional 5–20+ minutes.

A player may explore, inspect, customize, and experience the restored world.

No Tier D interaction may be required for Tier A/B progression.

---

## 3. Home screen contract

The Home screen should answer three questions in a few seconds:

1. **What changed?**
2. **What am I restoring now?**
3. **Do I need to do anything?**

Suggested hierarchy:

- major return/change card;
- current project and progress;
- recent eligible activity contribution;
- one next-action button;
- compact region status;
- optional secondary cards for expedition/discovery/producer status.

Avoid dashboards containing ten equally weighted cards.

---

## 4. Return summary

The return summary is the central re-entry mechanism.

### Rules

- show only if meaningful change occurred;
- aggregate low-level simulation;
- lead with transformation, not raw numbers;
- surface one primary next action;
- allow quick dismissal;
- never require reviewing every reward individually;
- do not block access to the app behind long animations.

### Durability and acknowledgement (implemented M3, D-033)

The summary is durable canonical state (`GameState.PendingReturnSummary`), composed before progress is persisted: a crash between committing progress and displaying it cannot lose the explanation of what was earned. Items are typed (transformation / actionable decision / production / notice / aggregate), deduplicated across merges, hard-bounded to 12 entries, ordered transformation-first, with a single derived primary next action or an explicit nothing-needs-attention state. Dismissing it (`AcknowledgeReturnSummary`) is idempotent and never alters earned progression; replayed activity cannot fabricate new "progress" items.

### Priority order

1. landmark/region transformation;
2. completed project;
3. newly available decision;
4. discovery/expedition result;
5. meaningful producer state;
6. aggregate activity/progress numbers.

---

## 5. Activity status UX

Activity integration must be understandable without exposing technical noise.

Player-facing states:

- connected and current;
- permission needed;
- permission denied/revoked;
- source unavailable;
- waiting for first data;
- temporarily unable to refresh;
- data processed successfully.

Do not show raw exception messages.

Diagnostics may expose detailed counts/checkpoints separately.

---

## 6. Permission UX

Permission request sequence:

1. explain the gameplay benefit;
2. state what data category is requested;
3. explain that the app does not need to stay open;
4. state privacy behavior briefly;
5. request system permission;
6. handle denial without trapping the player.

Never request unrelated permissions at first launch “just in case.”

---

## 7. Project selection UX

Projects must feel like meaningful restoration decisions, not a task list.

Each project card/detail should clearly communicate:

- what changes in the world;
- approximate effort/progress requirement;
- prerequisites;
- major unlocks;
- current status;
- whether it can be queued;
- any meaningful trade-off.

Do not force players to inspect hidden stat tooltips to understand consequences.

---

## 8. Queue UX

Queue design should favor resilience.

Required affordances:

- clear active item;
- ordered future items;
- drag/reorder or accessible equivalent;
- remove/pause where allowed;
- explain auto-advance;
- show if queue is empty;
- offer a recommended next project without silently choosing irreversible paths.

If the queue empties, progress should not be catastrophically wasted. A bounded fallback or banked progress policy should exist.

---

## 9. Region UX

The Region view is the strategic map of transformation.

It should communicate:

- restored vs damaged areas;
- current project location;
- locked/available landmarks;
- major routes;
- ecological recovery;
- region completion trajectory.

The lightweight Region view should not require loading the full 3D world.

---

## 10. Visit World UX

Visit World is optional experiential depth.

It should:

- load only on demand;
- start near a meaningful point;
- offer simple navigation;
- show clearly restored landmarks;
- provide inspect interactions;
- avoid high-pressure objectives;
- expose a fast exit back to lightweight UI;
- preserve settings such as reduced motion and quality level;
- be fully consistent with canonical world state.

If the 3D scene fails to load, the rest of the game must remain usable.

---

## 11. Onboarding flow

Recommended sequence:

1. **Premise:** “Your movement restores this world.”
2. **World baseline:** show one clearly damaged landmark.
3. **Activity connection:** explain and request permission.
4. **First project:** select/accept a simple restoration target.
5. **Simulation:** demonstrate that progress can happen while away.
6. **Exit message:** explicitly tell the player they do not need to keep the app open.

Avoid feature-tour carousels.

---

## 12. Copy principles

Tone should be:

- calm;
- optimistic;
- concise;
- non-judgmental;
- focused on world change rather than body metrics.

Prefer:

- “The wetland gained new growth.”
- “Your recent movement advanced the waterworks.”
- “Nothing needs your attention right now.”

Avoid:

- “You failed your goal.”
- “Don’t lose your streak!”
- “Only 800 more steps or you’ll miss today’s reward.”
- medical or weight-loss claims.

---

## 13. Notification UX

Notifications must be sparse enough that receiving one implies meaningful value.

Default-valid events:

- major project completion;
- expedition result;
- meaningful discovery;
- user-configured reminder.

Rules:

- group events where possible;
- respect quiet hours/system settings;
- allow category-level controls;
- never shame inactivity;
- do not create fake urgency;
- deep-link to the relevant lightweight screen, not necessarily the 3D world.

---

## 14. Accessibility

Core flows must be operable without precise gestures or 3D navigation.

Requirements:

- semantic labels for interactive UI;
- meaningful focus order;
- large enough touch targets;
- text scaling support where feasible;
- high-contrast/readable text;
- icons accompanied by labels where ambiguity exists;
- no color-only project/state distinction;
- reduced-motion option;
- disable haptics;
- captions/text equivalents for meaningful audio cues;
- alternatives for drag/reorder actions;
- Visit World not required for progression.

Accessibility regressions are release blockers when they prevent core use.

---

## 15. Reduced motion

Reduced-motion mode should affect:

- camera sweeps;
- parallax;
- screen transitions;
- large restoration animations;
- particle intensity;
- UI bounce/pulse loops;
- motion-heavy celebration sequences.

The player should still receive strong non-motion confirmation through text, sound if enabled, haptics if enabled, and static state change.

---

## 16. Error UX

Every major technical dependency requires a designed state.

### Activity unavailable
Explain that activity cannot currently be refreshed; preserve existing progress.

### Permission denied
Explain the feature impact and provide a settings route if platform-supported.

### Save recovery used
Use calm copy; indicate that the last valid state was restored if user-visible loss could have occurred.

### Migration failure
Do not proceed with partially migrated state. Preserve backup and surface a recoverable support/diagnostic path.

### 3D load failure
Return to lightweight region/home UI without losing game state.

---

## 17. Empty states

Required empty states include:

- no current project;
- no queued projects;
- no discoveries yet;
- no expeditions available;
- no activity data yet;
- no meaningful offline changes;
- producer not unlocked;
- region completed.

Each should explain the next useful action, not merely say “Nothing here.”

---

## 18. Progress visualization

Use layers of progress:

- immediate numeric progress for the active project;
- landmark stage change;
- region-level restoration map;
- environmental presentation change;
- long-term collection/ecology indicators.

Avoid one giant abstract account level as the only progression measure.

---

## 19. Celebration policy

Celebrate meaningful transformation without blocking the user.

### Minor event
Subtle inline feedback.

### Project completion
Short, skippable transition + clear before/after consequence.

### Major landmark/region milestone
Richer sequence may be appropriate, but must be skippable/reduced-motion compliant.

Never force repetitive reward-box opening.

---

## 20. Navigation principles

- Home is always one action away.
- The player can exit Visit World quickly.
- Core state is not hidden behind deep menu stacks.
- Settings/privacy/permissions are easy to find.
- Back behavior follows platform expectations.
- Loading states never look like frozen input.
- Destructive actions require explicit confirmation.

---

## 21. UX telemetry philosophy

The MVP should be diagnosable without requiring invasive analytics.

Local/test instrumentation can measure:

- time to complete onboarding;
- taps/actions in daily check-in;
- number of blocking decisions;
- return-summary size;
- scene load time;
- permission-flow transitions.

If remote analytics is introduced later, it requires a separate privacy decision and must not include raw health data.

---

## 22. UX acceptance tests

Core scenarios:

1. New player grants permission and starts first project.
2. New player denies permission and can still navigate/explore safely.
3. Player returns after one day of activity.
4. Player returns after seven days away.
5. Multiple projects completed while away.
6. Queue becomes empty while away.
7. Expedition completes while player is absent.
8. Activity source temporarily fails.
9. Permission was revoked externally.
10. Save recovery occurred.
11. Reduced motion enabled.
12. Screen reader navigates Home → Projects → Settings.
13. Visit World fails to load and user returns safely.

---

## 23. UX definition of done

A player-facing feature is not complete until it has:

- normal state;
- loading state;
- empty state where relevant;
- error/retry state;
- offline behavior;
- restart behavior;
- accessibility semantics;
- reduced-motion behavior where relevant;
- touch/input validation;
- concise copy;
- device verification for platform-specific interactions.

---

## 8. M4 presentation requirements (discoveries, expeditions, region completion)

M4 landed the canonical state and data contract; Unity screens remain M5/M6 scope. Runtime work must bind to:

- **Discovery journal** (DiscoveriesReadModel): every authored discovery with unlocked/reviewed flags; titles/bodies/provenance resolve via keys; unread affordance must survive restarts (reviewed is durable); reviewing never gates progression.
- **Expedition routes** (ExpeditionsReadModel): Locked/Available/Completed states with route markers; completion may fire one celebration hook; no claim interaction may be required (routes resolve while the app is closed).
- **Region status**: RegionReadModel now includes ecology/settlement arc stages (0–4), region completion flag/timestamp and collection counts; damaged-vs-restored remains distinguishable without color alone.
- **Return summary**: composer now emits transformation lines for landmark stages, arc advances, expedition completions, region closure, and notice lines for discoveries/route readiness — all inside the existing 12-item bound and priority order.
- **Closure beat**: IsCompleted triggers exactly one celebratory transformation moment; afterwards the world stays evergreen with no artificial reset.

Binding tables per landmark stage are documented in WORLD_AND_CONTENT §23; no scenes, prefabs or assets exist yet, and none are required for headless qualification.
