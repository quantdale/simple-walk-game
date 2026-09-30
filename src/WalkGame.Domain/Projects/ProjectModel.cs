using System;
using System.Collections.Generic;
using WalkGame.Domain.Economy;
using ProjectId = WalkGame.Domain.Common.Id<WalkGame.Domain.Common.ProjectIdKind>;

namespace WalkGame.Domain.Projects
{
    /// <summary>
    /// Canonical project state machine:
    /// Locked → Available → Queued → Active → Completed.
    /// Failure is expressed through failed operations, never as a persistent state.
    /// </summary>
    public enum ProjectStatus
    {
        Locked = 0,
        Available = 1,
        Queued = 2,
        Active = 3,
        Completed = 4,
    }

    /// <summary>Immutable content definition for a restoration project.</summary>
    public sealed class ProjectDefinition
    {
        public ProjectId Id { get; }
        public string TitleKey { get; }
        public string DescriptionKey { get; }
        public long VitalityCost { get; }
        public IReadOnlyList<ProjectId> Prerequisites { get; }

        /// <summary>
        /// Optional secondary-resource costs, consumed all-or-nothing when the project is
        /// ACTIVATED (D-054). Empty for the common case; authored for a small number of
        /// projects so that Materials and Knowledge carry a real spending decision instead
        /// of accumulating forever. Read-only; never mutated after construction.
        /// </summary>
        public IReadOnlyDictionary<ResourceType, long> ResourceCosts { get; }

        /// <summary>True when this project has at least one secondary-resource requirement.</summary>
        public bool HasResourceRequirement => ResourceCosts.Count > 0;

        public ProjectDefinition(
            ProjectId id,
            string titleKey,
            long vitalityCost,
            IEnumerable<ProjectId>? prerequisites = null,
            string? descriptionKey = null,
            IEnumerable<KeyValuePair<ResourceType, long>>? resourceCosts = null)
        {
            if (!id.IsValid)
                throw new ArgumentException("Project definition requires a valid ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(titleKey))
                throw new ArgumentException("Project definition requires a title key.", nameof(titleKey));
            if (vitalityCost <= 0L)
                throw new ArgumentException("Project vitality cost must be positive.", nameof(vitalityCost));

            Id = id;
            TitleKey = titleKey;
            DescriptionKey = descriptionKey ?? string.Empty;
            VitalityCost = vitalityCost;
            Prerequisites = prerequisites != null
                ? new List<ProjectId>(prerequisites).AsReadOnly()
                : (IReadOnlyList<ProjectId>)new List<ProjectId>().AsReadOnly();

            var costs = new Dictionary<ResourceType, long>();
            if (resourceCosts != null)
            {
                foreach (var pair in resourceCosts)
                {
                    // A non-positive secondary cost is a content authoring error, caught at
                    // construction rather than at validation time so a bad definition can
                    // never be constructed and then silently ignored.
                    if (pair.Value <= 0L)
                        throw new ArgumentException(
                            "Project '" + id.Value + "' secondary " + pair.Key + " cost must be positive.",
                            nameof(resourceCosts));
                    costs[pair.Key] = pair.Value;
                }
            }
            ResourceCosts = new Dictionary<ResourceType, long>(costs);
        }

        /// <summary>Secondary cost for a resource, or 0 when this project does not require it.</summary>
        public long ResourceCost(ResourceType type) =>
            ResourceCosts.TryGetValue(type, out var value) ? value : 0L;
    }

    /// <summary>Player-specific runtime progress for a single project.</summary>
    public sealed class ProjectState
    {
        public string ProjectId { get; set; } = string.Empty;

        public ProjectStatus Status { get; set; } = ProjectStatus.Locked;

        public long VitalityInvested { get; set; }

        public DateTimeOffset? CompletedAtUtc { get; set; }
    }

    /// <summary>
    /// Ordered project queue. Head-first ordering; at most one active project.
    /// AutoAdvance rolls completion into the next queued project without requiring
    /// the player to reopen the app.
    /// </summary>
    public sealed class ProjectQueueState
    {
        public List<string> QueuedProjectIds { get; } = new List<string>();

        public string? ActiveProjectId { get; set; }

        public bool AutoAdvance { get; set; } = true;
    }
}
