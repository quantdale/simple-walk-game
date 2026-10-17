using System;
using System.Collections.Generic;

namespace WalkGame.Application.ReadModels
{
    public enum VisualChangeKind
    {
        LocationUnlocked = 0,
        LandmarkStageChanged = 1,
        ConstructionAdvanced = 2,
        CharacterArrived = 3,
        CharacterMoved = 4,
        DialogueAvailable = 5,
        EcologyAdvanced = 6,
        SettlementAdvanced = 7,
        RegionCompleted = 8,
        /// <summary>
        /// D-176 (KI-027 a): a project was completed at <see cref="VisualChangeEvent.LocationId"/>; the subject is the
        /// project and <see cref="VisualChangeEvent.ToKey"/> its <c>@done</c> key (the bridge transition, bible §25).
        /// <see cref="VisualChangeEvent.FromKey"/> is null: the before-state is whatever the presenter is showing.
        /// Emitted first in a completion's batch.
        /// </summary>
        ProjectCompleted = 9,
    }

    /// <summary>
    /// D-167 (OpenSpec add-interactive-world-presentation, task 4): one visible transformation for a
    /// presentation to animate. Derived, never persisted.
    /// </summary>
    public sealed class VisualChangeEvent
    {
        /// <summary>D-180: <see cref="CompletionId"/> of the region-completion event.</summary>
        public const string RegionCompletionId = "region";

        public VisualChangeEvent(int sequence, VisualChangeKind kind, DateTimeOffset? atUtc, string? locationId,
            string? subjectId, string? fromKey, string? toKey, string? completionId = null)
        { Sequence = sequence; Kind = kind; AtUtc = atUtc; LocationId = locationId; SubjectId = subjectId; FromKey = fromKey; ToKey = toKey; CompletionId = completionId; }

        /// <summary>
        /// D-180 (KI-028): the completion that caused this change, a stable id: the completed project's id, or
        /// <see cref="RegionCompletionId"/>. Every event of one completion's batch shares it. Presentation memory
        /// records which completions were shown by this id, so a completion stamped earlier than the last shown
        /// instant (a device clock corrected backward) is still replayed exactly once. Null for in-session diffs.
        /// </summary>
        public string? CompletionId { get; }

        /// <summary>Position in the stream. Stable for the timestamp-derived stream regardless of the "since" filter.</summary>
        public int Sequence { get; }
        public VisualChangeKind Kind { get; }
        /// <summary>Canonical time of the change; null for in-session diffs.</summary>
        public DateTimeOffset? AtUtc { get; }
        public string? LocationId { get; }
        /// <summary>The completed project, the character, or null for region-level changes.</summary>
        public string? SubjectId { get; }
        /// <summary>Before: variant/construction key, previous location, previous line id or arc stage.</summary>
        public string? FromKey { get; }
        /// <summary>After: variant/construction key, new location, new line id or arc stage.</summary>
        public string? ToKey { get; }

        /// <summary>Identity without sequence, time or subject, for comparing streams from different sources.</summary>
        public string Shape => Kind + "@" + LocationId + ":" + FromKey + ">" + ToKey;

        public override string ToString() => Sequence + ":" + Shape + " (" + SubjectId + ", " + AtUtc?.ToString("u") + ")";
    }
}
