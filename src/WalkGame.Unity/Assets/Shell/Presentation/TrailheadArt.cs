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
