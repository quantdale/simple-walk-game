using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Regions;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-204 (Phase D slice): the painted Old Trailhead. Implements the D-169/KI-027 presenter seams for
    /// the trailhead only: two registered ground plates crossfaded by landmark stage, one structure layer
    /// (bridge + props) by bridge visual key, Mira in-world, and a skippable dialogue panel with her
    /// portrait and the place actions (Inspect / Help restore / Maybe later). Every visible change comes
    /// from a presentation pushed by <see cref="WorldPresentationController"/>; nothing here writes state.
    /// All text is drawn in-engine (UI Toolkit), never baked into art.
    /// </summary>
    public sealed class TrailheadSceneView : MonoBehaviour, IWorldPresenter, ILandmarkView, ICharacterPresenter, ICharacterView, IDialoguePresenter
    {
        public const float PixelsPerUnit = 100f;
        private PresentationAssetResolver<string> _resolver = new PresentationAssetResolver<string>(new DictionaryAssetCatalog<string>());
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private SpriteRenderer _low = null!, _high = null!, _structure = null!, _mira = null!;
        private float _highTarget, _highFrom, _fadeT = 1f, _fadeLen;
        private Vector3 _miraHome;
        private VisualElement? _dialogueRoot;
        private System.Action<DialogueAction>? _onAction;
        private System.Action? _onSkip;

        private bool _reduced;
        public bool ReducedMotion { get => _reduced; set { _reduced = value; ApplyParticles(); Tick(_clock); } }
        /// <summary>Player text scale (1–2); the dialogue panel grows with it and buttons wrap instead of clipping.</summary>
        public float TextScale { get; set; } = 1f;
        public TrailheadFrame? Frame { get; private set; }
        public DialogueSequence? Dialogue { get; private set; }
        public bool MiraVisible => _miraOn;
        public string LocationId => TrailheadArt.LocationId;
        public string CharacterId => TrailheadArt.MiraId;
        public IReadOnlyCollection<string> MissingKeys => _resolver.MissingKeys;

        /// <summary>Builds the layers. <paramref name="catalog"/> defaults to the shipped trailhead catalog.</summary>
        public void Build(IPresentationAssetCatalog<string>? catalog = null)
        {
            if (catalog == null)
            {
                var d = new DictionaryAssetCatalog<string>();
                foreach (var kv in TrailheadArt.CatalogEntries()) d.Add(kv.Key, kv.Value);
                catalog = d;
            }
            _resolver = new PresentationAssetResolver<string>(catalog);
            _low = Layer("ground_low", 0); _high = Layer("ground_high", 1); _structure = Layer("structures", 2); _mira = Layer("npc.mira", 3);
            _miraHome = new Vector3(1.75f, -5.3f, 0f);
            _mira.transform.localPosition = _miraHome;
            float scale = 0.21f * 12.8f / (TrailheadArt.MiraCanvasH / PixelsPerUnit); // in-world height ≈ 21 % of the view (style guide §2.2)
            _mira.transform.localScale = new Vector3(scale, scale, 1f);
            BuildMiraRig();
            _ripple = Layer("water_ripple", 1);
            _ripple.sprite = SpriteFor(TrailheadArt.WaterRipple, new Vector2(0.5f, 0.5f));
            _rippleHome = new Vector3((TrailheadArt.RippleX + TrailheadArt.RippleW / 2f - 360f) / PixelsPerUnit, (640f - TrailheadArt.RippleY - TrailheadArt.RippleH / 2f) / PixelsPerUnit, 0f);
            _ripple.transform.localPosition = _rippleHome;
            _ripple.color = new Color(1f, 1f, 1f, 0.35f);
            _ripple.sortingOrder = 1; _high.sortingOrder = 1; _ripple.sortingOrder = 2; _structure.sortingOrder = 3;
            BuildParticles();
            SetMiraVisible(false);
        }

        private SpriteRenderer? _ripple;
        private Vector3 _rippleHome;
        private ParticleSystem? _motes;
        private readonly List<(Transform t, string name)> _parts = new List<(Transform, string)>();
        private float _clock;
        /// <summary>Mira's parts (cut-out rig; transform joints, not skinned bones).</summary>
        public IReadOnlyList<(Transform t, string name)> MiraParts => _parts;
        public ParticleSystem? Motes => _motes;
        public SpriteRenderer? Ripple => _ripple;

        private void BuildMiraRig()
        {
            // The root renderer stays empty; parts are children placed so their joints line up on the body canvas.
            foreach (var p in TrailheadArt.MiraParts)
            {
                var go = new GameObject(p.name); go.transform.SetParent(_mira.transform, false);
                var r = go.AddComponent<SpriteRenderer>(); r.sortingOrder = 4 + p.order;
                var tex = Resources.Load<Texture2D>(TrailheadArt.ResourceFolder + p.name);
                if (tex == null) continue;
                var pivot = new Vector2((p.px - p.x) / (float)p.w, 1f - (p.py - p.y) / (float)p.h);
                r.sprite = Sprite.Create(tex, new Rect(0, 0, Mathf.Min(p.w, tex.width), Mathf.Min(p.h, tex.height)), pivot, PixelsPerUnit); // textures are padded to multiples of 4 at the top/right (block compression)
                go.transform.localPosition = new Vector3((p.px - TrailheadArt.MiraCanvasW / 2f) / PixelsPerUnit, (TrailheadArt.MiraCanvasH - p.py) / PixelsPerUnit, 0f);
                _parts.Add((go.transform, p.name));
            }
            if (_parts.Count == 0) _mira.sprite = SpriteFor(TrailheadArt.MiraBody, new Vector2(0.5f, 0f)); // fallback: the flat body
        }

        private void BuildParticles()
        {
            var go = new GameObject("fx_motes"); go.transform.SetParent(transform, false); go.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            _motes = go.AddComponent<ParticleSystem>();
            _motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _motes.main; main.maxParticles = 60; main.startLifetime = 6f; main.startSpeed = 0.15f; main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.34f); // D-211: larger glow, still ≤ 40 live (budget ≤ 60)
            main.startColor = new Color(1f, 0.92f, 0.66f, 1f); // warm light, brighter than the plate highlights
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.playOnAwake = false;
            var shape = _motes.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(7f, 9f, 0.1f);
            var col = _motes.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sortingOrder = 10;
            var dot = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {   // D-211: bright core + soft halo, so a mote reads against both the pale and the green plate
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16, 16)) / 16f;
                float core = Mathf.Clamp01(1f - d / 0.3f), halo = Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) * 0.7f;
                dot.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(core + halo)));
            }
            dot.Apply();
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) r.sharedMaterial = new Material(shader) { mainTexture = dot };
        }

        private void ApplyParticles()
        {
            if (_motes == null || Frame == null) return;
            int n = TrailheadArt.ParticleCount(Frame.Stage, ReducedMotion);
            var em = _motes.emission; em.rateOverTime = n / 6f;
            var main = _motes.main; main.maxParticles = Mathf.Max(1, n);
            if (n == 0) _motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            else if (!_motes.isPlaying) { _motes.Play(); _motes.Simulate(6f, true, false); _motes.Play(); }
        }

        private void SetMiraVisible(bool on)
        {
            _mira.enabled = on && _parts.Count == 0;
            foreach (var p in _parts) p.t.gameObject.SetActive(on);
            _miraOn = on;
        }
        private bool _miraOn;

        /// <summary>Advances ambient motion to time <paramref name="t"/> (seconds). Reduced motion: everything at rest.</summary>
        public void Tick(float t)
        {
            _clock = t;
            var pose = TrailheadArt.IdlePose(t, ReducedMotion);
            foreach (var (tr, name) in _parts)
            {
                float deg = name.EndsWith("head") ? pose.head : name.EndsWith("braid") ? pose.braid : name.EndsWith("scarf_tail") ? pose.scarf : 0f;
                tr.localRotation = Quaternion.Euler(0, 0, deg);
                if (name.EndsWith("body")) tr.localScale = new Vector3(1f, 1f + pose.breath, 1f);
            }
            if (_ripple != null)
            {
                float speed = TrailheadArt.RippleSpeed(Frame?.Stage ?? RestorationStage.Ruined, ReducedMotion);
                _ripple.transform.localPosition = _rippleHome + new Vector3(0.06f * Mathf.Sin(t * 0.8f * speed), 0f, 0f);
                var c = _ripple.color; c.a = (Frame?.Stage ?? RestorationStage.Ruined) == RestorationStage.Ruined ? 0.12f : 0.35f + 0.1f * Mathf.Sin(t * 1.7f * speed); _ripple.color = c;
            }
        }

        private SpriteRenderer Layer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = order;
            return r;
        }

        private Sprite? SpriteFor(string? resource, Vector2 pivot)
        {
            if (string.IsNullOrEmpty(resource)) return null;
            string id = resource + pivot;
            if (_sprites.TryGetValue(id, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>(TrailheadArt.ResourceFolder + resource);
            Sprite? s = tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, PixelsPerUnit);
            if (s != null) _sprites[id] = s;
            return s;
        }

        // IWorldPresenter
        public void ShowWorld(WorldViewReadModel view) { }
        public ILandmarkView? FindLandmark(string locationId) => locationId == TrailheadArt.LocationId ? this : null;

        // ILandmarkView
        public void Show(LandmarkPresentation landmark)
        {
            var f = TrailheadArt.Map(landmark, _resolver);
            Frame = f;
            var center = new Vector2(0.5f, 0.5f);
            _low.sprite = SpriteFor(f.GroundLow, center);
            _high.sprite = SpriteFor(f.GroundHigh, center);
            _structure.sprite = SpriteFor(f.Structure, center);
            _structure.enabled = _structure.sprite != null;
            _highFrom = _high.color.a; _highTarget = f.HighShare;
            _fadeLen = TrailheadArt.TransitionSeconds(ReducedMotion);
            _fadeT = _fadeLen <= 0f ? 1f : 0f;
            if (_fadeT >= 1f) SetHigh(_highTarget);
            ApplyParticles();
            Tick(_clock);
        }

        private void SetHigh(float a) { var c = _high.color; c.a = a; _high.color = c; }

        private void Update()
        {
            if (_fadeT < 1f)
            {
                _fadeT = Mathf.Min(1f, _fadeT + Time.deltaTime / Mathf.Max(0.0001f, _fadeLen));
                SetHigh(Mathf.Lerp(_highFrom, _highTarget, _fadeT));
            }
            Tick(Time.time);
        }

        /// <summary>Jumps any running crossfade to its end (captures, tests).</summary>
        public void Settle() { _fadeT = 1f; SetHigh(_highTarget); }

        // ICharacterPresenter / ICharacterView
        public ICharacterView? FindCharacter(string characterId) => characterId == TrailheadArt.MiraId ? this : null;
        public void Show(CharacterPresentation character) => SetMiraVisible(character.LocationId == TrailheadArt.LocationId && (_parts.Count > 0 || _mira.sprite != null));
        public void React(CharacterReaction reaction) { }

        // IDialoguePresenter
        /// <summary>Where the dialogue panel is drawn, and what its buttons call (set by the host).</summary>
        public void BindDialogue(VisualElement root, System.Action<DialogueAction> onAction, System.Action onSkip)
        { _dialogueRoot = root; _onAction = onAction; _onSkip = onSkip; }

        public void ShowDialogue(DialogueSequence sequence) { Dialogue = sequence; RedrawDialogue(); }
        public void HideDialogue() { Dialogue = null; RedrawDialogue(); }
        /// <summary>Redraws after the sequence advanced.</summary>
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
