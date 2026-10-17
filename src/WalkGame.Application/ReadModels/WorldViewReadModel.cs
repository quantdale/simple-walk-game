using System;
using System.Collections.Generic;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// D-165 (OpenSpec add-interactive-world-presentation): engine-agnostic world view. One row
    /// per location with style-neutral keys an art pipeline binds to. Derived from canonical
    /// state at read time; reading it never changes state or the save.
    /// </summary>
    public sealed class WorldViewReadModel
    {
        public WorldViewReadModel(IReadOnlyList<LocationRow> locations, int ecologyStage, int settlementStage,
            bool regionCompleted, string? activeProjectId)
        {
            Locations = locations ?? throw new ArgumentNullException(nameof(locations));
            EcologyStage = ecologyStage;
            SettlementStage = settlementStage;
            RegionCompleted = regionCompleted;
            ActiveProjectId = activeProjectId;
        }

        public IReadOnlyList<LocationRow> Locations { get; }
        public int EcologyStage { get; }
        public int SettlementStage { get; }
        public bool RegionCompleted { get; }
        public string? ActiveProjectId { get; }

        /// <summary>Region-wide atmosphere key, e.g. <c>region@eco2-set1</c>.</summary>
        public string AtmosphereKey => AtmosphereKeyFor(EcologyStage, SettlementStage);

        public static string AtmosphereKeyFor(int ecology, int settlement) => "region@eco" + ecology + "-set" + settlement;

        /// <summary>Variant key for a location at a stage, e.g. <c>lm.wetland@flourishing</c>.</summary>
        public static string VariantKeyFor(string locationId, RestorationStage stage) =>
            locationId + "@" + stage.ToString().ToLowerInvariant();

        /// <summary>Construction key for an in-progress project, e.g. <c>proj.canopy-walkway@build-2of4</c>.</summary>
        public static string ConstructionKeyFor(string projectId, int stage, int stages) =>
            projectId + "@build-" + stage + "of" + stages;

        /// <summary>
        /// D-176 (KI-027 a): per-project visual key, e.g. <c>proj.rebuild-trail-bridges@ruined</c> (not started),
        /// <c>@build-1of3</c> (being built: the construction key) or <c>@done</c> (restored).
        /// </summary>
        public static string ProjectVisualKeyFor(string projectId, ProjectStatus status, int stage, int stages) =>
            status == ProjectStatus.Completed ? ProjectDoneKeyFor(projectId)
            : status == ProjectStatus.Active ? ConstructionKeyFor(projectId, stage, Math.Max(1, stages))
            : projectId + "@ruined";

        /// <summary>D-176: the restored key of a project, e.g. <c>proj.rebuild-trail-bridges@done</c>.</summary>
        public static string ProjectDoneKeyFor(string projectId) => projectId + "@done";

        /// <summary>D-176: a project's visual row anywhere in the world, or null.</summary>
        public ProjectVisual? FindProject(string projectId)
        {
            foreach (var l in Locations)
                foreach (var p in l.ProjectVisuals)
                    if (string.Equals(p.ProjectId, projectId, StringComparison.Ordinal)) return p;
            return null;
        }

        public LocationRow? Find(string locationId)
        {
            foreach (var l in Locations) if (string.Equals(l.LocationId, locationId, StringComparison.Ordinal)) return l;
            return null;
        }

        public sealed class LocationRow
        {
            public LocationRow(string locationId, string title, RestorationStage stage, bool isUnlocked, bool isInteractable,
                IReadOnlyList<string> projectIds, IReadOnlyList<string> selectableProjectIds, string? activeProjectId,
                string? constructionKey, int discoveriesFound, IReadOnlyList<string> characterIds,
                IReadOnlyList<PendingLine> pendingDialogue, IReadOnlyList<PendingLine>? currentLines = null,
                int constructionStage = 0, int constructionStages = 1, string? focusProjectId = null,
                IReadOnlyList<ProjectVisual>? projectVisuals = null)
            {
                ProjectVisuals = projectVisuals ?? Array.Empty<ProjectVisual>();
                CurrentLines = currentLines ?? Array.Empty<PendingLine>();
                FocusProjectId = focusProjectId;
                ConstructionStage = constructionStage; ConstructionStages = constructionStages;
                LocationId = locationId; Title = title; Stage = stage; IsUnlocked = isUnlocked; IsInteractable = isInteractable;
                ProjectIds = projectIds; SelectableProjectIds = selectableProjectIds; ActiveProjectId = activeProjectId;
                ConstructionKey = constructionKey; DiscoveriesFound = discoveriesFound; CharacterIds = characterIds;
                PendingDialogue = pendingDialogue;
            }

            public string LocationId { get; }
            /// <summary>Title through the ContentText overlay.</summary>
            public string Title { get; }
            public RestorationStage Stage { get; }
            public string VariantKey => VariantKeyFor(LocationId, Stage);
            /// <summary>Reached: restored at least once, or one of its projects is no longer locked.</summary>
            public bool IsUnlocked { get; }
            /// <summary>Unlocked and has something to do: a goal to pick, work underway, someone to talk to or a find to inspect.</summary>
            public bool IsInteractable { get; }
            /// <summary>Every project whose work happens here, in content order.</summary>
            public IReadOnlyList<string> ProjectIds { get; }
            /// <summary>Restoration goals the player can choose here (Available or Queued).</summary>
            public IReadOnlyList<string> SelectableProjectIds { get; }
            /// <summary>The active project, when its work happens here.</summary>
            public string? ActiveProjectId { get; }
            /// <summary>Visible construction stage of the active project here (D-071), or null.</summary>
            public string? ConstructionKey { get; }
            /// <summary>Build stage of the active project here (0 when none), of <see cref="ConstructionStages"/>.</summary>
            public int ConstructionStage { get; }
            public int ConstructionStages { get; }
            public int DiscoveriesFound { get; }
            /// <summary>Characters currently at this location.</summary>
            public IReadOnlyList<string> CharacterIds { get; }
            /// <summary>Present characters whose current line the presentation has not marked seen.</summary>
            public IReadOnlyList<PendingLine> PendingDialogue { get; }
            public bool HasPendingDialogue => PendingDialogue.Count > 0;
            /// <summary>Every present character's current line, seen or not (for diffs and revisiting).</summary>
            public IReadOnlyList<PendingLine> CurrentLines { get; }
            /// <summary>
            /// KI-027(d): the project this place is "about" right now, for Inspect / Help restore
            /// (bible §15–§16): the active project here, else the first selectable goal here, else
            /// the last project restored here (content order), else null. Derived; never saved.
            /// </summary>
            public string? FocusProjectId { get; }
            /// <summary>
            /// D-176 (KI-027 a): every project here with its own visual key (props such as the bridge),
            /// in content order. Lets a place show a repaired prop even when its landmark stage does not change.
            /// </summary>
            public IReadOnlyList<ProjectVisual> ProjectVisuals { get; }
        }

        /// <summary>D-176: one project's visible state at its place. Derived; never saved.</summary>
        public sealed class ProjectVisual
        {
            public ProjectVisual(string projectId, string locationId, ProjectStatus status, int buildStage, int buildStages)
            {
                ProjectId = projectId; LocationId = locationId; Status = status;
                BuildStage = status == ProjectStatus.Active ? buildStage : 0;
                BuildStages = Math.Max(1, buildStages);
            }

            public string ProjectId { get; }
            public string LocationId { get; }
            public ProjectStatus Status { get; }
            /// <summary>Visible build stage while active (0 otherwise), of <see cref="BuildStages"/> (the authored D-071 count).</summary>
            public int BuildStage { get; }
            public int BuildStages { get; }
            public bool IsCompleted => Status == ProjectStatus.Completed;
            /// <summary><c>&lt;id&gt;@ruined</c>, <c>&lt;id&gt;@build-&lt;k&gt;of&lt;n&gt;</c> or <c>&lt;id&gt;@done</c>.</summary>
            public string VisualKey => ProjectVisualKeyFor(ProjectId, Status, BuildStage, BuildStages);
        }

        public sealed class PendingLine
        {
            public PendingLine(string characterId, string lineId) { CharacterId = characterId; LineId = lineId; }
            public string CharacterId { get; }
            public string LineId { get; }
        }
    }

    /// <summary>
    /// D-165: one character with every line reached so far (revisitable) and the current one.
    /// Lines are localized through ContentText. Nothing here can gate progression.
    /// </summary>
    public sealed class CharacterDialogueReadModel
    {
        public CharacterDialogueReadModel(string characterId, string name, string portraitKey, string? locationId,
            IReadOnlyList<LineRow> reachedLines)
        {
            CharacterId = characterId; Name = name; PortraitKey = portraitKey; LocationId = locationId;
            ReachedLines = reachedLines ?? throw new ArgumentNullException(nameof(reachedLines));
        }

        public string CharacterId { get; }
        public string Name { get; }
        public string PortraitKey { get; }
        /// <summary>Where the character is now; null while they have not arrived.</summary>
        public string? LocationId { get; }
        public bool IsPresent => LocationId != null;
        /// <summary>Reached lines in order. Empty while absent.</summary>
        public IReadOnlyList<LineRow> ReachedLines { get; }
        public LineRow? CurrentLine => ReachedLines.Count == 0 ? null : ReachedLines[ReachedLines.Count - 1];

        public sealed class LineRow
        {
            public LineRow(string lineId, int order, string? milestoneProjectId, string expression, string text)
            { LineId = lineId; Order = order; MilestoneProjectId = milestoneProjectId; Expression = expression; Text = text; }
            public string LineId { get; }
            public int Order { get; }
            public string? MilestoneProjectId { get; }
            public string Expression { get; }
            public string Text { get; }
        }
    }
}
