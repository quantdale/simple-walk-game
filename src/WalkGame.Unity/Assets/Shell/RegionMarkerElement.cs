using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Domain.Regions;

namespace WalkGame.Shell
{
    /// <summary>
    /// D-221: a structural stage vignette for one Region map pin. Shape count and silhouette change
    /// with <see cref="LandmarkVisual.Level"/> so a grayscale read still names the stage; colour is secondary.
    /// </summary>
    public sealed class RegionMarkerElement : VisualElement
    {
        private readonly LandmarkVisual _v;
        public RegionMarkerElement(LandmarkVisual v)
        {
            _v = v;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
            style.flexGrow = 1;
        }

        private void Paint(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 2 || r.height < 2) return;
            float cx = r.x + r.width * 0.5f, cy = r.y + r.height * 0.62f;
            float s = Mathf.Min(r.width, r.height) * 0.42f;
            int level = _v.Level;
            var painter = ctx.painter2D;
            // Warm ink contour + fill from stage colour (muted).
            Color fill = ColorFrom(_v.Stage);
            Color ink = new Color(0.353f, 0.243f, 0.169f, 1f); // #5A3E2B
            painter.lineWidth = Mathf.Max(1.5f, s * 0.08f);
            painter.strokeColor = ink;
            painter.fillColor = fill;

            // Body: kind silhouette (structure first).
            switch (_v.Kind)
            {
                case LandmarkKind.Waterworks: DrawArch(painter, cx, cy, s, level); break;
                case LandmarkKind.Grove: DrawTree(painter, cx, cy, s, level); break;
                case LandmarkKind.Observatory: DrawDome(painter, cx, cy, s, level); break;
                case LandmarkKind.Settlement: DrawHouse(painter, cx, cy, s, level); break;
                case LandmarkKind.Wetland: DrawReeds(painter, cx, cy, s, level); break;
                default: DrawGate(painter, cx, cy, s, level); break;
            }
        }

        private static Color ColorFrom(RestorationStage st) => st switch
        {
            RestorationStage.Ruined => new Color(0.66f, 0.61f, 0.53f),
            RestorationStage.Stabilized => new Color(0.52f, 0.55f, 0.45f),
            RestorationStage.Functional => new Color(0.51f, 0.66f, 0.47f),
            RestorationStage.Restored => new Color(0.32f, 0.48f, 0.36f),
            _ => new Color(0.25f, 0.42f, 0.30f),
        };

        private static void DrawGate(Painter2D p, float cx, float cy, float s, int level)
        {
            if (level <= 0)
            {   // ruined: two leaning stubs + rubble
                Tri(p, cx - s * 0.55f, cy + s * 0.2f, cx - s * 0.25f, cy - s * 0.1f, cx - s * 0.05f, cy + s * 0.35f);
                Tri(p, cx + s * 0.1f, cy + s * 0.3f, cx + s * 0.45f, cy + s * 0.05f, cx + s * 0.55f, cy + s * 0.4f);
                return;
            }
            // posts
            Rect(p, cx - s * 0.45f, cy - s * 0.55f, s * 0.18f, s * 1.0f);
            Rect(p, cx + s * 0.27f, cy - s * 0.55f, s * 0.18f, s * 1.0f);
            if (level >= 2) Rect(p, cx - s * 0.45f, cy - s * 0.55f, s * 0.9f, s * 0.18f); // lintel / usable span
            if (level >= 3) Rect(p, cx - s * 0.2f, cy - s * 0.2f, s * 0.4f, s * 0.55f);   // gate leaf
            if (level >= 4) { p.fillColor = new Color(0.898f, 0.729f, 0.412f); Circ(p, cx, cy - s * 0.7f, s * 0.12f); } // garland knob
        }

