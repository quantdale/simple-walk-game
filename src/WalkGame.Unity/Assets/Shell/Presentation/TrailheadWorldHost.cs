using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Application;
using WalkGame.Application.ReadModels;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-207: hosts the painted Old Trailhead in the running app (opened from Home). Owns a camera, the
    /// <see cref="TrailheadSceneView"/>, and one UI Toolkit overlay with: a tap layer (taps on Mira, the
    /// signpost and the bridge go through <see cref="TrailheadArt.HitTest"/> and the controller), the dialogue
    /// panel, goal and inspect cards, and a Home button so the text shell is always one tap away. Every state
    /// change goes through <see cref="WorldPresentationController"/> (goal start = the existing EnqueueProject).
    /// </summary>
    public sealed class TrailheadWorldHost : MonoBehaviour, IChoicePreviewPresenter, IInspectPresenter, IVisualChangePlayer
    {
        private static readonly Color Canvas = new Color(0.961f, 0.922f, 0.867f), Charcoal = new Color(0.239f, 0.251f, 0.220f),
            ForestDeep = new Color(0.247f, 0.384f, 0.282f), Ink = new Color(0.353f, 0.243f, 0.169f);
        private Camera? _cam; private UIDocument? _doc; private VisualElement? _cards;
        private Action? _onClose;
        private IPaintedPlaceView _place = null!;
        /// <summary>The painted place on screen (D-216: the Old Trailhead or the River Intake).</summary>
        public IPaintedPlaceView Place => _place;
        /// <summary>The trailhead view, or null while the River Intake is shown.</summary>
        public TrailheadSceneView View => (_place as TrailheadSceneView)!;
        /// <summary>D-216: the River Intake view, or null while the trailhead is shown.</summary>
        public RiverIntakeSceneView? Intake => _place as RiverIntakeSceneView;
        public string LocationId => _place.LocationId;
        private GameSession _session = null!; private IPresentationMemory _memory = null!; private IOsMotionPreference _os = null!;
        private float _textScale; private PanelSettings? _panelOverride; private RenderTexture? _target;
        public WorldPresentationController Controller { get; private set; } = null!;
        public VisualElement? Overlay => _doc?.rootVisualElement;
        public string? ShownCard { get; private set; }

        /// <summary>Opens the trailhead for <paramref name="session"/>. <paramref name="panelOverride"/> is for captures.</summary>
        public void Open(GameSession session, IPresentationMemory memory, IOsMotionPreference os, float textScale, Action onClose,
            PanelSettings? panelOverride = null, RenderTexture? target = null, string? locationId = null)
        {
            _onClose = onClose; _session = session; _memory = memory; _os = os; _textScale = textScale; _panelOverride = panelOverride; _target = target;
            var camGo = new GameObject("trailhead-camera"); camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true; _cam.orthographicSize = 6.4f; _cam.transform.localPosition = new Vector3(0, 0, -10);
            _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = Canvas; _cam.depth = 10;
            if (target != null) _cam.targetTexture = target;
            BuildPlace(locationId);
            _doc = gameObject.AddComponent<UIDocument>();
            if (panelOverride != null) _doc.panelSettings = panelOverride;
            else
            {
                var panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ShellTheme");
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize; panel.referenceResolution = new Vector2Int(720, 1280);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; panel.match = 1f;
                panel.clearColor = false; panel.sortingOrder = 10;
                _doc.panelSettings = panel;
            }
            BuildOverlay();
            Controller.Resume();
            RefreshFind();
        }

        private GameObject? _viewGo;
        private void BuildPlace(string? locationId)
        {
            _viewGo = new GameObject(locationId == RiverIntakeArt.LocationId ? "intake-view" : "trailhead-view"); _viewGo.transform.SetParent(transform, false);
            _place = locationId == RiverIntakeArt.LocationId ? _viewGo.AddComponent<RiverIntakeSceneView>() : (IPaintedPlaceView)_viewGo.AddComponent<TrailheadSceneView>();
            _place.Build();
            _place.ReducedMotion = MotionPreference.Effective(_session.GetSettings().ReducedMotion, _os);
            _place.TextScale = _textScale;
            Controller = new WorldPresentationController(_session, _place, _place, this, this, _memory, _place, this) { OsMotion = _os };
        }

        /// <summary>D-216: walks to another painted place in the same host (the overlay and Home button stay).</summary>
        public void GoTo(string locationId)
        {
            if (locationId == _place.LocationId) return;
            Controller.CloseLocation(); _cards?.Clear(); ShownCard = null; _choices = null;
            if (_viewGo != null) DestroyImmediate(_viewGo);
            BuildPlace(locationId);
            BuildOverlay();
            Controller.Resume();
            RefreshFind();
        }

        /// <summary>The optional find for the painted place on screen (intake plate or trailhead millstone).</summary>
        private string? ActiveDiscoveryId =>
            Intake != null ? RiverIntakeArt.DiscoveryId : TrailheadArt.DiscoveryId;

        private WalkGame.Application.ReadModels.DiscoveriesReadModel.DiscoveryRow? FindRow(string? id = null)
        {
            id ??= ActiveDiscoveryId;
            if (id == null) return null;
            foreach (var r in _session.GetDiscoveries().Discoveries) if (r.DiscoveryId == id) return r;
            return null;
        }
        private void RefreshFind()
        {
            var r = FindRow();
            bool unlocked = r != null && r.Unlocked, reviewed = r != null && r.Reviewed;
            if (Intake != null) Intake.SetDiscovery(unlocked, reviewed);
            else if (_place is TrailheadSceneView th) th.SetDiscovery(unlocked, reviewed);
            if (_plateButton != null)
            {
                bool show = Intake != null ? Intake.PlateTappable : (_place is TrailheadSceneView v && v.MillstoneTappable);
                _plateButton.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
        private Button? _plateButton;

        /// <summary>Optional find. Shows its card (in-engine text) and marks it reviewed through the existing
        /// command (GAME_SYSTEMS §7: presentation convenience, never gates progress). False when it is not offered.</summary>
        public bool OpenFind()
        {
            if (_cards == null) return false;
            bool offered = Intake != null ? Intake.PlateTappable : (_place is TrailheadSceneView v && v.MillstoneTappable);
            if (!offered) return false;
            var r = FindRow(); if (r == null) return false;
            _cards.Clear(); ShownCard = "find:" + r.DiscoveryId;
            var c = Card();
            var title = new Label(PlayerStrings.F("world.findHeading", WalkGame.Shell.PlayerCopy.Title(r.TitleKey))) { name = "find-title" }; Text(title, 20, true); c.Add(title);
            var body = _session.DiscoveryCopy(r.DiscoveryId, WalkGame.Application.Content.ContentText.Fields.Body, r.BodyKey);
            if (body != null) { var b = new Label(body) { name = "find-body" }; Text(b, 16, false); c.Add(b); }
            var origin = _session.DiscoveryCopy(r.DiscoveryId, WalkGame.Application.Content.ContentText.Fields.Provenance, r.ProvenanceKey);
            if (origin != null) { var o = new Label(origin) { name = "find-origin" }; Text(o, 15, false); o.style.unityFontStyleAndWeight = FontStyle.Italic; c.Add(o); }
            var close = new Button(CloseFind) { text = PlayerStrings.T("world.close"), name = "card-close" };
            Style(close, Canvas, Charcoal, 18 * _place.TextScale); c.Add(close);
            _cards.Add(c);
            _session.MarkDiscoveryReviewed(r.DiscoveryId);
            RefreshFind();
            return true;
        }

        public void CloseFind() { if (ShownCard != null && ShownCard.StartsWith("find:")) { _cards?.Clear(); ShownCard = null; } }

        private void Text(Label l, float size, bool heading)
        { l.style.color = Charcoal; l.style.fontSize = size * _place.TextScale; l.style.whiteSpace = WhiteSpace.Normal; if (heading) l.style.unityFontStyleAndWeight = FontStyle.Bold; l.style.marginBottom = 4; }

        private void BuildOverlay()
        {
            var root = _doc!.rootVisualElement; root.Clear();
            var tap = new VisualElement { name = "world-tap" }; Fill(tap);
            tap.RegisterCallback<PointerDownEvent>(e =>
            {
                var r = tap.worldBound;
                TapAt(new Vector2((e.position.x - r.xMin) / r.width, (e.position.y - r.yMin) / r.height), r.width / r.height);
            });
            root.Add(tap);
            var dialogue = new VisualElement { name = "world-dialogue", pickingMode = PickingMode.Ignore }; Fill(dialogue); root.Add(dialogue);
            _cards = new VisualElement { name = "world-cards", pickingMode = PickingMode.Ignore }; Fill(_cards); root.Add(_cards);
            var home = new Button(Close) { text = PlayerStrings.T("label.home"), name = "world-home" };
            home.style.position = Position.Absolute; home.style.top = 12; home.style.right = 12; home.style.minHeight = 48;
            Style(home, Canvas, Charcoal, 18 * Mathf.Max(1f, _place.TextScale));
            root.Add(home);
            // D-216: the path between the two painted places (only once the intake is open to the player).
            bool atIntake = Intake != null;
            if (atIntake || IntakeOpen())
            {
                var go = new Button(() => GoTo(atIntake ? TrailheadArt.LocationId : RiverIntakeArt.LocationId))
                    { text = PlayerStrings.T(atIntake ? "world.goTrailhead" : "world.goIntake"), name = "world-travel" };
                go.style.position = Position.Absolute; go.style.top = 72; go.style.right = 12; go.style.maxWidth = 300;
                Style(go, Canvas, Charcoal, 18 * Mathf.Max(1f, _place.TextScale)); go.style.whiteSpace = WhiteSpace.Normal;
                root.Add(go);
            }
            _plateButton = null;
            {   // screen-reader / keyboard route to the same optional find (the glow ring is the sighted route)
                string key = atIntake ? "world.plate" : "world.millstone";
                float top = atIntake ? 132f : (IntakeOpen() ? 132f : 72f);
                _plateButton = new Button(() => OpenFind()) { text = PlayerStrings.T(key), name = "world-find" };
                _plateButton.style.position = Position.Absolute; _plateButton.style.top = top; _plateButton.style.right = 12; _plateButton.style.maxWidth = 300;
                Style(_plateButton, Canvas, Charcoal, 16 * Mathf.Max(1f, _place.TextScale)); _plateButton.style.whiteSpace = WhiteSpace.Normal;
                root.Add(_plateButton);
            }
            _place.BindDialogue(dialogue, a => { if (a.Kind == DialogueActionKind.HelpRestore) _place.React(); Controller.ChooseDialogueAction(a); }, () => Controller.SkipDialogue());
        }

        /// <summary>A tap at normalized screen coordinates (0..1 from top-left) on a screen of the given aspect (w/h).</summary>
        private bool IntakeOpen()
        {
            foreach (var l in _session.GetWorldView().Locations) if (l.LocationId == RiverIntakeArt.LocationId) return l.IsUnlocked;
            return false;
        }

        /// <summary>D-216: a tap on the River Intake (Tavi talks, the plate opens the find, the gate opens its card or the place).</summary>
        public IntakeTarget TapIntakeAt(Vector2 screen01, float aspect)
        {
            if (Intake == null) return IntakeTarget.None;
            var art = TrailheadArt.ScreenToArt(screen01.x, screen01.y, aspect);
            var target = RiverIntakeArt.HitTest(art.x, art.y, Intake.TaviVisible, Intake.PlateTappable);
            switch (target)
            {
                case IntakeTarget.Tavi: Controller.TapCharacter(RiverIntakeArt.TaviId); break;
                case IntakeTarget.Plate: OpenFind(); break;
                case IntakeTarget.Gate:
                    if (Controller.TapProp(RiverIntakeArt.GateId) != TapOutcome.Opened) Controller.TapLandmark(RiverIntakeArt.LocationId);
                    break;
            }
            return target;
        }

        public TrailheadTarget TapAt(Vector2 screen01, float aspect)
        {
            if (Intake != null) { TapIntakeAt(screen01, aspect); return TrailheadTarget.None; }
            var art = TrailheadArt.ScreenToArt(screen01.x, screen01.y, aspect);
            var target = TrailheadArt.HitTest(art.x, art.y, View.MiraVisible, View.MillstoneTappable);
            if (target == TrailheadTarget.Millstone) { OpenFind(); return target; }
            TrailheadArt.Route(target, Controller);
            return target;
        }

        public void Close()
        {
            Controller?.CloseLocation();
            var cb = _onClose; _onClose = null;
            Destroy(gameObject);
            cb?.Invoke();
        }

        // IChoicePreviewPresenter: the goal card; its start button is the existing EnqueueProject command.
        private IReadOnlyList<RestorationChoicesReadModel.Choice>? _choices;
        private bool _choicesDrawn;
        public void ShowChoices(string locationId, IReadOnlyList<RestorationChoicesReadModel.Choice> choices) { _choices = choices; _choicesDrawn = false; DrawChoicesIfFree(); }
        /// <summary>Goal cards wait while a dialogue is open (the dialogue offers Help restore); drawn as soon as it closes.</summary>
        public void DrawChoicesIfFree()
        {
            if (_cards == null || _choices == null || _choicesDrawn || _place.Dialogue != null) return;
            _cards.Clear(); _choicesDrawn = true; ShownCard = "choices";
            var card = Card();
            foreach (var c in _choices)
            {
                var label = new Label(c.Title); label.style.color = Charcoal; label.style.fontSize = 20 * _place.TextScale; label.style.whiteSpace = WhiteSpace.Normal;
                card.Add(label);
                var progress = new Label(PlayerStrings.F("world.goalProgress", c.VitalityInvested, c.VitalityCost)); progress.style.color = Charcoal; progress.style.fontSize = 16 * _place.TextScale;
                card.Add(progress);
                if (c.Status == WalkGame.Domain.Projects.ProjectStatus.Available)
                {
                    var id = c.ProjectId;
                    var b = new Button(() => Controller.ChooseRestorationGoal(id)) { text = PlayerStrings.F("label.start", c.Title), name = "start-goal" };
                    Style(b, ForestDeep, Canvas, 18 * _place.TextScale); card.Add(b);
                }
            }
            card.Add(CloseButton());
            _cards.Add(card);
        }
        private void LateUpdate() => DrawChoicesIfFree();
        public void HideChoices() { _choices = null; if (ShownCard == "choices") { _cards?.Clear(); ShownCard = null; } }

        public void ShowInspect(InspectCard card)
        {
            if (_cards == null) return;
            _cards.Clear(); ShownCard = "inspect:" + (card.ProjectId ?? card.LocationId);
            var c = Card();
            var t = new Label(card.ProjectTitle ?? card.LocationTitle); t.style.color = Charcoal; t.style.fontSize = 20 * _place.TextScale; t.style.whiteSpace = WhiteSpace.Normal;
            c.Add(t);
            if (card.ProjectId != null)
            {
                var p = new Label(PlayerStrings.F("world.goalProgress", card.VitalityInvested, card.VitalityCost)); p.style.color = Charcoal; p.style.fontSize = 16 * _place.TextScale;
                c.Add(p);
            }
            c.Add(CloseButton());
            _cards.Add(c);
        }
        public void HideInspect() { if (ShownCard != null && ShownCard.StartsWith("inspect")) { _cards?.Clear(); ShownCard = null; } }

        public void Play(IReadOnlyList<VisualChangeEvent> events, bool reducedMotion) { _place.ReducedMotion = reducedMotion; RefreshFind(); }

        private Button CloseButton()
        {
            var b = new Button(() => Controller.CloseLocation()) { text = PlayerStrings.T("world.close"), name = "card-close" };
            Style(b, Canvas, Charcoal, 18 * _place.TextScale); return b;
        }

        private VisualElement Card()
        {
            var c = new VisualElement { name = "world-card" };
            c.style.position = Position.Absolute; c.style.left = 16; c.style.right = 16; c.style.bottom = 24;
            c.style.backgroundColor = Canvas; c.style.paddingLeft = 14; c.style.paddingRight = 14; c.style.paddingTop = 12; c.style.paddingBottom = 12;
            Border(c, Ink, 3, 18);
            return c;
        }

        private static void Fill(VisualElement e) { e.style.position = Position.Absolute; e.style.left = 0; e.style.right = 0; e.style.top = 0; e.style.bottom = 0; }
        private static void Style(Button b, Color bg, Color fg, float size)
        { b.style.backgroundColor = bg; b.style.color = fg; b.style.fontSize = size; b.style.minHeight = 48; b.style.marginTop = 6; Border(b, Ink, 2, 14); }
        private static void Border(VisualElement e, Color c, float w, float r)
        {
            e.style.borderTopColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c; e.style.borderRightColor = c;
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w; e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }
    }
}
