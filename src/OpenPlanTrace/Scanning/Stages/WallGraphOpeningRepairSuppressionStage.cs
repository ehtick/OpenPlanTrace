namespace OpenPlanTrace;

internal sealed class WallGraphOpeningRepairSuppressionStage : IPipelineStage
{
    public string Name => "wall-graph-opening-repair-suppression";

    public ValueTask ExecuteAsync(ScanContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.WallGraph.RepairCandidates.Count == 0 || context.Openings.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        var suppressed = context.WallGraph.RepairCandidates
            .Where(candidate => candidate.Kind is WallGraphRepairCandidateKind.EndpointToWall or WallGraphRepairCandidateKind.EndpointToEndpoint)
            .Where(candidate => IsSuppressedByOpening(candidate, context.Openings, context.Options))
            .ToArray();
        if (suppressed.Length == 0)
        {
            return ValueTask.CompletedTask;
        }

        var suppressedIds = suppressed.Select(candidate => candidate.Id).ToHashSet(StringComparer.Ordinal);
        var retained = context.WallGraph.RepairCandidates
            .Where(candidate => !suppressedIds.Contains(candidate.Id))
            .ToArray();
        context.WallGraph = context.WallGraph with { RepairCandidates = retained };

        var hasRemainingEndpointGapCandidates = retained.Any(candidate =>
            candidate.Kind is WallGraphRepairCandidateKind.EndpointToWall or WallGraphRepairCandidateKind.EndpointToEndpoint);
        var removedDiagnosticCount = context.Diagnostics.RemoveWhere(diagnostic =>
            (string.Equals(diagnostic.Code, "wall_graph.endpoint_gap.review", StringComparison.Ordinal)
                && diagnostic.Properties.TryGetValue("repairCandidateId", out var repairCandidateId)
                && suppressedIds.Contains(repairCandidateId))
            || (string.Equals(diagnostic.Code, "wall_graph.endpoint_gaps.detected", StringComparison.Ordinal)
                && !hasRemainingEndpointGapCandidates));

        context.AddDiagnostic(
            "wall_graph.endpoint_gap.opening_suppressed",
            DiagnosticSeverity.Info,
            Name,
            "Endpoint gap repair candidates crossing detected door/window openings were suppressed.",
            confidence: Confidence.Medium,
            scope: DiagnosticScope.Detection,
            sourcePrimitiveIds: suppressed.SelectMany(candidate => candidate.SourcePrimitiveIds),
            properties: new Dictionary<string, string>
            {
                ["suppressedEndpointGapCandidateCount"] = suppressed.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["removedDiagnosticCount"] = removedDiagnosticCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["repairCandidateIds"] = string.Join(",", suppressedIds.Order(StringComparer.Ordinal).Take(20))
            });

        return ValueTask.CompletedTask;
    }

    private static bool IsSuppressedByOpening(
        WallGraphRepairCandidate candidate,
        IReadOnlyList<OpeningCandidate> openings,
        ScannerOptions options)
    {
        var wallIds = candidate.WallIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(candidate.HostWallId))
        {
            wallIds.Add(candidate.HostWallId);
        }

        if (wallIds.Count == 0)
        {
            return false;
        }

        var tolerance = Math.Max(options.WallSnapTolerance * 3.0, options.DefaultWallThickness * 2.0);
        var candidateBounds = candidate.Bounds.Inflate(tolerance);
        return openings.Any(opening =>
            opening.PageNumber == candidate.PageNumber
            && opening.Type is OpeningType.Door or OpeningType.Window or OpeningType.GenericOpening
            && opening.Confidence.Value >= 0.55
            && candidateBounds.Intersects(opening.Bounds, tolerance)
            && OpeningReferencesAnyWall(opening, wallIds));
    }

    private static bool OpeningReferencesAnyWall(
        OpeningCandidate opening,
        IReadOnlySet<string> wallIds) =>
        (!string.IsNullOrWhiteSpace(opening.WallId) && wallIds.Contains(opening.WallId))
        || opening.HostWallIds.Any(wallIds.Contains)
        || opening.AdjacentWallIds.Any(wallIds.Contains)
        || (!string.IsNullOrWhiteSpace(opening.Placement?.HostWallId) && wallIds.Contains(opening.Placement.HostWallId))
        || (opening.Placement?.AnchorWallIds.Any(wallIds.Contains) ?? false)
        || opening.Evidence.Any(item => wallIds.Any(wallId => item.Contains(wallId, StringComparison.OrdinalIgnoreCase)));
}
