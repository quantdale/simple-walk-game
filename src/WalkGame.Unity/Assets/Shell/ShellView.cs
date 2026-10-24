using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Shell.Presentation;

namespace WalkGame.Shell
{
    /// <summary>
    /// Renders a <see cref="ScreenModel"/> into UI Toolkit elements. No game logic here.
    /// Styling follows UX_DESIGN §12/§14: calm palette, high-contrast text (≥ 7:1 body), OS large-text scaling (D-073),
    /// ≥ 56 dp touch targets, and state never conveyed by colour alone (error/notice carry
    /// a text prefix and a border). There is no motion at all, so reduced motion is trivially met. The Region picture is drawn
    /// procedurally (<see cref="ValleyElement"/>) and always duplicated by text.
    /// </summary>
    public static class ShellView
    {
        // D-215: direction A palette (STYLE_GUIDE_A §1.1/§9.5), kept at the shell's 7:1 body target (stricter than the guide's AA):
        public static readonly Color Background = new Color32(0xF5, 0xEB, 0xDD, 0xFF); // Canvas parchment
        public static readonly Color Ink = new Color32(0x3D, 0x40, 0x38, 0xFF);        // Charcoal, 8.95:1 on Canvas
        public static readonly Color Muted = new Color32(0x45, 0x48, 0x3F, 0xFF);      // 7.91:1 on Canvas
        public static readonly Color Accent = new Color32(0x2F, 0x4A, 0x36, 0xFF);     // Forest deep, darkened: white text 9.75:1 (guide #3F6248 is 6.88:1)
        public static readonly Color ErrorInk = new Color32(0x8A, 0x1C, 0x1C, 0xFF);   // 7.87:1 on Canvas
        public static readonly Color InkBrown = new Color32(0x5A, 0x3E, 0x2B, 0xFF);   // painted card border (§9.1), 8.25:1 on Canvas
        public static readonly Color Honey = new Color32(0xE5, 0xBA, 0x69, 0xFF);      // ribbon decoration only (charcoal on honey is 5.81:1 < 7)

        /// <summary>D-215: the painted world behind the shell (current trailhead stage plate); null = plain canvas (tests).</summary>
        public static Texture2D Backdrop;
        /// <summary>D-215: share of the screen height left to the painted world above the card when a backdrop is shown.</summary>
        public const float WorldShare = 0.42f;

        private static Font _display, _body, _bodyBold; private static bool _fontsLoaded;
        /// <summary>D-215: Fraunces (display) and Nunito (body/UI), SIL OFL, from Resources/Fonts.</summary>
        public static Font DisplayFont { get { LoadFonts(); return _display; } }
        public static Font BodyFont { get { LoadFonts(); return _body; } }
        public static Font ButtonFont { get { LoadFonts(); return _bodyBold; } }
        private static void LoadFonts()
        {
            if (_fontsLoaded) return; _fontsLoaded = true;
            _display = Resources.Load<Font>("Fonts/Fraunces-SemiBold");
            _body = Resources.Load<Font>("Fonts/Nunito-Medium");
            _bodyBold = Resources.Load<Font>("Fonts/Nunito-ExtraBold");
        }

        public const float TitleSize = 28f, BodySize = 18f, ButtonHeight = 56f;

        public static void Render(VisualElement root, ScreenModel model, System.Action<string> onAction)
            => Render(root, model, onAction, 1f);

        /// <summary>Large text (D-073): OS font scale, clamped so text never shrinks below the base sizes.</summary>
        public const float MinTextScale = 1f, MaxTextScale = 2f;
        public static float ClampTextScale(float s) => float.IsNaN(s) ? 1f : Mathf.Clamp(s, MinTextScale, MaxTextScale);

