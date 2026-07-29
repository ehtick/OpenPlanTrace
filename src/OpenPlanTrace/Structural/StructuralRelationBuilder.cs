namespace OpenPlanTrace;

internal static class StructuralRelationBuilder
{
    public static IReadOnlyList<StructuralEvidenceRelation> Build(
        IReadOnlyList<StructuralWallCandidate> candidates,
        StructuralSolverOptions options)
    {
        var relations = new Dictionary<string, StructuralEvidenceRelation>(StringComparer.Ordinal);
        var angleTolerance = options.AngleToleranceDegrees * Math.PI / 180.0;

        foreach (var orientationGroup in candidates
                     .Where(candidate => candidate.IsEligible)
                     .GroupBy(candidate => (
                         candidate.PageNumber,
                         Bucket: StructuralGeometry.OrientationBucket(candidate.CenterLine, angleTolerance))))
        {
            var ordered = orientationGroup
                .OrderBy(candidate => StructuralGeometry.AxisCoordinate(candidate.CenterLine))
                .ThenBy(candidate => candidate.Bounds.X)
                .ThenBy(candidate => candidate.Bounds.Y)
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                .ToArray();

            for (var firstIndex = 0; firstIndex < ordered.Length; firstIndex++)
            {
                var first = ordered[firstIndex];
                var firstAxis = StructuralGeometry.AxisCoordinate(first.CenterLine);
                for (var secondIndex = firstIndex + 1; secondIndex < ordered.Length; secondIndex++)
                {
                    var second = ordered[secondIndex];
                    var secondAxis = StructuralGeometry.AxisCoordinate(second.CenterLine);
                    if (Math.Abs(secondAxis - firstAxis) > options.AxisTolerance * 1.75)
                    {
                        break;
                    }

                    if (!StructuralGeometry.AreParallel(first.CenterLine, second.CenterLine, angleTolerance))
                    {
                        continue;
                    }

                    var axisDistance = StructuralGeometry.PerpendicularDistance(first.CenterLine, second.CenterLine);
                    if (axisDistance > options.AxisTolerance)
                    {
                        continue;
                    }

                    var overlapLength = StructuralGeometry.OverlapLength(
                        first.CenterLine,
                        second.CenterLine);
                    var overlapRatio = overlapLength
                        / Math.Max(1e-9, Math.Min(first.DrawingLength, second.DrawingLength));
                    var bidirectionalOverlapRatio = overlapLength
                        / Math.Max(1e-9, Math.Max(first.DrawingLength, second.DrawingLength));
                    var gap = StructuralGeometry.ProjectedGap(first.CenterLine, second.CenterLine);
                    if (overlapRatio >= options.DuplicateOverlapRatio)
                    {
                        if (IsSourceBackedShellExtension(
                                first,
                                second,
                                bidirectionalOverlapRatio))
                        {
                            Add(
                                relations,
                                Relation(
                                    StructuralEvidenceRelationKind.Continuation,
                                    first,
                                    second,
                                    weight: 0.14
                                        + (0.08 * (1 - bidirectionalOverlapRatio)),
                                    hard: false,
                                    "source-backed exterior shell extends shorter collinear wall evidence",
                                    $"short-span overlap ratio {overlapRatio:0.###}",
                                    $"long-span overlap ratio {bidirectionalOverlapRatio:0.###}",
                                    $"axis distance {axisDistance:0.###}"));
                        }
                        else
                        {
                            Add(
                                relations,
                                Relation(
                                    StructuralEvidenceRelationKind.Duplicate,
                                    first,
                                    second,
                                    weight: -(0.95 + (overlapRatio * 0.40)),
                                    hard: overlapRatio >= 0.92
                                        && bidirectionalOverlapRatio >= 0.72,
                                    $"collinear overlap ratio {overlapRatio:0.###}",
                                    $"bidirectional overlap ratio {bidirectionalOverlapRatio:0.###}",
                                    $"axis distance {axisDistance:0.###}"));
                        }
                    }
                    else if (gap <= options.MaximumContinuationGap)
                    {
                        var strength = 0.10
                            + (0.12 * (1 - Math.Clamp(gap / Math.Max(1, options.MaximumContinuationGap), 0, 1)));
                        Add(
                            relations,
                            Relation(
                                StructuralEvidenceRelationKind.Continuation,
                                first,
                                second,
                                strength,
                                hard: false,
                                $"collinear continuation gap {gap:0.###}",
                                $"axis distance {axisDistance:0.###}"));
                    }

                    if (first.SourceRoomIds.Intersect(second.SourceRoomIds, StringComparer.Ordinal).Any())
                    {
                        Add(
                            relations,
                            Relation(
                                StructuralEvidenceRelationKind.SharedRoomBoundary,
                                first,
                                second,
                                0.06,
                                hard: false,
                                "candidates participate in the same room loop"));
                    }
                }
            }
        }

        AddEndpointRelations(candidates, options, relations);
        AddEndpointToWallRelations(candidates, options, relations);
        return relations.Values.ToArray();
    }

