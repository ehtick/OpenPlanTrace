namespace OpenPlanTrace;

public static class JointStructuralSolver
{
    public static StructuralPlanSolution Solve(
        StructuralEvidenceGraph graph,
        StructuralSolverOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(graph);

        options ??= new StructuralSolverOptions();
        if (!options.Enabled || graph.WallCandidates.Count == 0)
        {
            return StructuralPlanSolution.Empty;
        }

        var objective = new StructuralObjective(graph, options);
        var dominatedByCandidateId =
            StructuralCandidateDominance.FindDominatedCandidates(graph, options);
        var junctionSupportedCandidateIds = graph.Relations
            .Where(relation =>
                relation.Kind == StructuralEvidenceRelationKind.Junction
                && relation.Weight > 0)
            .SelectMany(relation => new[]
            {
                relation.FirstCandidateId,
                relation.SecondCandidateId
            })
            .ToHashSet(StringComparer.Ordinal);
        var selected = BuildInitialSelection(
            graph.WallCandidates,
            options,
            dominatedByCandidateId.Keys);
        var considered = graph.WallCandidates
            .Where(candidate => candidate.IsEligible)
            .Where(candidate => !dominatedByCandidateId.ContainsKey(candidate.Id))
            .Where(candidate => !candidate.HasStrongRepeatedDetailEvidence)
            .Where(candidate => !candidate.HasAbsoluteBlockingEvidence)
            .Where(candidate =>
                candidate.UnaryScore >= options.MinimumConsiderationScore
                || candidate.WasAcceptedByPreliminaryPipeline
                || candidate.Origins.HasFlag(StructuralCandidateOrigin.RoomBoundary)
                || candidate.Origins.HasFlag(StructuralCandidateOrigin.ExteriorShell)
                || (!candidate.HasStrongNegativeEvidence
                    && junctionSupportedCandidateIds.Contains(candidate.Id)))
            .OrderByDescending(candidate => SelectionPriority(candidate))
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();

        var completedPasses = OptimizeSingleCandidateMoves(
            considered,
            selected,
            objective,
            options);
        var bundleResult = StructuralCoherentBundleOptimizer.Optimize(
            graph,
            considered,
            selected,
            objective,
            options);
        if (bundleResult.AcceptedBundleCount > 0)
        {
            completedPasses += OptimizeSingleCandidateMoves(
                considered,
                selected,
                objective,
                options);
        }

        ResolveHardConflicts(graph, selected, objective);
        var topology = CanonicalStructuralTopologyBuilder.Build(graph, selected, options);
        var roomClosures = BuildRoomClosures(graph, selected, topology.WallRuns, objective);
        var decisions = BuildDecisions(
            graph,
            selected,
            objective,
            options,
            dominatedByCandidateId);
        var objectiveScore = objective.Evaluate(selected);
        var meanCoverage = roomClosures.Count == 0
            ? 0
            : roomClosures.Average(room => room.BoundaryCoverage);
        var metrics = new StructuralSolutionMetrics(
            graph.WallCandidates.Count,
            graph.WallCandidates.Count(candidate => candidate.IsEligible),
            selected.Count,
            decisions.Count(decision => decision.Decision == StructuralWallDecisionKind.Rejected),
            decisions.Count(decision => decision.Decision == StructuralWallDecisionKind.RetainedForReview),
            topology.WallRuns.Count,
            topology.Junctions.Count,
            roomClosures.Count,
            roomClosures.Count(room => room.IsClosed),
            Round(meanCoverage),
            graph.WallCandidates.Count(candidate => candidate.WasAcceptedByPreliminaryPipeline),
            graph.WallCandidates.Count(candidate =>
                selected.Contains(candidate.Id)
                && !candidate.WasAcceptedByPreliminaryPipeline),
            graph.WallCandidates.Count(candidate =>
                selected.Contains(candidate.Id)
                && candidate.HasStrongNegativeEvidence),
            completedPasses,
            bundleResult.CompletedPassCount,
            bundleResult.AcceptedBundleCount,
            bundleResult.AddedCandidateCount);

        return new StructuralPlanSolution(
            StructuralPlanSolution.CurrentSolverVersion,
            graph.ContractVersion,
            Round(objectiveScore),
            decisions,
            topology.WallRuns,
            topology.Junctions,
            roomClosures,
            metrics,
            new[]
            {
                $"joint solver considered {considered.Length} of {graph.WallCandidates.Count} retained candidate(s)",
                $"suppressed {dominatedByCandidateId.Count} contained fragment or duplicate wall-face representation(s) behind stronger source-backed wall bodies",
                $"joint solver selected {selected.Count} candidate(s) after {completedPasses} deterministic pass(es)",
                $"coherent bundle search accepted {bundleResult.AcceptedBundleCount} bundle(s) and recovered {bundleResult.AddedCandidateCount} mutually supporting candidate(s)",
                $"accepted bundles included {bundleResult.AcceptedRoomClosureBundleCount} room closure bundle(s) and {bundleResult.AcceptedContinuationBundleCount} continuation bundle(s)",
                $"selected candidates compacted into {topology.WallRuns.Count} canonical wall run(s)",
                $"canonical topology contains {topology.Junctions.Count} unsplit junction reference(s)",
                $"room-loop objective closed {metrics.ClosedRoomLoopCount} of {metrics.EvaluatedRoomLoopCount} evaluated room loop(s)",
                "preliminary detections and rejects remain available in the structural evidence graph"
            });
    }

