# SIMPLE WALK GAME — Creative Direction Bible & Visual Experience Reconstruction
Cozy Grove–Inspired Art Direction | Version 1.0 | From Michael, Oct 17, 2026, 4:12 PM Asia/Manila
Repository: https://github.com/quantdale/simple-walk-game
Primary artistic reference: Cozy Grove (Spry Fox)
Project identity: an original, cozy, illustrated world-restoration game powered by real-life walking.
Priority: major overhaul of the art direction and the player experience.
Creative objective: turn the current functional restoration simulation into a beautiful, atmospheric, character-rich, interactive indie game.
This document SUPERSEDES the earlier art-checkpoint concepts (A painterly 2D / B iso diorama / C low-poly 3D). It builds on /workspace/briefs/simple-walk-redesign-2026-10-17.md.

## PART I — CREATIVE VISION
### 1. Fundamental direction
I want to change substantially how Simple Walk Game looks and feels. I dislike the current environment assets, the basic geometric illustrations, the generic interface and the text-heavy interactions. The game feels like a set of illustrated status reports or a storyboard, not an actual game.
Lean strongly toward Cozy Grove:
- beautiful hand-illustrated scenery
- whimsical, charming environments
- soft, organic shapes and silhouettes
- expressive, memorable characters
- a gentle, welcoming atmosphere
- textured, storybook-like environment art
- pleasant, cohesive colors
- illustrated buildings, trees, terrain, props and wildlife
- meaningful visual interaction with the environment
- dialogue woven naturally into gameplay
- a world that gradually goes from barren and lifeless to colorful and thriving

It should feel like a cozy indie adventure or life-sim, with real-world activity driving progress. Do NOT clone Cozy Grove. Borrow its broad qualities while creating an original visual identity, characters, setting, world, mechanics and assets. The game must be identifiable as Simple Walk Game.

### 2. Central player fantasy
"My movement in real life brings an abandoned world back to life."
The player starts in a damaged, forgotten valley: abandoned buildings, broken bridges, dry or polluted streams, sparse plants, quiet settlements. Eligible real-life movement generates resources. The player applies them to restoration projects, which make lasting improvements to the world.
Over time:
- dead trees regain foliage
- rivers flow again
- wildflowers grow
- buildings become functional
- animals and birds return
- villagers resume their activities
- infrastructure is restored
- inaccessible places open up
- the settlement grows into a welcoming community

Every meaningful improvement should be visible and emotionally rewarding. The world is the central expression of progress.

### 3. Emotional goals
- **Comfort:** welcome on entering.
- **Curiosity:** details, characters and small mysteries that invite exploring.
- **Accomplishment:** real effort clearly mattered.
- **Attachment:** characters and places become meaningful.
- **Anticipation:** looking forward to seeing what changed after a walk.
- **Calm:** no stress, guilt, urgency or pressure.

Stay pleasant even while the world is damaged. Avoid heavy darkness, horror, bleak realism and oppressive post-apocalyptic visuals. This is a story of recovery and hope.

## PART II — VISUAL IDENTITY
### 4. Style: hand-painted, illustrated 2.5D (leading proposal)
A stylized, hand-painted 2D or 2.5D environment seen from a slightly elevated three-quarter view, like a living illustrated diorama. Hand-drawn or hand-painted-looking assets with soft watercolor or gouache textures. Organic, slightly imperfect forms and carefully controlled dark outlines. Simplified but expressive proportions, layered scenery with real depth, and distinctive architecture and objects. Warm atmospheric lighting, charming exaggeration and small animated details.
Nothing rigid, geometrically sterile or procedurally assembled from generic primitives. Tree crowns can be asymmetric, roofs slightly irregular, and paths can curve naturally. Vegetation can overlap structures, and props can look handcrafted. The goal is intentional irregularity, not low-quality drawing.
Camera: explore a three-quarter, slightly elevated view that lets the player:
- recognize characters
- read landmarks
- appreciate the environment and its spatial relationships
- tap objects accurately
- see changes
- explore without complex controls

An isometric-like or oblique view is promising, but don't commit before prototyping. Choose the most attractive, readable and technically viable view for mobile. Avoid a purely flat side-scroller unless it's clearly better. Avoid expensive full 3D when a 2.5D scene gives better art at lower cost.

