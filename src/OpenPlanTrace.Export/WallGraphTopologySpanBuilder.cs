using System.Globalization;

namespace OpenPlanTrace.Export;

public sealed record WallGraphTopologySpan(
    string Id,
    int PageNumber,
    string WallId,
    string FromNodeId,
    string ToNodeId,
    PlanLineSegment CenterLine,
    PlanRect Bounds,
    double DrawingLength,
    double? SourceWallStartOffsetDrawingUnits,
    double? SourceWallEndOffsetDrawingUnits,
    double? SourceWallProjectedLengthDrawingUnits,
    double? SourceWallStartParameter,
    double? SourceWallEndParameter,
    double? SourceWallCenterParameter,
    double? SourceWallStartProjectionDistanceDrawingUnits,
    double? SourceWallEndProjectionDistanceDrawingUnits,
    double Thickness,
    Confidence Confidence,
    IReadOnlyList<string> SourcePrimitiveIds,
    IReadOnlyList<string> SourceWallGraphEdgeIds,
    IReadOnlyList<string> Evidence,
    WallSegment? SourceWall);

internal static class WallGraphTopologySpanBuilder
{
    private const double MaxDominantAxisSkewRatio = 0.04;
    private const double MaxDominantAxisSkewDrawingUnits = 8.0;

    public static IReadOnlyList<WallGraphTopologySpan> Build(
        WallGraph graph,
        IReadOnlyList<WallSegment> walls)
    {
        if (graph.Edges.Count == 0 || graph.Nodes.Count == 0)
        {
            return Array.Empty<WallGraphTopologySpan>();
        }

        var nodesById = graph.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var wallsById = walls.ToDictionary(wall => wall.Id, StringComparer.Ordinal);
        var spans = new List<WallGraphTopologySpan>();

        foreach (var edge in graph.Edges)
        {
            if (!nodesById.TryGetValue(edge.FromNodeId, out var from)
                || !nodesById.TryGetValue(edge.ToNodeId, out var to))
            {
                continue;
            }

            var rawCenterLine = new PlanLineSegment(from.Position, to.Position);
            wallsById.TryGetValue(edge.WallId, out var wall);
            var projection = ProjectSpanToSourceAxis(rawCenterLine, wall);
            var centerLine = projection.CenterLine;
            if (centerLine.Length <= 0.001)
            {
                continue;
            }

            var thickness = wall?.Thickness ?? 1.0;
            var bounds = centerLine.Bounds.Inflate(Math.Max(thickness / 2.0, 0.5));
            var sourcePrimitiveIds = wall?.SourcePrimitiveIds ?? Array.Empty<string>();
            var placement = SourceWallPlacement(centerLine, wall);
            var evidence = new List<string>
            {
                $"wall graph topology span from {edge.FromNodeId} to {edge.ToNodeId}",
                $"source wall {edge.WallId}"
            };

            if (wall is not null)
            {
                evidence.AddRange(wall.Evidence);
            }

            if (projection.AxisShift > 0.001)
            {
                evidence.Add(
                    $"topology span projected back to source wall axis by up to {Format(projection.AxisShift)} drawing units");
            }

            if (placement is not null)
            {
                evidence.Add(
                    $"span projects to source wall offsets {Format(placement.StartOffset)} -> {Format(placement.EndOffset)} drawing units");
                evidence.Add(
                    $"span endpoint projection distances {Format(placement.StartProjectionDistance)} and {Format(placement.EndProjectionDistance)} drawing units");
            }

            spans.Add(new WallGraphTopologySpan(
                edge.Id,
                edge.PageNumber,
                edge.WallId,
                edge.FromNodeId,
                edge.ToNodeId,
                centerLine,
                bounds,
                centerLine.Length,
                placement?.StartOffset,
                placement?.EndOffset,
                placement?.ProjectedLength,
                placement?.StartParameter,
                placement?.EndParameter,
                placement?.CenterParameter,
                placement?.StartProjectionDistance,
                placement?.EndProjectionDistance,
                thickness,
                edge.Confidence,
                sourcePrimitiveIds,
                [edge.Id],
                evidence.Distinct(StringComparer.Ordinal).ToArray(),
                wall));
        }

        return spans;
    }

