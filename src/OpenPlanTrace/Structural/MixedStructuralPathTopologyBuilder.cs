using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OpenPlanTrace;

public static class MixedStructuralPathTopologyBuilder
{
    private const double MinimumCornerAngleDegrees = 20;

    public static StructuralPathTopology Build(
        StructuralPlanSolution structuralSolution,
        IReadOnlyList<CurvedWallCandidate> curvedWallCandidates,
        IReadOnlyList<WallSegment> sourceWalls,
        PlanCalibration calibration,
        StructuralSolverOptions options)
    {
        ArgumentNullException.ThrowIfNull(structuralSolution);
        ArgumentNullException.ThrowIfNull(curvedWallCandidates);
        ArgumentNullException.ThrowIfNull(sourceWalls);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(options);

        var sourceWallsById = sourceWalls
            .GroupBy(wall => wall.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var paths = structuralSolution.WallRuns
            .Where(run => run.CenterLine.Length > double.Epsilon)
            .Select(run => CreateLinePath(run, sourceWallsById, calibration))
            .Concat(curvedWallCandidates
                .Where(IsValidCurve)
                .Select(CreateCircularArcPath))
            .OrderBy(path => path.PageNumber)
            .ThenBy(path => path.Kind)
            .ThenBy(path => path.Id, StringComparer.Ordinal)
            .ToArray();

        var junctions = BuildJunctions(paths, options);
        paths = AttachConnectivity(paths, junctions);

        var linePaths = paths.Count(path => path.Kind == StructuralPathKind.Line);
        var curvedPaths = paths
            .Where(path => path.Kind == StructuralPathKind.CircularArc)
            .ToArray();
        var connectedCurvedPaths = curvedPaths.Count(path => path.ConnectedStraightPathSupportCount > 0);
        var metrics = new StructuralPathTopologyMetrics(
            linePaths,
            curvedPaths.Length,
            junctions.Count,
            junctions.Count(junction => junction.Kind == StructuralPathJunctionKind.Tangent),
            junctions.Count(junction => junction.Kind == StructuralPathJunctionKind.Corner),
            connectedCurvedPaths,
            curvedPaths.Length - connectedCurvedPaths,
            curvedWallCandidates.Count - curvedPaths.Length,
            paths.Count(path => path.ReadyForCoordinatePlacement),
            paths.Count(path => path.RequiresReview));

        return new StructuralPathTopology(
            StructuralPathTopology.CurrentContractVersion,
            paths,
            junctions,
            metrics,
            new[]
            {
                "mixed structural topology preserves exact source line and circular-arc geometry",
                "line-to-arc connectivity is inferred only between original path endpoints",
                "junction proposed positions do not mutate source path coordinates",
                "circular-arc paths and mixed junctions remain review-only"
            });
    }

    private static StructuralPath CreateLinePath(
        StructuralWallRun run,
        IReadOnlyDictionary<string, WallSegment> sourceWallsById,
        PlanCalibration calibration)
    {
        var sourceWallRecords = run.SourceWallIds
            .Where(sourceWallsById.ContainsKey)
            .Select(id => sourceWallsById[id])
            .ToArray();
        var sourceRegionId = SingleValue(sourceWallRecords.Select(wall => wall.SourceRegionId));
        var scaleGroup = ResolveScaleGroup(
            run.PageNumber,
            run.Bounds,
            sourceRegionId,
            sourceWallRecords,
            calibration);
        var thicknessMillimeters = calibration.ToMillimeters(run.Thickness, scaleGroup);
        var lengthMillimeters = calibration.ToMillimeters(run.DrawingLength, scaleGroup);
        var reliabilityConfidence = run.Reliability.Confidence > 0
            ? Math.Min(run.Confidence.Value, run.Reliability.Confidence)
            : run.Confidence.Value;

        return new StructuralPath(
            $"structural-path:line:{StableId(new[] { run.Id })}",
            run.PageNumber,
            new StructuralLinePathGeometry(run.CenterLine),
            run.Thickness,
            thicknessMillimeters,
            lengthMillimeters is { } length ? length / 1000.0 : null,
            scaleGroup?.Id,
            run.WallType,
            new Confidence(reliabilityConfidence),
            run.Reliability.ReadyForCoordinatePlacement,
            run.Reliability.RequiresReview,
            new[] { run.Id },
            Array.Empty<string>(),
            run.SourceWallIds,
            run.SourceWallGraphEdgeIds,
            run.SourcePrimitiveIds,
            run.Evidence
                .Append("canonical straight structural path derived from the selected wall solution")
                .Distinct(StringComparer.Ordinal)
                .ToArray())
        {
            SourceRegionId = sourceRegionId
        };
    }

    private static StructuralPath CreateCircularArcPath(CurvedWallCandidate curve) =>
        new(
            $"structural-path:arc:{StableId(new[] { curve.Id })}",
            curve.PageNumber,
            new StructuralCircularArcPathGeometry(
                curve.Center,
                curve.CenterlineRadius,
                curve.StartAngleRadians,
                curve.SweepAngleRadians),
            curve.Thickness,
            curve.ThicknessMillimeters,
            curve.ArcLengthMeters,
            curve.MeasurementScaleGroupId,
            WallType.Unknown,
            curve.Confidence,
            ReadyForCoordinatePlacement: false,
            RequiresReview: true,
            Array.Empty<string>(),
            new[] { curve.Id },
            Array.Empty<string>(),
            Array.Empty<string>(),
            curve.SourcePrimitiveIds,
            curve.Evidence
                .Append("exact circular-arc path is excluded from linear topology and retained for mixed-topology review")
                .Distinct(StringComparer.Ordinal)
                .ToArray())
        {
            SourceRegionId = curve.SourceRegionId
        };

    private static IReadOnlyList<StructuralPathJunction> BuildJunctions(
        IReadOnlyList<StructuralPath> paths,
        StructuralSolverOptions options)
    {
        var lines = paths
            .Where(path => path.Kind == StructuralPathKind.Line)
            .ToArray();
        var arcs = paths
            .Where(path => path.Kind == StructuralPathKind.CircularArc)
            .ToArray();
        var proposals = new List<JunctionProposal>();
        var tangentTolerance = Math.Clamp(options.AngleToleranceDegrees * 3.0, 8.0, 15.0);

        foreach (var arc in arcs)
        {
            foreach (var arcEndpoint in Endpoints(arc))
            {
                foreach (var line in lines.Where(line => line.PageNumber == arc.PageNumber))
                {
                    foreach (var lineEndpoint in Endpoints(line))
                    {
                        var tolerance = MatchTolerance(line, arc, options);
                        var distance = lineEndpoint.Position.DistanceTo(arcEndpoint.Position);
                        if (distance > tolerance)
                        {
                            continue;
                        }

                        var directionAngle = DirectionAngleDegrees(
                            lineEndpoint.DirectionIntoPath,
                            arcEndpoint.DirectionIntoPath);
                        var tangentDeviation = Math.Abs(180.0 - directionAngle);
                        var kind = tangentDeviation <= tangentTolerance
                            ? StructuralPathJunctionKind.Tangent
                            : directionAngle >= MinimumCornerAngleDegrees
                                ? StructuralPathJunctionKind.Corner
                                : StructuralPathJunctionKind.Unknown;
                        if (kind == StructuralPathJunctionKind.Unknown)
                        {
                            continue;
                        }

                        var normalizedDistance = tolerance > double.Epsilon ? distance / tolerance : 1;
                        var orientationPenalty = kind == StructuralPathJunctionKind.Tangent
                            ? tangentDeviation / tangentTolerance
                            : 0.35 + (Math.Abs(90.0 - directionAngle) / 900.0);
                        proposals.Add(
                            new JunctionProposal(
                                line,
                                lineEndpoint,
                                arc,
                                arcEndpoint,
                                kind,
                                distance,
                                tolerance,
                                directionAngle,
                                tangentDeviation,
                                normalizedDistance + (orientationPenalty * 0.3)));
                    }
                }
            }
        }

        var occupiedLineEndpoints = new HashSet<string>(StringComparer.Ordinal);
        var occupiedArcEndpoints = new HashSet<string>(StringComparer.Ordinal);
        var accepted = new List<StructuralPathJunction>();
        foreach (var proposal in proposals
                     .OrderBy(item => item.Score)
                     .ThenBy(item => EndpointKey(item.ArcEndpoint), StringComparer.Ordinal)
                     .ThenBy(item => EndpointKey(item.LineEndpoint), StringComparer.Ordinal))
        {
            var lineEndpointKey = EndpointKey(proposal.LineEndpoint);
            var arcEndpointKey = EndpointKey(proposal.ArcEndpoint);
            if (!occupiedLineEndpoints.Add(lineEndpointKey))
            {
                continue;
            }

            if (!occupiedArcEndpoints.Add(arcEndpointKey))
            {
                occupiedLineEndpoints.Remove(lineEndpointKey);
                continue;
            }

            accepted.Add(CreateJunction(proposal));
        }

        return accepted
            .OrderBy(junction => junction.PageNumber)
            .ThenBy(junction => junction.ProposedPosition.Y)
            .ThenBy(junction => junction.ProposedPosition.X)
            .ThenBy(junction => junction.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static StructuralPathJunction CreateJunction(JunctionProposal proposal)
    {
        var distanceSupport = proposal.MatchTolerance > double.Epsilon
            ? 1.0 - Math.Clamp(proposal.EndpointDistance / proposal.MatchTolerance, 0, 1)
            : 0;
        var orientationSupport = proposal.Kind == StructuralPathJunctionKind.Tangent
            ? 1.0 - Math.Clamp(proposal.TangentDeviationDegrees / 15.0, 0, 1)
            : Math.Clamp(
                Math.Sin(proposal.DirectionAngleDegrees * Math.PI / 180.0),
                0.25,
                1.0);
        var confidence = Math.Min(proposal.Line.Confidence.Value, proposal.Arc.Confidence.Value)
            * (0.55 + (distanceSupport * 0.30) + (orientationSupport * 0.15));
        var sourcePrimitiveIds = proposal.Line.SourcePrimitiveIds
            .Concat(proposal.Arc.SourcePrimitiveIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var proposedPosition = new PlanPoint(
            (proposal.LineEndpoint.Position.X + proposal.ArcEndpoint.Position.X) / 2.0,
            (proposal.LineEndpoint.Position.Y + proposal.ArcEndpoint.Position.Y) / 2.0);
        var stableParts = new[]
        {
            EndpointKey(proposal.LineEndpoint),
            EndpointKey(proposal.ArcEndpoint)
        };

        return new StructuralPathJunction(
            $"structural-path-junction:{StableId(stableParts)}",
            proposal.Line.PageNumber,
            proposal.Kind,
            proposal.LineEndpoint,
            proposal.ArcEndpoint,
            proposedPosition,
            proposal.EndpointDistance,
            proposal.MatchTolerance,
            proposal.DirectionAngleDegrees,
            proposal.TangentDeviationDegrees,
            new Confidence(confidence),
            RequiresReview: true,
            sourcePrimitiveIds,
            new[]
            {
                $"line and circular-arc source endpoints are {Format(proposal.EndpointDistance)} drawing unit(s) apart",
                $"endpoint directions form {Format(proposal.DirectionAngleDegrees)} degree(s)",
                $"classified as a review-only {proposal.Kind.ToString().ToLowerInvariant()} relation",
                "proposed junction position is advisory and source endpoint coordinates remain unchanged"
            });
    }

    private static StructuralPath[] AttachConnectivity(
        IReadOnlyList<StructuralPath> paths,
        IReadOnlyList<StructuralPathJunction> junctions)
    {
        var pathsById = paths.ToDictionary(path => path.Id, StringComparer.Ordinal);
        var connectedByPath = junctions
            .SelectMany(junction => new[]
            {
                (PathId: junction.FirstEndpoint.PathId, ConnectedId: junction.SecondEndpoint.PathId),
                (PathId: junction.SecondEndpoint.PathId, ConnectedId: junction.FirstEndpoint.PathId)
            })
            .GroupBy(item => item.PathId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.ConnectedId)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        return paths
            .Select(path =>
            {
                var connected = connectedByPath.TryGetValue(path.Id, out var connectedIds)
                    ? connectedIds
                    : Array.Empty<string>();
                var connectedStraight = path.Kind == StructuralPathKind.CircularArc
                    ? connected
                        .Where(id => pathsById.TryGetValue(id, out var connectedPath)
                            && connectedPath.Kind == StructuralPathKind.Line)
                        .ToArray()
                    : Array.Empty<string>();
                return path with
                {
                    ConnectedPathIds = connected,
                    ConnectedStraightPathIds = connectedStraight
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<StructuralPathEndpointReference> Endpoints(StructuralPath path)
    {
        if (path.Geometry is StructuralLinePathGeometry line)
        {
            return new[]
            {
                new StructuralPathEndpointReference(
                    path.Id,
                    StructuralPathEndpointKind.Start,
                    line.StartPoint,
                    (line.EndPoint - line.StartPoint).Normalize()),
                new StructuralPathEndpointReference(
                    path.Id,
                    StructuralPathEndpointKind.End,
                    line.EndPoint,
                    (line.StartPoint - line.EndPoint).Normalize())
            };
        }

        if (path.Geometry is StructuralCircularArcPathGeometry arc)
        {
            var sweepSign = Math.Sign(arc.SweepAngleRadians);
            var endAngle = arc.StartAngleRadians + arc.SweepAngleRadians;
            var startForward = new PlanVector(
                -Math.Sin(arc.StartAngleRadians) * sweepSign,
                Math.Cos(arc.StartAngleRadians) * sweepSign).Normalize();
            var endForward = new PlanVector(
                -Math.Sin(endAngle) * sweepSign,
                Math.Cos(endAngle) * sweepSign).Normalize();
            return new[]
            {
                new StructuralPathEndpointReference(
                    path.Id,
                    StructuralPathEndpointKind.Start,
                    arc.StartPoint,
                    startForward),
                new StructuralPathEndpointReference(
                    path.Id,
                    StructuralPathEndpointKind.End,
                    arc.EndPoint,
                    endForward * -1)
            };
        }

        return Array.Empty<StructuralPathEndpointReference>();
    }

    private static double MatchTolerance(
        StructuralPath line,
        StructuralPath arc,
        StructuralSolverOptions options)
    {
        var minimum = Math.Max(0.5, options.EndpointTolerance);
        var maximum = Math.Max(minimum, options.MaximumContinuationGap);
        var thicknessAllowance = Math.Max(line.Thickness, arc.Thickness) * 0.4;
        return Math.Clamp(Math.Max(minimum, thicknessAllowance), minimum, maximum);
    }

    private static double DirectionAngleDegrees(PlanVector first, PlanVector second)
    {
        if (first.Length <= double.Epsilon || second.Length <= double.Epsilon)
        {
            return 0;
        }

        var dot = Math.Clamp(first.Normalize().Dot(second.Normalize()), -1, 1);
        return Math.Acos(dot) * 180.0 / Math.PI;
    }

    private static bool IsValidCurve(CurvedWallCandidate curve) =>
        curve.CenterlineRadius > double.Epsilon
        && double.IsFinite(curve.CenterlineRadius)
        && Math.Abs(curve.SweepAngleRadians) > 1e-6
        && double.IsFinite(curve.StartAngleRadians)
        && double.IsFinite(curve.SweepAngleRadians);

    private static CalibrationScaleGroup? ResolveScaleGroup(
        int pageNumber,
        PlanRect bounds,
        string? sourceRegionId,
        IReadOnlyList<WallSegment> sourceWalls,
        PlanCalibration calibration)
    {
        var sourceScaleGroupId = SingleValue(sourceWalls.Select(wall => wall.MeasurementScaleGroupId));
        if (!string.IsNullOrWhiteSpace(sourceScaleGroupId))
        {
            var sourceScaleGroup = calibration.ScaleGroups.FirstOrDefault(group =>
                string.Equals(group.Id, sourceScaleGroupId, StringComparison.Ordinal));
            if (sourceScaleGroup is not null)
            {
                return sourceScaleGroup;
            }
        }

        return calibration.SelectMeasurementScaleGroup(pageNumber, bounds, sourceRegionId);
    }

    private static string? SingleValue(IEnumerable<string?> values)
    {
        var distinct = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static string EndpointKey(StructuralPathEndpointReference endpoint) =>
        $"{endpoint.PathId}:{endpoint.Endpoint}";

    private static string StableId(IEnumerable<string> values)
    {
        var content = string.Join("\n", values.Order(StringComparer.Ordinal));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static string Format(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    private sealed record JunctionProposal(
        StructuralPath Line,
        StructuralPathEndpointReference LineEndpoint,
        StructuralPath Arc,
        StructuralPathEndpointReference ArcEndpoint,
        StructuralPathJunctionKind Kind,
        double EndpointDistance,
        double MatchTolerance,
        double DirectionAngleDegrees,
        double TangentDeviationDegrees,
        double Score);
}
