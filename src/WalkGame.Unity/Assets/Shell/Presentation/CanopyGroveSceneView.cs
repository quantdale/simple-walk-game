using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Application.ReadModels;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-222: the painted Canopy Grove (direction A), third Region-1 place. Ground S0/S4 crossfade by landmark stage,
    /// fallen timber while Ruined, walkway from CanopyWalkway build/done, Rowan with bone idle (cut-out fallback),
    /// optional orchids find with honey glow (static under reduced motion). Presentation only.
    /// </summary>
    public sealed class CanopyGroveSceneView : MonoBehaviour, IPaintedPlaceView
    {
        public const float PixelsPerUnit = 100f;
        private PresentationAssetResolver<string> _resolver = new PresentationAssetResolver<string>(new DictionaryAssetCatalog<string>());
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private SpriteRenderer _low = null!, _high = null!, _timber = null!, _walk = null!, _orchids = null!, _rowan = null!, _glow = null!;
        private float _highTarget, _highFrom, _fadeT = 1f, _fadeLen, _clock, _reactAt = float.NegativeInfinity;
        private Vector3 _rowanHome;
        private VisualElement? _dialogueRoot;
        private System.Action<DialogueAction>? _onAction;
        private System.Action? _onSkip;
        private bool _reduced, _rowanOn, _discoveryUnlocked, _discoveryReviewed;
        private readonly List<(Transform t, string name)> _parts = new List<(Transform, string)>();
        private readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Quaternion> _boneRest = new Dictionary<string, Quaternion>();
        private Vector3 _hipsRest; private Transform? _rigRoot;
        public IReadOnlyList<(Transform t, string name)> RowanPartsList => _parts;
        public IReadOnlyDictionary<string, Transform> RowanBones => _bones;
        public Transform? RowanRigRoot => _rigRoot;

        public bool ReducedMotion { get => _reduced; set { _reduced = value; Tick(_clock); } }
        public float TextScale { get; set; } = 1f;
        public CanopyFrame? Frame { get; private set; }
        public DialogueSequence? Dialogue { get; private set; }
        public bool RowanVisible => _rowanOn;
        public string LocationId => CanopyGroveArt.LocationId;
        public string CharacterId => CanopyGroveArt.RowanId;
        public IReadOnlyCollection<string> MissingKeys => _resolver.MissingKeys;
        public SpriteRenderer Glow => _glow;
        public Transform Rowan => _rowan.transform;
        public bool OrchidsTappable => CanopyGroveArt.OrchidsTappable(_discoveryUnlocked, Frame?.Stage ?? WalkGame.Domain.Regions.RestorationStage.Ruined);
        public bool TimberVisible => Frame?.Timber != null;
        public bool WalkwayVisible => Frame?.Walkway != null;

        public void Build(IPresentationAssetCatalog<string>? catalog = null)
        {
            if (catalog == null)
            {
                var d = new DictionaryAssetCatalog<string>();
                foreach (var kv in CanopyGroveArt.CatalogEntries()) d.Add(kv.Key, kv.Value);
                catalog = d;
            }
            _resolver = new PresentationAssetResolver<string>(catalog);
            _low = Layer("ground_low", 0); _high = Layer("ground_high", 1); _timber = Layer("timber", 3); _walk = Layer("walkway", 4);
            _orchids = Layer("orchids", 5); _glow = Layer("orchids_glow", 6); _rowan = Layer("npc.rowan", 7);
            _rowanHome = new Vector3(-2.0f, -5.5f, 0f);
            _rowan.transform.localPosition = _rowanHome;
            float scale = 0.22f * 12.8f / (CanopyGroveArt.RowanCanvasH / PixelsPerUnit);
            _rowan.transform.localScale = new Vector3(scale, scale, 1f);
            BuildRowanRig();
            _glow.sprite = Sprite.Create(GlowRing(), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
            _glow.transform.localPosition = new Vector3((CanopyGroveArt.OrchidsX - 0.5f) * 7.2f, (0.5f - CanopyGroveArt.OrchidsY) * 12.8f, 0f);
            _glow.transform.localScale = new Vector3(0.38f, 0.38f, 1f);
            _glow.enabled = false;
            _orchids.sprite = SpriteFor(CanopyGroveArt.OrchidsSprite, new Vector2(0.5f, 0.5f));
            _orchids.transform.localPosition = _glow.transform.localPosition;
            float oScale = CanopyGroveArt.OrchidWorldScale;
            _orchids.transform.localScale = new Vector3(oScale, oScale, 1f);
            _orchids.enabled = false;
            SetRowan(false);
        }

        private static Texture2D GlowRing()
        {
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32)) / 32f;
                float ring = Mathf.Exp(-Mathf.Pow((d - 0.72f) / 0.12f, 2f)), core = Mathf.Clamp01(1f - d) * 0.25f;
                t.SetPixel(x, y, new Color(0.898f, 0.729f, 0.412f, Mathf.Clamp01(ring + core)));
            }
            t.Apply(); return t;
        }

        public void SetDiscovery(bool unlocked, bool reviewed) { _discoveryUnlocked = unlocked; _discoveryReviewed = reviewed; Tick(_clock); }
        public void React() { _reactAt = _clock; Tick(_clock); }

        private bool BuildBoneRig()
        {
            var prefab = Resources.Load<GameObject>(CanopyGroveArt.ResourceFolder + CanopyGroveArt.RowanRig);
            if (prefab == null) return false;
            var go = Instantiate(prefab, _rowan.transform, false); go.name = "rowan_rig";
            go.transform.localPosition = Vector3.zero;
            foreach (var tr in go.GetComponentsInChildren<Transform>(true))
                if (System.Array.IndexOf(CanopyGroveArt.RowanBones, tr.name) >= 0) { _bones[tr.name] = tr; _boneRest[tr.name] = tr.localRotation; }
            if (_bones.Count != CanopyGroveArt.RowanBones.Length) { DestroyImmediate(go); _bones.Clear(); _boneRest.Clear(); return false; }
            foreach (var r in go.GetComponentsInChildren<SpriteRenderer>(true)) r.sortingOrder += 5;
            _hipsRest = _bones["hips"].localPosition; _rigRoot = go.transform;
            return true;
        }

        private void BuildRowanRig()
        {
            // D-224: cut-out idle (reliable silhouette). Live SpriteSkin weights are authored on
            // rowan_rig.psb (hips/spine/head, spine+head neck blend) for a follow-up once mesh settles.
            foreach (var p in CanopyGroveArt.RowanParts)
            {
                var go = new GameObject(p.name); go.transform.SetParent(_rowan.transform, false);
                var r = go.AddComponent<SpriteRenderer>(); r.sortingOrder = 5 + p.order;
                var tex = Resources.Load<Texture2D>(CanopyGroveArt.ResourceFolder + p.name);
                if (tex == null) continue;
                var pivot = new Vector2((p.px - p.x) / (float)p.w, 1f - (p.py - p.y) / (float)p.h);
                r.sprite = Sprite.Create(tex, new Rect(0, 0, Mathf.Min(p.w, tex.width), Mathf.Min(p.h, tex.height)), pivot, PixelsPerUnit);
                go.transform.localPosition = new Vector3((p.px - CanopyGroveArt.RowanCanvasW / 2f) / PixelsPerUnit, (CanopyGroveArt.RowanCanvasH - p.py) / PixelsPerUnit, 0f);
                _parts.Add((go.transform, p.name));
            }
            if (_parts.Count > 0) return;
            if (BuildBoneRig()) return;
            _rowan.sprite = SpriteFor(CanopyGroveArt.RowanBody, new Vector2(0.5f, 0f));
        }

        private void SetRowan(bool on)
        {
            _rowanOn = on && (_rigRoot != null || _parts.Count > 0 || _rowan.sprite != null);
            _rowan.enabled = on && _parts.Count == 0 && _rigRoot == null;
            if (_rigRoot != null) _rigRoot.gameObject.SetActive(on);
            foreach (var p in _parts) p.t.gameObject.SetActive(on);
        }

        public void Tick(float t)
        {
            _clock = t;
            var pose = CanopyGroveArt.IdlePose(t, ReducedMotion);
            var re = TrailheadArt.ReactionPose(t - _reactAt, ReducedMotion);
            if (_rigRoot != null)
            {
                void Rot(string b, float deg) => _bones[b].localRotation = _boneRest[b] * Quaternion.Euler(0, 0, deg);
                Rot("hips", 0f); Rot("spine", pose.breath * 60f); Rot("head", pose.head + re.head);
                _bones["hips"].localPosition = _hipsRest + new Vector3(0f, re.hop * PixelsPerUnit / 100f, 0f);
            }
            foreach (var (tr, name) in _parts)
            {
                float deg = name.EndsWith("head") ? pose.head + re.head : 0f;
                tr.localRotation = Quaternion.Euler(0, 0, deg);
                if (name.EndsWith("body")) tr.localScale = new Vector3(1f, 1f + pose.breath, 1f);
            }
            if (_rigRoot == null && _parts.Count == 0)
            {
                _rowan.transform.localRotation = Quaternion.Euler(0, 0, pose.head * 0.5f);
                float s = Mathf.Abs(_rowan.transform.localScale.x);
                _rowan.transform.localScale = new Vector3(s, s * (1f + pose.breath), 1f);
                _rowan.transform.localPosition = _rowanHome + new Vector3(0f, re.hop, 0f);
            }
            else _rowan.transform.localPosition = _rowanHome;
            var st = Frame?.Stage ?? WalkGame.Domain.Regions.RestorationStage.Ruined;
            bool showFind = CanopyGroveArt.OrchidsTappable(_discoveryUnlocked, st);
            _orchids.enabled = showFind && _orchids.sprite != null;
            bool glow = CanopyGroveArt.OrchidsGlow(_discoveryUnlocked, st, _discoveryReviewed);
            _glow.enabled = glow;
            if (glow) _glow.color = new Color(1f, 1f, 1f, CanopyGroveArt.GlowAlpha(t, ReducedMotion));
        }

        private SpriteRenderer Layer(string name, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>(); r.sortingOrder = order; return r;
        }

        private Sprite? SpriteFor(string? resource, Vector2 pivot)
        {
            if (string.IsNullOrEmpty(resource)) return null;
            string id = resource + pivot;
            if (_sprites.TryGetValue(id, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>(CanopyGroveArt.ResourceFolder + resource);
            Sprite? s = tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, PixelsPerUnit);
            if (s != null) _sprites[id] = s;
            return s;
        }

        public void ShowWorld(WorldViewReadModel view) { }
        public ILandmarkView? FindLandmark(string locationId) => locationId == CanopyGroveArt.LocationId ? this : null;

        public void Show(LandmarkPresentation landmark)
        {
            var f = CanopyGroveArt.Map(landmark, _resolver);
            Frame = f;
            var c = new Vector2(0.5f, 0.5f);
            _low.sprite = SpriteFor(f.GroundLow, c); _high.sprite = SpriteFor(f.GroundHigh, c);
            _timber.sprite = SpriteFor(f.Timber, c); _timber.enabled = _timber.sprite != null;
            _walk.sprite = SpriteFor(f.Walkway, c); _walk.enabled = _walk.sprite != null;
            _highFrom = _high.color.a; _highTarget = f.HighShare;
            _fadeLen = TrailheadArt.TransitionSeconds(ReducedMotion);
            _fadeT = _fadeLen <= 0f ? 1f : 0f;
            if (_fadeT >= 1f) SetHigh(_highTarget);
            Tick(_clock);
        }

        private void SetHigh(float a) { var col = _high.color; col.a = a; _high.color = col; }
        private void Update()
        {
            if (_fadeT < 1f) { _fadeT = Mathf.Min(1f, _fadeT + Time.deltaTime / Mathf.Max(0.0001f, _fadeLen)); SetHigh(Mathf.Lerp(_highFrom, _highTarget, _fadeT)); }
            Tick(Time.time);
        }
        public void Settle() { _fadeT = 1f; SetHigh(_highTarget); }

        public ICharacterView? FindCharacter(string characterId) => characterId == CanopyGroveArt.RowanId ? this : null;
        public void Show(CharacterPresentation character) => SetRowan(character.LocationId == CanopyGroveArt.LocationId && (_rigRoot != null || _parts.Count > 0 || _rowan.sprite != null));
        public void React(CharacterReaction reaction) { }

        public void BindDialogue(VisualElement root, System.Action<DialogueAction> onAction, System.Action onSkip)
        { _dialogueRoot = root; _onAction = onAction; _onSkip = onSkip; }
        public void ShowDialogue(DialogueSequence sequence) { Dialogue = sequence; RedrawDialogue(); }
        public void HideDialogue() { Dialogue = null; RedrawDialogue(); }
        public void RedrawDialogue()
        {
            if (_dialogueRoot == null) return;
            _dialogueRoot.Clear();
            var beat = Dialogue?.Current;
            if (Dialogue == null || beat == null) return;
            _dialogueRoot.Add(TrailheadDialoguePanel.Build(beat, Dialogue.Actions, SpriteFor(_resolver.Resolve(beat.PortraitKey + "@" + beat.Expression).Asset, new Vector2(0.5f, 0.5f))?.texture,
                a => _onAction?.Invoke(a), () => _onSkip?.Invoke(), TextScale));
        }
    }
}
