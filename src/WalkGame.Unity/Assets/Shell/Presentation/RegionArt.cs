using System;
using WalkGame.Domain.Regions;

namespace WalkGame.Shell.Presentation
{
    /// <summary>
    /// D-219/D-221: the painted Region 1 map and its interactive landmark markers (direction A).
    /// Marker positions are art-space (0..1 of the 720×1280 map). Stage reads as structure first
    /// (STYLE_GUIDE_A §1): each level adds geometry a grayscale screenshot still distinguishes.
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
            landmarkId == TrailheadArt.LocationId || landmarkId == RiverIntakeArt.LocationId;

        /// <summary>Authored map pins for Region 1 (tuned to region1_map.png).</summary>
        public static readonly (string id, float x, float y)[] Pins =
        {
            ("lm.trailhead", 0.30f, 0.76f),
            ("lm.river-intake", 0.66f, 0.50f),
            ("lm.canopy", 0.22f, 0.44f),
            ("lm.settlement", 0.50f, 0.46f),
            ("lm.wetland", 0.78f, 0.70f),
            ("lm.observatory", 0.72f, 0.18f),
        };

        public static bool TryPin(string landmarkId, out float x, out float y)
        {
            foreach (var p in Pins)
                if (p.id == landmarkId) { x = p.x; y = p.y; return true; }
            x = y = 0f; return false;
        }

        /// <summary>Marker hit size in art space (≥ 48 dp at 720×1280 ≈ 0.067).</summary>
        public const float MarkerHit = 0.090f;

        /// <summary>Structural marker level 0..4 (matches RestorationStage). Colour is secondary.</summary>
        public static int MarkerLevel(RestorationStage stage) => (int)stage;

        /// <summary>How many distinct filled shapes the marker draws at this level (structure budget).</summary>
        public static int MarkerShapeCount(int level) => 2 + Math.Clamp(level, 0, 4); // body + accent + up to 4 growth/details
    }
}
