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

    [Fact]
    public async Task WallTypeRefinement_OverridesExteriorGuessForSharedRoomAdjacency()
    {
        var wall = new WallSegment(
            "wall-shared-room-boundary-misclassified-exterior",
            1,
            new PlanLineSegment(new PlanPoint(100, 100), new PlanPoint(100, 300)),
            6,
            Confidence.High)
        {
            WallType = WallType.Exterior,
            Evidence = new[]
            {
                "wall type exterior: near detected floorplan/wall envelope or local outer boundary"
            }
        };
        var context = CreateContext("shared-room-boundary-exterior-override");
        context.Walls.Add(wall);
        context.WallGraph = GraphFor(wall);
        context.RoomAdjacencyGraph = new RoomAdjacencyGraph(
            new[]
            {
                new RoomAdjacencyEdge(
                    "adjacency:room-a:room-b",
                    1,
                    "room-a",
                    "Room A",
                    "room-b",
                    "Room B",
                    RoomAdjacencyKind.BoundaryAdjacent,
                    RoomAdjacencyDirection.East,
                    RoomAdjacencyDirection.West,
                    200,
                    wall.CenterLine,
                    Confidence.High,
                    new[] { wall.Id },
                    Array.Empty<string>(),
                    new[] { "test adjacency shares the wall" })
            },
            Array.Empty<RoomCluster>());

        await new WallTypeRefinementStage().ExecuteAsync(context, CancellationToken.None);

        var refined = Assert.Single(context.Walls);
        Assert.Equal(WallType.Interior, refined.WallType);
        Assert.Contains(
            refined.Evidence,
            item => item.Contains("shared by room adjacency", StringComparison.OrdinalIgnoreCase)
                && item.Contains("overrides exterior", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WallTypeRefinement_OverridesExteriorGuessWhenRoomsAreDetectedOnBothSides()
    {
        var wall = new WallSegment(
            "wall-two-sided-room-boundary-misclassified-exterior",
            1,
            new PlanLineSegment(new PlanPoint(100, 100), new PlanPoint(100, 300)),
            6,
            Confidence.High)
        {
            WallType = WallType.Exterior,
            Evidence = new[]
            {
                "wall type exterior: near detected floorplan/wall envelope or local outer boundary"
            }
        };
        var context = CreateContext("two-sided-room-boundary-exterior-override");
        context.Walls.Add(wall);
        context.Rooms.Add(new RoomRegion(
            "room-left",
            1,
            new PlanRect(0, 80, 94, 240),
            new[]
            {
                new PlanPoint(0, 80),
                new PlanPoint(94, 80),
                new PlanPoint(94, 320),
                new PlanPoint(0, 320)
            },
            Array.Empty<string>(),
            Confidence.High));
        context.Rooms.Add(new RoomRegion(
            "room-right",
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
            Confidence.High));
        context.WallGraph = GraphFor(wall);

        await new WallTypeRefinementStage().ExecuteAsync(context, CancellationToken.None);

        var refined = Assert.Single(context.Walls);
        Assert.Equal(WallType.Interior, refined.WallType);
        Assert.Contains(
            refined.Evidence,
            item => item.Contains("room evidence on both sides", StringComparison.OrdinalIgnoreCase)
                && item.Contains("overrides exterior", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WallTypeRefinement_PreservesExteriorWhenSharedAdjacencyIncludesOutdoorRoom()
    {
        var wall = new WallSegment(
            "wall-indoor-outdoor-boundary",
            1,
            new PlanLineSegment(new PlanPoint(100, 100), new PlanPoint(100, 300)),
            6,
            Confidence.High)
        {
            WallType = WallType.Exterior,
            Evidence = new[]
            {
                "wall type exterior: near detected floorplan/wall envelope or local outer boundary"
            }
        };
        var context = CreateContext("shared-outdoor-boundary-preserves-exterior");
        context.Walls.Add(wall);
        context.Rooms.Add(Room("room-indoor", RoomUseKind.Office, wall.Id));
        context.Rooms.Add(Room("room-terrace", RoomUseKind.Outdoor, wall.Id));
        context.WallGraph = GraphFor(wall);
        context.RoomAdjacencyGraph = new RoomAdjacencyGraph(
            new[]
            {
                new RoomAdjacencyEdge(
                    "adjacency:room-indoor:room-terrace",
                    1,
                    "room-indoor",
                    "Office",
                    "room-terrace",
                    "Terrace",
                    RoomAdjacencyKind.BoundaryAdjacent,
                    RoomAdjacencyDirection.East,
                    RoomAdjacencyDirection.West,
                    200,
                    wall.CenterLine,
                    Confidence.High,
                    new[] { wall.Id },
                    Array.Empty<string>(),
                    new[] { "test adjacency includes outdoor room" })
            },
            Array.Empty<RoomCluster>());

        await new WallTypeRefinementStage().ExecuteAsync(context, CancellationToken.None);

        var refined = Assert.Single(context.Walls);
        Assert.Equal(WallType.Exterior, refined.WallType);
        Assert.Contains(
            refined.Evidence,
            item => item.Contains("includes outdoor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WallTypeRefinement_KeepsTwoSidedOutdoorRoomBoundaryExterior()
    {
        var wall = new WallSegment(
            "wall-two-sided-outdoor-boundary",
            1,
            new PlanLineSegment(new PlanPoint(100, 100), new PlanPoint(100, 300)),
            6,
            Confidence.High)
        {
            WallType = WallType.Exterior,
            Evidence = new[]
            {
                "wall type exterior: near detected floorplan/wall envelope or local outer boundary"
            }
        };
        var context = CreateContext("two-sided-outdoor-boundary-preserves-exterior");
        context.Walls.Add(wall);
        context.Rooms.Add(Room(
            "room-indoor",
            RoomUseKind.Office,
            new PlanRect(0, 80, 94, 240),
            new[]
            {
                new PlanPoint(0, 80),
                new PlanPoint(94, 80),
                new PlanPoint(94, 320),
                new PlanPoint(0, 320)
            }));
        context.Rooms.Add(Room(
            "room-terrace",
            RoomUseKind.Outdoor,
            new PlanRect(106, 80, 120, 240),
            new[]
            {
                new PlanPoint(106, 80),
                new PlanPoint(226, 80),
                new PlanPoint(226, 320),
                new PlanPoint(106, 320)
            }));
        context.WallGraph = GraphFor(wall);

        await new WallTypeRefinementStage().ExecuteAsync(context, CancellationToken.None);

        var refined = Assert.Single(context.Walls);
        Assert.Equal(WallType.Exterior, refined.WallType);
        Assert.Contains(
            refined.Evidence,
            item => item.Contains("outdoor/terrace", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WallTypeRefinement_PromotesRoomConfirmedMediumWallEvidence()
    {
        var wall = new WallSegment(
            "wall-room-confirmed-medium",
            1,
            new PlanLineSegment(new PlanPoint(100, 100), new PlanPoint(100, 300)),
            6,
            Confidence.High)
        {
            WallType = WallType.Interior,
            Evidence = new[] { "wall type interior: supported wall evidence inside exterior envelope" }
        };
        var context = CreateContext("room-confirmed-medium-wall-promotion");
        context.Walls.Add(wall);
        context.Rooms.Add(Room("room-a", RoomUseKind.Office, wall.Id));
        context.Rooms.Add(Room("room-b", RoomUseKind.Office, wall.Id));
        context.WallGraph = GraphFor(wall);
        context.RoomAdjacencyGraph = new RoomAdjacencyGraph(
            new[]
            {
                new RoomAdjacencyEdge(
                    "adjacency:room-a:room-b",
                    1,
                    "room-a",
                    "Room A",
                    "room-b",
                    "Room B",
                    RoomAdjacencyKind.BoundaryAdjacent,
                    RoomAdjacencyDirection.East,
                    RoomAdjacencyDirection.West,
                    200,
                    wall.CenterLine,
                    Confidence.High,
                    new[] { wall.Id },
                    Array.Empty<string>(),
                    new[] { "test adjacency shares the wall" })
            },
            Array.Empty<RoomCluster>());
        context.WallEvidenceMap = EvidenceMapFor(
            wall,
            WallEvidenceCategory.MediumWallBody,
            placementReady: false,
            requiresReview: true,
            rejectedAsNoise: false,
            new[] { "wall evidence assessment: MediumWallBody / review / confidence 0.88", "parallel wall-face pair" });

        await new WallTypeRefinementStage().ExecuteAsync(context, CancellationToken.None);

        var promoted = Assert.Single(context.WallEvidenceMap.WallAssessments);
        Assert.True(promoted.PlacementReady);
        Assert.False(promoted.RequiresReview);
        Assert.Equal(WallEvidenceDecision.Accept, promoted.Decision);
        Assert.Contains(
            promoted.Evidence,
            item => item.Contains("room-confirmed wall body promoted", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            context.Diagnostics.Build().Messages,
            diagnostic => diagnostic.Code == "walls.architectural_type_refined"
                && diagnostic.Properties["roomConfirmedPlacementPromotedWallCount"] == "1");
    }

    [Fact]
    public async Task WallTypeRefinement_DoesNotPromoteOneSidedRoomReference()
    {
        var wall = new WallSegment(
            "wall-one-sided-medium",
            1,
            new PlanLineSegment(new PlanPoint(100, 100), new PlanPoint(100, 300)),
            6,
            Confidence.High)
        {
            WallType = WallType.Interior,
            Evidence = new[] { "wall type interior: supported wall evidence inside exterior envelope" }
        };
        var context = CreateContext("one-sided-medium-wall-stays-review");
        context.Walls.Add(wall);
        context.Rooms.Add(Room("room-a", RoomUseKind.Office, wall.Id));
        context.WallGraph = GraphFor(wall);
        context.WallEvidenceMap = EvidenceMapFor(
            wall,
            WallEvidenceCategory.MediumWallBody,
            placementReady: false,
            requiresReview: true,
            rejectedAsNoise: false,
            new[] { "wall evidence assessment: MediumWallBody / review / confidence 0.88", "parallel wall-face pair" });

        await new WallTypeRefinementStage().ExecuteAsync(context, CancellationToken.None);

        var retained = Assert.Single(context.WallEvidenceMap.WallAssessments);
        Assert.False(retained.PlacementReady);
        Assert.True(retained.RequiresReview);
        Assert.Equal(WallEvidenceDecision.Review, retained.Decision);
        Assert.DoesNotContain(
            retained.Evidence,
            item => item.Contains("room-confirmed wall body promoted", StringComparison.OrdinalIgnoreCase));
    }

    private static ScanContext CreateContext(string documentId) =>
        new(
            new PlanDocument(
                documentId,
                new[]
                {
                    new PlanPage(1, new PlanSize(400, 400), Array.Empty<PlanPrimitive>())
                }),
            new ScannerOptions());

    private static WallGraph GraphFor(WallSegment wall) =>
        new(
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

    private static RoomRegion Room(string id, RoomUseKind useKind, params string[] wallIds) =>
        Room(
            id,
            useKind,
            new PlanRect(0, 0, 10, 10),
            new[]
            {
                new PlanPoint(0, 0),
                new PlanPoint(10, 0),
                new PlanPoint(10, 10),
                new PlanPoint(0, 10)
            },
            wallIds);

    private static RoomRegion Room(
        string id,
        RoomUseKind useKind,
        PlanRect bounds,
        IReadOnlyList<PlanPoint> boundary,
        IReadOnlyList<string>? wallIds = null) =>
        new(
            id,
            1,
            bounds,
            boundary,
            wallIds ?? Array.Empty<string>(),
            Confidence.High)
        {
            UseKind = useKind
        };

    private static WallEvidenceMap EvidenceMapFor(
        WallSegment wall,
        WallEvidenceCategory category,
        bool placementReady,
        bool requiresReview,
        bool rejectedAsNoise,
        IReadOnlyList<string> evidence) =>
        new(
            Array.Empty<WallEvidenceSegment>(),
            Array.Empty<WallEvidenceBand>(),
            new[]
            {
                new WallEvidenceWallAssessment(
                    wall.Id,
                    wall.PageNumber,
                    wall.Bounds,
                    category,
                    Confidence.High,
                    placementReady,
                    requiresReview,
                    rejectedAsNoise,
                    wall.SourcePrimitiveIds,
                    evidence)
                {
                    Decision = placementReady
                        ? WallEvidenceDecision.Accept
                        : rejectedAsNoise
                            ? WallEvidenceDecision.Reject
                            : WallEvidenceDecision.Review
                }
            });
}
