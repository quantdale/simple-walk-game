using System;
using System.Collections.Generic;
using WalkGame.Domain.Expeditions;
using WalkGame.Domain.Projects;
using WalkGame.Domain.Regions;

namespace WalkGame.Domain.Validation
{
    /// <summary>
    /// Static validation for authored content. Invalid content must fail validation before
    /// any runtime exists — never appear as silent runtime corruption. This is a release
    /// gate for the M4 Region 1 contract (campaign workstream E): reference integrity,
    /// cycle/deadlock freedom, reachability of every piece of content and of the closure
    /// milestone, arc monotonicity, discovery/expedition integrity, and overflow safety.
    ///
    /// Validation is order-independent: every ID set is collected before any reference is
    /// resolved, so valid forward references are never falsely rejected.
    /// </summary>
    public static class ContentValidator
    {
        /// <summary>Sanity bound keeping summed chain costs far away from overflow.</summary>
        private const long MaxTotalVitalityCost = 1_000_000_000L;

        /// <summary>Sane upper bound on authored progression stages per axis.</summary>
        private const int MaxStagesPerArc = 10;

        /// <summary>
        /// The largest authored producer rate the production tick can represent without
        /// wrapping a <see cref="long"/> (D-058). DERIVED from the tick's real arithmetic by
        /// <see cref="ProducerArithmetic"/>, not chosen as a round number.
        ///
        /// The previous value here was 1e12, which was wrong by roughly five orders of
        /// magnitude: <c>TickProducers</c> multiplies the sub-day remainder
        /// (<c>&lt; TicksPerDay</c> ≈ 8.64e11) by the rate, so anything above ~1.07e7
        /// overflowed. C# arithmetic there is unchecked, so the product wrapped instead of
        /// throwing, and a wrapped value drove <c>StoredMilliUnits</c> NEGATIVE — which
        /// <c>GameStateValidator</c> rejects on the next boot, making the player's world
        /// unloadable from a rate this validator had explicitly approved.
        ///
        /// Declared `static readonly` rather than `const` because it is DERIVED: a
        /// compile-time constant cannot reference the simulation's maximum interval, and
        /// hardcoding the number is precisely the failure this change exists to remove.
        /// </summary>
        public static readonly long MaxMilliUnitsPerDay = ProducerArithmetic.MaxRepresentableMilliUnitsPerDay;

        /// <summary>
        /// Re-derives the representability bound from the simulation's own interval and the
        /// integer range, and reports any disagreement with the published bound.
        ///
        /// This exists because a bound that merely *looks* right is not a bound. If the
        /// published constant is ever edited by hand, or the simulation's maximum interval
        /// changes, the two drift silently and the validator starts approving content the
        /// simulation cannot represent. The check is cheap (once per content validation)
        /// and turns that class of drift into a loud, named violation.
        ///
        /// Returns every problem found, so a single run reports all of them. The published
        /// bound is a PARAMETER rather than a default so the self-check itself can be
        /// exercised with a deliberately perturbed value; production passes
        /// <see cref="MaxMilliUnitsPerDay"/>.
        /// </summary>
        public static IReadOnlyList<string> CheckNumericBoundsAgainstSimulation(
            long publishedMaxMilliUnitsPerDay)
        {
            var problems = new List<string>();

            long derived = ProducerArithmetic.DeriveMaxRepresentableMilliUnitsPerDay();
            if (publishedMaxMilliUnitsPerDay != derived)
            {
                problems.Add(
                    "Content validator producer-rate bound is out of step with the simulation: published "
                    + publishedMaxMilliUnitsPerDay + ", derived " + derived
                    + " (from MaxInterval " + ProducerArithmetic.MaxInterval
                    + " and the sub-day remainder < " + TimeSpan.TicksPerDay + ").");
            }

            // Record WHICH term binds rather than assuming it. If a future change to
            // MaxInterval made the whole-day term the tighter constraint, the
            // sub-day-term reasoning in the docs and in ProducerArithmetic would be wrong
            // and the validator would no longer be deriving from the real formula.
            if (!ProducerArithmetic.SubDayRemainderBinds)
            {
                problems.Add(
                    "Producer-rate bound is no longer bound by the sub-day remainder term ("
                    + ProducerArithmetic.SubDayTermBound + ") but by the whole-day term ("
                    + ProducerArithmetic.WholeDayTermBound + "); the documented derivation is stale.");
            }

            return problems;
        }

