using System.Text.Json;

namespace OpenPlanTrace.Tests;

public sealed class CurvedWallDetectionTests
{
    [Fact]
    public async Task WallDetection_PreservesConcentricArcFacesAsReviewableCurvedWall()
    {
        var center = new PlanPoint(150, 140);
        var primitives = new PlanPrimitive[]
        {
            Arc("curve-face-inner", center, 70, Math.PI * 0.15, Math.PI * 0.8),
            Arc("curve-face-outer", center, 80, Math.PI * 0.15, Math.PI * 0.8)
        };
        var document = new PlanDocument(
            "curved-wall-test",
            new[] { new PlanPage(1, new PlanSize(340, 300), primitives) });
        var context = new ScanContext(document, new ScannerOptions());
        context.SheetRegions.Add(new SheetRegion(
            "page:1:main",
            1,
            RegionKind.MainFloorPlan,
            new PlanRect(0, 0, 340, 300),
            Confidence.High));

        await new WallDetectionStage().ExecuteAsync(context, CancellationToken.None);

        var curve = Assert.Single(context.CurvedWallCandidates);
        Assert.Equal(75, curve.CenterlineRadius, precision: 3);
        Assert.Equal(10, curve.Thickness, precision: 3);
        Assert.False(curve.ReadyForCoordinatePlacement);
        Assert.True(curve.ExcludedFromLinearTopology);
        Assert.True(curve.RequiresReview);
        Assert.Equal(
            new[] { "curve-face-inner", "curve-face-outer" },
            curve.SourcePrimitiveIds.Order(StringComparer.Ordinal));
        Assert.Empty(context.WallCandidates);
        Assert.Contains(
            curve.Evidence,
            item => item.Contains("not replaced by tangent extensions", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WallDetection_RejectsSmallNearClosedConcentricSymbolRings()
    {
        var center = new PlanPoint(90, 90);
        var primitives = new PlanPrimitive[]
        {
            CirclePolyline("symbol-ring-inner", center, 7),
            CirclePolyline("symbol-ring-outer", center, 9)
        };
        var document = new PlanDocument(
            "curved-symbol-test",
            new[] { new PlanPage(1, new PlanSize(180, 180), primitives) });
        var context = Context(document, new PlanRect(0, 0, 180, 180));

        await new WallDetectionStage().ExecuteAsync(context, CancellationToken.None);

        Assert.Empty(context.CurvedWallCandidates);
    }

    [Fact]
    public async Task WallDetection_RejectsCompactHighSweepPolylineSymbolRing()
    {
        var center = new PlanPoint(90, 90);
        var start = Math.PI * 0.22;
        var sweep = Math.PI * 1.43;
        var primitives = new PlanPrimitive[]
        {
            ArcPolyline("symbol-arc-inner", center, 12, start, sweep),
            ArcPolyline("symbol-arc-outer", center, 18, start, sweep)
        };
        var document = new PlanDocument(
            "curved-high-sweep-symbol-test",
            new[] { new PlanPage(1, new PlanSize(180, 180), primitives) });
        var context = Context(document, new PlanRect(0, 0, 180, 180));

        await new WallDetectionStage().ExecuteAsync(context, CancellationToken.None);

        Assert.Empty(context.CurvedWallCandidates);
    }

    [Fact]
    public async Task WallDetection_CollapsesDirectionReversedDuplicateArcPairs()
    {
        var center = new PlanPoint(150, 140);
        var start = Math.PI * 0.15;
        var sweep = Math.PI * 0.8;
        var primitives = new PlanPrimitive[]
        {
            Arc("inner-forward", center, 70, start, sweep),
            Arc("outer-forward", center, 80, start, sweep),
            Arc("inner-reverse", center, 70, start + sweep, -sweep),
            Arc("outer-reverse", center, 80, start + sweep, -sweep)
        };
        var document = new PlanDocument(
            "curved-wall-duplicate-test",
            new[] { new PlanPage(1, new PlanSize(340, 300), primitives) });
        var context = Context(document, new PlanRect(0, 0, 340, 300));

        await new WallDetectionStage().ExecuteAsync(context, CancellationToken.None);

        var curve = Assert.Single(context.CurvedWallCandidates);
        Assert.Equal(4, curve.SourcePrimitiveIds.Count);
        Assert.Contains(
            curve.Evidence,
            item => item.Contains("collapsed to one physical", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WallDetection_RejectsConcentricStairArcWithDenseRadialSpokes()
    {
        var center = new PlanPoint(150, 140);
        var primitives = new List<PlanPrimitive>
        {
            Arc("stair-arc-inner", center, 70, 0, Math.PI * 0.45),
            Arc("stair-arc-outer", center, 80, 0, Math.PI * 0.45)
        };
        primitives.AddRange(Enumerable.Range(0, 6).Select(index =>
        {
            var angle = 0.12 + (index * 0.22);
            return (PlanPrimitive)new LinePrimitive(new PlanLineSegment(
                new PlanPoint(
                    center.X + (Math.Cos(angle) * 30),
                    center.Y + (Math.Sin(angle) * 30)),
                new PlanPoint(
                    center.X + (Math.Cos(angle) * 115),
                    center.Y + (Math.Sin(angle) * 115))));
        }));
        var document = new PlanDocument(
            "curved-stair-test",
            new[] { new PlanPage(1, new PlanSize(340, 300), primitives) });
        var context = Context(document, new PlanRect(0, 0, 340, 300));

        await new WallDetectionStage().ExecuteAsync(context, CancellationToken.None);

        Assert.Empty(context.CurvedWallCandidates);
    }

    [Fact]
    public async Task JsonExporter_PreservesCanonicalCurvedWallGeometryAndProvenance()
    {
        var result = await DetectConcentricCurvedWallAsync();

        using var document = JsonDocument.Parse(PlanTraceJsonExporter.Serialize(result));
        var curve = Assert.Single(document.RootElement.GetProperty("curvedWalls").EnumerateArray());

        Assert.Equal("page:1:curved-wall:001", curve.GetProperty("id").GetString());
        Assert.Equal(75, curve.GetProperty("centerlineRadius").GetDouble(), precision: 3);
        Assert.Equal(10, curve.GetProperty("thickness").GetDouble(), precision: 3);
        Assert.Equal("NativeArcPair", curve.GetProperty("sourceKind").GetString());
        Assert.False(curve.GetProperty("readyForCoordinatePlacement").GetBoolean());
        Assert.True(curve.GetProperty("excludedFromLinearTopology").GetBoolean());
        Assert.True(curve.GetProperty("requiresReview").GetBoolean());
        Assert.Equal(2, curve.GetProperty("sourcePrimitiveIds").GetArrayLength());
        Assert.Contains("A-WALL-CURVE", curve.GetProperty("sourceLayers")
            .EnumerateArray()
            .Select(item => item.GetString()));

        var topology = document.RootElement.GetProperty("structuralPathTopology");
        var path = Assert.Single(topology.GetProperty("paths").EnumerateArray());
        Assert.Equal("CircularArc", path.GetProperty("kind").GetString());
        Assert.False(path.GetProperty("readyForCoordinatePlacement").GetBoolean());
        Assert.True(path.GetProperty("requiresReview").GetBoolean());
        Assert.Empty(topology.GetProperty("junctions").EnumerateArray());
    }

    [Fact]
    public async Task GeoJsonExporter_SamplesCurvedWallAsStandardLineStringAndKeepsArcContract()
    {
        var result = await DetectConcentricCurvedWallAsync();
        var expected = Assert.Single(result.CurvedWalls);

        using var document = JsonDocument.Parse(PlanTraceGeoJsonExporter.Serialize(result));
        var feature = Assert.Single(document.RootElement.GetProperty("features")
            .EnumerateArray()
            .Where(item => item.GetProperty("properties").GetProperty("featureType").GetString() == "curvedWall"));
        var geometry = feature.GetProperty("geometry");
        var properties = feature.GetProperty("properties");
        var coordinates = geometry.GetProperty("coordinates").EnumerateArray().ToArray();

        Assert.Equal("LineString", geometry.GetProperty("type").GetString());
        Assert.True(coordinates.Length >= 12);
        Assert.Equal(expected.StartPoint.X, coordinates[0][0].GetDouble(), precision: 4);
        Assert.Equal(expected.StartPoint.Y, coordinates[0][1].GetDouble(), precision: 4);
        Assert.Equal(expected.EndPoint.X, coordinates[^1][0].GetDouble(), precision: 4);
        Assert.Equal(expected.EndPoint.Y, coordinates[^1][1].GetDouble(), precision: 4);
        Assert.Equal("CircularArc", properties.GetProperty("canonicalGeometry").GetString());
        Assert.Equal("SampledLineString", properties.GetProperty("geoJsonApproximation").GetString());
        Assert.Equal(expected.CenterlineRadius, properties.GetProperty("centerlineRadius").GetDouble(), precision: 4);
        Assert.True(properties.GetProperty("excludedFromLinearTopology").GetBoolean());
        Assert.Equal(
            result.StructuralPathTopology.Paths.Single().Id,
            properties.GetProperty("structuralPathId").GetString());
        Assert.Equal(0, properties.GetProperty("connectedStraightPathSupportCount").GetInt32());
        Assert.False(properties.GetProperty("structuralPathReadyForCoordinatePlacement").GetBoolean());
        Assert.True(properties.GetProperty("structuralPathRequiresReview").GetBoolean());
        Assert.Equal(
            StructuralPathTopology.CurrentContractVersion,
            document.RootElement.GetProperty("structuralPathTopologyContractVersion").GetString());
    }

    [Fact]
    public async Task WallQaFocus_RendersAndSnapshotsReviewableCurvedWall()
    {
        var result = await DetectConcentricCurvedWallAsync();
        var expected = Assert.Single(result.CurvedWalls);
        var options = SvgOverlayRenderOptions.ForProfile(SvgOverlayRenderProfile.WallQaFocus);

        var svg = PlanOverlaySvgRenderer.RenderPage(result, 1, options);
        var snapshot = PlanOverlaySnapshot.From(result, svgOptions: options);
        var page = Assert.Single(snapshot.Pages);
        var layer = Assert.Single(page.Layers, item => item.Name == "curvedWalls");

        Assert.Contains("id=\"curved-walls\"", svg);
        Assert.Contains("class=\"curved-wall-review\"", svg);
        Assert.Contains("Magenta dashed = curved wall review candidates", svg);
        Assert.Equal(1, layer.Count);
        Assert.Equal(expected.Bounds.X, layer.Bounds.X, precision: 3);
        Assert.Equal(expected.Bounds.Y, layer.Bounds.Y, precision: 3);
        Assert.Contains("curvedWalls", page.VisibleLayerNames);
    }

    private static async Task<PlanScanResult> DetectConcentricCurvedWallAsync()
    {
        var center = new PlanPoint(150, 140);
        var document = new PlanDocument(
            "curved-wall-export-test",
            new[]
            {
                new PlanPage(
                    1,
                    new PlanSize(340, 300),
                    new PlanPrimitive[]
                    {
                        Arc("curve-face-inner", center, 70, Math.PI * 0.15, Math.PI * 0.8),
                        Arc("curve-face-outer", center, 80, Math.PI * 0.15, Math.PI * 0.8)
                    })
            });
        var context = Context(document, new PlanRect(0, 0, 340, 300));

        await new WallDetectionStage().ExecuteAsync(context, CancellationToken.None);
        await new StructuralPathTopologyStage().ExecuteAsync(context, CancellationToken.None);

        return context.ToResult();
    }

    private static ScanContext Context(PlanDocument document, PlanRect bounds)
    {
        var context = new ScanContext(document, new ScannerOptions());
        context.SheetRegions.Add(new SheetRegion(
            "page:1:main",
            1,
            RegionKind.MainFloorPlan,
            bounds,
            Confidence.High));
        return context;
    }

    private static PolylinePrimitive CirclePolyline(
        string sourceId,
        PlanPoint center,
        double radius) =>
        new(
            Enumerable.Range(0, 33)
                .Select(index =>
                {
                    var angle = Math.PI * 2.0 * index / 32.0;
                    return new PlanPoint(
                        center.X + (Math.Cos(angle) * radius),
                        center.Y + (Math.Sin(angle) * radius));
                })
                .ToArray(),
            Closed: true)
        {
            SourceId = sourceId,
            Source = new PrimitiveSourceMetadata
            {
                SourceId = sourceId,
                EntityType = "polyline",
                LineType = "solid",
                Properties = new Dictionary<string, string>
                {
                    ["isFilled"] = "False"
                }
            }
        };

    private static PolylinePrimitive ArcPolyline(
        string sourceId,
        PlanPoint center,
        double radius,
        double start,
        double sweep) =>
        new(
            Enumerable.Range(0, 25)
                .Select(index =>
                {
                    var angle = start + (sweep * index / 24.0);
                    return new PlanPoint(
                        center.X + (Math.Cos(angle) * radius),
                        center.Y + (Math.Sin(angle) * radius));
                })
                .ToArray(),
            Closed: false)
        {
            SourceId = sourceId,
            Source = new PrimitiveSourceMetadata
            {
                SourceId = sourceId,
                EntityType = "polyline",
                LineType = "solid",
                Properties = new Dictionary<string, string>
                {
                    ["isFilled"] = "False"
                }
            }
        };

    private static ArcPrimitive Arc(
        string sourceId,
        PlanPoint center,
        double radius,
        double start,
        double sweep) =>
        new(center, radius, start, sweep)
        {
            SourceId = sourceId,
            Layer = "A-WALL-CURVE",
            Source = new PrimitiveSourceMetadata
            {
                SourceId = sourceId,
                EntityType = "arc",
                LineType = "solid"
            }
        };
}
