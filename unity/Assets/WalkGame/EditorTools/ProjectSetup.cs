using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.UnityShell;
using WalkGame.UnityShell.Shell;

namespace WalkGame.UnityShell.EditorTools
{
    public static class ProjectSetup
    {
        private const string SettingsDir = "Assets/WalkGame/Settings";
        private const string ScenesDir = "Assets/WalkGame/Scenes";
        private const string PanelSettingsPath = SettingsDir + "/PanelSettings.asset";
        private const string BootstrapScenePath = ScenesDir + "/Bootstrap.unity";
        private const string DevDefine = "WALKGAME_DEV_TOOLS";

        public static void SetupProject()
        {
            EnsureFolders();
            ApplyScriptingDefines();
            var panel = EnsurePanelSettings();
            EnsureBootstrapScene(panel);
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[WalkGame.ProjectSetup] project setup complete");
        }

        private static void EnsureFolders()
        {
            foreach (var dir in new[] { SettingsDir, ScenesDir })
                if (!AssetDatabase.IsValidFolder(dir))
                    AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(dir)!, System.IO.Path.GetFileName(dir));
        }

        private static void ApplyScriptingDefines()
        {
            foreach (BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (group == BuildTargetGroup.Unknown)
                    continue;
                if (!IsMobileOrDesktop(group))
                    continue;
                var defines = PlayerSettings.GetScriptingDefineSymbols(group);
                if (!defines.Contains(DevDefine))
                    PlayerSettings.SetScriptingDefineSymbols(group, defines.Length > 0 ? defines + ";" + DevDefine : DevDefine);
            }
        }

        private static bool IsMobileOrDesktop(BuildTargetGroup group)
        {
            return group is BuildTargetGroup.Android or BuildTargetGroup.iOS
                or BuildTargetGroup.Standalone or BuildTargetGroup.WebGL;
        }

        private static PanelSettings EnsurePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null)
                return existing;

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            var theme = LoadDefaultTheme();
            if (theme != null)
                panel.themeStyleSheet = theme;
            else
                Debug.LogWarning("[WalkGame.ProjectSetup] default runtime theme not found; UI will use fallback styling");

            AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            return panel;
        }

        private static ThemeStyleSheet? LoadDefaultTheme()
        {
            string[] candidates =
            {
                "Packages/com.unity.ui/PackageResources/StyleSheets/Generated/UnityDefaultRuntimeTheme.tss",
                "Packages/com.unity.ui-elements/PackageResources/StyleSheets/Generated/UnityDefaultRuntimeTheme.tss",
            };
            return candidates.Select(p => AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(p))
                .FirstOrDefault(t => t != null);
        }

        private static void EnsureBootstrapScene(PanelSettings panel)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("AppRoot");
            root.AddComponent<AppHost>();

            var document = root.AddComponent<UIDocument>();
            document.panelSettings = panel;

            root.AddComponent<AppShell>();

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
            };
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Quantdale";
            PlayerSettings.productName = "Walk Game";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.RunInBackground = false;
        }
    }
}