    private static bool IsSourceBackedShellExtension(
        StructuralWallCandidate first,
        StructuralWallCandidate second,
        double bidirectionalOverlapRatio)
    {
        var longer = first.DrawingLength >= second.DrawingLength ? first : second;
        var shorter = ReferenceEquals(longer, first) ? second : first;
        return longer.Origins.HasFlag(StructuralCandidateOrigin.ExteriorShell)
            && !longer.HasStrongNegativeEvidence
            && longer.DrawingLength >= shorter.DrawingLength * 1.50
            && bidirectionalOverlapRatio < 0.72;
    }

    private static void AddEndpointRelations(
        IReadOnlyList<StructuralWallCandidate> candidates,
        StructuralSolverOptions options,
        IDictionary<string, StructuralEvidenceRelation> relations)
    {
        var cellSize = Math.Max(1, options.EndpointTolerance);
        var cells = new Dictionary<(int Page, int X, int Y), List<EndpointReference>>();
        foreach (var candidate in candidates.Where(candidate => candidate.IsEligible))
        {
            AddEndpoint(candidate, candidate.CenterLine.Start, cells, cellSize);
            AddEndpoint(candidate, candidate.CenterLine.End, cells, cellSize);
        }

        foreach (var cell in cells)
        {
            foreach (var endpoint in cell.Value)
            {
                for (var xOffset = -1; xOffset <= 1; xOffset++)
                {
                    for (var yOffset = -1; yOffset <= 1; yOffset++)
                    {
                        var neighborKey = (
                            cell.Key.Page,
                            cell.Key.X + xOffset,
                            cell.Key.Y + yOffset);
                        if (!cells.TryGetValue(neighborKey, out var neighbors))
                        {
                            continue;
                        }

                        foreach (var neighbor in neighbors)
                        {
                            if (string.Equals(endpoint.Candidate.Id, neighbor.Candidate.Id, StringComparison.Ordinal)
                                || string.CompareOrdinal(endpoint.Candidate.Id, neighbor.Candidate.Id) >= 0)
                            {
                                continue;
                            }

                            var distance = endpoint.Point.DistanceTo(neighbor.Point);
                            if (distance > options.EndpointTolerance)
                            {
                                continue;
                            }

                            var strength = 0.08
                                + (0.10 * (1 - Math.Clamp(distance / cellSize, 0, 1)));
                            Add(
                                relations,
                                Relation(
                                    StructuralEvidenceRelationKind.Junction,
                                    endpoint.Candidate,
                                    neighbor.Candidate,
                                    strength,
                                    hard: false,
                                    $"endpoint distance {distance:0.###}"));
                        }
                    }
                }
            }
        }
    }

