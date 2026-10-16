using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Domain.Regions;

namespace WalkGame.Shell
{
    /// <summary>
    /// Draws a <see cref="RegionScene"/> with UI Toolkit vector shapes (Painter2D). No textures,
    /// no per-frame work: it repaints only when the panel asks (layout/scene change).
    /// </summary>
    public sealed class ValleyElement : VisualElement
    {
        public const float Height = 200f;
        public RegionScene Scene { get; }
        public int LastShapeCount { get; private set; }

        public ValleyElement(RegionScene scene)
        {
            Scene = scene;
            name = "valley";
            tooltip = scene.AltText;
            style.height = Height;
            style.marginBottom = 16;
            style.borderTopLeftRadius = style.borderTopRightRadius = style.borderBottomLeftRadius = style.borderBottomRightRadius = 12;
            style.overflow = Overflow.Hidden;
            generateVisualContent += Draw;
        }

        private static Color C(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);

        private void Draw(MeshGenerationContext ctx)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w <= 0 || h <= 0) return;
            var p = ctx.painter2D;
            int shapes = 0;

            Rect(p, 0, 0, w, h, C(Scene.Completed ? 0xCFE6EEu : 0xE6E0D2u)); shapes++;                // sky
            Circle(p, w * 0.86f, h * 0.16f, h * 0.07f, C(Scene.Greenness > 0.5f ? 0xF4D35Eu : 0xE9CF9Bu)); shapes++; // sun
            Hill(p, w, h, 0.45f, C(Mix(SceneHill(), 0xFFFFFF, 0.35f))); shapes++;                       // far hills
            Hill(p, w, h, 0.60f, C(SceneHill())); shapes++;                                             // near hills
            Rect(p, 0, h * 0.86f, w, h * 0.06f, C(Scene.Greenness > 0.5f ? 0x6FA3BFu : 0x9FB3B8u)); shapes++; // river
            shapes += DrawAmbient(p, w, h);                                                             // D-101 ambience

