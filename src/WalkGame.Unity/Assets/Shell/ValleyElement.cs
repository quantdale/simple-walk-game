using UnityEngine;
using UnityEngine.UIElements;

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
            Hill(p, w, h, 0.45f, C(Mix(SceneHill(), 0xFFFFFF, 0.35f))); shapes++;                       // far hills
            Hill(p, w, h, 0.60f, C(SceneHill())); shapes++;                                             // near hills
            Rect(p, 0, h * 0.86f, w, h * 0.06f, C(Scene.Greenness > 0.5f ? 0x6FA3BFu : 0x9FB3B8u)); shapes++; // river

            foreach (var l in Scene.Landmarks)
            {
                float cx = l.X * w, cy = l.Y * h, s = 10f + 3f * l.Level;
                var col = C(RegionScene.StageColor(l.Stage));
                // base: ruined = broken low block; higher stages stand taller
                Rect(p, cx - s, cy - s * (0.4f + 0.15f * l.Level), s * 2, s * (0.4f + 0.15f * l.Level), col); shapes++;
                for (int g = 0; g < l.Level; g++)                                                       // growth: trees
                {
                    float tx = cx - s + (g + 0.5f) * (2 * s / 4f);
                    Tri(p, tx, cy - s * 1.1f - (g % 2) * 4f, 6f + l.Level, C(0x3E7A4Bu)); shapes++;
                }
                Circle(p, cx, cy + 6f, 3f, col); shapes++;                                               // marker
            }
            LastShapeCount = shapes;
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
