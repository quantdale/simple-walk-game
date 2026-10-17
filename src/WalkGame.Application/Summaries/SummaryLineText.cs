using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using WalkGame.Application.Content;
using WalkGame.Application.ReadModels;
using WalkGame.Domain.Regions;

namespace WalkGame.Application.Summaries
{
    /// <summary>
    /// D-138 (KI-023, D-122): stored return-summary lines are display-localized at READ time.
    /// The save keeps the authored English sentence exactly as composed (no schema, content-hash or
    /// save change). At presentation, a line is matched against the closed set of composer
    /// sentence forms; a match is re-rendered from the overlay pattern ("summary.&lt;name&gt;#text",
    /// positional {0}/{1}) with content titles mapped back to their stable id and localized. Any
    /// line that matches no form, any missing pattern and any broken pattern falls back to the
    /// stored English, so a partial translation never shows a raw key and English is byte-identical.
    /// </summary>
    public sealed class SummaryLineText
    {
        public const string Field = "text";
        public static string PatternId(string name) => "summary." + name;

        private readonly ContentText _text;
        private readonly Dictionary<string, string> _titleToId = new Dictionary<string, string>(StringComparer.Ordinal);

        // Ordered: more specific forms first ("X's restoration is complete." before "X is complete.").
        // Group "t" is a content title (re-localized); group "n" is a number kept verbatim.
        private static readonly (string Name, Regex Re, string English)[] Forms =
        {
            ("restorations-complete", R(@"^(?<n>[\d,]+) restoration projects are complete\.$"), "{0} restoration projects are complete."),
            ("more-ready", R(@"^(?<n>[\d,]+) more projects became ready to queue\.$"), "{0} more projects became ready to queue."),
            ("producers-delivered", R(@"^Producers delivered (?<n>[\d,]+) units\.$"), "Producers delivered {0} units."),
            ("vitality-credited", R(@"^Vitality credited: (?<n>-?[\d,]+)\.$"), "Vitality credited: {0}."),
            ("duplicates-ignored", R(@"^(?<n>[\d,]+) duplicate activity report\(s\) were safely ignored\.$"), "{0} duplicate activity report(s) were safely ignored."),
            ("ecology-stage", R(@"^The valley's ecology has reached stage (?<n>\d+)\.$"), "The valley's ecology has reached stage {0}."),
            ("settlement-stage", R(@"^The settlement has reached stage (?<n>\d+)\.$"), "The settlement has reached stage {0}."),
            ("new-chapter", R(@"^New chapter: (?<t>.+)\.$"), "New chapter: {0}."),
            ("discovery-recorded", R(@"^Discovery recorded: (?<t>.+)\.$"), "Discovery recorded: {0}."),
            ("region-complete", R(@"^(?<t>.+)'s restoration is complete\.$"), "{0}'s restoration is complete."),
            ("producer-capped", R(@"^(?<t>.+) is at its storage cap; the surplus is waiting and returns on its own\. Nothing was lost\.$"), "{0} is at its storage cap; the surplus is waiting and returns on its own. Nothing was lost."),
            ("producer-store-filled", R(@"^(?<t>.+) storage filled; surplus time created no waste\.$"), "{0} storage filled; surplus time created no waste."),
            ("project-complete", R(@"^(?<t>.+) is complete\.$"), "{0} is complete."),
            ("project-ready", R(@"^(?<t>.+) is ready to queue\.$"), "{0} is ready to queue."),
            ("producer-operating", R(@"^(?<t>.+) is now operating\.$"), "{0} is now operating."),
            ("expedition-ready", R(@"^(?<t>.+) route is ready\.$"), "{0} route is ready."),
            ("expedition-complete", R(@"^(?<t>.+) expedition complete\.$"), "{0} expedition complete."),
            ("next-action-queue", R(@"^Queue the next restoration project\.$"), "Queue the next restoration project."),
        };

        private static Regex R(string p) => new Regex(p, RegexOptions.CultureInvariant);

        public SummaryLineText(ContentText text, RegionDefinition content)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            if (content == null) throw new ArgumentNullException(nameof(content));
            Add(content.Id.Value, content.TitleKey);
            foreach (var p in content.Projects) Add(p.Id.Value, p.TitleKey);
            foreach (var l in content.Landmarks) Add(l.Id.Value, l.TitleKey);
            foreach (var p in content.Producers) Add(p.Id.Value, p.TitleKey);
            foreach (var d in content.Discoveries) Add(d.Id.Value, d.TitleKey);
            foreach (var e in content.Expeditions) Add(e.Id.Value, e.TitleKey);
            foreach (var c in content.Chapters) Add(c.Id, c.TitleKey);
        }

        private void Add(string id, string title)
        {
            // An English title shared by two ids is ambiguous; it then stays English.
            if (string.IsNullOrWhiteSpace(title)) return;
            if (_titleToId.ContainsKey(title)) _titleToId[title] = string.Empty; else _titleToId[title] = id;
        }

        /// <summary>The display form of one stored line; the stored English when no form applies.</summary>
        public string Localize(string? stored)
        {
            if (string.IsNullOrEmpty(stored) || ReferenceEquals(_text, ContentText.English)) return stored ?? string.Empty;
            string line = stored!;

            // Landmark lines share the glance phrase form (D-065) and its overlay (glance.*).
            foreach (RestorationStage stage in Enum.GetValues(typeof(RestorationStage)))
            {
                string suffix = " " + GlancePhrases.PhraseFor(stage) + ".";
                if (line.EndsWith(suffix, StringComparison.Ordinal) && line.Length > suffix.Length)
                {
                    string title = line.Substring(0, line.Length - suffix.Length);
                    if (_titleToId.TryGetValue(title, out var lid) && lid.Length > 0)
                        return GlancePhrases.FormatLandmark(LocalTitle(title), stage, _text);
                }
            }

            // D-140 authored progression sentences: whole-sentence overlay entries.
            for (int i = 0; i < ReturnSummaryComposer.EcologyLines.Count; i++)
                if (line == ReturnSummaryComposer.EcologyLines[i]) return _text.Resolve(PatternId("ecology." + (i + 1)), Field, line);
            for (int i = 0; i < ReturnSummaryComposer.SettlementLines.Count; i++)
                if (line == ReturnSummaryComposer.SettlementLines[i]) return _text.Resolve(PatternId("settlement." + (i + 1)), Field, line);

            foreach (var (name, re, english) in Forms)
            {
                var m = re.Match(line);
                if (!m.Success) continue;
                string raw = m.Groups["t"].Success ? m.Groups["t"].Value : m.Groups["n"].Success ? m.Groups["n"].Value : string.Empty;
                string arg = m.Groups["t"].Success ? LocalTitle(raw) : raw;
                string pattern = _text.Resolve(PatternId(name), Field, english);
                if (ReferenceEquals(pattern, english) && arg == raw) return line; // nothing translated: stored bytes
                try { return string.Format(CultureInfo.InvariantCulture, pattern, arg); }
                catch (FormatException) { return string.Format(CultureInfo.InvariantCulture, english, arg); }
            }
            return line;
        }

        private string LocalTitle(string englishTitle) =>
            _titleToId.TryGetValue(englishTitle, out var id) && id.Length > 0
                ? _text.Resolve(id, ContentText.Fields.Title, englishTitle)
                : englishTitle;
    }
}