        /// <summary>
        /// Every resource a producer can emit must have an explicit, positive, representable
        /// authored cap. A missing cap used to be a silent runtime default of
        /// <see cref="long.MaxValue"/>, which is how the cap/parking machinery in
        /// <c>OfflineAdvancer</c> became unreachable in production (D-054).
        /// </summary>
        private static void ValidateResourceCaps(RegionDefinition content, List<string> violations)
        {
            foreach (var cap in content.ResourceCaps)
            {
                // Vitality is the activity-derived progression resource and is deliberately
                // NOT cappable (D-054). GameFactory applies every authored cap to a fresh
                // balance, so an authored Vitality cap would not merely be wrong in intent --
                // it would silently clamp real walking credit away, which is the exact
                // data-loss defect the exemption exists to prevent. Fail closed at authoring
                // time rather than trusting a future campaign to remember.
                if (cap.Key == Economy.ResourceType.Vitality)
                    violations.Add(
                        $"Resource cap for {cap.Key} is forbidden: Vitality is activity-derived, is spent " +
                        "against authored project costs, and has no producer store to park refused units in, " +
                        "so a cap would silently discard earned walking credit (D-054).");
                else if (cap.Value <= 0L)
                    violations.Add($"Resource cap for {cap.Key} must be positive, but is {cap.Value}.");

                // The producer store capacity a cap implies must stay representable, and
                // the cap must be able to hold at least one whole unit of output.
                if (cap.Value > long.MaxValue / ProducerDefinition.MilliUnitsPerUnit)
                    violations.Add($"Resource cap for {cap.Key} is too large to represent safely.");
            }

            foreach (var producer in content.Producers)
            {
                if (!content.ResourceCaps.ContainsKey(producer.Output))
                {
                    violations.Add(
                        $"Producer '{producer.Id}' emits {producer.Output} but the region authors no cap for it; " +
                        "every produced resource must declare an explicit cap.");
                }
            }
        }

