using System;
using System.Collections.Generic;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Regions;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-216 (Phase D, direction A): maps the River Intake's canonical keys (landmark variant <c>lm.river-intake@&lt;stage&gt;</c>,
    /// gate visual <c>proj.river-intake@ruined|@build-kofN|@done</c>, Tavi's portrait key) to the painted layers in
    /// <c>Resources/Art/Intake</c>, plus the one optional in-world find (the brass intake plate, §48). Pure and
    /// EditMode-testable; reads keys only and never writes game state.
    /// </summary>
    public static class RiverIntakeArt
    {
        public const string LocationId = "lm.river-intake";
        public const string GateId = "proj.river-intake";
        public const string TaviId = "npc.tavi";
        public const string DiscoveryId = "disc.intake-plate-stamp";
        public const string ResourceFolder = "Art/Intake/";
        public const string GroundForgotten = "intake_ground_S0";
        public const string GroundFlourishing = "intake_ground_S4";
        /// <summary>Gate layers: G0 jammed (silt, broken wheel), G2 restored (new boards, wheel, brass plate on the left post).</summary>
        public const string GateJammed = "intake_gate_G0", GateRestored = "intake_gate_G2";
        public const string TaviBody = "tavi_body";
        public const int TaviCanvasW = 484, TaviCanvasH = 1124;
        public static readonly string[] TaviExpressions = { "neutral", "curious", "glad", "proud", "wistful" };

        /// <summary>The brass plate on the restored gate's left post (art space 0..1 of the 720×1280 view) and its tap box.</summary>
        public const float PlateX = 0.590f, PlateY = 0.494f;
        public static readonly (IntakeTarget target, float x0, float y0, float x1, float y1)[] TapRegions =
        {
            (IntakeTarget.Plate, 0.555f, 0.470f, 0.625f, 0.520f),   // ≥ 48 dp at 720×1280 (50 × 64 px)
            (IntakeTarget.Tavi, 0.06f, 0.62f, 0.30f, 0.93f),
            (IntakeTarget.Gate, 0.45f, 0.36f, 1.00f, 0.80f),
        };

        /// <summary>
        /// The optional find is offered only once it is canonically unlocked (the intake project's discovery) and the gate is
        /// restored; it never gates progress. <paramref name="reviewed"/> = the player already read it: the plate stays tappable
        /// but stops glowing.
        /// </summary>
        public static bool PlateTappable(bool discoveryUnlocked, int? gateIndex) => discoveryUnlocked && gateIndex == 1;
        public static bool PlateGlows(bool discoveryUnlocked, int? gateIndex, bool reviewed) => PlateTappable(discoveryUnlocked, gateIndex) && !reviewed;
        /// <summary>Glow ring pulse (alpha); static under reduced motion (still visible, never blinking).</summary>
        public static float GlowAlpha(float t, bool reducedMotion) => reducedMotion ? 0.8f : 0.55f + 0.35f * (0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * t / 2.4f));

        /// <summary>Tavi's idle sway (degrees about the feet, body breath scale); zero with reduced motion.</summary>
        public static (float sway, float breath) IdlePose(float t, bool reducedMotion)
        {
            if (reducedMotion) return (0f, 0f);
            float w = 2f * (float)Math.PI / 3.6f;
            return (0.8f * (float)Math.Sin(w * t), 0.008f * (float)Math.Sin(w * t + 0.5f));
        }

        /// <summary>Gate layer index: 0 jammed for @ruined and every build step, 1 restored for @done; null for foreign/malformed keys.</summary>
        public static int? GateIndexFor(string? gateVisualKey)
        {
            if (string.IsNullOrEmpty(gateVisualKey) || !gateVisualKey!.StartsWith(GateId + "@", StringComparison.Ordinal)) return null;
            string s = gateVisualKey.Substring(GateId.Length + 1);
            if (s == "done") return 1;
            if (s == "ruined" || s.StartsWith("build-", StringComparison.Ordinal)) return 0;
            return null;
        }

        public static string GateLayer(int index) => index >= 1 ? GateRestored : GateJammed;

        public static IntakeTarget HitTest(float ax, float ay, bool taviPresent, bool plateTappable)
        {
            foreach (var r in TapRegions)
            {
                if (r.target == IntakeTarget.Tavi && !taviPresent) continue;
                if (r.target == IntakeTarget.Plate && !plateTappable) continue;
                if (ax >= r.x0 && ax <= r.x1 && ay >= r.y0 && ay <= r.y1) return r.target;
            }
            return IntakeTarget.None;
        }

        public static IReadOnlyDictionary<string, string> CatalogEntries()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [LocationId + "@ruined"] = GroundForgotten,
                [LocationId + "@flourishing"] = GroundFlourishing,
                [LocationId] = GroundForgotten,
                [GateId + "@ruined"] = GateJammed,
                [GateId + "@done"] = GateRestored,
                [GateId] = GateJammed,
                [TaviId] = TaviBody,
                ["portrait." + TaviId] = "portrait_tavi_neutral",
            };
            foreach (var e in TaviExpressions) d["portrait." + TaviId + "@" + e] = "portrait_tavi_" + e;
            return d;
        }

        /// <summary>What the intake view draws; shares the trailhead's stage ramp (<see cref="TrailheadArt.FlourishingShare"/>).</summary>
        public static IntakeFrame Map(LandmarkPresentation landmark, PresentationAssetResolver<string> resolver)
        {
            if (landmark == null) throw new ArgumentNullException(nameof(landmark));
            var stage = TrailheadArt.StageOf(landmark.VariantKey);
            var low = resolver.Resolve(LocationId + "@ruined"); var high = resolver.Resolve(LocationId + "@flourishing");
            string? gateKey = null;
            foreach (var p in landmark.Props) if (p.ProjectId == GateId) { gateKey = p.VisualKey; break; }
            int? idx = GateIndexFor(gateKey);
            string? layer = idx != null ? GateLayer(idx.Value) : gateKey != null ? resolver.Resolve(gateKey).Asset : null;
            return new IntakeFrame(stage ?? RestorationStage.Ruined, low.Asset, high.Asset, stage == null ? 0f : TrailheadArt.FlourishingShare(stage.Value), gateKey, idx, layer);
        }
    }

    public enum IntakeTarget { None = 0, Tavi = 1, Gate = 2, Plate = 3 }

    public sealed class IntakeFrame
    {
        public IntakeFrame(RestorationStage stage, string? low, string? high, float highShare, string? gateKey, int? gateIndex, string? gate)
        { Stage = stage; GroundLow = low; GroundHigh = high; HighShare = highShare; GateKey = gateKey; GateIndex = gateIndex; Gate = gate; }
        public RestorationStage Stage { get; }
        public string? GroundLow { get; }
        public string? GroundHigh { get; }
        public float HighShare { get; }
        public string? GateKey { get; }
        public int? GateIndex { get; }
        public string? Gate { get; }
    }
}
