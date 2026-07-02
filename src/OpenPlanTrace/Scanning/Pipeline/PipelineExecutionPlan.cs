namespace OpenPlanTrace;

public sealed record PipelineExecutionPlan(
    string ExecutionModel,
    IReadOnlyList<PlanArtifactKind> SourceArtifacts,
    IReadOnlyList<PipelineExecutionWave> ExecutionWaves,
    IReadOnlyList<PipelineStagePlan> Stages,
    IReadOnlyList<PipelinePlanIssue> Issues)
{
    public IReadOnlyList<PipelineRerunImpact> RerunImpacts { get; init; } =
        Array.Empty<PipelineRerunImpact>();

    public IReadOnlyList<PipelineRerunPlan> RerunPlans { get; init; } =
        Array.Empty<PipelineRerunPlan>();

    public IReadOnlyList<PipelineArtifactPlan> ArtifactPlans { get; init; } =
        Array.Empty<PipelineArtifactPlan>();

    private static readonly IReadOnlyList<PlanArtifactKind> DefaultSourceArtifacts =
        new[]
        {
            PlanArtifactKind.Document,
            PlanArtifactKind.Pages,
            PlanArtifactKind.Primitives,
            PlanArtifactKind.RasterImages,
            PlanArtifactKind.PdfImages,
            PlanArtifactKind.Diagnostics
        };

    public static PipelineExecutionPlan Empty { get; } =
        new(
            "none",
            Array.Empty<PlanArtifactKind>(),
            Array.Empty<PipelineExecutionWave>(),
            Array.Empty<PipelineStagePlan>(),
            Array.Empty<PipelinePlanIssue>());

    public bool HasErrors => Issues.Any(issue => issue.Severity == DiagnosticSeverity.Error);

    public bool HasWarnings => Issues.Any(issue => issue.Severity == DiagnosticSeverity.Warning);

    public bool IsDependencyReady => !HasErrors;

    public static PipelineExecutionPlan FromStages(
        IEnumerable<PipelineStageMetadata> stages,
        string executionModel = "fixed-stage-chain",
        IEnumerable<PlanArtifactKind>? sourceArtifacts = null)
    {
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionModel);

        var source = Normalize(sourceArtifacts ?? DefaultSourceArtifacts);
        var available = new HashSet<PlanArtifactKind>(source);
        var hardArtifactLevels = source.ToDictionary(artifact => artifact, _ => 0);
        var preferredArtifactLevels = source.ToDictionary(artifact => artifact, _ => 0);
        var stagePlans = new List<PipelineStagePlan>();
        var issues = new List<PipelinePlanIssue>();
        var seenStages = new HashSet<string>(StringComparer.Ordinal);

        var order = 0;
        foreach (var metadata in stages)
        {
            order++;
            if (!seenStages.Add(metadata.Stage))
            {
                issues.Add(new PipelinePlanIssue(
                    "pipeline.stage.duplicate",
                    DiagnosticSeverity.Warning,
                    metadata.Stage,
                    $"Pipeline stage '{metadata.Stage}' appears more than once in the execution plan.",
                    Array.Empty<PlanArtifactKind>()));
            }

            var requiredReads = Normalize(metadata.Reads);
            var optionalReads = Normalize(metadata.OptionalReads);
            var writes = Normalize(metadata.Writes);
            var availableBefore = available.OrderBy(artifact => artifact.ToString(), StringComparer.Ordinal).ToArray();
            var satisfiedReads = requiredReads.Where(available.Contains).ToArray();
            var missingRequiredReads = requiredReads.Where(artifact => !available.Contains(artifact)).ToArray();
            var satisfiedOptionalReads = optionalReads.Where(available.Contains).ToArray();
            var missingOptionalReads = optionalReads.Where(artifact => !available.Contains(artifact)).ToArray();
            var dependencyLevel = ComputeDependencyLevel(requiredReads, hardArtifactLevels);
            var preferredDependencyLevel = ComputeDependencyLevel(
                requiredReads.Concat(satisfiedOptionalReads),
                preferredArtifactLevels);

            if (missingRequiredReads.Length > 0)
            {
                issues.Add(new PipelinePlanIssue(
                    "pipeline.stage.required_artifacts_missing",
                    DiagnosticSeverity.Error,
                    metadata.Stage,
                    $"Pipeline stage '{metadata.Stage}' requires artifacts that are not available before it runs: {string.Join(", ", missingRequiredReads)}.",
                    missingRequiredReads));
            }

            if (writes.Count == 0)
            {
                issues.Add(new PipelinePlanIssue(
                    "pipeline.stage.no_declared_writes",
                    DiagnosticSeverity.Warning,
                    metadata.Stage,
                    $"Pipeline stage '{metadata.Stage}' does not declare any output artifacts.",
                    Array.Empty<PlanArtifactKind>()));
            }

            stagePlans.Add(new PipelineStagePlan(
                order,
                metadata.Stage,
                metadata.Kind,
                dependencyLevel,
                false,
                dependencyLevel,
                preferredDependencyLevel,
                requiredReads,
                optionalReads,
                writes,
                availableBefore,
                satisfiedReads,
                missingRequiredReads,
                satisfiedOptionalReads,
                missingOptionalReads));

            foreach (var artifact in writes)
            {
                available.Add(artifact);
                SetArtifactLevel(hardArtifactLevels, artifact, dependencyLevel);
                SetArtifactLevel(preferredArtifactLevels, artifact, preferredDependencyLevel);
            }
        }

        var executionWaves = BuildExecutionWaves(stagePlans);
        var parallelStageNames = executionWaves
            .Where(wave => wave.IsParallelCandidate)
            .SelectMany(wave => wave.Stages)
            .ToHashSet(StringComparer.Ordinal);
        stagePlans = stagePlans
            .Select(stage => stage with { IsParallelCandidate = parallelStageNames.Contains(stage.Stage) })
            .ToList();

        var rerunImpacts = BuildRerunImpacts(stagePlans, source);
        var artifactPlans = BuildArtifactPlans(stagePlans, source, rerunImpacts);
        issues.AddRange(BuildArtifactPlanIssues(artifactPlans));

        return new PipelineExecutionPlan(executionModel, source, executionWaves, stagePlans, issues)
        {
            ArtifactPlans = artifactPlans,
            RerunImpacts = rerunImpacts,
            RerunPlans = BuildStandardRerunPlans(stagePlans, source, executionWaves, rerunImpacts)
        };
    }

    public PipelineRerunPlan CreateRerunPlan(
        string planId,
        string displayName,
        IEnumerable<PlanArtifactKind> changedArtifacts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(changedArtifacts);

        return BuildRerunPlan(
            planId,
            displayName,
            Normalize(changedArtifacts),
            SourceArtifacts.ToHashSet(),
            Stages,
            ExecutionWaves,
            RerunImpacts);
    }

    private static IReadOnlyList<PipelineRerunPlan> BuildStandardRerunPlans(
        IReadOnlyList<PipelineStagePlan> stages,
        IReadOnlyList<PlanArtifactKind> sourceArtifacts,
        IReadOnlyList<PipelineExecutionWave> executionWaves,
        IReadOnlyList<PipelineRerunImpact> rerunImpacts)
    {
        var sourceSet = sourceArtifacts.ToHashSet();
        return new[]
            {
                BuildRerunPlan(
                    "source-primitives",
                    "Source primitive correction",
                    new[] { PlanArtifactKind.Primitives },
                    sourceSet,
                    stages,
                    executionWaves,
                    rerunImpacts),
                BuildRerunPlan(
                    "wall-geometry",
                    "Wall geometry correction",
                    new[] { PlanArtifactKind.Walls },
                    sourceSet,
                    stages,
                    executionWaves,
                    rerunImpacts),
                BuildRerunPlan(
                    "wall-topology",
                    "Wall graph topology correction",
                    new[] { PlanArtifactKind.WallGraph, PlanArtifactKind.TopologySpans },
                    sourceSet,
                    stages,
                    executionWaves,
                    rerunImpacts),
                BuildRerunPlan(
                    "openings",
                    "Opening correction",
                    new[] { PlanArtifactKind.Openings },
                    sourceSet,
                    stages,
                    executionWaves,
                    rerunImpacts),
                BuildRerunPlan(
                    "rooms",
                    "Room correction",
                    new[] { PlanArtifactKind.Rooms, PlanArtifactKind.RoomAdjacency },
                    sourceSet,
                    stages,
                    executionWaves,
                    rerunImpacts),
                BuildRerunPlan(
                    "objects",
                    "Object semantics correction",
                    new[] { PlanArtifactKind.ObjectCandidates, PlanArtifactKind.ObjectGroups, PlanArtifactKind.ObjectAggregates },
                    sourceSet,
                    stages,
                    executionWaves,
                    rerunImpacts),
                BuildRerunPlan(
                    "calibration",
                    "Calibration or scale correction",
                    new[] { PlanArtifactKind.Calibration },
                    sourceSet,
                    stages,
                    executionWaves,
                    rerunImpacts),
                BuildRerunPlan(
                    "routing",
                    "Routing artifact correction",
                    new[]
                    {
                        PlanArtifactKind.RoutingBarriers,
                        PlanArtifactKind.RoutingPassages,
                        PlanArtifactKind.RoutingObstacles,
                        PlanArtifactKind.RoutingRoomUseHints,
                        PlanArtifactKind.RoutingSuppressedObjects,
                        PlanArtifactKind.RoutingIgnoredObjects
                    },
                    sourceSet,
                    stages,
                    executionWaves,
                    rerunImpacts)
            }
            .ToArray();
    }

    private static PipelineRerunPlan BuildRerunPlan(
        string planId,
        string displayName,
        IReadOnlyList<PlanArtifactKind> changedArtifacts,
        IReadOnlySet<PlanArtifactKind> sourceArtifacts,
        IReadOnlyList<PipelineStagePlan> stages,
        IReadOnlyList<PipelineExecutionWave> executionWaves,
        IReadOnlyList<PipelineRerunImpact> rerunImpacts)
    {
        var changed = Normalize(changedArtifacts);
        var impactsByArtifact = rerunImpacts.ToDictionary(impact => impact.Artifact);
        var selectedImpacts = changed
            .Where(impactsByArtifact.ContainsKey)
            .Select(artifact => impactsByArtifact[artifact])
            .ToArray();
        var rerunStageSet = selectedImpacts
            .SelectMany(impact => impact.AffectedStages)
            .ToHashSet(StringComparer.Ordinal);
        var rerunStages = stages
            .Where(stage => rerunStageSet.Contains(stage.Stage))
            .OrderBy(stage => stage.Order)
            .ToArray();
        var rerunStageNames = rerunStages.Select(stage => stage.Stage).ToArray();
        var rerunWaves = rerunStages
            .Select(stage => stage.ExecutionWave)
            .Distinct()
            .Order()
            .ToArray();
        var affectedArtifacts = Normalize(selectedImpacts.SelectMany(impact => impact.AffectedArtifacts));
        var directConsumers = selectedImpacts
            .SelectMany(impact => impact.DirectConsumerStages)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(stage => stages.FirstOrDefault(candidate => candidate.Stage == stage)?.Order ?? int.MaxValue)
            .ThenBy(stage => stage, StringComparer.Ordinal)
            .ToArray();
        var changedSourceArtifacts = Normalize(changed.Where(sourceArtifacts.Contains));
        var waveSet = rerunWaves.ToHashSet();
        var hasParallelCandidate = executionWaves
            .Where(wave => waveSet.Contains(wave.Level) && wave.IsParallelCandidate)
            .Any(wave => wave.Stages.Count(rerunStageSet.Contains) > 1);
        var recommendedExecutionMode = rerunStageNames.Length == 0
            ? "None"
            : hasParallelCandidate
                ? "WaveOrderedWithParallelCandidates"
                : "WaveOrderedSequential";
        var evidence = new List<string>
        {
            changed.Count == 0 ? "changed artifacts: none" : $"changed artifacts: {string.Join(", ", changed)}",
            changedSourceArtifacts.Count == 0 ? "changed source artifacts: none" : $"changed source artifacts: {string.Join(", ", changedSourceArtifacts)}",
            directConsumers.Length == 0 ? "direct consumers: none" : $"direct consumers: {string.Join(", ", directConsumers.Take(8))}",
            rerunStageNames.Length == 0 ? "rerun stages: none" : $"rerun stages: {rerunStageNames.Length}",
            affectedArtifacts.Count == 0 ? "affected artifacts: none" : $"affected artifacts: {string.Join(", ", affectedArtifacts.Take(12))}"
        };

        return new PipelineRerunPlan(
            planId,
            displayName,
            changed,
            changedSourceArtifacts,
            directConsumers,
            rerunStageNames,
            rerunWaves,
            affectedArtifacts,
            rerunWaves.Length == 0 ? 0 : rerunWaves.First(),
            rerunWaves.Length == 0 ? 0 : rerunWaves.Last(),
            rerunStageNames.Length,
            affectedArtifacts.Count,
            recommendedExecutionMode,
            evidence);
    }

    private static IReadOnlyList<PipelineRerunImpact> BuildRerunImpacts(
        IReadOnlyList<PipelineStagePlan> stages,
        IReadOnlyList<PlanArtifactKind> sourceArtifacts)
    {
        var sourceSet = sourceArtifacts.ToHashSet();
        var artifacts = Normalize(sourceArtifacts
            .Concat(stages.SelectMany(stage => stage.Reads))
            .Concat(stages.SelectMany(stage => stage.OptionalReads))
            .Concat(stages.SelectMany(stage => stage.Writes)))
            .Where(artifact => artifact != PlanArtifactKind.Diagnostics)
            .ToArray();

        return artifacts
            .Select(artifact => BuildRerunImpact(artifact, sourceSet.Contains(artifact), stages))
            .OrderBy(impact => impact.Artifact.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<PipelineArtifactPlan> BuildArtifactPlans(
        IReadOnlyList<PipelineStagePlan> stages,
        IReadOnlyList<PlanArtifactKind> sourceArtifacts,
        IReadOnlyList<PipelineRerunImpact> rerunImpacts)
    {
        var sourceSet = sourceArtifacts.ToHashSet();
        var impactsByArtifact = rerunImpacts.ToDictionary(impact => impact.Artifact);
        var artifacts = Normalize(sourceArtifacts
            .Concat(stages.SelectMany(stage => stage.Reads))
            .Concat(stages.SelectMany(stage => stage.OptionalReads))
            .Concat(stages.SelectMany(stage => stage.Writes)));

        return artifacts
            .Select(artifact => BuildArtifactPlan(artifact, sourceSet.Contains(artifact), stages, impactsByArtifact))
            .OrderBy(plan => plan.Artifact.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    private static PipelineArtifactPlan BuildArtifactPlan(
        PlanArtifactKind artifact,
        bool isSourceArtifact,
        IReadOnlyList<PipelineStagePlan> stages,
        IReadOnlyDictionary<PlanArtifactKind, PipelineRerunImpact> impactsByArtifact)
    {
        var producers = stages
            .Where(stage => stage.Writes.Contains(artifact))
            .OrderBy(stage => stage.Order)
            .ToArray();
        var primaryProducers = producers
            .Where(stage => !IsArtifactRefinementStage(stage, artifact))
            .ToArray();
        var producerConflictCount = primaryProducers.Length == 0
            ? producers.Length
            : primaryProducers.Length;
        var requiredConsumers = stages
            .Where(stage => stage.Reads.Contains(artifact))
            .OrderBy(stage => stage.Order)
            .ToArray();
        var optionalConsumers = stages
            .Where(stage => stage.OptionalReads.Contains(artifact))
            .OrderBy(stage => stage.Order)
            .ToArray();
        var consumers = requiredConsumers
            .Concat(optionalConsumers)
            .DistinctBy(stage => stage.Stage)
            .OrderBy(stage => stage.Order)
            .ToArray();
        var evidence = new List<string>
        {
            isSourceArtifact ? "artifact is available from source ingestion" : "artifact depends on scanner stage output",
            producers.Length == 0 ? "producer stages: none" : $"producer stages: {string.Join(", ", producers.Select(stage => stage.Stage))}",
            requiredConsumers.Length == 0 ? "required consumers: none" : $"required consumers: {string.Join(", ", requiredConsumers.Select(stage => stage.Stage))}",
            optionalConsumers.Length == 0 ? "optional consumers: none" : $"optional consumers: {string.Join(", ", optionalConsumers.Select(stage => stage.Stage))}"
        };

        if (impactsByArtifact.TryGetValue(artifact, out var impact))
        {
            evidence.Add(impact.HasImpact
                ? $"correction impact affects {impact.AffectedStageCount} stage(s)"
                : "correction impact affects no later stages");
        }

        if (!isSourceArtifact && producers.Length == 0)
        {
            evidence.Add("artifact has no planned producer; it can only be satisfied by optional external data or a future plugin.");
        }
        else if (producers.Length > 1)
        {
            evidence.Add(producerConflictCount > 1
                ? "artifact has multiple planned producers and should be reviewed for write ownership."
                : "artifact has one primary producer plus refinement stage(s).");
        }

        if (consumers.Length == 0)
        {
            evidence.Add("artifact is terminal in the current pipeline graph.");
        }

        return new PipelineArtifactPlan(
            artifact,
            isSourceArtifact,
            producers.Length > 0,
            consumers.Length > 0,
            consumers.Length == 0,
            producers.Select(stage => stage.Stage).ToArray(),
            requiredConsumers.Select(stage => stage.Stage).ToArray(),
            optionalConsumers.Select(stage => stage.Stage).ToArray(),
            consumers.Select(stage => stage.Stage).ToArray(),
            producers.Length,
            consumers.Length,
            producers.Length == 0 ? 0 : producers.First().Order,
            producers.Length == 0 ? 0 : producers.Last().Order,
            consumers.Length == 0 ? 0 : consumers.First().Order,
            consumers.Length == 0 ? 0 : consumers.Last().Order,
            producers.Length == 0 ? 0 : producers.First().ExecutionWave,
            producers.Length == 0 ? 0 : producers.Last().ExecutionWave,
            consumers.Length == 0 ? 0 : consumers.First().ExecutionWave,
            consumers.Length == 0 ? 0 : consumers.Last().ExecutionWave,
            producerConflictCount > 1,
            requiredConsumers.Length > 0,
            DependencyRoleFor(isSourceArtifact, producers.Length, consumers.Length),
            evidence);
    }

    private static bool IsArtifactRefinementStage(PipelineStagePlan stage, PlanArtifactKind artifact) =>
        stage.Writes.Contains(artifact)
        && stage.Order > 0
        && (stage.Reads.Contains(artifact) || stage.OptionalReads.Contains(artifact));

    private static string DependencyRoleFor(bool isSourceArtifact, int producerCount, int consumerCount)
    {
        if (isSourceArtifact && consumerCount > 0)
        {
            return "SourceInput";
        }

        if (isSourceArtifact)
        {
            return "SourceTerminal";
        }

        if (producerCount == 0)
        {
            return "UnproducedRead";
        }

        return consumerCount == 0 ? "ProducedTerminal" : "ProducedAndConsumed";
    }

    private static IReadOnlyList<PipelinePlanIssue> BuildArtifactPlanIssues(
        IReadOnlyList<PipelineArtifactPlan> artifactPlans)
    {
        var issues = new List<PipelinePlanIssue>();
        foreach (var plan in artifactPlans.Where(plan => plan.Artifact != PlanArtifactKind.Diagnostics))
        {
            if (!plan.IsSourceArtifact && !plan.IsProducedByStage && plan.RequiredConsumerStages.Count > 0)
            {
                issues.Add(new PipelinePlanIssue(
                    "pipeline.artifact.producer_missing",
                    DiagnosticSeverity.Error,
                    FirstStage(plan.RequiredConsumerStages),
                    $"Required artifact '{plan.Artifact}' has consumer stage(s) but no source or planned producer stage.",
                    new[] { plan.Artifact }));
            }

            if (!plan.IsSourceArtifact
                && plan.FirstProducerOrder > 0
                && plan.FirstConsumerOrder > 0
                && plan.FirstConsumerOrder < plan.FirstProducerOrder
                && plan.RequiredConsumerStages.Count > 0)
            {
                issues.Add(new PipelinePlanIssue(
                    "pipeline.artifact.producer_after_required_consumer",
                    DiagnosticSeverity.Error,
                    FirstStage(plan.RequiredConsumerStages),
                    $"Required artifact '{plan.Artifact}' is consumed before its first planned producer runs.",
                    new[] { plan.Artifact }));
            }

            if (!plan.IsSourceArtifact && plan.HasMultipleProducers)
            {
                issues.Add(new PipelinePlanIssue(
                    "pipeline.artifact.multiple_producers",
                    DiagnosticSeverity.Warning,
                    FirstStage(plan.ProducerStages),
                    $"Artifact '{plan.Artifact}' has multiple planned producer stages: {string.Join(", ", plan.ProducerStages)}.",
                    new[] { plan.Artifact }));
            }
        }

        return issues
            .OrderByDescending(issue => issue.Severity)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.Stage, StringComparer.Ordinal)
            .ThenBy(issue => issue.Artifacts.FirstOrDefault().ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    private static string FirstStage(IReadOnlyList<string> stages) =>
        stages.Count == 0 ? string.Empty : stages[0];

    private static PipelineRerunImpact BuildRerunImpact(
        PlanArtifactKind artifact,
        bool isSourceArtifact,
        IReadOnlyList<PipelineStagePlan> stages)
    {
        var producerStages = stages
            .Where(stage => stage.Writes.Contains(artifact))
            .OrderBy(stage => stage.Order)
            .ToArray();
        var producerNames = producerStages.Select(stage => stage.Stage).ToArray();
        var firstConsumerOrder = isSourceArtifact || producerStages.Length == 0
            ? 0
            : producerStages.Min(stage => stage.Order);
        var directConsumers = stages
            .Where(stage => stage.Order > firstConsumerOrder)
            .Where(stage => stage.Reads.Contains(artifact) || stage.OptionalReads.Contains(artifact))
            .OrderBy(stage => stage.Order)
            .ToArray();
        var affectedStages = BuildAffectedStages(artifact, directConsumers, stages);
        var affectedArtifacts = Normalize(affectedStages.SelectMany(stage => stage.Writes))
            .Where(item => item != PlanArtifactKind.Diagnostics)
            .ToArray();
        var affectedStageNames = affectedStages.Select(stage => stage.Stage).ToArray();
        var evidence = new List<string>
        {
            isSourceArtifact ? "artifact is provided by source ingestion" : "artifact is produced by scanner stages",
            producerNames.Length == 0 ? "producer stages: none" : $"producer stages: {string.Join(", ", producerNames)}",
            directConsumers.Length == 0 ? "direct consumers: none" : $"direct consumers: {string.Join(", ", directConsumers.Select(stage => stage.Stage))}",
            affectedStageNames.Length == 0 ? "transitive affected stages: none" : $"transitive affected stages: {affectedStageNames.Length}"
        };

        if (affectedArtifacts.Length > 0)
        {
            evidence.Add($"affected artifacts: {string.Join(", ", affectedArtifacts)}");
        }

        var impactScope = affectedStageNames.Length == 0
            ? "None"
            : isSourceArtifact
                ? "SourceArtifact"
                : "DerivedArtifact";

        return new PipelineRerunImpact(
            artifact,
            isSourceArtifact,
            impactScope,
            producerNames,
            directConsumers.Select(stage => stage.Stage).ToArray(),
            affectedStageNames,
            affectedArtifacts,
            affectedStages.Count == 0 ? 0 : affectedStages.Min(stage => stage.ExecutionWave),
            affectedStageNames.Length,
            evidence);
    }

    private static IReadOnlyList<PipelineStagePlan> BuildAffectedStages(
        PlanArtifactKind changedArtifact,
        IReadOnlyList<PipelineStagePlan> directConsumers,
        IReadOnlyList<PipelineStagePlan> stages)
    {
        var affected = new HashSet<string>(StringComparer.Ordinal);
        var changedArtifacts = new HashSet<PlanArtifactKind> { changedArtifact };
        var queue = new Queue<PipelineStagePlan>(directConsumers);
        var orderedAffected = new List<PipelineStagePlan>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!affected.Add(current.Stage))
            {
                continue;
            }

            orderedAffected.Add(current);
            foreach (var write in current.Writes.Where(write => write is not PlanArtifactKind.Unknown and not PlanArtifactKind.Diagnostics))
            {
                changedArtifacts.Add(write);
            }

            foreach (var candidate in stages.Where(stage => stage.Order > current.Order && !affected.Contains(stage.Stage)))
            {
                if (candidate.Reads.Concat(candidate.OptionalReads).Any(changedArtifacts.Contains))
                {
                    queue.Enqueue(candidate);
                }
            }
        }

        return orderedAffected
            .OrderBy(stage => stage.Order)
            .ToArray();
    }

    private static IReadOnlyList<PipelineExecutionWave> BuildExecutionWaves(IReadOnlyList<PipelineStagePlan> stages) =>
        stages
            .GroupBy(stage => stage.ExecutionWave)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var stageGroup = group.OrderBy(stage => stage.Order).ToArray();
                var writeConflictArtifacts = stageGroup
                    .SelectMany(stage => stage.Writes)
                    .Where(artifact => artifact != PlanArtifactKind.Diagnostics)
                    .GroupBy(artifact => artifact)
                    .Where(artifactGroup => artifactGroup.Count() > 1)
                    .Select(artifactGroup => artifactGroup.Key)
                    .OrderBy(artifact => artifact.ToString(), StringComparer.Ordinal)
                    .ToArray();
                var intraWaveDependencies = BuildIntraWaveDependencies(stageGroup);
                var isParallelCandidate =
                    stageGroup.Length > 1
                    && writeConflictArtifacts.Length == 0
                    && intraWaveDependencies.Count == 0;
                var directDownstreamStages = BuildDirectDownstreamStages(group.Key, stages, stageGroup.SelectMany(stage => stage.Writes));
                var downstreamReadArtifacts = BuildDownstreamReadArtifacts(group.Key, stages, stageGroup.SelectMany(stage => stage.Writes));

                return new PipelineExecutionWave(
                    group.Key,
                    stageGroup.Select(stage => stage.Stage).ToArray(),
                    Normalize(stageGroup.SelectMany(stage => stage.Reads.Concat(stage.OptionalReads))),
                    Normalize(stageGroup.SelectMany(stage => stage.Writes)),
                    writeConflictArtifacts,
                    intraWaveDependencies,
                    isParallelCandidate,
                    directDownstreamStages,
                    downstreamReadArtifacts);
            })
            .ToArray();

    private static IReadOnlyList<string> BuildDirectDownstreamStages(
        int waveLevel,
        IReadOnlyList<PipelineStagePlan> stages,
        IEnumerable<PlanArtifactKind> writes)
    {
        var writeSet = writes
            .Where(artifact => artifact is not PlanArtifactKind.Unknown and not PlanArtifactKind.Diagnostics)
            .ToHashSet();
        if (writeSet.Count == 0)
        {
            return Array.Empty<string>();
        }

        return stages
            .Where(stage => stage.ExecutionWave > waveLevel)
            .Where(stage => stage.Reads.Concat(stage.OptionalReads).Any(writeSet.Contains))
            .OrderBy(stage => stage.Order)
            .Select(stage => stage.Stage)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<PlanArtifactKind> BuildDownstreamReadArtifacts(
        int waveLevel,
        IReadOnlyList<PipelineStagePlan> stages,
        IEnumerable<PlanArtifactKind> writes)
    {
        var writeSet = writes
            .Where(artifact => artifact is not PlanArtifactKind.Unknown and not PlanArtifactKind.Diagnostics)
            .ToHashSet();
        if (writeSet.Count == 0)
        {
            return Array.Empty<PlanArtifactKind>();
        }

        return Normalize(stages
            .Where(stage => stage.ExecutionWave > waveLevel)
            .SelectMany(stage => stage.Reads.Concat(stage.OptionalReads))
            .Where(writeSet.Contains));
    }

    private static IReadOnlyList<PipelineWaveDependency> BuildIntraWaveDependencies(IReadOnlyList<PipelineStagePlan> stages)
    {
        var dependencies = new List<PipelineWaveDependency>();
        foreach (var stage in stages)
        {
            var reads = stage.Reads
                .Concat(stage.OptionalReads)
                .Where(artifact => artifact != PlanArtifactKind.Diagnostics)
                .Distinct()
                .ToArray();
            if (reads.Length == 0)
            {
                continue;
            }

            foreach (var candidate in stages.Where(candidate => !string.Equals(candidate.Stage, stage.Stage, StringComparison.Ordinal)))
            {
                var shared = reads
                    .Intersect(candidate.Writes.Where(artifact => artifact != PlanArtifactKind.Diagnostics))
                    .OrderBy(artifact => artifact.ToString(), StringComparer.Ordinal)
                    .ToArray();
                if (shared.Length == 0)
                {
                    continue;
                }

                dependencies.Add(new PipelineWaveDependency(stage.Stage, candidate.Stage, shared));
            }
        }

        return dependencies
            .OrderBy(dependency => dependency.Stage, StringComparer.Ordinal)
            .ThenBy(dependency => dependency.DependsOnStage, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<PlanArtifactKind> Normalize(IEnumerable<PlanArtifactKind> artifacts) =>
        artifacts
            .Where(artifact => artifact != PlanArtifactKind.Unknown)
            .Distinct()
            .OrderBy(artifact => artifact.ToString(), StringComparer.Ordinal)
            .ToArray();

    private static int ComputeDependencyLevel(
        IEnumerable<PlanArtifactKind> reads,
        IReadOnlyDictionary<PlanArtifactKind, int> artifactLevels)
    {
        var maxLevel = 0;
        foreach (var artifact in reads)
        {
            if (artifactLevels.TryGetValue(artifact, out var level))
            {
                maxLevel = Math.Max(maxLevel, level);
            }
        }

        return maxLevel + 1;
    }

    private static void SetArtifactLevel(
        Dictionary<PlanArtifactKind, int> artifactLevels,
        PlanArtifactKind artifact,
        int level)
    {
        if (!artifactLevels.TryGetValue(artifact, out var existing) || level > existing)
        {
            artifactLevels[artifact] = level;
        }
    }
}
