namespace OpenPlanTrace.Export;

public static partial class GlobalWallSolutionBuilder
{
    public const string TopologyOptimizerVersion =
        "openplantrace.wall-topology-optimizer.v2";

    private const string TopologyOptimizationMethod =
        "RobustWeightedLeastSquaresHuber";
    private const int TopologyOptimizationIterations = 6;
    private const double MinimumIntersectionSine = 0.35;
    private const double OrthogonalDotTolerance = 0.18;
    private const double CrossingClusterDistance = 0.75;
    private const double MinimumReviewHostConfidence = 0.65;
    private const double MaximumReviewEndpointResidual = 0.75;

    private static InlineJunctionTopologyBuildResult BuildInlineJunctionTopology(
        IReadOnlyList<PlacementSolvedWallRunExport> sourceRuns)
    {
        var runs = sourceRuns.ToArray();
        var seeds = new List<InlineJunctionSeed>();
        var evaluatedPairCount = 0;

        for (var firstIndex = 0; firstIndex < runs.Length; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < runs.Length; secondIndex++)
            {
                var first = runs[firstIndex];
                var second = runs[secondIndex];
                if (first.PageNumber != second.PageNumber)
                {
                    continue;
                }

                evaluatedPairCount++;
                if (!TryBuildInlineJunctionSeed(first, second, out var seed))
                {
                    continue;
                }

                seeds.Add(seed);
            }
        }

        var assignedSeeds = AssignInlineJunctionNodeIds(seeds);
        var nodeFits = assignedSeeds
            .GroupBy(seed => seed.NodeId, StringComparer.Ordinal)
            .Select(group => FitInlineJunctionNode(group.ToArray(), runs))
            .OrderBy(fit => fit.PageNumber)
            .ThenBy(fit => fit.NodePosition.Y)
            .ThenBy(fit => fit.NodePosition.X)
            .ThenBy(fit => fit.NodeId, StringComparer.Ordinal)
            .ToArray();
        var referencesByRunId = nodeFits
            .SelectMany(fit => BuildInlineJunctionReferences(fit, runs))
            .GroupBy(reference => reference.WallRunId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PlacementSolvedWallInlineJunctionExport>)group
                    .OrderBy(reference => reference.OffsetDrawingUnits)
                    .ThenBy(reference => reference.NodeId, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        var enrichedRuns = runs
            .Select(run => run with
            {
                InlineJunctions = referencesByRunId.TryGetValue(run.Id, out var references)
                    ? references
                    : Array.Empty<PlacementSolvedWallInlineJunctionExport>()
            })
            .ToArray();
        var constraintCount = nodeFits.Sum(fit =>
            fit.Optimization.ObservationCount + fit.Optimization.LineConstraintCount);
        var rootMeanSquareResidual = constraintCount == 0
            ? 0
            : Math.Sqrt(nodeFits.Sum(fit =>
                    Math.Pow(fit.Optimization.RootMeanSquareResidualDrawingUnits, 2)
                    * (fit.Optimization.ObservationCount + fit.Optimization.LineConstraintCount))
                / constraintCount);
        var maximumResidual = nodeFits.Length == 0
            ? 0
            : nodeFits.Max(fit => fit.Optimization.MaximumResidualDrawingUnits);
        var summary = new PlacementWallTopologyOptimizationSummaryExport(
            TopologyOptimizerVersion,
            TopologyOptimizationMethod,
            evaluatedPairCount,
            nodeFits.Length,
            enrichedRuns.Sum(run => run.InlineJunctions.Count),
            nodeFits.Count(fit => string.Equals(fit.Kind, "TJunction", StringComparison.Ordinal)),
            nodeFits.Count(fit => string.Equals(fit.Kind, "Crossing", StringComparison.Ordinal)),
            nodeFits.Count(fit => fit.Optimization.EndpointAnchored),
            nodeFits.Sum(fit => fit.Optimization.ObservationCount),
            nodeFits.Sum(fit => fit.Optimization.LineConstraintCount),
            nodeFits.Length == 0
                ? 0
                : nodeFits.Max(fit => fit.Optimization.IterationCount),
            Round(rootMeanSquareResidual),
            Round(maximumResidual),
            Round(nodeFits.Sum(fit => fit.Optimization.RobustObjective)),
            new[]
            {
                $"topology optimizer evaluated {evaluatedPairCount} canonical wall pair(s)",
                $"topology optimizer retained {nodeFits.Length} shared junction node(s) as {enrichedRuns.Sum(run => run.InlineJunctions.Count)} inline wall reference(s)",
                "long canonical walls remain unsplit; inline references preserve T-junction and crossing connectivity",
                "Huber-weighted residuals limit the influence of displaced endpoint observations",
                "opening intervals are excluded from automatic inline junction attachment"
            });

        return new InlineJunctionTopologyBuildResult(enrichedRuns, summary);
    }