    private static int OptimizeSingleCandidateMoves(
        IReadOnlyList<StructuralWallCandidate> considered,
        HashSet<string> selected,
        StructuralObjective objective,
        StructuralSolverOptions options)
    {
        var completedPasses = 0;
        for (var pass = 0; pass < options.MaximumOptimizationPasses; pass++)
        {
            var changed = false;
            foreach (var candidate in considered.Where(candidate => !selected.Contains(candidate.Id)))
            {
                var delta = objective.ToggleDelta(candidate.Id, selected, add: true);
                if (delta > options.ObjectiveImprovementTolerance)
                {
                    selected.Add(candidate.Id);
                    changed = true;
                }
            }

            foreach (var candidate in considered
                         .Where(candidate => selected.Contains(candidate.Id))
                         .OrderBy(candidate => candidate.UnaryScore)
                         .ThenBy(candidate => candidate.DrawingLength)
                         .ThenBy(candidate => candidate.Id, StringComparer.Ordinal))
            {
                var delta = objective.ToggleDelta(candidate.Id, selected, add: false);
                if (delta > options.ObjectiveImprovementTolerance)
                {
                    selected.Remove(candidate.Id);
                    changed = true;
                }
            }

            completedPasses++;
            if (!changed)
            {
                break;
            }
        }

        return completedPasses;
    }

