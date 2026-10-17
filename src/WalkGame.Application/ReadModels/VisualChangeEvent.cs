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
    }

    /// <summary>
    /// D-167 (OpenSpec add-interactive-world-presentation, task 4): one visible transformation for a
    /// presentation to animate. Derived, never persisted.
    /// </summary>
    public sealed class VisualChangeEvent
    {
        public VisualChangeEvent(int sequence, VisualChangeKind kind, DateTimeOffset? atUtc, string? locationId,
            string? subjectId, string? fromKey, string? toKey)
        { Sequence = sequence; Kind = kind; AtUtc = atUtc; LocationId = locationId; SubjectId = subjectId; FromKey = fromKey; ToKey = toKey; }

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
