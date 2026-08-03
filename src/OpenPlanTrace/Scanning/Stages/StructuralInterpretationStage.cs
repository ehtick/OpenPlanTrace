using System.Globalization;

namespace OpenPlanTrace;

internal sealed class StructuralInterpretationStage : IPipelineStage
{
    public string Name => "structural-interpretation";

    public ValueTask ExecuteAsync(
        ScanContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!context.Options.StructuralSolver.Enabled)
        {
            context.StructuralEvidenceGraph = StructuralEvidenceGraph.Empty;
            context.StructuralPlanSolution = StructuralPlanSolution.Empty;
            context.AddDiagnostic(
                "structural_interpretation.disabled",
                DiagnosticSeverity.Info,
                Name,
                "Joint structural interpretation is disabled by scanner options.",
                confidence: Confidence.High,
                scope: DiagnosticScope.Document);
            return ValueTask.CompletedTask;
        }

        var source = new StructuralEvidenceSource(
            context.WallCandidates,
            context.Walls,
            context.WallEvidenceMap,
            context.WallGraph,
            context.Rooms,
            context.Openings,
            context.SheetRegions,
            context.Options.DefaultWallThickness)
        {
            SurfacePatterns = context.SurfacePatterns,
            Dimensions = context.Dimensions,
            Annotations = context.Annotations
        };
        context.StructuralEvidenceGraph = StructuralEvidenceGraphBuilder.Build(
            source,
            context.Options.StructuralSolver);
        context.StructuralPlanSolution = JointStructuralSolver.Solve(
            context.StructuralEvidenceGraph,
            context.Options.StructuralSolver);

