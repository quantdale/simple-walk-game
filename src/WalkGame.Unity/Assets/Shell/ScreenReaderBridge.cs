using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.UIElements;

namespace WalkGame.Shell
{
    /// <summary>
    /// Publishes an <see cref="AccessibleOutline"/> to Unity's OS screen-reader bridge
    /// (TalkBack/VoiceOver via <see cref="AssistiveSupport"/>). The hierarchy and its invoke
    /// wiring are Unity-editor-tested; real TalkBack behaviour is UNVERIFIED (KI-002).
    /// </summary>
    public static class ScreenReaderBridge
    {
        public static AccessibilityHierarchy BuildHierarchy(IReadOnlyList<AccessibleItem> items, Action<string> onAction,
                                                           Func<string, Rect>? frameOf = null)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (onAction == null) throw new ArgumentNullException(nameof(onAction));
            var h = new AccessibilityHierarchy();
            foreach (var item in items)
            {
                var node = h.AddNode(item.Label);
                node.role = RoleOf(item.Role);
                if (item.Hint != null) node.hint = item.Hint;
                if (item.ActionId != null)
                {
                    string id = item.ActionId;
                    node.invoked += () => { onAction(id); return true; };
                    if (frameOf != null) node.frameGetter = () => frameOf(id);
                }
            }
            return h;
        }

        public static AccessibilityRole RoleOf(AccessibleRole role) => role switch
        {
            AccessibleRole.Header => AccessibilityRole.Header,
            AccessibleRole.Image => AccessibilityRole.Image,
            AccessibleRole.Button => AccessibilityRole.Button,
            _ => AccessibilityRole.StaticText,
        };

        /// <summary>Only when a screen reader is on: replaces the active hierarchy for the current screen.</summary>
        public static void Publish(ScreenModel model, VisualElement root, Action<string> onAction)
        {
            if (!AssistiveSupport.isScreenReaderEnabled) return;
            AssistiveSupport.activeHierarchy = BuildHierarchy(AccessibleOutline.Build(model), onAction, id => ScreenFrame(root, id));
        }

        // Panel coordinates → screen points (top-left origin, as the bridge expects); device-UNVERIFIED.
        private static Rect ScreenFrame(VisualElement root, string id)
        {
            var el = root.Q<VisualElement>(id);
            if (el == null || root.panel == null) return Rect.zero;
            var b = el.worldBound;
            float sx = root.worldBound.width > 0 ? Screen.width / root.worldBound.width : 1f;
            float sy = root.worldBound.height > 0 ? Screen.height / root.worldBound.height : 1f;
            return new Rect(b.x * sx, b.y * sy, b.width * sx, b.height * sy);
        }
    }
}
