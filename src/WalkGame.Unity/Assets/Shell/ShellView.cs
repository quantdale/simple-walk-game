using UnityEngine;
using UnityEngine.UIElements;

namespace WalkGame.Shell
{
    /// <summary>
    /// Renders a <see cref="ScreenModel"/> into UI Toolkit elements. No game logic here.
    /// Styling follows UX_DESIGN §12/§14: calm palette, high-contrast text (≥ 7:1 body),
    /// ≥ 56 dp touch targets, and state never conveyed by colour alone (error/notice carry
    /// a text prefix and a border). There is no motion at all, so reduced motion is trivially met.
    /// </summary>
    public static class ShellView
    {
        public static readonly Color Background = new Color32(0xF4, 0xF1, 0xEA, 0xFF); // warm paper
        public static readonly Color Ink = new Color32(0x1F, 0x2A, 0x24, 0xFF);        // ~13:1 on Background
        public static readonly Color Muted = new Color32(0x44, 0x50, 0x48, 0xFF);      // ~7.9:1 on Background
        public static readonly Color Accent = new Color32(0x2F, 0x6B, 0x4F, 0xFF);     // white text ~6.3:1
        public static readonly Color ErrorInk = new Color32(0x8A, 0x1C, 0x1C, 0xFF);

        public const float TitleSize = 28f, BodySize = 18f, ButtonHeight = 56f;

        public static void Render(VisualElement root, ScreenModel model, System.Action<string> onAction)
        {
            root.Clear();
            root.style.flexGrow = 1;
            root.style.backgroundColor = Background;

            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "content" };
            scroll.style.flexGrow = 1;
            scroll.contentContainer.style.paddingLeft = 24;
            scroll.contentContainer.style.paddingRight = 24;
            scroll.contentContainer.style.paddingTop = 56;
            scroll.contentContainer.style.paddingBottom = 32;
            root.Add(scroll);

            var title = Text(model.Title, TitleSize, Ink);
            title.name = "title";
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 16;
            scroll.Add(title);

            if (model.Notice != null)
            {
                bool error = model.State == ScreenState.Error;
                var notice = Text((error ? "Problem: " : "Note: ") + model.Notice, BodySize, error ? ErrorInk : Ink);
                notice.name = error ? "error" : "notice";
                notice.style.borderLeftWidth = 4;
                notice.style.borderLeftColor = error ? ErrorInk : Accent;
                notice.style.paddingLeft = 12;
                notice.style.marginBottom = 16;
                scroll.Add(notice);
            }

            if (model.State == ScreenState.Empty)
            {
                var empty = Text("Nothing here yet", 14f, Muted);
                empty.name = "empty-state";
                scroll.Add(empty);
            }

            foreach (var line in model.Lines)
            {
                var l = Text(line, BodySize, Ink);
                l.style.marginBottom = 10;
                scroll.Add(l);
            }

            var actions = new VisualElement { name = "actions" };
            actions.style.marginTop = 24;
            scroll.Add(actions);
            bool first = true;
            foreach (var a in model.Actions)
            {
                var id = a.Id;
                var b = new Button(() => onAction(id)) { text = a.Label, name = id, tooltip = a.Label };
                b.style.minHeight = ButtonHeight;
                b.style.fontSize = BodySize;
                b.style.marginBottom = 12;
                b.style.marginLeft = 0; b.style.marginRight = 0;
                b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 12;
                b.style.whiteSpace = WhiteSpace.Normal;
                // Primary action is distinguished by fill AND position (first), not colour alone.
                b.style.backgroundColor = first ? Accent : Color.white;
                b.style.color = first ? Color.white : Ink;
                b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 2;
                b.style.borderTopColor = b.style.borderBottomColor = b.style.borderLeftColor = b.style.borderRightColor = Accent;
                actions.Add(b);
                first = false;
            }
        }

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
