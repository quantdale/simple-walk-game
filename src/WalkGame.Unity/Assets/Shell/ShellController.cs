using System;
using System.Collections.Generic;
using System.Linq;
using WalkGame.Application;
using WalkGame.Application.Activity;
using WalkGame.Application.Development;
using WalkGame.Application.Persistence;
using WalkGame.Application.ReadModels;
using WalkGame.Application.Ux;
using WalkGame.Domain.Common;
using WalkGame.Domain.Projects;

namespace WalkGame.Shell
{
    /// <summary>
    /// Engine-free presenter for the M5-U1 shell. It only calls public <see cref="GameSession"/>
    /// commands and builds <see cref="ScreenModel"/>s from read models; it never touches
    /// canonical state directly (enforced by verify-architecture.sh rule 8).
    /// </summary>
    public sealed class ShellController
    {
        private readonly GameSession _session;
        private readonly Func<ulong> _seedSource;
        private readonly bool _devTools;
        private string? _notice;
        private string? _error;
        private bool _booted;

        public ShellController(GameSession session, Func<ulong> seedSource, bool devTools)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
            _devTools = devTools;
        }

        public ShellScreen Current { get; private set; } = ShellScreen.Onboarding;
        public StartStatus? LastStartStatus { get; private set; }

        public void Boot()
        {
            var result = _session.Continue();
            LastStartStatus = result.Status;
            var (screen, notice) = BootRouter.Route(result.Status);
            _booted = screen == ShellScreen.Home;
            _notice = notice;
            _error = null;
            Current = screen;
            if (_booted && OnboardingIncomplete()) Current = ShellScreen.Guide;
            else if (_booted && _session.GetPendingReturnSummary() != null) Current = ShellScreen.ReturnSummary;
        }

        /// <summary>App returned to the foreground: re-run Continue() so offline progress and a return summary appear.</summary>
        public void Resume()
        {
            if (!_booted) return;
            Boot();
        }

        public void Invoke(string actionId)
        {
            _error = null;
            if (actionId.StartsWith("nav:", StringComparison.Ordinal))
            {
                _notice = null;
                Current = (ShellScreen)Enum.Parse(typeof(ShellScreen), actionId.Substring(4));
                return;
            }
            if (actionId.StartsWith("queue:", StringComparison.Ordinal)) { Check(_session.EnqueueProject(actionId.Substring(6))); return; }
            if (actionId.StartsWith("up:", StringComparison.Ordinal)) { MoveUp(actionId.Substring(3)); return; }
            switch (actionId)
            {
                case "start":
                    var started = _session.StartNewGame(_seedSource());
                    LastStartStatus = started.Status;
                    if (started.Status == StartStatus.NewGameCreated)
                    {
                        _booted = true;
                        Check(_session.AdvanceOnboarding(OnboardingStage.WorldBaseline));
                        Current = OnboardingIncomplete() ? ShellScreen.Guide : ShellScreen.Home;
                    }
                    else _error = "Could not start a new game (" + started.Status + "): " + started.Detail;
                    return;
                case "retry": Boot(); return;
                case "guide:next": GuideNext(); return;
                case "ack": if (Check(_session.AcknowledgeReturnSummary())) Current = ShellScreen.Home; return;
                case "toggle:reducedMotion": Check(_session.SetReducedMotion(!_session.GetSettings().ReducedMotion)); return;
                case "toggle:sound": Check(_session.SetSoundEnabled(!_session.GetSettings().SoundEnabled)); return;
                case "toggle:diagnostics": Check(_session.SetDiagnosticsVisible(!_session.GetSettings().DiagnosticsVisible)); return;
                case "delete:request": Current = ShellScreen.DeleteConfirm; return;
                case "delete:cancel": Current = ShellScreen.Settings; return;
                case "delete:confirm":
                    var erased = _session.DeleteAllLocalData();
                    if (erased.Status == LocalDataEraseStatus.Erased)
                    {
                        _booted = false; _notice = "All local data was deleted."; Current = ShellScreen.Onboarding;
                    }
                    else { Current = ShellScreen.Settings; _error = "Delete failed (" + (erased.FailureCategory ?? erased.Status.ToString()) + "). Your data was not changed."; }
                    return;
                case "dev:walk":
                    if (!_devTools) { _error = "Development tools are disabled."; return; }
                    var now = DateTimeOffset.UtcNow;
                    _session.IngestFromSource(new SyntheticWalkingSource(6000), now.AddDays(-1), now);
                    return;
                default: _error = "Unknown action: " + actionId; return;
            }
        }

        private bool OnboardingIncomplete() => _session.GetOnboarding().CurrentStage < OnboardingStage.Complete;