    private static HashSet<string> BuildInitialSelection(
        IReadOnlyList<StructuralWallCandidate> candidates,
        StructuralSolverOptions options,
        IEnumerable<string> dominatedCandidateIds)
    {
        var dominated = dominatedCandidateIds.ToHashSet(StringComparer.Ordinal);
        return candidates
            .Where(candidate => candidate.IsEligible)
            .Where(candidate => !dominated.Contains(candidate.Id))
            .Where(candidate =>
                (!candidate.HasStrongNegativeEvidence
                    && candidate.UnaryScore >= options.InitialSelectionScore)
                || (candidate.WasAcceptedByPreliminaryPipeline
                    && !candidate.HasStrongNegativeEvidence
                    && candidate.UnaryScore >= options.MinimumConsiderationScore))
            .Select(candidate => candidate.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void ResolveHardConflicts(
        StructuralEvidenceGraph graph,
        HashSet<string> selected,
        StructuralObjective objective)
    {
        foreach (var relation in graph.Relations
                     .Where(relation => relation.IsHardConstraint)
                     .Where(relation =>
                         relation.Kind is StructuralEvidenceRelationKind.Duplicate
                             or StructuralEvidenceRelationKind.Conflict)
                     .OrderBy(relation => relation.Id, StringComparer.Ordinal))
        {
            if (!selected.Contains(relation.FirstCandidateId)
                || !selected.Contains(relation.SecondCandidateId))
            {
                continue;
            }

            var firstRemovalDelta = objective.ToggleDelta(
                relation.FirstCandidateId,
                selected,
                add: false);
            var secondRemovalDelta = objective.ToggleDelta(
                relation.SecondCandidateId,
                selected,
                add: false);
            if (firstRemovalDelta > secondRemovalDelta)
            {
                selected.Remove(relation.FirstCandidateId);
            }
            else if (secondRemovalDelta > firstRemovalDelta)
            {
                selected.Remove(relation.SecondCandidateId);
            }
            else
            {
                selected.Remove(string.CompareOrdinal(relation.FirstCandidateId, relation.SecondCandidateId) > 0
                    ? relation.FirstCandidateId
                    : relation.SecondCandidateId);
            }
        }
    }

    private static IReadOnlyList<StructuralWallDecision> BuildDecisions(
        StructuralEvidenceGraph graph,
        IReadOnlySet<string> selected,
        StructuralObjective objective,
        StructuralSolverOptions options,
        IReadOnlyDictionary<string, string> dominatedByCandidateId)
    {
        return graph.WallCandidates
            .Select(candidate =>
            {
                var isSelected = selected.Contains(candidate.Id);
                var isDominated = dominatedByCandidateId.TryGetValue(
                    candidate.Id,
                    out var dominantCandidateId);
                var decision = !candidate.IsEligible
                    ? StructuralWallDecisionKind.Invalid
                    : isSelected
                        ? StructuralWallDecisionKind.Selected
                        : isDominated
                            ? StructuralWallDecisionKind.Rejected
                        : candidate.UnaryScore >= options.MinimumConsiderationScore
                            || candidate.Origins.HasFlag(StructuralCandidateOrigin.RoomBoundary)
                            ? StructuralWallDecisionKind.RetainedForReview
                            : StructuralWallDecisionKind.Rejected;
                var reasons = new List<string>
                {
                    $"unary score {candidate.UnaryScore:0.###}",
                    $"origins {candidate.Origins}"
                };
                if (candidate.HasStrongNegativeEvidence)
                {
                    reasons.Add("contains strong negative structural evidence");
                }

                if (candidate.SourceRoomIds.Count > 0)
                {
                    reasons.Add($"supported by {candidate.SourceRoomIds.Count} room loop(s)");
                }

                if (isDominated)
                {
                    reasons.Add(
                        $"contained fragment or duplicate wall-face represented by stronger wall candidate {dominantCandidateId}");
                }

                reasons.Add(decision switch
                {
                    StructuralWallDecisionKind.Selected => "selected by joint structural objective",
                    StructuralWallDecisionKind.Rejected when isDominated =>
                        "suppressed as a redundant partial representation of a cleaner wall body",
                    StructuralWallDecisionKind.RetainedForReview => "not selected, but retained as an auditable alternative",
                    StructuralWallDecisionKind.Invalid => "invalid or degenerate geometry",
                    _ => "rejected by joint structural objective"
                });

                return new StructuralWallDecision(
                    candidate.Id,
                    decision,
                    candidate.UnaryScore,
                    Round(objective.CandidateContribution(candidate.Id, selected)),
                    reasons)
                {
                    SourceWallIds = candidate.SourceWallIds,
                    AbsolutePlacementBlock = candidate.HasAbsoluteBlockingEvidence,
                    BlockingSignalKinds = candidate.Signals
                        .Where(signal => signal.IsStrongBlockingSemanticNegative)
                        .Select(signal => signal.Kind)
                        .Distinct()
                        .Order()
                        .ToArray()
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<StructuralRoomClosure> BuildRoomClosures(
        StructuralEvidenceGraph graph,
        IReadOnlySet<string> selected,
        IReadOnlyList<StructuralWallRun> runs,
        StructuralObjective objective)
    {
        var runIdsByCandidate = runs
            .SelectMany(run => run.CandidateIds.Select(candidateId => (CandidateId: candidateId, RunId: run.Id)))
            .GroupBy(item => item.CandidateId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.RunId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        return graph.RoomLoops
            .Select(loop =>
            {
                var missing = loop.BoundaryEdges
                    .Where(edge => !edge.CandidateIds.Any(selected.Contains))
                    .Select(edge => edge.Id)
                    .ToArray();
                var selectedRunIds = loop.BoundaryEdges
                    .SelectMany(edge => edge.CandidateIds)
                    .Where(selected.Contains)
                    .Where(runIdsByCandidate.ContainsKey)
                    .SelectMany(candidateId => runIdsByCandidate[candidateId])
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var coverage = objective.RoomBoundaryCoverage(loop, selected);
                return new StructuralRoomClosure(
                    loop.Id,
                    loop.SourceRoomId,
                    Round(coverage),
                    coverage >= 0.90,
                    selectedRunIds,
                    missing);
            })
            .ToArray();
    }

    private static double SelectionPriority(StructuralWallCandidate candidate)
    {
        var originPriority = candidate.Origins.HasFlag(StructuralCandidateOrigin.AcceptedWall) ? 2.0 : 0;
        originPriority += candidate.Origins.HasFlag(StructuralCandidateOrigin.WallGraph) ? 1.0 : 0;
        originPriority += candidate.Origins.HasFlag(StructuralCandidateOrigin.RoomBoundary) ? 0.8 : 0;
        originPriority += candidate.Origins.HasFlag(StructuralCandidateOrigin.ExteriorShell) ? 0.6 : 0;
        return originPriority + candidate.UnaryScore + Math.Min(0.5, candidate.DrawingLength / 1000.0);
    }

    private static double Round(double value) =>
        Math.Round(value, 6, MidpointRounding.AwayFromZero);
}
