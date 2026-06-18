namespace OpenPlanTrace.Tests;

public sealed class WallTypeRefinementTests
{
    [Fact]
    public async Task WallTypeRefinement_DoesNotFlipInteriorWallToExteriorFromOneSidedRoomEvidence()
    {
        var wall = new WallSegment(
            "wall-interior-one-sided-room",
            1,
            new PlanLineSegment(new PlanPoint(100, 100), new PlanPoint(100, 300)),
            6,
            Confidence.High)
        {
            WallType = WallType.Interior,
            Evidence = new[]
            {
                "wall type interior: supported wall evidence inside exterior envelope"
            }
        };
        var room = new RoomRegion(
            "room-on-one-side",
            1,
            new PlanRect(106, 80, 120, 240),
            new[]
            {
                new PlanPoint(106, 80),
                new PlanPoint(226, 80),
                new PlanPoint(226, 320),
                new PlanPoint(106, 320)
            },
            Array.Empty<string>(),
            Confidence.High);
        var context = new ScanContext(
            new PlanDocument(
                "one-sided-interior-wall",
                new[]
                {
                    new PlanPage(1, new PlanSize(400, 400), Array.Empty<PlanPrimitive>())
                }),
            new ScannerOptions());

        context.Walls.Add(wall);
        context.Rooms.Add(room);
        context.WallGraph = new WallGraph(
            Array.Empty<WallNode>(),
            Array.Empty<WallEdge>(),
            new[]
            {
                new WallGraphComponent(
                    "component-main",
                    1,
                    WallGraphComponentKind.MainStructural,
                    wall.Bounds,
                    new[] { wall.Id },
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    wall.SourcePrimitiveIds,
                    wall.DrawingLength,
                    Confidence.High,
                    Array.Empty<string>())
            });

        await new WallTypeRefinementStage().ExecuteAsync(context, CancellationToken.None);

        var refined = Assert.Single(context.Walls);
        Assert.Equal(WallType.Interior, refined.WallType);
        Assert.Contains(
            refined.Evidence,
            item => item.Contains("one-sided room evidence did not override interior", StringComparison.OrdinalIgnoreCase));
    }
}