    private static bool TryBuildInlineJunctionSeed(
        PlacementSolvedWallRunExport first,
        PlacementSolvedWallRunExport second,
        out InlineJunctionSeed seed)
    {
        seed = default!;
        if (!TopologyRunHasUsableGeometry(first)
            || !TopologyRunHasUsableGeometry(second)
            || !TryInfiniteLineIntersection(
                first.CenterLine,
                second.CenterLine,
                out var intersection,
                out var firstParameter,
                out var secondParameter,
                out var sine,
                out var absoluteDot)
            || sine < MinimumIntersectionSine)
        {
            return false;
        }

        var firstLength = first.DrawingLength;
        var secondLength = second.DrawingLength;
        var firstEndpointTolerance = TopologyEndpointTolerance(first);
        var secondEndpointTolerance = TopologyEndpointTolerance(second);
        var firstEndpoint = TryResolveEndpoint(
            first,
            intersection,
            firstEndpointTolerance,
            out var firstNodeId,
            out var firstNodePosition);
        var secondEndpoint = TryResolveEndpoint(
            second,
            intersection,
            secondEndpointTolerance,
            out var secondNodeId,
            out var secondNodePosition);
        var firstInterior = IsInteriorParameter(
            firstParameter,
            firstLength,
            TopologyInteriorMargin(first));
        var secondInterior = IsInteriorParameter(
            secondParameter,
            secondLength,
            TopologyInteriorMargin(second));
        var orthogonal = absoluteDot <= OrthogonalDotTolerance;
        var requiresReview = first.Reliability.RequiresReview
            || second.Reliability.RequiresReview
            || !orthogonal;

        if (firstEndpoint && secondInterior)
        {
            if (!CanCreateTJunction(
                    first,
                    second,
                    intersection,
                    firstNodePosition,
                    secondParameter,
                    orthogonal)
                || ParameterIntersectsOpening(second, secondParameter))
            {
                return false;
            }

            seed = new InlineJunctionSeed(
                first.PageNumber,
                "TJunction",
                firstNodeId,
                firstNodePosition,
                intersection,
                [first.Id, second.Id],
                [second.Id],
                Math.Min(first.Confidence, second.Confidence),
                requiresReview,
                sine,
                absoluteDot);
            return true;
        }

        if (secondEndpoint && firstInterior)
        {
            if (!CanCreateTJunction(
                    second,
                    first,
                    intersection,
                    secondNodePosition,
                    firstParameter,
                    orthogonal)
                || ParameterIntersectsOpening(first, firstParameter))
            {
                return false;
            }

            seed = new InlineJunctionSeed(
                first.PageNumber,
                "TJunction",
                secondNodeId,
                secondNodePosition,
                intersection,
                [first.Id, second.Id],
                [first.Id],
                Math.Min(first.Confidence, second.Confidence),
                requiresReview,
                sine,
                absoluteDot);
            return true;
        }

        if (!first.Reliability.ReadyForCoordinatePlacement
            || !second.Reliability.ReadyForCoordinatePlacement
            || !firstInterior
            || !secondInterior
            || ParameterIntersectsOpening(first, firstParameter)
            || ParameterIntersectsOpening(second, secondParameter))
        {
            return false;
        }

        seed = new InlineJunctionSeed(
            first.PageNumber,
            "Crossing",
            NodeId: null,
            NodePosition: intersection,
            SeedPosition: intersection,
            IncidentWallRunIds: [first.Id, second.Id],
            InlineWallRunIds: [first.Id, second.Id],
            Confidence: Math.Min(first.Confidence, second.Confidence),
            RequiresReview: requiresReview,
            IntersectionSine: sine,
            AbsoluteDirectionDot: absoluteDot);
        return true;
    }

    private static bool TopologyRunHasUsableGeometry(PlacementSolvedWallRunExport run) =>
        run.DrawingLength > 0.5
        && run.Confidence >= 0.50;

