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

        /// <summary>D-111: larger exports are refused (Android intent extras are limited to about 1 MB).</summary>
        public const int MaxExportChars = 512 * 1024;

        public const string TextPreviewSample = "Preview: The valley remembers every walk you take.";

        /// <summary>Settings text-size line (B3, D-083). Never throws on a hostile scale source.</summary>
        public static string TextSizeLine(float raw)
        {
            float k = ShellView.ClampTextScale(raw);
            string pct = Math.Round(k * 100f).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
            return raw > ShellView.MaxTextScale
                ? "Text size: " + pct + ", the largest this game supports. It follows your phone's font size setting."
                : "Text size: " + pct + ". It follows your phone's font size setting.";
        }

        /// <summary>D-097: settings status lines say what the setting does, in plain words.</summary>
        public static string ReducedMotionLine(bool on) => on
            ? "Reduced motion is on. Your choice is saved on this device."
            : "Reduced motion is off. You can turn it on at any time.";

        public static string SoundLine(bool on) => on
            ? "Sound is on. You can turn it off at any time."
            : "Sound is off. The game stays quiet.";

        public static string DiagnosticsLine(bool on) => on
            ? "Support details are shown. They are only for looking into a step-count question."
            : "Support details are hidden. You only need them to look into a step-count question.";

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
            if (_booted) SyncLiveSource();
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
            if (string.IsNullOrEmpty(actionId)) { _error = "Unknown action."; return; }
            if (actionId.StartsWith("nav:", StringComparison.Ordinal))
            {
                _notice = null;
                // C (D-084): a malformed or unknown target, or a game screen before a game is
                // loaded, is a calm error on the current screen, never an exception.
                string target = actionId.Substring(4);
                // Exact names only: Enum.TryParse also accepts numbers and comma lists ("Home,Story"
                // parses to the flags OR of the two values, which is another real screen).
                if (Array.IndexOf(Enum.GetNames(typeof(ShellScreen)), target) < 0 || !Enum.TryParse(target, false, out ShellScreen screen))
                { _error = "That screen does not exist."; return; }
                if (!_booted && screen != ShellScreen.Onboarding && screen != ShellScreen.BootError)
                { _error = "Start or continue a game first."; return; }
                // D-110: BootError describes a save that could not be opened. With a game loaded
                // that is false, so the shell refuses it calmly and stays where it is.
                if (_booted && screen == ShellScreen.BootError)
                { _error = "Your world is open and fine. There is no problem to show."; return; }
                Current = screen;
                return;
            }
            if (!_booted && actionId != "start" && actionId != "retry" && actionId != "activity:connect")
            { _error = "Start or continue a game first."; return; }
            if (actionId.StartsWith("queue:", StringComparison.Ordinal)) { Check(_session.EnqueueProject(actionId.Substring(6))); return; }
            if (actionId.StartsWith("up:", StringComparison.Ordinal)) { MoveUp(actionId.Substring(3)); return; }
            switch (actionId)
            {
                case "start":
                    // C (D-084): StartNewGame overwrites unconditionally, so only the onboarding
                    // screen of an unbooted shell may start. A loaded game or an unreadable save
                    // (BootError) is never replaced from here.
                    if (_booted || Current != ShellScreen.Onboarding) { _error = "A game is already here; it was not replaced."; return; }
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
                    else if (started.Status == StartStatus.ExistingSaveRefused) _error = "A game is already here; it was not replaced.";
                    else _error = "We couldn't start a new world. Nothing on this device was changed.";
                    return;
                case "retry": Boot(); return;
                case "activity:connect":
                    if (_requestPermission == null) { _error = "Walking data can't be connected on this device."; return; }
                    _requestPermission();
                    return;
                case "guide:next": GuideNext(); return;
                case "ack": if (Check(_session.AcknowledgeReturnSummary())) Current = ShellScreen.Home; return;
                case "toggle:reducedMotion": Check(_session.SetReducedMotion(!_session.GetSettings().ReducedMotion)); return;
                case "toggle:sound": Check(_session.SetSoundEnabled(!_session.GetSettings().SoundEnabled)); return;
                case "toggle:diagnostics": Check(_session.SetDiagnosticsVisible(!_session.GetSettings().DiagnosticsVisible)); return;
                case "export": Export(); return;
                case "delete:request": Current = ShellScreen.DeleteConfirm; return;
                case "delete:cancel": Current = ShellScreen.Settings; return;
                case "delete:confirm":
                    var erased = _session.DeleteAllLocalData();
                    if (erased.Status == LocalDataEraseStatus.Erased)
                    {
                        _booted = false; _notice = "All local data was deleted."; Current = ShellScreen.Onboarding;
                    }
                    else { Current = ShellScreen.Settings; _error = "Your data could not be deleted, so nothing was changed. You can try again."; }
                    return;
                case "dev:walk":
                    if (!_devTools) { _error = "Development tools are disabled."; return; }
                    var now = DateTimeOffset.UtcNow;
                    _session.IngestFromSource(new SyntheticWalkingSource(6000), now.AddDays(-1), now);
                    return;
                default: _error = "Unknown action."; return;
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
            _error = "That could not be done just now. Your progress is safe.";
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
            // D-106: the empty-state line below already says nothing is being restored; skip the duplicate.
            if (!empty) m.Lines.Add(glance.RestoringNowSentence);
            if (glance.AttentionSentence != null) m.Lines.Add(glance.AttentionSentence);
            m.Lines.Add(glance.ActivitySentence);
            m.Lines.Add(HomeStoryLine(_session.GetStory()));
            m.Lines.Add("Vitality " + home.Vitality + " · Materials " + home.Materials + " · Knowledge " + home.Knowledge);
            if (empty) m.Lines.Add("Nothing is being restored yet. Choose a project to begin.");
            if (home.HasPendingSummary) m.Actions.Add(new ShellAction("nav:ReturnSummary", "While you were away"));
            m.Actions.Add(new ShellAction("nav:Projects", "Projects"));
            m.Actions.Add(new ShellAction("nav:Region", "The valley"));
            m.Actions.Add(new ShellAction("nav:Story", "Story"));
            m.Actions.Add(new ShellAction("nav:Progress", "Your progress"));
            m.Actions.Add(new ShellAction("nav:ActivityStatus", "Activity"));
            m.Actions.Add(new ShellAction("nav:Settings", "Settings"));
            if (_devTools) m.Actions.Add(new ShellAction("dev:walk", "[dev] Simulate a day of walking"));
            m.Lines.RemoveAll(string.IsNullOrEmpty);
            // D-106: the glance can repeat a sentence (what changed == attention); show each line once.
            var seen = new HashSet<string>(StringComparer.Ordinal);
            m.Lines.RemoveAll(l => !seen.Add(l));
            return m;
        }

        private ScreenModel Summary()
        {
            var s = _session.GetPendingReturnSummary();
            var m = With(ShellScreen.ReturnSummary, "While you were away", s == null ? ScreenState.Empty : ScreenState.Normal);
            if (s == null) m.Lines.Add("Nothing new since your last visit.");
            else
            {
                // UX_DESIGN §4/§19: lead with transformation; restoration moments are shown inline on
                // the valley picture (static, skippable by simply continuing), never as a blocking sequence.
                var moments = s.Items.Where(i => i.Kind == WalkGame.Domain.Summaries.SummaryItemKind.Transformation && i.IsCurrent)
                                     .Select(i => i.Text).ToList();
                if (moments.Count > 0)
                {
                    m.Scene = RegionScene.Build(_session.GetRegion(), moments);
                    m.Lines.Add(m.Scene.ChangedCount > 0 ? "The valley changed while you were away." : "Your walks changed the valley.");
                }
                // D-100: a finished project gets one calm follow-up line naming the landmark stage it
                // brought and its storyline chapter (derived from authored content; stored text unchanged).
                var content = WalkGame.Application.Content.Region1Catalog.Create();
                var unlocked = new HashSet<string>(_session.GetStory().Chapters.Where(c => c.Unlocked).Select(c => c.ChapterId));
                foreach (var item in s.Items)
                {
                    m.Lines.Add((item.IsCurrent ? "" : "Earlier: ") + item.Text);
                    if (!item.IsCurrent) continue;
                    var detail = WalkGame.Application.Summaries.ProjectCompletionMoments.Describe(content, item.Text, unlocked);
                    if (detail != null) m.Lines.Add(detail);
                }
            }
            bool celebrating = s != null && Celebrate(m, s.Items.Where(i => i.IsCurrent).Select(i => i.Text));
            if (s != null)
            {
                bool chapter = s.Items.Any(i => i.IsCurrent && i.Text.StartsWith(ChapterMomentPrefix, StringComparison.Ordinal));
                bool restored = s.Items.Any(i => i.IsCurrent && i.Kind == WalkGame.Domain.Summaries.SummaryItemKind.Transformation);
                m.Cue = SoundCues.ForSummary(_session.GetSettings().SoundEnabled, celebrating, chapter, restored);
            }
            if (s != null) m.Actions.Add(new ShellAction("ack", "Continue"));
            if (celebrating) m.Actions.Add(new ShellAction("nav:Region", "See the restored valley"));
            // D-076: a chapter moment is a Transformation item; offer the journal after the primary Continue.
            if (s != null && s.Items.Any(i => i.IsCurrent && i.Text.StartsWith(ChapterMomentPrefix, StringComparison.Ordinal)))
                m.Actions.Add(new ShellAction("nav:Story", "Read the new chapter"));
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
                    : "No projects are open right now. Finishing a restoration opens new ones.");
            else if (!anyQueued)
                m.Lines.Add("Nothing is queued. Choose a project below and your walks will start restoring it.");
            foreach (var r in p.Projects.Where(r => r.Status != ProjectStatus.Locked).OrderBy(r => r.QueuedPosition ?? int.MaxValue))
            {
                string title = PlayerCopy.Title(r.TitleKey);
                m.Lines.Add(title + " — " + PlayerCopy.ProjectStatus(r.Status, r.QueuedPosition) + " (" + r.VitalityInvested + " of " + r.VitalityCost + " vitality)");
                if (r.Status == ProjectStatus.Available) m.Actions.Add(new ShellAction("queue:" + r.ProjectId, QueueLabel(title)));
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
            m.Scene = RegionScene.Build(r);
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
            m.Actions.Add(new ShellAction("nav:Discoveries", "Discoveries and expeditions"));
            m.Actions.Add(new ShellAction("nav:Home", "Home"));
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
            var m = With(ShellScreen.Discoveries, "Discoveries and expeditions", empty ? ScreenState.Empty : ScreenState.Normal);
            m.Lines.Add("Discoveries: " + d.UnlockedCount + " of " + d.TotalDiscoveries + " found.");
            if (d.UnlockedCount == 0) m.Lines.Add("Nothing found yet. Restoring landmarks reveals what the valley kept.");
            foreach (var row in d.Discoveries.Where(x => x.Unlocked).OrderBy(x => x.DiscoveredAtUtc))
            {
                m.Lines.Add(PlayerCopy.Title(row.TitleKey) + " · " + PlayerCopy.DiscoveryCategory(row.Category)
                    + (row.DiscoveredAtUtc.HasValue ? " · found " + row.DiscoveredAtUtc.Value.UtcDateTime.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture) : ""));
                // D-086: authored body and origin copy; a key without copy is skipped, never shown raw.
                var body = WalkGame.Application.Content.Region1DiscoveryCopy.Text(row.BodyKey);
                if (body != null) m.Lines.Add(body);
                var origin = WalkGame.Application.Content.Region1DiscoveryCopy.Text(row.ProvenanceKey);
                if (origin != null) m.Lines.Add("Where it came from: " + origin);
            }
            int hidden = d.TotalDiscoveries - d.UnlockedCount;
            if (hidden > 0) m.Lines.Add(hidden + (hidden == 1 ? " more find is" : " more finds are") + " still waiting in the valley.");

            m.Lines.Add("Expedition routes: " + e.AvailableCount + " open, " + e.CompletedCount + " of " + e.TotalExpeditions + " completed.");
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
                        m.Lines.Add(PlayerCopy.Title(x.TitleKey) + " · open; it completes on its own while you walk." + reward); break;
                    default:
                        var needs = x.RequiredProjectIds.Select(id => projects.TryGetValue(id, out var t) ? t : GlancePhrases.UntitledProject)
                            .Concat(x.RequiredStageKeys.Select(k => StageNeed(k, landmarks))).ToList();
                        m.Lines.Add(PlayerCopy.Title(x.TitleKey) + " · opens after: " + (needs.Count == 0 ? "more restoration" : string.Join(", ", needs)) + "." + reward);
                        break;
                }
            }
            m.Actions.Add(new ShellAction("nav:Region", "The valley"));
            m.Actions.Add(new ShellAction("nav:Home", "Home"));
            return m;
        }

        /// <summary>"lm.canopy@Flourishing" → "Canopy flourishing"; unknown ids never leak raw.</summary>
        public static string StageNeed(string key, IReadOnlyDictionary<string, string> landmarks)
        {
            int at = key == null ? -1 : key.LastIndexOf('@');
            if (at <= 0) return "more restoration";
            string name = landmarks.TryGetValue(key.Substring(0, at), out var t) ? t : "a landmark";
            return name + " " + key.Substring(at + 1).ToLowerInvariant();
        }

        /// <summary>Suffix the composer uses for the region-complete moment (ReturnSummaryComposer, RegionCompleted).</summary>
        public const string RegionCompleteSuffix = "'s restoration is complete.";

        /// <summary>Celebration headline for Region 1 completion (D-080).</summary>
        public const string CelebrationHeadline = "You restored the whole valley.";

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
            m.Title = "The valley is restored";
            m.Lines.Insert(0, CelebrationHeadline);
            m.Lines.Insert(1, "Every place you brought back stays restored. Your walks keep the valley flourishing.");
            return true;
        }

        /// <summary>Home's one-line story pointer once every chapter is unlocked (the epilogue line).</summary>
        public const string StoryEpilogueLine = "Story: Millbrook's story is complete. The valley stays restored, and every chapter is here to reread.";

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
            string where = latest == null ? "The story begins soon."
                : (latest.Number == 0 ? "Prologue · " : "Chapter " + latest.Number + " · ") + latest.TitleKey + ".";
            return "Story: " + where + " Next: " + story.NextHintKey;
        }

        /// <summary>Prefix the composer uses for chapter moments (ReturnSummaryComposer, D-076).</summary>
        public const string ChapterMomentPrefix = "New chapter: ";

        /// <summary>
        /// Story journal (D-076). Unlocked chapters show title and text in reading order; the
        /// next locked chapter shows its gentle hint; later chapters show only their titles so
        /// the journal never spoils the story and never uses guilt language.
        /// </summary>
        private ScreenModel Story()
        {
            var story = _session.GetStory();
            var m = With(ShellScreen.Story, "Story");
            m.Lines.Add(story.UnlockedCount == story.TotalChapters
                ? "You have read the whole story of the valley. It stays here to reread."
                : story.UnlockedCount + " of " + story.TotalChapters + " chapters unlocked. Restoring the valley reveals the next one.");
            bool hinted = false;
            foreach (var c in story.Chapters)
            {
                string heading = c.Number == 0 ? "Prologue · " + c.TitleKey : "Chapter " + c.Number + " · " + c.TitleKey;
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
            m.Actions.Add(new ShellAction("nav:Home", "Home"));
            return m;
        }

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
            var m = With(ShellScreen.Progress, "Your progress", empty ? ScreenState.Empty : ScreenState.Normal);
            foreach (var line in ProgressLines(vitality, home.CompletedProjects, home.TotalProjects, disc.UnlockedCount, disc.TotalDiscoveries,
                         story.UnlockedCount, story.TotalChapters, home.RegionComplete))
                m.Lines.Add(line);
            // D-103: days walked, before the closing reassurance line; "at least" when the
            // count is a proven lower bound (migrated save, or walks without a local date).
            var days = _session.GetDaysWalked();
            string daysLine = DaysWalkedLine(days.Days, days.IsLowerBound);
            if (daysLine != null) m.Lines.Insert(Math.Max(0, m.Lines.Count - 1), daysLine);
            m.Actions.Add(new ShellAction("nav:Home", "Home"));
            return m;
        }

        /// <summary>D-103 days-walked copy; null when no day is known yet (the empty-state line covers it).</summary>
        public static string DaysWalkedLine(int days, bool isLowerBound)
        {
            if (days <= 0) return null;
            string unit = days == 1 ? " day." : " days.";
            return (isLowerBound ? "You have walked on at least " : "You have walked on ") + days + unit;
        }

        /// <summary>D-099 Progress copy, pure so every state can be checked without a session.</summary>
        public static System.Collections.Generic.List<string> ProgressLines(long lifetimeVitality, int projectsDone, int projectsTotal,
            int discoveriesFound, int discoveriesTotal, int chaptersRead, int chaptersTotal, bool regionComplete)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var lines = new System.Collections.Generic.List<string>();
            if (lifetimeVitality <= 0 && projectsDone == 0)
            {
                lines.Add("Your first walk will show up here. There is no rush.");
            }
            else
            {
                long steps = lifetimeVitality * WalkGame.Domain.Activity.StepConversionRuleV1.StepsPerVitality;
                lines.Add("Vitality gathered from your walks: " + lifetimeVitality.ToString("N0", ci) + ".");
                lines.Add("That is about " + steps.ToString("N0", ci) + " steps, counted whenever they happened.");
            }
            lines.Add(regionComplete
                ? "Every restoration in the valley is finished (" + projectsDone + " of " + projectsTotal + ")."
                : "Restorations finished: " + projectsDone + " of " + projectsTotal + ".");
            lines.Add("Discoveries found: " + discoveriesFound + " of " + discoveriesTotal + ".");
            lines.Add("Story chapters unlocked: " + chaptersRead + " of " + chaptersTotal + ".");
            lines.Add("Nothing here expires. Every walk counts, whenever it happens.");
            return lines;
        }

        private ScreenModel Settings()
        {
            var s = _session.GetSettings();
            var m = With(ShellScreen.Settings, "Settings");
            m.Lines.Add(ReducedMotionLine(s.ReducedMotion));
            m.Lines.Add(SoundLine(s.SoundEnabled));
            m.Lines.Add(DiagnosticsLine(s.DiagnosticsVisible));
            float raw;
            try { raw = TextScaleSource?.Invoke() ?? 1f; } catch (Exception) { raw = 1f; }
            m.Lines.Add(TextSizeLine(raw));
            m.TextPreview = TextPreviewSample;
            m.Actions.Add(new ShellAction("toggle:reducedMotion", (s.ReducedMotion ? "Turn off" : "Turn on") + " reduced motion"));
            m.Actions.Add(new ShellAction("toggle:sound", (s.SoundEnabled ? "Turn off" : "Turn on") + " sound"));
            m.Actions.Add(new ShellAction("toggle:diagnostics", (s.DiagnosticsVisible ? "Hide" : "Show") + " diagnostics"));
            m.Actions.Add(new ShellAction("nav:Privacy", "Privacy and your data"));
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
            if (a.RecommendedAction == ActivityRecommendedAction.Connect && _requestPermission != null)
                m.Actions.Add(new ShellAction("activity:connect", "Connect walking data"));
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
                    bool needsUpdate = ob.ActivityStep == OnboardingActivityStepState.ProviderUpdateRequired;
                    // D-094: an outdated Health Connect shows the update-required state, never a Connect dead end.
                    m = With(ShellScreen.Guide, needsUpdate ? "Update Health Connect" : "Connect your walking");
                    m.Lines.Add("The game reads only your step counts, on this device. Nothing is uploaded.");
                    m.Lines.Add(PlayerCopy.ActivityStatus(_session.GetActivityStatus().Status));
                    if (needsUpdate) m.Lines.Add(PlayerCopy.ActivityAction(ActivityRecommendedAction.UpdateProvider));
                    m.Lines.Add("You can keep going without connecting; nothing is locked.");
                    if (!needsUpdate && _session.GetActivityStatus().RecommendedAction == ActivityRecommendedAction.Connect && _requestPermission != null)
                        m.Actions.Add(new ShellAction("activity:connect", "Connect walking data"));
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
                            m.Actions.Add(new ShellAction("queue:" + r.ProjectId, QueueLabel(PlayerCopy.Title(r.TitleKey))));
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

        /// <summary>D-109: project titles are already verb phrases ("Restore the river intake"), so the
        /// queue button must not prefix another verb ("Restore Restore the river intake").</summary>
        public static string QueueLabel(string title) => "Start: " + title;

        public static readonly string[] PrivacyLines =
        {
            "Everything this game knows stays on this phone, in the game's own private storage. Your phone's encryption protects it.",
            "It keeps your valley: projects, restored places, story chapters and discoveries.",
            "It keeps your walks: step counts, when each walk started and ended, the day it was, and the record number your walking app gave it, so no walk is counted twice.",
            "It keeps your settings, such as sound and reduced motion, and how far you are through the guide.",
            "It never asks for your name, email or location. There are no accounts, ads or analytics, and nothing is sent anywhere.",
            "You can share a copy of your save. Your phone's share menu opens and you choose where it goes. The copy includes your walks.",
        };

        private ScreenModel Privacy()
        {
            var m = With(ShellScreen.Privacy, "Privacy and your data");
            foreach (var line in PrivacyLines) m.Lines.Add(line);
            m.Actions.Add(new ShellAction("export", "Share a copy of your save"));
            m.Actions.Add(new ShellAction("nav:Settings", "Settings"));
            return m;
        }

        /// <summary>D-111: readable JSON copy of the save to the OS share sheet. Local only; fails closed.</summary>
        private void Export()
        {
            _notice = null;
            if (ShareText == null) { _error = "Sharing isn't available on this device. Your save is safe."; return; }
            var result = WalkGame.Infrastructure.Persistence.SaveExport.ToReadableJson(_session.ExportSaveEnvelope());
            if (result.Status != WalkGame.Infrastructure.Persistence.SaveExportStatus.Exported || result.Json == null)
            { _error = "We couldn't make a copy just now. Your save is safe."; return; }
            if (result.Json.Length > MaxExportChars) { _error = "Your save is too large to share this way. Your save is safe."; return; }
            bool shared;
            try { shared = ShareText(result.Json); } catch (Exception) { shared = false; }
            if (shared) _notice = "Your phone's share menu has the copy. You choose where it goes.";
            else _error = "The share menu didn't open. Your save is safe.";
        }

        private ScreenModel BootErrorScreen()
        {
            // Deliberately no "start" action: a save we could not read is never overwritten from here.
            var m = new ScreenModel(ShellScreen.BootError, ScreenState.Error, "We couldn't open your world");
            // D-109: no raw StartStatus identifier; the notice already explains the problem in plain words.
            m.Actions.Add(new ShellAction("retry", "Try again"));
            return m;
        }
    }
}
