using System;
using System.Collections.Generic;
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
                int constructionStage = 0, int constructionStages = 1)
            {
                CurrentLines = currentLines ?? Array.Empty<PendingLine>();
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
