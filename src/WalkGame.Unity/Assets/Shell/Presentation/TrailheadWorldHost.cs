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
        public TrailheadSceneView View { get; private set; } = null!;
        public WorldPresentationController Controller { get; private set; } = null!;
        public VisualElement? Overlay => _doc?.rootVisualElement;
        public string? ShownCard { get; private set; }

        /// <summary>Opens the trailhead for <paramref name="session"/>. <paramref name="panelOverride"/> is for captures.</summary>
        public void Open(GameSession session, IPresentationMemory memory, IOsMotionPreference os, float textScale, Action onClose,
            PanelSettings? panelOverride = null, RenderTexture? target = null)
        {
            _onClose = onClose;
            var camGo = new GameObject("trailhead-camera"); camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true; _cam.orthographicSize = 6.4f; _cam.transform.localPosition = new Vector3(0, 0, -10);
            _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = Canvas; _cam.depth = 10;
            if (target != null) _cam.targetTexture = target;
            var viewGo = new GameObject("trailhead-view"); viewGo.transform.SetParent(transform, false);
            View = viewGo.AddComponent<TrailheadSceneView>(); View.Build();
            View.ReducedMotion = MotionPreference.Effective(session.GetSettings().ReducedMotion, os);
            View.TextScale = textScale;
            Controller = new WorldPresentationController(session, View, View, this, this, memory, View, this) { OsMotion = os };

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
        }

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
            Style(home, Canvas, Charcoal, 18 * Mathf.Max(1f, View.TextScale));
            root.Add(home);
            View.BindDialogue(dialogue, a => { Controller.ChooseDialogueAction(a); }, () => Controller.SkipDialogue());
        }

        /// <summary>A tap at normalized screen coordinates (0..1 from top-left) on a screen of the given aspect (w/h).</summary>
        public TrailheadTarget TapAt(Vector2 screen01, float aspect)
        {
            var art = TrailheadArt.ScreenToArt(screen01.x, screen01.y, aspect);
            var target = TrailheadArt.HitTest(art.x, art.y, View.MiraVisible);
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
            if (_cards == null || _choices == null || _choicesDrawn || View.Dialogue != null) return;
            _cards.Clear(); _choicesDrawn = true; ShownCard = "choices";
            var card = Card();
            foreach (var c in _choices)
            {
                var label = new Label(c.Title); label.style.color = Charcoal; label.style.fontSize = 20 * View.TextScale; label.style.whiteSpace = WhiteSpace.Normal;
                card.Add(label);
                var progress = new Label(PlayerStrings.F("world.goalProgress", c.VitalityInvested, c.VitalityCost)); progress.style.color = Charcoal; progress.style.fontSize = 16 * View.TextScale;
                card.Add(progress);
                if (c.Status == WalkGame.Domain.Projects.ProjectStatus.Available)
                {
                    var id = c.ProjectId;
                    var b = new Button(() => Controller.ChooseRestorationGoal(id)) { text = PlayerStrings.F("label.start", c.Title), name = "start-goal" };
                    Style(b, ForestDeep, Canvas, 18 * View.TextScale); card.Add(b);
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
            var t = new Label(card.ProjectTitle ?? card.LocationTitle); t.style.color = Charcoal; t.style.fontSize = 20 * View.TextScale; t.style.whiteSpace = WhiteSpace.Normal;
            c.Add(t);
            if (card.ProjectId != null)
            {
                var p = new Label(PlayerStrings.F("world.goalProgress", card.VitalityInvested, card.VitalityCost)); p.style.color = Charcoal; p.style.fontSize = 16 * View.TextScale;
                c.Add(p);
            }
            c.Add(CloseButton());
            _cards.Add(c);
        }
        public void HideInspect() { if (ShownCard != null && ShownCard.StartsWith("inspect")) { _cards?.Clear(); ShownCard = null; } }

        public void Play(IReadOnlyList<VisualChangeEvent> events, bool reducedMotion) { View.ReducedMotion = reducedMotion; }

        private Button CloseButton()
        {
            var b = new Button(() => Controller.CloseLocation()) { text = PlayerStrings.T("world.close"), name = "card-close" };
            Style(b, Canvas, Charcoal, 18 * View.TextScale); return b;
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