        /// <summary>
        /// A secondary-resource requirement that the region's own producers can never
        /// supply within the cap is a content authoring failure: it would make a project
        /// permanently unactivatable, and if that project is on the path to the closure
        /// milestone the region becomes uncompletable. Both rules name project and resource.
        /// </summary>
        private static void ValidateSecondaryRequirements(RegionDefinition content, List<string> violations)
        {
            // Head-room available for a resource across the design horizon: the authored
            // cap minus what the region's own producers of that resource can deliver in
            // MaxRequirementHorizonDays. Expedition grants are not counted here because an
            // expedition is a bonus, not a guaranteed source; a requirement that only an
            // expedition can satisfy is still satisfiable, so counting producers alone is
            // the conservative check.
            const long MaxRequirementHorizonDays = 400L;

            foreach (var project in content.Projects)
            {
                foreach (var requirement in project.ResourceCosts)
                {
                    var resource = requirement.Key;
                    var cost = requirement.Value;

                    long cap = content.ResourceCap(resource) ?? long.MaxValue;
                    if (cost > cap)
                    {
                        violations.Add(
                            $"Project '{project.Id}' requires {cost} {resource}, which exceeds the region's " +
                            $"authored cap of {cap} {resource}; the project could never be activated.");
                        continue;
                    }

                    if (resource == Economy.ResourceType.Vitality)
                        continue; // Vitality is unbounded by design: it is the activity sink.

                    long producedUnits = 0L;
                    bool anyProducer = false;
                    foreach (var producer in content.Producers)
                    {
                        if (producer.Output != resource)
                            continue;
                        anyProducer = true;
                        // What the region can actually DELIVER inside the horizon is its
                        // production rate, not its store size: parked units flush
                        // repeatedly over time, so total deliverable is bounded by
                        // (whole units/day × horizon), not by capacity. Integer division
                        // matches the simulation's own whole-unit delivery, so the check
                        // cannot disagree with what a tick will really produce.
                        long unitsPerDay = producer.MilliUnitsPerDay / ProducerDefinition.MilliUnitsPerUnit;
                        long perHorizon = unitsPerDay > long.MaxValue / MaxRequirementHorizonDays
                            ? long.MaxValue
                            : unitsPerDay * MaxRequirementHorizonDays;
                        producedUnits = producedUnits > long.MaxValue - perHorizon
                            ? long.MaxValue
                            : producedUnits + perHorizon;
                    }

                    if (!anyProducer)
                    {
                        violations.Add(
                            $"Project '{project.Id}' requires {cost} {resource} but the region authors no producer for {resource}.");
                        continue;
                    }

                    if (producedUnits < cost)
                    {
                        violations.Add(
                            $"Project '{project.Id}' requires {cost} {resource} but the region's producers can " +
                            $"supply only {producedUnits} {resource} within the {MaxRequirementHorizonDays}-day design horizon.");
                    }
                }
            }
        }

        /// <summary>
        /// A retired identifier must be RETIRED: if it is also a live definition, the
        /// tombstone is dead weight and, worse, a live reference silently resolving to
        /// "retired" would grant nothing for content that is supposed to be playable. A
        /// live project may not also list a tombstoned identifier as a prerequisite, because
        /// a live chain that depends on retired content is not completable.
        /// </summary>
        private static void ValidateTombstones(
            RegionDefinition content,
            HashSet<string> projectIds,
            List<string> violations)
        {
            if (content.Tombstones.Count == 0)
                return;

            var liveIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var project in content.Projects) liveIds.Add(project.Id.Value);
            foreach (var landmark in content.Landmarks) liveIds.Add(landmark.Id.Value);
            foreach (var producer in content.Producers) liveIds.Add(producer.Id.Value);
            foreach (var discovery in content.Discoveries) liveIds.Add(discovery.Id.Value);
            foreach (var expedition in content.Expeditions) liveIds.Add(expedition.Id.Value);
            foreach (var program in content.PostCompletionPrograms) liveIds.Add(program.Id);

            foreach (var pair in content.Tombstones)
            {
                string id = pair.Key;
                var tombstone = pair.Value;

                if (string.IsNullOrWhiteSpace(tombstone.Reason))
                    violations.Add($"Content tombstone '{id}' has no reason.");
                if (liveIds.Contains(id))
                    violations.Add($"Identifier '{id}' is both a live definition and a content tombstone; a tombstone must only cover RETIRED content.");
            }

            foreach (var project in content.Projects)
            {
                foreach (var prerequisite in project.Prerequisites)
                {
                    if (content.IsTombstoned(prerequisite.Value))
                    {
                        violations.Add(
                            $"Live project '{project.Id}' lists tombstoned prerequisite '{prerequisite.Value}'; " +
                            "a live chain cannot depend on retired content.");
                    }
                }
            }
        }