            foreach (var l in Scene.Landmarks)
            {
                float cx = l.X * w, cy = l.Y * h, s = Mathf.Min(24f + 3f * l.Level, w * 0.06f + 3f * l.Level);
                bool ruined = l.Stage == RestorationStage.Ruined;
                var col = C(RegionScene.StageColor(l.Stage));
                var dark = C(Mix(RegionScene.StageColor(l.Stage), 0x1F2A24u, 0.45f));
                shapes += Silhouette(p, l.Kind, cx, cy, s, ruined, col, dark);                           // body + top
                if (ruined)                                                                             // detail: rubble beside it
                    Rect(p, cx + s * 0.75f, cy - s * 0.22f, s * 0.45f, s * 0.22f, dark);
                else                                                                                    // detail: lit window / opening
                    Rect(p, cx - s * 0.12f, cy - s * 0.55f, s * 0.24f, s * 0.24f, C(0xF3E9C6u));
                shapes++;
                for (int g = 0; g < l.Level; g++)                                                       // growth: trees around it
                {
                    float tx = Mathf.Clamp(cx - s * 1.3f + g * (s * 2.6f / 3f), 8f, w - 8f);
                    Tri(p, tx, cy - s * 0.25f - (g % 2) * 4f, 5f + l.Level, C(0x3E7A4Bu)); shapes++;
                }
                Circle(p, cx, cy + 6f, 3f, col); shapes++;                                               // marker
                if (l.Changed)                                                                          // moment ring (static, no motion)
                {
                    p.strokeColor = C(0x1F2A24u); p.lineWidth = 2.5f; p.BeginPath();
                    var ring = RingGeometry(cx, cy, s, w, h);                                            // KI-020: kept inside the picture
                    p.Arc(new Vector2(ring.X, ring.Y), ring.R, 0f, 360f); p.Stroke(); shapes++;
                }
            }
            if (Scene.BuildSite != null)                                                                 // D-071 build site
            {
                var b = Scene.BuildSite;
                float bx = w * 0.04f, by = h * 0.84f, bw = w * 0.18f;
                Rect(p, bx, by - 4f, bw, 4f, C(0x6B5A48u)); shapes++;                                    // slab
                float step = bw / b.Stages;
                for (int k = 0; k <= b.Stage; k++)                                                      // posts rise per stage
                {
                    float ph = h * 0.05f * (1 + k);
                    Rect(p, bx + k * step + step * 0.3f, by - 4f - ph, step * 0.4f, ph, C(0xB08D57u)); shapes++;
                }
            }
            LastShapeCount = shapes;
        }

        /// <summary>
        /// Two filled shapes (body + top) per landmark. Ruined: squat, broken (top knocked aside
        /// and darkened). Restored stages stand to full height in the stage colour.
        /// </summary>
        /// <summary>Stroke width of the moment ring.</summary>
        public const float RingStroke = 2.5f;

        /// <summary>
        /// KI-020 (D-087): the moment ring around a changed landmark, shrunk and shifted only as
        /// much as needed so the whole stroke stays inside a w×h picture. Engine-free, so tests can
        /// check every landmark position without rendering.
        /// </summary>
        public static (float X, float Y, float R) RingGeometry(float cx, float cy, float s, float w, float h)
        {
            float m = RingStroke * 0.5f + 1f;
            float y0 = cy - s * 0.5f;
            // Prefer shrinking (down to 1.2×s so the ring still surrounds the landmark), then shift.
            float room = Mathf.Min(Mathf.Min(cx - m, w - m - cx), Mathf.Min(y0 - m, h - m - y0));
            float r = Mathf.Min(s * 1.9f, Mathf.Max(s * 1.2f, room));
            r = Mathf.Max(0f, Mathf.Min(r, Mathf.Min(w, h) * 0.5f - m));
            float x = Mathf.Clamp(cx, m + r, Mathf.Max(m + r, w - m - r));
            float y = Mathf.Clamp(y0, m + r, Mathf.Max(m + r, h - m - r));
            return (x, y, r);
        }

        private static int Silhouette(Painter2D p, LandmarkKind kind, float cx, float cy, float s, bool ruined, Color col, Color dark)
        {
            float hgt = ruined ? s * 0.7f : s * 1.1f;
            float tilt = ruined ? s * 0.35f : 0f;           // a ruined top sits knocked off-centre
            var top = ruined ? dark : col;
            switch (kind)
            {
                case LandmarkKind.Gate:                       // signpost + board
                    Rect(p, cx - s * 0.12f, cy - hgt * 1.3f, s * 0.24f, hgt * 1.3f, col);
                    Rect(p, cx - s * 0.6f + tilt, cy - hgt * 1.3f, s * 1.2f, s * 0.35f, top);
                    break;
                case LandmarkKind.Waterworks:                 // weir + water wheel
                    Rect(p, cx - s, cy - hgt * 0.6f, s * 2f, hgt * 0.6f, col);
                    Circle(p, cx + s * 0.55f + tilt * 0.3f, cy - hgt * 0.75f, s * (ruined ? 0.3f : 0.5f), top);
                    break;
                case LandmarkKind.Settlement:                 // house + pitched roof
                    Rect(p, cx - s * 0.8f, cy - hgt, s * 1.6f, hgt, col);
                    Tri(p, cx + tilt, cy - hgt - s * 0.35f, s * 0.6f, top);
                    break;
                case LandmarkKind.Grove:                      // trunk + crown
                    Rect(p, cx - s * 0.15f, cy - hgt, s * 0.3f, hgt, col);
                    Circle(p, cx + tilt, cy - hgt - s * 0.3f, s * (ruined ? 0.35f : 0.75f), top);
                    break;
                case LandmarkKind.Wetland:                    // pool + reeds
                    Rect(p, cx - s, cy - s * 0.25f, s * 2f, s * 0.25f, C(ruined ? 0x8C7B6Bu : 0x7FB3C8u));
                    Tri(p, cx + tilt, cy - s * 0.25f - hgt * 0.5f, hgt * 0.5f, top);
                    break;
                case LandmarkKind.Observatory:                // tower + dome
                    Rect(p, cx - s * 0.4f, cy - hgt * 1.2f, s * 0.8f, hgt * 1.2f, col);
                    Circle(p, cx + tilt, cy - hgt * 1.2f, s * 0.45f, top);
                    break;
                default:                                      // hut
                    Rect(p, cx - s * 0.7f, cy - hgt, s * 1.4f, hgt, col);
                    Tri(p, cx + tilt, cy - hgt - s * 0.3f, s * 0.5f, top);
                    break;
            }
            return 2;
        }

        /// <summary>
        /// D-101: footpaths between recovered neighbours, meadow flowers and birds. Bounded by
        /// <see cref="RegionScene.MaxAmbientShapes"/>; positions are fixed fractions, so it is static.
        /// </summary>
        private int DrawAmbient(Painter2D p, float w, float h)
        {
            int shapes = 0;
            var ls = Scene.Landmarks;
            int trail = 0;
            for (int i = 1; i < ls.Count && trail < Scene.TrailSegments; i++)
            {
                if (ls[i - 1].Stage == RestorationStage.Ruined || ls[i].Stage == RestorationStage.Ruined) continue;
                p.strokeColor = C(0xD9C9A3u); p.lineWidth = 3f; p.lineCap = LineCap.Round; p.BeginPath();
                p.MoveTo(new Vector2(ls[i - 1].X * w, ls[i - 1].Y * h + 6f));
                p.LineTo(new Vector2(ls[i].X * w, ls[i].Y * h + 6f));
                p.Stroke(); shapes++; trail++;
            }
            uint[] petals = { 0xF2C6D0u, 0xF4E3A1u, 0xE8EEF2u, 0xD7B8E8u, 0xF6D0A8u };
            for (int f = 0; f < Scene.Flowers; f++)
            {
                float fx = w * (0.10f + 0.19f * f), fy = h * (0.80f + 0.025f * (f % 2));
                Circle(p, fx, fy, 3.5f, C(petals[f % petals.Length])); shapes++;
            }
            for (int b = 0; b < Scene.Birds; b++)
            {
                float bx = w * (0.30f + 0.18f * b), by = h * (0.18f + 0.06f * b), r = 6f;
                p.strokeColor = C(0x4A5A55u); p.lineWidth = 1.6f; p.BeginPath();
                p.MoveTo(new Vector2(bx - r, by - r * 0.4f)); p.LineTo(new Vector2(bx, by)); p.LineTo(new Vector2(bx + r, by - r * 0.4f));
                p.Stroke(); shapes++;
            }
            return shapes;
        }

        private uint SceneHill() => RegionScene.HillColor(Scene.Greenness);

        private static uint Mix(uint a, uint b, float t)
        {
            uint ch(int sh) => (uint)Mathf.RoundToInt(((a >> sh) & 0xFF) * (1 - t) + ((b >> sh) & 0xFF) * t);
            return (ch(16) << 16) | (ch(8) << 8) | ch(0);
        }

        private static void Rect(Painter2D p, float x, float y, float w, float h, Color c)
        {
            p.fillColor = c; p.BeginPath();
            p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + w, y)); p.LineTo(new Vector2(x + w, y + h)); p.LineTo(new Vector2(x, y + h));
            p.ClosePath(); p.Fill();
        }

        private static void Tri(Painter2D p, float x, float y, float r, Color c)
        {
            p.fillColor = c; p.BeginPath();
            p.MoveTo(new Vector2(x, y - r)); p.LineTo(new Vector2(x + r * 0.8f, y + r)); p.LineTo(new Vector2(x - r * 0.8f, y + r));
            p.ClosePath(); p.Fill();
        }

        private static void Circle(Painter2D p, float x, float y, float r, Color c)
        {
            p.fillColor = c; p.BeginPath(); p.Arc(new Vector2(x, y), r, 0f, 360f); p.Fill();
        }

        private static void Hill(Painter2D p, float w, float h, float top, Color c)
        {
            p.fillColor = c; p.BeginPath();
            p.MoveTo(new Vector2(0, h)); p.LineTo(new Vector2(0, h * (top + 0.1f)));
            p.BezierCurveTo(new Vector2(w * 0.3f, h * (top - 0.15f)), new Vector2(w * 0.6f, h * (top + 0.2f)), new Vector2(w, h * top));
            p.LineTo(new Vector2(w, h)); p.ClosePath(); p.Fill();
        }
    }
}
