using System;
using System.Collections.Generic;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Regions;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-204 (Phase D slice, direction A): maps the Old Trailhead's canonical keys (landmark variant
    /// <c>lm.trailhead@&lt;stage&gt;</c>, bridge visual <c>proj.rebuild-trail-bridges@ruined|@build-kofN|@done</c>,
    /// Mira's portrait key) to the painted layers in <c>Resources/Art/Trailhead</c>. Pure: no Unity objects,
    /// so the mapping, its fallbacks and the reduced-motion rule are EditMode-testable. Reads keys only;
    /// never writes game state.
    /// </summary>
    public static class TrailheadArt
    {
        public const string LocationId = "lm.trailhead";
        public const string BridgeId = "proj.rebuild-trail-bridges";
        public const string MiraId = "npc.mira";
        public const string ResourceFolder = "Art/Trailhead/";

        /// <summary>The two registered ground plates (same camera; static drift checked by eye, see ledger).</summary>
        public const string GroundForgotten = "trailhead_ground_S0";
        public const string GroundFlourishing = "trailhead_ground_S4";
        /// <summary>Structure layers (bridge + props) per bridge build state B0..B4.</summary>
        public static readonly string[] StructureLayers =
            { "trailhead_structures_B0", "trailhead_structures_B1", "trailhead_structures_B2", "trailhead_structures_B3", "trailhead_structures_B4" };
        public const string MiraBody = "mira_body";
        public const string MiraPortrait = "portrait_mira_neutral";
        /// <summary>D-206: portrait busts, one per expression, all from the same locked base.</summary>
        public static readonly string[] MiraExpressions = { "neutral", "glad", "worried", "proud", "celebration" };

        /// <summary>D-206: the bridge-specific label key for a dialogue action, or the generic one.</summary>
        public static string LabelKeyFor(DialogueAction action) =>
            action.ProjectId == BridgeId && (action.Kind == DialogueActionKind.Inspect || action.Kind == DialogueActionKind.HelpRestore)
                ? action.LabelKey + ".bridge" : action.LabelKey;

        /// <summary>D-208: Mira cut-out rig parts (pixels in the 576×1091 body canvas, top-left origin; pivot = joint).</summary>
        public static readonly (string name, int x, int y, int w, int h, int px, int py, int order)[] MiraParts =
        {
            ("mira_part_body", 0, 0, 576, 1091, 288, 1091, 0),
            ("mira_part_braid", 298, 260, 83, 229, 338, 266, 1),
            ("mira_part_scarf_tail", 418, 303, 133, 200, 425, 315, 1),
            ("mira_part_head", 79, 0, 434, 271, 300, 262, 2),
        };
        public const int MiraCanvasW = 576, MiraCanvasH = 1091;
        /// <summary>D-208: the ripple strip (pixels in the 720×1280 plate).</summary>
        public const string WaterRipple = "water_ripple";
        public const int RippleX = 0, RippleY = 476, RippleW = 720, RippleH = 350;

        /// <summary>D-220: optional in-world find — the old millstone (catalog disc.old-millstone; unlocked by proj.clear-trailhead).</summary>
        public const string DiscoveryId = "disc.old-millstone";
        public const string MillstoneSprite = "millstone";
        /// <summary>Art-space centre (0..1 of the 720×1280 view) and tap box (≥ 48 dp at 720×1280).</summary>
        public const float MillstoneX = 0.195f, MillstoneY = 0.855f;


        /// <summary>D-208: idle pose at time t (seconds): degrees per part and body breath scale; all zero with reduced motion.</summary>
        public static (float head, float braid, float scarf, float breath) IdlePose(float t, bool reducedMotion)
        {
            if (reducedMotion) return (0f, 0f, 0f, 0f);
            float w = 2f * (float)Math.PI / 3.2f; // 3.2 s breathe loop (style guide §6.4)
            return (1.2f * (float)Math.Sin(w * t), 2.5f * (float)Math.Sin(w * t + 0.8f), 3.5f * (float)Math.Sin(w * t * 1.3f + 1.6f), 0.008f * (float)Math.Sin(w * t));
        }

        /// <summary>D-215: the painted plate behind the text shell for the trailhead's canonical stage (forgotten until it is Functional).</summary>
        public static string BackdropFor(RestorationStage? stage) => stage.HasValue && FlourishingShare(stage.Value) >= 0.5f ? GroundFlourishing : GroundForgotten;

        /// <summary>D-215: world-first boot. Home opens straight into the painted trailhead once per launch, except with a screen
        /// reader on, where the accessible text shell stays the entry point (the world's Home button always returns to it).</summary>
        public static bool OpenWorldFirst(bool atHome, bool screenReaderOn, bool alreadyOpenedThisLaunch) => atHome && !screenReaderOn && !alreadyOpenedThisLaunch;

        /// <summary>D-212: Mira's 2D Animation rig (PSD Importer character prefab, Resources path) and its bone names.</summary>
        public const string MiraRig = "Rig/mira_rig";
        public static readonly string[] MiraBones = { "hips", "spine", "head", "braid_1", "braid_2", "scarf_1", "scarf_2" };
        public const float ReactionSeconds = 1.1f;

        /// <summary>D-212: the "Help restore it" reaction — a glad nod with a small hop and a braid flick, <paramref name="t"/>
        /// seconds after the tap. Reduced motion: no movement (the portrait's glad expression carries it).</summary>
        public static (float head, float hop, float braid) ReactionPose(float t, bool reducedMotion)
        {
            if (reducedMotion || t < 0f || t >= ReactionSeconds) return (0f, 0f, 0f);
            float u = t / ReactionSeconds, env = (float)Math.Sin(Math.PI * u);
            return (-6f * (float)Math.Sin(2 * Math.PI * u * 2f) * env, 0.12f * (float)Math.Max(0, Math.Sin(Math.PI * u * 2f)) * (u < 0.5f ? 1f : 0f), 8f * (float)Math.Sin(2 * Math.PI * u * 1.5f) * env);
        }

        /// <summary>D-208: ripple drift speed multiplier per stage (style guide §1.2); 0 with reduced motion.</summary>
        public static float RippleSpeed(RestorationStage stage, bool reducedMotion) => reducedMotion ? 0f : stage switch
        {
            RestorationStage.Ruined => 0.25f, RestorationStage.Stabilized => 0.5f, RestorationStage.Functional => 1f,
            RestorationStage.Restored => 1.25f, RestorationStage.Flourishing => 1.5f, _ => 0.25f,
        };

        /// <summary>D-208: light motes per stage (≤ 60, PERFORMANCE_BUDGETS §9); none with reduced motion.</summary>
        public static int ParticleCount(RestorationStage stage, bool reducedMotion) => reducedMotion ? 0 : stage switch
        {
            RestorationStage.Stabilized => 8, RestorationStage.Functional => 16, RestorationStage.Restored => 28, RestorationStage.Flourishing => 40, _ => 0,
        };

        /// <summary>Crossfade length for a stage change; 0 (instant swap) with reduced motion.</summary>
        public static float TransitionSeconds(bool reducedMotion) => reducedMotion ? 0f : 0.6f;
        /// <summary>Mira's idle bob amplitude in world units; 0 with reduced motion (style guide §6.4).</summary>
        public static float IdleBobAmplitude(bool reducedMotion) => reducedMotion ? 0f : 0.03f;

        /// <summary>
        /// Share of the flourishing plate laid over the forgotten one, per landmark stage. Each stage also
        /// changes structure (crowns, grass, water) because the two plates differ structurally, not by tint.
        /// </summary>
        public static float FlourishingShare(RestorationStage stage) => stage switch
        {
            RestorationStage.Ruined => 0f,
            RestorationStage.Stabilized => 0.35f,
            RestorationStage.Functional => 0.7f,
            RestorationStage.Restored => 0.88f,
            RestorationStage.Flourishing => 1f,
            _ => 0f,
        };

        /// <summary>Stage from a variant key such as <c>lm.trailhead@functional</c>; null when it is not a known stage.</summary>
        public static RestorationStage? StageOf(string? variantKey)
        {
            if (string.IsNullOrEmpty(variantKey)) return null;
            int at = variantKey!.IndexOf('@');
            if (at < 0) return null;
            return Enum.TryParse<RestorationStage>(variantKey.Substring(at + 1), true, out var s) && Enum.IsDefined(typeof(RestorationStage), s) ? s : (RestorationStage?)null;
        }

        /// <summary>
        /// Structure layer index (0..4) for a bridge visual key: @ruined → 0 (collapsed, debris),
        /// @build-0ofN → 1 (new posts), then planks by progress (2 half span, 3 full span + rails), @done → 4
        /// (garlands, bench). Unknown or malformed keys return null (the caller falls back).
        /// </summary>
        public static int? StructureIndexFor(string? bridgeVisualKey)
        {
            if (string.IsNullOrEmpty(bridgeVisualKey) || !bridgeVisualKey!.StartsWith(BridgeId + "@", StringComparison.Ordinal)) return null;
            string suffix = bridgeVisualKey.Substring(BridgeId.Length + 1);
            if (suffix == "ruined") return 0;
            if (suffix == "done") return 4;
            if (!suffix.StartsWith("build-", StringComparison.Ordinal)) return null;
            int of = suffix.IndexOf("of", 6, StringComparison.Ordinal);
            if (of < 0 || !int.TryParse(suffix.Substring(6, of - 6), out int k) || !int.TryParse(suffix.Substring(of + 2), out int n) || n < 1 || k < 0 || k >= n) return null;
            if (k == 0) return 1;
            return k * 2 < n ? 2 : 3; // first half of the build: half span; later: full span
        }

        // D-207: tappable props. Regions in art space (0..1 of the 720×1280 view, from top-left), checked in priority order.
        public static readonly (TrailheadTarget target, float x0, float y0, float x1, float y1)[] TapRegions =
        {
            (TrailheadTarget.Mira, 0.66f, 0.69f, 0.84f, 0.93f),
            (TrailheadTarget.Millstone, 0.12f, 0.78f, 0.28f, 0.94f), // ≥ 48 dp (≈115×205 px)
            (TrailheadTarget.Signpost, 0.00f, 0.36f, 0.20f, 0.62f),
            (TrailheadTarget.Bridge, 0.28f, 0.28f, 0.70f, 0.70f),
        };

        public static bool MillstoneTappable(bool discoveryUnlocked) => discoveryUnlocked;
        public static bool MillstoneGlows(bool discoveryUnlocked, bool reviewed) => discoveryUnlocked && !reviewed;
        /// <summary>Glow ring pulse (alpha); static under reduced motion (still visible, never blinking).</summary>
        public static float GlowAlpha(float t, bool reducedMotion) => reducedMotion ? 0.8f : 0.55f + 0.35f * (0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * t / 2.4f));

        /// <summary>Screen (0..1 from top-left, aspect w/h) to art space; the camera fits the 9:16 art to the screen height.</summary>
        public static (float x, float y) ScreenToArt(float sx, float sy, float aspect) =>
            (0.5f + (sx - 0.5f) * aspect / (720f / 1280f), sy);

        public static TrailheadTarget HitTest(float ax, float ay, bool miraPresent, bool millstoneTappable = false)
        {
            foreach (var r in TapRegions)
            {
                if (r.target == TrailheadTarget.Mira && !miraPresent) continue;
                if (r.target == TrailheadTarget.Millstone && !millstoneTappable) continue;
                if (ax >= r.x0 && ax <= r.x1 && ay >= r.y0 && ay <= r.y1) return r.target;
            }
            return TrailheadTarget.None;
        }

        /// <summary>Routes a tap through the controller: Mira talks, the signpost opens the place, the bridge opens its inspect card (or the place while it is locked).</summary>
        public static bool Route(TrailheadTarget target, WorldPresentationController controller)
        {
            switch (target)
            {
                case TrailheadTarget.Mira: return controller.TapCharacter(MiraId) == TapOutcome.Opened;
                case TrailheadTarget.Signpost: return controller.TapLandmark(LocationId) == LandmarkTapOutcome.Opened;
                case TrailheadTarget.Bridge:
                    return controller.TapProp(BridgeId) == TapOutcome.Opened || controller.TapLandmark(LocationId) == LandmarkTapOutcome.Opened;
                default: return false;
            }
        }

        /// <summary>The catalog of keys this slice ships, as resource names (asset-key catalog, D-169).</summary>
        public static IReadOnlyDictionary<string, string> CatalogEntries()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [LocationId + "@ruined"] = GroundForgotten,
                [LocationId + "@flourishing"] = GroundFlourishing,
                [LocationId] = GroundForgotten,                 // subject fallback
                [BridgeId + "@ruined"] = StructureLayers[0],
                [BridgeId + "@build-0of3"] = StructureLayers[1],
                [BridgeId + "@build-1of3"] = StructureLayers[2],
                [BridgeId + "@build-2of3"] = StructureLayers[3],
                [BridgeId + "@done"] = StructureLayers[4],
                [BridgeId] = StructureLayers[0],                // subject fallback: collapsed bridge
                [MiraId] = MiraBody,
                ["portrait." + MiraId] = MiraPortrait,          // unknown expressions (e.g. curious) fall back to the neutral bust
            };
            foreach (var e in MiraExpressions) d["portrait." + MiraId + "@" + e] = "portrait_mira_" + e;
            return d;
        }

        /// <summary>
        /// What the trailhead view draws for a landmark presentation. Bridge keys the catalog lacks are
        /// mapped by progress (<see cref="StructureIndexFor"/>) before the resolver's subject/global fallback.
        /// </summary>
        public static TrailheadFrame Map(LandmarkPresentation landmark, PresentationAssetResolver<string> resolver)
        {
            if (landmark == null) throw new ArgumentNullException(nameof(landmark));
            var stage = StageOf(landmark.VariantKey);
            var low = resolver.Resolve(landmark.LocationId + "@ruined");
            var high = resolver.Resolve(landmark.LocationId + "@flourishing");
            string? bridgeKey = null;
            foreach (var p in landmark.Props) if (p.ProjectId == BridgeId) { bridgeKey = p.VisualKey; break; }
            string? structure = null; var resolution = AssetResolution.Missing;
            if (bridgeKey != null)
            {
                var r = resolver.Resolve(bridgeKey);
                int? byProgress = StructureIndexFor(bridgeKey);
                if (r.Resolution != AssetResolution.Exact && byProgress != null) { structure = StructureLayers[byProgress.Value]; resolution = AssetResolution.SubjectFallback; }
                else { structure = r.Asset; resolution = r.Resolution; }
            }
            return new TrailheadFrame(stage ?? RestorationStage.Ruined, stage == null, low.Asset, high.Asset,
                stage == null ? 0f : FlourishingShare(stage.Value), bridgeKey, structure, resolution);
        }
    }

    /// <summary>D-207: what a tap on the trailhead hit.</summary>
    public enum TrailheadTarget { None = 0, Mira = 1, Signpost = 2, Bridge = 3, Millstone = 4 }

    /// <summary>One drawable state of the trailhead (resource names; null = draw nothing for that layer).</summary>
    public sealed class TrailheadFrame
    {
        public TrailheadFrame(RestorationStage stage, bool stageWasUnknown, string? groundLow, string? groundHigh, float highShare,
            string? bridgeKey, string? structure, AssetResolution structureResolution)
        {
            Stage = stage; StageWasUnknown = stageWasUnknown; GroundLow = groundLow; GroundHigh = groundHigh; HighShare = highShare;
            BridgeKey = bridgeKey; Structure = structure; StructureResolution = structureResolution;
        }
        public RestorationStage Stage { get; }
        public bool StageWasUnknown { get; }
        public string? GroundLow { get; }
        public string? GroundHigh { get; }
        public float HighShare { get; }
        public string? BridgeKey { get; }
        public string? Structure { get; }
        public AssetResolution StructureResolution { get; }
    }
}
