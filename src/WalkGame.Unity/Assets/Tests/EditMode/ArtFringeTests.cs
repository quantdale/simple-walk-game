using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WalkGame.Shell.Presentation;

namespace WalkGame.Shell.Tests
{
    /// <summary>D-223: neon-green / magenta key fringe must stay below threshold on every character and find-prop layer (D-214 choke).</summary>
    public sealed class ArtFringeTests
    {
        public const float MaxKeyFringeRatio = 0.04f;

        [Test] public void Canopy_character_and_find_layers_have_no_key_fringe()
        {
            string[] rel =
            {
                "Assets/Resources/Art/Canopy/rowan_body.png",
                "Assets/Resources/Art/Canopy/rowan_part_body.png",
                "Assets/Resources/Art/Canopy/rowan_part_head.png",
                "Assets/Resources/Art/Canopy/portrait_rowan_neutral.png",
                "Assets/Resources/Art/Canopy/portrait_rowan_glad.png",
                "Assets/Resources/Art/Canopy/portrait_rowan_wistful.png",
                "Assets/Resources/Art/Canopy/portrait_rowan_proud.png",
                "Assets/Resources/Art/Canopy/portrait_rowan_curious.png",
                "Assets/Resources/Art/Canopy/orchids.png",
            };
            foreach (var r in rel) AssertFringe(r);
        }

        [Test] public void Trailhead_and_intake_character_layers_have_no_key_fringe()
        {
            string[] rel =
            {
                "Assets/Resources/Art/Trailhead/mira_body.png",
                "Assets/Resources/Art/Trailhead/millstone.png",
                "Assets/Resources/Art/Intake/tavi_body.png",
            };
            foreach (var r in rel) AssertFringe(r);
        }

        [Test] public void Orchid_sprite_is_humble_versus_Rowan_canvas()
        {
            var orch = LoadPng("Assets/Resources/Art/Canopy/orchids.png");
            try
            {
                Assert.LessOrEqual(Mathf.Max(orch.width, orch.height), 240, "orchid texture stays small");
                Assert.Less(orch.height / (float)CanopyGroveArt.RowanCanvasH, 0.35f, "orchid shorter than ~⅓ of Rowan");
            }
            finally { Object.DestroyImmediate(orch); }
        }

        /// <summary>D-225: opaque chroma-key pockets (enclosed #00FF00 islands) escaped the semi-alpha fringe metric
        /// and showed as a neon fragment at Rowan's hip. Zero tolerance across every sprite under Resources/Art.</summary>
        [Test] public void No_sprite_under_Resources_Art_contains_saturated_key_green()
        {
            string root = Path.GetFullPath(Path.Combine(global::UnityEngine.Application.dataPath, ".."));
            var files = Directory.GetFiles(Path.Combine(root, "Assets/Resources/Art"), "*.png", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 20, "art sweep found sprites");
            var bad = new System.Collections.Generic.List<string>();
            foreach (var f in files)
            {
                var tex = LoadPng(f.Substring(root.Length + 1));
                try { int n = KeyGreenPixelCount(tex); if (n > 0) bad.Add(f.Substring(root.Length + 1) + "=" + n); }
                finally { Object.DestroyImmediate(tex); }
            }
            Assert.IsEmpty(bad, "saturated key-green pixels: " + string.Join(", ", bad));
        }

        /// <summary>D-225: the cut-out idle Rowan (body + head composited per CanopyGroveArt.RowanParts) carries no key green.</summary>
        [Test] public void Rowan_cutout_idle_composite_has_no_key_green()
        {
            int w = CanopyGroveArt.RowanCanvasW, h = CanopyGroveArt.RowanCanvasH;
            var canvas = new Color32[w * h];
            foreach (var p in CanopyGroveArt.RowanParts.OrderBy(p => p.order))
            {
                var tex = LoadPng("Assets/Resources/Art/Canopy/" + p.name + ".png");
                try
                {
                    var src = tex.GetPixels32();
                    for (int y = 0; y < Mathf.Min(p.h, tex.height); y++)
                    for (int x = 0; x < Mathf.Min(p.w, tex.width); x++)
                    {
                        int cx = p.x + x, cyTop = p.y + y;           // top-left canvas coords
                        if (cx < 0 || cx >= w || cyTop < 0 || cyTop >= h) continue;
                        var s = src[(tex.height - 1 - y) * tex.width + x]; // texture rows are bottom-up
                        if (s.a == 0) continue;
                        int di = (h - 1 - cyTop) * w + cx;
                        canvas[di] = s.a >= canvas[di].a ? s : Color32.Lerp(canvas[di], s, s.a / 255f);
                    }
                }
                finally { Object.DestroyImmediate(tex); }
            }
            var comp = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                comp.SetPixels32(canvas);
                Assert.Greater(canvas.Count(c => c.a > 12), w * h / 10, "composite has coverage");
                Assert.AreEqual(0, KeyGreenPixelCount(comp), "cut-out idle composite key-green pixels");
            }
            finally { Object.DestroyImmediate(comp); }
        }

        /// <summary>Near-#00FF00: green dominant by &gt;70, bright, saturated. Painted foliage (olive/sage) stays well below.</summary>
        public static int KeyGreenPixelCount(Texture2D tex)
        {
            var px = tex.GetPixels32(); int n = 0;
            foreach (var c in px)
            {
                if (c.a <= 12) continue;
                int r = c.r, g = c.g, b = c.b;
                int max = Mathf.Max(r, Mathf.Max(g, b)), min = Mathf.Min(r, Mathf.Min(g, b));
                float sat = max == 0 ? 0f : (max - min) / (float)max;
                if (g >= 150 && g > r + 70 && g > b + 70 && sat >= 0.55f) n++;
            }
            return n;
        }

        private static void AssertFringe(string relPath)
        {
            var tex = LoadPng(relPath);
            try
            {
                float ratio = KeyFringeRatio(tex);
                Assert.LessOrEqual(ratio, MaxKeyFringeRatio, relPath + " key_fringe_ratio=" + ratio.ToString("F4"));
            }
            finally { Object.DestroyImmediate(tex); }
        }

        public static float KeyFringeRatio(Texture2D tex)
        {
            var px = tex.GetPixels32();
            int edge = 0, fringe = 0;
            for (int i = 0; i < px.Length; i++)
            {
                byte a = px[i].a;
                if (a <= 12 || a >= 220) continue;
                edge++;
                // neon green (high sat) or hot magenta in the mushy rim
                float r = px[i].r, g = px[i].g, b = px[i].b;
                float max = Mathf.Max(r, Mathf.Max(g, b)), min = Mathf.Min(r, Mathf.Min(g, b));
                float sat = max <= 1f ? 0f : (max - min) / max;
                bool neonGreen = g > r + 40 && g > b + 40 && sat > 0.45f && g > 100;
                bool magenta = r > 160 && b > 160 && g < 110 && sat > 0.35f;
                if (neonGreen || magenta) fringe++;
            }
            return edge == 0 ? 0f : fringe / (float)edge;
        }

        private static Texture2D LoadPng(string relPath)
        {
            // Unity project root is parent of Assets/
            string root = Path.GetFullPath(Path.Combine(global::UnityEngine.Application.dataPath, ".."));
            string path = Path.Combine(root, relPath);
            Assert.IsTrue(File.Exists(path), path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)), path);
            return tex;
        }
    }
}
