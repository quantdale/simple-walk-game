#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Animation;
using UnityEditor.U2D.PSD;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.U2D;

namespace WalkGame.Shell.Editor
{
    /// <summary>
    /// D-212: authors Mira's 2D Animation skeleton and skin weights into the PSD Importer data of
    /// <c>Assets/Resources/Art/Trailhead/Rig/mira_rig.psb</c> (character mode). Deterministic, so the rig is reproducible
    /// from the PSB plus this file: bones are placed on the D-208 joints, each layer gets a grid mesh and distance-based
    /// weights (body: hips/spine blend; head: head; braid and scarf tail: two-bone chains). Run from the menu or batchmode:
    /// <c>-executeMethod WalkGame.Shell.Editor.MiraRigAuthoring.Author</c>. The result lives in the .psb.meta.
    /// </summary>
    public static class MiraRigAuthoring
    {
        public const string Path = "Assets/Resources/Art/Trailhead/Rig/mira_rig.psb";
        const int H = 1092; // document height; image rows -> document y (bottom-up)

        // name, parent, start (image px), end (image px)
        static readonly (string name, int parent, Vector2 a, Vector2 b)[] Bones =
        {
            ("hips",     -1, new Vector2(288, 700), new Vector2(288, 520)),
            ("spine",     0, new Vector2(288, 520), new Vector2(298, 300)),
            ("head",      1, new Vector2(300, 262), new Vector2(300, 60)),
            ("braid_1",   1, new Vector2(338, 266), new Vector2(343, 370)),
            ("braid_2",   3, new Vector2(343, 370), new Vector2(345, 480)),
            ("scarf_1",   1, new Vector2(425, 315), new Vector2(470, 400)),
            ("scarf_2",   5, new Vector2(470, 400), new Vector2(520, 495)),
        };
        static readonly Dictionary<string, int[]> Influences = new Dictionary<string, int[]>
        {
            ["body"] = new[] { 0, 1 }, ["head"] = new[] { 2 }, ["braid"] = new[] { 3, 4 }, ["scarf_tail"] = new[] { 5, 6 },
        };
        static Vector2 Doc(Vector2 img) => new Vector2(img.x, H - img.y);

