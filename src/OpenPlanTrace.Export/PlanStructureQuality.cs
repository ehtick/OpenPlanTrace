namespace OpenPlanTrace.Export;

public sealed record PlanStructureQualityExport(
    double IntegrityScore,
    string Grade,
    bool ReadyForWallImport,
    bool ReadyForCoordinateImport,
    bool ReadyForMetricImport,
    bool RequiresReview,
    bool SourcePlacementReadyForCoordinateImport,
    bool SourcePlacementReadyForMetricImport,
    int InfoIssueCount,
    int WarningIssueCount,
    int ErrorIssueCount,
    IReadOnlyList<string> Evidence);

public sealed record PlanStructureSummaryExport(
    int PageCount,
    int WallRunCount,
    int ExteriorWallRunCount,
    int InteriorWallRunCount,
    int MixedWallRunCount,
    int UnknownWallRunCount,
    int NodeCount,
    int EndpointNodeCount,
    int JunctionNodeCount,
    int InlineJunctionReferenceCount,
    int TJunctionNodeCount,
    int CrossingNodeCount,
    int ConnectedComponentCount,
    int RoomCount,
    int OpeningCount,
    int AnchoredOpeningCount,
    int CanonicallyAnchoredOpeningCount,
    int LogicalOpeningHostWallRunCount,
    int OpeningIntervalCount,
    int SolidIntervalCount,
    double TotalWallLengthDrawingUnits,
    double? TotalWallLengthMeters,
    double TotalSolidWallLengthDrawingUnits,
    double? TotalSolidWallLengthMeters,
    double TotalOpeningLengthDrawingUnits,
    double SourceWallLengthRepresentationRatio,
    double AxisAlignedWallLengthRatio,
    double CoordinateReadyWallLengthRatio,
    double MetricReadyWallLengthRatio,
    double LengthWeightedWallConfidence,
    double AnchoredOpeningRatio,
    int DuplicateWallRunPairCount,
    double DuplicateWallRunLengthRatio,
    int ResidualEndpointOnHostCount,
    IReadOnlyList<PlanStructurePageSummaryExport> PageSummaries);

public sealed record PlanStructurePageSummaryExport(
    int PageNumber,
    int WallRunCount,
    int ExteriorWallRunCount,
    int InteriorWallRunCount,
    int NodeCount,
    int RoomCount,
    int OpeningCount,
    int CanonicallyAnchoredOpeningCount,
    int LogicalOpeningHostWallRunCount,
    int OpeningIntervalCount,
    int SolidIntervalCount,
    double WallLengthDrawingUnits,
    double? WallLengthMeters,
    double SolidWallLengthDrawingUnits,
    double? SolidWallLengthMeters,
    double OpeningLengthDrawingUnits);

public sealed record PlanStructureIssueExport(
    string Code,
    string Severity,
    string Message,
    int? PageNumber,
    IReadOnlyList<string> EntityIds,
    string RecommendedAction,
    IReadOnlyList<string> Evidence);

