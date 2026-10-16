using System;
using System.Collections.Generic;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Regions;

namespace WalkGame.Shell
{
    /// <summary>Silhouette family for a landmark (M6b readability): each reads as a distinct thing, not a bar.</summary>
    public enum LandmarkKind { Generic, Gate, Waterworks, Settlement, Grove, Wetland, Observatory }

    /// <summary>One landmark as drawn in the valley picture.</summary>
    public sealed class LandmarkVisual
    {
        public LandmarkVisual(string label, RestorationStage stage, float x, float y, bool changed = false, LandmarkKind kind = LandmarkKind.Generic)
        { Label = label; Stage = stage; X = x; Y = y; Changed = changed; Kind = kind; }
        public LandmarkKind Kind { get; }
        /// <summary>Restoration moment: this landmark changed while the player was away (UX_DESIGN §4/§19).</summary>
        public bool Changed { get; }
        public string Label { get; }
        public RestorationStage Stage { get; }
        /// <summary>Normalized position in the picture, 0..1.</summary>
        public float X { get; }
        public float Y { get; }
        /// <summary>0 (ruined) .. 4 (flourishing): drives size, shape count and colour.</summary>
        public int Level => (int)Stage;
    }

    /// <summary>D-071 build site: the active long project rising in authored stages.</summary>
    public sealed class BuildSiteVisual
    {
        public BuildSiteVisual(string label, int stage, int stages) { Label = label; Stage = stage; Stages = stages; }
        public string Label { get; }
        /// <summary>0-based stage reached (shown as Stage+1 posts).</summary>
        public int Stage { get; }
        public int Stages { get; }
    }

    /// <summary>
    /// Engine-free description of the Region picture (M6 restoration visuals, created assets:
    /// procedural shapes only, no textures; docs/ASSET_LICENSES.md). Built only from
    /// <see cref="RegionReadModel"/>; rendered by <see cref="ValleyElement"/>. Static: no motion,
    /// so reduced motion is met by construction. Performance budget: at most
    /// <see cref="MaxShapes"/> filled shapes per repaint, independent of save size.
    /// </summary>
    public sealed class RegionScene
    {
        public const int MaxLandmarks = 8;        // Region 1 authors 6
        public const int ShapesPerLandmark = 9;   // body + top + detail (rubble or window) + up to 4 growth + marker + moment ring
        public const int BackdropShapes = 5;      // sky, sun, far hills, near hills, river
        /// <summary>Build site (D-071): ground slab + one post per authored stage reached.</summary>
        public const int BuildSiteShapes = 1 + WalkGame.Domain.Projects.ProjectDefinition.MaxVisibleBuildStages;
        /// <summary>D-101 restoration ambience: footpath links between recovered neighbours, meadow flowers, birds.</summary>
        public const int MaxTrailSegments = MaxLandmarks - 1;
        public const int MaxFlowers = 5;
        public const int MaxBirds = 2;
        public const int MaxAmbientShapes = MaxTrailSegments + MaxFlowers + MaxBirds;
        public const int MaxShapes = BackdropShapes + MaxLandmarks * ShapesPerLandmark + BuildSiteShapes + MaxAmbientShapes;

        /// <summary>Active project's visible build progress, or null when it authors no stages.</summary>
        public BuildSiteVisual? BuildSite { get; }

        public IReadOnlyList<LandmarkVisual> Landmarks { get; }
        /// <summary>0..1 share of restorations finished; tints the hills from dry to green.</summary>
        public float Greenness { get; }
        public bool Completed { get; }
        /// <summary>Text alternative for the picture (UX_DESIGN accessibility: never picture-only).</summary>
        public string AltText { get; }

        /// <summary>Footpath segments: one per adjacent pair of landmarks that are both past Ruined.</summary>
        public int TrailSegments { get; }
        /// <summary>Meadow flowers, 0..<see cref="MaxFlowers"/>, growing with restorations finished.</summary>
        public int Flowers { get; }
        /// <summary>Birds over the valley once it is at least half green (0..<see cref="MaxBirds"/>).</summary>
        public int Birds { get; }

        private RegionScene(IReadOnlyList<LandmarkVisual> landmarks, float greenness, bool completed, string alt, BuildSiteVisual? site)
        {
            BuildSite = site; Landmarks = landmarks; Greenness = greenness; Completed = completed; AltText = alt;
            (TrailSegments, Flowers, Birds) = Ambient(landmarks, greenness);
        }

        /// <summary>
        /// D-101: static, bounded ambience derived only from landmark stages and greenness, so the
        /// picture visibly fills in as restoration proceeds without any per-frame work.
        /// </summary>
        public static (int Trail, int Flowers, int Birds) Ambient(IReadOnlyList<LandmarkVisual> landmarks, float greenness)
        {
            int trail = 0;
            for (int i = 1; i < landmarks.Count && trail < MaxTrailSegments; i++)
                if (landmarks[i - 1].Stage > RestorationStage.Ruined && landmarks[i].Stage > RestorationStage.Ruined) trail++;
            float g = Clamp01(greenness);
            int flowers = Math.Min(MaxFlowers, (int)Math.Floor(g * MaxFlowers + 0.0001f));
            if (flowers == 0 && g > 0f) flowers = 1;  // the first finished restoration already shows a bloom
            int birds = g < 0.5f ? 0 : g < 1f ? 1 : MaxBirds;
            return (trail, flowers, birds);
        }

        public static RegionScene Build(RegionReadModel region) => Build(region, null);

