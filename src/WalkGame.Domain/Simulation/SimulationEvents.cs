using System;
using System.Collections.Generic;
using WalkGame.Domain.Regions;

namespace WalkGame.Domain.Simulation
{
    /// <summary>Base type for events produced by committed domain operations.</summary>
    public abstract record SimulationEvent(DateTimeOffset AtUtc);

    public sealed record ActivityCredited : SimulationEvent
    {
        public string TransactionId { get; }
        public long VitalityApplied { get; }

        public ActivityCredited(DateTimeOffset atUtc, string transactionId, long vitalityApplied) : base(atUtc)
        {
            TransactionId = transactionId ?? string.Empty;
            VitalityApplied = vitalityApplied;
        }
    }

    public sealed record ActivityDuplicate : SimulationEvent
    {
        public string TransactionId { get; }

        public ActivityDuplicate(DateTimeOffset atUtc, string transactionId) : base(atUtc)
        {
            TransactionId = transactionId ?? string.Empty;
        }
    }

    /// <summary>A correction/deletion adjustment committed by the trust pipeline.</summary>
    public sealed record ActivityCorrected : SimulationEvent
    {
        public string TransactionId { get; }
        public long VitalityApplied { get; }

        public ActivityCorrected(DateTimeOffset atUtc, string transactionId, long vitalityApplied) : base(atUtc)
        {
            TransactionId = transactionId ?? string.Empty;
            VitalityApplied = vitalityApplied;
        }
    }

    public sealed record ProjectBecameAvailable : SimulationEvent
    {
        public string ProjectId { get; }

        public ProjectBecameAvailable(DateTimeOffset atUtc, string projectId) : base(atUtc)
        {
            ProjectId = projectId ?? string.Empty;
        }
    }

    public sealed record ProjectBecameActive : SimulationEvent
    {
        public string ProjectId { get; }

        public ProjectBecameActive(DateTimeOffset atUtc, string projectId) : base(atUtc)
        {
            ProjectId = projectId ?? string.Empty;
        }
    }

    public sealed record ProjectCompleted : SimulationEvent
    {
        public string ProjectId { get; }

        public ProjectCompleted(DateTimeOffset atUtc, string projectId) : base(atUtc)
        {
            ProjectId = projectId ?? string.Empty;
        }
    }

    public sealed record LandmarkStageReached : SimulationEvent
    {
        public string LandmarkId { get; }
        public RestorationStage Stage { get; }

        public LandmarkStageReached(DateTimeOffset atUtc, string landmarkId, RestorationStage stage) : base(atUtc)
        {
            LandmarkId = landmarkId ?? string.Empty;
            Stage = stage;
        }
    }

    public sealed record ProducerUnlocked : SimulationEvent
    {
        public string ProducerId { get; }

        public ProducerUnlocked(DateTimeOffset atUtc, string producerId) : base(atUtc)
        {
            ProducerId = producerId ?? string.Empty;
        }
    }

    public sealed record ProducerProduced : SimulationEvent
    {
        public string ProducerId { get; }
        public long MilliUnitsGained { get; }
        public bool HitCapacity { get; }

        public ProducerProduced(DateTimeOffset atUtc, string producerId, long milliUnitsGained, bool hitCapacity) : base(atUtc)
        {
            ProducerId = producerId ?? string.Empty;
            MilliUnitsGained = milliUnitsGained;
            HitCapacity = hitCapacity;
        }
    }

    public sealed record ClockSkewIgnored : SimulationEvent
    {
        public TimeSpan AttemptedBackstep { get; }

        public ClockSkewIgnored(DateTimeOffset atUtc, TimeSpan attemptedBackstep) : base(atUtc)
        {
            AttemptedBackstep = attemptedBackstep;
        }
    }
}
