using System.Text.Json;

namespace OpenPlanTrace.Tests;

public sealed class StructuralTopologyFilteringTests
{
    [Fact]
    public async Task ScanAsync_ExcludesObjectLikeWallComponentsFromRoomSolving()
    {
        var document = Document(
            "object-like-component-room-filter",
            Wall("room-top", new PlanPoint(100, 100), new PlanPoint(430, 100)),
            Wall("room-right", new PlanPoint(430, 100), new PlanPoint(430, 320)),
            Wall("room-bottom", new PlanPoint(430, 320), new PlanPoint(100, 320)),
            Wall("room-left", new PlanPoint(100, 320), new PlanPoint(100, 100)),
            Wall("fixture-top", new PlanPoint(520, 160), new PlanPoint(590, 160)),
            Wall("fixture-right", new PlanPoint(590, 160), new PlanPoint(590, 210)),
            Wall("fixture-bottom", new PlanPoint(590, 210), new PlanPoint(520, 210)),
            Wall("fixture-left", new PlanPoint(520, 210), new PlanPoint(520, 160)));

        var result = await new OpenPlanTraceScanner().ScanAsync(document);
        var unfiltered = await new OpenPlanTraceScanner().ScanAsync(
            document,
            new ScannerOptions
            {
                ExcludeObjectLikeWallComponentsFromStructuralTopology = false
            });

        var objectLike = Assert.Single(result.WallGraph.Components, component => component.Kind == WallGraphComponentKind.ObjectLikeIsland);
        var objectLikeWallIds = objectLike.WallIds.ToHashSet(StringComparer.Ordinal);

        Assert.True(objectLike.ExcludedFromStructuralTopology);
        Assert.Contains("excluded from structural room/opening topology solving", objectLike.Evidence);
        Assert.Single(result.Rooms);
        Assert.DoesNotContain(result.Rooms, room => room.WallIds.Intersect(objectLikeWallIds, StringComparer.Ordinal).Any());
        Assert.Contains(result.Diagnostics.Messages, message =>
            message.Code == "rooms.non_structural_wall_components_excluded"
            && message.Properties["excludedComponentCount"] == "1"
            && message.Properties["objectLikeIslandCount"] == "1");

        using var placementJson = JsonDocument.Parse(PlanPlacementJsonExporter.Serialize(result));
        var placementObjectLike = placementJson.RootElement
            .GetProperty("walls")
            .EnumerateArray()
            .Single(wall => wall.GetProperty("sourcePrimitiveIds")
                .EnumerateArray()
                .Any(sourceId => sourceId.GetString() == "fixture-top"));
        var reliability = placementObjectLike.GetProperty("reliability");
        var reasons = reliability.GetProperty("reasons")
            .EnumerateArray()
            .Select(reason => reason.GetString())
            .ToArray();

        Assert.True(reliability.GetProperty("requiresReview").GetBoolean());
        Assert.Contains("wall belongs to compact object-like linework component", reasons);
        Assert.Contains("wall component excluded from structural topology", reasons);

        Assert.Contains(unfiltered.Rooms, room => room.WallIds.Any(objectLikeWallIds.Contains));
    }

    [Fact]
    public async Task ScanAsync_ExcludesObjectLikeWallComponentsFromOpeningSolving()
    {
        var document = Document(
            "object-like-component-opening-filter",
            Wall("room-top", new PlanPoint(100, 100), new PlanPoint(430, 100)),
            Wall("room-right", new PlanPoint(430, 100), new PlanPoint(430, 320)),
            Wall("room-bottom", new PlanPoint(430, 320), new PlanPoint(100, 320)),
            Wall("room-left", new PlanPoint(100, 320), new PlanPoint(100, 100)),
            Wall("fixture-top-left", new PlanPoint(520, 160), new PlanPoint(545, 160)),
            Wall("fixture-top-right", new PlanPoint(560, 160), new PlanPoint(590, 160)),
            Wall("fixture-right", new PlanPoint(590, 160), new PlanPoint(590, 210)),
            Wall("fixture-bottom", new PlanPoint(590, 210), new PlanPoint(520, 210)),
            Wall("fixture-left", new PlanPoint(520, 210), new PlanPoint(520, 160)));

        var result = await new OpenPlanTraceScanner().ScanAsync(document);
        var unfiltered = await new OpenPlanTraceScanner().ScanAsync(
            document,
            new ScannerOptions
            {
                ExcludeObjectLikeWallComponentsFromStructuralTopology = false
            });

        var objectLike = Assert.Single(result.WallGraph.Components, component => component.Kind == WallGraphComponentKind.ObjectLikeIsland);
        var objectLikeWallIds = objectLike.WallIds.ToHashSet(StringComparer.Ordinal);

        Assert.True(objectLike.ExcludedFromStructuralTopology);
        Assert.DoesNotContain(
            unfiltered.WallGraph.Components.Where(component => component.Kind == WallGraphComponentKind.ObjectLikeIsland),
            component => component.ExcludedFromStructuralTopology);
        Assert.DoesNotContain(result.Openings, opening => opening.HostWallIds.Intersect(objectLikeWallIds, StringComparer.Ordinal).Any());
        Assert.Contains(result.Diagnostics.Messages, message =>
            message.Code == "openings.non_structural_wall_components_excluded"
            && message.Properties["excludedComponentCount"] == "1"
            && message.Properties["objectLikeIslandCount"] == "1");

        Assert.Contains(unfiltered.Openings, opening => opening.HostWallIds.Intersect(objectLikeWallIds, StringComparer.Ordinal).Any());
    }