        AddDiagnostics(context);
        return ValueTask.CompletedTask;
    }

    private void AddDiagnostics(ScanContext context)
    {
        var graph = context.StructuralEvidenceGraph;
        var solution = context.StructuralPlanSolution;
        var selectedCandidateIds = solution.CandidateDecisions
            .Where(decision => decision.Decision == StructuralWallDecisionKind.Selected)
            .Select(decision => decision.CandidateId)
            .ToHashSet(StringComparer.Ordinal);
        var decisionsByCandidateId = solution.CandidateDecisions
            .ToDictionary(decision => decision.CandidateId, StringComparer.Ordinal);
        var objective = new StructuralObjective(
            graph,
            context.Options.StructuralSolver);
        var strongNegativeSelected = graph.WallCandidates
            .Where(candidate => selectedCandidateIds.Contains(candidate.Id))
            .Where(candidate => candidate.HasStrongNegativeEvidence)
            .ToArray();
        var strongNegativeCandidates = graph.WallCandidates
            .Where(candidate => candidate.HasStrongNegativeEvidence)
            .ToArray();
        var absoluteBlockedCandidates = strongNegativeCandidates
            .Where(candidate => candidate.HasAbsoluteBlockingEvidence)
            .ToArray();
        var absoluteBlockedDecisions = solution.CandidateDecisions
            .Where(decision => decision.AbsolutePlacementBlock)
            .ToArray();
        var placementAuthorities = graph.WallCandidates
            .Select(candidate => (
                Candidate: candidate,
                Authority: StructuralPlacementAuthorityEvaluator.Evaluate(candidate)))
            .ToArray();
        var reviewOnlyAuthorityCandidates = placementAuthorities
            .Where(item =>
                item.Authority.Kind == StructuralPlacementAuthorityKind.ReviewOnly)
            .ToArray();
        var selectedAuthorityViolations = reviewOnlyAuthorityCandidates
            .Where(item => selectedCandidateIds.Contains(item.Candidate.Id))
            .ToArray();
        var selectedSourceIds = solution.WallRuns
            .SelectMany(run => run.SourcePrimitiveIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        context.AddDiagnostic(
            "structural_interpretation.solved",
            DiagnosticSeverity.Info,
            Name,
            $"Joint structural solving retained {solution.Metrics.SelectedCandidateCount} candidate(s) as {solution.Metrics.CanonicalWallRunCount} canonical wall run(s).",
            confidence: solution.Metrics.StrongNegativeSelectedCandidateCount == 0
                ? Confidence.High
                : Confidence.Medium,
            scope: DiagnosticScope.Document,
            sourcePrimitiveIds: selectedSourceIds,
            properties: new Dictionary<string, string>
            {
                ["evidenceContractVersion"] = graph.ContractVersion,
                ["solverVersion"] = solution.SolverVersion,
                ["candidateCount"] = solution.Metrics.CandidateCount.ToString(CultureInfo.InvariantCulture),
                ["eligibleCandidateCount"] = solution.Metrics.EligibleCandidateCount.ToString(CultureInfo.InvariantCulture),
                ["selectedCandidateCount"] = solution.Metrics.SelectedCandidateCount.ToString(CultureInfo.InvariantCulture),
                ["reviewCandidateCount"] = solution.Metrics.ReviewCandidateCount.ToString(CultureInfo.InvariantCulture),
                ["wallRunCount"] = solution.Metrics.CanonicalWallRunCount.ToString(CultureInfo.InvariantCulture),
                ["junctionCount"] = solution.Metrics.JunctionCount.ToString(CultureInfo.InvariantCulture),
                ["roomLoopCount"] = solution.Metrics.EvaluatedRoomLoopCount.ToString(CultureInfo.InvariantCulture),
                ["closedRoomLoopCount"] = solution.Metrics.ClosedRoomLoopCount.ToString(CultureInfo.InvariantCulture),
                ["meanRoomBoundaryCoverage"] = solution.Metrics.MeanRoomBoundaryCoverage.ToString("0.######", CultureInfo.InvariantCulture),
                ["recoveredSelectedCandidateCount"] = solution.Metrics.RecoveredSelectedCandidateCount.ToString(CultureInfo.InvariantCulture),
                ["strongNegativeSelectedCandidateCount"] = solution.Metrics.StrongNegativeSelectedCandidateCount.ToString(CultureInfo.InvariantCulture),
                ["strongNegativeSelectedCandidateIds"] = string.Join(",", strongNegativeSelected.Select(candidate => candidate.Id).Take(80)),
                ["strongNegativeSelectedWallIds"] = string.Join(",", strongNegativeSelected.SelectMany(candidate => candidate.SourceWallIds).Distinct(StringComparer.Ordinal).Take(80)),
                ["strongNegativeCandidateCount"] = strongNegativeCandidates.Length.ToString(CultureInfo.InvariantCulture),
                ["absoluteBlockedCandidateCount"] = absoluteBlockedCandidates.Length.ToString(CultureInfo.InvariantCulture),
                ["absoluteBlockedDecisionCount"] = absoluteBlockedDecisions.Length.ToString(CultureInfo.InvariantCulture),
                ["reviewOnlyPlacementAuthorityCount"] = reviewOnlyAuthorityCandidates.Length.ToString(CultureInfo.InvariantCulture),
                ["selectedPlacementAuthorityViolationCount"] = selectedAuthorityViolations.Length.ToString(CultureInfo.InvariantCulture),
                ["placementAuthorityCounts"] = string.Join(
                    ",",
                    placementAuthorities
                        .GroupBy(item => item.Authority.Kind)
                        .OrderBy(group => group.Key)
                        .Select(group => $"{group.Key}:{group.Count()}")),
                ["absoluteBlockedWallIds"] = string.Join(
                    ",",
                    absoluteBlockedCandidates
                        .SelectMany(candidate => candidate.SourceWallIds)
                        .Distinct(StringComparer.Ordinal)
                        .Take(80)),
                ["absoluteBlockedDecisionWallIds"] = string.Join(
                    ",",
                    absoluteBlockedDecisions
                        .SelectMany(decision => decision.SourceWallIds)
                        .Distinct(StringComparer.Ordinal)
                        .Take(80)),
                ["blockingCandidateDetails"] = string.Join(
                    ";",
                    strongNegativeCandidates
                        .Take(80)
                        .Select(candidate =>
                        {
                            decisionsByCandidateId.TryGetValue(candidate.Id, out var decision);
                            var selectionDelta = selectedCandidateIds.Contains(candidate.Id)
                                ? 0
                                : objective.ToggleDelta(
                                    candidate.Id,
                                    selectedCandidateIds,
                                    add: true);
                            var selectedRelations = graph.Relations
                                .Where(relation =>
                                    string.Equals(
                                        relation.FirstCandidateId,
                                        candidate.Id,
                                        StringComparison.Ordinal)
                                    || string.Equals(
                                        relation.SecondCandidateId,
                                        candidate.Id,
                                        StringComparison.Ordinal))
                                .Select(relation => (
                                    Relation: relation,
                                    OtherId: string.Equals(
                                        relation.FirstCandidateId,
                                        candidate.Id,
                                        StringComparison.Ordinal)
                                            ? relation.SecondCandidateId
                                            : relation.FirstCandidateId))
                                .Where(item => selectedCandidateIds.Contains(item.OtherId))
                                .OrderBy(item => item.Relation.Kind)
                                .ThenBy(item => item.OtherId, StringComparer.Ordinal)
                                .Select(item =>
                                    $"{item.Relation.Kind}:{item.Relation.Weight:0.###}:{item.Relation.IsHardConstraint}:{item.OtherId}")
                                .ToArray();
                            var authority =
                                StructuralPlacementAuthorityEvaluator.Evaluate(candidate);
                            return $"{candidate.Id}|decision={decision?.Decision}|absolute={candidate.HasAbsoluteBlockingEvidence}|authority={authority.Kind}|authorityReason={authority.Reason}|score={candidate.UnaryScore:0.###}|selectionDelta={selectionDelta:0.###}|origins={candidate.Origins}|signals={string.Join(",", candidate.Signals.Where(signal => signal.IsStrongBlockingSemanticNegative).Select(signal => $"{signal.Kind}:{signal.Weight:0.###}"))}|selectedRelations={string.Join(",", selectedRelations)}|reasons={string.Join("/", decision?.Reasons ?? Array.Empty<string>())}";
                        })),
                ["strongNegativeSelectedDetails"] = string.Join(
                    ";",
                    strongNegativeSelected
                        .Take(80)
                        .Select(candidate =>
                            $"{candidate.Id}|score={candidate.UnaryScore:0.###}|origins={candidate.Origins}|rooms={candidate.SourceRoomIds.Count}|signals={string.Join(",", candidate.Signals.Where(signal => signal.Weight < 0).Select(signal => $"{signal.Kind}:{signal.Weight:0.###}"))}")),
                ["objectiveScore"] = solution.ObjectiveScore.ToString("0.######", CultureInfo.InvariantCulture),
                ["optimizationPassCount"] = solution.Metrics.OptimizationPassCount.ToString(CultureInfo.InvariantCulture),
                ["coherentBundlePassCount"] = solution.Metrics.CoherentBundlePassCount.ToString(CultureInfo.InvariantCulture),
                ["acceptedCoherentBundleCount"] = solution.Metrics.AcceptedCoherentBundleCount.ToString(CultureInfo.InvariantCulture),
                ["bundleRecoveredCandidateCount"] = solution.Metrics.BundleRecoveredCandidateCount.ToString(CultureInfo.InvariantCulture)
            });

        if (solution.Metrics.StrongNegativeSelectedCandidateCount > 0)
        {
            context.AddDiagnostic(
                "structural_interpretation.negative_evidence_selected",
                DiagnosticSeverity.Warning,
                Name,
                $"{solution.Metrics.StrongNegativeSelectedCandidateCount} selected structural candidate(s) retain strong negative evidence and require review.",
                confidence: Confidence.Medium,
                scope: DiagnosticScope.Document,
                sourcePrimitiveIds: selectedSourceIds,
                properties: new Dictionary<string, string>
                {
                    ["selectedCount"] = solution.Metrics.SelectedCandidateCount.ToString(CultureInfo.InvariantCulture),
                    ["strongNegativeSelectedCount"] = solution.Metrics.StrongNegativeSelectedCandidateCount.ToString(CultureInfo.InvariantCulture)
                });
        }
    }
}
