using System;
using UnityEngine;
using UnityEngine.UIElements;
using WalkGame.Application;
using WalkGame.Application.Content;
using WalkGame.Infrastructure.Persistence;
using WalkGame.Infrastructure.Platform;

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

        public static GameSession CreateSession(string directory)
        {
            return new GameSession(
                new AtomicFileSaveStore(directory),
                new SaveCodec(new MigrationRunner(DefaultMigrations.All)),
                new SystemClock(),
                Region1Catalog.Create(),
                new LocalPreferencesStore(directory),
                new ShellConnectionPort(UnityEngine.Debug.isDebugBuild));
        }

        private void Start()
        {
            System.IO.Directory.CreateDirectory(SaveDirectory);
            _controller = new ShellController(CreateSession(SaveDirectory),
                () => (ulong)DateTime.UtcNow.Ticks, Debug.isDebugBuild);
            _controller.Boot();

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            Redraw();
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
            ShellView.Render(_document.rootVisualElement, _controller.Render(), id => { _controller.Invoke(id); Redraw(); });
        }
    }
}
