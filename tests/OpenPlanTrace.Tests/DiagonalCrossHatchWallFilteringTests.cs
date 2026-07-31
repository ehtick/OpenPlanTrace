namespace OpenPlanTrace.Tests;

public sealed class DiagonalCrossHatchWallFilteringTests
{
    [Fact]
    public async Task WallDetection_SuppressesInteriorCrossHatchButPreservesSeparateDiagonalWallPair()
    {
        var primitives = new List<PlanPrimitive>();
        for (var index = 0; index < 4; index++)
        {
            var offset = 24 + (index * 18);
            primitives.Add(Line(
                $"hatch-up-{index}",
                new PlanPoint(30, offset),
                new PlanPoint(150, offset + 120)));
            primitives.Add(Line(
                $"hatch-down-{index}",
                new PlanPoint(30, offset + 120),
                new PlanPoint(150, offset)));
        }

        primitives.Add(Line(
            "real-diagonal-face-a",
            new PlanPoint(190, 40),
            new PlanPoint(280, 130)));
        primitives.Add(Line(
            "real-diagonal-face-b",
            new PlanPoint(184.343, 45.657),
            new PlanPoint(274.343, 135.657)));
        var document = new PlanDocument(
            "diagonal-cross-hatch-test",
            new[] { new PlanPage(1, new PlanSize(340, 240), primitives) });
        var context = new ScanContext(document, new ScannerOptions());
        context.SheetRegions.Add(new SheetRegion(
            "page:1:main",
            1,
            RegionKind.MainFloorPlan,
            new PlanRect(0, 0, 340, 240),
            Confidence.High));

        await new WallDetectionStage().ExecuteAsync(context, CancellationToken.None);

        var pattern = Assert.Single(
            context.SurfacePatterns,
            candidate => candidate.Kind == SurfacePatternKind.DenseDiagonalCrossHatch);
        Assert.True(pattern.ExcludedFromWallDetection);
        Assert.True(pattern.ExcludedFromStructuralTopology);
        Assert.True(pattern.IntersectionCount >= 6);
        Assert.DoesNotContain(
            context.WallCandidates,
            wall => wall.SourcePrimitiveIds.Any(id => id.StartsWith("hatch-", StringComparison.Ordinal)));
        var structuralDiagonal = Assert.Single(
            context.WallCandidates,
            wall => wall.SourcePrimitiveIds.Contains("real-diagonal-face-a", StringComparer.Ordinal)
                && wall.SourcePrimitiveIds.Contains("real-diagonal-face-b", StringComparer.Ordinal));
        Assert.Equal(WallDetectionKind.ParallelLinePair, structuralDiagonal.DetectionKind);
        Assert.Equal(45, structuralDiagonal.CenterLine.AngleRadians * 180.0 / Math.PI, precision: 2);
    }

    private static LinePrimitive Line(
        string sourceId,
        PlanPoint start,
        PlanPoint end) =>
        new(new PlanLineSegment(start, end))
        {
            SourceId = sourceId,
            Source = new PrimitiveSourceMetadata
            {
                SourceId = sourceId,
                EntityType = "line",
                LineType = "solid",
                Color = "RGB: (0.5, 0.5, 0.5)"
            }
        };
}
