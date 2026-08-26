using System;
using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Domain.Common;
using WalkGame.UnityShell.Screens;

namespace WalkGame.UnityShell.Shell
{
    [RequireComponent(typeof(AppHost))]
    public sealed class AppShell : MonoBehaviour
    {
        public static bool ReducedMotion { get; private set; }

        private readonly ScreenCoordinator _navigator = new ScreenCoordinator();
        private VisualElement _root = null!;
        private Label _headerTitle = null!;
        private Label _headerResources = null!;
        private VisualElement _feedbackSlot = null!;
        private VisualElement _bannerSlot = null!;
        private VisualElement _screenSlot = null!;
        private VisualElement _overlaySlot = null!;
        private VisualElement _navBar = null!;
        private DomainResult? _lastFeedback;

        public event Action? LayoutRebuilt;

        private void OnEnable()
        {
            var host = GetComponent<AppHost>();
            host.StateChanged += OnStateChanged;
            _navigator.CurrentChanged += OnStateChanged;
            BuildLayout(host);
            ApplyMotionPreference();
            OnStateChanged();
        }

        private void OnDisable()
        {
            var host = GetComponent<AppHost>();
            if (host != null)
                host.StateChanged -= OnStateChanged;
            _navigator.CurrentChanged -= OnStateChanged;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                _navigator.NavigateBack();
        }

        private void BuildLayout(AppHost host)
        {
            var document = GetComponent<UIDocument>();
            if (document == null || document.rootVisualElement == null)
                return;

            _root = document.rootVisualElement;
            _root.Clear();
            _root.style.backgroundColor = Ui.Background;
            _root.style.paddingTop = 12;
            _root.style.paddingBottom = 8;
            _root.style.paddingLeft = 12;
            _root.style.paddingRight = 12;

            var header = Ui.Column(2);
            _headerTitle = new Label(string.Empty);
            _headerTitle.style.fontSize = 20;
            _headerTitle.style.color = Ui.TextMain;
            _headerTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _headerResources = new Label(string.Empty);
            _headerResources.style.fontSize = 13;
            _headerResources.style.color = Ui.TextMuted;
            header.Add(_headerTitle);
            header.Add(_headerResources);
            _root.Add(header);

            _bannerSlot = Ui.Column(6);
            _root.Add(_bannerSlot);

            _feedbackSlot = Ui.Column(6);
            _root.Add(_feedbackSlot);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            _screenSlot = scroll.contentContainer;
            _root.Add(scroll);

            _navBar = new VisualElement();
            _navBar.style.flexDirection = FlexDirection.Row;
            _navBar.style.justifyContent = Justify.FlexStart;
            _root.Add(_navBar);
            AddNavButton("Home", ScreenId.Home);
            AddNavButton("Projects", ScreenId.Projects);
            AddNavButton("Region", ScreenId.Region);
            AddNavButton("Journal", ScreenId.Journal);
            AddNavButton("Routes", ScreenId.Expeditions);
            AddNavButton("Settings", ScreenId.Settings);
            AddNavButton("Diagnostics", ScreenId.Diagnostics);

            _overlaySlot = new VisualElement();
            _overlaySlot.style.position = Position.Absolute;
            _overlaySlot.style.top = 0;
            _overlaySlot.style.bottom = 0;
            _overlaySlot.style.left = 0;
            _overlaySlot.style.right = 0;
            _overlaySlot.style.backgroundColor = new Color(0f, 0f, 0f, 0.65f);
            _overlaySlot.style.display = DisplayStyle.None;
            _overlaySlot.pickingMode = PickingMode.Position;
            _root.Add(_overlaySlot);

            LayoutRebuilt?.Invoke();
        }

        private void AddNavButton(string text, ScreenId id)
        {
            var b = Ui.GhostButton(text, () => _navigator.Show(id));
            b.name = "nav." + id;
            b.style.height = 38;
            b.style.fontSize = 13;
            b.style.marginRight = 4;
            b.style.flexGrow = 0;
            _navBar.Add(b);
        }

        private void OnStateChanged()
        {
            if (_root == null)
                return;
            RenderFeedback();
            RenderScreen();
            RenderOverlay();
        }

        public void Refresh()
        {
            OnStateChanged();
        }

        public void NavigateTo(ScreenId id)
        {
            _navigator.ResetTo(id);
        }

        public void ShowReturnSummary()
        {
            RefreshWithOverlay(ReturnSummaryOverlay.Build(this));
        }

        internal void RefreshWithOverlay(VisualElement overlay)
        {
            _pendingOverlay = overlay;
            Refresh();
        }

