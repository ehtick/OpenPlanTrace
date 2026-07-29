using System.Text.Json;
using OpenPlanTrace.Export;

namespace OpenPlanTrace.Tests;

public sealed class WallGeometryBenchmarkTests
{
    [Fact]
    public async Task Evaluate_WallTargetMatchesReversedExactCenterLine()
    {
        var result = await CreateScanResultAsync();
        var wall = result.Walls.OrderByDescending(candidate => candidate.DrawingLength).First();
        var reversed = new PlanLineSegment(wall.CenterLine.End, wall.CenterLine.Start);

        var metric = EvaluateWallTarget(result, ExactTarget(wall) with { CenterLine = reversed });

        Assert.Equal(1, metric.MatchedCount);
        Assert.Equal(1, metric.ExpectedCount);
        Assert.Equal(1, metric.Recall);
        Assert.Contains("centerline angle", metric.Matches[0].Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Evaluate_WallTargetRejectsPerpendicularOffset()
    {
        var result = await CreateScanResultAsync();
        var wall = result.Walls.OrderByDescending(candidate => candidate.DrawingLength).First();
        var line = wall.CenterLine;
        var length = Math.Max(line.Length, 0.001);
        var offsetX = -(line.End.Y - line.Start.Y) / length * 20;
        var offsetY = (line.End.X - line.Start.X) / length * 20;
        var shifted = new PlanLineSegment(
            new PlanPoint(line.Start.X + offsetX, line.Start.Y + offsetY),
            new PlanPoint(line.End.X + offsetX, line.End.Y + offsetY));

        var metric = EvaluateWallTarget(result, ExactTarget(wall) with
        {
            CenterLine = shifted,
            MaxLineDistance = 1
        });

        Assert.Equal(0, metric.MatchedCount);
        Assert.Equal(1, metric.MissedCount);
    }

    [Fact]
    public async Task Evaluate_WallTargetRejectsEndpointOverrun()
    {
        var result = await CreateScanResultAsync();
        var wall = result.Walls.OrderByDescending(candidate => candidate.DrawingLength).First();
        var line = wall.CenterLine;
        var length = Math.Max(line.Length, 0.001);
        var unitX = (line.End.X - line.Start.X) / length;
        var unitY = (line.End.Y - line.Start.Y) / length;
        var overrun = new PlanLineSegment(
            new PlanPoint(line.Start.X - (unitX * 20), line.Start.Y - (unitY * 20)),
            new PlanPoint(line.End.X + (unitX * 20), line.End.Y + (unitY * 20)));

        var metric = EvaluateWallTarget(result, ExactTarget(wall) with
        {
            CenterLine = overrun,
            MaxEndpointDistance = 2
        });

        Assert.Equal(0, metric.MatchedCount);
        Assert.Equal(1, metric.MissedCount);
    }

    [Fact]
    public async Task Evaluate_WallTargetRejectsWrongWallType()
    {
        var result = await CreateScanResultAsync();
        var wall = result.Walls.OrderByDescending(candidate => candidate.DrawingLength).First();
        var wrongType = wall.WallType == WallType.Exterior ? WallType.Interior : WallType.Exterior;

        var metric = EvaluateWallTarget(result, ExactTarget(wall) with { WallType = wrongType });

        Assert.Equal(0, metric.MatchedCount);
        Assert.Equal(1, metric.MissedCount);
    }

    [Fact]
    public async Task DraftBuilder_EmitsExactWallGeometryCriteria()
    {
        var result = await CreateScanResultAsync();
        var json = PlanTraceJsonExporter.Serialize(
            result,
            new PlanTraceJsonExportOptions { WriteIndented = false });
        using var document = JsonDocument.Parse(json);

        var manifest = BenchmarkManifestDraftBuilder.FromScanJson(
            document,
            new BenchmarkManifestDraftOptions
            {
                FixtureId = "wall-geometry-draft",
                SourcePath = "wall-geometry.plan",
                MaxTargetsPerDetector = 4
            });
        var targets = Assert.Single(manifest.Fixtures).Expectations.WallMetrics.Targets;

        Assert.NotEmpty(targets);
        Assert.All(targets, target =>
        {
            Assert.NotNull(target.CenterLine);
            Assert.Equal(2, target.MaxLineDistance);
            Assert.Equal(6, target.MaxEndpointDistance);
            Assert.Equal(3, target.MaxAngularDifferenceDegrees);
            Assert.Equal(0.90, target.MinLengthOverlapRatio);
        });
    }

    private static BenchmarkDetectorMetrics EvaluateWallTarget(
        PlanScanResult result,
        BenchmarkDetectionTarget target)
    {
        var fixture = new BenchmarkFixture
        {
            Id = "exact-wall-geometry",
            SourcePath = "exact-wall-geometry.plan",
            Expectations = new BenchmarkExpectations
            {
                WallMetrics = new BenchmarkDetectorMetricExpectations
                {
                    Targets = new[] { target },
                    MinRecall = 1
                }
            }
        };

        return PlanBenchmarkEvaluator.Evaluate(fixture, result, TimeSpan.Zero)
            .Metrics
            .Single(metric => metric.Detector == "walls");
    }

    private static BenchmarkDetectionTarget ExactTarget(WallSegment wall) =>
        new()
        {
            Id = "wall-target",
            PageNumber = wall.PageNumber,
            Bounds = wall.Bounds,
            CenterLine = wall.CenterLine,
            MaxLineDistance = 1,
            MaxEndpointDistance = 2,
            MaxAngularDifferenceDegrees = 1,
            MinLengthOverlapRatio = 0.98,
            WallType = wall.WallType
        };

    private static async Task<PlanScanResult> CreateScanResultAsync()
    {
        var document = new PlanDocument(
            "wall-geometry-benchmark",
            new[]
            {
                new PlanPage(
                    1,
                    new PlanSize(500, 400),
                    new PlanPrimitive[]
                    {
                        WallLine("wall-top", new PlanPoint(100, 100), new PlanPoint(320, 100)),
                        WallLine("wall-right", new PlanPoint(320, 100), new PlanPoint(320, 280)),
                        WallLine("wall-bottom", new PlanPoint(320, 280), new PlanPoint(100, 280)),
                        WallLine("wall-left", new PlanPoint(100, 280), new PlanPoint(100, 100)),
                        new TextPrimitive("ROOM", new PlanRect(175, 175, 50, 16))
                    })
            });

        return await new OpenPlanTraceScanner().ScanAsync(document);
    }

    private static LinePrimitive WallLine(string sourceId, PlanPoint start, PlanPoint end) =>
        new(new PlanLineSegment(start, end))
        {
            SourceId = sourceId,
            Layer = "Wall",
            Source = new PrimitiveSourceMetadata
            {
                SourceFormat = "test",
                SourceId = sourceId,
                EntityType = "LINE",
                Layer = "Wall",
                LineWeight = 1.0,
                DrawingSpace = SourceDrawingSpace.Model
            }
        };
}