    private static bool CanCreateTJunction(
        PlacementSolvedWallRunExport endpointRun,
        PlacementSolvedWallRunExport hostRun,
        PointExport intersection,
        PointExport endpointPosition,
        double hostParameter,
        bool orthogonal)
    {
        if (!endpointRun.Reliability.ReadyForCoordinatePlacement)
        {
            return false;
        }

        if (hostRun.Reliability.ReadyForCoordinatePlacement)
        {
            return true;
        }

        return orthogonal
            && hostRun.Reliability.RequiresReview
            && hostRun.HasCoherentRoomBoundarySupport
            && hostRun.Confidence >= MinimumReviewHostConfidence
            && !string.Equals(hostRun.WallType, "Unknown", StringComparison.OrdinalIgnoreCase)
            && Distance(ToPlanPoint(intersection), ToPlanPoint(endpointPosition))
                <= MaximumReviewEndpointResidual
            && ParameterIntersectsSolid(hostRun, hostParameter);
    }

    private static double TopologyEndpointTolerance(PlacementSolvedWallRunExport run) =>
        Math.Clamp(run.ThicknessDrawingUnits * 0.20, 0.75, 2.5);

    private static double TopologyInteriorMargin(PlacementSolvedWallRunExport run) =>
        Math.Clamp(run.ThicknessDrawingUnits * 0.35, 0.75, 4.0);

    private static bool TryResolveEndpoint(
        PlacementSolvedWallRunExport run,
        PointExport intersection,
        double tolerance,
        out string nodeId,
        out PointExport nodePosition)
    {
        var startDistance = Distance(
            ToPlanPoint(run.CenterLine.Start),
            ToPlanPoint(intersection));
        var endDistance = Distance(
            ToPlanPoint(run.CenterLine.End),
            ToPlanPoint(intersection));
        if (Math.Min(startDistance, endDistance) > tolerance)
        {
            nodeId = string.Empty;
            nodePosition = default!;
            return false;
        }

        if (startDistance <= endDistance)
        {
            nodeId = run.FromNodeId;
            nodePosition = run.CenterLine.Start;
        }
        else
        {
            nodeId = run.ToNodeId;
            nodePosition = run.CenterLine.End;
        }

        return true;
    }

    private static bool IsInteriorParameter(
        double parameter,
        double length,
        double margin)
    {
        if (length <= 0.001)
        {
            return false;
        }

        var offset = parameter * length;
        return offset >= margin && offset <= length - margin;
    }

    private static bool ParameterIntersectsOpening(
        PlacementSolvedWallRunExport run,
        double parameter) =>
        run.OpeningIntervals.Any(interval =>
            parameter >= interval.StartParameter - 0.0005
            && parameter <= interval.EndParameter + 0.0005);

    private static bool ParameterIntersectsSolid(
        PlacementSolvedWallRunExport run,
        double parameter) =>
        run.SolidIntervals.Any(interval =>
            parameter >= interval.StartParameter - 0.0005
            && parameter <= interval.EndParameter + 0.0005);

    private static bool TryInfiniteLineIntersection(
        LineExport first,
        LineExport second,
        out PointExport intersection,
        out double firstParameter,
        out double secondParameter,
        out double sine,
        out double absoluteDot)
    {
        var firstX = first.End.X - first.Start.X;
        var firstY = first.End.Y - first.Start.Y;
        var secondX = second.End.X - second.Start.X;
        var secondY = second.End.Y - second.Start.Y;
        var firstLength = Math.Sqrt(firstX * firstX + firstY * firstY);
        var secondLength = Math.Sqrt(secondX * secondX + secondY * secondY);
        if (firstLength <= 0.001 || secondLength <= 0.001)
        {
            intersection = default!;
            firstParameter = 0;
            secondParameter = 0;
            sine = 0;
            absoluteDot = 1;
            return false;
        }

        var cross = firstX * secondY - firstY * secondX;
        sine = Math.Abs(cross) / (firstLength * secondLength);
        absoluteDot = Math.Abs(firstX * secondX + firstY * secondY)
            / (firstLength * secondLength);
        if (Math.Abs(cross) <= firstLength * secondLength * 0.000001)
        {
            intersection = default!;
            firstParameter = 0;
            secondParameter = 0;
            return false;
        }

        var deltaX = second.Start.X - first.Start.X;
        var deltaY = second.Start.Y - first.Start.Y;
        firstParameter = (deltaX * secondY - deltaY * secondX) / cross;
        secondParameter = (deltaX * firstY - deltaY * firstX) / cross;
        intersection = PointAt(first, firstParameter);
        return true;
    }

