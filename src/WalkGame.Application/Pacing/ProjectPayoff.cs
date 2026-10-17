using System;
using WalkGame.Domain.Activity;

namespace WalkGame.Application.Pacing
{
    /// <summary>
    /// D-145: display-time progress for the active project, derived only from canonical state
    /// (invested Vitality, authored cost and authored visible build stages). Never persisted, so
    /// it needs no save or content change. It exists so a very short session pays off: a
    /// 300-step walk on the 300-Vitality first project is shown as "1% restored" with the
    /// distance to the next thing the player will see, instead of only a raw credit number.
    /// Integer math only.
    /// </summary>
    public readonly struct ProjectPayoff
    {
        /// <summary>Percent restored, 1..99 while the project is funded but incomplete.</summary>
        public int Percent { get; }

        /// <summary>Vitality still needed to reach the next visible change (build stage or completion).</summary>
        public long VitalityToNext { get; }

        /// <summary>Approximate steps for <see cref="VitalityToNext"/> at the canonical conversion rate.</summary>
        public long StepsToNext => VitalityToNext * StepConversionRuleV1.StepsPerVitality;

        /// <summary>True when the next visible change is an intermediate build stage, false when it is completion.</summary>
        public bool NextIsBuildStage { get; }

        private ProjectPayoff(int percent, long vitalityToNext, bool nextIsBuildStage)
        {
            Percent = percent;
            VitalityToNext = vitalityToNext;
            NextIsBuildStage = nextIsBuildStage;
        }

        /// <summary>
        /// Payoff for a project, or null when there is nothing to report (no cost, nothing
        /// invested yet, or already complete).
        /// </summary>
        public static ProjectPayoff? For(long invested, long cost, int visibleBuildStages)
        {
            if (cost <= 0L || invested <= 0L || invested >= cost) return null;
            int stages = Math.Max(1, visibleBuildStages);

            long rawPercent = invested * 100L / cost;
            int percent = (int)Math.Max(1L, Math.Min(99L, rawPercent));

            // Same rule as ProjectDefinition.BuildStageFor: stage = floor(invested * stages / cost).
            long stage = Math.Min(invested * stages / cost, stages - 1L);
            long nextStage = stage + 1L;
            if (nextStage < stages)
            {
                // Smallest investment whose floor reaches nextStage: ceil(nextStage * cost / stages).
                long threshold = (nextStage * cost + stages - 1L) / stages;
                return new ProjectPayoff(percent, threshold - invested, true);
            }
            return new ProjectPayoff(percent, cost - invested, false);
        }
    }
}