        /// <summary>
        /// Post-completion programs (D-061) are validated as authored content under every rule
        /// that applies to ordinary nodes — unique identity, resolvable references, positive
        /// and representable costs, and being enabled by the completed region — with ONE
        /// deliberate exemption: they are NOT subject to the entry-reachability rule, because
        /// a program is reachable only from the closure milestone. Reachability is a property
        /// of content that must be earned; a post-completion program is earned by definition.
        ///
        /// The exemption is written down here, and nowhere else, so that the tempting "fix"
        /// for a newly authored program — deleting it because the reachability pass flagged it
        /// — is visibly the wrong move.
        ///
        /// The reverse trap is guarded too: a program identifier that also exists as ordinary
        /// content was authored in the wrong collection (i.e. it is NOT declared
        /// post-completion) and is reported as ordinary content would be.
        /// </summary>
        private static void ValidatePostCompletionPrograms(RegionDefinition content, List<string> violations)
        {
            var liveContentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var project in content.Projects) liveContentIds.Add(project.Id.Value);
            foreach (var landmark in content.Landmarks) liveContentIds.Add(landmark.Id.Value);
            foreach (var producer in content.Producers) liveContentIds.Add(producer.Id.Value);
            foreach (var discovery in content.Discoveries) liveContentIds.Add(discovery.Id.Value);
            foreach (var expedition in content.Expeditions) liveContentIds.Add(expedition.Id.Value);

