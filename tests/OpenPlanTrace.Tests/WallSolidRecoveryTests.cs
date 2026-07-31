namespace OpenPlanTrace.Tests;

public sealed class WallSolidRecoveryTests
{
    [Fact]
    public async Task WallDetection_AddsFilledWallSolidEvidenceToLongThinFilledRectangle()
    {
        var primitive = FilledRectangle(
            "filled-wall-solid",
            new PlanRect(40, 100, 180, 8),
            "RGB: (0,5, 0,5, 0,5)");
        var context = ContextFor(primitive);

        await new WallDetectionStage().ExecuteAsync(context, CancellationToken.None);

        var wall = Assert.Single(
            context.WallCandidates,
            candidate => candidate.SourcePrimitiveIds.Contains("filled-wall-solid", StringComparer.Ordinal)
                && candidate.Evidence.Any(item => item.Contains("filled wall-solid primitive", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(WallDetectionKind.ParallelLinePair, wall.DetectionKind);
        Assert.Equal(8, wall.Thickness, precision: 3);
        Assert.Contains(
            wall.Evidence,
            item => item.Contains("filled closed vector wall body", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            context.Diagnostics.Build().Messages,
            diagnostic => diagnostic.Code == "walls.filled_solids.recovered"
                && diagnostic.Properties["filledWallSolidCount"] == "1");
    }

    [Fact]
    public async Task WallDetection_DoesNotAddFilledWallSolidEvidenceToWhiteFilledRectangle()
    {
        var primitive = FilledRectangle(
            "white-filled-fixture",
            new PlanRect(40, 100, 180, 8),
            "RGB: (1, 1, 1)");
        var context = ContextFor(primitive);

        await new WallDetectionStage().ExecuteAsync(context, CancellationToken.None);

        Assert.DoesNotContain(
            context.WallCandidates,
            candidate => candidate.SourcePrimitiveIds.Contains("white-filled-fixture", StringComparer.Ordinal)
                && candidate.Evidence.Any(item => item.Contains("filled wall-solid primitive", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task WallDetection_UsesParallelPolygonFacesForRotatedFilledWallSolid()
    {
        var primitive = FilledPolyline(
            "rotated-filled-wall-solid",
            new[]
            {
                new PlanPoint(53.5355, 46.4645),
                new PlanPoint(194.9569, 187.8859),
                new PlanPoint(187.8859, 194.9569),
                new PlanPoint(46.4645, 53.5355)
            },
            "RGB: (0, 0, 0)");
        var context = ContextFor(primitive);

        await new WallDetectionStage().ExecuteAsync(context, CancellationToken.None);

        var wall = Assert.Single(
            context.WallCandidates,
            candidate => candidate.SourcePrimitiveIds.Contains("rotated-filled-wall-solid", StringComparer.Ordinal)
                && candidate.Evidence.Any(item => item.Contains("filled wall-solid primitive", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(WallDetectionKind.ParallelLinePair, wall.DetectionKind);
        Assert.Equal(10, wall.Thickness, precision: 2);
        Assert.Equal(200, wall.DrawingLength, precision: 2);
        Assert.Equal(45, wall.CenterLine.AngleRadians * 180.0 / Math.PI, precision: 2);
        Assert.Contains(
            wall.Evidence,
            item => item.Contains("rotated filled wall-solid centerline", StringComparison.OrdinalIgnoreCase));
    }

    private static ScanContext ContextFor(params PlanPrimitive[] primitives)
    {
        var document = new PlanDocument(
            "filled-wall-solid-test",
            new[]
            {
                new PlanPage(1, new PlanSize(320, 260), primitives)
            });
        var context = new ScanContext(document, new ScannerOptions());
        context.SheetRegions.Add(new SheetRegion(
            "page:1:main",
            1,
            RegionKind.MainFloorPlan,
            new PlanRect(0, 0, 320, 260),
            Confidence.High));
        return context;
    }

    private static RectanglePrimitive FilledRectangle(
        string sourceId,
        PlanRect bounds,
        string color) =>
        new(bounds)
        {
            SourceId = sourceId,
            Source = new PrimitiveSourceMetadata
            {
                SourceId = sourceId,
                EntityType = "rectangle",
                Color = color,
                LineType = "solid",
                Properties = new Dictionary<string, string>
                {
                    ["isFilled"] = "True",
                    ["isClipping"] = "False"
                }
            }
        };

    private static PolylinePrimitive FilledPolyline(
        string sourceId,
        IReadOnlyList<PlanPoint> points,
        string color) =>
        new(points, Closed: true)
        {
            SourceId = sourceId,
            Source = new PrimitiveSourceMetadata
            {
                SourceId = sourceId,
                EntityType = "polyline",
                Color = color,
                LineType = "solid",
                Properties = new Dictionary<string, string>
                {
                    ["isFilled"] = "True",
                    ["isClipping"] = "False"
                }
            }
        };
}