    private static IReadOnlyList<AssignedInlineJunctionSeed> AssignInlineJunctionNodeIds(
        IReadOnlyList<InlineJunctionSeed> seeds)
    {
        var assigned = new List<AssignedInlineJunctionSeed>();
        foreach (var seed in seeds
                     .Where(item => item.NodeId is not null)
                     .OrderBy(item => item.PageNumber)
                     .ThenBy(item => item.NodePosition.Y)
                     .ThenBy(item => item.NodePosition.X)
                     .ThenBy(item => item.NodeId, StringComparer.Ordinal))
        {
            assigned.Add(new AssignedInlineJunctionSeed(seed.NodeId!, seed));
        }

        var crossingClusters = new List<List<InlineJunctionSeed>>();
        foreach (var seed in seeds
                     .Where(item => item.NodeId is null)
                     .OrderBy(item => item.PageNumber)
                     .ThenBy(item => item.NodePosition.Y)
                     .ThenBy(item => item.NodePosition.X)
                     .ThenBy(item => string.Join("|", item.IncidentWallRunIds), StringComparer.Ordinal))
        {
            var cluster = crossingClusters.FirstOrDefault(items =>
                items[0].PageNumber == seed.PageNumber
                && Distance(
                    ToPlanPoint(items[0].NodePosition),
                    ToPlanPoint(seed.NodePosition)) <= CrossingClusterDistance);
            if (cluster is null)
            {
                crossingClusters.Add([seed]);
            }
            else
            {
                cluster.Add(seed);
            }
        }

        for (var index = 0; index < crossingClusters.Count; index++)
        {
            var nodeId =
                $"wall-solution:page:{crossingClusters[index][0].PageNumber}:inline-node:{index + 1}";
            assigned.AddRange(crossingClusters[index].Select(seed =>
                new AssignedInlineJunctionSeed(nodeId, seed)));
        }

        return assigned;
    }

