using System;
using System.Diagnostics;
using WalkGame.Application;
using WalkGame.UnityShell.Composition;
using UnityEngine;

namespace WalkGame.UnityShell
{
    public enum BootPhase
    {
        Booting,
        NeedsNewGame,
        Ready,
        RecoveredFromBackup,
        Unrecoverable,
    }

    public sealed class AppHost : MonoBehaviour
    {
        private const string SaveFolderName = "saves";

        public static AppHost Instance { get; private set; }

        public AppGraph Graph { get; private set; }
        public BootPhase Phase { get; private set; } = BootPhase.Booting;
        public string? BootDetail { get; private set; }

        private IngestResult? _lastIngestResult;
        private DateTimeOffset _lastReconcileUtc;
        private readonly Stopwatch _bootStopwatch = new Stopwatch();

        public IngestResult? LastIngestResult => _lastIngestResult;
        public TimeSpan LastBootDuration => _bootStopwatch.Elapsed;
        public DateTimeOffset LastReconcileUtc => _lastReconcileUtc;

        public event Action? StateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            _bootStopwatch.Start();
            Graph = AppComposer.Compose(System.IO.Path.Combine(Application.persistentDataPath, SaveFolderName));
            RunBoot();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && Phase is BootPhase.Ready or BootPhase.RecoveredFromBackup)
                ReconcileAfterBackground();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused && Phase is BootPhase.Ready or BootPhase.RecoveredFromBackup)
                ReconcileAfterBackground();
        }

        private void OnApplicationQuit()
        {
            // Every committed mutation already persisted atomically before presentation.
        }

        public void RunBoot()
        {
            Phase = BootPhase.Booting;
            var result = Graph.Session.Continue();

            switch (result.Status)
            {
                case StartStatus.NoSaveFound:
                    Phase = BootPhase.NeedsNewGame;
                    break;
                case StartStatus.Loaded:
                    Phase = BootPhase.Ready;
                    break;
                case StartStatus.RecoveredFromBackup:
                    Phase = BootPhase.RecoveredFromBackup;
                    BootDetail = result.Detail;
                    break;
                default:
                    Phase = BootPhase.Unrecoverable;
                    BootDetail = result.Detail;
                    break;
            }

            _lastReconcileUtc = ClockNowUtc();
            NotifyChanged();
        }

        public StartResult StartNewGame(ulong seed)
        {
            var result = Graph.Session.StartNewGame(seed);
            if (result.Status == StartStatus.NewGameCreated)
            {
                Phase = BootPhase.Ready;
                BootDetail = null;
            }
            else
            {
                BootDetail = result.Detail;
            }

            _lastReconcileUtc = ClockNowUtc();
            NotifyChanged();
            return result;
        }

        public void ReconcileAfterBackground()
        {
            var session = Graph.Session;
            var nowUtc = ClockNowUtc();

            var boot = session.Continue();
            if (boot.Status == StartStatus.Loaded || boot.Status == StartStatus.RecoveredFromBackup)
            {
                Phase = boot.Status == StartStatus.RecoveredFromBackup
                    ? BootPhase.RecoveredFromBackup
                    : BootPhase.Ready;
                BootDetail = boot.Detail;

                try
                {
                    var source = Development.DevActivityGate.CreateSourceIfEnabled();
                    if (source != null)
                        _lastIngestResult = session.IngestFromSource(source, _lastReconcileUtc, nowUtc);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            else if (boot.Status != StartStatus.NoSaveFound)
            {
                Phase = BootPhase.Unrecoverable;
                BootDetail = boot.Detail;
            }

            _lastReconcileUtc = nowUtc;
            NotifyChanged();
        }

        public void InjectDevActivity(int days, long stepsPerDay)
        {
            if (!Development.DevActivityGate.Enabled)
                return;

            var nowUtc = ClockNowUtc();
            var source = Development.DevActivityGate.CreateSource(stepsPerDay);
            _lastIngestResult = Graph.Session.IngestFromSource(source, nowUtc.AddDays(-days), nowUtc);
            _lastReconcileUtc = nowUtc;
            NotifyChanged();
        }

        private DateTimeOffset ClockNowUtc() => DateTime.UtcNow;

        private void NotifyChanged()
        {
            StateChanged?.Invoke();
        }
    }
}