        private void GuideNext()
        {
            var stage = _session.GetOnboarding().CurrentStage;
            if (stage < OnboardingStage.WorldBaseline) stage = OnboardingStage.Premise;
            var next = stage + 1;
            if (Check(_session.AdvanceOnboarding(next)) && next == OnboardingStage.Complete) Current = ShellScreen.Home;
        }

        private void MoveUp(string projectId)
        {
            var queued = _session.GetHome().Queued.Select(q => q.ProjectId).ToList();
            int i = queued.IndexOf(projectId);
            if (i <= 0) return;
            queued.RemoveAt(i); queued.Insert(i - 1, projectId);
            Check(_session.ReorderQueue(queued));
        }

        private bool Check(DomainResult r)
        {
            if (r.IsSuccess) return true;
            _error = r.Error?.Message ?? "The action could not be completed.";
            return false;
        }

        public ScreenModel Render()
        {
            var m = Build();
            if (_notice != null) m.Notice = _notice;
            return m;
        }

        private ScreenModel Build()
        {
            switch (Current)
            {
                case ShellScreen.Onboarding: return Onboarding();
                case ShellScreen.Home: return Home();
                case ShellScreen.ReturnSummary: return Summary();
                case ShellScreen.Projects: return Projects();
                case ShellScreen.Settings: return Settings();
                case ShellScreen.DeleteConfirm: return DeleteConfirm();
                case ShellScreen.ActivityStatus: return Activity();
                case ShellScreen.BootError: return BootErrorScreen();
                case ShellScreen.Guide: return Guide();
                case ShellScreen.Region: return Region();
                default: throw new InvalidOperationException("Unmapped screen " + Current);
            }
        }

        private ScreenModel With(ShellScreen s, string title, ScreenState normal = ScreenState.Normal)
            => new ScreenModel(s, _error != null ? ScreenState.Error : normal, title) { Notice = _error };

        private ScreenModel Onboarding()
        {
            var m = With(ShellScreen.Onboarding, "Your movement restores this world");
            m.Lines.Add("A forgotten valley is waiting. Each walk you take brings it back, a little at a time.");
            m.Lines.Add("Nothing is lost while you are away.");
            m.Actions.Add(new ShellAction("start", "Begin restoring"));
            return m;
        }

        private ScreenModel Home()
        {
            var home = _session.GetHome();
            var glance = _session.GetGlance();
            bool empty = home.ActiveProjectId == null && home.Queued.Count == 0;
            var m = With(ShellScreen.Home, "Home", empty ? ScreenState.Empty : ScreenState.Normal);
            m.Lines.Add(glance.WhatChangedSentence);
            m.Lines.Add(glance.RestoringNowSentence);
            if (glance.AttentionSentence != null) m.Lines.Add(glance.AttentionSentence);
            m.Lines.Add(glance.ActivitySentence);
            m.Lines.Add("Vitality " + home.Vitality + " · Materials " + home.Materials + " · Knowledge " + home.Knowledge);
            if (empty) m.Lines.Add("Nothing is being restored yet. Choose a project to begin.");
            if (home.HasPendingSummary) m.Actions.Add(new ShellAction("nav:ReturnSummary", "While you were away"));
            m.Actions.Add(new ShellAction("nav:Projects", "Projects"));
            m.Actions.Add(new ShellAction("nav:Region", "The valley"));
            m.Actions.Add(new ShellAction("nav:ActivityStatus", "Activity"));
            m.Actions.Add(new ShellAction("nav:Settings", "Settings"));
            if (_devTools) m.Actions.Add(new ShellAction("dev:walk", "[dev] Simulate a day of walking"));
            m.Lines.RemoveAll(string.IsNullOrEmpty);
            return m;
        }

        private ScreenModel Summary()
        {
            var s = _session.GetPendingReturnSummary();
            var m = With(ShellScreen.ReturnSummary, "While you were away", s == null ? ScreenState.Empty : ScreenState.Normal);
            if (s == null) m.Lines.Add("Nothing new since your last visit.");
            else foreach (var item in s.Items) m.Lines.Add(item.Text);
            if (s != null) m.Actions.Add(new ShellAction("ack", "Continue"));
            m.Actions.Add(new ShellAction("nav:Home", "Home"));
            return m;
        }