    private static FittedInlineJunctionNode FitInlineJunctionNode(
        IReadOnlyList<AssignedInlineJunctionSeed> assignedSeeds,
        IReadOnlyList<PlacementSolvedWallRunExport> runs)
    {
        var first = assignedSeeds[0];
        var incidentRunIds = assignedSeeds
            .SelectMany(item => item.Seed.IncidentWallRunIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var inlineRunIds = assignedSeeds
            .SelectMany(item => item.Seed.InlineWallRunIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var incidentRuns = runs
            .Where(run => incidentRunIds.Contains(run.Id, StringComparer.Ordinal))
            .ToArray();
        var endpointAnchored = assignedSeeds.Any(item =>
            string.Equals(item.Seed.Kind, "TJunction", StringComparison.Ordinal));
        var endpointPositions = assignedSeeds
            .Where(item => item.Seed.NodeId is not null)
            .Select(item => item.Seed.NodePosition)
            .Distinct()
            .ToArray();
        var seedPositions = assignedSeeds
            .Select(item => item.Seed.SeedPosition)
            .ToArray();
        var initialPosition = endpointPositions.FirstOrDefault()
            ?? new PointExport(
                seedPositions.Average(position => position.X),
                seedPositions.Average(position => position.Y));
        var fit = FitRobustJunctionPosition(
            initialPosition,
            endpointPositions,
            seedPositions,
            incidentRuns,
            endpointAnchored);
        var kind = assignedSeeds.Any(item =>
                string.Equals(item.Seed.Kind, "TJunction", StringComparison.Ordinal))
            && assignedSeeds.Any(item =>
                string.Equals(item.Seed.Kind, "Crossing", StringComparison.Ordinal))
                ? "MultiJunction"
                : assignedSeeds.Any(item =>
                    string.Equals(item.Seed.Kind, "TJunction", StringComparison.Ordinal))
                    ? "TJunction"
                    : "Crossing";
        var confidence = Math.Clamp(
            assignedSeeds.Min(item => item.Seed.Confidence)
            * Math.Exp(-fit.MaximumResidualDrawingUnits / 3.0),
            0,
            1);
        var requiresReview = assignedSeeds.Any(item => item.Seed.RequiresReview)
            || fit.MaximumResidualDrawingUnits > 0.75;

        return new FittedInlineJunctionNode(
            first.NodeId,
            first.Seed.PageNumber,
            kind,
            fit.Position,
            incidentRunIds,
            inlineRunIds,
            confidence,
            requiresReview,
            fit.Optimization);
    }

    private static RobustJunctionFit FitRobustJunctionPosition(
        PointExport initialPosition,
        IReadOnlyList<PointExport> endpointPositions,
        IReadOnlyList<PointExport> seedPositions,
        IReadOnlyList<PlacementSolvedWallRunExport> incidentRuns,
        bool endpointAnchored)
    {
        var pointObservations = endpointPositions
            .Select(position => new WeightedPointObservation(position, 8.0))
            .Concat(seedPositions.Select(position =>
                new WeightedPointObservation(position, 1.0)))
            .ToArray();
        var lineConstraints = incidentRuns
            .Select(run => WeightedLineConstraint.From(
                run.CenterLine,
                1.0 + run.Confidence * 2.0))
            .Where(constraint => constraint is not null)
            .Cast<WeightedLineConstraint>()
            .ToArray();
        var delta = Math.Clamp(
            incidentRuns.Select(run => run.ThicknessDrawingUnits)
                .DefaultIfEmpty(4.0)
                .Average() * 0.15,
            0.5,
            1.5);
        var current = initialPosition;
        var iterations = 0;
        var converged = false;

        if (!endpointAnchored)
        {
            for (var iteration = 0; iteration < TopologyOptimizationIterations; iteration++)
            {
                iterations = iteration + 1;
                var a00 = 0.0;
                var a01 = 0.0;
                var a11 = 0.0;
                var b0 = 0.0;
                var b1 = 0.0;
                foreach (var observation in pointObservations)
                {
                    var residual = Distance(
                        ToPlanPoint(current),
                        ToPlanPoint(observation.Position));
                    var weight = observation.Weight * HuberWeight(residual, delta);
                    a00 += weight;
                    a11 += weight;
                    b0 += weight * observation.Position.X;
                    b1 += weight * observation.Position.Y;
                }

                foreach (var constraint in lineConstraints)
                {
                    var residual = Math.Abs(
                        constraint.NormalX * current.X
                        + constraint.NormalY * current.Y
                        - constraint.Offset);
                    var weight = constraint.Weight * HuberWeight(residual, delta);
                    a00 += weight * constraint.NormalX * constraint.NormalX;
                    a01 += weight * constraint.NormalX * constraint.NormalY;
                    a11 += weight * constraint.NormalY * constraint.NormalY;
                    b0 += weight * constraint.Offset * constraint.NormalX;
                    b1 += weight * constraint.Offset * constraint.NormalY;
                }

                var determinant = a00 * a11 - a01 * a01;
                if (Math.Abs(determinant) <= 0.000000001)
                {
                    break;
                }

                var next = new PointExport(
                    (b0 * a11 - b1 * a01) / determinant,
                    (a00 * b1 - a01 * b0) / determinant);
                if (Distance(ToPlanPoint(current), ToPlanPoint(next)) <= 0.000001)
                {
                    current = next;
                    converged = true;
                    break;
                }

                current = next;
            }
        }
        else
        {
            iterations = 1;
            converged = true;
        }

        var residuals = pointObservations
            .Select(observation => Distance(
                ToPlanPoint(current),
                ToPlanPoint(observation.Position)))
            .Concat(lineConstraints.Select(constraint => Math.Abs(
                constraint.NormalX * current.X
                + constraint.NormalY * current.Y
                - constraint.Offset)))
            .ToArray();
        var rootMeanSquareResidual = residuals.Length == 0
            ? 0
            : Math.Sqrt(residuals.Sum(residual => residual * residual) / residuals.Length);
        var maximumResidual = residuals.Length == 0 ? 0 : residuals.Max();
        var objective = pointObservations.Sum(observation =>
                observation.Weight * HuberLoss(
                    Distance(ToPlanPoint(current), ToPlanPoint(observation.Position)),
                    delta))
            + lineConstraints.Sum(constraint =>
                constraint.Weight * HuberLoss(
                    Math.Abs(
                        constraint.NormalX * current.X
                        + constraint.NormalY * current.Y
                        - constraint.Offset),
                    delta));
        var optimization = new PlacementWallJunctionOptimizationExport(
            TopologyOptimizerVersion,
            TopologyOptimizationMethod,
            endpointAnchored,
            iterations,
            pointObservations.Length,
            lineConstraints.Length,
            Round(rootMeanSquareResidual),
            Round(maximumResidual),
            Round(objective),
            converged,
            new[]
            {
                endpointAnchored
                    ? "existing canonical endpoint is a hard topology anchor"
                    : "junction position solved from wall-line factors and geometric observations",
                $"Huber transition delta {delta:0.###} drawing units",
                $"fit used {pointObservations.Length} point observation(s) and {lineConstraints.Length} line constraint(s)"
            });
        return new RobustJunctionFit(current, maximumResidual, optimization);
    }

    private static double HuberWeight(double residual, double delta) =>
        residual <= delta || residual <= 0.000000001
            ? 1.0
            : delta / residual;

    private static double HuberLoss(double residual, double delta) =>
        residual <= delta
            ? 0.5 * residual * residual
            : delta * (residual - 0.5 * delta);

    private static IEnumerable<PlacementSolvedWallInlineJunctionExport>
        BuildInlineJunctionReferences(
            FittedInlineJunctionNode fit,
            IReadOnlyList<PlacementSolvedWallRunExport> runs)
    {
        foreach (var runId in fit.InlineWallRunIds)
        {
            var run = runs.First(item =>
                string.Equals(item.Id, runId, StringComparison.Ordinal));
            var parameter = Math.Clamp(ProjectParameter(run.CenterLine, fit.NodePosition), 0, 1);
            var wallPosition = PointAt(run.CenterLine, parameter);
            var projectionResidual = Distance(
                ToPlanPoint(fit.NodePosition),
                ToPlanPoint(wallPosition));
            var offset = parameter * run.DrawingLength;
            var scale = run.MillimetersPerDrawingUnit;
            var requiresReview = fit.RequiresReview
                || projectionResidual > Math.Max(0.5, run.ThicknessDrawingUnits * 0.10);
            yield return new PlacementSolvedWallInlineJunctionExport(
                $"{run.Id}:inline-junction:{fit.NodeId}",
                run.Id,
                fit.NodeId,
                run.PageNumber,
                fit.Kind,
                fit.NodePosition,
                ScalePoint(fit.NodePosition, scale),
                wallPosition,
                ScalePoint(wallPosition, scale),
                Round(parameter),
                Round(offset),
                scale is > 0 ? Round(offset * scale.Value) : null,
                Round(projectionResidual),
                scale is > 0 ? Round(projectionResidual * scale.Value) : null,
                fit.IncidentWallRunIds,
                Round(fit.Confidence),
                requiresReview,
                fit.Optimization,
                new[]
                {
                    $"{fit.Kind} retained as an inline reference on an unsplit canonical wall run",
                    $"junction parameter {parameter:0.######} and offset {offset:0.###} drawing units",
                    $"node-to-wall projection residual {projectionResidual:0.###} drawing units",
                    $"incident canonical wall runs: {string.Join(",", fit.IncidentWallRunIds)}"
                });
        }
    }

    private sealed record InlineJunctionTopologyBuildResult(
        IReadOnlyList<PlacementSolvedWallRunExport> Runs,
        PlacementWallTopologyOptimizationSummaryExport Summary);

    private sealed record InlineJunctionSeed(
        int PageNumber,
        string Kind,
        string? NodeId,
        PointExport NodePosition,
        PointExport SeedPosition,
        IReadOnlyList<string> IncidentWallRunIds,
        IReadOnlyList<string> InlineWallRunIds,
        double Confidence,
        bool RequiresReview,
        double IntersectionSine,
        double AbsoluteDirectionDot);

    private sealed record AssignedInlineJunctionSeed(
        string NodeId,
        InlineJunctionSeed Seed);

    private sealed record FittedInlineJunctionNode(
        string NodeId,
        int PageNumber,
        string Kind,
        PointExport NodePosition,
        IReadOnlyList<string> IncidentWallRunIds,
        IReadOnlyList<string> InlineWallRunIds,
        double Confidence,
        bool RequiresReview,
        PlacementWallJunctionOptimizationExport Optimization);

    private sealed record RobustJunctionFit(
        PointExport Position,
        double MaximumResidualDrawingUnits,
        PlacementWallJunctionOptimizationExport Optimization);

    private sealed record WeightedPointObservation(
        PointExport Position,
        double Weight);

    private sealed record WeightedLineConstraint(
        double NormalX,
        double NormalY,
        double Offset,
        double Weight)
    {
        public static WeightedLineConstraint? From(LineExport line, double weight)
        {
            var dx = line.End.X - line.Start.X;
            var dy = line.End.Y - line.Start.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length <= 0.001)
            {
                return null;
            }

            var normalX = -dy / length;
            var normalY = dx / length;
            return new WeightedLineConstraint(
                normalX,
                normalY,
                normalX * line.Start.X + normalY * line.Start.Y,
                weight);
        }
    }
}
