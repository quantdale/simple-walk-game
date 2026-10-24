using System;
using WalkGame.Domain.Regions;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-219/D-221/D-223: the painted Region 1 map and its interactive landmark markers (direction A).
    /// Marker positions are art-space (0..1 of the 720×1280 map). Stage reads as structure first
    /// (STYLE_GUIDE_A §1): each level adds geometry a grayscale screenshot still distinguishes.
    /// D-223: place name lives on a separate label plate (never inside the pin circle).
    /// </summary>
    public static class RegionArt
    {
        public const string ResourceFolder = "Art/Region/";
        public const string Map = "region1_map";
        public static string Backdrop() => Map;

        /// <summary>Map share of the Region screen (card is compact underneath). ≥ WorldShare so the map leads.</summary>
        public const float MapShare = 0.58f;

        public const string PlaceActionPrefix = "region:place:";
        public static string PlaceAction(string landmarkId) => PlaceActionPrefix + landmarkId;
        public static bool IsPlaceAction(string actionId) =>
            actionId != null && actionId.StartsWith(PlaceActionPrefix, StringComparison.Ordinal);
        public static string? PlaceIdOf(string actionId) =>
            IsPlaceAction(actionId) ? actionId.Substring(PlaceActionPrefix.Length) : null;

        /// <summary>Painted places the Region map can open in the world host today.</summary>
        public static bool IsPaintedPlace(string landmarkId) =>
            landmarkId == TrailheadArt.LocationId || landmarkId == RiverIntakeArt.LocationId || landmarkId == CanopyGroveArt.LocationId;

        /// <summary>Authored map pins: id, pin (x,y), label centre offset from pin (lx,ly) in art space.</summary>
        public static readonly (string id, float x, float y, float lx, float ly)[] Pins =
        {
            // lx/ly = label-plate centre relative to pin; |offset| clears PinDp/2 + plate half-height.
            ("lm.trailhead", 0.30f, 0.76f, 0.00f, -0.100f),
            ("lm.river-intake", 0.60f, 0.56f, 0.00f, -0.100f), // ABOVE — clears wetland
            ("lm.canopy", 0.20f, 0.48f, 0.00f, 0.095f),
            ("lm.settlement", 0.46f, 0.30f, 0.00f, -0.095f), // high — clears observatory at 2x
            ("lm.wetland", 0.86f, 0.74f, 0.00f, -0.095f),    // above, far right
            ("lm.observatory", 0.78f, 0.20f, 0.12f, 0.00f),  // label RIGHT of pin
        };

        public static bool TryPin(string landmarkId, out float x, out float y)
        {
            foreach (var p in Pins)
                if (p.id == landmarkId) { x = p.x; y = p.y; return true; }
            x = y = 0f; return false;
        }

        public static bool TryLabelOffset(string landmarkId, out float lx, out float ly)
        {
            foreach (var p in Pins)
                if (p.id == landmarkId) { lx = p.lx; ly = p.ly; return true; }
            lx = 0f; ly = 0.065f; return false;
        }

        /// <summary>Marker hit size in art space (≥ 48 dp at 720×1280 ≈ 0.067).</summary>
        public const float MarkerHit = 0.090f;
        /// <summary>Pin circle size in dp at text scale 1 (hit ≥ 48).</summary>
        public const float PinDp = 52f;
        /// <summary>Label plate font size at text scale 1.</summary>
        public const float LabelFontDp = 13f;
        /// <summary>Horizontal padding inside the label plate (dp).</summary>
        public const float LabelPadX = 8f;
        /// <summary>Max label plate width (dp) before wrapping at spaces.</summary>
        public const float LabelMaxWidthDp = 168f;

        /// <summary>Structural marker level 0..4 (matches RestorationStage). Colour is secondary.</summary>
        public static int MarkerLevel(RestorationStage stage) => (int)stage;

        /// <summary>How many distinct filled shapes the marker draws at this level (structure budget).</summary>
        public static int MarkerShapeCount(int level) => 2 + Math.Clamp(level, 0, 4);

        /// <summary>
        /// Wrap <paramref name="text"/> at spaces only so no mid-word break. A single word longer than
        /// <paramref name="maxChars"/> stays on its own line (never hyphenated).
        /// </summary>
        public static string WrapAtSpaces(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || maxChars < 1) return text ?? "";
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return "";
            var lines = new System.Collections.Generic.List<string>();
            var cur = words[0];
            for (int i = 1; i < words.Length; i++)
            {
                string next = cur + " " + words[i];
                if (next.Length <= maxChars) cur = next;
                else { lines.Add(cur); cur = words[i]; }
            }
            lines.Add(cur);
            return string.Join("\n", lines);
        }

        /// <summary>Longest whitespace-delimited word (for min plate width).</summary>
        public static string LongestWord(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string best = "";
            foreach (var w in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (w.Length > best.Length) best = w;
            return best;
        }

        /// <summary>True when every line is either empty or a sequence of whole words from <paramref name="text"/> (no mid-word split).</summary>
        public static bool LinesKeepWholeWords(string text, string wrapped)
        {
            if (wrapped == null) return false;
            var words = new System.Collections.Generic.HashSet<string>(
                (text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            foreach (var line in wrapped.Split('\n'))
            {
                if (string.IsNullOrEmpty(line)) continue;
                foreach (var w in line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    if (!words.Contains(w)) return false;
            }
            // every original word appears
            foreach (var w in words)
                if (wrapped.IndexOf(w, StringComparison.Ordinal) < 0) return false;
            return !wrapped.Contains("-") || text.Contains("-"); // no invented hyphenation
        }

        /// <summary>Approx char width factor for Nunito at this size (UI Toolkit measure substitute for pure tests).</summary>
        public static float ApproxCharWidth(float fontSize) => fontSize * 0.72f; // Nunito Bold upper bound

        public static int MaxCharsForWidth(float widthDp, float fontSize) =>
            Math.Max(1, (int)((widthDp - 2f * LabelPadX) / Math.Max(1f, ApproxCharWidth(fontSize))));

        /// <summary>Plate width that fits the longest wrapped line (never narrower than the longest word).</summary>
        public static float PlateWidthDp(string label, float fontSize, float textScale = 1f)
        {
            int maxChars = MaxCharsForWidth(LabelMaxWidthDp * textScale, fontSize);
            string wrapped = WrapAtSpaces(label, maxChars);
            int longest = 0;
            foreach (var line in wrapped.Split('\n'))
                if (line.Length > longest) longest = line.Length;
            float w = longest * ApproxCharWidth(fontSize) + 2f * LabelPadX * textScale;
            float minWord = LongestWord(label).Length * ApproxCharWidth(fontSize) + 2f * LabelPadX * textScale;
            return Math.Max(48f * textScale, Math.Max(w, minWord));
        }
    }
}
