using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WalkGame.Shell.Editor
{
    /// <summary>Batch-mode entry points used by tools/unity/*.sh. Never needs the editor GUI.</summary>
    public static class ShellBuild
    {
        private const string ScenePath = "Assets/Scenes/Boot.unity";

        public static void EnsureBootScene()
        {
            if (System.IO.File.Exists(ScenePath)) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            new GameObject("Shell").AddComponent<ShellBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        public static void BuildAndroidDebug()
        {
            EnsureBootScene();
            string output = Environment.GetEnvironmentVariable("WG_APK_PATH") ?? "Build/Android/simple-walk-game-debug.apk";
            PlayerSettings.companyName = "quantdale";
            PlayerSettings.productName = "Simple Walk Game";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.quantdale.simplewalkgame");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            // WG_ANDROID_ARCH=x86_64 builds an emulator-only APK (the box's emulator runs x86_64 images).
            PlayerSettings.Android.targetArchitectures =
                Environment.GetEnvironmentVariable("WG_ANDROID_ARCH") == "x86_64" ? AndroidArchitecture.X86_64 : AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.Development,
            });
            Debug.Log("WG_BUILD_RESULT " + report.summary.result + " size=" + report.summary.totalSize + " path=" + output);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
            /// <summary>
        /// Exports an unsigned Xcode project (KI-022). Output: WG_XCODE_PATH (default Build/iOS/xcode).
        /// No signing and no team id; the project is verified by CI with xcodebuild CODE_SIGNING_ALLOWED=NO.
        /// Like the Android build, this touches ProjectSettings.asset on disk; never commit that (KI-015).
        /// </summary>
        public static void BuildIosXcode()
        {
            EnsureBootScene();
            string output = Environment.GetEnvironmentVariable("WG_XCODE_PATH") ?? "Build/iOS/xcode";
            PlayerSettings.companyName = "quantdale";
            PlayerSettings.productName = "Simple Walk Game";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.quantdale.simplewalkgame");
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.iOS.appleDeveloperTeamID = "";
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            });
            Debug.Log("WG_BUILD_RESULT " + report.summary.result + " size=" + report.summary.totalSize + " path=" + output);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
