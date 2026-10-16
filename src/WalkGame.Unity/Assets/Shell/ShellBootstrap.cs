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
            => CreateSession(directory, port, new SystemClock());

        /// <summary>Test seam (D-073 return-after-absence tests): same composition with an injected clock.</summary>
        public static GameSession CreateSession(string directory, WalkGame.Application.Activity.IActivityConnectionPort port, WalkGame.Domain.Time.IClock clock)
        {
            return new GameSession(
                new AtomicFileSaveStore(directory),
                new SaveCodec(new MigrationRunner(DefaultMigrations.All)),
                clock,
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
            _controller.TextScaleSource = () => UnityEngine.Accessibility.AccessibilitySettings.fontScale;
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
            UnityEngine.Accessibility.AccessibilitySettings.fontScaleChanged += _ => Redraw();
            UnityEngine.Accessibility.AssistiveSupport.screenReaderStatusChanged += _ => Redraw();
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

        private ShellScreen? _lastScreen;
        private AudioSource _audio;

        private void PlayCue(SoundCue cue)
        {
            var samples = SoundCues.Samples(cue);
            if (samples.Length == 0) return;
            if (_audio == null) { _audio = gameObject.AddComponent<AudioSource>(); _audio.playOnAwake = false; }
            var clip = AudioClip.Create("wg-cue-" + cue, samples.Length, 1, SoundCues.SampleRate, false);
            clip.SetData(samples, 0);
            _audio.PlayOneShot(clip);
        }

        private void Redraw()
        {
            if (_controller == null || _document == null) return;
            var model = _controller.Render();
            // D-088: per-render logging only in development builds (Debug.Log builds strings and
            // captures a stack trace on every screen switch in a release build too).
            if (Debug.isDebugBuild) Debug.Log("WG_RENDER screen=" + model.Screen + " state=" + model.State + " lines=" + model.Lines.Count + " actions=" + model.Actions.Count + (model.Notice != null ? " notice=" + model.Notice : ""));
            System.Action<string> act = id => { _controller.Invoke(id); Redraw(); };
            ShellView.Render(_document.rootVisualElement, model, act, UnityEngine.Accessibility.AccessibilitySettings.fontScale);
            // D-104: a calm cue only on arrival at a summary that earned one, and only with sound on.
            if (SoundCues.ShouldPlay(_lastScreen, model)) PlayCue(model.Cue);
            _lastScreen = model.Screen;
            ScreenReaderBridge.Publish(model, _document.rootVisualElement, act);
        }
    }
}
