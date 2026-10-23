using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Application.ReadModels;

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

        public bool ReducedMotion { get; set; }
        public TrailheadFrame? Frame { get; private set; }
        public DialogueSequence? Dialogue { get; private set; }
        public bool MiraVisible => _mira != null && _mira.enabled;
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
            _mira.sprite = SpriteFor(TrailheadArt.MiraBody, new Vector2(0.5f, 0f));
            if (_mira.sprite != null)
            {
                float h = 0.21f * 12.8f; // in-world height ≈ 21 % of the 12.8-unit view (style guide §2.2)
                float s = h / _mira.sprite.bounds.size.y;
                _mira.transform.localScale = new Vector3(s, s, 1f);
            }
            _miraHome = new Vector3(1.75f, -5.3f, 0f);
            _mira.transform.localPosition = _miraHome;
            _mira.enabled = false;
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
        }

        private void SetHigh(float a) { var c = _high.color; c.a = a; _high.color = c; }

        private void Update()
        {
            if (_fadeT < 1f)
            {
                _fadeT = Mathf.Min(1f, _fadeT + Time.deltaTime / Mathf.Max(0.0001f, _fadeLen));
                SetHigh(Mathf.Lerp(_highFrom, _highTarget, _fadeT));
            }
            if (_mira != null && _mira.enabled)
                _mira.transform.localPosition = _miraHome + new Vector3(0f, TrailheadArt.IdleBobAmplitude(ReducedMotion) * Mathf.Sin(Time.time * 2f * Mathf.PI / 3.2f), 0f);
        }

        /// <summary>Jumps any running crossfade to its end (captures, tests).</summary>
        public void Settle() { _fadeT = 1f; SetHigh(_highTarget); }

        // ICharacterPresenter / ICharacterView
        public ICharacterView? FindCharacter(string characterId) => characterId == TrailheadArt.MiraId ? this : null;
        public void Show(CharacterPresentation character) => _mira.enabled = character.LocationId == TrailheadArt.LocationId && _mira.sprite != null;
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
                a => _onAction?.Invoke(a), () => _onSkip?.Invoke()));
        }
    }
}
