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