        private static void DrawArch(Painter2D p, float cx, float cy, float s, int level)
        {
            if (level <= 0) { Tri(p, cx - s * 0.5f, cy + s * 0.3f, cx, cy - s * 0.1f, cx + s * 0.5f, cy + s * 0.35f); return; }
            Rect(p, cx - s * 0.5f, cy - s * 0.1f, s, s * 0.55f); // wall
            if (level >= 2) Circ(p, cx, cy - s * 0.05f, s * 0.28f); // wheel / arch
            if (level >= 3) Rect(p, cx - s * 0.08f, cy - s * 0.55f, s * 0.16f, s * 0.5f); // post
            if (level >= 4) { p.fillColor = new Color(0.51f, 0.72f, 0.77f); Rect(p, cx - s * 0.55f, cy + s * 0.25f, s * 1.1f, s * 0.12f); } // water
        }

        private static void DrawTree(Painter2D p, float cx, float cy, float s, int level)
        {
            if (level <= 0) { Rect(p, cx - s * 0.06f, cy - s * 0.2f, s * 0.12f, s * 0.6f); return; } // bare trunk
            Rect(p, cx - s * 0.07f, cy - s * 0.1f, s * 0.14f, s * 0.55f);
            Circ(p, cx, cy - s * 0.35f, s * (0.25f + 0.08f * level));
            if (level >= 4) Circ(p, cx + s * 0.25f, cy - s * 0.15f, s * 0.18f);
        }

        private static void DrawDome(Painter2D p, float cx, float cy, float s, int level)
        {
            if (level <= 0) { Tri(p, cx - s * 0.4f, cy + s * 0.2f, cx, cy - s * 0.2f, cx + s * 0.4f, cy + s * 0.25f); return; }
            Rect(p, cx - s * 0.4f, cy - s * 0.05f, s * 0.8f, s * 0.45f);
            if (level >= 2) Circ(p, cx, cy - s * 0.15f, s * 0.35f);
            if (level >= 4) Rect(p, cx - s * 0.04f, cy - s * 0.7f, s * 0.08f, s * 0.25f); // spire
        }

        private static void DrawHouse(Painter2D p, float cx, float cy, float s, int level)
        {
            if (level <= 0) { Tri(p, cx - s * 0.5f, cy + s * 0.2f, cx - s * 0.1f, cy - s * 0.1f, cx + s * 0.4f, cy + s * 0.3f); return; }
            Rect(p, cx - s * 0.4f, cy - s * 0.15f, s * 0.8f, s * 0.55f);
            Tri(p, cx - s * 0.5f, cy - s * 0.15f, cx, cy - s * 0.55f, cx + s * 0.5f, cy - s * 0.15f);
            if (level >= 2) Rect(p, cx - s * 0.08f, cy + s * 0.05f, s * 0.16f, s * 0.35f); // door
            if (level >= 3) Rect(p, cx + s * 0.15f, cy - s * 0.05f, s * 0.14f, s * 0.14f); // window
            if (level >= 4) Rect(p, cx + s * 0.28f, cy - s * 0.45f, s * 0.1f, s * 0.25f); // chimney
        }

        private static void DrawReeds(Painter2D p, float cx, float cy, float s, int level)
        {
            if (level <= 0) { Rect(p, cx - s * 0.4f, cy + s * 0.2f, s * 0.8f, s * 0.12f); return; } // mud bank
            for (int i = 0; i < 2 + level; i++)
                Rect(p, cx - s * 0.35f + i * s * 0.18f, cy - s * 0.45f, s * 0.08f, s * 0.85f);
        }

        private static void Rect(Painter2D p, float x, float y, float w, float h)
        {
            p.BeginPath(); p.MoveTo(new Vector2(x, y)); p.LineTo(new Vector2(x + w, y)); p.LineTo(new Vector2(x + w, y + h)); p.LineTo(new Vector2(x, y + h)); p.ClosePath(); p.Fill(); p.Stroke();
        }
        private static void Circ(Painter2D p, float cx, float cy, float rad)
        {
            p.BeginPath(); p.Arc(new Vector2(cx, cy), rad, 0f, 360f); p.ClosePath(); p.Fill(); p.Stroke();
        }
        private static void Tri(Painter2D p, float x1, float y1, float x2, float y2, float x3, float y3)
        {
            p.BeginPath(); p.MoveTo(new Vector2(x1, y1)); p.LineTo(new Vector2(x2, y2)); p.LineTo(new Vector2(x3, y3)); p.ClosePath(); p.Fill(); p.Stroke();
        }
    }
}