    private static SourceAxisProjection ProjectSpanToSourceAxis(
        PlanLineSegment span,
        WallSegment? wall)
    {
        if (wall is null || wall.CenterLine.Length <= 0.001)
        {
            return new SourceAxisProjection(span, 0);
        }

        var orientation = ResolveDominantOrthogonalOrientation(wall.CenterLine);
        if (orientation == PlacementRunOrientation.Unknown)
        {
            return new SourceAxisProjection(span, 0);
        }

        var sourceLine = wall.CenterLine;
        var startParameter = Math.Clamp(sourceLine.ProjectParameter(span.Start), 0, 1);
        var endParameter = Math.Clamp(sourceLine.ProjectParameter(span.End), 0, 1);
        var sourceStart = sourceLine.PointAt(startParameter);
        var sourceEnd = sourceLine.PointAt(endParameter);
        var centerAxis = orientation == PlacementRunOrientation.Horizontal
            ? (sourceLine.Start.Y + sourceLine.End.Y) / 2.0
            : (sourceLine.Start.X + sourceLine.End.X) / 2.0;
        var centerLine = orientation == PlacementRunOrientation.Horizontal
            ? new PlanLineSegment(
                new PlanPoint(sourceStart.X, centerAxis),
                new PlanPoint(sourceEnd.X, centerAxis))
            : new PlanLineSegment(
                new PlanPoint(centerAxis, sourceStart.Y),
                new PlanPoint(centerAxis, sourceEnd.Y));

        return centerLine.Length <= 0.001
            ? new SourceAxisProjection(centerLine, 0)
            : new SourceAxisProjection(centerLine, MaxSourceAxisShift(span, centerLine, orientation));
    }

    private static PlacementRunOrientation ResolveDominantOrthogonalOrientation(PlanLineSegment line)
    {
        if (line.IsHorizontal())
        {
            return PlacementRunOrientation.Horizontal;
        }

        if (line.IsVertical())
        {
            return PlacementRunOrientation.Vertical;
        }

        var dx = Math.Abs(line.End.X - line.Start.X);
        var dy = Math.Abs(line.End.Y - line.Start.Y);
        var dominant = Math.Max(dx, dy);
        var minor = Math.Min(dx, dy);
        if (dominant <= 0.001
            || minor > MaxDominantAxisSkewDrawingUnits
            || minor / dominant > MaxDominantAxisSkewRatio)
        {
            return PlacementRunOrientation.Unknown;
        }

        return dx >= dy
            ? PlacementRunOrientation.Horizontal
            : PlacementRunOrientation.Vertical;
    }

    private static double MaxSourceAxisShift(
        PlanLineSegment source,
        PlanLineSegment projected,
        PlacementRunOrientation orientation)
    {
        return orientation == PlacementRunOrientation.Horizontal
            ? Math.Max(
                Math.Abs(source.Start.Y - projected.Start.Y),
                Math.Abs(source.End.Y - projected.End.Y))
            : Math.Max(
                Math.Abs(source.Start.X - projected.Start.X),
                Math.Abs(source.End.X - projected.End.X));
    }

    private static SourceWallSpanPlacement? SourceWallPlacement(
        PlanLineSegment span,
        WallSegment? wall)
    {
        if (wall is null || wall.CenterLine.Length <= 0.001)
        {
            return null;
        }

        var sourceLine = wall.CenterLine;
        var sourceLength = sourceLine.Length;
        var startParameter = sourceLine.ProjectParameter(span.Start);
        var endParameter = sourceLine.ProjectParameter(span.End);
        var centerParameter = sourceLine.ProjectParameter(span.Midpoint);
        var startOffset = startParameter * sourceLength;
        var endOffset = endParameter * sourceLength;

        return new SourceWallSpanPlacement(
            startOffset,
            endOffset,
            Math.Abs(endOffset - startOffset),
            startParameter,
            endParameter,
            centerParameter,
            sourceLine.DistanceToPoint(span.Start),
            sourceLine.DistanceToPoint(span.End));
    }

    private static string Format(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

    private sealed record SourceWallSpanPlacement(
        double StartOffset,
        double EndOffset,
        double ProjectedLength,
        double StartParameter,
        double EndParameter,
        double CenterParameter,
        double StartProjectionDistance,
        double EndProjectionDistance);

    private sealed record SourceAxisProjection(
        PlanLineSegment CenterLine,
        double AxisShift);

    private enum PlacementRunOrientation
    {
        Unknown,
        Horizontal,
        Vertical
    }
}
