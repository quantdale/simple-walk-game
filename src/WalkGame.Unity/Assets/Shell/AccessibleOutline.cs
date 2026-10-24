using System;
using System.Collections.Generic;
using WalkGame.Application.ReadModels;

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
    /// D-221: on Region, each map pin\'s full stage phrase is announced (compact card keeps the short title on the button).
    /// </summary>
    public static class AccessibleOutline
    {
        public static string PrimaryHint => PlayerStrings.T("a11y.primaryHint");

        public static List<AccessibleItem> Build(ScreenModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var items = new List<AccessibleItem> { new AccessibleItem(AccessibleRole.Header, model.Title) };
            if (model.Notice != null)
                items.Add(new AccessibleItem(AccessibleRole.StaticText,
                    PlayerStrings.F(model.State == ScreenState.Error ? "label.problem" : "label.note", model.Notice)));
            if (model.Scene != null) items.Add(new AccessibleItem(AccessibleRole.Image, model.Scene.AltText));
            foreach (var line in model.Lines)
                if (!string.IsNullOrWhiteSpace(line)) items.Add(new AccessibleItem(AccessibleRole.StaticText, line));
            // D-221: full landmark phrases for TalkBack (visible Region card stays compact).
            if (model.Screen == ShellScreen.Region && model.Scene != null)
                foreach (var lv in model.Scene.Landmarks)
                    items.Add(new AccessibleItem(AccessibleRole.StaticText, GlancePhrases.FormatLandmark(lv.Label, lv.Stage)));
            if (!string.IsNullOrWhiteSpace(model.TextPreview)) items.Add(new AccessibleItem(AccessibleRole.StaticText, model.TextPreview!));
            bool first = true;
            foreach (var a in model.Actions)
            {
                string? hint = first && model.Actions.Count > 1 ? PrimaryHint : null;
                if (WalkGame.Shell.Presentation.RegionArt.IsPlaceAction(a.Id) && model.Scene != null)
                {
                    string id = WalkGame.Shell.Presentation.RegionArt.PlaceIdOf(a.Id)!;
                    foreach (var lv in model.Scene.Landmarks)
                        if (lv.LandmarkId == id) { hint = GlancePhrases.FormatLandmark(lv.Label, lv.Stage); break; }
                }
                items.Add(new AccessibleItem(AccessibleRole.Button, a.AccessibleLabel, a.Id, hint));
                first = false;
            }
            return items;
        }
    }
}
