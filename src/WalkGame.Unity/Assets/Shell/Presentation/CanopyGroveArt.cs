using System;
using System.Collections.Generic;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Regions;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-222 (Phase D, direction A): maps Canopy Grove's canonical keys (landmark <c>lm.canopy@&lt;stage&gt;</c>,
    /// walkway visual <c>proj.canopy-walkway@ruined|@build-kofN|@done</c>, Rowan's portrait) to the painted layers in
    /// <c>Resources/Art/Canopy</c>, plus the optional understory-orchids find. Pure and EditMode-testable.
    /// </summary>
    public static class CanopyGroveArt
    {
        public const string LocationId = "lm.canopy";
        public const string WalkwayId = "proj.canopy-walkway";
        public const string ClearTimberId = "proj.clear-fallen-timber";
        public const string RowanId = "npc.rowan";
        public const string DiscoveryId = "disc.understory-orchids";
        public const string ResourceFolder = "Art/Canopy/";
        public const string GroundForgotten = "canopy_ground_S0";
        public const string GroundFlourishing = "canopy_ground_S4";
        public const string TimberFallen = "canopy_timber_T0";
        public const string WalkwayRaised = "canopy_walkway_W3";
        public const string OrchidsSprite = "orchids";
        /// <summary>D-223: humble understory scale in world units (fraction of the 7.2×12.8 view).</summary>
        public const float OrchidWorldScale = 0.28f;
        public const string RowanBody = "rowan_body";
        public const int RowanCanvasW = 436, RowanCanvasH = 1068;
        /// <summary>Dialogue expressions map to the locked neutral bust until dedicated plates ship.</summary>
        public static readonly string[] RowanExpressions = { "neutral", "wistful", "glad", "proud", "curious" };
        public static readonly (string name, int x, int y, int w, int h, int px, int py, int order)[] RowanParts =
        {
            ("rowan_part_body", 0, 0, 436, 1068, 218, 1068, 0),
            ("rowan_part_head", 94, 0, 306, 408, 235, 320, 1),
        };
        public const string RowanRig = "Rig/rowan_rig";
        public static readonly string[] RowanBones = { "hips", "spine", "head" };

        public const float OrchidsX = 0.72f, OrchidsY = 0.78f;
        public static readonly (CanopyTarget target, float x0, float y0, float x1, float y1)[] TapRegions =
        {
            (CanopyTarget.Orchids, 0.65f, 0.72f, 0.82f, 0.86f),
            (CanopyTarget.Rowan, 0.10f, 0.58f, 0.34f, 0.93f),
            (CanopyTarget.Walkway, 0.08f, 0.36f, 0.52f, 0.78f),
            (CanopyTarget.Timber, 0.20f, 0.48f, 0.80f, 0.82f),
        };

        /// <summary>Fallen timber clears when <c>proj.clear-fallen-timber</c> completes (landmark may stay Ruined until WetlandDrainage).</summary>
        public static bool ShowTimber(RestorationStage stage, bool clearFallenDone) => stage == RestorationStage.Ruined && !clearFallenDone;
        public static bool ShowWalkway(int? walkwayIndex) => walkwayIndex is >= 1;
        public static bool OrchidsTappable(bool discoveryUnlocked, RestorationStage stage) =>
            discoveryUnlocked && stage >= RestorationStage.Functional;
        public static bool OrchidsGlow(bool discoveryUnlocked, RestorationStage stage, bool reviewed) =>
            OrchidsTappable(discoveryUnlocked, stage) && !reviewed;
        public static float GlowAlpha(float t, bool reducedMotion) =>
            reducedMotion ? 0.8f : 0.55f + 0.35f * (0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * t / 2.4f));

        public static (float head, float breath) IdlePose(float t, bool reducedMotion)
        {
            if (reducedMotion) return (0f, 0f);
            float w = 2f * (float)Math.PI / 3.4f;
            return (1.0f * (float)Math.Sin(w * t), 0.007f * (float)Math.Sin(w * t));
        }

        /// <summary>0 = none/ruined, 1 = building, 2 = done; null for foreign keys.</summary>
        public static int? WalkwayIndexFor(string? walkwayVisualKey)
        {
            if (string.IsNullOrEmpty(walkwayVisualKey) || !walkwayVisualKey!.StartsWith(WalkwayId + "@", StringComparison.Ordinal)) return null;
            string s = walkwayVisualKey.Substring(WalkwayId.Length + 1);
            if (s == "done") return 2;
            if (s == "ruined") return 0;
            if (s.StartsWith("build-", StringComparison.Ordinal)) return 1;
            return null;
        }

        public static CanopyTarget HitTest(float ax, float ay, bool rowanPresent, bool orchidsTappable, bool timberVisible, bool walkwayVisible)
        {
            foreach (var r in TapRegions)
            {
                if (r.target == CanopyTarget.Rowan && !rowanPresent) continue;
                if (r.target == CanopyTarget.Orchids && !orchidsTappable) continue;
                if (r.target == CanopyTarget.Timber && !timberVisible) continue;
                if (r.target == CanopyTarget.Walkway && !walkwayVisible) continue;
                if (ax >= r.x0 && ax <= r.x1 && ay >= r.y0 && ay <= r.y1) return r.target;
            }
            return CanopyTarget.None;
        }

        public static IReadOnlyDictionary<string, string> CatalogEntries()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [LocationId + "@ruined"] = GroundForgotten,
                [LocationId + "@flourishing"] = GroundFlourishing,
                [LocationId] = GroundForgotten,
                [LocationId + "@timber"] = TimberFallen,
                [WalkwayId + "@ruined"] = TimberFallen,
                [WalkwayId + "@build-0of5"] = WalkwayRaised,
                [WalkwayId + "@build-1of5"] = WalkwayRaised,
                [WalkwayId + "@build-2of5"] = WalkwayRaised,
                [WalkwayId + "@build-3of5"] = WalkwayRaised,
                [WalkwayId + "@build-4of5"] = WalkwayRaised,
                [WalkwayId + "@done"] = WalkwayRaised,
                [WalkwayId] = WalkwayRaised,
                [DiscoveryId] = OrchidsSprite,
                [RowanId] = RowanBody,
                ["portrait." + RowanId] = "portrait_rowan_neutral",
            };
            foreach (var e in RowanExpressions) d["portrait." + RowanId + "@" + e] = "portrait_rowan_neutral";
            return d;
        }

        public static CanopyFrame Map(LandmarkPresentation landmark, PresentationAssetResolver<string> resolver)
        {
            if (landmark == null) throw new ArgumentNullException(nameof(landmark));
            var stage = TrailheadArt.StageOf(landmark.VariantKey);
            var low = resolver.Resolve(LocationId + "@ruined"); var high = resolver.Resolve(LocationId + "@flourishing");
            string? walkKey = null; bool clearDone = false;
            foreach (var p in landmark.Props)
            {
                if (p.ProjectId == WalkwayId) walkKey = p.VisualKey;
                if (p.ProjectId == ClearTimberId && p.Status == WalkGame.Domain.Projects.ProjectStatus.Completed) clearDone = true;
            }
            int? idx = WalkwayIndexFor(walkKey);
            var st = stage ?? RestorationStage.Ruined;
            string? timber = ShowTimber(st, clearDone) ? TimberFallen : null;
            string? walk = ShowWalkway(idx) ? WalkwayRaised : null;
            return new CanopyFrame(st, low.Asset, high.Asset, stage == null ? 0f : TrailheadArt.FlourishingShare(stage.Value), walkKey, idx, timber, walk);
        }
    }

    public enum CanopyTarget { None = 0, Rowan = 1, Walkway = 2, Timber = 3, Orchids = 4 }

    public sealed class CanopyFrame
    {
        public CanopyFrame(RestorationStage stage, string? low, string? high, float highShare, string? walkKey, int? walkIndex, string? timber, string? walkway)
        { Stage = stage; GroundLow = low; GroundHigh = high; HighShare = highShare; WalkwayKey = walkKey; WalkwayIndex = walkIndex; Timber = timber; Walkway = walkway; }
        public RestorationStage Stage { get; }
        public string? GroundLow { get; }
        public string? GroundHigh { get; }
        public float HighShare { get; }
        public string? WalkwayKey { get; }
        public int? WalkwayIndex { get; }
        public string? Timber { get; }
        public string? Walkway { get; }
    }
}
