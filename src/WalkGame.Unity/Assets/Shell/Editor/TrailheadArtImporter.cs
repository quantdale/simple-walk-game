using UnityEditor;

namespace WalkGame.Shell.Editor
{
    /// <summary>
    /// D-204: import rules for the painted trailhead art (PERFORMANCE_BUDGETS §9, style guide §7.5–7.6):
    /// no mipmaps, max 2048, clamp, alpha-is-transparency, compressed; ASTC 6×6 on Android and iOS.
    /// </summary>
    public sealed class TrailheadArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Resources/Art/Trailhead/";
        /// <summary>D-216: the River Intake art follows the same rules.</summary>
        public const string IntakeFolder = "Assets/Resources/Art/Intake/";
        public const string RegionFolder = "Assets/Resources/Art/Region/";
        /// <summary>D-222: the Canopy Grove art follows the same rules.</summary>
        public const string CanopyFolder = "Assets/Resources/Art/Canopy/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder) && !assetPath.StartsWith(IntakeFolder) && !assetPath.StartsWith(RegionFolder) && !assetPath.StartsWith(CanopyFolder)) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            t.npotScale = TextureImporterNPOTScale.None;
            t.maxTextureSize = 2048;
            t.textureCompression = TextureImporterCompression.Compressed;
            foreach (var platform in new[] { "Android", "iPhone" })
                t.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = platform, overridden = true, maxTextureSize = 2048, format = TextureImporterFormat.ASTC_6x6,
                });
        }
    }
}
