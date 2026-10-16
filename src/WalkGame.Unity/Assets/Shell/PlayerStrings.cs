using System;
using System.Collections.Generic;
using System.Globalization;

namespace WalkGame.Shell
{
    /// <summary>
    /// D-120: the single table for player-facing shell strings (localization readiness).
    /// Today it holds English only; a translation replaces the table, not the call sites.
    /// Templates use string.Format placeholders ({0}, {1}) so word order can change per language.
    /// Migration is ratcheted by <c>LocalizationReadinessTests</c>: migrated files may contain no
    /// player-facing literals, and the remaining files may never gain new ones.
    /// </summary>
    public static class PlayerStrings
    {
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Boot outcomes (BootRouter)
            ["boot.recovered"] = "Your last save was damaged; your world was restored from the backup copy.",
            ["boot.unreadable"] = "Your save could not be read. Nothing has been changed or deleted.",
            ["boot.invalid"] = "Your save did not pass its safety check. Nothing has been changed or deleted.",
            ["boot.newer"] = "This save was made by a newer version of the game. Update the game to open it; nothing has been changed.",
            ["boot.existing"] = "A saved game is already on this device; it was not replaced. Try again to load it.",
            // Settings status lines (D-097)
            ["settings.motion.on"] = "Reduced motion is on. Your choice is saved on this device.",
            ["settings.motion.off"] = "Reduced motion is off. You can turn it on at any time.",
            ["settings.sound.on"] = "Sound is on. You can turn it off at any time.",
            ["settings.sound.off"] = "Sound is off. The game stays quiet.",
            ["settings.diagnostics.on"] = "Support details are shown. They are only for looking into a step-count question.",
            ["settings.diagnostics.off"] = "Support details are hidden. You only need them to look into a step-count question.",
            ["settings.textsize.max"] = "Text size: {0}, the largest this game supports. It follows your phone's font size setting.",
            ["settings.textsize"] = "Text size: {0}. It follows your phone's font size setting.",
        };

        /// <summary>The string for key; a missing key returns an empty string (never the raw key).</summary>
        public static string T(string key) => key != null && En.TryGetValue(key, out var v) ? v : string.Empty;

        /// <summary>Formats a template with invariant culture (numbers are pre-formatted by callers).</summary>
        public static string F(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, T(key), args);

        public static IEnumerable<string> Keys => En.Keys;
        public static bool Has(string key) => key != null && En.ContainsKey(key);
    }
}
