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
        private readonly HashSet<string> _projectIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _expeditionIds = new HashSet<string>(StringComparer.Ordinal);

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

        public const string ExpeditionRewardName = "expedition-complete-reward";
        private const string ExpeditionRewardEnglish = "{0} expedition complete (+{1} {2}).";
        private static readonly Regex ExpeditionReward =
            R(@"^(?<t>.+) expedition complete \(\+(?<n>[\d,]+) (?<r>[A-Za-z]+)\)\.$");

        /// <summary>D-141: overlay id for a resource name shown in a summary line ("resource.Materials#title").</summary>
        public static string ResourceId(string resourceType) => "resource." + resourceType;

        private static Regex R(string p) => new Regex(p, RegexOptions.CultureInvariant);

        /// <summary>The overlay this instance renders with.</summary>
        public ContentText Locale => _text;

        public SummaryLineText(ContentText text, RegionDefinition content)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            if (content == null) throw new ArgumentNullException(nameof(content));
            Add(content.Id.Value, content.TitleKey);
            foreach (var p in content.Projects) { Add(p.Id.Value, p.TitleKey); _projectIds.Add(p.Id.Value); }
            foreach (var l in content.Landmarks) Add(l.Id.Value, l.TitleKey);
            foreach (var p in content.Producers) Add(p.Id.Value, p.TitleKey);
            foreach (var d in content.Discoveries) Add(d.Id.Value, d.TitleKey);
            foreach (var e in content.Expeditions) { Add(e.Id.Value, e.TitleKey); _expeditionIds.Add(e.Id.Value); }
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

            // D-141: the reward-suffixed expedition line carries three arguments (title, units, resource).
            var em = ExpeditionReward.Match(line);
            if (em.Success)
            {
                string rawTitle = em.Groups["t"].Value, units = em.Groups["n"].Value, rawRes = em.Groups["r"].Value;
                string title = LocalTitle(rawTitle);
                string res = _text.Resolve(ResourceId(rawRes), ContentText.Fields.Title, rawRes);
                string pattern = _text.Resolve(PatternId(ExpeditionRewardName), Field, ExpeditionRewardEnglish);
                if (ReferenceEquals(pattern, ExpeditionRewardEnglish) && title == rawTitle && res == rawRes) return line;
                try { return string.Format(CultureInfo.InvariantCulture, pattern, title, units, res); }
                catch (FormatException) { return string.Format(CultureInfo.InvariantCulture, ExpeditionRewardEnglish, title, units, res); }
            }

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

        // ------------------------------------------------------------------ D-162 display copy

        /// <summary>D-162: English display forms for stored lines whose stored form reads badly with
        /// imperative project titles or "... Route" expedition titles. A locale's own pattern wins.</summary>
        public const string ProjectReadyDisplayEnglish = "Ready to queue: {0}.";
        public const string ExpeditionReadyDisplayEnglish = "A new expedition is open: {0}.";
        public const string ExpeditionCompleteDisplayEnglish = "Expedition walked: {0}.";
        public const string ExpeditionRewardDisplayEnglish = "Expedition walked: {0} (+{1} {2}).";
        /// <summary>Fallback completion form for a known project with no authored completion sentence.</summary>
        public const string ProjectCompleteFallbackEnglish = "Finished: {0}.";

        private static readonly Regex ProjectCompleteLine = R(@"^(?<t>.+) is complete\.$");
        private static readonly Regex ProjectReadyLine = R(@"^(?<t>.+) is ready to queue\.$");
        private static readonly Regex ExpeditionReadyLine = R(@"^(?<t>.+) route is ready\.$");
        private static readonly Regex ExpeditionCompleteLine = R(@"^(?<t>.+) expedition complete\.$");

        /// <summary>
        /// D-162: the line a player sees for one stored summary line, in EVERY locale including
        /// English. Lines naming a known project or expedition are re-rendered in a form that reads
        /// well with any title ("The valley survey is complete.", "Ready to queue: Clear the old
        /// trailhead."); everything else is <see cref="Localize"/>. The stored line is never changed,
        /// and shells keep parsing the stored text, never this.
        /// </summary>
        public string Display(string? stored)
        {
            if (string.IsNullOrEmpty(stored)) return stored ?? string.Empty;
            string line = stored!;
            const string Sentinel = "\u0000";

            var pc = ProjectCompleteLine.Match(line);
            if (pc.Success && !line.EndsWith("'s restoration is complete.", StringComparison.Ordinal)
                && TryId(pc.Groups["t"].Value, _projectIds, out var pid))
            {
                string own = _text.Resolve(pid, ContentText.Fields.Completed, Sentinel);
                if (own != Sentinel) return own;
                if (_text.Resolve(PatternId("project-complete"), Field, Sentinel) != Sentinel) return Localize(line);
                var authored = Region1SummaryCopy.CompletedLine(pid);
                return authored ?? Format(ProjectCompleteFallbackEnglish, LocalTitle(pc.Groups["t"].Value));
            }

            var pr = ProjectReadyLine.Match(line);
            if (pr.Success && TryId(pr.Groups["t"].Value, _projectIds, out _))
                return Format(_text.Resolve(PatternId("project-ready"), Field, ProjectReadyDisplayEnglish), LocalTitle(pr.Groups["t"].Value), ProjectReadyDisplayEnglish);

            var er = ExpeditionReadyLine.Match(line);
            if (er.Success && TryId(er.Groups["t"].Value, _expeditionIds, out _))
                return Format(_text.Resolve(PatternId("expedition-ready"), Field, ExpeditionReadyDisplayEnglish), LocalTitle(er.Groups["t"].Value), ExpeditionReadyDisplayEnglish);

            var ew = ExpeditionReward.Match(line);
            if (ew.Success && TryId(ew.Groups["t"].Value, _expeditionIds, out _))
            {
                string rawRes = ew.Groups["r"].Value;
                string pattern = _text.Resolve(PatternId(ExpeditionRewardName), Field, ExpeditionRewardDisplayEnglish);
                object[] args = { LocalTitle(ew.Groups["t"].Value), ew.Groups["n"].Value, _text.Resolve(ResourceId(rawRes), ContentText.Fields.Title, rawRes) };
                try { return string.Format(CultureInfo.InvariantCulture, pattern, args); }
                catch (FormatException) { return string.Format(CultureInfo.InvariantCulture, ExpeditionRewardDisplayEnglish, args); }
            }

            var ec = ExpeditionCompleteLine.Match(line);
            if (ec.Success && TryId(ec.Groups["t"].Value, _expeditionIds, out _))
                return Format(_text.Resolve(PatternId("expedition-complete"), Field, ExpeditionCompleteDisplayEnglish), LocalTitle(ec.Groups["t"].Value), ExpeditionCompleteDisplayEnglish);

            return Localize(line);
        }

        private bool TryId(string englishTitle, HashSet<string> kind, out string id)
        {
            id = string.Empty;
            if (!_titleToId.TryGetValue(englishTitle, out var found) || found.Length == 0 || !kind.Contains(found)) return false;
            id = found;
            return true;
        }

        private static string Format(string pattern, string arg, string? englishFallback = null)
        {
            try { return string.Format(CultureInfo.InvariantCulture, pattern, arg); }
            catch (FormatException) { return string.Format(CultureInfo.InvariantCulture, englishFallback ?? "{0}", arg); }
        }

        private string LocalTitle(string englishTitle) =>
            _titleToId.TryGetValue(englishTitle, out var id) && id.Length > 0
                ? _text.Resolve(id, ContentText.Fields.Title, englishTitle)
                : englishTitle;
    }
}
