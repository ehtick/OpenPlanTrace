using OpenPlanTrace;

namespace OpenPlanTrace.Tests;

public sealed class MixedStructuralPathTopologyBuilderTests
{
    [Fact]
    public void Build_ClassifiesOpposedEndpointDirectionsAsTangent()
    {
        var line = Run("line-a", new PlanPoint(10, -10), new PlanPoint(10, 0));
        var curve = Arc("arc-a", 0, 0, 10, 0, Math.PI / 2.0);

        var topology = Build(new[] { line }, new[] { curve });

        var junction = Assert.Single(topology.Junctions);
        Assert.Equal(StructuralPathJunctionKind.Tangent, junction.Kind);
        Assert.Equal(0, junction.EndpointDistance, 6);
        Assert.Equal(180, junction.DirectionAngleDegrees, 6);
        Assert.Equal(0, junction.TangentDeviationDegrees, 6);
        Assert.True(junction.RequiresReview);
        var curvedPath = Assert.Single(topology.Paths.Where(path => path.Kind == StructuralPathKind.CircularArc));
        Assert.Equal(1, curvedPath.ConnectedStraightPathSupportCount);
        Assert.False(curvedPath.ReadyForCoordinatePlacement);
        Assert.True(curvedPath.RequiresReview);
    }

    [Fact]
    public void Build_ClassifiesPerpendicularEndpointDirectionsAsCorner()
    {
        var line = Run("line-a", new PlanPoint(0, 0), new PlanPoint(10, 0));
        var curve = Arc("arc-a", 0, 0, 10, 0, Math.PI / 2.0);

        var topology = Build(new[] { line }, new[] { curve });

        var junction = Assert.Single(topology.Junctions);
        Assert.Equal(StructuralPathJunctionKind.Corner, junction.Kind);
        Assert.Equal(90, junction.DirectionAngleDegrees, 6);
    }

    [Fact]
    public void Build_DoesNotConnectLineEndpointToArcInterior()
    {
        var arcMidpoint = new PlanPoint(Math.Sqrt(50), Math.Sqrt(50));
        var line = Run("line-a", arcMidpoint.Translate(0, -10), arcMidpoint);
        var curve = Arc("arc-a", 0, 0, 10, 0, Math.PI / 2.0);

        var topology = Build(new[] { line }, new[] { curve });

        Assert.Empty(topology.Junctions);
        var curvedPath = Assert.Single(topology.Paths.Where(path => path.Kind == StructuralPathKind.CircularArc));
        Assert.Empty(curvedPath.ConnectedStraightPathIds);
    }

    [Fact]
    public void Build_RejectsCoincidentBacktrackingEndpointRelation()
    {
        var line = Run("line-a", new PlanPoint(10, 0), new PlanPoint(10, 10));
        var curve = Arc("arc-a", 0, 0, 10, 0, Math.PI / 2.0);

        var topology = Build(new[] { line }, new[] { curve });

        Assert.Empty(topology.Junctions);
    }

    [Fact]
    public void Build_PreservesExactSourceGeometryAndUsesAdvisoryJunctionPosition()
    {
        var originalLine = new PlanLineSegment(new PlanPoint(10.25, -10), new PlanPoint(10.25, 0.25));
        var line = Run("line-a", originalLine.Start, originalLine.End);
        var curve = Arc("arc-a", 0, 0, 10, 0, Math.PI / 2.0);

        var topology = Build(new[] { line }, new[] { curve });

        var linePath = Assert.Single(topology.Paths.Where(path => path.Kind == StructuralPathKind.Line));
        var lineGeometry = Assert.IsType<StructuralLinePathGeometry>(linePath.Geometry);
        Assert.Equal(originalLine, lineGeometry.CenterLine);
        var arcPath = Assert.Single(topology.Paths.Where(path => path.Kind == StructuralPathKind.CircularArc));
        var arcGeometry = Assert.IsType<StructuralCircularArcPathGeometry>(arcPath.Geometry);
        Assert.Equal(curve.Center, arcGeometry.Center);
        Assert.Equal(curve.CenterlineRadius, arcGeometry.Radius);
        Assert.Equal(curve.StartAngleRadians, arcGeometry.StartAngleRadians);
        Assert.Equal(curve.SweepAngleRadians, arcGeometry.SweepAngleRadians);

        var junction = Assert.Single(topology.Junctions);
        Assert.Equal(originalLine.End, junction.FirstEndpoint.Position);
        Assert.Equal(curve.StartPoint, junction.SecondEndpoint.Position);
        Assert.NotEqual(junction.FirstEndpoint.Position, junction.ProposedPosition);
        Assert.NotEqual(junction.SecondEndpoint.Position, junction.ProposedPosition);
    }