        /// <summary>
        /// <paramref name="changedTexts"/>: current Transformation lines of the pending return
        /// summary. A landmark whose player title appears in one is marked as a restoration moment.
        /// Matching is by title because summary items carry text only (documented limitation).
        /// </summary>
        public static RegionScene Build(RegionReadModel region, IReadOnlyList<string>? changedTexts)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            var list = new List<LandmarkVisual>();
            int n = Math.Min(region.Landmarks.Count, MaxLandmarks);
            for (int i = 0; i < n; i++)
            {
                var l = region.Landmarks[i];
                // Deterministic layout: spread along the valley floor, alternating near/far rows.
                float x = (i + 0.5f) / n;
                float y = 0.62f + (i % 2 == 0 ? 0.08f : -0.04f);
                string title = PlayerCopy.Title(l.TitleKey);
                bool changed = false;
                if (changedTexts != null && title != GlancePhrases.UntitledProject)
                    foreach (var t in changedTexts)
                        if (t != null && t.IndexOf(title, StringComparison.Ordinal) >= 0) { changed = true; break; }
                list.Add(new LandmarkVisual(title, l.Stage, x, y, changed, KindOf(l.LandmarkId)));
            }
            float green = region.TotalProjects <= 0 ? 0f : Clamp01((float)region.CompletedProjects / region.TotalProjects);
            if (region.RegionCompleted) green = 1f;
            int restored = 0;
            foreach (var v in list) if (v.Stage >= RestorationStage.Restored) restored++;
            string alt = region.RegionCompleted
                ? PlayerStrings.T("scene.alt.complete")
                : PlayerStrings.F("scene.alt.progress", restored, list.Count,
                    PlayerStrings.T(green < 0.34f ? "scene.alt.hills.dry" : green < 0.67f ? "scene.alt.hills.greening" : "scene.alt.hills.green"));
            var amb = Ambient(list, green);
            if (amb.Trail > 0) alt += amb.Trail == 1 ? PlayerStrings.T("scene.alt.trail.one") : PlayerStrings.F("scene.alt.trail.many", amb.Trail + 1);
            if (amb.Birds > 0) alt += PlayerStrings.T("scene.alt.birds");
            int moments = 0;
            foreach (var v in list) if (v.Changed) moments++;
            if (moments > 0)
                alt += moments == 1 ? PlayerStrings.T("scene.alt.moments.one")
                                    : PlayerStrings.F("scene.alt.moments.many", moments);
            BuildSiteVisual? site = null;
            if (!region.RegionCompleted && region.ActiveProjectId != null && region.ActiveBuildStages > 1)
            {
                int stages = Math.Min(region.ActiveBuildStages, WalkGame.Domain.Projects.ProjectDefinition.MaxVisibleBuildStages);
                int stage = Math.Max(0, Math.Min(region.ActiveBuildStage, stages - 1));
                string t = PlayerCopy.Title(region.ActiveProjectTitleKey ?? string.Empty);
                site = new BuildSiteVisual(t, stage, stages);
                alt += PlayerStrings.F("scene.alt.build", t, stage + 1, stages);
            }
            return new RegionScene(list, green, region.RegionCompleted, alt, site);
        }

        /// <summary>Total filled shapes a repaint will issue (budget check).</summary>
        public int ChangedCount { get { int n = 0; foreach (var l in Landmarks) if (l.Changed) n++; return n; } }

        public int ShapeCount()
        {
            int shapes = BackdropShapes;
            foreach (var l in Landmarks) shapes += 4 + l.Level + (l.Changed ? 1 : 0); // body + top + detail + marker + growth + ring
            if (BuildSite != null) shapes += 1 + BuildSite.Stage + 1;                  // slab + posts (stage 0 shows one post)
            return shapes + TrailSegments + Flowers + Birds;                            // D-101 ambience
        }

        /// <summary>Calm palette, dry ochre → living green. Returned as 0xRRGGBB.</summary>
        public static uint StageColor(RestorationStage stage)
        {
            switch (stage)
            {
                case RestorationStage.Ruined: return 0x8C7B6B;      // grey-brown rubble
                case RestorationStage.Stabilized: return 0xB08D57;  // ochre
                case RestorationStage.Functional: return 0x8FA05A;  // olive
                case RestorationStage.Restored: return 0x4F8A5B;    // green
                default: return 0x2F6B4F;                            // deep green (flourishing)
            }
        }

        public static uint HillColor(float greenness)
        {
            // Linear blend dry 0xC9B48A → green 0x7FA772.
            float t = Clamp01(greenness);
            uint r = (uint)Math.Round(0xC9 + (0x7F - 0xC9) * t);
            uint g = (uint)Math.Round(0xB4 + (0xA7 - 0xB4) * t);
            uint b = (uint)Math.Round(0x8A + (0x72 - 0x8A) * t);
            return (r << 16) | (g << 8) | b;
        }

        /// <summary>Silhouette by content id keyword; unknown ids fall back to a generic hut.</summary>
        public static LandmarkKind KindOf(string landmarkId)
        {
            string id = landmarkId ?? string.Empty;
            if (id.Contains("trail")) return LandmarkKind.Gate;
            if (id.Contains("river") || id.Contains("intake") || id.Contains("water")) return LandmarkKind.Waterworks;
            if (id.Contains("settlement") || id.Contains("workshop") || id.Contains("mill")) return LandmarkKind.Settlement;
            if (id.Contains("canopy") || id.Contains("grove") || id.Contains("wood")) return LandmarkKind.Grove;
            if (id.Contains("wetland") || id.Contains("marsh")) return LandmarkKind.Wetland;
            if (id.Contains("observatory") || id.Contains("lookout")) return LandmarkKind.Observatory;
            return LandmarkKind.Generic;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
