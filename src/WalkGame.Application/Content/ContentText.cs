using System;
using System.Collections.Generic;

namespace WalkGame.Application.Content
{
    /// <summary>
    /// D-122: presentation-only localization overlay for application/content copy (titles,
    /// chapters, letters, discovery copy). The authored English stays exactly where it is, as the
    /// canonical source: catalog fields, content versions, the content-compatibility gate and saves
    /// never see a translation. A locale is a flat table keyed by a STABLE content id plus a field
    /// ("letter.trailhead#body"); a missing entry falls back to the authored English, so a partial
    /// translation can never show a raw key or an empty line.
    /// </summary>
    public sealed class ContentText
    {
        private readonly IReadOnlyDictionary<string, string>? _overlay;

        private ContentText(string languageTag, IReadOnlyDictionary<string, string>? overlay)
        { LanguageTag = languageTag; _overlay = overlay; }

        /// <summary>The authored English source (no overlay). Byte-identical to pre-D-122 output.</summary>
        public static readonly ContentText English = new ContentText("en", null);

        /// <summary>A locale overlay. Blank values are ignored (they fall back to English).</summary>
        public static ContentText ForLocale(string languageTag, IReadOnlyDictionary<string, string> overlay)
        {
            if (string.IsNullOrWhiteSpace(languageTag)) throw new ArgumentException("language tag required", nameof(languageTag));
            if (overlay == null) throw new ArgumentNullException(nameof(overlay));
            return new ContentText(languageTag, new Dictionary<string, string>(overlay, StringComparer.Ordinal));
        }

        public string LanguageTag { get; }

        /// <summary>The overlay key for a stable content id and field.</summary>
        public static string Key(string stableId, string field) => stableId + "#" + field;

        /// <summary>The localized text for (stableId, field), or the authored English.</summary>
        public string Resolve(string stableId, string field, string authoredEnglish)
        {
            if (_overlay != null && _overlay.TryGetValue(Key(stableId, field), out var t) && !string.IsNullOrWhiteSpace(t)) return t;
            return authoredEnglish;
        }

        /// <summary>Field names (D-122): letters, then titles, chapters and discovery copy.</summary>
        public static class Fields
        {
            public const string Body = "body";
            public const string Place = "place";
            public const string Title = "title";
            public const string Hint = "hint";
            public const string Provenance = "provenance";
            /// <summary>D-141: expedition and post-completion care descriptions.</summary>
            public const string Description = "description";
            /// <summary>D-162: a project's display-time completion sentence (Region1SummaryCopy).</summary>
            public const string Completed = "completed";
            /// <summary>D-165: a character's dialogue line (Region1Characters).</summary>
            public const string Line = "line";
            /// <summary>D-165: a character's display name.</summary>
            public const string Name = "name";
        }
    }
}
