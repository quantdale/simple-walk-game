using System.Collections.Generic;
using WalkGame.Domain.Economy;
using WalkGame.Domain.Projects;

namespace WalkGame.Application.ReadModels
{
    /// <summary>
    /// Purpose-built read model for the Projects management screen: every project with
    /// status, effort, prerequisites and queue position, plus the persisted automation
    /// switch. Presentation renders this snapshot; it never inspects domain graphs.
    /// </summary>
    public sealed class ProjectsReadModel
    {
        public bool AutoAdvance { get; }

        public string? ActiveProjectId { get; }

        public IReadOnlyList<ProjectRow> Projects { get; }

        public ProjectsReadModel(bool autoAdvance, string? activeProjectId, IReadOnlyList<ProjectRow> projects)
        {
            AutoAdvance = autoAdvance;
            ActiveProjectId = activeProjectId;
            Projects = projects;
        }

        public sealed class ProjectRow
        {
            public string ProjectId { get; }
            public string TitleKey { get; }
            public long VitalityCost { get; }
            public long VitalityInvested { get; }
            public ProjectStatus Status { get; }

            /// <summary>Zero-based position in the ordered queue; null when not queued.</summary>
            public int? QueuedPosition { get; }

            public IReadOnlyList<string> PrerequisiteIds { get; }

            /// <summary>Authored secondary-resource costs, consumed when the project is
            /// activated (D-054). Empty for the common case.</summary>
            public IReadOnlyDictionary<ResourceType, long> ResourceCosts { get; }

            /// <summary>True when every secondary cost of this project is currently met.
            /// Purely informational: a project with an unmet requirement is not activatable,
            /// and auto-advance stalls at it rather than skipping past it.</summary>
            public bool RequirementsAffordable { get; }

            public ProjectRow(
                string projectId,
                string titleKey,
                long vitalityCost,
                long vitalityInvested,
                ProjectStatus status,
                int? queuedPosition,
                IReadOnlyList<string> prerequisiteIds,
                IReadOnlyDictionary<ResourceType, long>? resourceCosts = null,
                bool requirementsAffordable = true)
            {
                ProjectId = projectId;
                TitleKey = titleKey;
                VitalityCost = vitalityCost;
                VitalityInvested = vitalityInvested;
                Status = status;
                QueuedPosition = queuedPosition;
                PrerequisiteIds = prerequisiteIds;
                ResourceCosts = resourceCosts ?? new Dictionary<ResourceType, long>();
                RequirementsAffordable = requirementsAffordable;
            }

            /// <summary>Secondary cost for a resource, or 0 when this project does not require it.</summary>
            public long ResourceCost(ResourceType type) =>
                ResourceCosts.TryGetValue(type, out var value) ? value : 0L;
        }
    }
}