### 5. Linework
Linework is part of the identity: a hand-inked look with slight variation in stroke weight and rounded, organic curves. Outlines are warm charcoal, dark brown or muted dark green. Keep internal detail restrained, exaggerate contours selectively, and keep silhouettes readable at phone size.
Avoid:
- uniform vector outlines everywhere
- sterile geometric icons standing in for whole buildings
- lines so thin they vanish on a phone
- harsh black outlines around every object
- uncontrolled texture noise
- mixing photorealistic and hand-painted assets

Keep it coherent across environments, characters, props and the UI.

### 6. Texture and materials
Illustrated, not flat. Use paper-like grain, watercolor pigment variation, gentle brush texture and soft painted highlights. Wood looks rough, foliage organic, and stone and soil are simplified. Texture should add charm without hurting clarity at phone resolution. Don't put a generic paper overlay on everything as a stand-in for real material design. Shapes, composition and consistency come first, with texture in support.

## PART III — COLOR SYSTEM
### 7. Color driven by restoration
A muted world gradually regains vibrancy, using Simple Walk Game's own palette. Transitions are gradual, local and meaningful.
- **Stage 0, Forgotten:** desaturated gray-greens, warm gray-browns, dusty beige, faded olive, hazy light and few accents. Neglected, not fully monochrome.
- **Stage 1, Awakening:** soft grass greens, gentle yellow sunlight, small flowers, slightly clearer water and warm details on repaired structures.
- **Stage 2, Recovery:** richer vegetation, clear blue-green water, warm timber, varied flowers, cleaner paths and more inviting building colors. Unmistakably different from the start.
- **Stage 3, Restored:** more diverse foliage, distinct building materials, colorful decorations, wildlife, warm window light and more ambient movement.
- **Stage 4, Flourishing:** mature vegetation, more environmental diversity, rich but harmonious color, animated ecological details, occupied buildings, community decorations and healthy infrastructure. Lively without being chaotic.

### 8. Starting palette (for exploration, not final)
| Name | Hex | Use |
|---|---|---|
| Warm canvas | #F5EBDD | menus, dialogue backgrounds |
| Soft charcoal | #3D4038 | text, outlines |
| Faded earth | #A89B88 | ruined terrain |
| Dull moss | #858D72 | early restoration |
| Meadow green | #83A878 | restored vegetation |
| Forest green | #527A5B | mature foliage |
| Soft river blue | #83B8C4 | flowing water |
| Honey gold | #E5BA69 | sunlight, highlights |
| Clay orange | #CB866C | buildings, accents |
| Dusty pink | #DDA8AA | flowers, decoration |

Don't rely on color alone. Every stage also needs visible structural or environmental change, so a color-blind player can still tell an area is recovering.

## PART IV — WORLD AND ENVIRONMENT
### 9. Millbrook Valley is the first complete world
Use the existing Millbrook Valley content, systems, landmarks and progression as the foundation, and turn them into one coherent illustrated, connected place, not a set of separate dashboard screens. Landmarks share a recognizable visual identity.
- **Trailhead and paths:** starts with broken signposts, blocked or overgrown trails, damaged wooden bridges, fallen logs, sparse grass and neglected facilities. Restoration brings cleared paths, repaired signs, rebuilt bridges, trees and flowers, restored camps and lookouts, and NPCs or animals using the paths.
- **River and waterworks:** starts with dry or blocked channels, a damaged intake, debris, dead shoreline plants and neglected infrastructure. Restoration brings flowing water, a repaired water wheel or intake, clearer banks, aquatic plants, recovered wetlands and wildlife. Moving water becomes the area's defining feature.
- **Settlement:** starts with abandoned cottages, broken roofs, unused workshops, overgrown gathering spaces, dim windows and empty paths. Restoration brings repaired cottages and workshops, working fireplaces and chimneys, gardens, lanterns, villagers, small work animations and community spaces. The settlement should become emotionally important.
- **Woodland and canopy:** starts with sparse, damaged trees, fallen branches, neglected paths, little wildlife and bare ground. Restoration brings saplings, growing crowns, dense vegetation, birds and woodland creatures, restored canopy walkways and layered forest scenery.
- **Wetlands:** starts with stagnant or dry pools, damaged banks, sparse reeds and ecological decline. Restoration brings healthy pools, reeds and aquatic plants, frogs, birds and insects, clearer water and small animated ecological details.
- **Observatory and research site:** starts with an abandoned tower, damaged equipment, neglected paths and faded signage. Restoration brings repaired structures, working equipment, warm interior light, researchers or caretakers at work, and new interactions and discoveries.

