using System;
using System.Collections.Generic;
using WalkGame.Application.Content;
using WalkGame.Domain;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;
using WalkGame.Domain.Simulation;
using WalkGame.Domain.Story;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// D-167 (OpenSpec add-interactive-world-presentation, tasks 3–4): restoration-choice previews
    /// and the timestamp-derived visual-change stream. Pure: no clock, no writes; canonical state
    /// is only read (hypotheticals run on small copies, never on <see cref="GameState"/>).
    /// </summary>
    public static class WorldChangeProjector
    {
        // ------------------------------------------------------------------ choice previews

        public static RestorationChoicesReadModel Choices(RegionDefinition content, GameState game, ContentText text)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (game == null) throw new ArgumentNullException(nameof(game));
            text ??= ContentText.English;
            var region = game.Region;
            var locationOf = WorldPresentationProjector.LocationOfProject(content);
            var completed = WorldPresentationProjector.CompletedIn(region);
            var story = StoryProgress.Evaluate(content, region, game.CreatedAtUtc);

            var choices = new List<RestorationChoicesReadModel.Choice>();
            foreach (var def in content.Projects)
            {
                string x = def.Id.Value;
                var st = region.FindProject(x);
                if (st == null || (st.Status != ProjectStatus.Available && st.Status != ProjectStatus.Queued && st.Status != ProjectStatus.Active))
                    continue;
                Func<string, bool> completedWith = id => id == x || completed(id);
                var consequences = new List<Consequence>();
                locationOf.TryGetValue(x, out var home);
                string? before = null, after = null;

                foreach (var lm in content.Landmarks)
                {
                    var cur = StageOf(region, lm.Id.Value);
                    var next = StageAfter(lm, cur, x);
                    if (lm.Id.Value == home)
                    {
                        before = WorldViewReadModel.VariantKeyFor(home, cur);
                        after = WorldViewReadModel.VariantKeyFor(home, next);
                    }
                    if (next > cur)
                        consequences.Add(new Consequence(ConsequenceKind.LandmarkStage, lm.Id.Value, lm.Id.Value,
                            WorldViewReadModel.VariantKeyFor(lm.Id.Value, cur), WorldViewReadModel.VariantKeyFor(lm.Id.Value, next)));
                }

                foreach (var other in content.Projects)
                {
                    var os = region.FindProject(other.Id.Value);
                    if (os == null || os.Status != ProjectStatus.Locked) continue;
                    if (PrerequisitesMet(other, completedWith))
                    {
                        locationOf.TryGetValue(other.Id.Value, out var ol);
                        consequences.Add(new Consequence(ConsequenceKind.ProjectOpens, other.Id.Value, ol, null, null));
                    }
                }

                foreach (var producer in content.Producers)
                {
                    var runtime = region.FindProducer(producer.Id.Value);
                    if (producer.UnlockedByProjectId == x && !(runtime?.Unlocked ?? false))
                        consequences.Add(new Consequence(ConsequenceKind.ProducerUnlocks, producer.Id.Value, home, null, null));
                }

                int eco = ArcAfter(content.EcologyProgression, region.EcologyStage, x);
                if (eco > region.EcologyStage)
                    consequences.Add(new Consequence(ConsequenceKind.EcologyAdvances, null, null, region.EcologyStage.ToString(), eco.ToString(), eco));
                int set = ArcAfter(content.SettlementProgression, region.SettlementStage, x);
                if (set > region.SettlementStage)
                    consequences.Add(new Consequence(ConsequenceKind.SettlementAdvances, null, null, region.SettlementStage.ToString(), set.ToString(), set));

                foreach (var d in content.Discoveries)
                    if (d.UnlockedByProjectId == x && !region.Discoveries.ContainsKey(d.Id.Value))
                        consequences.Add(new Consequence(ConsequenceKind.DiscoveryAtLocation, null, home, null, null));

                AddCharacterChanges(consequences, completed, completedWith);

                var hypothetical = CloneProjects(region, x);
                var storyAfter = StoryProgress.Evaluate(content, hypothetical, game.CreatedAtUtc);
                for (int i = 0; i < story.Count && i < storyAfter.Count; i++)
                    if (!story[i].Unlocked && storyAfter[i].Unlocked)
                        consequences.Add(new Consequence(ConsequenceKind.ChapterUnlocks, story[i].Chapter.Id, null, null, null));

                choices.Add(new RestorationChoicesReadModel.Choice(x,
                    text.Resolve(x, ContentText.Fields.Title, def.TitleKey), home, st.Status, def.VitalityCost,
                    st.VitalityInvested, def.ResourceCosts, OfflineAdvancer.CanAffordRequirements(game, def),
                    before, after, x == content.CompletionMilestoneProjectId, consequences,
                    WorldViewReadModel.ProjectVisualKeyFor(x, st.Status,
                        st.Status == ProjectStatus.Active ? def.BuildStageFor(st.VitalityInvested) : 0, def.VisibleBuildStages)));
            }
            return new RestorationChoicesReadModel(choices);
        }

        private static void AddCharacterChanges(List<Consequence> into, Func<string, bool> before, Func<string, bool> after)
        {
            foreach (var c in Region1Characters.All)
            {
                var was = WorldPresentationProjector.CharacterLocation(c, before);
                var now = WorldPresentationProjector.CharacterLocation(c, after);
                if (was == null && now != null)
                    into.Add(new Consequence(ConsequenceKind.CharacterArrives, c.Id, now, null, now));
                else if (was != null && now != null && was != now)
                    into.Add(new Consequence(ConsequenceKind.CharacterMoves, c.Id, now, was, now));
                var lineBefore = Last(WorldPresentationProjector.ReachedLines(c, before));
                var lineAfter = Last(WorldPresentationProjector.ReachedLines(c, after));
                if (lineAfter != null && lineAfter.Id != lineBefore?.Id)
                    into.Add(new Consequence(ConsequenceKind.NewDialogue, c.Id, now, lineBefore?.Id, lineAfter.Id));
            }
        }

        // ------------------------------------------------------------------ change stream

        /// <summary>
        /// Visual changes after <paramref name="sinceUtc"/>, replayed from completion timestamps. Returns
        /// nothing when the replay cannot reproduce canonical state, so it never invents a change.
        /// Completions sharing a timestamp are replayed in content order.
        /// </summary>
        public static IReadOnlyList<VisualChangeEvent> ChangesSince(RegionDefinition content, GameState game, DateTimeOffset sinceUtc)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (game == null) throw new ArgumentNullException(nameof(game));
            var region = game.Region;
            var locationOf = WorldPresentationProjector.LocationOfProject(content);

            var order = new List<(DateTimeOffset? at, int index, string id)>();
            for (int i = 0; i < content.Projects.Count; i++)
            {
                var st = region.FindProject(content.Projects[i].Id.Value);
                if (st != null && st.Status == ProjectStatus.Completed) order.Add((st.CompletedAtUtc, i, st.ProjectId));
            }
            order.Sort((a, b) =>
            {
                // Legacy completions without a time are baseline: first, never emitted.
                if (a.at == null && b.at != null) return -1;
                if (a.at != null && b.at == null) return 1;
                int c = a.at == null ? 0 : a.at.Value.CompareTo(b.at!.Value);
                return c != 0 ? c : a.index.CompareTo(b.index);
            });

            var done = new HashSet<string>(StringComparer.Ordinal);
            var stages = new Dictionary<string, RestorationStage>(StringComparer.Ordinal);
            foreach (var lm in content.Landmarks) stages[lm.Id.Value] = RestorationStage.Ruined;
            int eco = 0, set = 0;
            var unlocked = UnlockedLocations(content, locationOf, stages, done.Contains);
            var events = new List<VisualChangeEvent>();
            int seq = 0;
            bool regionEmitted = false;

            foreach (var (at, _, x) in order)
            {
                if (!regionEmitted && region.RegionCompletedAtUtc != null && at != null && at.Value > region.RegionCompletedAtUtc.Value)
                {
                    Emit(events, ref seq, sinceUtc, region.RegionCompletedAtUtc, VisualChangeKind.RegionCompleted, null, null, null, null);
                    regionEmitted = true;
                }
                var beforeDone = new HashSet<string>(done, StringComparer.Ordinal);
                done.Add(x);
                var batch = new List<(VisualChangeKind kind, string? loc, string? subject, string? from, string? to)>();
                if (locationOf.TryGetValue(x, out var doneAt))
                    batch.Add((VisualChangeKind.ProjectCompleted, doneAt, x, null, WorldViewReadModel.ProjectDoneKeyFor(x)));

                var unlockedAfter = UnlockedLocations(content, locationOf, StagesAfter(content, stages, x), done.Contains);
                foreach (var lm in content.Landmarks)
                {
                    string id = lm.Id.Value;
                    var cur = stages[id];
                    var next = StageAfter(lm, cur, x);
                    if (!unlocked.Contains(id) && unlockedAfter.Contains(id)) batch.Add((VisualChangeKind.LocationUnlocked, id, x, null, null));
                    if (next > cur)
                    {
                        batch.Add((VisualChangeKind.LandmarkStageChanged, id, x, WorldViewReadModel.VariantKeyFor(id, cur), WorldViewReadModel.VariantKeyFor(id, next)));
                        stages[id] = next;
                    }
                }
                unlocked = unlockedAfter;

                foreach (var c in Region1Characters.All)
                {
                    var was = WorldPresentationProjector.CharacterLocation(c, beforeDone.Contains);
                    var now = WorldPresentationProjector.CharacterLocation(c, done.Contains);
                    if (was == null && now != null) batch.Add((VisualChangeKind.CharacterArrived, now, c.Id, null, now));
                    else if (was != null && now != null && was != now) batch.Add((VisualChangeKind.CharacterMoved, now, c.Id, was, now));
                }
                foreach (var c in Region1Characters.All)
                {
                    var lb = Last(WorldPresentationProjector.ReachedLines(c, beforeDone.Contains));
                    var la = Last(WorldPresentationProjector.ReachedLines(c, done.Contains));
                    if (la != null && la.Id != lb?.Id)
                        batch.Add((VisualChangeKind.DialogueAvailable, WorldPresentationProjector.CharacterLocation(c, done.Contains), c.Id, lb?.Id, la.Id));
                }

                int e2 = ArcAfter(content.EcologyProgression, eco, x);
                if (e2 > eco) { batch.Add((VisualChangeKind.EcologyAdvanced, null, x, eco.ToString(), e2.ToString())); eco = e2; }
                int s2 = ArcAfter(content.SettlementProgression, set, x);
                if (s2 > set) { batch.Add((VisualChangeKind.SettlementAdvanced, null, x, set.ToString(), s2.ToString())); set = s2; }

                foreach (var b in batch)
                    if (at != null) Emit(events, ref seq, sinceUtc, at, b.kind, b.loc, b.subject, b.from, b.to);
            }
            if (!regionEmitted && region.IsCompleted && region.RegionCompletedAtUtc != null)
                Emit(events, ref seq, sinceUtc, region.RegionCompletedAtUtc, VisualChangeKind.RegionCompleted, null, null, null, null);

            // Replay must reproduce canonical state, or nothing is reported.
            foreach (var lm in content.Landmarks)
                if (stages[lm.Id.Value] != StageOf(region, lm.Id.Value)) return Array.Empty<VisualChangeEvent>();
            if (eco != region.EcologyStage || set != region.SettlementStage) return Array.Empty<VisualChangeEvent>();
            return events;
        }

        private static void Emit(List<VisualChangeEvent> into, ref int seq, DateTimeOffset since, DateTimeOffset? at,
            VisualChangeKind kind, string? loc, string? subject, string? from, string? to)
        {
            int n = seq++;
            if (at != null && at.Value > since) into.Add(new VisualChangeEvent(n, kind, at, loc, subject, from, to));
        }

        // ------------------------------------------------------------------ helpers

        private static RestorationStage StageOf(RegionState region, string landmarkId) =>
            region.LandmarkStages.TryGetValue(landmarkId, out var s) ? s : RestorationStage.Ruined;

        /// <summary>Mirrors ApplyCompletionEffects: stages unlocked by <paramref name="x"/> raise the landmark, never lower it.</summary>
        private static RestorationStage StageAfter(LandmarkDefinition lm, RestorationStage current, string x)
        {
            var stage = current;
            foreach (var sd in lm.Stages)
                if (sd.UnlockedByProjectId == x && sd.Stage > stage) stage = sd.Stage;
            return stage;
        }

        private static Dictionary<string, RestorationStage> StagesAfter(RegionDefinition content, Dictionary<string, RestorationStage> stages, string x)
        {
            var copy = new Dictionary<string, RestorationStage>(stages, StringComparer.Ordinal);
            foreach (var lm in content.Landmarks) copy[lm.Id.Value] = StageAfter(lm, stages[lm.Id.Value], x);
            return copy;
        }

        /// <summary>Mirrors AdvanceArc: the highest stage above current unlocked by <paramref name="x"/>.</summary>
        private static int ArcAfter(RegionProgressionDefinition arc, int current, string x)
        {
            int stage = current;
            foreach (var sd in arc.Stages)
                if (sd.Stage > stage && sd.UnlockedByProjectId == x) stage = sd.Stage;
            return stage;
        }

        /// <summary>Mirrors UpdateAvailability.</summary>
        private static bool PrerequisitesMet(ProjectDefinition def, Func<string, bool> completed)
        {
            foreach (var p in def.Prerequisites) if (!completed(p.Value)) return false;
            return true;
        }

        private static HashSet<string> UnlockedLocations(RegionDefinition content, IReadOnlyDictionary<string, string> locationOf,
            Dictionary<string, RestorationStage> stages, Func<string, bool> completed)
        {
            var open = new HashSet<string>(StringComparer.Ordinal);
            foreach (var lm in content.Landmarks) if (stages[lm.Id.Value] > RestorationStage.Ruined) open.Add(lm.Id.Value);
            foreach (var def in content.Projects)
                if (locationOf.TryGetValue(def.Id.Value, out var loc) && PrerequisitesMet(def, completed)) open.Add(loc);
            return open;
        }

        private static RegionState CloneProjects(RegionState region, string completedId)
        {
            var copy = new RegionState { RegionId = region.RegionId, PostCompletionCyclesCompleted = region.PostCompletionCyclesCompleted };
            foreach (var pair in region.Projects)
            {
                var p = pair.Value;
                copy.Projects[pair.Key] = new ProjectState
                {
                    ProjectId = p.ProjectId,
                    Status = pair.Key == completedId ? ProjectStatus.Completed : p.Status,
                    VitalityInvested = p.VitalityInvested,
                    CompletedAtUtc = pair.Key == completedId ? (p.CompletedAtUtc ?? DateTimeOffset.MaxValue) : p.CompletedAtUtc,
                };
            }
            return copy;
        }

        private static Region1Characters.Line? Last(IReadOnlyList<Region1Characters.Line> lines) =>
            lines.Count == 0 ? null : lines[lines.Count - 1];
    }

    /// <summary>D-167: in-session visual changes between two world views (includes construction advances).</summary>
    public static class WorldViewDiff
    {
        public static IReadOnlyList<VisualChangeEvent> Between(WorldViewReadModel before, WorldViewReadModel after)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));
            var events = new List<VisualChangeEvent>();
            int seq = 0;
            void Add(VisualChangeKind k, string? loc, string? subject, string? from, string? to) =>
                events.Add(new VisualChangeEvent(seq++, k, null, loc, subject, from, to));

            foreach (var a in after.Locations)
            {
                var b = before.Find(a.LocationId);
                if (b == null) continue;
                foreach (var pa in a.ProjectVisuals)
                {
                    if (!pa.IsCompleted) continue;
                    bool wasDone = false;
                    foreach (var pb in b.ProjectVisuals) if (pb.ProjectId == pa.ProjectId) { wasDone = pb.IsCompleted; break; }
                    if (!wasDone) Add(VisualChangeKind.ProjectCompleted, a.LocationId, pa.ProjectId, null, pa.VisualKey);
                }
                if (!b.IsUnlocked && a.IsUnlocked) Add(VisualChangeKind.LocationUnlocked, a.LocationId, null, null, null);
                if (a.Stage > b.Stage) Add(VisualChangeKind.LandmarkStageChanged, a.LocationId, null, b.VariantKey, a.VariantKey);
                if (a.ConstructionKey != null && b.ConstructionKey != null && a.ActiveProjectId == b.ActiveProjectId
                    && a.ConstructionStage > b.ConstructionStage)
                    Add(VisualChangeKind.ConstructionAdvanced, a.LocationId, a.ActiveProjectId, b.ConstructionKey, a.ConstructionKey);
            }

            var whereBefore = CharacterLocations(before);
            var whereAfter = CharacterLocations(after);
            var lineBefore = CurrentLines(before);
            var lineAfter = CurrentLines(after);
            foreach (var c in Region1Characters.All)
            {
                whereBefore.TryGetValue(c.Id, out var was);
                if (!whereAfter.TryGetValue(c.Id, out var now)) continue;
                if (was == null) Add(VisualChangeKind.CharacterArrived, now, c.Id, null, now);
                else if (was != now) Add(VisualChangeKind.CharacterMoved, now, c.Id, was, now);
            }
            foreach (var c in Region1Characters.All)
            {
                lineBefore.TryGetValue(c.Id, out var lb);
                if (lineAfter.TryGetValue(c.Id, out var la) && la != lb)
                    Add(VisualChangeKind.DialogueAvailable, whereAfter[c.Id], c.Id, lb, la);
            }

            if (after.EcologyStage > before.EcologyStage)
                Add(VisualChangeKind.EcologyAdvanced, null, null, before.EcologyStage.ToString(), after.EcologyStage.ToString());
            if (after.SettlementStage > before.SettlementStage)
                Add(VisualChangeKind.SettlementAdvanced, null, null, before.SettlementStage.ToString(), after.SettlementStage.ToString());
            if (after.RegionCompleted && !before.RegionCompleted) Add(VisualChangeKind.RegionCompleted, null, null, null, null);
            return events;
        }

        private static Dictionary<string, string> CharacterLocations(WorldViewReadModel v)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var l in v.Locations) foreach (var c in l.CharacterIds) map[c] = l.LocationId;
            return map;
        }

        private static Dictionary<string, string> CurrentLines(WorldViewReadModel v)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var l in v.Locations) foreach (var p in l.CurrentLines) map[p.CharacterId] = p.LineId;
            return map;
        }
    }
}
