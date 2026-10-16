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
    }
}
