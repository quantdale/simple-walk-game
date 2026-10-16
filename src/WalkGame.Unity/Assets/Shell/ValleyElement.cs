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
                    p.Arc(new Vector2(cx, cy - s * 0.5f), s * 1.9f, 0f, 360f); p.Stroke(); shapes++;
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
