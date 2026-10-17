using System;
using System.Collections.Generic;
using WalkGame.Application.Content;
using WalkGame.Domain;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// D-165 (OpenSpec add-interactive-world-presentation): pure projections from canonical state
    /// plus authored content to the world-presentation read models. No clock, no writes, no
    /// randomness; the same state always gives the same output.
    /// </summary>
    public static class WorldPresentationProjector
    {
        /// <summary>Project id → location id, via the D-062 chains and <see cref="Region1WorldLayout"/>.
        /// A project in no bound chain has no location (tests require every project to have one).</summary>
        public static IReadOnlyDictionary<string, string> LocationOfProject(RegionDefinition content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var chain in content.Chains)
            {
                if (!Region1WorldLayout.LocationOfChain.TryGetValue(chain.Id, out var location)) continue;
                foreach (var projectId in chain.ProjectIds)
                    if (!map.ContainsKey(projectId)) map[projectId] = location;
            }
            return map;
        }

        /// <summary>Where a character is, given which projects are complete; null while absent.</summary>
        public static string? CharacterLocation(Region1Characters.Character character, Func<string, bool> isCompleted)
        {
            string? at = null;
            foreach (var a in character.Appearances)
                if (a.AfterProjectId == null || isCompleted(a.AfterProjectId)) at = a.LocationId;
            return at;
        }

        /// <summary>Lines reached so far, in order. Empty while the character is absent.</summary>
        public static IReadOnlyList<Region1Characters.Line> ReachedLines(Region1Characters.Character character, Func<string, bool> isCompleted)
        {
            var reached = new List<Region1Characters.Line>();
            if (CharacterLocation(character, isCompleted) == null) return reached;
            foreach (var line in character.Lines)
                if (line.MilestoneProjectId == null || isCompleted(line.MilestoneProjectId)) reached.Add(line);
            reached.Sort((a, b) => a.Order.CompareTo(b.Order));
            return reached;
        }

        public static Func<string, bool> CompletedIn(RegionState region) => id =>
        {
            var p = region.FindProject(id);
            return p != null && p.Status == ProjectStatus.Completed;
        };

        public static IReadOnlyList<CharacterDialogueReadModel> Characters(GameState game, ContentText text)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            text ??= ContentText.English;
            var completed = CompletedIn(game.Region);
            var rows = new List<CharacterDialogueReadModel>(Region1Characters.All.Count);
            foreach (var c in Region1Characters.All)
            {
                var lines = new List<CharacterDialogueReadModel.LineRow>();
                foreach (var l in ReachedLines(c, completed))
                    lines.Add(new CharacterDialogueReadModel.LineRow(l.Id, l.Order, l.MilestoneProjectId, l.Expression,
                        text.Resolve(l.Id, ContentText.Fields.Line, l.Text)));
                rows.Add(new CharacterDialogueReadModel(c.Id, text.Resolve(c.Id, ContentText.Fields.Name, c.Name),
                    c.PortraitKey, CharacterLocation(c, completed), lines));
            }
            return rows;
        }

        public static WorldViewReadModel WorldView(RegionDefinition content, GameState game, ContentText text,
            IReadOnlyCollection<string>? seenLineIds)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (game == null) throw new ArgumentNullException(nameof(game));
            text ??= ContentText.English;
            var region = game.Region;
            var completed = CompletedIn(region);
            var locationOf = LocationOfProject(content);

            var seen = seenLineIds == null ? null : new HashSet<string>(seenLineIds, StringComparer.Ordinal);
            var characters = Characters(game, text);

            var rows = new List<WorldViewReadModel.LocationRow>(content.Landmarks.Count);
            foreach (var landmark in content.Landmarks)
            {
                string id = landmark.Id.Value;
                var stage = region.LandmarkStages.TryGetValue(id, out var s) ? s : RestorationStage.Ruined;

                var projects = new List<string>();
                var selectable = new List<string>();
                bool anyOpen = false;
                string? active = null, construction = null, lastCompleted = null;
                int buildStage = 0, buildStages = 1;
                foreach (var def in content.Projects)
                {
                    string pid = def.Id.Value;
                    if (!locationOf.TryGetValue(pid, out var at) || at != id) continue;
                    projects.Add(pid);
                    var st = region.FindProject(pid);
                    if (st == null) continue;
                    if (st.Status != ProjectStatus.Locked) anyOpen = true;
                    if (st.Status == ProjectStatus.Completed) lastCompleted = pid;
                    if (st.Status == ProjectStatus.Available || st.Status == ProjectStatus.Queued) selectable.Add(pid);
                    if (st.Status == ProjectStatus.Active)
                    {
                        active = pid;
                        if (def.VisibleBuildStages > 1)
                        {
                            buildStage = def.BuildStageFor(st.VitalityInvested);
                            buildStages = def.VisibleBuildStages;
                            construction = WorldViewReadModel.ConstructionKeyFor(pid, buildStage, buildStages);
                        }
                    }
                }

                int found = 0;
                foreach (var d in content.Discoveries)
                    if (region.Discoveries.ContainsKey(d.Id.Value)
                        && locationOf.TryGetValue(d.UnlockedByProjectId, out var dl) && dl == id)
                        found++;

                var present = new List<string>();
                var pending = new List<WorldViewReadModel.PendingLine>();
                var currentLines = new List<WorldViewReadModel.PendingLine>();
                foreach (var c in characters)
                {
                    if (c.LocationId != id) continue;
                    present.Add(c.CharacterId);
                    var current = c.CurrentLine;
                    if (current != null) currentLines.Add(new WorldViewReadModel.PendingLine(c.CharacterId, current.LineId));
                    if (current != null && (seen == null || !seen.Contains(current.LineId)))
                        pending.Add(new WorldViewReadModel.PendingLine(c.CharacterId, current.LineId));
                }

                bool unlocked = stage > RestorationStage.Ruined || anyOpen;
                bool interactable = unlocked && (selectable.Count > 0 || active != null || present.Count > 0 || found > 0);
                rows.Add(new WorldViewReadModel.LocationRow(id,
                    text.Resolve(id, ContentText.Fields.Title, landmark.TitleKey), stage, unlocked, interactable,
                    projects, selectable, active, construction, found, present, pending, currentLines, buildStage, buildStages,
                    active ?? (selectable.Count > 0 ? selectable[0] : lastCompleted)));
            }

            return new WorldViewReadModel(rows, region.EcologyStage, region.SettlementStage, region.IsCompleted, game.Queue.ActiveProjectId);
        }
    }
}