        private ScreenModel Projects()
        {
            var p = _session.GetProjects();
            var open = p.Projects.Where(r => r.Status != ProjectStatus.Locked && r.Status != ProjectStatus.Completed).ToList();
            bool anyQueued = p.Projects.Any(r => r.Status == ProjectStatus.Queued || r.Status == ProjectStatus.Active);
            var m = With(ShellScreen.Projects, "Projects", open.Count == 0 ? ScreenState.Empty : ScreenState.Normal);
            // UX_DESIGN §17: empty states explain the next useful action.
            if (open.Count == 0)
                m.Lines.Add(p.Projects.All(r => r.Status == ProjectStatus.Completed)
                    ? "Every restoration in this valley is finished. It stays restored; check the valley to see it flourish."
                    : "No projects are open right now. Keep walking: finishing restorations unlocks new ones.");
            else if (!anyQueued)
                m.Lines.Add("Nothing is queued. Choose a project below and your walks will start restoring it.");
            foreach (var r in p.Projects.Where(r => r.Status != ProjectStatus.Locked).OrderBy(r => r.QueuedPosition ?? int.MaxValue))
            {
                string title = PlayerCopy.Title(r.TitleKey);
                m.Lines.Add(title + " — " + PlayerCopy.ProjectStatus(r.Status, r.QueuedPosition) + " (" + r.VitalityInvested + " of " + r.VitalityCost + " vitality)");
                if (r.Status == ProjectStatus.Available) m.Actions.Add(new ShellAction("queue:" + r.ProjectId, "Restore " + title));
                if (r.Status == ProjectStatus.Queued && r.QueuedPosition > 0) m.Actions.Add(new ShellAction("up:" + r.ProjectId, "Move " + title + " up"));
            }
            int locked = p.Projects.Count(r => r.Status == ProjectStatus.Locked);
            if (locked > 0) m.Lines.Add(locked + (locked == 1 ? " more project unlocks" : " more projects unlock") + " as the valley recovers.");
            m.Actions.Add(new ShellAction("nav:Home", "Home"));
            return m;
        }

        /// <summary>UX_DESIGN §9 compact region status: landmarks as phrases, producers, closure.</summary>
        private ScreenModel Region()
        {
            var r = _session.GetRegion();
            var m = With(ShellScreen.Region, "The valley");
            if (r.RegionCompleted)
                m.Lines.Add("The valley is fully restored. It stays this way; nothing will reset.");
            else
                m.Lines.Add(r.CompletedProjects + " of " + r.TotalProjects + " restorations finished.");
            foreach (var l in r.Landmarks) m.Lines.Add(GlancePhrases.FormatLandmark(PlayerCopy.Title(l.TitleKey), l.Stage));
            foreach (var pr in r.Producers)
                m.Lines.Add(PlayerCopy.Title(pr.TitleKey) + ": " + (!pr.Unlocked
                    ? "not running yet. Restore nearby landmarks to bring it back."
                    : pr.CapacityLimited ? "storage full, holding what it made." : "producing while you are away."));
            m.Lines.Add(r.DiscoveriesUnlocked == 0
                ? "No discoveries yet. Walks that restore landmarks reveal them."
                : r.DiscoveriesUnlocked + (r.DiscoveriesUnlocked == 1 ? " discovery found." : " discoveries found."));
            m.Lines.Add(r.ExpeditionsAvailable == 0 && r.ExpeditionsCompleted == 0
                ? "No expedition routes are open yet. Restoring the valley opens them."
                : r.ExpeditionsAvailable + " expedition routes open, " + r.ExpeditionsCompleted + " completed.");
            m.Actions.Add(new ShellAction("nav:Home", "Home"));
            return m;
        }

        private ScreenModel Settings()
        {
            var s = _session.GetSettings();
            var m = With(ShellScreen.Settings, "Settings");
            m.Lines.Add("Reduced motion: " + (s.ReducedMotion ? "on" : "off"));
            m.Lines.Add("Sound: " + (s.SoundEnabled ? "on" : "off"));
            m.Lines.Add("Diagnostics: " + (s.DiagnosticsVisible ? "visible" : "hidden"));
            m.Actions.Add(new ShellAction("toggle:reducedMotion", (s.ReducedMotion ? "Turn off" : "Turn on") + " reduced motion"));
            m.Actions.Add(new ShellAction("toggle:sound", (s.SoundEnabled ? "Turn off" : "Turn on") + " sound"));
            m.Actions.Add(new ShellAction("toggle:diagnostics", (s.DiagnosticsVisible ? "Hide" : "Show") + " diagnostics"));
            m.Actions.Add(new ShellAction("delete:request", "Delete all local data…"));
            m.Actions.Add(new ShellAction("nav:Home", "Home"));
            return m;
        }

        private ScreenModel DeleteConfirm()
        {
            var m = With(ShellScreen.DeleteConfirm, "Delete all local data?");
            m.Lines.Add("This permanently removes your world, its backup and your preferences from this device. It cannot be undone.");
            m.Actions.Add(new ShellAction("delete:cancel", "Keep my data"));
            m.Actions.Add(new ShellAction("delete:confirm", "Delete everything"));
            return m;
        }

