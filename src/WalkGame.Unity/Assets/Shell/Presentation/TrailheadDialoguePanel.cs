using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-204: Mira's dialogue panel (bible §15–§16): portrait bust, name ribbon, the current line, the
    /// place actions and a Skip button. Text is drawn by UI Toolkit from <see cref="PlayerStrings"/> and
    /// content, never baked into art. Colours are the direction-A palette (canvas panel, charcoal text,
    /// Forest-deep primary, Clay-deep secondary; both pass contrast with canvas text per style guide).
    /// </summary>
    public static class TrailheadDialoguePanel
    {
        private static readonly Color Canvas = Hex(0xF5EBDD), Charcoal = Hex(0x3D4038), ForestDeep = Hex(0x3F6248), ClayDeep = Hex(0x8E5240), Ink = Hex(0x5A3E2B), Honey = Hex(0xE5BA69);
        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

        public static VisualElement Build(DialogueBeat beat, IReadOnlyList<DialogueAction> actions, Texture2D? portrait,
            Action<DialogueAction> onAction, Action onSkip)
        {
            var root = new VisualElement { name = "trailhead-dialogue" };
            root.style.position = Position.Absolute; root.style.left = 16; root.style.right = 16; root.style.bottom = 24;
            root.style.backgroundColor = Canvas; Border(root, Ink, 3, 18);
            root.style.paddingLeft = 14; root.style.paddingRight = 14; root.style.paddingTop = 12; root.style.paddingBottom = 12;

            var top = new VisualElement(); top.style.flexDirection = FlexDirection.Row; top.style.alignItems = Align.FlexStart;
            if (portrait != null)
            {
                var img = new Image { image = portrait, scaleMode = ScaleMode.ScaleAndCrop, name = "portrait" };
                img.style.width = 132; img.style.height = 114; img.style.marginRight = 12; img.style.backgroundColor = Hex(0xE9DCC6);
                Border(img, Ink, 2, 12);
                top.Add(img);
            }
            var col = new VisualElement(); col.style.flexGrow = 1; col.style.flexShrink = 1;
            var name = new Label(beat.Name) { name = "speaker" };
            name.style.backgroundColor = Honey; name.style.color = Charcoal; name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.fontSize = 20; name.style.alignSelf = Align.FlexStart; name.style.paddingLeft = 10; name.style.paddingRight = 10;
            name.style.paddingTop = 2; name.style.paddingBottom = 2; Border(name, Ink, 2, 10);
            var line = new Label(beat.Text) { name = "line" };
            line.style.whiteSpace = WhiteSpace.Normal; line.style.color = Charcoal; line.style.fontSize = 19; line.style.marginTop = 8;
            col.Add(name); col.Add(line); top.Add(col);
            root.Add(top);

            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; row.style.marginTop = 12;
            foreach (var a in actions)
            {
                var act = a;
                bool primary = a.Kind == DialogueActionKind.HelpRestore;
                row.Add(Button(PlayerStrings.T(a.LabelKey), primary ? ForestDeep : a.Kind == DialogueActionKind.Inspect ? ClayDeep : Canvas,
                    primary || a.Kind == DialogueActionKind.Inspect ? Canvas : Charcoal, () => onAction(act)));
            }
            var skip = Button(PlayerStrings.T("dialogue.action.skip"), Canvas, Charcoal, onSkip);
            skip.name = "skip"; skip.style.marginLeft = StyleKeyword.Auto;
            row.Add(skip);
            root.Add(row);
            return root;
        }

        private static Button Button(string text, Color bg, Color fg, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.backgroundColor = bg; b.style.color = fg; b.style.fontSize = 18; b.style.minHeight = 48; // ≥ 48 px tap target
            b.style.paddingLeft = 14; b.style.paddingRight = 14; b.style.marginRight = 8; b.style.marginTop = 4;
            Border(b, Ink, 2, 14);
            return b;
        }

        private static void Border(VisualElement e, Color c, float w, float r)
        {
            e.style.borderTopColor = c; e.style.borderBottomColor = c; e.style.borderLeftColor = c; e.style.borderRightColor = c;
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w; e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r; e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }
    }
}
