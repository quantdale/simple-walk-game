using System;
using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Infrastructure.Platform;
using WalkGame.Infrastructure.Platform.HealthConnect;

namespace WalkGame.Shell
{
    /// <summary>
    /// Composition root (the ONLY Unity file allowed to construct Infrastructure adapters).
    /// Saves live under Application.persistentDataPath, which Android excludes from shared storage.
    /// </summary>
    public sealed class ShellBootstrap : MonoBehaviour
    {
        private ShellController? _controller;
        private UIDocument? _document;

        public static string SaveDirectory => System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "walkgame");

        public static GameSession CreateSession(string directory) =>
            CreateSession(directory, new ShellConnectionPort(UnityEngine.Debug.isDebugBuild));

        public static GameSession CreateSession(string directory, WalkGame.Application.Activity.IActivityConnectionPort port)
        {
            return new GameSession(
                new AtomicFileSaveStore(directory),
                new SaveCodec(new MigrationRunner(DefaultMigrations.All)),
                new SystemClock(),
                Region1Catalog.Create(),
                new LocalPreferencesStore(directory),
                port);
        }

        private void Start()
        {
            System.IO.Directory.CreateDirectory(SaveDirectory);
            if (AndroidHealthConnectBridge.IsAndroidPlayer)
            {
                // D-068: Android players read real steps through Health Connect.
                var health = new HealthConnectActivityAdapter(new AndroidHealthConnectBridge(), () => DateTimeOffset.UtcNow);
                _controller = new ShellController(CreateSession(SaveDirectory, health),
                    () => (ulong)DateTime.UtcNow.Ticks, Debug.isDebugBuild, health, AndroidHealthConnectBridge.RequestPermission);
            }
            else
                _controller = new ShellController(CreateSession(SaveDirectory),
                    () => (ulong)DateTime.UtcNow.Ticks, Debug.isDebugBuild);
            _controller.Boot();
            Debug.Log("WG_BOOT status=" + _controller.LastStartStatus + " screen=" + _controller.Current + " dir=" + SaveDirectory);

            _document = gameObject.AddComponent<UIDocument>();
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("ShellTheme");
            if (panel.themeStyleSheet == null) Debug.LogWarning("WG_THEME missing: Resources/ShellTheme.tss");
            // 1 UI unit = 1 dp, so the 56-unit buttons are 56 dp touch targets on every density.
            panel.scaleMode = PanelScaleMode.ConstantPhysicalSize;
            panel.referenceDpi = 160;
            panel.fallbackDpi = 160;
            _document.panelSettings = panel;
            Redraw();
            // Development-only emulator autodrive: `adb shell run-as <pkg> touch files/wg-autodrive`.
            if (Debug.isDebugBuild && System.IO.File.Exists(System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "wg-autodrive")))
                StartCoroutine(AutoDrive());
        }

        private static readonly string[] AutoDriveSteps =
            { "start", "guide:next", "guide:next", "queue:FIRST", "guide:next", "guide:next", "guide:next", "nav:Projects", "nav:Settings", "toggle:reducedMotion", "delete:request", "delete:cancel", "delete:request", "delete:confirm", "start", "dev:walk", "nav:ActivityStatus", "nav:Home" };

        private System.Collections.IEnumerator AutoDrive()
        {
            for (int i = 0; i < AutoDriveSteps.Length; i++)
            {
                yield return new WaitForSecondsRealtime(25f);
                if (_controller == null) yield break;
                string id = AutoDriveSteps[i];
                if (id == "queue:FIRST")
                    id = System.Linq.Enumerable.FirstOrDefault(_controller.Render().Actions, a => a.Id.StartsWith("queue:"))?.Id ?? "nav:Home";
                Debug.Log("WG_STEP " + i + " action=" + id);
                try { _controller.Invoke(id); }
                catch (Exception ex) { Debug.LogError("WG_STEP " + i + " action=" + id + " EXCEPTION " + ex); }
                Redraw();
            }
            Debug.Log("WG_AUTODRIVE_DONE");
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused || _controller == null) return;
            _controller.Resume();
            Redraw();
        }

        private void Redraw()
        {
            if (_controller == null || _document == null) return;
            var model = _controller.Render();
            Debug.Log("WG_RENDER screen=" + model.Screen + " state=" + model.State + " lines=" + model.Lines.Count + " actions=" + model.Actions.Count + (model.Notice != null ? " notice=" + model.Notice : ""));
            ShellView.Render(_document.rootVisualElement, model, id => { _controller.Invoke(id); Redraw(); });
        }
    }
}