        [MenuItem("Walk Game/Art/Author Mira rig (D-212)")]
        public static void Author()
        {
            var importer = (PSDImporter)AssetImporter.GetAtPath(Path);
            importer.useCharacterMode = true; importer.useMosaicMode = true; importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false; importer.mosiacPadding = 4;
            foreach (var (bt, name) in new[] { (BuildTarget.Android, "Android"), (BuildTarget.iOS, "iPhone") })
            {
                var ps = importer.GetImporterPlatformSettings(bt);
                ps.name = name; ps.overridden = true; ps.format = TextureImporterFormat.ASTC_6x6; ps.maxTextureSize = 2048; importer.SetImporterPlatformSettings(ps);
            }
            EditorUtility.SetDirty(importer); importer.SaveAndReimport();
            importer = (PSDImporter)AssetImporter.GetAtPath(Path); // the reimport replaced the importer instance

            var factory = new SpriteDataProviderFactories(); factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(importer); dp.InitSpriteEditorDataProvider();
            var ch = dp.GetDataProvider<ICharacterDataProvider>(); var meshes = dp.GetDataProvider<ISpriteMeshDataProvider>();
            var boneDp = dp.GetDataProvider<ISpriteBoneDataProvider>();
            var cd = ch.GetCharacterData();

            // character bones: root in document px, children in parent-local space; each bone points along +x
            var world = Bones.Select(b => (pos: Doc(b.a), ang: Mathf.Atan2(Doc(b.b).y - Doc(b.a).y, Doc(b.b).x - Doc(b.a).x) * Mathf.Rad2Deg, len: Vector2.Distance(b.a, b.b))).ToArray();
            SpriteBone[] MakeBones(Vector2 offset) => Bones.Select((b, i) =>
            {
                var w = world[i]; Vector2 p; float r;
                if (b.parent < 0) { p = w.pos - offset; r = w.ang; }
                else { var pw = world[b.parent]; p = Quaternion.Euler(0, 0, -pw.ang) * (w.pos - pw.pos); r = w.ang - pw.ang; }
                return new SpriteBone { name = b.name, guid = GUID.Generate().ToString(), parentId = b.parent, position = p, rotation = Quaternion.Euler(0, 0, r), length = w.len, color = Color.white };
            }).ToArray();
            cd.bones = MakeBones(Vector2.zero);
            cd.pivot = new Vector2(0.5f, 0f);
            var all = Enumerable.Range(0, Bones.Length).ToArray();
            var rects = dp.GetSpriteRects();
            for (int pi = 0; pi < cd.parts.Length; pi++)
            {
                var part = cd.parts[pi]; part.bones = all; cd.parts[pi] = part;
                var sr = rects.First(r => r.spriteID.ToString() == part.spriteId);
                var name = Influences.Keys.First(k => sr.name.EndsWith(k));
                Vector2 off = part.spritePosition.position; int w = part.spritePosition.width, h = part.spritePosition.height;
                boneDp.SetBones(sr.spriteID, MakeBones(off).ToList());
                // grid mesh over the layer rect
                int step = 24, nx = Mathf.Max(2, Mathf.CeilToInt(w / (float)step) + 1), ny = Mathf.Max(2, Mathf.CeilToInt(h / (float)step) + 1);
                var verts = new List<Vertex2DMetaData>(); var idx = new List<int>();
                for (int j = 0; j < ny; j++) for (int i = 0; i < nx; i++)
                {
                    var local = new Vector2(Mathf.Min(i * step, w), Mathf.Min(j * step, h));
                    verts.Add(new Vertex2DMetaData { position = local, boneWeight = Weigh(local + off, Influences[name], world) });
                }
                for (int j = 0; j < ny - 1; j++) for (int i = 0; i < nx - 1; i++)
                { int a = j * nx + i; idx.AddRange(new[] { a, a + 1, a + nx, a + 1, a + nx + 1, a + nx }); }
                var edges = new List<Vector2Int>();
                for (int i = 0; i < nx - 1; i++) { edges.Add(new Vector2Int(i, i + 1)); edges.Add(new Vector2Int((ny - 1) * nx + i, (ny - 1) * nx + i + 1)); }
                for (int j = 0; j < ny - 1; j++) { edges.Add(new Vector2Int(j * nx, (j + 1) * nx)); edges.Add(new Vector2Int(j * nx + nx - 1, (j + 1) * nx + nx - 1)); }
                meshes.SetVertices(sr.spriteID, verts.ToArray()); meshes.SetIndices(sr.spriteID, idx.ToArray()); meshes.SetEdges(sr.spriteID, edges.ToArray());
            }
            ch.SetCharacterData(cd);
            dp.Apply();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log("D212_MIRA_RIG bones=" + cd.bones.Length + " parts=" + cd.parts.Length);
        }

        /// <summary>Distance-to-segment weights over the layer's influencing bones (smooth, normalised, ≤ 2 bones).</summary>
        static BoneWeight Weigh(Vector2 docPt, int[] bones, (Vector2 pos, float ang, float len)[] world)
        {
            var ws = bones.Select(bi =>
            {
                var a = world[bi].pos; var dir = new Vector2(Mathf.Cos(world[bi].ang * Mathf.Deg2Rad), Mathf.Sin(world[bi].ang * Mathf.Deg2Rad));
                float t = Mathf.Clamp(Vector2.Dot(docPt - a, dir), 0f, world[bi].len);
                float d = Vector2.Distance(docPt, a + dir * t);
                return (bi, w: 1f / Mathf.Pow(d + 20f, 3f));
            }).OrderByDescending(x => x.w).Take(2).ToArray();
            float s = ws.Sum(x => x.w);
            var bw = new BoneWeight { boneIndex0 = ws[0].bi, weight0 = ws[0].w / s };
            if (ws.Length > 1) { bw.boneIndex1 = ws[1].bi; bw.weight1 = ws[1].w / s; }
            return bw;
        }
    }
}
