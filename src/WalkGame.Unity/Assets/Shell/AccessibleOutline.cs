using System;
using System.Collections.Generic;

namespace WalkGame.Shell
{
    public enum AccessibleRole { Header, StaticText, Image, Button }

    /// <summary>One screen-reader stop: what is announced, in reading order (D-073).</summary>
    public sealed class AccessibleItem
    {
        public AccessibleItem(AccessibleRole role, string label, string? actionId = null, string? hint = null)
        { Role = role; Label = label; ActionId = actionId; Hint = hint; }
        public AccessibleRole Role { get; }
        public string Label { get; }
        public string? ActionId { get; }
        public string? Hint { get; }
    }

    /// <summary>
    /// Engine-free screen-reader outline for a <see cref="ScreenModel"/> (M8 UX/accessibility, D-073).
    /// Reading order mirrors the visual order: title, notice/problem, picture description, text lines, then actions.
    /// The picture is announced by its alt text, never skipped. Every action is a labelled button.
    /// <see cref="ScreenReaderBridge"/> publishes this to the OS screen reader on device (UNVERIFIED, KI-002).
    /// </summary>
    public static class AccessibleOutline
    {
        public const string PrimaryHint = "Main action on this screen.";

        public static List<AccessibleItem> Build(ScreenModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var items = new List<AccessibleItem> { new AccessibleItem(AccessibleRole.Header, model.Title) };
            if (model.Notice != null)
                items.Add(new AccessibleItem(AccessibleRole.StaticText,
                    (model.State == ScreenState.Error ? "Problem: " : "Note: ") + model.Notice));
            if (model.Scene != null) items.Add(new AccessibleItem(AccessibleRole.Image, model.Scene.AltText));
            foreach (var line in model.Lines)
                if (!string.IsNullOrWhiteSpace(line)) items.Add(new AccessibleItem(AccessibleRole.StaticText, line));
            if (!string.IsNullOrWhiteSpace(model.TextPreview)) items.Add(new AccessibleItem(AccessibleRole.StaticText, model.TextPreview!));
            bool first = true;
            foreach (var a in model.Actions)
            {
                items.Add(new AccessibleItem(AccessibleRole.Button, a.Label, a.Id, first && model.Actions.Count > 1 ? PrimaryHint : null));
                first = false;
            }
            return items;
        }
    }
}