        private VisualElement? _pendingOverlay;

        private void RenderScreen()
        {
            var host = AppHost.Instance;
            if (host == null)
                return;

            _screenSlot.Clear();

            switch (host.Phase)
            {
                case BootPhase.Booting:
                    _screenSlot.Add(Ui.Muted("Loading…"));
                    break;

                case BootPhase.NeedsNewGame:
                    HideNavBar();
                    _screenSlot.Add(OnboardingScreen.Build(this));
                    break;

                case BootPhase.Unrecoverable:
                    HideNavBar();
                    _screenSlot.Add(UnrecoverableScreen.Build(this));
                    break;

                default:
                    ShowNavBar();
                    switch (_navigator.Current)
                    {
                        case ScreenId.Home: _screenSlot.Add(HomeScreen.Build(this)); break;
                        case ScreenId.Projects: _screenSlot.Add(ProjectsScreen.Build(this)); break;
                        case ScreenId.Region: _screenSlot.Add(RegionScreen.Build(this)); break;
                        case ScreenId.Journal: _screenSlot.Add(JournalScreen.Build(this)); break;
                        case ScreenId.Expeditions: _screenSlot.Add(ExpeditionsScreen.Build(this)); break;
                        case ScreenId.Settings: _screenSlot.Add(SettingsScreen.Build(this)); break;
                        case ScreenId.Diagnostics: _screenSlot.Add(DiagnosticsScreen.Build(this)); break;
                    }
                    break;
            }

            UpdateHeader(host);
        }

        private void UpdateHeader(AppHost host)
        {
            var session = host.Graph.Session;
            if (!session.HasLoadedState)
            {
                _headerTitle.text = "Walk Game";
                _headerResources.text = string.Empty;
                return;
            }

            var home = session.GetHome();
            _headerTitle.text = CopyTable.Text(home.RegionTitleKey);
            _headerResources.text =
                "+" + home.Vitality.ToString("N0") + " vitality   " +
                "+" + home.Materials.ToString("N0") + " materials   " +
                "+" + home.Knowledge.ToString("N0") + " knowledge";
        }

        private void RenderFeedback()
        {
            _feedbackSlot.Clear();
            if (_lastFeedback == null)
                return;
            var result = _lastFeedback.Value;
            string text = result.IsSuccess ? "Done." : result.Error!.Code + ": " + result.Error.Message;
            _feedbackSlot.Add(Ui.StatusLine(text, result.IsSuccess ? Ui.Accent : Ui.Warn));
        }

        public void ShowFeedback(DomainResult result)
        {
            _lastFeedback = result;
        }

        private void RenderOverlay()
        {
            var host = AppHost.Instance;
            VisualElement? overlay = _pendingOverlay;

            if (overlay == null && host != null && host.Phase is BootPhase.Ready or BootPhase.RecoveredFromBackup)
            {
                var session = host.Graph.Session;
                var pending = session.GetPendingReturnSummary();
                if (pending != null && pending.HasMeaningfulChange && !_summaryDismissedThisSession)
                    overlay = ReturnSummaryOverlay.Build(this);
            }

            _overlaySlot.Clear();
            if (overlay == null)
            {
                _overlaySlot.style.display = DisplayStyle.None;
                return;
            }

            _overlaySlot.style.display = DisplayStyle.Flex;
            var centered = new VisualElement();
            centered.style.flexGrow = 1;
            centered.style.alignItems = Align.Center;
            centered.style.justifyContent = Justify.Center;
            centered.style.paddingLeft = 16;
            centered.style.paddingRight = 16;
            var card = Ui.Card();
            card.style.backgroundColor = Ui.SurfaceAlt;
            card.style.maxHeight = Length.Percent(85);
            card.Add(overlay);
            centered.Add(card);
            _overlaySlot.Add(centered);
        }

        private bool _summaryDismissedThisSession;

        internal void CloseSummary(bool acknowledged)
        {
            if (acknowledged)
                AppHost.Instance.Graph.Session.AcknowledgeReturnSummary();
            _summaryDismissedThisSession = true;
            _pendingOverlay = null;
            Refresh();
        }

        public void ApplyMotionPreference()
        {
            ReducedMotion = Composition.AppSettings.GetBool(Composition.AppPreference.ReducedMotion, fallback: false);
        }

        private void HideNavBar()
        {
            _navBar.style.display = DisplayStyle.None;
        }

        private void ShowNavBar()
        {
            _navBar.style.display = DisplayStyle.Flex;
        }
    }
}
