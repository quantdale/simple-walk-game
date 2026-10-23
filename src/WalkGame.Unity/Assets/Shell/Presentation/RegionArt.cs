using WalkGame.Domain.Regions;

namespace WalkGame.Shell.Presentation
{
    /// <summary>D-219: the painted Region 1 map behind the Region text shell (direction A).</summary>
    public static class RegionArt
    {
        public const string ResourceFolder = "Art/Region/";
        public const string Map = "region1_map";
        /// <summary>The Region screen always uses the painted valley map (stage colour is in the landmark lines, not a second plate).</summary>
        public static string Backdrop() => Map;
    }
}
