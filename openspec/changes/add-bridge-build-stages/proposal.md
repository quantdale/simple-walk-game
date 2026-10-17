# Add bridge build stages and project visuals (KI-027 a, b; D-176)

## Why
The Old Trailhead vertical slice (bible §42.8) needs the trailhead footbridge to read
ruined → in progress → rebuilt and to play one "it is finished" transition. The bridge
changes no landmark stage and had no build stages, so the world view had no key for it and
no event when it completed (Phase A audit §4, KI-027 a and b).

## What changes
- Content v6: `proj.rebuild-trail-bridges` gets `visibleBuildStages: 3` (D-071 rule). No id,
  cost, prerequisite, state or save-schema change; v5 saves load as CompatibleOlder.
- Read models: every location row lists its projects as `ProjectVisual`s with a status, a
  build stage and one visual key (`<project>@ruined`, `<project>@build-kofN`, `<project>@done`).
  Choices carry `ProjectVisualKey` and `DoneVisualKey`.
- Change stream and in-session diff: a new `ProjectCompleted` kind (ToKey `<project>@done`),
  first in each completion batch, emitted once per completion.
- Unity seam: `LandmarkPresentation.Props`, `InspectCard.PropKey` / `DonePropKey`.