    private static void AddEndpointToWallRelations(
        IReadOnlyList<StructuralWallCandidate> candidates,
        StructuralSolverOptions options,
        IDictionary<string, StructuralEvidenceRelation> relations)
    {
        var eligible = candidates
            .Where(candidate =>
                candidate.IsEligible
                && !candidate.HasBlockingSemanticEvidence
                && candidate.DrawingLength >= MinimumTJunctionBranchLength(options))
            .OrderBy(candidate => candidate.PageNumber)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
        var parallelTolerance = Math.Max(
            options.AngleToleranceDegrees * 2.0,
            8.0) * Math.PI / 180.0;

        foreach (var branch in eligible)
        {
            foreach (var host in eligible)
            {
                if (string.CompareOrdinal(branch.Id, host.Id) == 0
                    || branch.PageNumber != host.PageNumber
                    || StructuralGeometry.AngleDifference(
                        branch.CenterLine,
                        host.CenterLine) <= parallelTolerance)
                {
                    continue;
                }

                var tolerance = JunctionTolerance(branch, host, options);
                var matches = new[]
                {
                    (Point: branch.CenterLine.Start, Endpoint: "start"),
                    (Point: branch.CenterLine.End, Endpoint: "end")
                }
                .Where(item =>
                    StructuralGeometry.IsPointNearInterior(
                        item.Point,
                        host.CenterLine,
                        tolerance))
                .Select(item => (
                    item.Endpoint,
                    Distance: host.CenterLine.DistanceToPoint(item.Point)))
                .OrderBy(item => item.Distance)
                .ToArray();
                if (matches.Length == 0)
                {
                    continue;
                }

                var best = matches[0];
                var proximity = 1
                    - Math.Clamp(
                        best.Distance / Math.Max(tolerance, 0.001),
                        0,
                        1);
                var angle = StructuralGeometry.AngleDifference(
                    branch.CenterLine,
                    host.CenterLine);
                var orthogonality = 1
                    - Math.Clamp(
                        Math.Abs((Math.PI / 2.0) - angle) / (Math.PI / 2.0),
                        0,
                        1);
                Add(
                    relations,
                    Relation(
                        StructuralEvidenceRelationKind.Junction,
                        branch,
                        host,
                        weight: 0.14
                            + (0.08 * proximity)
                            + (0.04 * orthogonality),
                        hard: false,
                        $"endpoint-to-wall T-junction from {branch.Id} {best.Endpoint} endpoint to {host.Id} interior",
                        $"endpoint-to-wall distance {best.Distance:0.###}",
                        $"junction angle {angle * 180.0 / Math.PI:0.###} degrees"));
            }
        }
    }

    private static double JunctionTolerance(
        StructuralWallCandidate branch,
        StructuralWallCandidate host,
        StructuralSolverOptions options) =>
        Math.Max(
            options.EndpointTolerance,
            Math.Min(
                options.EndpointTolerance * 2.0,
                options.AxisTolerance
                + (Math.Min(branch.Thickness, host.Thickness) / 2.0)));

    private static double MinimumTJunctionBranchLength(
        StructuralSolverOptions options) =>
        Math.Max(
            options.MaximumContinuationGap * 2.0,
            options.MinimumCandidateLength * 6.0);

    private static void AddEndpoint(
        StructuralWallCandidate candidate,
        PlanPoint point,
        IDictionary<(int Page, int X, int Y), List<EndpointReference>> cells,
        double cellSize)
    {
        var key = (
            candidate.PageNumber,
            (int)Math.Floor(point.X / cellSize),
            (int)Math.Floor(point.Y / cellSize));
        if (!cells.TryGetValue(key, out var values))
        {
            values = new List<EndpointReference>();
            cells.Add(key, values);
        }

        values.Add(new EndpointReference(candidate, point));
    }

    private static StructuralEvidenceRelation Relation(
        StructuralEvidenceRelationKind kind,
        StructuralWallCandidate first,
        StructuralWallCandidate second,
        double weight,
        bool hard,
        params string[] evidence)
    {
        var firstId = string.CompareOrdinal(first.Id, second.Id) <= 0 ? first.Id : second.Id;
        var secondId = string.Equals(firstId, first.Id, StringComparison.Ordinal) ? second.Id : first.Id;
        return new StructuralEvidenceRelation(
            $"structural-relation:{kind}:{firstId}:{secondId}",
            kind,
            firstId,
            secondId,
            weight,
            hard,
            evidence);
    }

    private static void Add(
        IDictionary<string, StructuralEvidenceRelation> relations,
        StructuralEvidenceRelation relation)
    {
        if (!relations.TryGetValue(relation.Id, out var existing)
            || Math.Abs(relation.Weight) > Math.Abs(existing.Weight))
        {
            relations[relation.Id] = relation;
        }
    }

    private sealed record EndpointReference(
        StructuralWallCandidate Candidate,
        PlanPoint Point);
}
