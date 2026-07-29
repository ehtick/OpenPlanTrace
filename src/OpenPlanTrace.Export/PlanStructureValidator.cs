namespace OpenPlanTrace.Export;

public sealed record PlanStructureValidationMessage(
    string Severity,
    string Code,
    string Path,
    string Message);

public static class PlanStructureValidator
{
    private const double CoordinateTolerance = 0.01;

    public static IReadOnlyList<PlanStructureValidationMessage> Validate(PlanStructureExport export)
    {
        ArgumentNullException.ThrowIfNull(export);

        var messages = new List<PlanStructureValidationMessage>();
        if (!string.Equals(
                export.SchemaVersion,
                PlanStructureExport.CurrentSchemaVersion,
                StringComparison.Ordinal))
        {
            AddError(
                messages,
                "structure.schema_version.unsupported",
                "$.schemaVersion",
                $"Expected '{PlanStructureExport.CurrentSchemaVersion}', found '{export.SchemaVersion}'.");
        }

        var pages = export.Pages ?? Array.Empty<PlacementPageExport>();
        var wallRuns = export.WallRuns ?? Array.Empty<PlanStructureWallRunExport>();
        var nodes = export.Nodes ?? Array.Empty<PlanStructureNodeExport>();
        var rooms = export.Rooms ?? Array.Empty<PlanStructureRoomExport>();
        var openings = export.Openings ?? Array.Empty<PlanStructureOpeningExport>();
        ValidateWallSolver(export.WallSolver, wallRuns, messages);
        var pageNumbers = pages.Select(page => page.PageNumber).ToHashSet();
        CheckUnique(
            pages.Select(page => page.PageNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            "$.pages",
            "structure.page.id_duplicate",
            messages);
        CheckUnique(wallRuns.Select(run => run.Id), "$.wallRuns", "structure.wall_run.id_duplicate", messages);
        CheckUnique(nodes.Select(node => node.Id), "$.nodes", "structure.node.id_duplicate", messages);
        CheckUnique(rooms.Select(room => room.Id), "$.rooms", "structure.room.id_duplicate", messages);
        CheckUnique(openings.Select(opening => opening.Id), "$.openings", "structure.opening.id_duplicate", messages);
        var openingIds = openings
            .Where(opening => !string.IsNullOrWhiteSpace(opening.Id))
            .Select(opening => opening.Id)
            .ToHashSet(StringComparer.Ordinal);
        var wallOpeningIntervals = wallRuns
            .SelectMany(run => run.OpeningIntervals ?? Array.Empty<PlacementSolvedWallOpeningIntervalExport>())
            .ToArray();
        CheckUnique(
            wallOpeningIntervals.Select(interval => interval.Id),
            "$.wallRuns[*].openingIntervals",
            "structure.wall_run.opening_interval_id_duplicate",
            messages);
        CheckUnique(
            wallRuns.SelectMany(run =>
                    run.InlineJunctions
                    ?? Array.Empty<PlacementSolvedWallInlineJunctionExport>())
                .Select(junction => junction.Id),
            "$.wallRuns[*].inlineJunctions",
            "structure.wall_run.inline_junction_id_duplicate",
            messages);
        var wallOpeningIntervalsById = wallOpeningIntervals
            .Where(interval => !string.IsNullOrWhiteSpace(interval.Id))
            .GroupBy(interval => interval.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        for (var index = 0; index < pages.Count; index++)
        {
            var page = pages[index];
            if (page.PageNumber <= 0 || !IsPositiveFinite(page.Width) || !IsPositiveFinite(page.Height))
            {
                AddError(
                    messages,
                    "structure.page.geometry_invalid",
                    $"$.pages[{index}]",
                    "Page number, width, and height must be positive.");
            }
        }

        var nodesById = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var runIds = wallRuns
            .Where(run => !string.IsNullOrWhiteSpace(run.Id))
            .Select(run => run.Id)
            .ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < wallRuns.Count; index++)
        {
            var run = wallRuns[index];
            var path = $"$.wallRuns[{index}]";
            ValidatePageReference(run.PageNumber, pageNumbers, path, messages);
            if (string.IsNullOrWhiteSpace(run.Id)
                || string.IsNullOrWhiteSpace(run.FromNodeId)
                || string.IsNullOrWhiteSpace(run.ToNodeId))
            {
                AddError(
                    messages,
                    "structure.wall_run.identity_missing",
                    path,
                    "Wall run id and endpoint node ids are required.");
            }

            if (string.Equals(run.FromNodeId, run.ToNodeId, StringComparison.Ordinal))
            {
                AddError(
                    messages,
                    "structure.wall_run.self_loop",
                    path,
                    "A wall run cannot reference the same node at both endpoints.");
            }

            var geometricLength = Distance(run.CenterLine.Start, run.CenterLine.End);
            if (!IsPositiveFinite(run.DrawingLength)
                || !IsPositiveFinite(geometricLength)
                || Math.Abs(geometricLength - run.DrawingLength) > Math.Max(CoordinateTolerance, run.DrawingLength * 0.001))
            {
                AddError(
                    messages,
                    "structure.wall_run.length_invalid",
                    path,
                    "drawingLength must be positive and match the centerline length.");
            }

            ValidateNodeReference(
                run.FromNodeId,
                run.PageNumber,
                run.CenterLine.Start,
                nodesById,
                $"{path}.fromNodeId",
                messages);
            ValidateNodeReference(
                run.ToNodeId,
                run.PageNumber,
                run.CenterLine.End,
                nodesById,
                $"{path}.toNodeId",
                messages);
            if (run.SourceWallIds is null || run.SourceWallIds.Count == 0)
            {
                messages.Add(new PlanStructureValidationMessage(
                    "Warning",
                    "structure.wall_run.source_wall_missing",
                    $"{path}.sourceWallIds",
                    "Canonical wall runs should retain at least one source wall id."));
            }

            ValidateWallIntervals(run, openingIds, path, messages);
            ValidateInlineJunctions(run, nodesById, runIds, path, messages);
            ValidateWallReconciliation(run, path, messages);
        }

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var path = $"$.nodes[{index}]";
            ValidatePageReference(node.PageNumber, pageNumbers, path, messages);
            if (!IsFinite(node.Position.X) || !IsFinite(node.Position.Y) || node.Degree < 1)
            {
                AddError(
                    messages,
                    "structure.node.geometry_invalid",
                    path,
                    "Node coordinates must be finite and degree must be positive.");
            }

            var endpointWallRunIds = wallRuns
                .Where(run =>
                    string.Equals(run.FromNodeId, node.Id, StringComparison.Ordinal)
                    || string.Equals(run.ToNodeId, node.Id, StringComparison.Ordinal))
                .Select(run => run.Id)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var inlineReferences = wallRuns
                .SelectMany(run =>
                    (run.InlineJunctions
                        ?? Array.Empty<PlacementSolvedWallInlineJunctionExport>())
                    .Where(junction =>
                        string.Equals(junction.NodeId, node.Id, StringComparison.Ordinal)))
                .ToArray();
            var inlineWallRunIds = inlineReferences
                .Select(junction => junction.WallRunId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var incidentWallRunIds = endpointWallRunIds
                .Concat(inlineWallRunIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var actualDegree = endpointWallRunIds.Length + inlineWallRunIds.Length * 2;
            if (actualDegree != node.Degree)
            {
                AddError(
                    messages,
                    "structure.node.degree_mismatch",
                    $"{path}.degree",
                    $"Node degree is {node.Degree}, but canonical incidences form {actualDegree} graph arm(s).");
            }

            if (!(node.EndpointWallRunIds ?? Array.Empty<string>())
                    .SequenceEqual(endpointWallRunIds, StringComparer.Ordinal)
                || !(node.InlineWallRunIds ?? Array.Empty<string>())
                    .SequenceEqual(inlineWallRunIds, StringComparer.Ordinal)
                || !(node.IncidentWallRunIds ?? Array.Empty<string>())
                    .SequenceEqual(incidentWallRunIds, StringComparer.Ordinal))
            {
                AddError(
                    messages,
                    "structure.node.incidence_mismatch",
                    path,
                    "Node endpoint, inline, and incident wall run ids must match canonical wall references.");
            }

            var expectedKind = actualDegree <= 1
                ? "Endpoint"
                : actualDegree == 2
                    ? "InlineOrCorner"
                    : "Junction";
            var expectedTopologyKind = ExpectedTopologyKind(
                actualDegree,
                node.Directions ?? Array.Empty<string>(),
                endpointWallRunIds,
                inlineWallRunIds,
                inlineReferences);
            if (!string.Equals(node.Kind, expectedKind, StringComparison.Ordinal)
                || !string.Equals(node.TopologyKind, expectedTopologyKind, StringComparison.Ordinal))
            {
                AddError(
                    messages,
                    "structure.node.kind_mismatch",
                    path,
                    $"Node kind/topologyKind must be '{expectedKind}'/'{expectedTopologyKind}' for its canonical incidences.");
            }

            var expectedReview = wallRuns
                    .Where(run => incidentWallRunIds.Contains(run.Id, StringComparer.Ordinal))
                    .Any(run => run.Reliability.RequiresReview)
                || inlineReferences.Any(junction => junction.RequiresReview);
            if (node.RequiresReview != expectedReview
                || inlineReferences.Length > 0 != (node.Optimization is not null))
            {
                AddError(
                    messages,
                    "structure.node.topology_metadata_mismatch",
                    path,
                    "Node review and optimization metadata must agree with its canonical wall incidences.");
            }
        }

        for (var index = 0; index < rooms.Count; index++)
        {
            var room = rooms[index];
            var path = $"$.rooms[{index}]";
            ValidatePageReference(room.PageNumber, pageNumbers, path, messages);
            if (room.Boundary is null || room.Boundary.Count < 3 || room.DrawingArea < 0)
            {
                AddError(
                    messages,
                    "structure.room.geometry_invalid",
                    path,
                    "Room boundary must contain at least three points and drawingArea cannot be negative.");
            }

            foreach (var wallRunId in room.BoundaryWallRunIds ?? Array.Empty<string>())
            {
                if (!runIds.Contains(wallRunId))
                {
                    AddError(
                        messages,
                        "structure.room.wall_run_reference_missing",
                        $"{path}.boundaryWallRunIds",
                        $"Room references missing wall run '{wallRunId}'.");
                }
            }
        }

        for (var index = 0; index < openings.Count; index++)
        {
            var opening = openings[index];
            var path = $"$.openings[{index}]";
            ValidatePageReference(opening.PageNumber, pageNumbers, path, messages);
            if (!IsPositiveFinite(opening.DrawingWidth))
            {
                AddError(
                    messages,
                    "structure.opening.width_invalid",
                    $"{path}.drawingWidth",
                    "Opening width must be positive.");
            }

            var anchored = opening.Placement is not null;
            if (anchored != string.Equals(opening.PlacementStatus, "Anchored", StringComparison.Ordinal))
            {
                AddError(
                    messages,
                    "structure.opening.placement_status_mismatch",
                    $"{path}.placementStatus",
                    "placementStatus must agree with the presence of placement geometry.");
            }

            foreach (var wallRunId in opening.HostWallRunIds ?? Array.Empty<string>())
            {
                if (!runIds.Contains(wallRunId))
                {
                    AddError(
                        messages,
                        "structure.opening.wall_run_reference_missing",
                        $"{path}.hostWallRunIds",
                        $"Opening references missing wall run '{wallRunId}'.");
                }
            }

            foreach (var intervalId in opening.HostWallOpeningIntervalIds ?? Array.Empty<string>())
            {
                if (!wallOpeningIntervalsById.TryGetValue(intervalId, out var interval))
                {
                    AddError(
                        messages,
                        "structure.opening.wall_interval_reference_missing",
                        $"{path}.hostWallOpeningIntervalIds",
                        $"Opening references missing wall opening interval '{intervalId}'.");
                    continue;
                }

                if (!string.Equals(interval.OpeningId, opening.Id, StringComparison.Ordinal))
                {
                    AddError(
                        messages,
                        "structure.opening.wall_interval_reference_mismatch",
                        $"{path}.hostWallOpeningIntervalIds",
                        $"Wall opening interval '{intervalId}' belongs to opening '{interval.OpeningId}', not '{opening.Id}'.");
                }
            }

            if ((opening.HostWallOpeningIntervalIds?.Count ?? 0) > 0
                && (opening.HostWallRunIds?.Count ?? 0) == 0)
            {
                AddError(
                    messages,
                    "structure.opening.wall_interval_host_missing",
                    path,
                    "An opening with canonical wall intervals must also reference its host wall run.");
            }
        }

        ValidateSummary(export, pages, wallRuns, nodes, rooms, openings, messages);
        if (!IsRatio(export.Quality.IntegrityScore))
        {
            AddError(
                messages,
                "structure.quality.score_invalid",
                "$.quality.integrityScore",
                "Integrity score must be between zero and one.");
        }

        return messages;
    }

    private static void ValidateWallSolver(
        PlanStructureWallSolverExport? solver,
        IReadOnlyList<PlanStructureWallRunExport> wallRuns,
        ICollection<PlanStructureValidationMessage> messages)
    {
        if (solver is null)
        {
            AddError(
                messages,
                "structure.wall_solver.missing",
                "$.wallSolver",
                "Canonical structure requires the wall-solver decision summary.");
            return;
        }

        if (!string.Equals(
                solver.SolverVersion,
                GlobalWallSolutionBuilder.SolverVersion,
                StringComparison.Ordinal))
        {
            AddError(
                messages,
                "structure.wall_solver.version_unsupported",
                "$.wallSolver.solverVersion",
                $"Expected '{GlobalWallSolutionBuilder.SolverVersion}', found '{solver.SolverVersion}'.");
        }

        if (!IsRatio(solver.SelectedScore)
            || solver.CandidateCount < 0
            || solver.SelectedCandidateCount < 0
            || solver.SelectedWallRunCount < 0
            || solver.IterationCount < 0
            || solver.SelectedCandidateCount > solver.CandidateCount)
        {
            AddError(
                messages,
                "structure.wall_solver.summary_invalid",
                "$.wallSolver",
                "Solver score and counts must be internally valid.");
        }

        if (solver.SelectedWallRunCount != wallRuns.Count)
        {
            AddError(
                messages,
                "structure.wall_solver.run_count_mismatch",
                "$.wallSolver.selectedWallRunCount",
                $"Solver reports {solver.SelectedWallRunCount} selected run(s), but structure contains {wallRuns.Count}.");
        }

        var hypotheses = solver.Hypotheses ?? Array.Empty<PlanStructureWallHypothesisExport>();
        var selected = hypotheses.Where(hypothesis => hypothesis.Selected).ToArray();
        if (hypotheses.Count == 0
            || selected.Length != 1
            || !string.Equals(selected.FirstOrDefault()?.Id, solver.SelectedHypothesisId, StringComparison.Ordinal)
            || !string.Equals(selected.FirstOrDefault()?.Profile, solver.SelectedProfile, StringComparison.Ordinal))
        {
            AddError(
                messages,
                "structure.wall_solver.selection_invalid",
                "$.wallSolver.hypotheses",
                "Exactly one hypothesis must be selected and it must match selectedHypothesisId and selectedProfile.");
        }

        ValidateSolverMetrics(solver.Metrics, "$.wallSolver.metrics", messages);
        ValidateWallReconciliationSummary(
            solver.Reconciliation,
            wallRuns,
            "$.wallSolver.reconciliation",
            messages);
        ValidateWallTopologySummary(
            solver.Topology,
            wallRuns,
            "$.wallSolver.topology",
            messages);
        for (var index = 0; index < hypotheses.Count; index++)
        {
            var hypothesis = hypotheses[index];
            if (!IsRatio(hypothesis.Score)
                || hypothesis.SelectedCandidateCount < 0
                || hypothesis.RecoveredCandidateCount < 0)
            {
                AddError(
                    messages,
                    "structure.wall_solver.hypothesis_invalid",
                    $"$.wallSolver.hypotheses[{index}]",
                    "Hypothesis score and counts must be non-negative and normalized.");
            }

            ValidateSolverMetrics(
                hypothesis.Metrics,
                $"$.wallSolver.hypotheses[{index}].metrics",
                messages);
        }
    }

    private static void ValidateWallTopologySummary(
        PlacementWallTopologyOptimizationSummaryExport? summary,
        IReadOnlyList<PlanStructureWallRunExport> wallRuns,
        string path,
        ICollection<PlanStructureValidationMessage> messages)
    {
        if (summary is null)
        {
            AddError(
                messages,
                "structure.wall_topology.summary_missing",
                path,
                "Canonical structure requires the robust topology optimization summary.");
            return;
        }

        var references = wallRuns
            .SelectMany(run =>
                run.InlineJunctions
                ?? Array.Empty<PlacementSolvedWallInlineJunctionExport>())
            .ToArray();
        var nodeOptimizations = references
            .GroupBy(reference => reference.NodeId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        if (!string.Equals(
                summary.OptimizerVersion,
                GlobalWallSolutionBuilder.TopologyOptimizerVersion,
                StringComparison.Ordinal)
            || !string.Equals(summary.Method, "RobustWeightedLeastSquaresHuber", StringComparison.Ordinal)
            || summary.EvaluatedWallPairCount < 0
            || summary.JunctionNodeCount < 0
            || summary.InlineJunctionReferenceCount < 0
            || summary.TJunctionNodeCount < 0
            || summary.CrossingNodeCount < 0
            || summary.EndpointAnchoredNodeCount < 0
            || summary.ObservationCount < 0
            || summary.LineConstraintCount < 0
            || summary.MaximumIterationCount < 0
            || !IsFinite(summary.RootMeanSquareResidualDrawingUnits)
            || summary.RootMeanSquareResidualDrawingUnits < 0
            || !IsFinite(summary.MaximumResidualDrawingUnits)
            || summary.MaximumResidualDrawingUnits < 0
            || !IsFinite(summary.RobustObjective)
            || summary.RobustObjective < 0)
        {
            AddError(
                messages,
                "structure.wall_topology.summary_invalid",
                path,
                "Topology optimizer version, method, counts, and residuals must be valid.");
            return;
        }

        var expectations = new[]
        {
            ("junctionNodeCount", summary.JunctionNodeCount, nodeOptimizations.Length),
            ("inlineJunctionReferenceCount", summary.InlineJunctionReferenceCount, references.Length),
            ("tJunctionNodeCount", summary.TJunctionNodeCount, nodeOptimizations.Count(reference => string.Equals(reference.Kind, "TJunction", StringComparison.Ordinal))),
            ("crossingNodeCount", summary.CrossingNodeCount, nodeOptimizations.Count(reference => string.Equals(reference.Kind, "Crossing", StringComparison.Ordinal))),
            ("endpointAnchoredNodeCount", summary.EndpointAnchoredNodeCount, nodeOptimizations.Count(reference => reference.Optimization.EndpointAnchored)),
            ("observationCount", summary.ObservationCount, nodeOptimizations.Sum(reference => reference.Optimization.ObservationCount)),
            ("lineConstraintCount", summary.LineConstraintCount, nodeOptimizations.Sum(reference => reference.Optimization.LineConstraintCount)),
            ("maximumIterationCount", summary.MaximumIterationCount, nodeOptimizations.Length == 0 ? 0 : nodeOptimizations.Max(reference => reference.Optimization.IterationCount))
        };
        foreach (var (name, actual, expected) in expectations)
        {
            if (actual != expected)
            {
                AddError(
                    messages,
                    "structure.wall_topology.summary_count_mismatch",
                    $"{path}.{name}",
                    $"Topology summary value {actual} does not match calculated value {expected}.");
            }
        }
    }

    private static void ValidateWallReconciliationSummary(
        PlacementWallReconciliationSummaryExport? summary,
        IReadOnlyList<PlanStructureWallRunExport> wallRuns,
        string path,
        ICollection<PlanStructureValidationMessage> messages)
    {
        if (summary is null)
        {
            AddError(
                messages,
                "structure.wall_reconciliation.summary_missing",
                path,
                "Canonical structure requires the wall evidence reconciliation summary.");
            return;
        }

        if (!string.Equals(
                summary.ReconcilerVersion,
                GlobalWallSolutionBuilder.ReconcilerVersion,
                StringComparison.Ordinal)
            || summary.EvaluatedWallRunCount < 0
            || summary.AdjustedWallRunCount < 0
            || summary.AxisAlignedWallRunCount < 0
            || summary.ExtendedEndpointCount < 0
            || summary.TrimmedEndpointCount < 0
            || summary.JunctionSnappedEndpointCount < 0
            || summary.CollapsedDuplicateWallRunCount < 0
            || summary.CandidateSupportedWallRunCount < 0
            || summary.RoomBoundarySupportedWallRunCount < 0
            || summary.OpeningSupportedWallRunCount < 0
            || summary.NeighborSupportedWallRunCount < 0
            || summary.PreservedForReviewWallRunCount < 0
            || !IsFinite(summary.TotalAxisShiftDrawingUnits)
            || summary.TotalAxisShiftDrawingUnits < 0
            || !IsFinite(summary.MaximumAxisShiftDrawingUnits)
            || summary.MaximumAxisShiftDrawingUnits < 0)
        {
            AddError(
                messages,
                "structure.wall_reconciliation.summary_invalid",
                path,
                "Wall reconciliation version, counts, and movement totals must be valid.");
            return;
        }

        var reconciliations = wallRuns.Select(run => run.Reconciliation).ToArray();
        var expectations = new[]
        {
            ("evaluatedWallRunCount", summary.EvaluatedWallRunCount, wallRuns.Count),
            ("adjustedWallRunCount", summary.AdjustedWallRunCount, reconciliations.Count(item => string.Equals(item.Status, "Adjusted", StringComparison.Ordinal))),
            ("axisAlignedWallRunCount", summary.AxisAlignedWallRunCount, reconciliations.Count(item => item.Actions.Contains("AxisAligned", StringComparer.Ordinal))),
            ("extendedEndpointCount", summary.ExtendedEndpointCount, reconciliations.Sum(item => item.Actions.Count(action => action is "ExtendedStart" or "ExtendedEnd"))),
            ("trimmedEndpointCount", summary.TrimmedEndpointCount, reconciliations.Sum(item => item.Actions.Count(action => action is "TrimmedStart" or "TrimmedEnd"))),
            ("junctionSnappedEndpointCount", summary.JunctionSnappedEndpointCount, reconciliations.Sum(item => item.JunctionSnapCount)),
            ("collapsedDuplicateWallRunCount", summary.CollapsedDuplicateWallRunCount, reconciliations.Sum(item => item.CollapsedDuplicateRunCount)),
            ("candidateSupportedWallRunCount", summary.CandidateSupportedWallRunCount, reconciliations.Count(item => item.CandidateVoteCount > 0)),
            ("roomBoundarySupportedWallRunCount", summary.RoomBoundarySupportedWallRunCount, reconciliations.Count(item => item.RoomBoundaryVoteCount > 0)),
            ("openingSupportedWallRunCount", summary.OpeningSupportedWallRunCount, reconciliations.Count(item => item.OpeningVoteCount > 0)),
            ("neighborSupportedWallRunCount", summary.NeighborSupportedWallRunCount, reconciliations.Count(item => item.NeighborVoteCount > 0)),
            ("preservedForReviewWallRunCount", summary.PreservedForReviewWallRunCount, reconciliations.Count(item => string.Equals(item.Status, "PreservedForReview", StringComparison.Ordinal)))
        };
        foreach (var (name, actual, expected) in expectations)
        {
            if (actual != expected)
            {
                AddError(
                    messages,
                    "structure.wall_reconciliation.summary_count_mismatch",
                    $"{path}.{name}",
                    $"Reconciliation summary value {actual} does not match calculated value {expected}.");
            }
        }

        var totalAxisShift = reconciliations.Sum(item => Math.Abs(item.AxisShiftDrawingUnits));
        var maximumAxisShift = reconciliations.Length == 0
            ? 0
            : reconciliations.Max(item => Math.Abs(item.AxisShiftDrawingUnits));
        if (Math.Abs(summary.TotalAxisShiftDrawingUnits - totalAxisShift) > CoordinateTolerance
            || Math.Abs(summary.MaximumAxisShiftDrawingUnits - maximumAxisShift) > CoordinateTolerance)
        {
            AddError(
                messages,
                "structure.wall_reconciliation.summary_movement_mismatch",
                path,
                "Reconciliation movement totals must match the canonical wall run decisions.");
        }
    }

    private static void ValidateWallReconciliation(
        PlanStructureWallRunExport run,
        string path,
        ICollection<PlanStructureValidationMessage> messages)
    {
        var reconciliation = run.Reconciliation;
        var reconciliationPath = $"{path}.reconciliation";
        if (reconciliation is null)
        {
            AddError(
                messages,
                "structure.wall_reconciliation.missing",
                reconciliationPath,
                "Every canonical wall run requires an evidence reconciliation decision.");
            return;
        }

        if (!IsRatio(reconciliation.Confidence)
            || reconciliation.CandidateVoteCount < 0
            || reconciliation.RoomBoundaryVoteCount < 0
            || reconciliation.OpeningVoteCount < 0
            || reconciliation.NeighborVoteCount < 0
            || reconciliation.JunctionSnapCount < 0
            || reconciliation.CollapsedDuplicateRunCount < 0
            || !IsFinite(reconciliation.AxisShiftDrawingUnits)
            || !IsFinite(reconciliation.StartEndpointDeltaDrawingUnits)
            || !IsFinite(reconciliation.EndEndpointDeltaDrawingUnits)
            || Distance(
                reconciliation.ReconciledCenterLine.Start,
                run.CenterLine.Start) > CoordinateTolerance
            || Distance(
                reconciliation.ReconciledCenterLine.End,
                run.CenterLine.End) > CoordinateTolerance)
        {
            AddError(
                messages,
                "structure.wall_reconciliation.geometry_invalid",
                reconciliationPath,
                "Reconciliation counts, confidence, and final centerline must agree with the canonical wall run.");
            return;
        }

        var originalLength = Distance(
            reconciliation.OriginalCenterLine.Start,
            reconciliation.OriginalCenterLine.End);
        if (!IsPositiveFinite(originalLength))
        {
            AddError(
                messages,
                "structure.wall_reconciliation.original_geometry_invalid",
                $"{reconciliationPath}.originalCenterLine",
                "Reconciliation originalCenterLine must have a positive finite length.");
            return;
        }

        var originalOrientation = ReconciliationOrientation(reconciliation.OriginalCenterLine);
        var finalOrientation = ReconciliationOrientation(run.CenterLine);
        var expectedAxisShift = originalOrientation == ReconciliationWallOrientation.Diagonal
            || finalOrientation != originalOrientation
                ? 0
                : ReconciliationAxis(run.CenterLine) - ReconciliationAxis(reconciliation.OriginalCenterLine);
        var expectedStartDelta = ReconciliationIntervalStart(run.CenterLine)
            - ReconciliationIntervalStart(reconciliation.OriginalCenterLine);
        var expectedEndDelta = ReconciliationIntervalEnd(run.CenterLine)
            - ReconciliationIntervalEnd(reconciliation.OriginalCenterLine);
        if (Math.Abs(expectedAxisShift - reconciliation.AxisShiftDrawingUnits) > CoordinateTolerance
            || Math.Abs(expectedStartDelta - reconciliation.StartEndpointDeltaDrawingUnits) > CoordinateTolerance
            || Math.Abs(expectedEndDelta - reconciliation.EndEndpointDeltaDrawingUnits) > CoordinateTolerance)
        {
            AddError(
                messages,
                "structure.wall_reconciliation.delta_mismatch",
                reconciliationPath,
                "Reconciliation axis and endpoint deltas must match original and final centerlines.");
        }

        var actions = reconciliation.Actions ?? Array.Empty<string>();
        var allowedActions = new HashSet<string>(
            [
                "AxisAligned",
                "ExtendedStart",
                "TrimmedStart",
                "ExtendedEnd",
                "TrimmedEnd",
                "JunctionSnapped",
                "PreservedForReview",
                "Unchanged"
            ],
            StringComparer.Ordinal);
        if (actions.Count == 0
            || actions.Any(action => !allowedActions.Contains(action))
            || (string.Equals(reconciliation.Status, "Unchanged", StringComparison.Ordinal)
                && (actions.Count != 1 || actions[0] != "Unchanged"))
            || (string.Equals(reconciliation.Status, "Adjusted", StringComparison.Ordinal)
                && actions.All(action => action is "Unchanged" or "PreservedForReview"))
            || (string.Equals(reconciliation.Status, "PreservedForReview", StringComparison.Ordinal)
                && !actions.Contains("PreservedForReview", StringComparer.Ordinal)))
        {
            AddError(
                messages,
                "structure.wall_reconciliation.action_invalid",
                $"{reconciliationPath}.actions",
                "Reconciliation status and action codes must describe the exported geometry decision.");
        }
    }

    private static void ValidateSolverMetrics(
        PlacementWallHypothesisMetricsExport? metrics,
        string path,
        ICollection<PlanStructureValidationMessage> messages)
    {
        if (metrics is null
            || !IsRatio(metrics.MajorWallCoverageRatio)
            || !IsRatio(metrics.LongWallCoverageRatio)
            || !IsRatio(metrics.EndpointConnectivityRatio)
            || !IsRatio(metrics.RoomBoundaryClosureRatio)
            || !IsRatio(metrics.ExteriorContinuityRatio)
            || !IsRatio(metrics.DuplicateLengthRatio)
            || !IsRatio(metrics.ReviewLengthRatio)
            || !IsRatio(metrics.NoiseLengthRatio)
            || !IsRatio(metrics.AverageConfidence)
            || !IsFinite(metrics.SelectedDrawingLength)
            || metrics.SelectedDrawingLength < 0
            || metrics.UnsupportedEndpointCount < 0
            || metrics.ClosedRoomCount < 0
            || metrics.EvaluatedRoomCount < 0
            || metrics.ClosedRoomCount > metrics.EvaluatedRoomCount)
        {
            AddError(
                messages,
                "structure.wall_solver.metrics_invalid",
                path,
                "Wall-solver metrics must contain normalized ratios and non-negative counts and lengths.");
        }
    }

    private static void ValidateInlineJunctions(
        PlanStructureWallRunExport run,
        IReadOnlyDictionary<string, PlanStructureNodeExport> nodesById,
        IReadOnlySet<string> runIds,
        string path,
        ICollection<PlanStructureValidationMessage> messages)
    {
        var junctions = run.InlineJunctions
            ?? Array.Empty<PlacementSolvedWallInlineJunctionExport>();
        for (var index = 0; index < junctions.Count; index++)
        {
            var junction = junctions[index];
            var junctionPath = $"{path}.inlineJunctions[{index}]";
            if (string.IsNullOrWhiteSpace(junction.Id)
                || string.IsNullOrWhiteSpace(junction.NodeId)
                || !string.Equals(junction.WallRunId, run.Id, StringComparison.Ordinal)
                || junction.PageNumber != run.PageNumber
                || junction.Kind is not ("TJunction" or "Crossing" or "MultiJunction"))
            {
                AddError(
                    messages,
                    "structure.wall_run.inline_junction_identity_invalid",
                    junctionPath,
                    "Inline junction identity, owner, page, and kind must describe the containing canonical wall.");
                continue;
            }

            if (!nodesById.TryGetValue(junction.NodeId, out var node))
            {
                AddError(
                    messages,
                    "structure.wall_run.inline_junction_node_missing",
                    $"{junctionPath}.nodeId",
                    $"Inline junction references missing node '{junction.NodeId}'.");
            }
            else if (node.PageNumber != run.PageNumber
                     || Distance(node.Position, junction.NodePosition) > CoordinateTolerance)
            {
                AddError(
                    messages,
                    "structure.wall_run.inline_junction_node_mismatch",
                    junctionPath,
                    "Inline junction node page and position must match its structure node.");
            }

            var expectedWallPosition = PointAt(run.CenterLine, junction.Parameter);
            var expectedOffset = junction.Parameter * run.DrawingLength;
            var expectedResidual = Distance(junction.NodePosition, expectedWallPosition);
            var tolerance = Math.Max(CoordinateTolerance, run.DrawingLength * 0.0001);
            if (!IsRatio(junction.Parameter)
                || junction.Parameter <= 0
                || junction.Parameter >= 1
                || !IsFinite(junction.OffsetDrawingUnits)
                || junction.OffsetDrawingUnits < 0
                || Distance(expectedWallPosition, junction.WallPosition) > CoordinateTolerance
                || Math.Abs(expectedOffset - junction.OffsetDrawingUnits) > tolerance
                || !IsFinite(junction.ProjectionResidualDrawingUnits)
                || junction.ProjectionResidualDrawingUnits < 0
                || Math.Abs(expectedResidual - junction.ProjectionResidualDrawingUnits) > CoordinateTolerance
                || !IsRatio(junction.Confidence))
            {
                AddError(
                    messages,
                    "structure.wall_run.inline_junction_geometry_invalid",
                    junctionPath,
                    "Inline junction parameters, coordinates, residual, offset, and confidence must agree with the unsplit wall centerline.");
            }

            var incidentIds = junction.IncidentWallRunIds
                ?? Array.Empty<string>();
            if (incidentIds.Count < 2
                || !incidentIds.Contains(run.Id, StringComparer.Ordinal)
                || incidentIds.Any(id => !runIds.Contains(id))
                || incidentIds.Distinct(StringComparer.Ordinal).Count() != incidentIds.Count)
            {
                AddError(
                    messages,
                    "structure.wall_run.inline_junction_incidence_invalid",
                    $"{junctionPath}.incidentWallRunIds",
                    "Inline junctions require at least two unique, existing incident canonical wall runs including their owner.");
            }

            if (run.OpeningIntervals.Any(interval =>
                    junction.Parameter >= interval.StartParameter - 0.000001
                    && junction.Parameter <= interval.EndParameter + 0.000001))
            {
                AddError(
                    messages,
                    "structure.wall_run.inline_junction_inside_opening",
                    junctionPath,
                    "An inline structural junction cannot be attached inside a canonical opening interval.");
            }

            ValidateJunctionOptimization(
                junction.Optimization,
                $"{junctionPath}.optimization",
                messages);
        }
    }

    private static void ValidateJunctionOptimization(
        PlacementWallJunctionOptimizationExport? optimization,
        string path,
        ICollection<PlanStructureValidationMessage> messages)
    {
        if (optimization is null
            || !string.Equals(
                optimization.OptimizerVersion,
                GlobalWallSolutionBuilder.TopologyOptimizerVersion,
                StringComparison.Ordinal)
            || !string.Equals(optimization.Method, "RobustWeightedLeastSquaresHuber", StringComparison.Ordinal)
            || optimization.IterationCount < 0
            || optimization.ObservationCount < 1
            || optimization.LineConstraintCount < 1
            || !IsFinite(optimization.RootMeanSquareResidualDrawingUnits)
            || optimization.RootMeanSquareResidualDrawingUnits < 0
            || !IsFinite(optimization.MaximumResidualDrawingUnits)
            || optimization.MaximumResidualDrawingUnits < 0
            || optimization.MaximumResidualDrawingUnits + CoordinateTolerance
                < optimization.RootMeanSquareResidualDrawingUnits
            || !IsFinite(optimization.RobustObjective)
            || optimization.RobustObjective < 0)
        {
            AddError(
                messages,
                "structure.wall_topology.optimization_invalid",
                path,
                "Junction optimization version, method, counts, residuals, and objective must be valid.");
        }
    }

    private static string ExpectedTopologyKind(
        int degree,
        IReadOnlyList<string> directions,
        IReadOnlyList<string> endpointWallRunIds,
        IReadOnlyList<string> inlineWallRunIds,
        IReadOnlyList<PlacementSolvedWallInlineJunctionExport> inlineReferences)
    {
        var inlineKinds = inlineReferences
            .Select(reference => reference.Kind)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (inlineKinds.Contains("MultiJunction", StringComparer.Ordinal)
            || inlineKinds.Contains("TJunction", StringComparer.Ordinal)
            && inlineKinds.Contains("Crossing", StringComparer.Ordinal))
        {
            return "MultiJunction";
        }

        if (inlineKinds.Contains("Crossing", StringComparer.Ordinal))
        {
            return endpointWallRunIds.Count > 0 ? "MultiJunction" : "Crossing";
        }

        if (inlineKinds.Contains("TJunction", StringComparer.Ordinal)
            || inlineWallRunIds.Count > 0 && endpointWallRunIds.Count > 0)
        {
            return degree > 3 ? "MultiJunction" : "TJunction";
        }

        if (degree <= 1)
        {
            return "Endpoint";
        }

        return directions.Count <= 1 ? "Inline" : "Corner";
    }

    private static void ValidateWallIntervals(
        PlanStructureWallRunExport run,
        IReadOnlySet<string> openingIds,
        string path,
        ICollection<PlanStructureValidationMessage> messages)
    {
        var openingIntervals = run.OpeningIntervals
            ?? Array.Empty<PlacementSolvedWallOpeningIntervalExport>();
        var solidIntervals = run.SolidIntervals
            ?? Array.Empty<PlacementSolvedWallSolidIntervalExport>();
        CheckUnique(
            solidIntervals.Select(interval => interval.Id),
            $"{path}.solidIntervals",
            "structure.wall_run.solid_interval_id_duplicate",
            messages);

        for (var index = 0; index < openingIntervals.Count; index++)
        {
            var interval = openingIntervals[index];
            var intervalPath = $"{path}.openingIntervals[{index}]";
            if (!string.Equals(interval.WallRunId, run.Id, StringComparison.Ordinal)
                || interval.PageNumber != run.PageNumber)
            {
                AddError(
                    messages,
                    "structure.wall_run.opening_interval_owner_mismatch",
                    intervalPath,
                    "Opening interval wallRunId and pageNumber must match the containing wall run.");
            }

            if (!openingIds.Contains(interval.OpeningId))
            {
                AddError(
                    messages,
                    "structure.wall_run.opening_reference_missing",
                    $"{intervalPath}.openingId",
                    $"Opening interval references missing opening '{interval.OpeningId}'.");
            }

            ValidateIntervalGeometry(
                run,
                interval.StartParameter,
                interval.EndParameter,
                interval.CenterParameter,
                interval.StartOffsetDrawingUnits,
                interval.EndOffsetDrawingUnits,
                interval.CenterOffsetDrawingUnits,
                interval.LengthDrawingUnits,
                interval.CenterLine,
                intervalPath,
                "opening",
                messages);
        }

        for (var index = 0; index < solidIntervals.Count; index++)
        {
            var interval = solidIntervals[index];
            var intervalPath = $"{path}.solidIntervals[{index}]";
            if (!string.Equals(interval.WallRunId, run.Id, StringComparison.Ordinal)
                || interval.PageNumber != run.PageNumber
                || interval.Sequence != index + 1)
            {
                AddError(
                    messages,
                    "structure.wall_run.solid_interval_owner_mismatch",
                    intervalPath,
                    "Solid interval wallRunId/pageNumber must match its wall and sequence values must be contiguous.");
            }

            if (interval.BodyPolygon is null || interval.BodyPolygon.Count < 4)
            {
                AddError(
                    messages,
                    "structure.wall_run.solid_interval_body_invalid",
                    $"{intervalPath}.bodyPolygon",
                    "Solid interval bodyPolygon must contain a wall footprint.");
            }

            ValidateIntervalGeometry(
                run,
                interval.StartParameter,
                interval.EndParameter,
                interval.CenterParameter,
                interval.StartOffsetDrawingUnits,
                interval.EndOffsetDrawingUnits,
                interval.CenterOffsetDrawingUnits,
                interval.DrawingLength,
                interval.CenterLine,
                intervalPath,
                "solid",
                messages);
        }

        var openingRanges = MergeParameterRanges(
            openingIntervals.Select(interval => (interval.StartParameter, interval.EndParameter)));
        var solidRanges = solidIntervals
            .Select(interval => (interval.StartParameter, interval.EndParameter))
            .OrderBy(interval => interval.StartParameter)
            .ThenBy(interval => interval.EndParameter)
            .ToArray();
        for (var index = 1; index < solidRanges.Length; index++)
        {
            if (solidRanges[index].StartParameter < solidRanges[index - 1].EndParameter - 0.000001)
            {
                AddError(
                    messages,
                    "structure.wall_run.solid_interval_overlap",
                    $"{path}.solidIntervals",
                    "Solid wall intervals cannot overlap.");
                break;
            }
        }

        var partitions = openingRanges
            .Select(range => (range.StartParameter, range.EndParameter, Kind: "opening"))
            .Concat(solidRanges.Select(range => (range.StartParameter, range.EndParameter, Kind: "solid")))
            .OrderBy(range => range.StartParameter)
            .ThenBy(range => range.EndParameter)
            .ToArray();
        var cursor = 0.0;
        foreach (var partition in partitions)
        {
            if (partition.StartParameter > cursor + 0.000001
                || partition.StartParameter < cursor - 0.000001)
            {
                AddError(
                    messages,
                    "structure.wall_run.interval_partition_invalid",
                    path,
                    "Solid and opening intervals must form a non-overlapping partition of the logical wall.");
                break;
            }

            cursor = Math.Max(cursor, partition.EndParameter);
        }

        if (Math.Abs(cursor - 1.0) > 0.000001)
        {
            AddError(
                messages,
                "structure.wall_run.interval_coverage_incomplete",
                path,
                "Solid and opening intervals must cover the full logical wall parameter range.");
        }

        var calculatedOpeningLength = openingRanges.Sum(range =>
            (range.EndParameter - range.StartParameter) * run.DrawingLength);
        var calculatedSolidLength = solidIntervals.Sum(interval => interval.DrawingLength);
        var tolerance = Math.Max(CoordinateTolerance, run.DrawingLength * 0.0001);
        if (Math.Abs(calculatedOpeningLength - run.OpeningDrawingLength) > tolerance
            || Math.Abs(calculatedSolidLength - run.SolidDrawingLength) > tolerance
            || Math.Abs(
                run.DrawingLength
                - run.OpeningDrawingLength
                - run.SolidDrawingLength) > tolerance)
        {
            AddError(
                messages,
                "structure.wall_run.interval_length_mismatch",
                path,
                "Logical, solid, and opening wall lengths must agree with the exported interval partition.");
        }

        var reconstructedCount = openingIntervals.Count(interval =>
            string.Equals(interval.AttachmentKind, "ReconstructedGap", StringComparison.Ordinal));
        if (run.ReconstructedOpeningGapCount != reconstructedCount)
        {
            AddError(
                messages,
                "structure.wall_run.reconstructed_gap_count_mismatch",
                $"{path}.reconstructedOpeningGapCount",
                "Reconstructed opening gap count must match ReconstructedGap opening intervals.");
        }
    }

    private static void ValidateIntervalGeometry(
        PlanStructureWallRunExport run,
        double startParameter,
        double endParameter,
        double centerParameter,
        double startOffset,
        double endOffset,
        double centerOffset,
        double length,
        LineExport centerLine,
        string path,
        string kind,
        ICollection<PlanStructureValidationMessage> messages)
    {
        if (!IsRatio(startParameter)
            || !IsRatio(endParameter)
            || !IsRatio(centerParameter)
            || endParameter <= startParameter
            || !IsPositiveFinite(length))
        {
            AddError(
                messages,
                $"structure.wall_run.{kind}_interval_range_invalid",
                path,
                $"{kind} interval parameters and length must define a positive range inside the logical wall.");
            return;
        }

        var expectedStart = PointAt(run.CenterLine, startParameter);
        var expectedEnd = PointAt(run.CenterLine, endParameter);
        var expectedStartOffset = startParameter * run.DrawingLength;
        var expectedEndOffset = endParameter * run.DrawingLength;
        var expectedCenterOffset = (expectedStartOffset + expectedEndOffset) / 2.0;
        var tolerance = Math.Max(CoordinateTolerance, run.DrawingLength * 0.0001);
        if (Distance(expectedStart, centerLine.Start) > CoordinateTolerance
            || Distance(expectedEnd, centerLine.End) > CoordinateTolerance
            || Math.Abs(expectedStartOffset - startOffset) > tolerance
            || Math.Abs(expectedEndOffset - endOffset) > tolerance
            || Math.Abs(expectedCenterOffset - centerOffset) > tolerance
            || Math.Abs((startParameter + endParameter) / 2.0 - centerParameter) > 0.000001
            || Math.Abs((expectedEndOffset - expectedStartOffset) - length) > tolerance)
        {
            AddError(
                messages,
                $"structure.wall_run.{kind}_interval_geometry_mismatch",
                path,
                $"{kind} interval coordinates, offsets, parameters, and length must agree with the logical wall centerline.");
        }
    }

    private static IReadOnlyList<(double StartParameter, double EndParameter)> MergeParameterRanges(
        IEnumerable<(double StartParameter, double EndParameter)> source)
    {
        var merged = new List<(double StartParameter, double EndParameter)>();
        foreach (var range in source
                     .OrderBy(range => range.StartParameter)
                     .ThenBy(range => range.EndParameter))
        {
            if (merged.Count == 0
                || range.StartParameter > merged[^1].EndParameter + 0.000001)
            {
                merged.Add(range);
                continue;
            }

            merged[^1] = (
                merged[^1].StartParameter,
                Math.Max(merged[^1].EndParameter, range.EndParameter));
        }

        return merged;
    }

    private static void ValidateSummary(
        PlanStructureExport export,
        IReadOnlyList<PlacementPageExport> pages,
        IReadOnlyList<PlanStructureWallRunExport> wallRuns,
        IReadOnlyList<PlanStructureNodeExport> nodes,
        IReadOnlyList<PlanStructureRoomExport> rooms,
        IReadOnlyList<PlanStructureOpeningExport> openings,
        ICollection<PlanStructureValidationMessage> messages)
    {
        var expectations = new[]
        {
            ("pageCount", export.Summary.PageCount, pages.Count),
            ("wallRunCount", export.Summary.WallRunCount, wallRuns.Count),
            ("nodeCount", export.Summary.NodeCount, nodes.Count),
            ("inlineJunctionReferenceCount", export.Summary.InlineJunctionReferenceCount, wallRuns.Sum(run => run.InlineJunctions.Count)),
            ("tJunctionNodeCount", export.Summary.TJunctionNodeCount, nodes.Count(node => string.Equals(node.TopologyKind, "TJunction", StringComparison.Ordinal))),
            ("crossingNodeCount", export.Summary.CrossingNodeCount, nodes.Count(node => string.Equals(node.TopologyKind, "Crossing", StringComparison.Ordinal))),
            ("roomCount", export.Summary.RoomCount, rooms.Count),
            ("openingCount", export.Summary.OpeningCount, openings.Count),
            ("anchoredOpeningCount", export.Summary.AnchoredOpeningCount, openings.Count(opening => opening.Placement is not null)),
            ("canonicallyAnchoredOpeningCount", export.Summary.CanonicallyAnchoredOpeningCount, openings.Count(opening => opening.Placement is not null && opening.HostWallRunIds.Count > 0)),
            ("logicalOpeningHostWallRunCount", export.Summary.LogicalOpeningHostWallRunCount, wallRuns.Count(run => run.OpeningIntervals.Count > 0)),
            ("openingIntervalCount", export.Summary.OpeningIntervalCount, wallRuns.Sum(run => run.OpeningIntervals.Count)),
            ("solidIntervalCount", export.Summary.SolidIntervalCount, wallRuns.Sum(run => run.SolidIntervals.Count))
        };
        foreach (var (name, actual, expected) in expectations)
        {
            if (actual != expected)
            {
                AddError(
                    messages,
                    "structure.summary.count_mismatch",
                    $"$.summary.{name}",
                    $"Summary value {actual} does not match calculated value {expected}.");
            }
        }

        var totalLength = wallRuns.Sum(run => run.DrawingLength);
        if (Math.Abs(totalLength - export.Summary.TotalWallLengthDrawingUnits)
            > Math.Max(CoordinateTolerance, totalLength * 0.0001))
        {
            AddError(
                messages,
                "structure.summary.length_mismatch",
                "$.summary.totalWallLengthDrawingUnits",
                "Summary wall length does not match canonical wall runs.");
        }

        var totalSolidLength = wallRuns.Sum(run => run.SolidDrawingLength);
        if (Math.Abs(totalSolidLength - export.Summary.TotalSolidWallLengthDrawingUnits)
            > Math.Max(CoordinateTolerance, totalSolidLength * 0.0001))
        {
            AddError(
                messages,
                "structure.summary.solid_length_mismatch",
                "$.summary.totalSolidWallLengthDrawingUnits",
                "Summary solid wall length does not match canonical wall intervals.");
        }

        var totalOpeningLength = wallRuns.Sum(run => run.OpeningDrawingLength);
        if (Math.Abs(totalOpeningLength - export.Summary.TotalOpeningLengthDrawingUnits)
            > Math.Max(CoordinateTolerance, totalOpeningLength * 0.0001))
        {
            AddError(
                messages,
                "structure.summary.opening_length_mismatch",
                "$.summary.totalOpeningLengthDrawingUnits",
                "Summary opening length does not match canonical wall intervals.");
        }
    }

    private static void ValidateNodeReference(
        string nodeId,
        int pageNumber,
        PointExport endpoint,
        IReadOnlyDictionary<string, PlanStructureNodeExport> nodesById,
        string path,
        ICollection<PlanStructureValidationMessage> messages)
    {
        if (!nodesById.TryGetValue(nodeId, out var node))
        {
            AddError(
                messages,
                "structure.wall_run.node_reference_missing",
                path,
                $"Wall run references missing node '{nodeId}'.");
            return;
        }

        if (node.PageNumber != pageNumber)
        {
            AddError(
                messages,
                "structure.wall_run.node_page_mismatch",
                path,
                $"Wall run page {pageNumber} does not match node page {node.PageNumber}.");
        }

        if (Distance(endpoint, node.Position) > CoordinateTolerance)
        {
            AddError(
                messages,
                "structure.wall_run.node_coordinate_mismatch",
                path,
                $"Wall run endpoint is not coincident with node '{nodeId}'.");
        }
    }

    private static void ValidatePageReference(
        int pageNumber,
        IReadOnlySet<int> pageNumbers,
        string path,
        ICollection<PlanStructureValidationMessage> messages)
    {
        if (!pageNumbers.Contains(pageNumber))
        {
            AddError(
                messages,
                "structure.page_reference.missing",
                $"{path}.pageNumber",
                $"Page {pageNumber} is not present in pages.");
        }
    }

    private static void CheckUnique(
        IEnumerable<string> ids,
        string path,
        string code,
        ICollection<PlanStructureValidationMessage> messages)
    {
        foreach (var duplicate in ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key))
        {
            AddError(messages, code, path, $"Duplicate id '{duplicate}'.");
        }
    }

    private static double Distance(PointExport left, PointExport right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static PointExport PointAt(LineExport line, double parameter) =>
        new(
            line.Start.X + (line.End.X - line.Start.X) * parameter,
            line.Start.Y + (line.End.Y - line.Start.Y) * parameter);

    private static bool IsPositiveFinite(double value) => IsFinite(value) && value > 0;

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool IsRatio(double value) => IsFinite(value) && value is >= 0 and <= 1;

    private static ReconciliationWallOrientation ReconciliationOrientation(LineExport line)
    {
        var dx = Math.Abs(line.End.X - line.Start.X);
        var dy = Math.Abs(line.End.Y - line.Start.Y);
        if (dy <= Math.Max(1.0, dx * 0.04))
        {
            return ReconciliationWallOrientation.Horizontal;
        }

        if (dx <= Math.Max(1.0, dy * 0.04))
        {
            return ReconciliationWallOrientation.Vertical;
        }

        return ReconciliationWallOrientation.Diagonal;
    }

    private static double ReconciliationAxis(LineExport line) =>
        ReconciliationOrientation(line) == ReconciliationWallOrientation.Horizontal
            ? (line.Start.Y + line.End.Y) / 2.0
            : (line.Start.X + line.End.X) / 2.0;

    private static double ReconciliationIntervalStart(LineExport line) =>
        ReconciliationOrientation(line) == ReconciliationWallOrientation.Horizontal
            ? Math.Min(line.Start.X, line.End.X)
            : Math.Min(line.Start.Y, line.End.Y);

    private static double ReconciliationIntervalEnd(LineExport line) =>
        ReconciliationOrientation(line) == ReconciliationWallOrientation.Horizontal
            ? Math.Max(line.Start.X, line.End.X)
            : Math.Max(line.Start.Y, line.End.Y);

    private enum ReconciliationWallOrientation
    {
        Horizontal = 0,
        Vertical,
        Diagonal
    }

    private static void AddError(
        ICollection<PlanStructureValidationMessage> messages,
        string code,
        string path,
        string message) =>
        messages.Add(new PlanStructureValidationMessage("Error", code, path, message));
}
