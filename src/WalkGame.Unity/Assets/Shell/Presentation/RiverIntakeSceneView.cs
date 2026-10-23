using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Application.ReadModels;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-216: the painted River Intake (direction A), the second Region-1 place. Same seams and bar as the trailhead:
    /// two registered ground plates crossfaded by landmark stage over a shared riverbed, one gate layer by the intake's
    /// visual key, Tavi in-world with a gentle idle, the shared dialogue panel, and the optional brass-plate find with a
    /// honey glow ring (static under reduced motion). Every visible change comes from a pushed presentation; nothing here
    /// writes state. All text is drawn in-engine.
    /// </summary>
    public sealed class RiverIntakeSceneView : MonoBehaviour, IPaintedPlaceView
    {
        public const float PixelsPerUnit = 100f;
        private PresentationAssetResolver<string> _resolver = new PresentationAssetResolver<string>(new DictionaryAssetCatalog<string>());
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private SpriteRenderer _low = null!, _high = null!, _gate = null!, _tavi = null!, _glow = null!;
        private float _highTarget, _highFrom, _fadeT = 1f, _fadeLen, _clock, _reactAt = float.NegativeInfinity;
        private Vector3 _taviHome;
        private VisualElement? _dialogueRoot;
        private System.Action<DialogueAction>? _onAction;
        private System.Action? _onSkip;
        private bool _reduced, _taviOn, _discoveryUnlocked, _discoveryReviewed;

        public bool ReducedMotion { get => _reduced; set { _reduced = value; Tick(_clock); } }
        public float TextScale { get; set; } = 1f;
        public IntakeFrame? Frame { get; private set; }
        public DialogueSequence? Dialogue { get; private set; }
        public bool TaviVisible => _taviOn;
        public string LocationId => RiverIntakeArt.LocationId;
        public string CharacterId => RiverIntakeArt.TaviId;
        public IReadOnlyCollection<string> MissingKeys => _resolver.MissingKeys;
        public SpriteRenderer Glow => _glow;
        public Transform Tavi => _tavi.transform;
        public bool PlateTappable => RiverIntakeArt.PlateTappable(_discoveryUnlocked, Frame?.GateIndex);

        public void Build(IPresentationAssetCatalog<string>? catalog = null)
        {
            if (catalog == null)
            {
                var d = new DictionaryAssetCatalog<string>();
                foreach (var kv in RiverIntakeArt.CatalogEntries()) d.Add(kv.Key, kv.Value);
                catalog = d;
            }
            _resolver = new PresentationAssetResolver<string>(catalog);
            _low = Layer("ground_low", 0); _high = Layer("ground_high", 1); _gate = Layer("gate", 3); _glow = Layer("plate_glow", 4); _tavi = Layer("npc.tavi", 5);
            _taviHome = new Vector3(-2.1f, -5.6f, 0f);
            _tavi.transform.localPosition = _taviHome;
            float scale = 0.22f * 12.8f / (RiverIntakeArt.TaviCanvasH / PixelsPerUnit);   // ≈ 22 % of the view, like Mira
            _tavi.transform.localScale = new Vector3(scale, scale, 1f);
            _tavi.sprite = SpriteFor(RiverIntakeArt.TaviBody, new Vector2(0.5f, 0f));
            _glow.sprite = Sprite.Create(GlowRing(), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64f);
            _glow.transform.localPosition = new Vector3((RiverIntakeArt.PlateX - 0.5f) * 7.2f, (0.5f - RiverIntakeArt.PlateY) * 12.8f, 0f);
            _glow.transform.localScale = new Vector3(0.62f, 0.62f, 1f);
            _glow.enabled = false;
            SetTavi(false);
        }

        /// <summary>A soft honey ring (decoration colour, never text), drawn in code so it needs no art file.</summary>
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

        /// <summary>The canonical state of the optional find (read by the host from the existing discoveries read model).</summary>
        public void SetDiscovery(bool unlocked, bool reviewed) { _discoveryUnlocked = unlocked; _discoveryReviewed = reviewed; Tick(_clock); }

        public void React() { _reactAt = _clock; Tick(_clock); }

        private void SetTavi(bool on) { _taviOn = on && _tavi.sprite != null; _tavi.enabled = _taviOn; }

        public void Tick(float t)
        {
            _clock = t;
            var pose = RiverIntakeArt.IdlePose(t, ReducedMotion);
            var re = TrailheadArt.ReactionPose(t - _reactAt, ReducedMotion);   // the shared glad nod/hop timing
            _tavi.transform.localRotation = Quaternion.Euler(0, 0, pose.sway + re.head * 0.4f);
            float s = _tavi.transform.localScale.x;
            _tavi.transform.localScale = new Vector3(s, s * (1f + pose.breath), 1f);
            _tavi.transform.localPosition = _taviHome + new Vector3(0f, re.hop, 0f);
            bool glow = RiverIntakeArt.PlateGlows(_discoveryUnlocked, Frame?.GateIndex, _discoveryReviewed);
            _glow.enabled = glow;
            if (glow) _glow.color = new Color(1f, 1f, 1f, RiverIntakeArt.GlowAlpha(t, ReducedMotion));
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
            var tex = Resources.Load<Texture2D>(RiverIntakeArt.ResourceFolder + resource);
            Sprite? s = tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, PixelsPerUnit);
            if (s != null) _sprites[id] = s;
            return s;
        }

        public void ShowWorld(WorldViewReadModel view) { }
        public ILandmarkView? FindLandmark(string locationId) => locationId == RiverIntakeArt.LocationId ? this : null;

        public void Show(LandmarkPresentation landmark)
        {
            var f = RiverIntakeArt.Map(landmark, _resolver);
            Frame = f;
            var c = new Vector2(0.5f, 0.5f);
            _low.sprite = SpriteFor(f.GroundLow, c); _high.sprite = SpriteFor(f.GroundHigh, c);
            _gate.sprite = SpriteFor(f.Gate, c); _gate.enabled = _gate.sprite != null;
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

        public ICharacterView? FindCharacter(string characterId) => characterId == RiverIntakeArt.TaviId ? this : null;
        public void Show(CharacterPresentation character) => SetTavi(character.LocationId == RiverIntakeArt.LocationId);
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
