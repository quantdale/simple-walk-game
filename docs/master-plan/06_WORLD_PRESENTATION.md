# 06 — World Presentation and Optional Visit World

## 1. Strategic decision

Do **not** begin by building a full third-person 3D game. The emotional payoff is the changing world, not locomotion mechanics.

The first world presentation should be a highly polished **2.5D/isometric regional diorama or illustrated interactive map** capable of showing staged restoration efficiently.

The optional 3D Visit World mode remains part of the long-term vision but is downstream of the passive loop.

## 2. Why 2.5D first

Benefits:

- faster development;
- easier deterministic visual states;
- lower mobile GPU/battery cost;
- easier accessibility/reduced-motion support;
- more screen-space available for before/after storytelling;
- simpler screenshots/marketing;
- world changes can be authored as composable layers rather than full environment assets;
- avoids letting engine work delay health/progression validation.

## 3. Rendering candidates

For the 2.5D map, evaluate in a short spike:

1. React Native views/images for simple layered map.
2. `@shopify/react-native-skia` for richer animated map/diorama rendering.
3. SVG only for low-complexity vector overlays.

Recommendation: prefer Skia if the team needs many animated/environmental layers and profiling confirms acceptable integration; otherwise use normal RN/image layers first.

The spike must measure memory, frame pacing, image decode, pan/zoom smoothness, and reduced-motion behavior on mid-tier Android.

## 4. World visual state

Presentation consumes a derived `RegionVisualState`, not raw project tables.

Example:

```ts
interface RegionVisualState {
  regionId: string;
  ecologyStage: number;
  waterStage: number;
  infrastructureStage: number;
  settlementStage: number;
  explorationStage: number;
  flags: string[];
  landmarks: string[];
  ambientEffects: string[];
}
```

This decouples art/rendering implementation from simulation internals.

## 5. Asset layering

A region may be constructed from deterministic layers:

- base terrain;
- water state;
- vegetation;
- ruins/buildings;
- infrastructure/routes;
- wildlife accents;
- weather/atmosphere;
- animated ambient effects;
- project markers;
- fog-of-war/exploration overlay.

Each layer should have stable asset keys and content definitions.

## 6. Transitions

When a project completes, world changes can animate on the next visit:

- polluted water fades/flows clear;
- vegetation grows in staged cross-fade;
- lights return to buildings;
- bridge appears/reconstructs;
- wildlife enters;
- fog recedes;
- route lines connect.

Transitions should be replayable from Journey for key milestones, but the canonical state is already applied before animation.

## 7. Before/after

Provide a milestone compare slider or snapshot system for major region transformations. This is one of the strongest ways to make weeks of physical activity emotionally legible.

Store milestone snapshot metadata, not necessarily full rendered bitmaps; rendering can reconstruct from historical visual state/event sequence.

## 8. Optional Visit World — 3D

Only begin after Milestones 0–6 prove the passive loop.

The 3D mode is for:

- walking around restored regions;
- inspecting landmarks;
- ambient NPC/wildlife observation;
- collecting lore already unlocked by activity;
- optional decorating/photo mode;
- environmental storytelling.

It must not contain mandatory grind/combat/resource collection.

## 9. 3D technology spike

Do not lock a library today without a spike against the then-current Expo/RN ecosystem.

Candidate approach:

- Three.js via React Three Fiber native / Expo-compatible GL stack if maintained and production-suitable;
- alternative native rendering/game framework only if it materially outperforms integration cost.

The spike must answer:

- current Expo SDK compatibility;
- New Architecture compatibility;
- asset pipeline;
- GLTF support;
- shader/material capabilities;
- Android/iOS device stability;
- suspend/resume/context-loss behavior;
- memory lifecycle;
- touch/gamepad input;
- 60 FPS feasibility on target mid-tier devices;
- app binary impact;
- build-system complexity.

Write an ADR after the spike.

## 10. 3D performance budgets

Initial target for optional 3D:

- 60 FPS target on reference mid-tier Android/iPhone in default quality;
- 30 FPS graceful fallback on lower tier;
- dynamic quality presets;
- bounded draw calls and texture memory;
- LOD/culling for region scenes;
- no high-frequency JS allocations in render loop;
- scene resources explicitly disposed;
- no impact on Today startup path: 3D bundles/assets should load lazily.

## 11. Art direction

Use a stylized, readable aesthetic rather than realism. Benefits:

- lower asset burden;
- transformation states can be exaggerated;
- mobile performance;
- visual coherence across many restoration phases;
- avoids competing with AAA expectations.

## 12. Audio

Audio is optional and respectful of context:

- default safe behavior around silent mode/platform conventions;
- ambient sound evolves with restoration;
- short completion cues;
- no audio required to understand state;
- haptics optional and disable-able.