### 10. Environmental density, in three layers
- **Primary:** key characters, interactive buildings and landmarks, recognizable instantly.
- **Secondary:** trees, shrubs, rocks, fences and architectural supports.
- **Ambient:** small flowers, grass, insects, particles, leaves, subtle movement and atmosphere.

Keep a clear hierarchy. Decoration never hides an interaction, and the view stays readable on a small phone.

## PART V — CHARACTERS AND NPCS
### 11. Character art
Characters are a main source of charm: illustrated and stylized, with distinct silhouettes, recognizable outfits and accessories, and expressive faces. Proportions are slightly exaggerated and personalities warm and friendly, with simple but appealing animation and clear separation from the background. They belong to the same world as the scenery, not stickers pasted on top.
Proportions to explore: relatively large heads, compact bodies, simplified limbs, readable eyes and expressions, and exaggerated hair, hats, clothing or accessories. Silhouettes drive the designs.
Avoid generic anime portraits, photorealism, corporate mascots and unrelated asset-pack characters.
Originality: do NOT copy Cozy Grove's ghost bears, the Spirit Scout, its signature character shapes or its supernatural-island setting. Simple Walk Game has its own setting and cast. A promising route is a community of valley residents, such as caretakers, craftspeople, naturalists, explorers and researchers, whose livelihoods return along with the environment. This is a proposal, not a locked cast.

### 12. NPC personalities (candidate cast; can be renamed or redesigned)
- **Mira, the Trailkeeper:** a hopeful local who keeps the paths and bridges. Curious and practical, and sometimes impatient with the broken infrastructure.
- **Rowan, the Gardener:** a gentle caretaker who knows plants, woodland recovery and ecological restoration.
- **Tavi, the Tinkerer:** an energetic mechanic who fixes water systems, workshops and old machines.
- **Eli, the Naturalist:** an observant researcher interested in the valley's wildlife, history and ecological change.