        /// <summary>
        /// D-136: the title grows half as fast as body text (28 -> 42 at 200%), and always stays
        /// at least 10% larger than body. At 2x the old 56 px title wrapped one word per line and
        /// pushed the Guide's Continue button below the fold on a phone-sized screen. Body text
        /// and touch targets still scale fully (D-073), so the 200% resize requirement holds.
        /// </summary>
        public static float TitleSizeFor(float textScale)
        {
            float k = ClampTextScale(textScale);
            return Mathf.Max(TitleSize * (1f + (k - 1f) * 0.5f), BodySize * k * 1.1f);
        }

        public static void Render(VisualElement root, ScreenModel model, System.Action<string> onAction, float textScale)
        {
            float k = ClampTextScale(textScale);
            // D-074: reuse ONE ScrollView per root. Creating a ScrollView retains ~230 B inside UI
            // Toolkit each time (measured), so rebuilding it per render grew memory linearly with
            // navigation. Only its content is replaced; the scroll position resets per render.
            var scroll = root.Q<ScrollView>("content");
            if (scroll == null || scroll.parent != root)
            {
                root.Clear();
                scroll = new ScrollView(ScrollViewMode.Vertical) { name = "content" };
                scroll.style.flexGrow = 1;
                scroll.contentContainer.style.paddingLeft = 20;
                scroll.contentContainer.style.paddingRight = 20;
                scroll.contentContainer.style.paddingTop = 20;
                scroll.contentContainer.style.paddingBottom = 20;
                // D-215: a parchment card with a hand-ink border (STYLE_GUIDE_A §9.1) over the painted world
                scroll.style.backgroundColor = Background;
                scroll.style.borderTopWidth = scroll.style.borderBottomWidth = scroll.style.borderLeftWidth = scroll.style.borderRightWidth = 3;
                scroll.style.borderTopColor = scroll.style.borderBottomColor = scroll.style.borderLeftColor = scroll.style.borderRightColor = InkBrown;
                scroll.style.borderTopLeftRadius = scroll.style.borderTopRightRadius = scroll.style.borderBottomLeftRadius = scroll.style.borderBottomRightRadius = 16;
                root.Add(scroll);
            }
            // D-221: drop a prior map layer so button Query order rebuilds cleanly with Actions.
            var oldMap = root.Q("region-map");
            if (oldMap != null) oldMap.RemoveFromHierarchy();
            scroll.Clear();
            scroll.scrollOffset = Vector2.zero;
            root.style.flexGrow = 1;
            root.style.backgroundColor = Background;
            // D-215/D-221: painted world dominant — Region uses a larger map share so pins lead.
            bool world = Backdrop != null;
            bool regionMap = world && model.Screen == ShellScreen.Region && model.Scene != null;
            float mapShare = regionMap ? RegionArt.MapShare : WorldShare;
            root.style.backgroundImage = world ? new StyleBackground(Backdrop) : new StyleBackground(StyleKeyword.None);
            root.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            root.style.justifyContent = Justify.FlexEnd;
            scroll.style.flexGrow = world ? 0 : 1;
            scroll.style.maxHeight = world ? new Length((1f - mapShare) * 100f, LengthUnit.Percent) : new StyleLength(StyleKeyword.None);
            scroll.style.marginLeft = scroll.style.marginRight = world ? 12 : 0;
            scroll.style.marginBottom = world ? 16 : 0; scroll.style.marginTop = world ? 12 : 0;

            var title = Text(model.Title, TitleSizeFor(k), Ink);
            title.name = "title";
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            if (DisplayFont != null) { title.style.unityFont = DisplayFont; title.style.unityFontDefinition = FontDefinition.FromFont(DisplayFont); }
            title.style.marginBottom = 12;
            scroll.Add(title);

            if (model.Notice != null)
            {
                bool error = model.State == ScreenState.Error;
                var notice = Text(PlayerStrings.F(error ? "label.problem" : "label.note", model.Notice), BodySize * k, error ? ErrorInk : Ink);
                notice.name = error ? "error" : "notice";
                notice.style.borderLeftWidth = 4;
                notice.style.borderLeftColor = error ? ErrorInk : Accent;
                notice.style.paddingLeft = 12;
                notice.style.marginBottom = 16;
                scroll.Add(notice);
            }

            // D-215: over the painted world the procedural valley strip (pre-A placeholder) is not drawn; its states are in the text lines.
            if (model.Scene != null && !world) scroll.Add(new ValleyElement(model.Scene));

            // Empty state (UX_DESIGN §17): the screen's own explanatory line IS the empty state,
            // at body size; no generic small-print filler.
            // D-088: body-line labels are pooled per root and reused across renders (text, size and
            // name are reset each time), so a screen switch no longer allocates a Label plus its
            // inline style block per line. Measured in ShellPerformanceTests (WG_PERF alloc).
            var pool = LinePool(root);
            bool markedEmpty = false;
            for (int i = 0; i < model.Lines.Count; i++)
            {
                Label l;
                if (i < pool.Count) l = pool[i];
                else
                {
                    l = Text(string.Empty, BodySize * k, Ink);
                    l.style.marginBottom = 10;
                    pool.Add(l);
                }
                l.text = model.Lines[i];
                l.style.fontSize = BodySize * k;
                string name = null;
                if (model.State == ScreenState.Empty && !markedEmpty) { name = "empty-state"; markedEmpty = true; }
                l.name = name;
                scroll.Add(l);
            }

            if (!string.IsNullOrEmpty(model.TextPreview))
            {
                // B3 (D-083): a bordered sample at the effective text scale, so the player sees
                // what their phone's font size does before leaving Settings.
                var preview = Text(model.TextPreview, BodySize * k, Ink);
                preview.name = "text-preview";
                preview.style.borderTopWidth = preview.style.borderBottomWidth = preview.style.borderLeftWidth = preview.style.borderRightWidth = 2;
                preview.style.borderTopColor = preview.style.borderBottomColor = preview.style.borderLeftColor = preview.style.borderRightColor = Accent;
                preview.style.paddingTop = preview.style.paddingBottom = 12;
                preview.style.paddingLeft = preview.style.paddingRight = 12;
                preview.style.marginTop = 8;
                scroll.Add(preview);
            }

            // D-089 (KI-018): the actions container and its buttons are pooled per root like the
            // body lines. A pooled button keeps ONE click handler that reads the action id and
            // callback current for this render, so no closure is allocated per switch; label,
            // name, tooltip, size and primary/secondary styling are reset on every render.
            var state = Pool(root);
            state.OnAction = onAction;
            if (state.Actions == null)
            {
                state.Actions = new VisualElement { name = "actions" };
                state.Actions.style.marginTop = 24;
            }
            var actions = state.Actions;
            scroll.Add(actions);
            actions.Clear();

            // D-221: place actions become map markers (first in Actions → first in button Query); chrome stays in the card.
            int markerCount = 0;
            if (regionMap)
            {
                var map = new VisualElement { name = "region-map", pickingMode = PickingMode.Ignore };
                map.style.position = Position.Absolute;
                map.style.left = 0; map.style.right = 0; map.style.top = 0;
                map.style.height = new Length(mapShare * 100f, LengthUnit.Percent);
                // Insert before the card so Query order = markers then card buttons (= Actions order).
                root.Insert(0, map);
                foreach (var a in model.Actions)
                {
                    if (!RegionArt.IsPlaceAction(a.Id)) continue;
                    string id = RegionArt.PlaceIdOf(a.Id)!;
                    LandmarkVisual pin = null;
                    foreach (var lv in model.Scene!.Landmarks) if (lv.LandmarkId == id) { pin = lv; break; }
                    if (pin == null) continue;
                    ActionButton b = MakeButton(state, markerCount);
                    markerCount++;
                    b.ActionId = a.Id; b.name = a.Id; b.tooltip = a.AccessibleLabel; b.text = a.Label;
                    b.style.fontSize = 11 * k; b.style.unityTextAlign = TextAnchor.LowerCenter;
                    b.style.position = Position.Absolute;
                    b.style.left = new Length(pin.X * 100f, LengthUnit.Percent);
                    b.style.top = new Length(pin.Y * 100f, LengthUnit.Percent);
                    b.style.translate = new Translate(new Length(-50, LengthUnit.Percent), new Length(-50, LengthUnit.Percent));
                    b.style.minWidth = 48 * k; b.style.minHeight = 48 * k;
                    b.style.width = 56 * k; b.style.height = 56 * k;
                    b.style.backgroundColor = new Color(0.961f, 0.922f, 0.867f, 0.82f);
                    b.style.color = Ink;
                    b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 2;
                    b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = InkBrown;
                    b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 28;
                    b.style.marginBottom = 0; b.style.paddingTop = b.style.paddingBottom = b.style.paddingLeft = b.style.paddingRight = 4;
                    b.Clear(); b.Add(new RegionMarkerElement(pin));
                    map.Add(b);
                }
            }

            int cardIndex = 0;
            for (int i = 0; i < model.Actions.Count; i++)
            {
                var a = model.Actions[i];
                if (regionMap && RegionArt.IsPlaceAction(a.Id)) continue; // already on the map
                ActionButton b = MakeButton(state, markerCount + cardIndex);
                cardIndex++;
                bool first = cardIndex == 1 && markerCount == 0;
                b.ActionId = a.Id;
                b.text = a.Label; b.name = a.Id; b.tooltip = a.AccessibleLabel;
                b.style.position = Position.Relative;
                b.style.left = StyleKeyword.Auto; b.style.top = StyleKeyword.Auto;
                b.style.translate = new Translate(0, 0);
                b.style.width = StyleKeyword.Auto; b.style.height = StyleKeyword.Auto;
                b.style.minHeight = ButtonHeight * k;
                b.style.minWidth = ButtonHeight * k;
                b.style.fontSize = BodySize * k;
                b.style.marginBottom = 12;
                b.style.backgroundColor = first ? Accent : Background;
                b.style.color = first ? Color.white : Ink;
                b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 2;
                b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = Accent;
                b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 14;
                b.Clear(); // no marker child
                actions.Add(b);
            }
        }

