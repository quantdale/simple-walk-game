using UnityEngine;
using UnityEngine.UIElements;

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
        public static readonly Color Background = new Color32(0xF4, 0xF1, 0xEA, 0xFF); // warm paper
        public static readonly Color Ink = new Color32(0x1F, 0x2A, 0x24, 0xFF);        // ~13:1 on Background
        public static readonly Color Muted = new Color32(0x44, 0x50, 0x48, 0xFF);      // 7.49:1 on Background (D-095 measured)
        public static readonly Color Accent = new Color32(0x24, 0x5A, 0x41, 0xFF);     // white text 8.03:1 (D-095: was 2F6B4F, 6.29:1, below the 7:1 body target)
        public static readonly Color ErrorInk = new Color32(0x8A, 0x1C, 0x1C, 0xFF);

        public const float TitleSize = 28f, BodySize = 18f, ButtonHeight = 56f;

        public static void Render(VisualElement root, ScreenModel model, System.Action<string> onAction)
            => Render(root, model, onAction, 1f);

        /// <summary>Large text (D-073): OS font scale, clamped so text never shrinks below the base sizes.</summary>
        public const float MinTextScale = 1f, MaxTextScale = 2f;
        public static float ClampTextScale(float s) => float.IsNaN(s) ? 1f : Mathf.Clamp(s, MinTextScale, MaxTextScale);

        public static void Render(VisualElement root, ScreenModel model, System.Action<string> onAction, float textScale)
        {
            float k = ClampTextScale(textScale);
            // D-074: reuse ONE ScrollView per root. Creating a ScrollView retains ~230 B inside UI
            // Toolkit each time (measured), so rebuilding it per render grew memory linearly with
            // navigation. Only its content is replaced; the scroll position resets per render.
            var scroll = root.Q<ScrollView>("content");
            if (scroll == null || scroll.parent != root || root.childCount != 1)
            {
                root.Clear();
                scroll = new ScrollView(ScrollViewMode.Vertical) { name = "content" };
                scroll.style.flexGrow = 1;
                scroll.contentContainer.style.paddingLeft = 24;
                scroll.contentContainer.style.paddingRight = 24;
                scroll.contentContainer.style.paddingTop = 56;
                scroll.contentContainer.style.paddingBottom = 32;
                root.Add(scroll);
            }
            scroll.Clear();
            scroll.scrollOffset = Vector2.zero;
            root.style.flexGrow = 1;
            root.style.backgroundColor = Background;

            var title = Text(model.Title, TitleSize * k, Ink);
            title.name = "title";
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 16;
            scroll.Add(title);

            if (model.Notice != null)
            {
                bool error = model.State == ScreenState.Error;
                var notice = Text((error ? "Problem: " : "Note: ") + model.Notice, BodySize * k, error ? ErrorInk : Ink);
                notice.name = error ? "error" : "notice";
                notice.style.borderLeftWidth = 4;
                notice.style.borderLeftColor = error ? ErrorInk : Accent;
                notice.style.paddingLeft = 12;
                notice.style.marginBottom = 16;
                scroll.Add(notice);
            }

            if (model.Scene != null) scroll.Add(new ValleyElement(model.Scene));

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
            for (int i = 0; i < model.Actions.Count; i++)
            {
                var a = model.Actions[i];
                ActionButton b;
                if (i < state.Buttons.Count) b = state.Buttons[i];
                else
                {
                    b = new ActionButton(state);
                    b.style.marginBottom = 12;
                    b.style.marginLeft = 0; b.style.marginRight = 0;
                    b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 12;
                    b.style.whiteSpace = WhiteSpace.Normal;
                    b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 2;
                    b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = Accent;
                    state.Buttons.Add(b);
                }
                bool first = i == 0;
                b.ActionId = a.Id;
                b.text = a.Label; b.name = a.Id; b.tooltip = a.Label;
                b.style.minHeight = ButtonHeight * k;
                b.style.fontSize = BodySize * k;
                // Primary action is distinguished by fill AND position (first), not colour alone.
                b.style.backgroundColor = first ? Accent : Color.white;
                b.style.color = first ? Color.white : Ink;
                actions.Add(b);
            }
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
            return l;
        }
    }
}
