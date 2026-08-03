using System.Text.Json;

namespace OpenPlanTrace.Export;

public sealed record PlanStructureJsonExportOptions
{
    public bool WriteIndented { get; init; } = true;
}

public static class PlanStructureJsonExporter
{
    public static string Serialize(
        PlanScanResult result,
        PlanStructureJsonExportOptions? options = null) =>
        Serialize(PlanStructureExport.From(result), options);

    public static string Serialize(
        PlanStructureExport export,
        PlanStructureJsonExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(export);
        return JsonSerializer.Serialize(export, CreateJsonOptions(options));
    }

    public static ValueTask WriteAsync(
        PlanScanResult result,
        Stream stream,
        PlanStructureJsonExportOptions? options = null,
        CancellationToken cancellationToken = default) =>
        WriteAsync(PlanStructureExport.From(result), stream, options, cancellationToken);

    public static async ValueTask WriteAsync(
        PlanStructureExport export,
        Stream stream,
        PlanStructureJsonExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(stream);

        await JsonSerializer.SerializeAsync(
                stream,
                export,
                CreateJsonOptions(options),
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static JsonSerializerOptions CreateJsonOptions(PlanStructureJsonExportOptions? options = null) =>
        new()
        {
            WriteIndented = options?.WriteIndented ?? true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
}

public sealed record PlanStructureExport(
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    string SourceScanSchemaVersion,
    string SourcePlacementSchemaVersion,
    PlacementDocumentExport Document,
    CoordinateSystemExport CoordinateSystem,
    PlacementCalibrationExport Calibration,
    PlanStructureQualityExport Quality,
    PlanStructureSummaryExport Summary,
    PlanStructureWallSolverExport WallSolver,
    IReadOnlyList<PlacementPageExport> Pages,
    IReadOnlyList<PlanStructureNodeExport> Nodes,
    IReadOnlyList<PlanStructureWallRunExport> WallRuns,
    IReadOnlyList<PlanStructureRoomExport> Rooms,
    IReadOnlyList<PlanStructureOpeningExport> Openings,
    IReadOnlyList<PlanStructureIssueExport> Issues)
{
    public const string CurrentSchemaVersion = "openplantrace.structure.v2";

    public StructuralPathTopologyExport StructuralPathTopology { get; init; } = StructuralPathTopologyExport.Empty;

    public static PlanStructureExport From(PlanScanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return PlanStructureExportCache.GetOrCreate(result);
    }

    internal static PlanStructureExport CreateUncached(PlanScanResult result)
    {
        var structure = From(PlanPlacementExport.From(result));
        var sourceLookup = PrimitiveSourceExport.From(result.Document)
            .Where(source => !string.IsNullOrWhiteSpace(source.SourceId))
            .ToDictionary(source => source.SourceId, StringComparer.Ordinal);
        return structure with
        {
            StructuralPathTopology = StructuralPathTopologyExport.From(
                result.StructuralPathTopology,
                sourceLookup)
        };
    }

    public static PlanStructureExport From(PlanPlacementExport placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        var wallRuns = placement.WallSolutions.SelectedWallRuns
            .Where(run => run.DrawingLength > 0)
            .Select(PlanStructureWallRunExport.From)
            .OrderBy(run => run.PageNumber)
            .ThenBy(run => run.Bounds.Y)
            .ThenBy(run => run.Bounds.X)
            .ThenBy(run => run.Id, StringComparer.Ordinal)
            .ToArray();
        var nodes = BuildNodes(placement.WallGraph.Nodes, wallRuns);
        var rooms = placement.Rooms
            .Select(room => PlanStructureRoomExport.From(room, wallRuns))
            .OrderBy(room => room.PageNumber)
            .ThenBy(room => room.Bounds.Y)
            .ThenBy(room => room.Bounds.X)
            .ThenBy(room => room.Id, StringComparer.Ordinal)
            .ToArray();
        var openings = placement.Openings
            .Select(opening => PlanStructureOpeningExport.From(opening, wallRuns))
            .OrderBy(opening => opening.PageNumber)
            .ThenBy(opening => opening.Bounds.Y)
            .ThenBy(opening => opening.Bounds.X)
            .ThenBy(opening => opening.Id, StringComparer.Ordinal)
            .ToArray();
        var assessment = PlanStructureAssessment.Evaluate(
            placement,
            wallRuns,
            nodes,
            rooms,
            openings);

        return new PlanStructureExport(
            CurrentSchemaVersion,
            placement.GeneratedAt,
            placement.ScanSchemaVersion,
            placement.SchemaVersion,
            placement.Document,
            placement.CoordinateSystem,
            placement.Calibration,
            assessment.Quality,
            assessment.Summary,
            PlanStructureWallSolverExport.From(placement.WallSolutions),
            placement.Pages,
            nodes,
            wallRuns,
            rooms,
            openings,
            assessment.Issues);
    }

    private static PlanStructureNodeExport[] BuildNodes(
        IReadOnlyList<PlacementWallGraphNodeExport> placementNodes,
        IReadOnlyList<PlanStructureWallRunExport> wallRuns)
    {
        var placementNodesById = placementNodes
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var endpointAssociations = wallRuns
            .SelectMany(run => new[]
            {
                new PlanStructureNodeAssociation(
                    run.FromNodeId,
                    run.PageNumber,
                    run.CenterLine.Start,
                    run.CenterLineMillimeters?.Start,
                    run.Id,
                    false,
                    null,
                    run.Confidence,
                    run.Reliability.RequiresReview,
                    null),
                new PlanStructureNodeAssociation(
                    run.ToNodeId,
                    run.PageNumber,
                    run.CenterLine.End,
                    run.CenterLineMillimeters?.End,
                    run.Id,
                    false,
                    null,
                    run.Confidence,
                    run.Reliability.RequiresReview,
                    null)
            });
        var inlineAssociations = wallRuns
            .SelectMany(run => run.InlineJunctions.Select(junction =>
                new PlanStructureNodeAssociation(
                    junction.NodeId,
                    junction.PageNumber,
                    junction.NodePosition,
                    junction.NodePositionMillimeters,
                    run.Id,
                    true,
                    junction.Kind,
                    junction.Confidence,
                    junction.RequiresReview,
                    junction.Optimization)));
        var associations = endpointAssociations
            .Concat(inlineAssociations)
            .GroupBy(association => association.NodeId, StringComparer.Ordinal);

        return associations
            .Select(group =>
            {
                var associationArray = group.ToArray();
                var endpointArray = associationArray
                    .Where(association => !association.Inline)
                    .ToArray();
                var inlineArray = associationArray
                    .Where(association => association.Inline)
                    .ToArray();
                placementNodesById.TryGetValue(group.Key, out var placementNode);
                var position = endpointArray.Length > 0
                    ? placementNode?.Position ?? endpointArray[0].Position
                    : inlineArray[0].Position;
                var positionMillimeters = endpointArray.Length > 0
                    ? placementNode?.PositionMillimeters
                      ?? endpointArray
                          .Select(endpoint => endpoint.PositionMillimeters)
                          .FirstOrDefault(point => point is not null)
                    : inlineArray
                        .Select(junction => junction.PositionMillimeters)
                        .FirstOrDefault(point => point is not null);
                var endpointWallRunIds = endpointArray
                    .Select(association => association.WallRunId)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var inlineWallRunIds = inlineArray
                    .Select(association => association.WallRunId)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var incidentWallRunIds = endpointWallRunIds
                    .Concat(inlineWallRunIds)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var degree = endpointWallRunIds.Length + inlineWallRunIds.Length * 2;
                var directions = wallRuns
                    .Where(run => incidentWallRunIds.Contains(run.Id, StringComparer.Ordinal))
                    .Select(run => RunDirection(run.CenterLine))
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var topologyKind = ResolveTopologyKind(
                    degree,
                    directions,
                    endpointWallRunIds,
                    inlineWallRunIds,
                    inlineArray);
                var evidence = (placementNode?.Evidence ?? Array.Empty<string>())
                    .Append(
                        $"canonical node has {degree} graph arm(s) across {incidentWallRunIds.Length} wall run(s)")
                    .Append(
                        $"endpoint incidences={endpointWallRunIds.Length}; inline incidences={inlineWallRunIds.Length}")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                return new PlanStructureNodeExport(
                    group.Key,
                    placementNode?.PageNumber ?? associationArray[0].PageNumber,
                    position,
                    positionMillimeters,
                    degree <= 1 ? "Endpoint" : degree == 2 ? "InlineOrCorner" : "Junction",
                    topologyKind,
                    degree,
                    incidentWallRunIds,
                    endpointWallRunIds,
                    inlineWallRunIds,
                    directions,
                    associationArray.Min(association => association.Confidence),
                    associationArray.Any(association => association.RequiresReview),
                    inlineArray.Select(association => association.Optimization)
                        .FirstOrDefault(optimization => optimization is not null),
                    evidence);
            })
            .OrderBy(node => node.PageNumber)
            .ThenBy(node => node.Position.Y)
            .ThenBy(node => node.Position.X)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolveTopologyKind(
        int degree,
        IReadOnlyList<string> directions,
        IReadOnlyList<string> endpointWallRunIds,
        IReadOnlyList<string> inlineWallRunIds,
        IReadOnlyList<PlanStructureNodeAssociation> inlineAssociations)
    {
        var inlineKinds = inlineAssociations
            .Select(association => association.TopologyKind)
            .Where(kind => !string.IsNullOrWhiteSpace(kind))
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

    private static string RunDirection(LineExport line)
    {
        var dx = line.End.X - line.Start.X;
        var dy = line.End.Y - line.Start.Y;
        if (Math.Abs(dy) <= Math.Max(0.5, Math.Abs(dx) * 0.01))
        {
            return "Horizontal";
        }

        if (Math.Abs(dx) <= Math.Max(0.5, Math.Abs(dy) * 0.01))
        {
            return "Vertical";
        }

        return "Diagonal";
    }

    private sealed record PlanStructureNodeAssociation(
        string NodeId,
        int PageNumber,
        PointExport Position,
        PointExport? PositionMillimeters,
        string WallRunId,
        bool Inline,
        string? TopologyKind,
        double Confidence,
        bool RequiresReview,
        PlacementWallJunctionOptimizationExport? Optimization);
}

public sealed record PlanStructureWallSolverExport(
    string SolverVersion,
    string SelectedHypothesisId,
    string SelectedProfile,
    double SelectedScore,
    int CandidateCount,
    int SelectedCandidateCount,
    int SelectedWallRunCount,
    int IterationCount,
    PlacementWallHypothesisMetricsExport Metrics,
    IReadOnlyList<PlanStructureWallHypothesisExport> Hypotheses,
    PlacementWallReconciliationSummaryExport Reconciliation,
    PlacementWallTopologyOptimizationSummaryExport Topology,
    IReadOnlyList<string> Evidence)
{
    public static PlanStructureWallSolverExport From(PlacementWallSolutionSetExport solutions)
    {
        var selected = solutions.Hypotheses.First(hypothesis => hypothesis.Selected);
        return new PlanStructureWallSolverExport(
            solutions.SolverVersion,
            solutions.SelectedHypothesisId,
            solutions.SelectedProfile,
            solutions.SelectedScore,
            solutions.CandidateCount,
            solutions.SelectedCandidateCount,
            solutions.SelectedWallRunCount,
            solutions.IterationCount,
            selected.Metrics,
            solutions.Hypotheses
                .Select(PlanStructureWallHypothesisExport.From)
                .ToArray(),
            solutions.Reconciliation,
            solutions.Topology,
            solutions.Evidence);
    }
}

public sealed record PlanStructureWallHypothesisExport(
    string Id,
    string Profile,
    double Score,
    bool Selected,
    int SelectedCandidateCount,
    int RecoveredCandidateCount,
    PlacementWallHypothesisMetricsExport Metrics)
{
    public static PlanStructureWallHypothesisExport From(PlacementWallHypothesisExport hypothesis) =>
        new(
            hypothesis.Id,
            hypothesis.Profile,
            hypothesis.Score,
            hypothesis.Selected,
            hypothesis.SelectedCandidateCount,
            hypothesis.RecoveredCandidateCount,
            hypothesis.Metrics);
}

public sealed record PlanStructureWallRunExport(
    string Id,
    int PageNumber,
    string FromNodeId,
    string ToNodeId,
    string WallType,
    string? WallComponentId,
    string? WallComponentKind,
    LineExport CenterLine,
    LineExport? CenterLineMillimeters,
    RectExport Bounds,
    RectExport? BoundsMillimeters,
    double DrawingLength,
    double? LengthMeters,
    double ThicknessDrawingUnits,
    double? ThicknessMillimeters,
    double? MillimetersPerDrawingUnit,
    double SolidDrawingLength,
    double? SolidLengthMeters,
    double OpeningDrawingLength,
    int ReconstructedOpeningGapCount,
    double Confidence,
    PlacementReliabilityExport Reliability,
    IReadOnlyList<PlacementSolvedWallOpeningIntervalExport> OpeningIntervals,
    IReadOnlyList<PlacementSolvedWallSolidIntervalExport> SolidIntervals,
    IReadOnlyList<string> SourceWallIds,
    IReadOnlyList<string> SourceWallGraphEdgeIds,
    IReadOnlyList<string> SourcePrimitiveIds,
    IReadOnlyList<string> SourceLayers,
    IReadOnlyList<PlacementSolvedWallInlineJunctionExport> InlineJunctions,
    PlacementSolvedWallReconciliationExport Reconciliation,
    IReadOnlyList<string> Evidence)
{
    public static PlanStructureWallRunExport From(PlacementSolvedWallRunExport run) =>
        new(
            run.Id,
            run.PageNumber,
            run.FromNodeId,
            run.ToNodeId,
            run.WallType,
            null,
            null,
            run.CenterLine,
            run.CenterLineMillimeters,
            run.Bounds,
            run.BoundsMillimeters,
            run.DrawingLength,
            run.LengthMeters,
            run.ThicknessDrawingUnits,
            run.ThicknessMillimeters,
            run.MillimetersPerDrawingUnit,
            run.SolidDrawingLength,
            run.SolidLengthMeters,
            run.OpeningDrawingLength,
            run.ReconstructedOpeningGapCount,
            run.Confidence,
            run.Reliability,
            run.OpeningIntervals,
            run.SolidIntervals,
            run.SourceWallIds,
            run.SourceWallGraphEdgeIds,
            run.SourcePrimitiveIds,
            run.SourceLayers,
            run.InlineJunctions,
            run.Reconciliation,
            run.Evidence);

}

public sealed record PlanStructureNodeExport(
    string Id,
    int PageNumber,
    PointExport Position,
    PointExport? PositionMillimeters,
    string Kind,
    string TopologyKind,
    int Degree,
    IReadOnlyList<string> IncidentWallRunIds,
    IReadOnlyList<string> EndpointWallRunIds,
    IReadOnlyList<string> InlineWallRunIds,
    IReadOnlyList<string> Directions,
    double Confidence,
    bool RequiresReview,
    PlacementWallJunctionOptimizationExport? Optimization,
    IReadOnlyList<string> Evidence);

public sealed record PlanStructureRoomExport(
    string Id,
    int PageNumber,
    RectExport Bounds,
    RectExport? BoundsMillimeters,
    PointExport Center,
    PointExport? CenterMillimeters,
    IReadOnlyList<PointExport> Boundary,
    IReadOnlyList<PointExport>? BoundaryMillimeters,
    IReadOnlyList<string> BoundaryWallRunIds,
    IReadOnlyList<string> SourceWallIds,
    double DrawingArea,
    double? AreaSquareMeters,
    string? Label,
    string UseKind,
    double Confidence,
    PlacementReliabilityExport Reliability,
    IReadOnlyList<string> Evidence)
{
    public static PlanStructureRoomExport From(
        PlacementRoomExport room,
        IReadOnlyList<PlanStructureWallRunExport> wallRuns)
    {
        var sourceWallIds = room.WallIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var sourceWallIdSet = sourceWallIds.ToHashSet(StringComparer.Ordinal);
        var boundaryWallRunIds = wallRuns
            .Where(run =>
                run.PageNumber == room.PageNumber
                && run.SourceWallIds.Any(sourceWallIdSet.Contains))
            .Select(run => run.Id)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new PlanStructureRoomExport(
            room.Id,
            room.PageNumber,
            room.Bounds,
            room.BoundsMillimeters,
            room.Center,
            room.CenterMillimeters,
            room.Boundary,
            room.BoundaryMillimeters,
            boundaryWallRunIds,
            sourceWallIds,
            room.DrawingArea,
            room.AreaSquareMeters,
            room.Label,
            room.UseKind,
            room.Confidence,
            room.Reliability,
            room.Evidence);
    }
}

public sealed record PlanStructureOpeningExport(
    string Id,
    int PageNumber,
    string Type,
    string Operation,
    string Orientation,
    LineExport CenterLine,
    LineExport? CenterLineMillimeters,
    RectExport Bounds,
    RectExport? BoundsMillimeters,
    double DrawingWidth,
    double? WidthMillimeters,
    string PlacementStatus,
    OpeningPlacementExport? Placement,
    string HingeSide,
    string SwingSide,
    string SwingDirection,
    PointExport? HingePoint,
    PointExport? HingePointMillimeters,
    IReadOnlyList<string> HostWallRunIds,
    IReadOnlyList<string> HostWallOpeningIntervalIds,
    IReadOnlyList<string> SourceHostWallIds,
    IReadOnlyList<string> ConnectedRoomIds,
    double Confidence,
    PlacementReliabilityExport Reliability,
    IReadOnlyList<string> SourcePrimitiveIds,
    IReadOnlyList<string> SourceLayers,
    IReadOnlyList<string> Evidence)
{
    public static PlanStructureOpeningExport From(
        PlacementOpeningExport opening,
        IReadOnlyList<PlanStructureWallRunExport> wallRuns)
    {
        var sourceHostWallIds = opening.HostWallIds
            .Concat(opening.Placement?.AnchorWallIds ?? Array.Empty<string>())
            .Concat(opening.Placement?.HostWallId is { Length: > 0 } hostWallId
                ? new[] { hostWallId }
                : Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var sourceHostWallIdSet = sourceHostWallIds.ToHashSet(StringComparer.Ordinal);
        var intervalMatches = wallRuns
            .SelectMany(run => run.OpeningIntervals
                .Where(interval => string.Equals(interval.OpeningId, opening.Id, StringComparison.Ordinal))
                .Select(interval => (RunId: run.Id, IntervalId: interval.Id)))
            .ToArray();
        var sourceMatchedWallRunIds = wallRuns
            .Where(run =>
                run.PageNumber == opening.PageNumber
                && run.SourceWallIds.Any(sourceHostWallIdSet.Contains))
            .Select(run => run.Id)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var hostWallRunIds = intervalMatches.Length > 0
            ? intervalMatches
                .Select(match => match.RunId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()
            : sourceMatchedWallRunIds;
        var hostWallOpeningIntervalIds = intervalMatches
            .Select(match => match.IntervalId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new PlanStructureOpeningExport(
            opening.Id,
            opening.PageNumber,
            opening.Type,
            opening.Operation,
            opening.Orientation,
            opening.CenterLine,
            opening.CenterLineMillimeters,
            opening.Bounds,
            opening.BoundsMillimeters,
            opening.DrawingWidth,
            opening.WidthMillimeters,
            opening.PlacementStatus,
            opening.Placement,
            opening.HingeSide,
            opening.SwingSide,
            opening.SwingDirection,
            opening.HingePoint,
            opening.HingePointMillimeters,
            hostWallRunIds,
            hostWallOpeningIntervalIds,
            sourceHostWallIds,
            opening.ConnectedRoomIds,
            opening.Confidence,
            opening.Reliability,
            opening.SourcePrimitiveIds,
            opening.SourceLayers,
            opening.Evidence);
    }
}