        private static ActionButton MakeButton(PoolState state, int index)
        {
            ActionButton b;
            if (index < state.Buttons.Count) b = state.Buttons[index];
            else
            {
                b = new ActionButton(state);
                b.style.marginLeft = 0; b.style.marginRight = 0;
                if (ButtonFont != null) { b.style.unityFont = ButtonFont; b.style.unityFontDefinition = FontDefinition.FromFont(ButtonFont); }
                b.style.whiteSpace = WhiteSpace.Normal;
                state.Buttons.Add(b);
            }
            return b;
        }

        private sealed class PoolState
        {
            public readonly System.Collections.Generic.List<Label> Lines = new System.Collections.Generic.List<Label>();
            public readonly System.Collections.Generic.List<ActionButton> Buttons = new System.Collections.Generic.List<ActionButton>();
            public VisualElement Actions;
            public System.Action<string> OnAction;
        }

        /// <summary>A reusable action button: one handler for its lifetime, id set per render.</summary>
        public sealed class ActionButton : Button
        {
            public string ActionId { get; internal set; }
            private readonly PoolState _owner;
            internal ActionButton(object owner)
            {
                _owner = (PoolState)owner;
                clicked += Activate;
            }
            /// <summary>Runs exactly what a tap runs: the current render's callback with the current id.</summary>
            public void Activate() { if (ActionId != null) _owner.OnAction?.Invoke(ActionId); }
        }

        private static PoolState Pool(VisualElement root)
        {
            if (root.userData is PoolState st) return st;
            st = new PoolState();
            root.userData = st;
            return st;
        }

        private static System.Collections.Generic.List<Label> LinePool(VisualElement root) => Pool(root).Lines;
        private static Label Text(string text, float size, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.Normal;
            if (BodyFont != null) { l.style.unityFont = BodyFont; l.style.unityFontDefinition = FontDefinition.FromFont(BodyFont); }
            return l;
        }
    }
}
