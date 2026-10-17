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
        private readonly IActivityRecordSource? _liveSource;
        private readonly Action? _requestPermission;

        /// <summary>Days of provider history re-read on each boot/resume; replays are exactly-once no-ops.</summary>
        public const int SyncWindowDays = 30;

        public ShellController(GameSession session, Func<ulong> seedSource, bool devTools,
            IActivityRecordSource? liveSource = null, Action? requestPermission = null)
        {
            _liveSource = liveSource;
            _requestPermission = requestPermission;
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
            _devTools = devTools;
        }

        /// <summary>Effective OS font scale source (D-073); the bootstrap wires AccessibilitySettings.fontScale. Tests inject values.</summary>
        public Func<float> TextScaleSource { get; set; } = () => 1f;

        /// <summary>
        /// D-111: hands text to the OS share sheet; returns false if it could not. Null means this
        /// device has no share sheet. The bootstrap wires <see cref="AndroidShareSheet"/>; tests inject fakes.
        /// </summary>
        public Func<string, bool>? ShareText { get; set; }

        /// <summary>D-119: optional gentle-reminder delivery; null where the OS has none (editor, iOS today).</summary>
        public IReminderScheduler? Reminders { get; set; }

        /// <summary>D-111: larger exports are refused (Android intent extras are limited to about 1 MB).</summary>
        public const int MaxExportChars = 512 * 1024;

        public static string TextPreviewSample => PlayerStrings.T("shell.textPreview");

        /// <summary>Settings text-size line (B3, D-083). Never throws on a hostile scale source.</summary>
        public static string TextSizeLine(float raw)
        {
            float k = ShellView.ClampTextScale(raw);
            string pct = Math.Round(k * 100f).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
            return raw > ShellView.MaxTextScale
                ? PlayerStrings.F("settings.textsize.max", pct)
                : PlayerStrings.F("settings.textsize", pct);
        }

        /// <summary>D-097: settings status lines say what the setting does, in plain words.</summary>
        public static string ReducedMotionLine(bool on) => on
            ? PlayerStrings.T("settings.motion.on")
            : PlayerStrings.T("settings.motion.off");

        public static string SoundLine(bool on) => on
            ? PlayerStrings.T("settings.sound.on")
            : PlayerStrings.T("settings.sound.off");

        public static string DiagnosticsLine(bool on) => on
            ? PlayerStrings.T("settings.diagnostics.on")
            : PlayerStrings.T("settings.diagnostics.off");

        /// <summary>
        /// D-119: the reminder's Settings line. Null availability = no notification support here.
        /// Calm and factual: no streak, count or guilt wording; it says how to turn it off.
        /// </summary>
        public static string ReminderLine(bool? wanted, ReminderPermission permission, int minutesOfDay)
        {
            if (wanted == null) return PlayerStrings.T("reminder.unavailable");
            if (wanted == false) return PlayerStrings.T("reminder.off");
            if (permission != ReminderPermission.Granted)
                return PlayerStrings.T("reminder.blocked");
            return PlayerStrings.F("reminder.on", ReminderTime(minutesOfDay));
        }

        /// <summary>"08:00" from minutes since midnight (device-local).</summary>
        public static string ReminderTime(int minutesOfDay)
        {
            int m = Math.Max(0, Math.Min(24 * 60 - 1, minutesOfDay));
            return (m / 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ":" + (m % 60).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>D-121: the reminder time moves in calm half-hour steps inside a daytime window (no night notes).</summary>
        public const int ReminderStepMinutes = 30;
        public const int ReminderEarliestMinutes = 6 * 60;
        public const int ReminderLatestMinutes = 21 * 60;

        /// <summary>
        /// D-121: the next reminder time one step earlier (direction &lt; 0) or later (&gt; 0). A stored
        /// time off the half-hour grid or outside the window is first snapped into it, so the result is
        /// always on the grid and inside 06:00–21:00. Returns the same time when no move in that direction is possible.
        /// </summary>
        public static int NextReminderTime(int minutesOfDay, int direction)
        {
            int snapped = (int)Math.Round(minutesOfDay / (double)ReminderStepMinutes, MidpointRounding.AwayFromZero) * ReminderStepMinutes;
            snapped = Math.Max(ReminderEarliestMinutes, Math.Min(ReminderLatestMinutes, snapped));
            int next = snapped + Math.Sign(direction) * ReminderStepMinutes;
            if (snapped != minutesOfDay && Math.Sign(snapped - minutesOfDay) == Math.Sign(direction)) next = snapped;
            next = Math.Max(ReminderEarliestMinutes, Math.Min(ReminderLatestMinutes, next));
            // Never move the "wrong" way: "later" from 23:00 must not offer 21:00.
            if (direction > 0 && next <= minutesOfDay) return minutesOfDay;
            if (direction < 0 && next >= minutesOfDay) return minutesOfDay;
            return next;
        }

        private void MoveReminder(int direction)
        {
            if (Reminders == null) { _error = PlayerStrings.T("reminder.error.unavailable"); return; }
            var s = _session.GetSettings();
            if (!ReminderWanted(s)) return;
            int next = NextReminderTime(s.DailyReminderMinutesOfDay, direction);
            if (next == s.DailyReminderMinutesOfDay) return;
            if (!Check(_session.SetDailyReminder(true, next))) return;
            _notice = PlayerStrings.F("reminder.moved", ReminderTime(next));
            SyncReminder();
        }

        /// <summary>D-119: the reminder is wanted only when BOTH the master opt-in and the reminder are on (default off).</summary>
        private static bool ReminderWanted(WalkGame.Application.ReadModels.SettingsReadModel s) => s.NotificationsOptIn && s.DailyReminderEnabled;

        private void ToggleReminder()
        {
            if (Reminders == null) { _error = PlayerStrings.T("reminder.error.unavailable"); return; }
            var s = _session.GetSettings();
            if (ReminderWanted(s))
            {
                Check(_session.SetDailyReminder(false, s.DailyReminderMinutesOfDay));
            }
            else
            {
                if (!Check(_session.SetNotificationsOptIn(true))) return;
                if (!Check(_session.SetDailyReminder(true, s.DailyReminderMinutesOfDay))) return;
                if (Reminders.Permission != ReminderPermission.Granted) Reminders.RequestPermission();
            }
            SyncReminder();
        }

        /// <summary>
        /// D-119: make the OS schedule match the preference. Called on boot, resume and toggle, so
        /// a permission granted (or revoked) in the OS takes effect the next time the game opens.
        /// Never schedules without both opt-ins and the OS permission.
        /// </summary>
        public void SyncReminder()
        {
            if (Reminders == null) return;
            try
            {
                var s = _session.GetSettings();
                if (ReminderWanted(s) && Reminders.Permission == ReminderPermission.Granted)
                    Reminders.ScheduleDaily(s.DailyReminderMinutesOfDay, ReminderCopy.Title, ReminderCopy.Body);
                else
                    Reminders.Cancel();
            }
            catch (Exception) { /* a delivery failure never blocks the game */ }
        }

        public ShellScreen Current { get; private set; } = ShellScreen.Onboarding;
        public StartStatus? LastStartStatus { get; private set; }

        /// <summary>
        /// D-126: one locale for content copy (session overlay, D-122) and shell strings
        /// (<see cref="PlayerStrings.Locale"/>). Presentation only; never saved. No player-facing
        /// language setting exists until real translations ship.
        /// </summary>
        public void SetLocale(WalkGame.Application.Content.ContentText locale)
        {
            var l = locale ?? WalkGame.Application.Content.ContentText.English;
            _session.ContentLocale = l;
            PlayerStrings.Locale = l;
        }

        // D-128: re-entrancy. Unity is single-threaded, but a platform callback (permission
        // dialog, OS pause/resume, a bridge that pumps messages) can call back into the shell
        // while a boot, resume or action is still running. One pipeline runs at a time: a
        // resume that arrives mid-pipeline is coalesced into ONE re-run afterwards, and an
        // action that arrives mid-pipeline (a double tap) is dropped.
        private bool _busy;
        private bool _resumePending;

        /// <summary>D-128: true while a boot, resume or action is running (tests and the bootstrap read it).</summary>
        public bool IsBusy => _busy;

        public void Boot()
        {
            if (_busy) { _resumePending = true; return; }
            _busy = true;
            try { Guarded(BootCore, bootFailure: true); }
            finally { _busy = false; }
            DrainPendingResume();
        }

        private void DrainPendingResume()
        {
            // At most one coalesced re-run; a resume during that re-run is dropped, never looped.
            if (!_resumePending) return;
            _resumePending = false;
            if (!_booted) return;
            _busy = true;
            try { Guarded(BootCore, bootFailure: true); }
            finally { _busy = false; _resumePending = false; }
        }

        /// <summary>
        /// D-128: the Unity-to-domain boundary. A persistence failure (the session throws
        /// IOException when a save cannot be committed), a missing state or a provider bridge
        /// exception must never escape into a UI Toolkit callback or MonoBehaviour message,
        /// where it would leave a half-drawn screen. The player gets one calm sentence; the
        /// committed save is untouched because the session never reports a failed commit as success.
        /// </summary>
        private void Guarded(Action body, bool bootFailure)
        {
            try { body(); }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException || ex is ArgumentException)
            {
                _notice = null;
                if (bootFailure && !_booted) { Current = ShellScreen.BootError; _notice = PlayerStrings.T("shell.thatCouldNotBe"); }
                else if (_session.HasUnsavedChanges) _error = PlayerStrings.T("shell.notSavedYet"); // D-131
                else _error = PlayerStrings.T("shell.thatCouldNotBe");
            }
        }

        private void BootCore()
        {
            // D-131: a commit failed earlier and the in-memory state is ahead of disk. Retry
            // first; reloading now would silently drop that progress. If storage is still
            // failing, keep the in-memory world (nothing is credited twice: the next good
            // commit writes the whole state once) and say so calmly.
            if (_booted && _session.HasUnsavedChanges && !_session.RetryPendingSave())
            {
                // D-134: still no reload, but offline time is caught up in memory (exactly
                // once; LastAdvancedUtc is monotonic) and a return summary is still shown.
                _notice = null;
                bool saved = _session.CatchUpWithoutReload();
                _error = saved ? null : PlayerStrings.T("shell.notSavedYet");
                if (OnboardingIncomplete()) Current = ShellScreen.Guide;
                else if (_session.GetPendingReturnSummary() != null) Current = ShellScreen.ReturnSummary;
                return;
            }
            var result = _session.Continue();
            LastStartStatus = result.Status;
            var (screen, notice) = BootRouter.Route(result.Status);
            _booted = screen == ShellScreen.Home;
            _notice = notice;
            _error = null;
            Current = screen;
            if (_booted) SyncLiveSource();
            if (_booted) SyncReminder();
            if (_booted && _session.HasUnsavedChanges) _error = PlayerStrings.T("shell.notSavedYet"); // D-131: a sync credit could not be committed.
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
            if (_busy) return; // D-128: a second tap while the first is still running is dropped.
            _busy = true;
            try { Guarded(() => InvokeCore(actionId), bootFailure: false); }
            finally { _busy = false; }
            DrainPendingResume();
        }

        private void InvokeCore(string actionId)
        {
            _error = null;
            if (string.IsNullOrEmpty(actionId)) { _error = PlayerStrings.T("shell.unknownAction"); return; }
            if (actionId.StartsWith("nav:", StringComparison.Ordinal))
            {
                _notice = null;
                // C (D-084): a malformed or unknown target, or a game screen before a game is
                // loaded, is a calm error on the current screen, never an exception.
                string target = actionId.Substring(4);
                // Exact names only: Enum.TryParse also accepts numbers and comma lists ("Home,Story"
                // parses to the flags OR of the two values, which is another real screen).
                if (Array.IndexOf(Enum.GetNames(typeof(ShellScreen)), target) < 0 || !Enum.TryParse(target, false, out ShellScreen screen))
                { _error = PlayerStrings.T("shell.thatScreenDoesNot"); return; }
                if (!_booted && screen != ShellScreen.Onboarding && screen != ShellScreen.BootError)
                { _error = PlayerStrings.T("shell.startOrContinueA"); return; }
                // D-110: BootError describes a save that could not be opened. With a game loaded
                // that is false, so the shell refuses it calmly and stays where it is.
                if (_booted && screen == ShellScreen.BootError)
                { _error = PlayerStrings.T("shell.yourWorldIsOpen"); return; }
                Current = screen;
                return;
            }
            if (!_booted && actionId != "start" && actionId != "retry" && actionId != "activity:connect")
            { _error = PlayerStrings.T("shell.startOrContinueA"); return; }
            if (actionId.StartsWith("queue:", StringComparison.Ordinal)) { Enqueue(actionId.Substring(6)); return; }
            if (actionId.StartsWith("up:", StringComparison.Ordinal)) { MoveUp(actionId.Substring(3)); return; }
            switch (actionId)
            {
                case "start":
                    // C (D-084): StartNewGame overwrites unconditionally, so only the onboarding
                    // screen of an unbooted shell may start. A loaded game or an unreadable save
                    // (BootError) is never replaced from here.
                    if (_booted || Current != ShellScreen.Onboarding) { _error = PlayerStrings.T("shell.aGameIsAlready"); return; }
                    // D-085: the Application layer refuses too, so a save the boot did not
                    // see (or could not read) is never replaced from the shell.
                    var started = _session.StartNewGame(_seedSource(), overwriteExisting: false);
                    LastStartStatus = started.Status;
                    if (started.Status == StartStatus.NewGameCreated)
                    {
                        _booted = true;
                        Check(_session.AdvanceOnboarding(OnboardingStage.WorldBaseline));
                        Current = OnboardingIncomplete() ? ShellScreen.Guide : ShellScreen.Home;
                    }
                    else if (started.Status == StartStatus.ExistingSaveRefused) _error = PlayerStrings.T("shell.aGameIsAlready");
                    else _error = PlayerStrings.T("shell.weCouldnTStart");
                    return;
                case "retry": BootCore(); return;
                case "activity:connect":
                    if (_requestPermission == null) { _error = PlayerStrings.T("shell.walkingDataCanT"); return; }
                    _requestPermission();
                    return;
                case "guide:next": GuideNext(); return;
                case "ack": if (Check(_session.AcknowledgeReturnSummary())) Current = ShellScreen.Home; return;
                case "toggle:reducedMotion": Check(_session.SetReducedMotion(!_session.GetSettings().ReducedMotion)); return;
                case "toggle:sound": Check(_session.SetSoundEnabled(!_session.GetSettings().SoundEnabled)); return;
                case "toggle:reminder": ToggleReminder(); return;
                case "reminder:earlier": MoveReminder(-1); return;
                case "reminder:later": MoveReminder(1); return;
                case "toggle:diagnostics": Check(_session.SetDiagnosticsVisible(!_session.GetSettings().DiagnosticsVisible)); return;
                case "export": Export(); return;
                case "delete:request": Current = ShellScreen.DeleteConfirm; return;
                case "delete:cancel": Current = ShellScreen.Settings; return;
                case "delete:confirm":
                    var erased = _session.DeleteAllLocalData();
                    if (erased.Status == LocalDataEraseStatus.Erased)
                    {
                        _booted = false; _notice = PlayerStrings.T("shell.allLocalDataWas"); Current = ShellScreen.Onboarding;
                    }
                    else { Current = ShellScreen.Settings; _error = PlayerStrings.T("shell.yourDataCouldNot"); }
                    return;
                case "dev:walk":
                    if (!_devTools) { _error = PlayerStrings.T("shell.developmentToolsAreDisabled"); return; }
                    var now = DateTimeOffset.UtcNow;
                    _session.IngestFromSource(new SyntheticWalkingSource(6000), now.AddDays(-1), now);
                    return;
                default: _error = PlayerStrings.T("shell.unknownAction"); return;
            }
        }

        /// <summary>
        /// Pulls the live provider (Health Connect, D-068) through the unchanged trust pipeline.
        /// Only when permission is granted; a failed fetch is already recorded durably by the
        /// session (D-063) and never blocks the shell.
        /// </summary>
        private void SyncLiveSource()
        {
            if (_liveSource == null || !_session.GetActivityStatus().PermissionGranted) return;
            var now = DateTimeOffset.UtcNow;
            try { _session.IngestFromSource(_liveSource, now.AddDays(-SyncWindowDays), now); }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException) { }
            // D-128: any other bridge exception (a JNI AndroidJavaException, a malformed record
            // surfacing as ArgumentException) must not abort the boot either; the game opens on
            // its last committed state and the next resume retries.
            catch (Exception) { }
        }

        /// <summary>D-128: a double-tapped "Start" on a project that is already queued or active is a no-op, not an error.</summary>
        private void Enqueue(string projectId)
        {
            var home = _session.GetHome();
            if (home.ActiveProjectId == projectId || home.Queued.Any(q => q.ProjectId == projectId)) return;
            Check(_session.EnqueueProject(projectId));
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
            // D-096: domain error text is for developers (it can echo ids such as 'proj.x');
            // the player sees one calm sentence.
            _error = PlayerStrings.T("shell.thatCouldNotBe");
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
                case ShellScreen.Story: return Story();
                case ShellScreen.Discoveries: return Discoveries();
                case ShellScreen.Progress: return Progress();
                case ShellScreen.Privacy: return Privacy();
                default: throw new InvalidOperationException("Unmapped screen " + Current); // l10n-ignore: developer exception, never shown
            }
        }

        private ScreenModel With(ShellScreen s, string title, ScreenState normal = ScreenState.Normal)
            => new ScreenModel(s, _error != null ? ScreenState.Error : normal, title) { Notice = _error };

        private ScreenModel Onboarding()
        {
            var m = With(ShellScreen.Onboarding, PlayerStrings.T("shell.yourMovementRestoresThis"));
            m.Lines.Add(PlayerStrings.T("shell.aForgottenValleyIs"));
            m.Lines.Add(PlayerStrings.T("shell.nothingIsLostWhile"));
            m.Actions.Add(new ShellAction("start", PlayerStrings.T("shell.beginRestoring")));
            return m;
        }

        private ScreenModel Home()
        {
            var home = _session.GetHome();
            var glance = _session.GetGlance();
            bool empty = home.ActiveProjectId == null && home.Queued.Count == 0;
            var m = With(ShellScreen.Home, PlayerStrings.T("label.home"), empty ? ScreenState.Empty : ScreenState.Normal);
            // Region 1 polish (grok0215): a brand-new world has no "last time", so "Nothing has changed
            // since you were last here" reads oddly on first run; skip it until anything has happened.
            bool firstRun = empty && !home.HasPendingSummary && home.CompletedProjects == 0 && home.Vitality == 0
                && home.AttentionReason == HomeAttentionReason.NoProjectStartedYet;
            if (!firstRun) m.Lines.Add(glance.WhatChangedSentence);
            // D-106: the empty-state line below already says nothing is being restored; skip the duplicate.
            // D-149: once the active project is funded, the D-145 payoff line names it with its percent
            // and steps to go, so "X is underway (n of m)" would say the same thing a third time.
            if (!empty && glance.PayoffSentence == null) m.Lines.Add(glance.RestoringNowSentence);
            // D-145: first-session payoff. Any funded project shows its percent and the distance to
            // the next visible change, so even a 300-step walk visibly moves something.
            if (glance.PayoffSentence != null) m.Lines.Add(glance.PayoffSentence);
            // The empty-state line also says "Choose a project to begin", so the glance's
            // "Choose the first thing to restore." would say it twice.
            if (glance.AttentionSentence != null && !(empty && home.AttentionReason == HomeAttentionReason.NoProjectStartedYet))
                m.Lines.Add(glance.AttentionSentence);
            m.Lines.Add(glance.ActivitySentence);
            m.Lines.Add(HomeStoryLine(_session.GetStory()));
            m.Lines.Add(PlayerStrings.F("label.resources", home.Vitality, home.Materials, home.Knowledge));
            if (empty) m.Lines.Add(PlayerStrings.T("shell.nothingIsBeingRestored"));
            if (home.HasPendingSummary) m.Actions.Add(new ShellAction("nav:ReturnSummary", PlayerStrings.T("shell.whileYouWereAway")));
            m.Actions.Add(new ShellAction("nav:Projects", PlayerStrings.T("label.projects")));
            m.Actions.Add(new ShellAction("nav:Region", PlayerStrings.T("shell.theValley")));
            m.Actions.Add(new ShellAction("nav:Story", PlayerStrings.T("label.story")));
            m.Actions.Add(new ShellAction("nav:Progress", PlayerStrings.T("shell.yourProgress")));
            m.Actions.Add(new ShellAction("nav:ActivityStatus", PlayerStrings.T("label.activity")));
            m.Actions.Add(new ShellAction("nav:Settings", PlayerStrings.T("label.settings")));
            if (_devTools) m.Actions.Add(new ShellAction("dev:walk", PlayerStrings.T("shell.devSimulateADay")));
            m.Lines.RemoveAll(string.IsNullOrEmpty);
            // D-106: the glance can repeat a sentence (what changed == attention); show each line once.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            m.Lines.RemoveAll(l => !seen.Add(l));
            return m;
        }

        private ScreenModel Summary()
        {
            var s = _session.GetPendingReturnSummary();
            var m = With(ShellScreen.ReturnSummary, PlayerStrings.T("shell.whileYouWereAway"), s == null ? ScreenState.Empty : ScreenState.Normal);
            if (s == null) m.Lines.Add(PlayerStrings.T("shell.nothingNewSinceYour"));
            else
            {
                // UX_DESIGN §4/§19: lead with transformation; restoration moments are shown inline on
                // the valley picture (static, skippable by simply continuing), never as a blocking sequence.
                var moments = s.Items.Where(i => i.Kind == WalkGame.Domain.Summaries.SummaryItemKind.Transformation && i.IsCurrent)
                                     .Select(i => i.Text).ToList();
                if (moments.Count > 0)
                {
                    m.Scene = RegionScene.Build(_session.GetRegion(), moments);
                    m.Lines.Add(m.Scene.ChangedCount > 0 ? PlayerStrings.T("shell.theValleyChangedWhile") : PlayerStrings.T("shell.yourWalksChangedThe"));
                }
                // D-100: a finished project gets one calm follow-up line naming the landmark stage it
                // brought and its storyline chapter (derived from authored content; stored text unchanged).
                var content = WalkGame.Application.Content.Region1Catalog.Create();
                var unlocked = new HashSet<string>(_session.GetStory().Chapters.Where(c => c.Unlocked).Select(c => c.ChapterId));
                foreach (var item in s.Items)
                {
                    m.Lines.Add(item.IsCurrent ? item.DisplayText : PlayerStrings.F("label.earlier", item.DisplayText));
                    if (!item.IsCurrent) continue;
                    var detail = WalkGame.Application.Summaries.ProjectCompletionMoments.Describe(content, item.Text, unlocked);
                    if (detail != null) m.Lines.Add(detail);
                }
                // D-145: after any credit, say what it bought ("4% restored, about N steps to ...")
                // instead of leaving the player with only "Vitality credited: N".
                if (s.HasCurrentItems)
                {
                    var payoff = _session.GetGlance().PayoffSentence;
                    if (payoff != null) m.Lines.Add(payoff);
                }
            }
            bool celebrating = s != null && Celebrate(m, s.Items.Where(i => i.IsCurrent).Select(i => i.Text));
            if (s != null)
            {
                bool chapter = s.Items.Any(i => i.IsCurrent && i.Text.StartsWith(ChapterMomentPrefix, StringComparison.Ordinal));
                bool restored = s.Items.Any(i => i.IsCurrent && i.Kind == WalkGame.Domain.Summaries.SummaryItemKind.Transformation);
                m.Cue = SoundCues.ForSummary(_session.GetSettings().SoundEnabled, celebrating, chapter, restored);
            }
            if (s != null) m.Actions.Add(new ShellAction("ack", PlayerStrings.T("label.continue")));
            if (celebrating) m.Actions.Add(new ShellAction("nav:Region", PlayerStrings.T("shell.seeTheRestoredValley")));
            // D-076: a chapter moment is a Transformation item; offer the journal after the primary Continue.
            if (s != null && s.Items.Any(i => i.IsCurrent && i.Text.StartsWith(ChapterMomentPrefix, StringComparison.Ordinal)))
                m.Actions.Add(new ShellAction("nav:Story", PlayerStrings.T("shell.readTheNewChapter")));
            m.Actions.Add(new ShellAction("nav:Home", PlayerStrings.T("label.home")));
            return m;
        }

        private ScreenModel Projects()
        {
            var p = _session.GetProjects();
            var open = p.Projects.Where(r => r.Status != ProjectStatus.Locked && r.Status != ProjectStatus.Completed).ToList();
            bool anyQueued = p.Projects.Any(r => r.Status == ProjectStatus.Queued || r.Status == ProjectStatus.Active);
            var m = With(ShellScreen.Projects, PlayerStrings.T("label.projects"), open.Count == 0 ? ScreenState.Empty : ScreenState.Normal);
            // UX_DESIGN §17: empty states explain the next useful action.
            if (open.Count == 0)
                m.Lines.Add(p.Projects.All(r => r.Status == ProjectStatus.Completed)
                    ? PlayerStrings.T("shell.everyRestorationInThis")
                    : PlayerStrings.T("shell.noProjectsAreOpen"));
            else if (!anyQueued)
                m.Lines.Add(PlayerStrings.T("shell.nothingIsQueuedChoose"));
            // Mid/late-game polish (D-127): what the player can act on comes first (being restored,
            // waiting, ready), restored projects last, so the options are not buried under history.
            // A restored row needs no "(300 of 300 vitality)".
            foreach (var r in p.Projects.Where(r => r.Status != ProjectStatus.Locked)
                         .OrderBy(r => ProjectsOrder(r.Status)).ThenBy(r => r.QueuedPosition ?? int.MaxValue))
            {
                string title = PlayerCopy.Title(r.TitleKey);
                m.Lines.Add(r.Status == ProjectStatus.Completed
                    ? title + " — " + PlayerCopy.ProjectStatus(r.Status, r.QueuedPosition)
                    : title + " — " + PlayerCopy.ProjectStatus(r.Status, r.QueuedPosition) + " (" + r.VitalityInvested + " of " + r.VitalityCost + " vitality)");
                if (r.Status == ProjectStatus.Available) m.Actions.Add(new ShellAction("queue:" + r.ProjectId, QueueLabel(title)));
                if (r.Status == ProjectStatus.Queued && r.QueuedPosition > 0) m.Actions.Add(new ShellAction("up:" + r.ProjectId, PlayerStrings.F("label.moveUp", title)));
            }
            int locked = p.Projects.Count(r => r.Status == ProjectStatus.Locked);
            if (locked > 0) m.Lines.Add(PlayerStrings.F(locked == 1 ? "shell.projects.locked.one" : "shell.projects.locked.many", locked));
            m.Actions.Add(new ShellAction("nav:Home", PlayerStrings.T("label.home")));
            return m;
        }

        private static int ProjectsOrder(ProjectStatus s) =>
            s == ProjectStatus.Active ? 0 : s == ProjectStatus.Queued ? 1 : s == ProjectStatus.Available ? 2 : 3;

        /// <summary>UX_DESIGN §9 compact region status: landmarks as phrases, producers, closure.</summary>
        private ScreenModel Region()
        {
            var r = _session.GetRegion();
            var m = With(ShellScreen.Region, PlayerStrings.T("shell.theValley"));
            m.Scene = RegionScene.Build(r);
            if (r.RegionCompleted)
                m.Lines.Add(PlayerStrings.T("shell.theValleyIsFully"));
            else
                m.Lines.Add(PlayerStrings.F("shell.region.finished", r.CompletedProjects, r.TotalProjects));
            foreach (var l in r.Landmarks) m.Lines.Add(GlancePhrases.FormatLandmark(PlayerCopy.Title(l.TitleKey), l.Stage));
            foreach (var pr in r.Producers)
                m.Lines.Add(PlayerCopy.Title(pr.TitleKey) + ": " + (!pr.Unlocked
                    ? PlayerStrings.T("shell.notRunningYetRestore")
                    : pr.CapacityLimited ? PlayerStrings.T("shell.storageFullHoldingWhat") : PlayerStrings.T("shell.producingWhileYouAre")));
            m.Lines.Add(r.DiscoveriesUnlocked == 0
                ? PlayerStrings.T("shell.noDiscoveriesYetWalks")
                : PlayerStrings.F(r.DiscoveriesUnlocked == 1 ? "shell.region.discoveries.one" : "shell.region.discoveries.many", r.DiscoveriesUnlocked));
            m.Lines.Add(r.ExpeditionsAvailable == 0 && r.ExpeditionsCompleted == 0
                ? PlayerStrings.T("shell.noExpeditionRoutesAre")
                : PlayerStrings.F("shell.region.expeditions", r.ExpeditionsAvailable, r.ExpeditionsCompleted));
            m.Actions.Add(new ShellAction("nav:Discoveries", PlayerStrings.T("shell.discoveriesAndExpeditions")));
            m.Actions.Add(new ShellAction("nav:Home", PlayerStrings.T("label.home")));
            return m;
        }

        /// <summary>
        /// Discoveries and expeditions detail (B2, D-082). Found discoveries show title, kind and
        /// when they were found; unfound ones stay unnamed so nothing is spoiled. Expedition routes
        /// show status, what opens them (in player words) and their reward. Read-only: viewing
        /// never changes progression (GAME_SYSTEMS §7).
        /// </summary>
        private ScreenModel Discoveries()
        {
            var d = _session.GetDiscoveries();
            var e = _session.GetExpeditions();
            bool empty = d.UnlockedCount == 0 && e.AvailableCount == 0 && e.CompletedCount == 0;
            var m = With(ShellScreen.Discoveries, PlayerStrings.T("shell.discoveriesAndExpeditions"), empty ? ScreenState.Empty : ScreenState.Normal);
            m.Lines.Add(PlayerStrings.F("label.discoveriesFound", d.UnlockedCount, d.TotalDiscoveries));
            if (d.UnlockedCount == 0) m.Lines.Add(PlayerStrings.T("shell.nothingFoundYetRestoring"));
            foreach (var row in d.Discoveries.Where(x => x.Unlocked).OrderBy(x => x.DiscoveredAtUtc))
            {
                m.Lines.Add(PlayerCopy.Title(row.TitleKey) + " · " + PlayerCopy.DiscoveryCategory(row.Category)
                    + (row.DiscoveredAtUtc.HasValue ? " · found " + row.DiscoveredAtUtc.Value.UtcDateTime.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) : "")); // l10n-ignore: date format pattern, not copy
                // D-086/D-122: authored body and origin copy via the overlay; a key without copy is skipped, never shown raw.
                var body = _session.DiscoveryCopy(row.DiscoveryId, WalkGame.Application.Content.ContentText.Fields.Body, row.BodyKey);
                if (body != null) m.Lines.Add(body);
                var origin = _session.DiscoveryCopy(row.DiscoveryId, WalkGame.Application.Content.ContentText.Fields.Provenance, row.ProvenanceKey);
                if (origin != null) m.Lines.Add(PlayerStrings.F("shell.discoveries.origin", origin));
            }
            int hidden = d.TotalDiscoveries - d.UnlockedCount;
            if (hidden > 0) m.Lines.Add(PlayerStrings.F(hidden == 1 ? "shell.discoveries.hidden.one" : "shell.discoveries.hidden.many", hidden));
            // D-118: Nell's letters are listed with the discoveries but read in the Story journal.
            var letters = _session.GetLetters();
            if (letters.FoundCount > 0) m.Lines.Add(LettersCountLine(letters) + " " + PlayerStrings.T("letters.inJournal"));

            m.Lines.Add(PlayerStrings.F("shell.discoveries.expeditions", e.AvailableCount, e.CompletedCount, e.TotalExpeditions));
            var projects = _session.GetProjects().Projects.ToDictionary(p => p.ProjectId, p => PlayerCopy.Title(p.TitleKey));
            var landmarks = _session.GetRegion().Landmarks.ToDictionary(l => l.LandmarkId, l => PlayerCopy.Title(l.TitleKey));
            foreach (var x in e.Expeditions)
            {
                string reward = x.RewardType.HasValue && x.RewardUnits > 0 ? " Reward: +" + x.RewardUnits + " " + x.RewardType.Value + "." : "";
                switch (x.Status)
                {
                    case ExpeditionsReadModel.ExpeditionStatus.Completed:
                        m.Lines.Add(PlayerCopy.Title(x.TitleKey) + " · completed." + reward); break;
                    case ExpeditionsReadModel.ExpeditionStatus.Available:
                        m.Lines.Add(PlayerStrings.F("shell.expedition.open", PlayerCopy.Title(x.TitleKey)) + reward); break;
                    default:
                        var needs = x.RequiredProjectIds.Select(id => projects.TryGetValue(id, out var t) ? t : GlancePhrases.UntitledProject)
                            .Concat(x.RequiredStageKeys.Select(k => StageNeed(k, landmarks))).ToList();
                        m.Lines.Add(PlayerStrings.F("shell.expedition.locked", PlayerCopy.Title(x.TitleKey), needs.Count == 0 ? PlayerStrings.T("shell.moreRestoration") : string.Join(", ", needs)) + reward);
                        break;
                }
            }
            m.Actions.Add(new ShellAction("nav:Region", PlayerStrings.T("shell.theValley")));
            m.Actions.Add(new ShellAction("nav:Home", PlayerStrings.T("label.home")));
            return m;
        }

        /// <summary>"lm.canopy@Flourishing" → "Canopy flourishing"; unknown ids never leak raw.</summary>
        public static string StageNeed(string key, IReadOnlyDictionary<string, string> landmarks)
        {
            int at = key == null ? -1 : key.LastIndexOf('@');
            if (at <= 0) return PlayerStrings.T("shell.moreRestoration");
            string name = landmarks.TryGetValue(key.Substring(0, at), out var t) ? t : "a landmark";
            return name + " " + key.Substring(at + 1).ToLowerInvariant();
        }

        /// <summary>Suffix the composer uses for the region-complete moment (ReturnSummaryComposer, RegionCompleted).</summary>
        public const string RegionCompleteSuffix = "'s restoration is complete."; // l10n-ignore: matches Application summary copy; moves with the Application-layer table

        /// <summary>Celebration headline for Region 1 completion (D-080).</summary>
        public static string CelebrationHeadline => PlayerStrings.T("shell.celebration.headline");

        /// <summary>
        /// Region 1 completion celebration (D-080): when the summary carries the region-complete
        /// moment, the screen is retitled and leads with a calm, static headline. No animation,
        /// no new state; it is shown once because the summary itself is acknowledged durably.
        /// Returns true when the celebration applied.
        /// </summary>
        public static bool Celebrate(ScreenModel m, IEnumerable<string> currentItemTexts)
        {
            if (m == null || currentItemTexts == null) return false;
            if (!currentItemTexts.Any(t => t != null && t.EndsWith(RegionCompleteSuffix, StringComparison.Ordinal))) return false;
            m.Title = PlayerStrings.T("shell.theValleyIsRestored");
            m.Lines.Insert(0, CelebrationHeadline);
            m.Lines.Insert(1, PlayerStrings.T("shell.everyPlaceYouBrought"));
            return true;
        }

        /// <summary>Home's one-line story pointer once every chapter is unlocked (the epilogue line).</summary>
        public static string StoryEpilogueLine => PlayerStrings.T("shell.story.epilogue");

        /// <summary>
        /// D-090: one line on Home linking play to the story. Names the latest unlocked chapter and
        /// the next chapter's gentle hint (the same hint the journal shows, so nothing is spoiled);
        /// once the story is fully read it shows the epilogue line. Shell-only: no content or save change.
        /// </summary>
        public static string HomeStoryLine(WalkGame.Application.ReadModels.StoryReadModel story)
        {
            if (story == null || story.TotalChapters == 0) return string.Empty;
            if (story.UnlockedCount >= story.TotalChapters || story.NextHintKey == null) return StoryEpilogueLine;
            var latest = story.Chapters.LastOrDefault(c => c.Unlocked);
            string where = latest == null ? PlayerStrings.T("shell.theStoryBeginsSoon")
                : (latest.Number == 0 ? PlayerStrings.F("label.prologue", latest.TitleKey) : PlayerStrings.F("label.chapter", latest.Number, latest.TitleKey)) + ".";
            return PlayerStrings.F("label.storyLine", where, story.NextHintKey);
        }

        /// <summary>Prefix the composer uses for chapter moments (ReturnSummaryComposer, D-076).</summary>
        public const string ChapterMomentPrefix = "New chapter: "; // l10n-ignore: matches Application summary copy; moves with the Application-layer table

        /// <summary>
        /// Story journal (D-076). Unlocked chapters show title and text in reading order; the
        /// next locked chapter shows its gentle hint; later chapters show only their titles so
        /// the journal never spoils the story and never uses guilt language.
        /// </summary>
        /// <summary>
        /// The Story journal's first line. Once the valley is restored the remaining chapter comes
        /// from tending, not restoring, so the hint must not say "restoring" (D-133, epilogue polish).
        /// </summary>
        public static string StoryProgressLine(int unlocked, int total, bool regionCompleted) =>
            unlocked >= total ? PlayerStrings.T("shell.youHaveReadThe")
            : regionCompleted ? PlayerStrings.F("shell.story.progressTending", unlocked, total)
            : PlayerStrings.F("shell.story.progress", unlocked, total);

        private ScreenModel Story()
        {
            var story = _session.GetStory();
            var m = With(ShellScreen.Story, PlayerStrings.T("label.story"));
            m.Lines.Add(StoryProgressLine(story.UnlockedCount, story.TotalChapters, _session.GetRegion().RegionCompleted));
            bool hinted = false;
            foreach (var c in story.Chapters)
            {
                string heading = c.Number == 0 ? PlayerStrings.F("label.prologue", c.TitleKey) : PlayerStrings.F("label.chapter", c.Number, c.TitleKey);
                if (c.Unlocked)
                {
                    m.Lines.Add(heading);
                    if (!string.IsNullOrWhiteSpace(c.BodyKey)) m.Lines.Add(c.BodyKey!);
                }
                else if (!hinted)
                {
                    hinted = true;
                    m.Lines.Add(heading + " (next). " + c.HintKey);
                }
                else
                {
                    m.Lines.Add(heading + " (ahead)");
                }
            }
            // D-118: optional letters found at restored sites. Shown only once one is found, in
            // the order they were written; unfound letters are never listed or hinted at.
            var letters = _session.GetLetters();
            if (letters.FoundCount > 0)
            {
                m.Lines.Add(LettersCountLine(letters));
                foreach (var l in letters.Letters.Where(x => x.Found && x.Body != null).OrderBy(x => x.Order))
                    m.Lines.Add(LetterLine(l));
            }
            m.Lines.AddRange(EpilogueLines(_session.GetEpilogue()));
            m.Actions.Add(new ShellAction("nav:Home", PlayerStrings.T("label.home")));
            return m;
        }

        /// <summary>
        /// D-129: the Region 1 epilogue in the Story journal, only after completion: the final
        /// journal entry, Nell's closing letters, "in the valley now" and earlier valley notes.
        /// No counts or "N of M" (nothing to collect), no Region 2 hook.
        /// </summary>
        public static System.Collections.Generic.List<string> EpilogueLines(WalkGame.Application.ReadModels.EpilogueReadModel e)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (e == null || !e.Available) return lines;
            lines.Add(PlayerStrings.T("epilogue.heading"));
            if (!string.IsNullOrWhiteSpace(e.JournalEntry)) lines.Add(e.JournalEntry!);
            foreach (var l in e.ClosingLetters) if (!string.IsNullOrWhiteSpace(l)) lines.Add(PlayerStrings.F("epilogue.letter", WalkGame.Application.Content.Region1Letters.Writer, l));
            if (!string.IsNullOrWhiteSpace(e.CurrentNote)) lines.Add(PlayerStrings.F("epilogue.now", e.CurrentNote!));
            var earlier = e.ValleyNotes.Where(n => !string.IsNullOrWhiteSpace(n) && n != e.CurrentNote).ToList();
            if (earlier.Count > 0)
            {
                lines.Add(PlayerStrings.T("epilogue.earlier"));
                lines.AddRange(earlier);
            }
            return lines;
        }

        /// <summary>D-118: "Letters from Nell Hartley: 2 of 6 found."</summary>
        public static string LettersCountLine(WalkGame.Application.ReadModels.LettersReadModel letters) =>
            PlayerStrings.F("letters.count", WalkGame.Application.Content.Region1Letters.Writer, letters.FoundCount, letters.TotalLetters);

        /// <summary>D-118: one found letter, with where it turned up.</summary>
        public static string LetterLine(WalkGame.Application.ReadModels.LettersReadModel.LetterRow l) =>
            PlayerStrings.F("letters.line", l.Place, l.Body);

        /// <summary>
        /// D-099: a calm look back at everything walking has done. Read-only; built from the
        /// existing read models (lifetime credited Vitality, finished restorations, discoveries,
        /// chapters). Deliberately no streaks, goals or per-day comparison.
        /// </summary>
        private ScreenModel Progress()
        {
            var home = _session.GetHome();
            var diag = _session.GetDiagnostics();
            var disc = _session.GetDiscoveries();
            var story = _session.GetStory();
            long vitality = Math.Max(0L, diag.LifetimeVitalityCredited);
            bool empty = vitality == 0 && home.CompletedProjects == 0;
            var m = With(ShellScreen.Progress, PlayerStrings.T("shell.yourProgress"), empty ? ScreenState.Empty : ScreenState.Normal);
            foreach (var line in ProgressLines(vitality, home.CompletedProjects, home.TotalProjects, disc.UnlockedCount, disc.TotalDiscoveries,
                         story.UnlockedCount, story.TotalChapters, home.RegionComplete))
                m.Lines.Add(line);
            // D-103: days walked, before the closing reassurance line; "at least" when the
            // count is a proven lower bound (migrated save, or walks without a local date).
            var days = _session.GetDaysWalked();
            string daysLine = DaysWalkedLine(days.Days, days.IsLowerBound);
            if (daysLine != null) m.Lines.Insert(Math.Max(0, m.Lines.Count - 1), daysLine);
            m.Actions.Add(new ShellAction("nav:Home", PlayerStrings.T("label.home")));
            return m;
        }

        /// <summary>D-103 days-walked copy; null when no day is known yet (the empty-state line covers it).</summary>
        public static string DaysWalkedLine(int days, bool isLowerBound)
        {
            if (days <= 0) return null;
            string key = isLowerBound ? (days == 1 ? "shell.progress.daysAtLeast.one" : "shell.progress.daysAtLeast.many")
                                      : (days == 1 ? "shell.progress.days.one" : "shell.progress.days.many");
            return PlayerStrings.F(key, days);
        }

        /// <summary>D-099 Progress copy, pure so every state can be checked without a session.</summary>
        public static System.Collections.Generic.List<string> ProgressLines(long lifetimeVitality, int projectsDone, int projectsTotal,
            int discoveriesFound, int discoveriesTotal, int chaptersRead, int chaptersTotal, bool regionComplete)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var lines = new System.Collections.Generic.List<string>();
            if (lifetimeVitality <= 0 && projectsDone == 0)
            {
                lines.Add(PlayerStrings.T("shell.yourFirstWalkWill"));
            }
            else
            {
                long steps = lifetimeVitality * WalkGame.Domain.Activity.StepConversionRuleV1.StepsPerVitality;
                lines.Add(PlayerStrings.F("shell.progress.vitality", lifetimeVitality.ToString("N0", ci)));
                lines.Add(PlayerStrings.F("shell.progress.steps", steps.ToString("N0", ci)));
            }
            lines.Add(regionComplete
                ? PlayerStrings.F("shell.progress.projects.complete", projectsDone, projectsTotal)
                : PlayerStrings.F("shell.progress.projects", projectsDone, projectsTotal));
            lines.Add(PlayerStrings.F("shell.progress.discoveries", discoveriesFound, discoveriesTotal));
            lines.Add(PlayerStrings.F("shell.progress.chapters", chaptersRead, chaptersTotal));
            lines.Add(PlayerStrings.T("shell.nothingHereExpiresEvery"));
            return lines;
        }

        private ScreenModel Settings()
        {
            var s = _session.GetSettings();
            var m = With(ShellScreen.Settings, PlayerStrings.T("label.settings"));
            m.Lines.Add(ReducedMotionLine(s.ReducedMotion));
            m.Lines.Add(SoundLine(s.SoundEnabled));
            m.Lines.Add(DiagnosticsLine(s.DiagnosticsVisible));
            bool reminderOn = ReminderWanted(s);
            m.Lines.Add(ReminderLine(Reminders == null ? (bool?)null : reminderOn, Reminders?.Permission ?? ReminderPermission.NotGranted, s.DailyReminderMinutesOfDay));
            float raw;
            try { raw = TextScaleSource?.Invoke() ?? 1f; } catch (Exception) { raw = 1f; }
            m.Lines.Add(TextSizeLine(raw));
            m.TextPreview = TextPreviewSample;
            m.Actions.Add(new ShellAction("toggle:reducedMotion", PlayerStrings.T(s.ReducedMotion ? "shell.settings.motion.off" : "shell.settings.motion.on")));
            m.Actions.Add(new ShellAction("toggle:sound", PlayerStrings.T(s.SoundEnabled ? "shell.settings.sound.off" : "shell.settings.sound.on")));
            if (Reminders != null)
            {
                m.Actions.Add(new ShellAction("toggle:reminder", reminderOn ? PlayerStrings.T("reminder.action.off") : PlayerStrings.T("reminder.action.on")));
                if (reminderOn)
                {
                    // D-121: plain buttons that name the resulting time, so a screen reader announces it.
                    int earlier = NextReminderTime(s.DailyReminderMinutesOfDay, -1), later = NextReminderTime(s.DailyReminderMinutesOfDay, 1);
                    if (earlier != s.DailyReminderMinutesOfDay) m.Actions.Add(new ShellAction("reminder:earlier", PlayerStrings.F("reminder.action.earlier", ReminderTime(earlier))));
                    if (later != s.DailyReminderMinutesOfDay) m.Actions.Add(new ShellAction("reminder:later", PlayerStrings.F("reminder.action.later", ReminderTime(later))));
                }
            }
            m.Actions.Add(new ShellAction("toggle:diagnostics", PlayerStrings.T(s.DiagnosticsVisible ? "label.diagnostics.hide" : "label.diagnostics.show")));
            m.Actions.Add(new ShellAction("nav:Privacy", PlayerStrings.T("shell.privacyAndYourData")));
            m.Actions.Add(new ShellAction("delete:request", PlayerStrings.T("shell.deleteAllLocalData")));
            m.Actions.Add(new ShellAction("nav:Home", PlayerStrings.T("label.home")));
            return m;
        }

        private ScreenModel DeleteConfirm()
        {
            var m = With(ShellScreen.DeleteConfirm, PlayerStrings.T("shell.delete.title"));
            m.Lines.Add(PlayerStrings.T("shell.thisPermanentlyRemovesYour"));
            m.Actions.Add(new ShellAction("delete:cancel", PlayerStrings.T("shell.keepMyData")));
            m.Actions.Add(new ShellAction("delete:confirm", PlayerStrings.T("shell.deleteEverything")));
            return m;
        }

        private ScreenModel Activity()
        {
            var a = _session.GetActivityStatus();
            var m = With(ShellScreen.ActivityStatus, PlayerStrings.T("label.activity"), a.HasProcessedAnyRecord ? ScreenState.Normal : ScreenState.Empty);
            // UX_DESIGN §5: player-facing states only; never raw identifiers or exception text.
            m.Lines.Add(PlayerCopy.ActivityStatus(a.Status));
            string next = PlayerCopy.ActivityAction(a.RecommendedAction);
            if (next != null) m.Lines.Add(next);
            if (a.RecommendedAction == ActivityRecommendedAction.Connect && _requestPermission != null)
                m.Actions.Add(new ShellAction("activity:connect", PlayerStrings.T("shell.connectWalkingData")));
            if (!a.HasProcessedAnyRecord) m.Lines.Add(PlayerStrings.T("shell.noWalkingDataHas"));
            else m.Lines.Add(PlayerStrings.F("shell.activity.lastBatch", a.LastBatchVitalityCredited));
            m.Actions.Add(new ShellAction("nav:Home", PlayerStrings.T("label.home")));
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
                    m = With(ShellScreen.Guide, PlayerStrings.T("shell.theValleyToday"));
                    var damaged = _session.GetRegion().Landmarks.FirstOrDefault();
                    if (damaged != null) m.Lines.Add(GlancePhrases.FormatLandmark(PlayerCopy.Title(damaged.TitleKey), damaged.Stage));
                    m.Lines.Add(PlayerStrings.T("shell.theValleyIsQuiet"));
                    m.Actions.Add(new ShellAction("guide:next", PlayerStrings.T("label.continue")));
                    break;
                case OnboardingStage.ActivityConnection:
                    bool needsUpdate = ob.ActivityStep == OnboardingActivityStepState.ProviderUpdateRequired;
                    // D-094: an outdated Health Connect shows the update-required state, never a Connect dead end.
                    m = With(ShellScreen.Guide, needsUpdate ? PlayerStrings.T("shell.updateHealthConnect") : PlayerStrings.T("shell.connectYourWalking"));
                    m.Lines.Add(PlayerStrings.T("shell.theGameReadsOnly"));
                    m.Lines.Add(PlayerCopy.ActivityStatus(_session.GetActivityStatus().Status));
                    if (needsUpdate) m.Lines.Add(PlayerCopy.ActivityAction(ActivityRecommendedAction.UpdateProvider));
                    m.Lines.Add(PlayerStrings.T("shell.youCanKeepGoing"));
                    if (!needsUpdate && _session.GetActivityStatus().RecommendedAction == ActivityRecommendedAction.Connect && _requestPermission != null)
                        m.Actions.Add(new ShellAction("activity:connect", PlayerStrings.T("shell.connectWalkingData")));
                    m.Actions.Add(new ShellAction("guide:next", PlayerStrings.T("label.continue")));
                    break;
                case OnboardingStage.FirstProject:
                    m = With(ShellScreen.Guide, PlayerStrings.T("shell.chooseAFirstRestoration"));
                    if (ob.FirstProjectChosen)
                    {
                        m.Lines.Add(PlayerStrings.T("shell.goodChoiceItWill"));
                        m.Actions.Add(new ShellAction("guide:next", PlayerStrings.T("label.continue")));
                    }
                    else
                    {
                        m.Lines.Add(PlayerStrings.T("shell.pickOnePlaceTo"));
                        foreach (var r in _session.GetProjects().Projects.Where(r => r.Status == ProjectStatus.Available))
                            m.Actions.Add(new ShellAction("queue:" + r.ProjectId, QueueLabel(PlayerCopy.Title(r.TitleKey))));
                    }
                    break;
                case OnboardingStage.Simulation:
                    m = With(ShellScreen.Guide, PlayerStrings.T("shell.progressWhileYouRe"));
                    m.Lines.Add(PlayerStrings.T("shell.stepsYouTakeWith"));
                    m.Actions.Add(new ShellAction("guide:next", PlayerStrings.T("label.continue")));
                    break;
                default:
                    m = With(ShellScreen.Guide, PlayerStrings.T("shell.thatSAll"));
                    m.Lines.Add(PlayerStrings.T("shell.youDonTNeed"));
                    m.Actions.Add(new ShellAction("guide:next", PlayerStrings.T("shell.goToTheValley")));
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
            return PlayerStrings.F("label.step", step, GuideSteps);
        }

        /// <summary>D-109: project titles are already verb phrases ("Restore the river intake"), so the
        /// queue button must not prefix another verb ("Restore Restore the river intake").</summary>
        public static string QueueLabel(string title) => PlayerStrings.F("label.start", title);

        public static readonly string[] PrivacyLines =
        {
            PlayerStrings.T("shell.everythingThisGameKnows"),
            PlayerStrings.T("shell.itKeepsYourValley"),
            PlayerStrings.T("shell.itKeepsYourWalks"),
            PlayerStrings.T("shell.itKeepsYourSettings"),
            PlayerStrings.T("shell.itNeverAsksFor"),
            PlayerStrings.T("shell.youCanShareA"),
        };

        private ScreenModel Privacy()
        {
            var m = With(ShellScreen.Privacy, PlayerStrings.T("shell.privacyAndYourData"));
            foreach (var line in PrivacyLines) m.Lines.Add(line);
            m.Actions.Add(new ShellAction("export", PlayerStrings.T("shell.shareACopyOf")));
            m.Actions.Add(new ShellAction("nav:Settings", PlayerStrings.T("label.settings")));
            return m;
        }

        /// <summary>D-111: readable JSON copy of the save to the OS share sheet. Local only; fails closed.</summary>
        private void Export()
        {
            _notice = null;
            if (ShareText == null) { _error = PlayerStrings.T("shell.sharingIsnTAvailable"); return; }
            var result = WalkGame.Infrastructure.Persistence.SaveExport.ToReadableJson(_session.ExportSaveEnvelope());
            if (result.Status != WalkGame.Infrastructure.Persistence.SaveExportStatus.Exported || result.Json == null)
            { _error = PlayerStrings.T("shell.weCouldnTMake"); return; }
            if (result.Json.Length > MaxExportChars) { _error = PlayerStrings.T("shell.yourSaveIsToo"); return; }
            bool shared;
            try { shared = ShareText(result.Json); } catch (Exception) { shared = false; }
            if (shared) _notice = PlayerStrings.T("shell.yourPhoneSShare");
            else _error = PlayerStrings.T("shell.theShareMenuDidn");
        }

        private ScreenModel BootErrorScreen()
        {
            // Deliberately no "start" action: a save we could not read is never overwritten from here.
            var m = new ScreenModel(ShellScreen.BootError, ScreenState.Error, PlayerStrings.T("shell.weCouldnTOpen"));
            // D-109: no raw StartStatus identifier; the notice already explains the problem in plain words.
            m.Actions.Add(new ShellAction("retry", PlayerStrings.T("shell.tryAgain")));
            return m;
        }
    }
}
