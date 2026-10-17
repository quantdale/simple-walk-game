using System;
using System.Collections.Generic;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// D-167 (OpenSpec add-interactive-world-presentation, task 3): what each restoration goal the
    /// player can pick (or is working on) would change. Consequences are computed with the same rules
    /// <c>OfflineAdvancer.ApplyCompletionEffects</c> applies, from authored content and canonical state,
    /// and never name an unfound discovery (D-155).
    /// </summary>
    public sealed class RestorationChoicesReadModel
    {
        public RestorationChoicesReadModel(IReadOnlyList<Choice> choices) =>
            Choices = choices ?? throw new ArgumentNullException(nameof(choices));

        /// <summary>Available, Queued and Active projects in content order.</summary>
        public IReadOnlyList<Choice> Choices { get; }

        public Choice? Find(string projectId)
        {
            foreach (var c in Choices) if (string.Equals(c.ProjectId, projectId, StringComparison.Ordinal)) return c;
            return null;
        }

        public sealed class Choice
        {
            public Choice(string projectId, string title, string? locationId, ProjectStatus status, long vitalityCost,
                long vitalityInvested, IReadOnlyDictionary<ResourceType, long> resourceCosts, bool canAfford,
                string? beforeVariantKey, string? afterVariantKey, bool isClosureMilestone, IReadOnlyList<Consequence> consequences,
                string? projectVisualKey = null)
            {
                ProjectVisualKey = projectVisualKey ?? projectId + "@ruined";
                ProjectId = projectId; Title = title; LocationId = locationId; Status = status; VitalityCost = vitalityCost;
                VitalityInvested = vitalityInvested; ResourceCosts = resourceCosts; CanAfford = canAfford;
                BeforeVariantKey = beforeVariantKey; AfterVariantKey = afterVariantKey; IsClosureMilestone = isClosureMilestone;
                Consequences = consequences;
            }

            public string ProjectId { get; }
            public string Title { get; }
            public string? LocationId { get; }
            public ProjectStatus Status { get; }
            public long VitalityCost { get; }
            public long VitalityInvested { get; }
            public IReadOnlyDictionary<ResourceType, long> ResourceCosts { get; }
            /// <summary>Secondary-resource requirements are met now (OfflineAdvancer.CanAffordRequirements).</summary>
            public bool CanAfford { get; }
            /// <summary>The project's own location now and after it completes (equal when its place does not change stage).</summary>
            public string? BeforeVariantKey { get; }
            public string? AfterVariantKey { get; }
            /// <summary>The region's completion milestone. Closure still needs the chain minimum (D-159).</summary>
            public bool IsClosureMilestone { get; }
            public IReadOnlyList<Consequence> Consequences { get; }
            /// <summary>D-176: the project's own visual now (<c>@ruined</c> or <c>@build-k-of-n</c>), the before image of a prop such as the bridge.</summary>
            public string ProjectVisualKey { get; }
            /// <summary>D-176: the project's restored visual (<c>@done</c>), the after preview of a prop.</summary>
            public string DoneVisualKey => WorldViewReadModel.ProjectDoneKeyFor(ProjectId);
        }
    }

    public enum ConsequenceKind
    {
        LandmarkStage = 0,
        ProjectOpens = 1,
        ProducerUnlocks = 2,
        EcologyAdvances = 3,
        SettlementAdvances = 4,
        /// <summary>Something will turn up at <see cref="Consequence.LocationId"/>; never says what.</summary>
        DiscoveryAtLocation = 5,
        CharacterArrives = 6,
        CharacterMoves = 7,
        NewDialogue = 8,
        ChapterUnlocks = 9,
    }

    /// <summary>One typed effect of completing a project.</summary>
    public sealed class Consequence
    {
        public Consequence(ConsequenceKind kind, string? subjectId, string? locationId, string? fromKey, string? toKey, int? stage = null)
        { Kind = kind; SubjectId = subjectId; LocationId = locationId; FromKey = fromKey; ToKey = toKey; Stage = stage; }

        public ConsequenceKind Kind { get; }
        /// <summary>Landmark, project, producer, character or chapter id. Null for a discovery.</summary>
        public string? SubjectId { get; }
        public string? LocationId { get; }
        /// <summary>Variant key before (landmark) or previous location (character move).</summary>
        public string? FromKey { get; }
        /// <summary>Variant key after (landmark), new location (character) or new line id (dialogue).</summary>
        public string? ToKey { get; }
        /// <summary>Arc stage reached (ecology/settlement).</summary>
        public int? Stage { get; }

        public override string ToString() => Kind + ":" + SubjectId + "@" + LocationId + ":" + FromKey + ">" + ToKey + ":" + Stage;
    }
}
