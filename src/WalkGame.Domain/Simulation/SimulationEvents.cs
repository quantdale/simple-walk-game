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

    /// <summary>A discovery unlocked from a canonical trigger; fires at most once (idempotent).</summary>
    public sealed record DiscoveryUnlocked : SimulationEvent
    {
        public string DiscoveryId { get; }

        public DiscoveryUnlocked(DateTimeOffset atUtc, string discoveryId) : base(atUtc)
        {
            DiscoveryId = discoveryId ?? string.Empty;
        }
    }

    /// <summary>An expedition route became available; fires at most once per route.</summary>
    public sealed record ExpeditionAvailable : SimulationEvent
    {
        public string ExpeditionId { get; }

        public ExpeditionAvailable(DateTimeOffset atUtc, string expeditionId) : base(atUtc)
        {
            ExpeditionId = expeditionId ?? string.Empty;
        }
    }

    /// <summary>
    /// An expedition completed deterministically. <see cref="UnitsGranted"/> records the
    /// cap-clamped reward actually applied in the same state transition.
    /// </summary>
    public sealed record ExpeditionCompleted : SimulationEvent
    {
        public string ExpeditionId { get; }
        public Economy.ResourceType? RewardType { get; }
        public long UnitsGranted { get; }

        public ExpeditionCompleted(DateTimeOffset atUtc, string expeditionId,
            Economy.ResourceType? rewardType, long unitsGranted) : base(atUtc)
        {
            ExpeditionId = expeditionId ?? string.Empty;
            RewardType = rewardType;
            UnitsGranted = unitsGranted;
        }
    }

    public enum RegionProgressionAxis
    {
        Ecology = 0,
        Settlement = 1,
    }

    /// <summary>A region-level progression arc advanced one discrete stage; monotonic, idempotent.</summary>
    public sealed record RegionProgressionAdvanced : SimulationEvent
    {
        public RegionProgressionAxis Axis { get; }
        public int Stage { get; }

        public RegionProgressionAdvanced(DateTimeOffset atUtc, RegionProgressionAxis axis, int stage) : base(atUtc)
        {
            Axis = axis;
            Stage = stage;
        }
    }

    /// <summary>The region closure milestone was reached; never resets afterwards.</summary>
    public sealed record RegionCompleted : SimulationEvent
    {
        public string MilestoneProjectId { get; }

        public RegionCompleted(DateTimeOffset atUtc, string milestoneProjectId) : base(atUtc)
        {
            MilestoneProjectId = milestoneProjectId ?? string.Empty;
        }
    }
}
