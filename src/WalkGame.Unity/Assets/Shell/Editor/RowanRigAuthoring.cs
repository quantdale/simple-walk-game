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
    /// D-222/D-224: authors Rowan's 2D Animation skeleton into <c>Assets/Resources/Art/Canopy/Rig/rowan_rig.psb</c>
    /// (same method as Mira, D-212). Bones on the locked BODY-02 joints; distance-based skin weights.
    /// Batchmode: <c>-executeMethod WalkGame.Shell.Editor.RowanRigAuthoring.Author</c>.
    /// </summary>
    public static class RowanRigAuthoring
    {
        public const string Path = "Assets/Resources/Art/Canopy/Rig/rowan_rig.psb";
        const int H = 1076;

        // name, parent, start (image px), end (image px) — pivots from tools/art/rowan_parts.py
        static readonly (string name, int parent, Vector2 a, Vector2 b)[] Bones =
        {
            ("hips",       -1, new Vector2(220, 700), new Vector2(220, 500)),
            ("spine",       0, new Vector2(220, 500), new Vector2(230, 340)),
            ("head",        1, new Vector2(237, 322), new Vector2(237, 80)),
        };
        static readonly Dictionary<string, int[]> Influences = new Dictionary<string, int[]>
        {
            ["body"] = new[] { 0, 1 }, ["head"] = new[] { 1, 2 }, // spine+head blend at the neck
        };
        static Vector2 Doc(Vector2 img) => new Vector2(img.x, H - img.y);

        [MenuItem("Walk Game/Art/Author Rowan rig (D-224)")]
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
            importer = (PSDImporter)AssetImporter.GetAtPath(Path);

            var factory = new SpriteDataProviderFactories(); factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(importer); dp.InitSpriteEditorDataProvider();
            var ch = dp.GetDataProvider<ICharacterDataProvider>(); var meshes = dp.GetDataProvider<ISpriteMeshDataProvider>();
            var boneDp = dp.GetDataProvider<ISpriteBoneDataProvider>();
            var cd = ch.GetCharacterData();

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
            Debug.Log("D224_ROWAN_RIG bones=" + cd.bones.Length + " parts=" + cd.parts.Length);
        }

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
