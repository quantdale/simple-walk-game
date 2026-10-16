using System;
using System.Collections.Generic;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Regions;

namespace WalkGame.Shell
{
    /// <summary>One landmark as drawn in the valley picture.</summary>
    public sealed class LandmarkVisual
    {
        public LandmarkVisual(string label, RestorationStage stage, float x, float y)
        { Label = label; Stage = stage; X = x; Y = y; }
        public string Label { get; }
        public RestorationStage Stage { get; }
        /// <summary>Normalized position in the picture, 0..1.</summary>
        public float X { get; }
        public float Y { get; }
        /// <summary>0 (ruined) .. 4 (flourishing): drives size, shape count and colour.</summary>
        public int Level => (int)Stage;
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
        public const int MaxLandmarks = 12;
        public const int ShapesPerLandmark = 6;   // base + up to 4 growth shapes + marker
        public const int BackdropShapes = 4;      // sky, far hills, near hills, river
        public const int MaxShapes = BackdropShapes + MaxLandmarks * ShapesPerLandmark;

        public IReadOnlyList<LandmarkVisual> Landmarks { get; }
        /// <summary>0..1 share of restorations finished; tints the hills from dry to green.</summary>
        public float Greenness { get; }
        public bool Completed { get; }
        /// <summary>Text alternative for the picture (UX_DESIGN accessibility: never picture-only).</summary>
        public string AltText { get; }

        private RegionScene(IReadOnlyList<LandmarkVisual> landmarks, float greenness, bool completed, string alt)
        { Landmarks = landmarks; Greenness = greenness; Completed = completed; AltText = alt; }

        public static RegionScene Build(RegionReadModel region)
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
                list.Add(new LandmarkVisual(PlayerCopy.Title(l.TitleKey), l.Stage, x, y));
            }
            float green = region.TotalProjects <= 0 ? 0f : Clamp01((float)region.CompletedProjects / region.TotalProjects);
            if (region.RegionCompleted) green = 1f;
            int restored = 0;
            foreach (var v in list) if (v.Stage >= RestorationStage.Restored) restored++;
            string alt = region.RegionCompleted
                ? "A picture of the valley, green and fully restored."
                : "A picture of the valley: " + restored + " of " + list.Count + " landmarks restored, the hills "
                  + (green < 0.34f ? "still dry." : green < 0.67f ? "turning green." : "mostly green.");
            return new RegionScene(list, green, region.RegionCompleted, alt);
        }

        /// <summary>Total filled shapes a repaint will issue (budget check).</summary>
        public int ShapeCount()
        {
            int shapes = BackdropShapes;
            foreach (var l in Landmarks) shapes += 2 + l.Level; // base + marker + growth
            return shapes;
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

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