    [Fact]
    public void Build_ProducesStableIdsIndependentOfInputOrder()
    {
        var firstLine = Run("line-a", new PlanPoint(10, -10), new PlanPoint(10, 0));
        var secondLine = Run("line-b", new PlanPoint(0, 20), new PlanPoint(0, 10));
        var firstArc = Arc("arc-a", 0, 0, 10, 0, Math.PI / 2.0);
        var secondArc = Arc("arc-b", 10, 10, 10, Math.PI, Math.PI / 2.0);

        var first = Build(new[] { firstLine, secondLine }, new[] { firstArc, secondArc });
        var second = Build(new[] { secondLine, firstLine }, new[] { secondArc, firstArc });

        Assert.Equal(first.Paths.Select(path => path.Id), second.Paths.Select(path => path.Id));
        Assert.Equal(first.Junctions.Select(junction => junction.Id), second.Junctions.Select(junction => junction.Id));
    }

    [Fact]
    public void Build_PreservesClockwiseArcGeometryAndBounds()
    {
        var line = Run("line-a", new PlanPoint(10, 10), new PlanPoint(10, 0));
        var curve = Arc("arc-a", 0, 0, 10, 0, -Math.PI / 2.0);

        var topology = Build(new[] { line }, new[] { curve });

        var path = Assert.Single(topology.Paths.Where(item => item.Kind == StructuralPathKind.CircularArc));
        var geometry = Assert.IsType<StructuralCircularArcPathGeometry>(path.Geometry);
        Assert.Equal(new PlanPoint(10, 0), geometry.StartPoint);
        Assert.Equal(0, geometry.EndPoint.X, 6);
        Assert.Equal(-10, geometry.EndPoint.Y, 6);
        Assert.Equal(0, geometry.Bounds.X, 6);
        Assert.Equal(-10, geometry.Bounds.Y, 6);
        Assert.Equal(10, geometry.Bounds.Width, 6);
        Assert.Equal(10, geometry.Bounds.Height, 6);
        Assert.Equal(Math.PI * 5, geometry.DrawingLength, 6);

        var junction = Assert.Single(topology.Junctions);
        Assert.Equal(StructuralPathJunctionKind.Tangent, junction.Kind);
        Assert.Equal(180, junction.DirectionAngleDegrees, 6);
    }

    private static StructuralPathTopology Build(
        IReadOnlyList<StructuralWallRun> lines,
        IReadOnlyList<CurvedWallCandidate> curves) =>
        MixedStructuralPathTopologyBuilder.Build(
            StructuralPlanSolution.Empty with { WallRuns = lines },
            curves,
            Array.Empty<WallSegment>(),
            PlanCalibration.Empty,
            new StructuralSolverOptions());

    private static StructuralWallRun Run(string id, PlanPoint start, PlanPoint end) =>
        new(
            id,
            1,
            new PlanLineSegment(start, end),
            1,
            WallType.Interior,
            Confidence.High,
            new[] { $"candidate-{id}" },
            new[] { $"wall-{id}" },
            Array.Empty<string>(),
            new[] { $"primitive-{id}" },
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { "test line" })
        {
            Reliability = new StructuralWallRunReliability(
                ReadyForCoordinatePlacement: true,
                RequiresReview: false,
                Confidence: 0.9,
                Reasons: Array.Empty<string>())
        };

    private static CurvedWallCandidate Arc(
        string id,
        double centerX,
        double centerY,
        double radius,
        double startAngle,
        double sweepAngle)
    {
        var center = new PlanPoint(centerX, centerY);
        return new CurvedWallCandidate(
            id,
            1,
            center,
            radius,
            startAngle,
            sweepAngle,
            1,
            new PlanRect(centerX - radius, centerY - radius, radius * 2, radius * 2),
            "region-1",
            CurvedWallSourceKind.NativeArcPair,
            1,
            0,
            ReadyForCoordinatePlacement: false,
            ExcludedFromLinearTopology: true,
            Confidence.High,
            RequiresReview: true,
            new[] { $"primitive-{id}" },
            new[] { "test arc" });
    }
}
