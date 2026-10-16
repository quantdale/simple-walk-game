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
            var m = With(ShellScreen.Projects, "Projects", open.Count == 0 ? ScreenState.Empty : ScreenState.Normal);
            if (open.Count == 0) m.Lines.Add("No projects are available right now.");
            foreach (var r in p.Projects.OrderBy(r => r.QueuedPosition ?? int.MaxValue))
            {
                string pos = r.QueuedPosition.HasValue ? "#" + (r.QueuedPosition.Value + 1) + " " : "";
                m.Lines.Add(pos + r.TitleKey + " — " + r.Status + " (" + r.VitalityInvested + "/" + r.VitalityCost + ")");
                if (r.Status == ProjectStatus.Available) m.Actions.Add(new ShellAction("queue:" + r.ProjectId, "Queue " + r.TitleKey));
                if (r.Status == ProjectStatus.Queued && r.QueuedPosition > 0) m.Actions.Add(new ShellAction("up:" + r.ProjectId, "Move up " + r.TitleKey));
            }
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
            m.Actions.Add(new ShellAction("toggle:reducedMotion", "Toggle reduced motion"));
            m.Actions.Add(new ShellAction("toggle:sound", "Toggle sound"));
            m.Actions.Add(new ShellAction("toggle:diagnostics", "Toggle diagnostics"));
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
            m.Lines.Add("Status: " + a.Status);
            m.Lines.Add("Recommended: " + a.RecommendedAction);
            if (!a.HasProcessedAnyRecord) m.Lines.Add("No walking data has been received yet.");
            else m.Lines.Add("Last batch credited " + a.LastBatchVitalityCredited + " vitality.");
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
                    var home = _session.GetHome();
                    var damaged = home.Landmarks.FirstOrDefault();
                    m.Lines.Add(damaged != null
                        ? damaged.TitleKey + " is still damaged (stage " + damaged.Stage + "). Your walks will bring it back."
                        : "The valley is quiet and worn. Your walks will bring it back.");
                    m.Actions.Add(new ShellAction("guide:next", "Continue"));
                    break;
                case OnboardingStage.ActivityConnection:
                    m = With(ShellScreen.Guide, "Connect your walking");
                    m.Lines.Add("The game reads only your step counts, on this device. Nothing is uploaded.");
                    m.Lines.Add("Activity source: " + _session.GetActivityStatus().Status + ".");
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
                            m.Actions.Add(new ShellAction("queue:" + r.ProjectId, "Restore " + r.TitleKey));
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
            return m;
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