Each gets a recognizable look, an occupation, a speaking style, an emotional arc and a reason to care about restoration. Don't make dozens of shallow characters before a small cast has proven engaging.
(Note from Grok Bot: the art checkpoint used "Nell". Michael's bible proposes Mira and the others; both are open to change. Reconcile this with the existing Millbrook content and record the decision.)

### 13. Expressions and animation
For the first vertical slice, polish ONE NPC with:
- an idle animation
- a talking animation or expression change
- happy and concerned or thoughtful expressions
- a brief celebration reaction
- a visible acknowledgement of restoration progress

Subtle breathing, blinking, swaying, gestures and expression changes are enough. Use animation deliberately.

## PART VI — DIALOGUE
### 14. Dialogue supports gameplay; it doesn't replace it (CRITICAL)
Meaningful dialogue, but NOT a visual novel: no paragraphs of reading and no repeated Next presses. Dialogue happens because the player interacts or because something meaningful changed. It is short, expressive, contextual and character-specific. It's optional when it isn't essential, skippable or revisitable, and relevant to gameplay or the world. One meaningful idea at a time. Longer optional conversations are fine for players who want them.

### 15. Presentation
When the player taps an NPC:
1. The NPC reacts visibly.
2. A dialogue panel or bubble appears.
3. It shows the NPC's name and expressive portrait.
4. A short line appears.
5. The player continues, inspects a related place or picks a meaningful action.
6. The player returns straight to the world.

The world stays visible behind the dialogue; never swap it for repeated full-screen reading panels. Visuals: warm parchment or cream backgrounds, soft rounded illustrated borders, clear portraits and readable type. A few distinct actions, small expressive animations, gentle transitions and enough contrast. The UI looks like part of the illustrated world.

### 16. Example
Mira stands by a damaged bridge, looking toward the unreachable woods.
Mira: "The forest's just across the river. Shame the old bridge gave up before we did."
Options: Inspect the bridge / Help restore it / Maybe later.
"Help restore" opens an illustrated restoration project showing the ruined bridge, what rebuilding will change and the progress needed. The player picks it and goes back to normal life. Walking gradually advances construction. On completion, the next visit shows a repaired bridge, flowing water, nearby plants and a character using the path.
Mira: "Look at that! The forest path is ours again."
The player can inspect the bridge, cross it in optional exploration, or leave. Dialogue introduces gameplay and reacts to it; gameplay is never reduced to dialogue.

### 17. Narrative structure
Tell the story through visible deterioration, NPC memories and motives, restoration milestones and changing character behavior. Also use discoverable objects, recovered history, new locations and a growing community. Avoid exposition. A workshop that becomes active says more than paragraphs saying it was repaired.

## PART VII — UI/UX REBUILD
### 18. Philosophy
The game should feel like opening a game, not a stats dashboard. Priorities, in order:
1. the living world
2. a clear restoration focus
3. an obvious useful interaction
4. optional secondary info

Text, percentages, currency and diagnostics stay accessible but visually secondary. No crowded screens of equally prominent info cards.

### 19. Home
A large illustrated view of the current region is the focus.
- **Top:** compact player status, a subtle activity-connection indicator and one lightweight progress indicator.
- **Center:** the environment, with NPCs, the current restoration target, changed landmarks and selectable objects.
- **Bottom:** one clear main action, minimal navigation and optional access to projects, discoveries and settings.

Home should communicate meaningful change in about 5–15 seconds.

### 20. Region
A spatial, illustrated map of the valley. Selecting a landmark shows:
- its current condition
- what needs repair and what will improve
- whether a project is available
- related characters or discoveries

Show actual environment states, not abstract symbols. Use a lightweight version for normal check-ins and keep deeper exploration separate.

### 21. Restoration projects
Choosing a project should feel like choosing what part of the world to bring back to life. Each project shows:
- an illustration of the target and its current condition
- a preview of the restored state
- a short description
- the progress required
- key unlocks
- a clear queue or start action

Never a plain text task list. Illustrations must convey the real gameplay consequences.

### 22. Return experience
After walking, open with a visually satisfying summary of what changed in the world:
1. The camera shows the valley.
2. A completed landmark is gently highlighted.
3. A short restoration transition plays.
4. A character reacts if it fits.
5. A concise summary shows what the walk contributed.
6. One useful next action is offered.

Never force long celebrations. Major animations are skippable, respect reduced motion, and don't replay on later views unless deliberately replayed. Never replay a finished restoration as if it just happened.

### 23. Typography and UI assets
A friendly, slightly expressive display font for titles and a highly legible body font, with a consistent hierarchy, generous spacing and short labels. Use clear illustrated icons and avoid ornamental type in long dialogue. UI assets share the world's visual language: organic borders, warm colors, subtle illustration and well-styled buttons. Generic line icons and corporate dashboard components are not the default identity.

## PART VIII — ANIMATION AND AUDIO
### 24. Environmental animation
Candidates:
- swaying grass and moving leaves
- flowing river water
- moving flowers
- birds flying or hopping
- NPC idles
- smoke from working chimneys
- warm window lights
- small ambient particles
- occasional construction activity

The world shouldn't feel frozen, but keep motion calm, efficient and readable. Don't flood the scene with constant motion.

### 25. Restoration animations
- **Tree:** shoots appear, branches leaf out and grass returns.
- **Bridge:** debris clears, supports rise, and the bridge becomes usable.
- **River:** water reappears and moves through the channel.
- **Settlement:** windows light up, structures repair, decorations appear and residents return.

Keep sequences short and appealing. Restoration state is saved independently of the animation, and skipping an animation must never skip progress.

### 26. Sound
Soft instrumental music and natural ambience: birdsong, flowing water and rustling leaves. Small, pleasant interaction sounds, gentle completion cues and distinct but unobtrusive NPC cues. Avoid loud arcade reward sounds, aggressive transitions and repetitive notifications. Sound changes with the environment's condition where it fits. The game must still make sense when muted.

## PART IX — GAMEPLAY INTEGRATION
### 27. Real-world walking stays central
Core loop: walk in real life, earn validated progress, restore the world, come back to observe and interact, choose future goals, keep walking. Optional in-game interactions add enjoyment but never replace physical activity as the main source of progress. No mandatory foreground grinding.

### 28. Passive and active play
- **Passive:** walk normally, open the game now and then, see the changes, read a concise status if wanted, and leave. Progress continues through the existing automation.
- **Active, optional:** explore, talk to NPCs, inspect landmarks, review discoveries, customize some areas, choose priorities and enjoy the world.

Neither style invalidates the other. Active players can get extra narrative and cosmetic depth, but no unfair core-progress advantage just for tapping more.

### 29. Meaningful interaction
Interactions are visually grounded. The player can:
- tap an NPC to talk
- tap a damaged bridge to inspect it
- tap a restoration marker to see the project
- tap a restored structure to see its function
- tap a discovery to view illustrated lore
- visit a landmark to see its current state

Avoid interactions whose only result is another paragraph. Let decisions produce visible effects wherever possible.

## PART X — ASSET PRODUCTION AND ENGINEERING
### 30. Replace the prototype art with a coherent pipeline
Stop polishing the geometric illustration system; it's temporary scaffolding. Build a proper asset workflow using original assets or licenses that meet the repo's documented requirements. Do NOT import Cozy Grove images, sprites, character art, UI or extracted files. Do NOT trace or closely reproduce recognizable copyrighted art. Create original characters, landmarks, backgrounds and props.

### 31. Technical approach
Favor an efficient illustrated 2D/2.5D presentation inside the existing Unity project. Evaluate:
- SpriteRenderer layered environments
- sorting layers and stable depth ordering
- sprite atlases
- modular environment assets
- lightweight character animation
- restoration-stage variants driven by assets
- region-aware asset loading
- carefully scoped effects
- data-driven dialogue
- UI Toolkit, or another justified choice, for the interface layers

Use Unity's built-in capabilities, with no second engine and no needless rewrite of the architecture. The canonical world state stays authoritative; the presentation reads it and renders the matching world.

### 32. Restoration asset structure
For each landmark, define:
- base art
- ruined-state assets
- intermediate variants
- restored assets
- optional flourishing details
- interactive object positions
- character placements
- ambient animations
- soundscape changes

Prefer a modular scene with state-driven variants over separate scenes per stage when that's easier to maintain. But don't over-optimize reuse until every landmark looks generic; quality and distinctiveness matter.

### 33. Minimum vertical-slice assets
Start with one focused location, not the whole region.
- **Environment:** one coherent illustrated location with foreground, midground and background layers. Ground and path treatment, several tree and plant variants, and rocks, fences and small props. One significant restoration landmark in ruined, partly restored and restored states.
- **Character:** one original NPC with an in-world asset, three or more portrait expressions, idle and interaction animations, and contextual dialogue.
- **Interface:** a redesigned Home composition, a dialogue component, a restoration-project component, a compact progress display and navigation that suits the illustrated experience.
- **Feedback:** one finished restoration transition, environmental animation, simple ambient audio and a meaningful visual response to interaction.

Deliver these as working game elements, not just disconnected concept art.

## PART XI — VISUAL REFERENCES
### 34. Primary: Cozy Grove
For hand-drawn environments, whimsical forms, a warm cozy atmosphere and organic silhouettes. Also for charming characters, rich props, a world gradually gaining color, and comfort and attachment. Don't copy its characters, supernatural-island premise, UI, exact layouts or gameplay restrictions.

### 35. Supporting references
- **Spiritfarer:** expressive characters, emotionally engaging interactions and polished illustrated animation.
- **Wytchwood:** distinctive hand-drawn shapes, fantastical details and stylized composition.
- **Stardew Valley:** how NPCs, places and lasting world changes support real gameplay rather than passive reading. Don't adopt pixel art just because it uses it.
- **Alba: A Wildlife Adventure:** optimistic environmental themes, approachable exploration and friendly interactions.
- **Terra Nil:** clear, dramatic environmental restoration. Don't copy its industrial gameplay or look.

Cozy Grove stays the main aesthetic anchor.

### 36. Originality
Make original:
- character silhouettes and NPC personalities
- architecture
- vegetation motifs and props
- decorative symbols
- dialogue presentation and UI layout
- color accents
- restoration animations
- settlement identity

The goal: "It has the charm of Cozy Grove," without anyone mistaking it for a Cozy Grove product.

## PART XII — WHAT TO AVOID
### 37. Rejected patterns (never the final design)
- text-heavy dashboards
- primitive shapes passed off as finished art
- symbolic rectangles standing in for buildings
- lifeless backgrounds under text overlays
- repeated Continue presses as the main mechanic
- storyboard sequences with no interaction
- static portraits disconnected from the world
- generic AI illustrations in inconsistent styles
- mismatched asset packs
- corporate interfaces
- lots of mandatory dialogue
- full-screen visual-novel scenes as the main activity
- empty 3D worlds full of placeholders
- overly complex management systems
- mandatory manual reward claiming
- forced daily play or penalties for inactivity

### 38. Superficial fixes that are NOT enough
- recoloring the basic shapes
- adding a paper texture to a generic UI
- rounding text cards
- adding more status lines
- decorative illustrations with no function
- a few NPC portraits on an experience that's still text-driven
- one nice concept image while the actual Unity build stays crude

The overhaul has to change the real game experience.

## PART XIII — EXECUTION PLAN
### 39. Phase A, full audit
Review AGENTS.md, docs/MASTER_PLAN.md, PRODUCT_SPEC.md, WORLD_AND_CONTENT.md, UX_DESIGN.md, GAME_SYSTEMS.md and ROADMAP.md. Also review the Unity Shell and Region rendering code, the asset registers, the current editor screenshots, and the build and test infrastructure. Identify what to replace, reuse or extend in the presentation. Don't destroy working progression, persistence or activity processing.

### 40. Phase B, three art-direction concepts (related but meaningfully different, all original)
- **Concept A, Painted Storybook Valley** (preferred starting point): painterly 2.5D, whimsical scenery, warm textures, cozy architecture and expressive characters. Closest to Cozy Grove's broad philosophy.
- **Concept B, Layered Paper-Craft Wonderland:** illustrated paper-like layers, gentle depth, crafted textures, playful shapes and soft movement. More deliberately handcrafted and graphic.
- **Concept C, Painterly Miniature Diorama:** a compact illustrated world with a bit more spatial depth, clear landmarks, delicate lighting and optional camera movement.

All three are cozy, original and character-focused. For EACH concept, produce:
1. a gameplay screen
2. a damaged-versus-restored comparison
3. an NPC illustration
4. a dialogue interaction
5. a compact UI treatment
6. a technical-feasibility and asset-effort estimate

Then stop at ONE creative approval checkpoint before committing the pipeline. Deliver actual visual mockups, not just written descriptions.

### 41. Phase C, visual style guide (after approval)
The canonical art-direction guide covers:
- camera
- environment and character proportions
- outlines
- texture language
- palette
- lighting
- typography
- UI and dialogue components
- animation conventions
- asset dimensions and naming
- render and export procedures
- licensing
- accessibility
- quality standards

Plus a reusable asset-production template.

### 42. Phase D, playable vertical slice
Suggested target: the Old Trailhead and the nearby damaged bridge. The player can:
1. open the actual Unity app
2. see a coherent illustrated damaged location
3. see an NPC physically present in it
4. tap the NPC and have a short dialogue
5. inspect the damaged landmark
6. choose a restoration project
7. apply eligible activity through the real progression system, or a properly isolated dev fixture
8. see intermediate progress
9. complete the restoration
10. see a materially transformed environment
11. get a contextual NPC acknowledgement
12. keep exploring or leave without losing progress

It survives app restarts and stays consistent with the canonical state.

### 43. Phase E, region expansion
Only after the slice passes its visual and functional criteria: extend the art language to other Millbrook landmarks and add NPCs where they genuinely help. Expand the environmental animation, improve transitions and feedback, and add richer optional exploration. Integrate discoveries and expeditions with the world, improve responsive mobile layouts and polish consistency. No second region until the first region's presentation is coherent.

### 44. Phase F, verification
- **Engineering:** progression stays deterministic and activity is credited exactly once. Save, load and recovery are correct, with no duplicate rewards and no restoration regressions. The game works reliably offline, the repo gates pass, and performance stays within the documented budgets.
- **Experience:** the art is coherent and NPCs and landmarks are recognizable. Key objects are readable on a phone and text at the supported accessibility sizes. Dialogue doesn't get in the way, transformations are obvious, and before and after differ substantially. It feels like a game, not a storyboard.

Never claim device verification from editor tests or APK builds alone.

## PART XIV — QUALITY GATES
### 45. Art approval gate
Three original style explorations, one recommended direction with rationale, and one coherent NPC design per concept. A before-and-after restoration comparison, a gameplay-and-dialogue mockup and an asset-production feasibility assessment. No large-scale art production before this checkpoint.

### 46. Vertical-slice acceptance gate
- It runs in Unity.
- It uses real authored illustrated environment assets.
- A character appears and interacts naturally.
- Dialogue is short, expressive and context-aware.
- A real restoration project changes the environment.
- Ruined and restored states are visibly distinct.
- The UI matches the art identity.
- The world dominates the screen.
- It doesn't look like a dashboard or a visual novel.
- Core state correctness is preserved.

### 47. Visual evidence gate
Capture the first launch or onboarding, the initial damaged world, an NPC interaction and the dialogue presentation. Also project selection, the intermediate and completed restoration states, the return-after-walking experience, Home and the Region view. Include actual Unity captures, plus a short gameplay recording or animation capture where feasible. Clearly label concept art, editor captures and device-tested results. Mockups don't prove the gameplay works.

### 48. Final evaluation questions
- Would someone recognize it as a cozy illustrated indie game?
- Does the world have charm and personality?
- Are the characters memorable and expressive?
- Can the player interact with the world naturally?
- Does real-life walking produce meaningful visible change?
- Can someone follow progress without reading paragraphs?
- Is there worthwhile optional gameplay beyond reading dialogue?
- Can the player leave without being penalized?

If any central answer is no, fix it before expanding secondary content.

## PART XV — AUTONOMY AND RULES
### 49. Autonomy
You're authorized to research, design, prototype, implement, test and improve autonomously within the repo's governance and technical limits. Make ordinary engineering decisions independently, without repeated approval requests for minor choices. BUT because earlier autonomous design choices produced an art direction Michael strongly dislikes, the initial artistic identity needs his explicit approval before large-scale asset production. After that, proceed autonomously.

### 50. Preserve the engineering investments
Keep the offline-first operation, the deterministic canonical world state and exactly-once activity processing. Keep the durable saves, safe activity reconciliation, automated progression and meaningful restoration content. Keep the privacy protections, low-attention gameplay and reproducible testing. Respect repo identity and writer leases. Don't conflate quantdale/simple-walk-game with quantdale/walk-game. A visual overhaul is not permission for unreviewed save-schema changes, broad feature rewrites or unsafe repo operations.

### 51. Reporting
Report through completed player-facing outcomes:
- what actually changed in the game
- which assets were added or improved
- what the player can now see or do
- how walking-driven progress shows up in the new presentation
- which real screenshots or demos verify it
- what technical and device qualification is still unverified

Commit counts and test totals are not a substitute.

## FINAL DIRECTIVE
Make Simple Walk Game a beautiful, cozy, illustrated world-restoration game inspired by Cozy Grove's warmth and charm. It needs expressive characters, charming environments, meaningful conversations, subtle animation, recognizable places, and a world that comes alive through real walking. Not the old primitive visuals polished, not a text dashboard, and not a visual novel: a real game that players interact with, get attached to and restore. Cozy Grove is the leading artistic reference, but the game stays original. Walking is the progression engine, the illustrated world is the reward, and characters and interactions bring it to life. Start with the audit, then the three concepts and mockups for approval. Then a polished Unity vertical slice, and expand systematically. Put artistic cohesion, meaningful transformation, satisfying interaction and real enjoyment ahead of feature counts and text polish.