    [Fact]
    public async Task ScanAsync_ExcludesWeakIsolatedWallFragmentsFromStructuralSolving()
    {
        var document = Document(
            "isolated-fragment-structural-filter",
            Wall("room-top", new PlanPoint(100, 100), new PlanPoint(430, 100)),
            Wall("room-right", new PlanPoint(430, 100), new PlanPoint(430, 320)),
            Wall("room-bottom", new PlanPoint(430, 320), new PlanPoint(100, 320)),
            Wall("room-left", new PlanPoint(100, 320), new PlanPoint(100, 100)),
            Wall("detail-fragment", new PlanPoint(520, 160), new PlanPoint(590, 160)));

        var result = await new OpenPlanTraceScanner().ScanAsync(
            document,
            new ScannerOptions
            {
                ExcludeWeakWallFragmentsFromStructuralTopology = true
            });
        var unfiltered = await new OpenPlanTraceScanner().ScanAsync(
            document,
            new ScannerOptions
            {
                ExcludeWeakWallFragmentsFromStructuralTopology = false
            });

        var fragment = Assert.Single(result.WallGraph.Components, component => component.Kind == WallGraphComponentKind.IsolatedFragment);
        var unfilteredFragment = Assert.Single(unfiltered.WallGraph.Components, component => component.Kind == WallGraphComponentKind.IsolatedFragment);

        Assert.True(fragment.ExcludedFromStructuralTopology);
        Assert.False(unfilteredFragment.ExcludedFromStructuralTopology);
        Assert.Contains("detail-fragment", fragment.SourcePrimitiveIds);
        Assert.Contains("excluded from structural room/opening topology solving", fragment.Evidence);
        Assert.Contains(
            fragment.Evidence,
            item => item.Contains("isolated wall fragment with weak topology", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics.Messages, message =>
            message.Code == "wall_graph.weak_fragments.excluded"
            && message.Properties["excludedIsolatedFragmentCount"] == "1");
        Assert.Contains(result.Diagnostics.Messages, message =>
            message.Code == "rooms.non_structural_wall_components_excluded"
            && message.Properties["isolatedFragmentCount"] == "1"
            && message.Properties["excludedWallCount"] == "1");

        using var placementJson = JsonDocument.Parse(PlanPlacementJsonExporter.Serialize(result));
        var placementFragment = placementJson.RootElement
            .GetProperty("walls")
            .EnumerateArray()
            .Single(wall => wall.GetProperty("sourcePrimitiveIds")
                .EnumerateArray()
                .Any(sourceId => sourceId.GetString() == "detail-fragment"));
        var reliability = placementFragment.GetProperty("reliability");
        var reasons = reliability.GetProperty("reasons")
            .EnumerateArray()
            .Select(reason => reason.GetString())
            .ToArray();

        Assert.True(reliability.GetProperty("requiresReview").GetBoolean());
        Assert.Contains("wall belongs to isolated wall graph fragment", reasons);
        Assert.Contains("wall component excluded from structural topology", reasons);
    }

    [Fact]
    public async Task RoutingLayer_SuppressesUnusedIsolatedWallFragmentsWhenStructuralWallsExist()
    {
        var result = await new OpenPlanTraceScanner().ScanAsync(
            Document(
                "isolated-fragment-routing-filter",
                Wall("room-top", new PlanPoint(100, 100), new PlanPoint(430, 100)),
                Wall("room-right", new PlanPoint(430, 100), new PlanPoint(430, 320)),
                Wall("room-bottom", new PlanPoint(430, 320), new PlanPoint(100, 320)),
                Wall("room-left", new PlanPoint(100, 320), new PlanPoint(100, 100)),
                Wall("detail-fragment", new PlanPoint(520, 160), new PlanPoint(590, 160))));

        var detailWall = Assert.Single(result.Walls, wall => wall.SourcePrimitiveIds.Contains("detail-fragment"));
        var fragment = Assert.Single(result.WallGraph.Components, component => component.Kind == WallGraphComponentKind.IsolatedFragment);

        Assert.False(fragment.ExcludedFromStructuralTopology);
        Assert.Contains(detailWall.Id, fragment.WallIds);
        Assert.DoesNotContain(result.RoutingLayer.Barriers, barrier => barrier.SourceId == detailWall.Id);
        Assert.Contains(
            result.RoutingLayer.Evidence,
            item => item == "unused isolated wall fragments suppressed as routing barriers: 1");
    }

    private static PlanDocument Document(string id, params PlanPrimitive[] primitives) =>
        new(
            id,
            new[]
            {
                new PlanPage(1, new PlanSize(760, 460), primitives)
            });

    private static LinePrimitive Wall(string sourceId, PlanPoint start, PlanPoint end) =>
        new(new PlanLineSegment(start, end))
        {
            SourceId = sourceId,
            Layer = "A-WALL",
            Source = new PrimitiveSourceMetadata
            {
                SourceFormat = "test",
                SourceId = sourceId,
                EntityType = "LINE",
                Layer = "A-WALL",
                DrawingSpace = SourceDrawingSpace.Model
            }
        };
}