        private ScreenModel Activity()
        {
            var a = _session.GetActivityStatus();
            var m = With(ShellScreen.ActivityStatus, "Activity", a.HasProcessedAnyRecord ? ScreenState.Normal : ScreenState.Empty);
            // UX_DESIGN §5: player-facing states only; never raw identifiers or exception text.
            m.Lines.Add(PlayerCopy.ActivityStatus(a.Status));
            string next = PlayerCopy.ActivityAction(a.RecommendedAction);
            if (next != null) m.Lines.Add(next);
            if (!a.HasProcessedAnyRecord) m.Lines.Add("No walking data has arrived yet. Your progress starts with your first walk.");
            else m.Lines.Add("Your last walks added " + a.LastBatchVitalityCredited + " vitality.");
            m.Actions.Add(new ShellAction("nav:Home", "Home"));
            return m;
        }

        /// <summary>UX_DESIGN §11: one calm step per screen, no carousel. Forward-only and resumable (D-042).</summary>
        private ScreenModel Guide()
        {
            var ob = _session.GetOnboarding();
            ScreenModel m;
            switch (ob.CurrentStage)
            {
                case OnboardingStage.NotStarted:
                case OnboardingStage.Premise:
                case OnboardingStage.WorldBaseline:
                    m = With(ShellScreen.Guide, "The valley today");
                    var damaged = _session.GetRegion().Landmarks.FirstOrDefault();
                    if (damaged != null) m.Lines.Add(GlancePhrases.FormatLandmark(PlayerCopy.Title(damaged.TitleKey), damaged.Stage));
                    m.Lines.Add("The valley is quiet and worn. Your walks will bring it back.");
                    m.Actions.Add(new ShellAction("guide:next", "Continue"));
                    break;
                case OnboardingStage.ActivityConnection:
                    m = With(ShellScreen.Guide, "Connect your walking");
                    m.Lines.Add("The game reads only your step counts, on this device. Nothing is uploaded.");
                    m.Lines.Add(PlayerCopy.ActivityStatus(_session.GetActivityStatus().Status));
                    m.Lines.Add("You can keep going without connecting; nothing is locked.");
                    m.Actions.Add(new ShellAction("guide:next", "Continue"));
                    break;
                case OnboardingStage.FirstProject:
                    m = With(ShellScreen.Guide, "Choose a first restoration");
                    if (ob.FirstProjectChosen)
                    {
                        m.Lines.Add("Good choice. It will grow as you move.");
                        m.Actions.Add(new ShellAction("guide:next", "Continue"));
                    }
                    else
                    {
                        m.Lines.Add("Pick one place to restore first. You can change the order later.");
                        foreach (var r in _session.GetProjects().Projects.Where(r => r.Status == ProjectStatus.Available))
                            m.Actions.Add(new ShellAction("queue:" + r.ProjectId, "Restore " + PlayerCopy.Title(r.TitleKey)));
                    }
                    break;
                case OnboardingStage.Simulation:
                    m = With(ShellScreen.Guide, "Progress while you're away");
                    m.Lines.Add("Steps you take with the app closed still count. When you return, you'll see what changed.");
                    m.Actions.Add(new ShellAction("guide:next", "Continue"));
                    break;
                default:
                    m = With(ShellScreen.Guide, "That's all");
                    m.Lines.Add("You don't need to keep the app open. Walk as you normally do and check in whenever you like.");
                    m.Actions.Add(new ShellAction("guide:next", "Go to the valley"));
                    break;
            }
            // UX_DESIGN §11/§21: a calm progress cue so the player knows the guide is short.
            m.Lines.Insert(0, GuideProgress(ob.CurrentStage));
            return m;
        }

        /// <summary>Five player-visible steps: valley, walking, first project, away, done.</summary>
        public const int GuideSteps = 5;

        public static string GuideProgress(OnboardingStage stage)
        {
            int step = stage <= OnboardingStage.WorldBaseline ? 1 : Math.Min(GuideSteps, (int)stage - 1);
            return "Step " + step + " of " + GuideSteps;
        }

        private ScreenModel BootErrorScreen()
        {
            // Deliberately no "start" action: a save we could not read is never overwritten from here.
            var m = new ScreenModel(ShellScreen.BootError, ScreenState.Error, "We couldn't open your world");
            m.Lines.Add("Status: " + LastStartStatus);
            m.Actions.Add(new ShellAction("retry", "Try again"));
            return m;
        }
    }
}
