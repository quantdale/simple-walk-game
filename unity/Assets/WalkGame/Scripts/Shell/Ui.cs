using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WalkGame.UnityShell.Shell
{
    public static class Ui
    {
        public static readonly Color Background = new Color(0x10 / 255f, 0x14 / 255f, 0x1a / 255f);
        public static readonly Color Surface = new Color(0x1a / 255f, 0x21 / 255f, 0x2b / 255f);
        public static readonly Color SurfaceAlt = new Color(0x22 / 255f, 0x2b / 255f, 0x37 / 255f);
        public static readonly Color TextMain = new Color(0xe8 / 255f, 0xed / 255f, 0xf2 / 255f);
        public static readonly Color TextMuted = new Color(0x9a / 255f, 0xa7 / 255f, 0xb4 / 255f);
        public static readonly Color Accent = new Color(0x3f / 255f, 0xae / 255f, 0x6a / 255f);
        public static readonly Color Warn = new Color(0xd9 / 255f, 0xa4 / 255f, 0x2a / 255f);
        public static readonly Color Danger = new Color(0xc8 / 255f, 0x4b / 255f, 0x4b / 255f);

        public static VisualElement Root(PanelSettings settings)
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.backgroundColor = Background;
            return root;
        }

        public static VisualElement Column(float spacing = 8)
        {
            var el = new VisualElement();
            el.style.flexDirection = FlexDirection.Column;
            el.style.marginBottom = spacing;
            return el;
        }

        public static VisualElement Row(float spacing = 8)
        {
            var el = new VisualElement();
            el.style.flexDirection = FlexDirection.Row;
            el.style.marginBottom = spacing;
            el.style.justifyContent = Justify.SpaceBetween;
            return el;
        }

        public static VisualElement Card()
        {
            var el = new VisualElement();
            el.style.backgroundColor = Surface;
            el.style.borderRadius = 8;
            el.style.paddingTop = 10;
            el.style.paddingBottom = 10;
            el.style.paddingLeft = 12;
            el.style.paddingRight = 12;
            el.style.marginBottom = 8;
            return el;
        }

        public static Label Title(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 22;
            l.style.color = TextMain;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginBottom = 4;
            return l;
        }

        public static Label SectionHeader(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 16;
            l.style.color = TextMain;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginTop = 6;
            l.style.marginBottom = 4;
            return l;
        }

        public static Label Body(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 15;
            l.style.color = TextMain;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        public static Label Muted(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 13;
            l.style.color = TextMuted;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        public static Label StatusLine(string text, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = 14;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        public static Button PrimaryButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            StyleButton(b, Accent, TextMain);
            return b;
        }

        public static Button GhostButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            StyleButton(b, SurfaceAlt, TextMain);
            return b;
        }

        public static Button DangerButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            StyleButton(b, Danger, TextMain);
            return b;
        }

        private static void StyleButton(Button b, Color background, Color foreground)
        {
            b.style.height = 44;
            b.style.backgroundColor = background;
            b.style.color = foreground;
            b.style.borderRadius = 8;
            b.style.borderWidth = 0;
            b.style.marginTop = 4;
            b.style.fontSize = 15;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
        }

        public static VisualElement KeyValueRow(string key, string value)
        {
            var row = Row(4);
            var k = new Label(key);
            k.style.fontSize = 14;
            k.style.color = TextMuted;
            k.style.flexShrink = 0;
            var v = new Label(value);
            v.style.fontSize = 14;
            v.style.color = TextMain;
            v.style.whiteSpace = WhiteSpace.Normal;
            v.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(k);
            row.Add(v);
            return row;
        }

        public static VisualElement ProgressBar(float fraction, out Label caption)
        {
            var wrap = Column(2);
            var track = new VisualElement();
            track.style.height = 8;
            track.style.backgroundColor = SurfaceAlt;
            track.style.borderRadius = 4;
            track.style.marginBottom = 2;
            var fill = new VisualElement();
            fill.style.height = 8;
            fill.style.backgroundColor = Accent;
            fill.style.borderRadius = 4;
            float pct = Mathf.Clamp(fraction, 0f, 1f) * 100f;
            fill.style.width = Length.Percent(pct);
            track.Add(fill);
            caption = Muted(Mathf.RoundToInt(pct) + "%");
            wrap.Add(track);
            wrap.Add(caption);
            return wrap;
        }

        public static Button ToggleButton(string text, bool isOn, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            StyleButton(b, isOn ? Accent : SurfaceAlt, TextMain);
            return b;
        }

        public static VisualElement Banner(string text, Color color)
        {
            var b = Card();
            b.style.backgroundColor = color;
            b.style.opacity = 0.95f;
            b.Add(Body(text));
            return b;
        }
    }
}
