# Reword title-led copy so imperative project titles read well (D-162)

**Status:** Implemented by the executing agent (D-162, PR #162), under the design authority Michael granted for this campaign. Display copy only; Region 1 scope.

## Why

Every project title is imperative ("Complete the valley survey"). Sentences that put a title in subject position read badly, e.g. "Complete the valley survey is complete." and "Clear the old trailhead is underway (3 of 300).".

## What Changes

- Stored summary lines are unchanged. `SummaryLineText.Display` renders them for display in every locale: per-project completion sentences (`Region1SummaryCopy`, overlay `<id>#completed`), "Ready to queue: {title}.", "A new expedition is open: {title}." and "Expedition walked: {title}[ (+N Resource)].". A locale's own `summary.*` pattern still wins.
- Glance restoring-now: "Now restoring: {title} ({invested} of {cost})." and "Next up: {title}." (was "{title} is underway (…)." and "Next up is {title}."). This updates the wording quoted in `define-headless-glance-language/design.md` and `add-first-session-payoff`. The requirements are unchanged: a display title, never an id.
- Shell: "Move up: {title}", "Getting there: {title} (opens after: …).".
- No save, schema, content or fingerprint change.