internal sealed record PlanStructureAssessment(
    PlanStructureQualityExport Quality,
    PlanStructureSummaryExport Summary,
    IReadOnlyList<PlanStructureIssueExport> Issues)
{
    public static PlanStructureAssessment Evaluate(
        PlanPlacementExport placement,
        IReadOnlyList<PlanStructureWallRunExport> wallRuns,
        IReadOnlyList<PlanStructureNodeExport> nodes,
        IReadOnlyList<PlanStructureRoomExport> rooms,
        IReadOnlyList<PlanStructureOpeningExport> openings)
    {
        var totalLength = wallRuns.Sum(run => run.DrawingLength);
        var totalLengthMeters = wallRuns.Count > 0 && wallRuns.All(run => run.LengthMeters is not null)
            ? wallRuns.Sum(run => run.LengthMeters!.Value)
            : (double?)null;
        var totalSolidLength = wallRuns.Sum(run => run.SolidDrawingLength);
        var totalSolidLengthMeters = wallRuns.Count > 0
            && wallRuns.All(run => run.SolidLengthMeters is not null)
                ? wallRuns.Sum(run => run.SolidLengthMeters!.Value)
                : (double?)null;
        var totalOpeningLength = wallRuns.Sum(run => run.OpeningDrawingLength);
        var sourceRepresentationRatio = SourceWallLengthRepresentationRatio(placement, wallRuns);
        var axisAlignedRatio = LengthRatio(wallRuns, IsAxisAligned);
        var coordinateReadyRatio = LengthRatio(wallRuns, run => run.Reliability.ReadyForCoordinatePlacement);
        var metricReadyRatio = LengthRatio(wallRuns, run => run.Reliability.ReadyForMetricPlacement);
        var weightedConfidence = totalLength > 0
            ? wallRuns.Sum(run => run.DrawingLength * run.Confidence) / totalLength
            : 0;
        var anchoredOpeningCount = openings.Count(opening => opening.Placement is not null);
        var canonicallyAnchoredOpeningCount = openings.Count(opening =>
            opening.Placement is not null
            && opening.HostWallRunIds.Count > 0);
        var anchoredOpeningRatio = anchoredOpeningCount == 0
            ? 1
            : (double)canonicallyAnchoredOpeningCount / anchoredOpeningCount;
        var duplicateSummary = FindDuplicateWallRuns(wallRuns, totalLength);
        var residualEndpointCount = placement.WallGraph.ResidualEndpointOnHostCandidates.Count;
        var connectedComponentCount = CountConnectedComponents(wallRuns);
        var pageSummaries = placement.Pages
            .Select(page => BuildPageSummary(page.PageNumber, wallRuns, nodes, rooms, openings))
            .ToArray();

        var summary = new PlanStructureSummaryExport(
            placement.Pages.Count,
            wallRuns.Count,
            CountType(wallRuns, "Exterior"),
            CountType(wallRuns, "Interior"),
            CountType(wallRuns, "Mixed"),
            CountType(wallRuns, "Unknown"),
            nodes.Count,
            nodes.Count(node => node.Degree <= 1),
            nodes.Count(node => node.Degree >= 3),
            wallRuns.Sum(run => run.InlineJunctions.Count),
            nodes.Count(node => string.Equals(node.TopologyKind, "TJunction", StringComparison.Ordinal)),
            nodes.Count(node => string.Equals(node.TopologyKind, "Crossing", StringComparison.Ordinal)),
            connectedComponentCount,
            rooms.Count,
            openings.Count,
            anchoredOpeningCount,
            canonicallyAnchoredOpeningCount,
            wallRuns.Count(run => run.OpeningIntervals.Count > 0),
            wallRuns.Sum(run => run.OpeningIntervals.Count),
            wallRuns.Sum(run => run.SolidIntervals.Count),
            totalLength,
            totalLengthMeters,
            totalSolidLength,
            totalSolidLengthMeters,
            totalOpeningLength,
            sourceRepresentationRatio,
            axisAlignedRatio,
            coordinateReadyRatio,
            metricReadyRatio,
            weightedConfidence,
            anchoredOpeningRatio,
            duplicateSummary.PairCount,
            duplicateSummary.LengthRatio,
            residualEndpointCount,
            pageSummaries);
        var issues = BuildIssues(placement, wallRuns, openings, summary);
        var score = IntegrityScore(summary);
        var grade = score >= 0.90
            ? "Strong"
            : score >= 0.75
                ? "Usable"
                : score >= 0.55
                    ? "ReviewRequired"
                    : "Poor";
        var readyForWallImport = wallRuns.Count > 0
            && score >= 0.75
            && coordinateReadyRatio >= 0.85
            && sourceRepresentationRatio >= 0.75
            && duplicateSummary.LengthRatio <= 0.05
            && residualEndpointCount <= Math.Max(1, wallRuns.Count / 100);
        var readyForCoordinateImport = readyForWallImport
            && placement.QualityGate.ReadyForCoordinatePlacement;
        var readyForMetricImport = readyForCoordinateImport
            && placement.QualityGate.ReadyForMetricPlacement
            && metricReadyRatio >= 0.85;
        var errorCount = issues.Count(issue => string.Equals(issue.Severity, "Error", StringComparison.Ordinal));
        var warningCount = issues.Count(issue => string.Equals(issue.Severity, "Warning", StringComparison.Ordinal));
        var infoCount = issues.Count - errorCount - warningCount;
        var quality = new PlanStructureQualityExport(
            score,
            grade,
            readyForWallImport,
            readyForCoordinateImport,
            readyForMetricImport,
            !readyForCoordinateImport || warningCount > 0 || errorCount > 0,
            placement.QualityGate.ReadyForCoordinatePlacement,
            placement.QualityGate.ReadyForMetricPlacement,
            infoCount,
            warningCount,
            errorCount,
            new[]
            {
                "Integrity score measures canonical graph consistency and source-evidence retention; it is not a ground-truth wall accuracy score.",
                $"Canonical graph represents {sourceRepresentationRatio:P1} of reliability-tracked source wall length.",
                $"Canonical wall runs are {coordinateReadyRatio:P1} coordinate-ready and {metricReadyRatio:P1} metric-ready by length.",
                $"Canonical walls expose {wallRuns.Sum(run => run.OpeningIntervals.Count)} opening interval(s) and {wallRuns.Sum(run => run.SolidIntervals.Count)} solid interval(s).",
                $"Canonical graph contains {duplicateSummary.PairCount} near-duplicate run pair(s) and {residualEndpointCount} residual endpoint-on-host candidate(s).",
                $"Canonical graph retains {wallRuns.Sum(run => run.InlineJunctions.Count)} inline junction reference(s) without splitting long wall runs.",
                $"Source placement gate: coordinate={placement.QualityGate.ReadyForCoordinatePlacement}, metric={placement.QualityGate.ReadyForMetricPlacement}."
            });

        return new PlanStructureAssessment(quality, summary, issues);
    }

    private static IReadOnlyList<PlanStructureIssueExport> BuildIssues(
        PlanPlacementExport placement,
        IReadOnlyList<PlanStructureWallRunExport> wallRuns,
        IReadOnlyList<PlanStructureOpeningExport> openings,
        PlanStructureSummaryExport summary)
    {
        var issues = new List<PlanStructureIssueExport>();
        if (wallRuns.Count == 0)
        {
            issues.Add(new PlanStructureIssueExport(
                "structure.wall_graph.empty",
                "Error",
                "No canonical structural wall runs were produced.",
                null,
                Array.Empty<string>(),
                "Review source loading, wall evidence, and structural wall recovery before import.",
                Array.Empty<string>()));
        }

        if (summary.SourceWallLengthRepresentationRatio < 0.75)
        {
            issues.Add(new PlanStructureIssueExport(
                "structure.wall_graph.low_source_representation",
                "Warning",
                $"Canonical runs represent only {summary.SourceWallLengthRepresentationRatio:P1} of reliability-tracked source wall length.",
                null,
                wallRuns.SelectMany(run => run.SourceWallIds).Distinct(StringComparer.Ordinal).ToArray(),
                "Review omitted long walls and verify wall-only overlays against the source plan.",
                placement.WallSets.Evidence));
        }

        if (summary.DuplicateWallRunPairCount > 0)
        {
            issues.Add(new PlanStructureIssueExport(
                "structure.wall_graph.duplicate_runs",
                summary.DuplicateWallRunLengthRatio > 0.05 ? "Warning" : "Info",
                $"Canonical graph retains {summary.DuplicateWallRunPairCount} near-coincident overlapping wall run pair(s).",
                null,
                Array.Empty<string>(),
                "Review the listed overlap metric and collapse true duplicate centerlines before downstream import.",
                new[] { $"duplicate wall run length ratio {summary.DuplicateWallRunLengthRatio:0.###}" }));
        }

        if (summary.ResidualEndpointOnHostCount > 0)
        {
            issues.Add(new PlanStructureIssueExport(
                "structure.wall_graph.residual_endpoint_on_host",
                "Warning",
                $"Canonical graph retains {summary.ResidualEndpointOnHostCount} endpoint-on-host candidate(s).",
                null,
                placement.WallGraph.ResidualEndpointOnHostCandidates
                    .SelectMany(candidate => new[] { candidate.EndpointEdgeId, candidate.HostEdgeId })
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                "Review whether each candidate should snap, split a host wall, or remain an intentional opening boundary.",
                placement.WallGraph.ResidualEndpointOnHostCandidates
                    .SelectMany(candidate => candidate.Evidence)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()));
        }

        var mixedRuns = wallRuns
            .Where(run => string.Equals(run.WallType, "Mixed", StringComparison.Ordinal))
            .Select(run => run.Id)
            .ToArray();
        if (mixedRuns.Length > 0)
        {
            issues.Add(new PlanStructureIssueExport(
                "structure.wall_graph.mixed_wall_type",
                "Warning",
                $"{mixedRuns.Length} canonical run(s) merge conflicting exterior and interior classifications.",
                null,
                mixedRuns,
                "Review wall type classification before using exterior/interior semantics downstream.",
                Array.Empty<string>()));
        }

        var missingCanonicalHosts = openings
            .Where(opening => opening.Placement is not null && opening.HostWallRunIds.Count == 0)
            .Select(opening => opening.Id)
            .ToArray();
        if (missingCanonicalHosts.Length > 0)
        {
            issues.Add(new PlanStructureIssueExport(
                "structure.opening.canonical_host_missing",
                "Warning",
                $"{missingCanonicalHosts.Length} anchored opening(s) do not map to a canonical wall run.",
                null,
                missingCanonicalHosts,
                "Review source wall provenance and opening host anchors before placing openings.",
                Array.Empty<string>()));
        }

        if (!placement.QualityGate.ReadyForCoordinatePlacement)
        {
            issues.Add(new PlanStructureIssueExport(
                "structure.source_placement_gate.blocked",
                "Warning",
                "The source placement export is not ready for coordinate placement.",
                null,
                Array.Empty<string>(),
                "Resolve placement blocking issues and review the wall-only overlay before import.",
                placement.QualityGate.Evidence));
        }

        return issues;
    }

    private static double IntegrityScore(PlanStructureSummaryExport summary)
    {
        if (summary.WallRunCount == 0)
        {
            return 0;
        }

        var duplicateCleanliness = 1 - Math.Clamp(summary.DuplicateWallRunLengthRatio * 4, 0, 1);
        var residualRatio = (double)summary.ResidualEndpointOnHostCount / Math.Max(1, summary.NodeCount);
        var endpointCleanliness = 1 - Math.Clamp(residualRatio * 4, 0, 1);
        var axisContribution = 0.75 + (0.25 * summary.AxisAlignedWallLengthRatio);

        return Math.Clamp(
            (summary.SourceWallLengthRepresentationRatio * 0.35)
            + (duplicateCleanliness * 0.20)
            + (endpointCleanliness * 0.15)
            + (summary.CoordinateReadyWallLengthRatio * 0.10)
            + (summary.AnchoredOpeningRatio * 0.10)
            + (summary.LengthWeightedWallConfidence * 0.05)
            + (axisContribution * 0.05),
            0,
            1);
    }

    private static double SourceWallLengthRepresentationRatio(
        PlanPlacementExport placement,
        IReadOnlyList<PlanStructureWallRunExport> wallRuns)
    {
        var trackedIds = placement.WallSets.ReliabilityTrackedWallIds.ToHashSet(StringComparer.Ordinal);
        var trackedWalls = placement.Walls
            .Where(wall => trackedIds.Contains(wall.Id) && wall.DrawingLength > 0)
            .ToArray();
        if (trackedWalls.Length == 0)
        {
            return wallRuns.Count > 0 ? 1 : 0;
        }

        var representedIds = wallRuns
            .SelectMany(run => run.SourceWallIds)
            .ToHashSet(StringComparer.Ordinal);
        var total = trackedWalls.Sum(wall => wall.DrawingLength);
        var represented = trackedWalls
            .Where(wall => representedIds.Contains(wall.Id))
            .Sum(wall => wall.DrawingLength);
        return total > 0 ? Math.Clamp(represented / total, 0, 1) : 0;
    }

    private static double LengthRatio(
        IReadOnlyList<PlanStructureWallRunExport> wallRuns,
        Func<PlanStructureWallRunExport, bool> predicate)
    {
        var total = wallRuns.Sum(run => run.DrawingLength);
        return total > 0
            ? Math.Clamp(wallRuns.Where(predicate).Sum(run => run.DrawingLength) / total, 0, 1)
            : 0;
    }

    private static bool IsAxisAligned(PlanStructureWallRunExport run)
    {
        var dx = Math.Abs(run.CenterLine.End.X - run.CenterLine.Start.X);
        var dy = Math.Abs(run.CenterLine.End.Y - run.CenterLine.Start.Y);
        return Math.Min(dx, dy) <= Math.Max(0.5, run.DrawingLength * 0.01);
    }

    private static PlanStructureDuplicateSummary FindDuplicateWallRuns(
        IReadOnlyList<PlanStructureWallRunExport> wallRuns,
        double totalLength)
    {
        var spans = wallRuns
            .Select(TryCreateAxisSpan)
            .Where(span => span is not null)
            .Select(span => span!)
            .ToArray();
        var pairCount = 0;
        var duplicateLength = 0d;

        for (var leftIndex = 0; leftIndex < spans.Length; leftIndex++)
        {
            var left = spans[leftIndex];
            for (var rightIndex = leftIndex + 1; rightIndex < spans.Length; rightIndex++)
            {
                var right = spans[rightIndex];
                if (left.PageNumber != right.PageNumber || left.Horizontal != right.Horizontal)
                {
                    continue;
                }

                var axisTolerance = Math.Clamp(
                    Math.Min(left.Thickness, right.Thickness) * 0.25,
                    0.75,
                    1.5);
                if (Math.Abs(left.Axis - right.Axis) > axisTolerance)
                {
                    continue;
                }

                var overlap = Math.Max(0, Math.Min(left.End, right.End) - Math.Max(left.Start, right.Start));
                var minLength = Math.Min(left.End - left.Start, right.End - right.Start);
                if (minLength <= 0 || overlap / minLength < 0.80)
                {
                    continue;
                }

                pairCount++;
                duplicateLength += overlap;
            }
        }

        return new PlanStructureDuplicateSummary(
            pairCount,
            totalLength > 0 ? Math.Clamp(duplicateLength / totalLength, 0, 1) : 0);
    }

    private static PlanStructureAxisSpan? TryCreateAxisSpan(PlanStructureWallRunExport run)
    {
        var dx = Math.Abs(run.CenterLine.End.X - run.CenterLine.Start.X);
        var dy = Math.Abs(run.CenterLine.End.Y - run.CenterLine.Start.Y);
        if (dy <= Math.Max(0.5, dx * 0.01))
        {
            return new PlanStructureAxisSpan(
                run.PageNumber,
                true,
                (run.CenterLine.Start.Y + run.CenterLine.End.Y) / 2,
                Math.Min(run.CenterLine.Start.X, run.CenterLine.End.X),
                Math.Max(run.CenterLine.Start.X, run.CenterLine.End.X),
                Math.Max(run.ThicknessDrawingUnits, 1));
        }

        if (dx <= Math.Max(0.5, dy * 0.01))
        {
            return new PlanStructureAxisSpan(
                run.PageNumber,
                false,
                (run.CenterLine.Start.X + run.CenterLine.End.X) / 2,
                Math.Min(run.CenterLine.Start.Y, run.CenterLine.End.Y),
                Math.Max(run.CenterLine.Start.Y, run.CenterLine.End.Y),
                Math.Max(run.ThicknessDrawingUnits, 1));
        }

        return null;
    }

    private static int CountConnectedComponents(IReadOnlyList<PlanStructureWallRunExport> wallRuns)
    {
        if (wallRuns.Count == 0)
        {
            return 0;
        }

        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var run in wallRuns)
        {
            var runNodeIds = new[] { run.FromNodeId, run.ToNodeId }
                .Concat(run.InlineJunctions.Select(junction => junction.NodeId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            foreach (var nodeId in runNodeIds)
            {
                Ensure(nodeId);
            }

            for (var index = 1; index < runNodeIds.Length; index++)
            {
                Union(runNodeIds[0], runNodeIds[index]);
            }
        }

        return parents.Keys
            .Select(Find)
            .Distinct(StringComparer.Ordinal)
            .Count();

        void Ensure(string nodeId)
        {
            if (!parents.ContainsKey(nodeId))
            {
                parents[nodeId] = nodeId;
            }
        }

        string Find(string nodeId)
        {
            var parent = parents[nodeId];
            if (string.Equals(parent, nodeId, StringComparison.Ordinal))
            {
                return parent;
            }

            parents[nodeId] = Find(parent);
            return parents[nodeId];
        }

        void Union(string left, string right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (!string.Equals(leftRoot, rightRoot, StringComparison.Ordinal))
            {
                parents[rightRoot] = leftRoot;
            }
        }
    }

    private static int CountType(
        IEnumerable<PlanStructureWallRunExport> wallRuns,
        string wallType) =>
        wallRuns.Count(run => string.Equals(run.WallType, wallType, StringComparison.Ordinal));

    private static PlanStructurePageSummaryExport BuildPageSummary(
        int pageNumber,
        IReadOnlyList<PlanStructureWallRunExport> wallRuns,
        IReadOnlyList<PlanStructureNodeExport> nodes,
        IReadOnlyList<PlanStructureRoomExport> rooms,
        IReadOnlyList<PlanStructureOpeningExport> openings)
    {
        var pageRuns = wallRuns.Where(run => run.PageNumber == pageNumber).ToArray();
        var pageOpenings = openings.Where(opening => opening.PageNumber == pageNumber).ToArray();
        return new PlanStructurePageSummaryExport(
            pageNumber,
            pageRuns.Length,
            CountType(pageRuns, "Exterior"),
            CountType(pageRuns, "Interior"),
            nodes.Count(node => node.PageNumber == pageNumber),
            rooms.Count(room => room.PageNumber == pageNumber),
            pageOpenings.Length,
            pageOpenings.Count(opening => opening.Placement is not null && opening.HostWallRunIds.Count > 0),
            pageRuns.Count(run => run.OpeningIntervals.Count > 0),
            pageRuns.Sum(run => run.OpeningIntervals.Count),
            pageRuns.Sum(run => run.SolidIntervals.Count),
            pageRuns.Sum(run => run.DrawingLength),
            pageRuns.Length > 0 && pageRuns.All(run => run.LengthMeters is not null)
                ? pageRuns.Sum(run => run.LengthMeters!.Value)
                : null,
            pageRuns.Sum(run => run.SolidDrawingLength),
            pageRuns.Length > 0 && pageRuns.All(run => run.SolidLengthMeters is not null)
                ? pageRuns.Sum(run => run.SolidLengthMeters!.Value)
                : null,
            pageRuns.Sum(run => run.OpeningDrawingLength));
    }

    private sealed record PlanStructureAxisSpan(
        int PageNumber,
        bool Horizontal,
        double Axis,
        double Start,
        double End,
        double Thickness);

    private sealed record PlanStructureDuplicateSummary(int PairCount, double LengthRatio);
}