            var seenPrograms = new HashSet<string>(StringComparer.Ordinal);
            foreach (var program in content.PostCompletionPrograms)
            {
                if (!seenPrograms.Add(program.Id))
                    violations.Add($"Duplicate post-completion program ID '{program.Id}'.");

                if (liveContentIds.Contains(program.Id))
                    violations.Add(
                        $"Identifier '{program.Id}' is authored both as ordinary content and as a post-completion program; "
                        + "a post-completion program is declared by being in the declared post-completion set, not by "
                        + "also existing as restoration content.");

                if (program.VitalityCostPerCycle <= 0L)
                    violations.Add($"Post-completion program '{program.Id}' has a non-positive cycle cost.");
                if (program.VitalityCostPerCycle > MaxTotalVitalityCost)
                    violations.Add($"Post-completion program '{program.Id}' cycle cost is unrepresentably large.");

                if (string.IsNullOrEmpty(content.CompletionMilestoneProjectId))
                {
                    violations.Add(
                        $"Post-completion program '{program.Id}' is authored but the region defines no completion milestone to enable it.");
                    continue;
                }

                if (!string.Equals(program.EnabledByMilestoneProjectId, content.CompletionMilestoneProjectId, StringComparison.Ordinal))
                    violations.Add(
                        $"Post-completion program '{program.Id}' is enabled by project '{program.EnabledByMilestoneProjectId}', "
                        + $"which is not the region's closure milestone '{content.CompletionMilestoneProjectId}'; "
                        + "a post-completion program may only be enabled by the completed region.");
            }
        }

        /// <summary>
        /// Restoration chains and the closure criterion (D-062).
        ///
        /// The criterion is the contract this whole rule group exists to protect: "the
        /// region is finished" must be expressible in content, must be reachable by the
        /// projects the reachability pass has already proven reachable, and must require a
        /// strong enough majority that the ending cannot be reached cheaply. A criterion that
        /// cannot be met is a content failure named for the criterion, not a profile that
        /// silently fails to finish.
        /// </summary>
        private static void ValidateChainsAndClosureCriterion(RegionDefinition content, HashSet<string> projectIds, List<string> violations)
        {
            var seenChains = new HashSet<string>(StringComparer.Ordinal);
            var chainMembers = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var chain in content.Chains)
            {
                if (!seenChains.Add(chain.Id))
                    violations.Add($"Duplicate restoration chain ID '{chain.Id}'.");

                foreach (var projectId in chain.ProjectIds)
                {
                    if (!projectIds.Contains(projectId))
                    {
                        violations.Add($"Chain '{chain.Id}' references missing project '{projectId}'.");
                        continue;
                    }
                    if (!chainMembers.TryAdd(projectId, chain.Id))
                        violations.Add(
                            $"Project '{projectId}' is claimed by chains '{chainMembers[projectId]}' and '{chain.Id}'; "
                            + "a chain grouping must partition the roster.");
                }
            }

            if (content.ClosureCriterion == null)
                return;

            var criterion = content.ClosureCriterion;
            if (!projectIds.Contains(criterion.MilestoneProjectId))
            {
                violations.Add(
                    $"Closure criterion names unknown milestone project '{criterion.MilestoneProjectId}'.");
                return;
            }

            if (content.Chains.Count == 0)
            {
                violations.Add("Closure criterion is authored but the region defines no restoration chains to measure it against.");
                return;
            }

            // A STRONG majority: fewer than that and the ending becomes purchasable by
            // skipping most of the valley, which is the cheap-ending failure the criterion
            // was authored to prevent.
            int majority = (content.Chains.Count / 2) + 1;
            if (criterion.MinimumChainsComplete < majority)
                violations.Add(
                    $"Closure criterion requires {criterion.MinimumChainsComplete} of {content.Chains.Count} chains, "
                    + $"which is below the {majority}-chain majority a closure route must require; "
                    + "the ending would be reachable by leaving most of the valley unrestored.");

            if (criterion.MinimumChainsComplete > content.Chains.Count)
                violations.Add(
                    $"Closure criterion requires {criterion.MinimumChainsComplete} chains but the region authors {content.Chains.Count}.");

            // The alternative route must be satisfiable from content the reachability pass
            // has ALREADY proven reachable, so a criterion can never be met only by content
            // the player cannot get to.
            if (criterion.MinimumChainsComplete < content.Chains.Count)
            {
                var reachable = ComputeReachable(content);
                int reachableChains = 0;
                foreach (var chain in content.Chains)
                {
                    bool allReachable = true;
                    foreach (var projectId in chain.ProjectIds)
                        if (!reachable.Contains(projectId))
                            allReachable = false;
                    if (allReachable)
                        reachableChains++;
                }

                if (reachableChains < criterion.MinimumChainsComplete)
                    violations.Add(
                        $"Closure criterion requires {criterion.MinimumChainsComplete} chains but only {reachableChains} "
                        + "are reachable from an entry project; a closure route a player cannot reach is a content defect.");
            }
        }

        public static List<string> Validate(RegionDefinition? content)
        {
            var violations = new List<string>();
            if (content == null)
            {
                violations.Add("Region definition is null.");
                return violations;
            }

            // ---- Pass 1: collect every ID set before resolving any reference. ----
            var projectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var project in content.Projects)
                projectIds.Add(project.Id.Value);

            var landmarkIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var landmark in content.Landmarks)
                landmarkIds.Add(landmark.Id.Value);

            var discoveryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var discovery in content.Discoveries)
                discoveryIds.Add(discovery.Id.Value);

            var expeditionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var expedition in content.Expeditions)
                expeditionIds.Add(expedition.Id.Value);

            // ---- Storyline chapters (D-076): ordered, resolvable, milestone-gated, glanceable. ----
            var chapterIds = new HashSet<string>(StringComparer.Ordinal);
            for (int c = 0; c < content.Chapters.Count; c++)
            {
                var chapter = content.Chapters[c];
                if (!chapterIds.Add(chapter.Id))
                    violations.Add($"Duplicate chapter ID '{chapter.Id}'.");
                if (chapter.Number != c)
                    violations.Add($"Chapter '{chapter.Id}' is numbered {chapter.Number}; chapters must be numbered contiguously from 0 in reading order.");
                if (chapter.BodyKey.Length > Story.StoryChapterDefinition.MaxBodyLength)
                    violations.Add($"Chapter '{chapter.Id}' body exceeds the glance budget ({Story.StoryChapterDefinition.MaxBodyLength} characters).");
                if (c > 0 && chapter.RequiredProjectIds.Count == 0 && chapter.MinCareCycles == 0)
                    violations.Add($"Chapter '{chapter.Id}' has no restoration milestone; only the prologue may be free.");
                if (c > 0 && string.IsNullOrWhiteSpace(chapter.HintKey))
                    violations.Add($"Chapter '{chapter.Id}' needs a hint for while it is locked.");
                foreach (var required in chapter.RequiredProjectIds)
                    if (!projectIds.Contains(required))
                        violations.Add($"Chapter '{chapter.Id}' requires missing project '{required}'.");
                if (chapter.MinCareCycles > 0 && content.PostCompletionPrograms.Count == 0)
                    violations.Add($"Chapter '{chapter.Id}' requires care cycles but the region authors no post-completion program.");
            }
            if (content.Chapters.Count > 0 && content.CompletionMilestoneProjectId != null)
            {
                bool closureChapter = false;
                foreach (var chapter in content.Chapters)
                    foreach (var required in chapter.RequiredProjectIds)
                        if (required == content.CompletionMilestoneProjectId) closureChapter = true;
                if (!closureChapter)
                    violations.Add("The storyline has no chapter for the region's closure milestone.");
            }

            // ---- Projects: identity, cost sanity, forward-safe references. ----
            var seenProjects = new HashSet<string>(StringComparer.Ordinal);
            long totalCost = 0L;
            foreach (var project in content.Projects)
            {
                if (!seenProjects.Add(project.Id.Value))
                    violations.Add($"Duplicate project ID '{project.Id}'.");
                if (project.VitalityCost <= 0L)
                    violations.Add($"Project '{project.Id}' has non-positive cost.");
                totalCost = checked(totalCost + project.VitalityCost);
                if (totalCost > MaxTotalVitalityCost)
                    violations.Add($"Combined project costs exceed the representable budget at '{project.Id}'.");
                foreach (var prerequisite in project.Prerequisites)
                    if (!projectIds.Contains(prerequisite.Value))
                        violations.Add($"Project '{project.Id}' references missing prerequisite '{prerequisite}'.");
            }

            // ---- Landmarks: unique stages, ascending monotonic triggers. ----
            var seenLandmarks = new HashSet<string>(StringComparer.Ordinal);
            foreach (var landmark in content.Landmarks)
            {
                if (!seenLandmarks.Add(landmark.Id.Value))
                    violations.Add($"Duplicate landmark ID '{landmark.Id}'.");

                RestorationStage previous = RestorationStage.Ruined;
                foreach (var stage in landmark.Stages)
                {
                    if (!projectIds.Contains(stage.UnlockedByProjectId))
                        violations.Add($"Landmark '{landmark.Id}' stage {stage.Stage} references missing project '{stage.UnlockedByProjectId}'.");
                    if (stage.Stage <= previous && previous != RestorationStage.Ruined)
                        violations.Add($"Landmark '{landmark.Id}' stages are not strictly ascending.");
                    else if (stage.Stage == RestorationStage.Ruined)
                        violations.Add($"Landmark '{landmark.Id}' defines a Ruined stage; Ruined is the implicit initial state.");
                    previous = stage.Stage;
                }
            }

            // ---- Producers: unique unlockers, representable rate/capacity. ----
            var producerUnlockProjects = new HashSet<string>(StringComparer.Ordinal);
            var seenProducers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var producer in content.Producers)
            {
                if (!seenProducers.Add(producer.Id.Value))
                    violations.Add($"Duplicate producer ID '{producer.Id}'.");
                if (!projectIds.Contains(producer.UnlockedByProjectId))
                    violations.Add($"Producer '{producer.Id}' references missing unlock project '{producer.UnlockedByProjectId}'.");
                if (producer.MilliUnitsPerDay > MaxMilliUnitsPerDay)
                    violations.Add($"Producer '{producer.Id}' rate {producer.MilliUnitsPerDay} exceeds the maximum representable rate {MaxMilliUnitsPerDay} (the production tick would overflow).");
                if (producer.CapacityUnits > long.MaxValue / ProducerDefinition.MilliUnitsPerUnit)
                    violations.Add($"Producer '{producer.Id}' capacity is too large to represent safely.");
                if (producer.CapacityUnits <= 0L)
                    violations.Add($"Producer '{producer.Id}' capacity must be positive.");
                producerUnlockProjects.Add(producer.UnlockedByProjectId);
            }

            // ---- Discoveries: key integrity and trigger resolution. ----
            var seenDiscoveries = new HashSet<string>(StringComparer.Ordinal);
            foreach (var discovery in content.Discoveries)
            {
                if (!seenDiscoveries.Add(discovery.Id.Value))
                    violations.Add($"Duplicate discovery ID '{discovery.Id}'.");
                if (!projectIds.Contains(discovery.UnlockedByProjectId))
                    violations.Add($"Discovery '{discovery.Id}' references missing unlock project '{discovery.UnlockedByProjectId}'.");
            }

            // ---- Expeditions: requirement/reward integrity. ----
            var seenExpeditions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var expedition in content.Expeditions)
            {
                if (!seenExpeditions.Add(expedition.Id.Value))
                    violations.Add($"Duplicate expedition ID '{expedition.Id}'.");
                foreach (var projectId in expedition.RequiredProjectIds)
                    if (!projectIds.Contains(projectId))
                        violations.Add($"Expedition '{expedition.Id}' requires missing project '{projectId}'.");
                foreach (var requirement in expedition.RequiredStages)
                {
                    var landmark = content.FindLandmark(requirement.LandmarkId);
                    if (landmark == null)
                    {
                        violations.Add($"Expedition '{expedition.Id}' requires unknown landmark '{requirement.LandmarkId}'.");
                        continue;
                    }
                    bool stageDefined = false;
                    foreach (var stage in landmark.Stages)
                        if (stage.Stage == requirement.Stage)
                            stageDefined = true;
                    if (requirement.Stage != RestorationStage.Ruined && !stageDefined)
                        violations.Add($"Expedition '{expedition.Id}' requires landmark '{requirement.LandmarkId}' at stage {requirement.Stage}, which its content never reaches.");
                }
                if (expedition.Reward != null && expedition.Reward.Units > MaxTotalVitalityCost)
                    violations.Add($"Expedition '{expedition.Id}' reward is unrepresentably large.");
            }

            // ---- Progression arcs: strictly ascending, resolvable, bounded. ----
            ValidateArc(content.EcologyProgression, projectIds, "ecology", MaxStagesPerArc, violations);
            ValidateArc(content.SettlementProgression, projectIds, "settlement", MaxStagesPerArc, violations);

            // ---- Resource caps and secondary requirements (D-054). ----
            ValidateResourceCaps(content, violations);
            ValidateSecondaryRequirements(content, violations);

            // ---- Retired content identifiers (D-059). ----
            ValidateTombstones(content, projectIds, violations);

            // ---- Post-completion programs (D-061). ----
            ValidatePostCompletionPrograms(content, violations);

            // ---- Restoration chains and the closure criterion (D-062). ----
            ValidateChainsAndClosureCriterion(content, projectIds, violations);

            // ---- Self-check: the published bounds still match the simulation (D-058). ----
            violations.AddRange(CheckNumericBoundsAgainstSimulation(MaxMilliUnitsPerDay));

            // ---- Reachability: entry path, no hidden deadlock, closure reachable. ----
            bool hasReachableEntry = false;
            foreach (var project in content.Projects)
            {
                if (project.Prerequisites.Count != 0)
                    continue;
                hasReachableEntry = true;
                break;
            }
            if (!hasReachableEntry)
                violations.Add("Region has no entry project (all projects have prerequisites).");

            var cycleRoot = FindAnyCycle(content);
            if (cycleRoot != null)
                violations.Add($"Project prerequisite chain contains a cycle at '{cycleRoot}'.");

            if (hasReachableEntry)
            {
                var reachable = ComputeReachable(content);
                if (content.CompletionMilestoneProjectId != null &&
                    !reachable.Contains(content.CompletionMilestoneProjectId))
                {
                    violations.Add($"Completion milestone '{content.CompletionMilestoneProjectId}' is not reachable from an entry project.");
                }
                if (content.CompletionMilestoneProjectId != null &&
                    !projectIds.Contains(content.CompletionMilestoneProjectId))
                {
                    violations.Add($"Completion milestone '{content.CompletionMilestoneProjectId}' is not a defined project.");
                }

                foreach (var project in content.Projects)
                    if (!reachable.Contains(project.Id.Value))
                        violations.Add($"Project '{project.Id}' is unreachable from any entry project (hidden deadlock).");
                foreach (var landmark in content.Landmarks)
                    foreach (var stage in landmark.Stages)
                        if (!reachable.Contains(stage.UnlockedByProjectId))
                            violations.Add($"Landmark '{landmark.Id}' depends on unreachable project '{stage.UnlockedByProjectId}'.");
                foreach (var producerId in producerUnlockProjects)
                    if (!reachable.Contains(producerId))
                        violations.Add($"Producer unlock depends on unreachable project '{producerId}'.");
                foreach (var discovery in content.Discoveries)
                    if (!reachable.Contains(discovery.UnlockedByProjectId))
                        violations.Add($"Discovery '{discovery.Id}' depends on unreachable project '{discovery.UnlockedByProjectId}'.");
                foreach (var expedition in content.Expeditions)
                    foreach (var projectId in expedition.RequiredProjectIds)
                        if (!reachable.Contains(projectId))
                            violations.Add($"Expedition '{expedition.Id}' depends on unreachable project '{projectId}'.");
            }

            return violations;
        }

        private static void ValidateArc(
            RegionProgressionDefinition arc,
            HashSet<string> projectIds,
            string axisName,
            int maxStages,
            List<string> violations)
        {
            int previousStage = 0;
            foreach (var stage in arc.Stages)
            {
                if (stage.Stage <= previousStage)
                    violations.Add($"{axisName} progression stages are not strictly ascending at stage {stage.Stage}.");
                if (!projectIds.Contains(stage.UnlockedByProjectId))
                    violations.Add($"{axisName} progression stage {stage.Stage} references missing project '{stage.UnlockedByProjectId}'.");
                previousStage = stage.Stage;
            }
            if (arc.Stages.Count > maxStages)
                violations.Add($"{axisName} progression arc exceeds the stage bound ({arc.Stages.Count}).");
        }

        /// <summary>
        /// Fixpoint reachability over AND-prerequisites: a project becomes reachable when
        /// every one of its prerequisites is reachable (entry projects have none).
        /// </summary>
        private static HashSet<string> ComputeReachable(RegionDefinition content)
        {
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var project in content.Projects)
                {
                    if (reachable.Contains(project.Id.Value))
                        continue;
                    bool satisfied = true;
                    foreach (var prerequisite in project.Prerequisites)
                        if (!reachable.Contains(prerequisite.Value))
                            satisfied = false;
                    if (!satisfied)
                        continue;
                    reachable.Add(project.Id.Value);
                    changed = true;
                }
            }
            return reachable;
        }

        private static string? FindAnyCycle(Regions.RegionDefinition content)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var stack = new HashSet<string>(StringComparer.Ordinal);

            bool Visit(string projectId)
            {
                if (visited.Contains(projectId))
                    return false;
                if (!stack.Add(projectId))
                    return true;

                var definition = content.FindProject(projectId);
                if (definition != null)
                {
                    foreach (var prerequisite in definition.Prerequisites)
                        if (Visit(prerequisite.Value))
                            return true;
                }

                stack.Remove(projectId);
                visited.Add(projectId);
                return false;
            }

            foreach (var project in content.Projects)
                if (Visit(project.Id.Value))
                    return project.Id.Value;
            return null;
        }
    }
}
