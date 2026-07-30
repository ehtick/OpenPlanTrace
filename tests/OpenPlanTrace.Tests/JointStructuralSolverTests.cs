using OpenPlanTrace;

namespace OpenPlanTrace.Tests;

public sealed class JointStructuralSolverTests
{
    [Fact]
    public void EvidenceGraph_RetainsPreliminaryRejectsWithoutSelectingStrongNoise()
    {
        var wall = Wall(
            "dimension-like",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(120, 0)),
            Confidence.High);
        var assessment = new WallEvidenceWallAssessment(
            wall.Id,
            wall.PageNumber,
            wall.Bounds,
            WallEvidenceCategory.DimensionOrAnnotation,
            wall.Confidence,
            PlacementReady: false,
            RequiresReview: true,
            RejectedAsNoise: true,
            wall.SourcePrimitiveIds,
            new[] { "dimension baseline evidence" })
        {
            Decision = WallEvidenceDecision.Reject
        };
        var source = Source(
            wallCandidates: new[] { wall },
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                new[] { assessment },
                SourceCandidateWallCount: 1));

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var solution = JointStructuralSolver.Solve(graph);

        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.True(candidate.HasStrongNegativeEvidence);
        Assert.Contains(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.DimensionOrAnnotation);
        var decision = Assert.Single(solution.CandidateDecisions);
        Assert.NotEqual(StructuralWallDecisionKind.Selected, decision.Decision);
        Assert.Empty(solution.WallRuns);
    }

    [Fact]
    public void EvidenceGraph_DoesNotPromoteOpeningClearanceReviewGeometry()
    {
        var wall = Wall(
            "opening-clearance-edge",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(120, 0)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.SingleLine,
            Evidence =
            [
                "single wall-length vector run",
                "wall evidence: demoted from placement-ready because source line belongs to an unfilled exterior opening-clearance rectangle",
                "wall evidence: exterior opening-clearance rectangle retained as review geometry instead of canonical wall placement"
            ]
        };
        var assessment = new WallEvidenceWallAssessment(
            wall.Id,
            wall.PageNumber,
            wall.Bounds,
            WallEvidenceCategory.MediumWallBody,
            wall.Confidence,
            PlacementReady: false,
            RequiresReview: true,
            RejectedAsNoise: false,
            wall.SourcePrimitiveIds,
            wall.Evidence)
        {
            Decision = WallEvidenceDecision.Review
        };
        var room = new RoomRegion(
            "room-beyond-clearance",
            1,
            new PlanRect(0, 0, 120, 80),
            new[]
            {
                new PlanPoint(0, 0),
                new PlanPoint(120, 0),
                new PlanPoint(120, 80),
                new PlanPoint(0, 80)
            },
            new[] { wall.Id },
            Confidence.High)
        {
            Label = "Office",
            UseKind = RoomUseKind.Office
        };
        var source = Source(
            wallCandidates: [wall],
            acceptedWalls: [wall],
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                [assessment],
                SourceCandidateWallCount: 1),
            rooms: [room]);

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var solution = JointStructuralSolver.Solve(graph);

        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.False(candidate.WasAcceptedByPreliminaryPipeline);
        Assert.True(candidate.HasStrongNegativeEvidence);
        Assert.Contains(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.DoorOrOpeningSymbol
                && signal.Weight <= -1.0);
        Assert.DoesNotContain(
            solution.CandidateDecisions,
            decision => decision.CandidateId == candidate.Id
                && decision.Decision == StructuralWallDecisionKind.Selected);
        Assert.DoesNotContain(
            solution.WallRuns,
            run => run.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_RejectsWallSharingDetectedDimensionPrimitiveFamily()
    {
        var wall = Wall(
            "dimension-witness-wall",
            new PlanLineSegment(new PlanPoint(100, 100), new PlanPoint(100, 180)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            SourcePrimitiveIds = ["pdf:p1:path:42:subpath:1:line:4"],
            Evidence = ["strong parallel-face wall pair"]
        };
        var assessment = new WallEvidenceWallAssessment(
            wall.Id,
            wall.PageNumber,
            wall.Bounds,
            WallEvidenceCategory.StrongWallBody,
            wall.Confidence,
            PlacementReady: true,
            RequiresReview: false,
            RejectedAsNoise: false,
            wall.SourcePrimitiveIds,
            wall.Evidence)
        {
            Decision = WallEvidenceDecision.Accept
        };
        var dimension = new DimensionAnnotation(
            "dimension-sharing-path",
            1,
            DimensionKind.Linear,
            DimensionOrientation.Horizontal,
            "1 300",
            "1300",
            new PlanRect(80, 180, 80, 20),
            PlanMeasurementUnit.Millimeter,
            1300,
            new PlanLineSegment(new PlanPoint(80, 180), new PlanPoint(160, 180)),
            80,
            16.25,
            Confidence.High,
            null,
            ["pdf:p1:word:1", "pdf:p1:path:42:subpath:1:line:2"],
            ["matched one dimension line and two witness lines"]);
        var source = Source(
            wallCandidates: [wall],
            acceptedWalls: [wall],
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                [assessment],
                SourceCandidateWallCount: 1),
            dimensions: [dimension]);

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var solution = JointStructuralSolver.Solve(graph);

        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.True(candidate.HasStrongNegativeEvidence);
        Assert.Contains(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.DimensionOrAnnotation
                && signal.Weight <= -1.0
                && signal.SourceId.Contains(dimension.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(
            solution.CandidateDecisions,
            decision => decision.CandidateId == candidate.Id
                && decision.Decision == StructuralWallDecisionKind.Selected);
    }

    [Fact]
    public void EvidenceGraph_TreatsSparseDimensionFamilyOverlapAsContamination()
    {
        var wall = Wall(
            "wall-crossed-by-dimension",
            new PlanLineSegment(new PlanPoint(100, 100), new PlanPoint(100, 320)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            SourcePrimitiveIds =
            [
                "pdf:p1:path:42:subpath:1:line:4",
                "pdf:p1:path:100:subpath:1:line:1",
                "pdf:p1:path:101:subpath:1:line:1",
                "pdf:p1:path:102:subpath:1:line:1",
                "pdf:p1:path:103:subpath:1:line:1",
                "pdf:p1:path:104:subpath:1:line:1",
                "pdf:p1:path:105:subpath:1:line:1",
                "pdf:p1:path:106:subpath:1:line:1",
                "pdf:p1:path:107:subpath:1:line:1",
                "pdf:p1:path:108:subpath:1:line:1"
            ],
            Evidence =
            [
                "strong parallel-face wall pair",
                "layer (unlayered) classified Unknown (0.35)",
                "layer evidence: no strong layer name or geometry evidence"
            ]
        };
        var dimension = new DimensionAnnotation(
            "dimension-crossing-wall",
            1,
            DimensionKind.Linear,
            DimensionOrientation.Horizontal,
            "1 300",
            "1300",
            new PlanRect(80, 180, 80, 20),
            PlanMeasurementUnit.Millimeter,
            1300,
            new PlanLineSegment(new PlanPoint(80, 180), new PlanPoint(160, 180)),
            80,
            16.25,
            Confidence.High,
            null,
            ["pdf:p1:word:1", "pdf:p1:path:42:subpath:1:line:2"],
            ["matched one dimension line and two witness lines"]);
        var source = Source(
            wallCandidates: [wall],
            acceptedWalls: [wall],
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                [AcceptedAssessment(wall, WallEvidenceCategory.StrongWallBody)],
                SourceCandidateWallCount: 1),
            dimensions: [dimension]);

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var solution = JointStructuralSolver.Solve(graph);

        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        var signal = Assert.Single(
            candidate.Signals,
            item => item.Kind == StructuralEvidenceSignalKind.DimensionOrAnnotation);
        Assert.InRange(signal.Weight, -0.12, -0.08);
        Assert.False(candidate.HasStrongNegativeEvidence);
        Assert.Contains(
            solution.CandidateDecisions,
            decision => decision.CandidateId == candidate.Id
                && decision.Decision == StructuralWallDecisionKind.Selected);
        Assert.Contains(
            solution.WallRuns,
            run => run.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_DimensionLayerCorroboratesSparseDimensionFamilyOverlap()
    {
        var wall = Wall(
            "dimension-area-boundary",
            new PlanLineSegment(new PlanPoint(100, 100), new PlanPoint(320, 100)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            SourcePrimitiveIds =
            [
                "pdf:p1:path:42:subpath:1:line:4",
                "pdf:p1:path:100:subpath:1:line:1",
                "pdf:p1:path:101:subpath:1:line:1",
                "pdf:p1:path:102:subpath:1:line:1",
                "pdf:p1:path:103:subpath:1:line:1",
                "pdf:p1:path:104:subpath:1:line:1",
                "pdf:p1:path:105:subpath:1:line:1",
                "pdf:p1:path:106:subpath:1:line:1",
                "pdf:p1:path:107:subpath:1:line:1",
                "pdf:p1:path:108:subpath:1:line:1"
            ],
            Evidence =
            [
                "strong parallel-face wall pair",
                "layer (unlayered) classified Dimension (0.24)",
                "layer evidence: contains dimension-like text"
            ]
        };
        var dimension = new DimensionAnnotation(
            "dimension-sharing-area-boundary",
            1,
            DimensionKind.Linear,
            DimensionOrientation.Horizontal,
            "811,6 m2",
            "811600",
            new PlanRect(80, 100, 260, 20),
            PlanMeasurementUnit.Millimeter,
            811600,
            new PlanLineSegment(new PlanPoint(80, 100), new PlanPoint(340, 100)),
            260,
            3121.54,
            Confidence.High,
            null,
            ["pdf:p1:word:1", "pdf:p1:path:42:subpath:1:line:2"],
            ["matched dimension-like area boundary"]);
        var source = Source(
            wallCandidates: [wall],
            acceptedWalls: [wall],
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                [AcceptedAssessment(wall, WallEvidenceCategory.StrongWallBody)],
                SourceCandidateWallCount: 1),
            dimensions: [dimension]);

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var solution = JointStructuralSolver.Solve(graph);

        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        var signal = Assert.Single(
            candidate.Signals,
            item => item.Kind == StructuralEvidenceSignalKind.DimensionOrAnnotation);
        Assert.Equal(-1.10, signal.Weight, precision: 6);
        Assert.True(candidate.HasStrongNegativeEvidence);
        Assert.DoesNotContain(
            solution.CandidateDecisions,
            decision => decision.CandidateId == candidate.Id
                && decision.Decision == StructuralWallDecisionKind.Selected);
    }

    [Fact]
    public void EvidenceGraph_DoesNotUseEndpointContinuationAsRoomBoundarySupport()
    {
        var continuation = Wall(
            "room-edge-continuation",
            new PlanLineSegment(new PlanPoint(100, 100), new PlanPoint(100, 165)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair
        };
        var room = Room(
            "office",
            new PlanRect(0, 0, 100, 100),
            [
                new PlanPoint(0, 0),
                new PlanPoint(100, 0),
                new PlanPoint(100, 100),
                new PlanPoint(0, 100)
            ]);
        var source = Source(
            wallCandidates: [continuation],
            acceptedWalls: [continuation],
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                [AcceptedAssessment(continuation, WallEvidenceCategory.StrongWallBody)],
                SourceCandidateWallCount: 1),
            rooms: [room]);

        var graph = StructuralEvidenceGraphBuilder.Build(source);

        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(
                continuation.Id,
                StringComparer.Ordinal));
        Assert.DoesNotContain(room.Id, candidate.SourceRoomIds);
        Assert.DoesNotContain(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.RoomBoundary);
    }

    [Fact]
    public void EvidenceGraph_RejectsOneEndedOutwardShellWhisker()
    {
        var whisker = Wall(
            "outward-shell-whisker",
            new PlanLineSegment(new PlanPoint(50, 100), new PlanPoint(50, 170)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            WallType = WallType.Exterior,
            Evidence =
            [
                "strong parallel-face wall pair",
                "wall evidence: exterior shell repair support anchored-shell-span from trusted exterior wall graph"
            ]
        };
        var room = Room(
            "office",
            new PlanRect(0, 0, 100, 100),
            [
                new PlanPoint(0, 0),
                new PlanPoint(100, 0),
                new PlanPoint(100, 100),
                new PlanPoint(0, 100)
            ]);
        var source = Source(
            wallCandidates: [whisker],
            acceptedWalls: [whisker],
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                [AcceptedAssessment(whisker, WallEvidenceCategory.StrongWallBody)],
                SourceCandidateWallCount: 1),
            rooms: [room],
            sheetRegions:
            [
                new SheetRegion(
                    "main-plan",
                    1,
                    RegionKind.MainFloorPlan,
                    new PlanRect(0, 0, 400, 400),
                    Confidence.High)
            ]);

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var solution = JointStructuralSolver.Solve(graph);

        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(whisker.Id, StringComparer.Ordinal));
        Assert.True(candidate.HasStrongNegativeEvidence);
        Assert.True(candidate.HasAbsoluteBlockingEvidence);
        Assert.Contains(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.UnoccupiedShellExtension
                && signal.Weight <= -1.0
                && signal.Description.Contains(
                    "leaves trusted occupied territory",
                    StringComparison.Ordinal));
        Assert.DoesNotContain(
            solution.WallRuns,
            run => run.SourceWallIds.Contains(whisker.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_RejectsRecoveredUnknownOneEndedShellWhisker()
    {
        var recovered = Wall(
            "recovered-shell-whisker",
            new PlanLineSegment(new PlanPoint(-70, 92), new PlanPoint(0, 92)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            WallType = WallType.Unknown
        };
        var anchor = Wall(
            "trusted-shell-anchor",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(0, 100)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            WallType = WallType.Exterior,
            Evidence =
            [
                "filled wall-solid primitive",
                "wall evidence: filled closed vector wall body"
            ]
        };
        var room = Room(
            "office",
            new PlanRect(0, 0, 100, 100),
            [
                new PlanPoint(0, 0),
                new PlanPoint(100, 0),
                new PlanPoint(100, 100),
                new PlanPoint(0, 100)
            ]);
        var source = Source(
            wallCandidates: [anchor, recovered],
            acceptedWalls: [anchor, recovered],
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                [
                    AcceptedAssessment(anchor, WallEvidenceCategory.StrongWallBody),
                    AcceptedAssessment(recovered, WallEvidenceCategory.RecoveredWallBody)
                ],
                SourceCandidateWallCount: 2),
            rooms: [room],
            sheetRegions:
            [
                new SheetRegion(
                    "main-plan",
                    1,
                    RegionKind.MainFloorPlan,
                    new PlanRect(-100, -50, 400, 400),
                    Confidence.High)
            ]);

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(recovered.Id, StringComparer.Ordinal));

        Assert.True(candidate.HasAbsoluteBlockingEvidence);
        Assert.Contains(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.UnoccupiedShellExtension);
        Assert.DoesNotContain(
            JointStructuralSolver.Solve(graph).WallRuns,
            run => run.SourceWallIds.Contains(recovered.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_PreservesExteriorWallWithOccupiedSideSupport()
    {
        var exterior = Wall(
            "occupied-side-exterior",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(0, 100)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            WallType = WallType.Exterior
        };
        var room = Room(
            "office",
            new PlanRect(0, 0, 100, 100),
            [
                new PlanPoint(0, 0),
                new PlanPoint(100, 0),
                new PlanPoint(100, 100),
                new PlanPoint(0, 100)
            ]);
        var source = Source(
            wallCandidates: [exterior],
            acceptedWalls: [exterior],
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                [AcceptedAssessment(exterior, WallEvidenceCategory.StrongWallBody)],
                SourceCandidateWallCount: 1),
            rooms: [room],
            sheetRegions:
            [
                new SheetRegion(
                    "main-plan",
                    1,
                    RegionKind.MainFloorPlan,
                    new PlanRect(0, 0, 200, 200),
                    Confidence.High)
            ]);

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(exterior.Id, StringComparer.Ordinal));

        Assert.DoesNotContain(
            candidate.Signals,
            signal => signal.Description.Contains(
                "leaves trusted occupied territory",
                StringComparison.Ordinal));
        Assert.Contains(
            JointStructuralSolver.Solve(graph).WallRuns,
            run => run.SourceWallIds.Contains(exterior.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_DemotesAcceptedObliqueSingleLineWithoutIndependentWallBody()
    {
        var wall = Wall(
            "oblique-furniture-edge",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(50, 80)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.SingleLine,
            Evidence = new[] { "non-orthogonal wall-length vector" }
        };
        var assessment = new WallEvidenceWallAssessment(
            wall.Id,
            wall.PageNumber,
            wall.Bounds,
            WallEvidenceCategory.MediumWallBody,
            wall.Confidence,
            PlacementReady: true,
            RequiresReview: false,
            RejectedAsNoise: false,
            wall.SourcePrimitiveIds,
            wall.Evidence)
        {
            Decision = WallEvidenceDecision.Accept
        };
        var source = Source(
            wallCandidates: new[] { wall },
            acceptedWalls: new[] { wall },
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                new[] { assessment },
                SourceCandidateWallCount: 1));

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var solution = JointStructuralSolver.Solve(graph);

        var candidate = Assert.Single(graph.WallCandidates);
        Assert.True(candidate.HasStrongNegativeEvidence);
        Assert.Contains(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.UnsupportedOblique);
        Assert.DoesNotContain(
            solution.CandidateDecisions,
            decision => decision.Decision == StructuralWallDecisionKind.Selected);
        Assert.Empty(solution.WallRuns);
    }

    [Fact]
    public void EvidenceGraph_TreatsExplicitFilledWallBodyAsIndependentGeometry()
    {
        var wall = Wall(
            "filled-medium-wall-body",
            new PlanLineSegment(new PlanPoint(20, 0), new PlanPoint(20, 180)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            Evidence =
            [
                "filled wall-solid primitive",
                "wall evidence: filled closed vector wall body"
            ]
        };
        var assessment = new WallEvidenceWallAssessment(
            wall.Id,
            wall.PageNumber,
            wall.Bounds,
            WallEvidenceCategory.MediumWallBody,
            wall.Confidence,
            PlacementReady: true,
            RequiresReview: false,
            RejectedAsNoise: false,
            wall.SourcePrimitiveIds,
            wall.Evidence)
        {
            Decision = WallEvidenceDecision.Accept
        };
        var source = Source(
            wallCandidates: new[] { wall },
            acceptedWalls: new[] { wall },
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                new[] { assessment },
                SourceCandidateWallCount: 1));

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var candidate = Assert.Single(graph.WallCandidates);
        var solution = JointStructuralSolver.Solve(graph);

        Assert.True(candidate.HasIndependentWallBodyEvidence);
        Assert.Contains(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.WallBody
                && signal.Weight >= 0.30
                && signal.Description.Contains(
                    "explicit filled wall-body geometry",
                    StringComparison.OrdinalIgnoreCase));
        var run = Assert.Single(solution.WallRuns);
        Assert.True(run.Reliability.ReadyForCoordinatePlacement);
        Assert.False(run.Reliability.RequiresReview);
    }

    [Fact]
    public void EvidenceGraph_BlocksUnfilledInteriorWallBodyThicknessOutlier()
    {
        var anchors = new[]
        {
            PairedWall("profile-anchor-1", 0, 4, filled: true),
            PairedWall("profile-anchor-2", 30, 4, filled: true),
            PairedWall("profile-anchor-3", 60, 4, filled: true)
        };
        var detail = PairedWall("wide-cabinet-pair", 100, 18);
        var walls = anchors.Append(detail).ToArray();
        var source = Source(
            wallCandidates: walls,
            acceptedWalls: walls,
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                anchors
                    .Select(wall => AcceptedAssessment(
                        wall,
                        WallEvidenceCategory.StrongWallBody))
                    .Append(AcceptedAssessment(
                        detail,
                        WallEvidenceCategory.StrongWallBody))
                    .ToArray(),
                SourceCandidateWallCount: walls.Length));

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(detail.Id, StringComparer.Ordinal));

        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.WallBodyThicknessOutlier
                && signal.Weight <= -1.3
                && signal.Description.Contains(
                    "implausible interior outlier",
                    StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            JointStructuralSolver.Solve(graph).WallRuns,
            run => run.SourceWallIds.Contains(detail.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_BlocksShortReviewDimensionOwnedPairOutsideThicknessProfile()
    {
        var anchors = new[]
        {
            PairedWall("profile-anchor-1", 0, 4, filled: true),
            PairedWall("profile-anchor-2", 30, 4, filled: true),
            PairedWall("profile-anchor-3", 60, 4, filled: true)
        };
        var detail = PairedWall(
            "door-leaf-pair",
            100,
            7.25,
            length: 55) with
        {
            SourcePrimitiveIds = ["pdf:p1:path:700:subpath:1:line:1"]
        };
        var dimension = new DimensionAnnotation(
            "shared-dimension-family",
            1,
            DimensionKind.Linear,
            DimensionOrientation.Horizontal,
            "1 000",
            "1000",
            new PlanRect(0, 140, 100, 20),
            PlanMeasurementUnit.Millimeter,
            1000,
            new PlanLineSegment(new PlanPoint(0, 140), new PlanPoint(100, 140)),
            100,
            10,
            Confidence.High,
            null,
            ["pdf:p1:path:700:subpath:1:line:4"],
            ["matched dimension geometry"]);
        var walls = anchors.Append(detail).ToArray();
        var source = Source(
            wallCandidates: walls,
            acceptedWalls: anchors,
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                anchors
                    .Select(wall => AcceptedAssessment(
                        wall,
                        WallEvidenceCategory.StrongWallBody))
                    .Append(ReviewAssessment(
                        detail,
                        WallEvidenceCategory.MediumWallBody))
                    .ToArray(),
                SourceCandidateWallCount: walls.Length),
            dimensions: [dimension]);

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(detail.Id, StringComparer.Ordinal));

        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.WallBodyThicknessOutlier
                && signal.Weight <= -1.1
                && signal.Description.Contains(
                    "review-only dimension-owned",
                    StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            JointStructuralSolver.Solve(graph).WallRuns,
            run => run.SourceWallIds.Contains(detail.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_PreservesExplicitFilledThickWallBody()
    {
        var anchors = new[]
        {
            PairedWall("profile-anchor-1", 0, 4, filled: true),
            PairedWall("profile-anchor-2", 30, 4, filled: true),
            PairedWall("profile-anchor-3", 60, 4, filled: true)
        };
        var thickWall = PairedWall("filled-thick-wall", 100, 18, filled: true);
        var walls = anchors.Append(thickWall).ToArray();
        var source = Source(
            wallCandidates: walls,
            acceptedWalls: walls,
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                walls
                    .Select(wall => AcceptedAssessment(
                        wall,
                        WallEvidenceCategory.StrongWallBody))
                    .ToArray(),
                SourceCandidateWallCount: walls.Length));

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(thickWall.Id, StringComparer.Ordinal));

        Assert.DoesNotContain(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.WallBodyThicknessOutlier);
        Assert.Contains(
            JointStructuralSolver.Solve(graph).WallRuns,
            run => run.SourceWallIds.Contains(thickWall.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_TreatsOneSidedOutdoorPromotionAsContextOnlyWithoutWallBody()
    {
        var wall = Wall(
            "outdoor-context-only-line",
            new PlanLineSegment(new PlanPoint(0, 40), new PlanPoint(120, 40)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.SingleLine,
            WallType = WallType.Exterior,
            Evidence =
            [
                "wall type refined exterior: detected room evidence on one side is outdoor/terrace space"
            ]
        };
        var assessment = new WallEvidenceWallAssessment(
            wall.Id,
            wall.PageNumber,
            wall.Bounds,
            WallEvidenceCategory.MediumWallBody,
            wall.Confidence,
            PlacementReady: true,
            RequiresReview: false,
            RejectedAsNoise: false,
            wall.SourcePrimitiveIds,
            wall.Evidence)
        {
            Decision = WallEvidenceDecision.Accept
        };
        var source = Source(
            wallCandidates: new[] { wall },
            acceptedWalls: new[] { wall },
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                new[] { assessment },
                SourceCandidateWallCount: 1));

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var candidate = Assert.Single(graph.WallCandidates);
        var solution = JointStructuralSolver.Solve(graph);

        Assert.Contains(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.ContextOnlyBoundary
                && signal.Weight <= -0.45
                && signal.Description.Contains(
                    "one-sided outdoor room context",
                    StringComparison.OrdinalIgnoreCase));
        Assert.True(candidate.HasAbsoluteBlockingEvidence);
        Assert.Empty(solution.WallRuns);
    }

    [Fact]
    public void EvidenceGraph_DoesNotPromoteOutdoorRoomOutlineWithoutWallBody()
    {
        var wall = Wall(
            "terrace-outline",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(120, 0)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.SingleLine,
            WallType = WallType.Exterior
        };
        var assessment = new WallEvidenceWallAssessment(
            wall.Id,
            wall.PageNumber,
            wall.Bounds,
            WallEvidenceCategory.MediumWallBody,
            wall.Confidence,
            PlacementReady: true,
            RequiresReview: false,
            RejectedAsNoise: false,
            wall.SourcePrimitiveIds,
            new[] { "room boundary support" })
        {
            Decision = WallEvidenceDecision.Accept
        };
        var room = new RoomRegion(
            "terrace",
            1,
            new PlanRect(0, 0, 120, 80),
            new[]
            {
                new PlanPoint(0, 0),
                new PlanPoint(120, 0),
                new PlanPoint(120, 80),
                new PlanPoint(0, 80)
            },
            new[] { wall.Id },
            Confidence.High)
        {
            Label = "Terrace",
            UseKind = RoomUseKind.Outdoor
        };
        var source = Source(
            wallCandidates: new[] { wall },
            acceptedWalls: new[] { wall },
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                new[] { assessment },
                SourceCandidateWallCount: 1),
            rooms: new[] { room });

        var graph = StructuralEvidenceGraphBuilder.Build(source);
        var solution = JointStructuralSolver.Solve(graph);

        var sourceCandidate = Assert.Single(
            graph.WallCandidates,
            candidate => candidate.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Contains(
            sourceCandidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.ContextOnlyBoundary);
        Assert.All(
            graph.RoomLoops,
            loop => Assert.Equal(StructuralRoomLoopContext.Outdoor, loop.Context));
        Assert.Empty(solution.WallRuns);
    }

    [Fact]
    public void EvidenceGraph_CorroboratesAcceptedMediumWallBetweenOppositeProvisionalRooms()
    {
        var wall = Wall(
            "accepted-provisional-room-partition",
            new PlanLineSegment(new PlanPoint(60, 0), new PlanPoint(60, 80)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.FragmentMerged,
            WallType = WallType.Interior
        };
        var leftRoom = new RoomRegion(
            "provisional-left-room",
            1,
            new PlanRect(0, 0, 60, 80),
            [
                new PlanPoint(0, 0),
                new PlanPoint(60, 0),
                new PlanPoint(60, 80),
                new PlanPoint(0, 80)
            ],
            [wall.Id],
            Confidence.High);
        var rightRoom = new RoomRegion(
            "provisional-right-room",
            1,
            new PlanRect(60, 0, 60, 80),
            [
                new PlanPoint(60, 0),
                new PlanPoint(120, 0),
                new PlanPoint(120, 80),
                new PlanPoint(60, 80)
            ],
            [wall.Id],
            Confidence.High);
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: [wall],
                acceptedWalls: [wall],
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    [AcceptedAssessment(wall, WallEvidenceCategory.MediumWallBody)],
                    SourceCandidateWallCount: 1),
                wallGraph: WallGraphFor(
                    [wall],
                    WallGraphComponentKind.MainStructural,
                    excludedFromStructuralTopology: false),
                rooms: [leftRoom, rightRoom]));

        var candidate = Assert.Single(
            graph.WallCandidates,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Contains(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.ContextOnlyBoundary
                && signal.Weight <= -0.45);
        Assert.Contains(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.OppositeRoomBoundary
                && signal.Description.Contains(
                    "opposite provisional room interiors",
                    StringComparison.OrdinalIgnoreCase));
        Assert.True(candidate.HasCrossDomainWallBodyEvidence);
        Assert.False(candidate.HasAbsoluteBlockingEvidence);

        var solution = JointStructuralSolver.Solve(graph);
        var run = Assert.Single(
            solution.WallRuns,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.True(run.Reliability.ReadyForCoordinatePlacement);
        Assert.False(run.Reliability.RequiresReview);
        Assert.Contains(
            run.Reliability.Reasons,
            reason => reason.Contains("cross-domain", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvidenceGraph_CorroboratesContinuousFragmentAxisBetweenOppositeProvisionalRooms()
    {
        var wall = FragmentedReviewWall(
            "continuous-fragment-partition",
            new PlanLineSegment(new PlanPoint(60, 0), new PlanPoint(60, 165)));
        var leftRoom = new RoomRegion(
            "fragment-left-room",
            1,
            new PlanRect(0, 0, 60, 165),
            [
                new PlanPoint(0, 0),
                new PlanPoint(60, 0),
                new PlanPoint(60, 165),
                new PlanPoint(0, 165)
            ],
            [wall.Id],
            Confidence.High);
        var rightRoom = new RoomRegion(
            "fragment-right-room",
            1,
            new PlanRect(60, 0, 60, 165),
            [
                new PlanPoint(60, 0),
                new PlanPoint(120, 0),
                new PlanPoint(120, 165),
                new PlanPoint(60, 165)
            ],
            [wall.Id],
            Confidence.High);
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: [wall],
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    [ReviewAssessment(wall, WallEvidenceCategory.MediumWallBody)],
                    SourceCandidateWallCount: 1),
                wallGraph: WallGraphFor(
                    [wall],
                    WallGraphComponentKind.MainStructural,
                    excludedFromStructuralTopology: false),
                rooms: [leftRoom, rightRoom]));

        var candidate = Assert.Single(
            graph.WallCandidates,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.FragmentAxisContinuity
                && signal.Weight >= 0.16);
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.OppositeRoomBoundary
                && signal.Weight >= 0.08);
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.ContextOnlyBoundary
                && signal.Weight > -0.45);
        Assert.True(candidate.HasCorroboratedFragmentAxisEvidence);
        Assert.True(candidate.HasCrossDomainWallBodyEvidence);
        Assert.False(candidate.HasAbsoluteBlockingEvidence);

        var solution = JointStructuralSolver.Solve(graph);
        Assert.Contains(
            solution.CandidateDecisions,
            decision =>
                decision.CandidateId == candidate.Id
                && decision.Decision == StructuralWallDecisionKind.Selected);
        var run = Assert.Single(
            solution.WallRuns,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Equal(165, run.DrawingLength, precision: 6);
        Assert.True(run.Reliability.ReadyForCoordinatePlacement);
    }

    [Fact]
    public void EvidenceGraph_DoesNotCorroborateDimensionContaminatedFragmentAxis()
    {
        var wall = FragmentedReviewWall(
            "dimension-fragment-axis",
            new PlanLineSegment(new PlanPoint(60, 0), new PlanPoint(60, 165))) with
        {
            SourcePrimitiveIds =
            [
                "pdf:p1:path:42:subpath:1:line:4",
                "pdf:p1:path:100:subpath:1:line:1"
            ]
        };
        var leftRoom = new RoomRegion(
            "dimension-fragment-left-room",
            1,
            new PlanRect(0, 0, 60, 165),
            [
                new PlanPoint(0, 0),
                new PlanPoint(60, 0),
                new PlanPoint(60, 165),
                new PlanPoint(0, 165)
            ],
            [wall.Id],
            Confidence.High);
        var rightRoom = new RoomRegion(
            "dimension-fragment-right-room",
            1,
            new PlanRect(60, 0, 60, 165),
            [
                new PlanPoint(60, 0),
                new PlanPoint(120, 0),
                new PlanPoint(120, 165),
                new PlanPoint(60, 165)
            ],
            [wall.Id],
            Confidence.High);
        var dimension = new DimensionAnnotation(
            "dimension-fragment-family",
            1,
            DimensionKind.Linear,
            DimensionOrientation.Horizontal,
            "1 300",
            "1300",
            new PlanRect(40, 165, 80, 20),
            PlanMeasurementUnit.Millimeter,
            1300,
            new PlanLineSegment(new PlanPoint(40, 165), new PlanPoint(120, 165)),
            80,
            16.25,
            Confidence.High,
            null,
            ["pdf:p1:word:1", "pdf:p1:path:42:subpath:1:line:2"],
            ["matched one dimension line and two witness lines"]);
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: [wall],
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    [ReviewAssessment(wall, WallEvidenceCategory.MediumWallBody)],
                    SourceCandidateWallCount: 1),
                wallGraph: WallGraphFor(
                    [wall],
                    WallGraphComponentKind.MainStructural,
                    excludedFromStructuralTopology: false),
                rooms: [leftRoom, rightRoom],
                dimensions: [dimension]));

        var candidate = Assert.Single(
            graph.WallCandidates,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Contains(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.FragmentAxisContinuity);
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.DimensionOrAnnotation
                && signal.Weight <= -0.45);
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.ContextOnlyBoundary
                && signal.Weight <= -0.45);
        Assert.False(candidate.HasCorroboratedFragmentAxisEvidence);
        Assert.False(candidate.HasCrossDomainWallBodyEvidence);
        Assert.DoesNotContain(
            JointStructuralSolver.Solve(graph).WallRuns,
            run => run.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void JointSolver_PrefersLongCrossDomainWallOverContainedRecoveredFallback()
    {
        var wall = Wall(
            "long-cross-domain-partition",
            new PlanLineSegment(new PlanPoint(60, 0), new PlanPoint(60, 80)),
            new Confidence(0.81)) with
        {
            DetectionKind = WallDetectionKind.FragmentMerged,
            WallType = WallType.Interior
        };
        var recovered = Wall(
            "contained-recovered-partition",
            new PlanLineSegment(new PlanPoint(60, 0), new PlanPoint(60, 30)),
            new Confidence(0.91)) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            WallType = WallType.Interior,
            SourcePrimitiveIds = ["primitive:long-cross-domain-partition"],
            PairEvidence = new WallPairEvidence(
                new PlanLineSegment(new PlanPoint(59, 0), new PlanPoint(59, 30)),
                new PlanLineSegment(new PlanPoint(61, 0), new PlanPoint(61, 30)),
                FaceSeparation: 2,
                OverlapRatio: 1,
                Score: 0.91,
                FirstFaceFragmentCount: 1,
                SecondFaceFragmentCount: 1,
                FirstFaceSourcePrimitiveIds: ["primitive:contained-recovered:first"],
                SecondFaceSourcePrimitiveIds: ["primitive:contained-recovered:second"])
        };
        var leftRoom = new RoomRegion(
            "dominance-left-room",
            1,
            new PlanRect(0, 0, 60, 80),
            [
                new PlanPoint(0, 0),
                new PlanPoint(60, 0),
                new PlanPoint(60, 80),
                new PlanPoint(0, 80)
            ],
            [wall.Id, recovered.Id],
            Confidence.High);
        var rightRoom = new RoomRegion(
            "dominance-right-room",
            1,
            new PlanRect(60, 0, 60, 80),
            [
                new PlanPoint(60, 0),
                new PlanPoint(120, 0),
                new PlanPoint(120, 80),
                new PlanPoint(60, 80)
            ],
            [wall.Id, recovered.Id],
            Confidence.High);
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: [wall, recovered],
                acceptedWalls: [wall, recovered],
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    [
                        AcceptedAssessment(wall, WallEvidenceCategory.MediumWallBody),
                        AcceptedAssessment(recovered, WallEvidenceCategory.RecoveredWallBody)
                    ],
                    SourceCandidateWallCount: 2),
                wallGraph: WallGraphFor(
                    [wall, recovered],
                    WallGraphComponentKind.MainStructural,
                    excludedFromStructuralTopology: false),
                rooms: [leftRoom, rightRoom]));

        var solution = JointStructuralSolver.Solve(graph);
        var wallCandidate = Assert.Single(
            graph.WallCandidates,
            candidate => candidate.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        var recoveredCandidate = Assert.Single(
            graph.WallCandidates,
            candidate => candidate.SourceWallIds.Contains(recovered.Id, StringComparer.Ordinal));
        Assert.True(wallCandidate.HasCrossDomainWallBodyEvidence);
        Assert.Contains(
            solution.CandidateDecisions,
            decision =>
                decision.CandidateId == wallCandidate.Id
                && decision.Decision == StructuralWallDecisionKind.Selected);
        Assert.Contains(
            solution.CandidateDecisions,
            decision =>
                decision.CandidateId == recoveredCandidate.Id
                && decision.Decision == StructuralWallDecisionKind.Rejected
                && decision.Reasons.Any(reason =>
                    reason.Contains(wallCandidate.Id, StringComparison.Ordinal)));
        var run = Assert.Single(
            solution.WallRuns,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Equal(80, run.DrawingLength, precision: 6);
        Assert.DoesNotContain(recovered.Id, run.SourceWallIds);
    }

    [Fact]
    public void EvidenceGraph_DoesNotCorroborateAcceptedMediumWallWhenProvisionalRoomsShareOneSide()
    {
        var wall = Wall(
            "accepted-one-sided-provisional-boundary",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(0, 80)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.FragmentMerged,
            WallType = WallType.Interior
        };
        var firstRoom = new RoomRegion(
            "first-one-sided-room",
            1,
            new PlanRect(0, 0, 60, 80),
            [
                new PlanPoint(0, 0),
                new PlanPoint(60, 0),
                new PlanPoint(60, 80),
                new PlanPoint(0, 80)
            ],
            [wall.Id],
            Confidence.High);
        var secondRoom = new RoomRegion(
            "second-one-sided-room",
            1,
            new PlanRect(0, 0, 80, 80),
            [
                new PlanPoint(0, 0),
                new PlanPoint(80, 0),
                new PlanPoint(80, 80),
                new PlanPoint(0, 80)
            ],
            [wall.Id],
            Confidence.High);
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: [wall],
                acceptedWalls: [wall],
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    [AcceptedAssessment(wall, WallEvidenceCategory.MediumWallBody)],
                    SourceCandidateWallCount: 1),
                wallGraph: WallGraphFor(
                    [wall],
                    WallGraphComponentKind.MainStructural,
                    excludedFromStructuralTopology: false),
                rooms: [firstRoom, secondRoom]));

        var candidate = Assert.Single(
            graph.WallCandidates,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.DoesNotContain(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.OppositeRoomBoundary);
        Assert.False(candidate.HasCrossDomainWallBodyEvidence);
        Assert.True(candidate.HasAbsoluteBlockingEvidence);
        Assert.DoesNotContain(
            JointStructuralSolver.Solve(graph).WallRuns,
            run => run.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_DoesNotCorroborateAcceptedMediumWallBetweenTinyProvisionalLoops()
    {
        var wall = Wall(
            "accepted-fixture-edge",
            new PlanLineSegment(new PlanPoint(0, 25.5), new PlanPoint(25.5, 25.5)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.SingleLine,
            WallType = WallType.Interior
        };
        var upperLoop = new RoomRegion(
            "tiny-upper-loop",
            1,
            new PlanRect(0, 0, 25.5, 25.5),
            [
                new PlanPoint(0, 0),
                new PlanPoint(25.5, 0),
                new PlanPoint(25.5, 25.5),
                new PlanPoint(0, 25.5)
            ],
            [wall.Id],
            Confidence.High)
        {
            AreaSquareMeters = 0.2
        };
        var lowerLoop = new RoomRegion(
            "tiny-lower-loop",
            1,
            new PlanRect(0, 25.5, 25.5, 25.5),
            [
                new PlanPoint(0, 25.5),
                new PlanPoint(25.5, 25.5),
                new PlanPoint(25.5, 51),
                new PlanPoint(0, 51)
            ],
            [wall.Id],
            Confidence.High)
        {
            AreaSquareMeters = 0.2
        };
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: [wall],
                acceptedWalls: [wall],
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    [AcceptedAssessment(wall, WallEvidenceCategory.MediumWallBody)],
                    SourceCandidateWallCount: 1),
                wallGraph: WallGraphFor(
                    [wall],
                    WallGraphComponentKind.MainStructural,
                    excludedFromStructuralTopology: false),
                rooms: [upperLoop, lowerLoop]));

        var candidate = Assert.Single(
            graph.WallCandidates,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Contains(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.ContextOnlyBoundary);
        Assert.DoesNotContain(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.OppositeRoomBoundary);
        Assert.False(candidate.HasCrossDomainWallBodyEvidence);
        Assert.True(candidate.HasAbsoluteBlockingEvidence);
        Assert.DoesNotContain(
            JointStructuralSolver.Solve(graph).WallRuns,
            run => run.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void JointSolver_MarksStrongWallBodyReadyDespiteLegacyRepresentationBookkeeping()
    {
        var wall = Wall(
            "paired-shell",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(180, 0)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            WallType = WallType.Exterior
        };
        var assessment = new WallEvidenceWallAssessment(
            wall.Id,
            wall.PageNumber,
            wall.Bounds,
            WallEvidenceCategory.StrongWallBody,
            wall.Confidence,
            PlacementReady: true,
            RequiresReview: false,
            RejectedAsNoise: false,
            wall.SourcePrimitiveIds,
            new[] { "strong parallel-face wall pair" })
        {
            Decision = WallEvidenceDecision.Accept
        };
        var source = Source(
            wallCandidates: new[] { wall },
            acceptedWalls: new[] { wall },
            evidence: new WallEvidenceMap(
                Array.Empty<WallEvidenceSegment>(),
                Array.Empty<WallEvidenceBand>(),
                new[] { assessment },
                SourceCandidateWallCount: 1));

        var solution = JointStructuralSolver.Solve(
            StructuralEvidenceGraphBuilder.Build(source));

        var run = Assert.Single(solution.WallRuns);
        Assert.True(run.Reliability.ReadyForCoordinatePlacement);
        Assert.False(run.Reliability.RequiresReview);
        Assert.Contains(
            run.Reliability.Reasons,
            reason => reason.Contains("strong wall-body", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WallRunReadiness_StrongBodyOutweighsLocalOutdoorContext()
    {
        var candidate = Candidate(
            "strong-outdoor-boundary",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(180, 0)),
            unaryScore: 0.70) with
        {
            Signals =
            [
                Signal(StructuralEvidenceSignalKind.WallBody, 0.34),
                Signal(StructuralEvidenceSignalKind.ContextOnlyBoundary, -1.25)
            ]
        };

        var reliability = StructuralWallRunReadinessEvaluator.Evaluate(
            new[] { candidate },
            new StructuralSolverOptions());

        Assert.False(candidate.HasAbsoluteBlockingEvidence);
        Assert.True(reliability.ReadyForCoordinatePlacement);
        Assert.False(reliability.RequiresReview);
        Assert.Contains(
            reliability.Reasons,
            reason => reason.Contains("outweighs outdoor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WallRunReadiness_IsolatedTerritoryBlocksStrongBodyPlacement()
    {
        var candidate = Candidate(
            "strong-isolated-detail",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(180, 0)),
            unaryScore: 0.70) with
        {
            Signals =
            [
                Signal(StructuralEvidenceSignalKind.WallBody, 0.34),
                Signal(StructuralEvidenceSignalKind.IsolatedStructuralIsland, -1.35)
            ]
        };

        var reliability = StructuralWallRunReadinessEvaluator.Evaluate(
            new[] { candidate },
            new StructuralSolverOptions());

        Assert.True(candidate.HasAbsoluteBlockingEvidence);
        Assert.False(reliability.ReadyForCoordinatePlacement);
        Assert.True(reliability.RequiresReview);
        Assert.Contains(
            reliability.Reasons,
            reason => reason.Contains("excluded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WallRunReadiness_OpeningConfirmsMediumIndoorOutdoorBoundary()
    {
        var candidate = Candidate(
            "door-host-boundary",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(120, 0)),
            unaryScore: 0.58) with
        {
            Origins = StructuralCandidateOrigin.DetectedWall
                | StructuralCandidateOrigin.AcceptedWall
                | StructuralCandidateOrigin.OpeningHost,
            Signals =
            [
                Signal(StructuralEvidenceSignalKind.WallBody, 0.20),
                Signal(StructuralEvidenceSignalKind.AcceptedWall, 0.22),
                Signal(StructuralEvidenceSignalKind.OpeningHost, 0.30),
                Signal(StructuralEvidenceSignalKind.ContextOnlyBoundary, -1.25)
            ]
        };

        var reliability = StructuralWallRunReadinessEvaluator.Evaluate(
            new[] { candidate },
            new StructuralSolverOptions());

        Assert.True(reliability.ReadyForCoordinatePlacement);
        Assert.False(reliability.RequiresReview);
        Assert.Contains(
            reliability.Reasons,
            reason => reason.Contains("opening-host", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WallRunReadiness_OpeningProximityDoesNotConfirmOutdoorBoundary()
    {
        var candidate = Candidate(
            "terrace-outline-near-door",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(120, 0)),
            unaryScore: 0.58) with
        {
            Signals =
            [
                Signal(StructuralEvidenceSignalKind.WallBody, 0.20),
                Signal(StructuralEvidenceSignalKind.AcceptedWall, 0.22),
                Signal(StructuralEvidenceSignalKind.OpeningHost, 0.025),
                Signal(StructuralEvidenceSignalKind.ContextOnlyBoundary, -1.25)
            ]
        };

        var reliability = StructuralWallRunReadinessEvaluator.Evaluate(
            new[] { candidate },
            new StructuralSolverOptions());

        Assert.False(reliability.ReadyForCoordinatePlacement);
        Assert.True(reliability.RequiresReview);
        Assert.Contains(
            reliability.Reasons,
            reason => reason.Contains(
                "context-only",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WallRunReadiness_OutdoorOutlineWithoutOpeningRemainsReviewOnly()
    {
        var candidate = Candidate(
            "terrace-outline",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(120, 0)),
            unaryScore: 0.58) with
        {
            Signals =
            [
                Signal(StructuralEvidenceSignalKind.WallBody, 0.20),
                Signal(StructuralEvidenceSignalKind.AcceptedWall, 0.22),
                Signal(StructuralEvidenceSignalKind.ContextOnlyBoundary, -1.25)
            ]
        };

        var reliability = StructuralWallRunReadinessEvaluator.Evaluate(
            new[] { candidate },
            new StructuralSolverOptions());

        Assert.False(reliability.ReadyForCoordinatePlacement);
        Assert.True(reliability.RequiresReview);
    }

    [Fact]
    public void EvidenceGraph_MarksUnknownRoomOverlappingOutdoorRoomAsConflicted()
    {
        var outdoor = new RoomRegion(
            "outdoor",
            1,
            new PlanRect(0, 0, 120, 80),
            new[]
            {
                new PlanPoint(0, 0),
                new PlanPoint(120, 0),
                new PlanPoint(120, 80),
                new PlanPoint(0, 80)
            },
            Array.Empty<string>(),
            Confidence.High)
        {
            UseKind = RoomUseKind.Outdoor
        };
        var unknown = new RoomRegion(
            "dimension-cycle",
            1,
            new PlanRect(10, 10, 100, 50),
            new[]
            {
                new PlanPoint(10, 10),
                new PlanPoint(110, 10),
                new PlanPoint(110, 60),
                new PlanPoint(10, 60)
            },
            Array.Empty<string>(),
            Confidence.High);

        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(rooms: new[] { outdoor, unknown }));

        Assert.Equal(
            StructuralRoomLoopContext.Outdoor,
            Assert.Single(graph.RoomLoops, loop => loop.SourceRoomId == outdoor.Id).Context);
        Assert.Equal(
            StructuralRoomLoopContext.Conflicted,
            Assert.Single(graph.RoomLoops, loop => loop.SourceRoomId == unknown.Id).Context);
    }

    [Fact]
    public void JointSolver_DoesNotPromoteUnlabeledGeometryLoopIntoWalls()
    {
        var room = new RoomRegion(
            "room-1",
            1,
            new PlanRect(0, 0, 100, 80),
            new[]
            {
                new PlanPoint(0, 0),
                new PlanPoint(100, 0),
                new PlanPoint(100, 80),
                new PlanPoint(0, 80)
            },
            Array.Empty<string>(),
            Confidence.High);
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(rooms: new[] { room }));

        var solution = JointStructuralSolver.Solve(graph);

        Assert.Equal(4, graph.WallCandidates.Count);
        Assert.Equal(
            StructuralRoomLoopContext.Unknown,
            Assert.Single(graph.RoomLoops).Context);
        Assert.Equal(0, solution.Metrics.SelectedCandidateCount);
        Assert.Empty(solution.WallRuns);
        var closure = Assert.Single(solution.RoomClosures);
        Assert.False(closure.IsClosed);
        Assert.Equal(0, closure.BoundaryCoverage);
        Assert.Equal(4, closure.MissingBoundaryEdgeIds.Count);
    }

    [Fact]
    public void JointSolver_RecoversTypedRoomLoopFromWeakBoundaryEvidence()
    {
        var room = new RoomRegion(
            "office-1",
            1,
            new PlanRect(0, 0, 100, 80),
            new[]
            {
                new PlanPoint(0, 0),
                new PlanPoint(100, 0),
                new PlanPoint(100, 80),
                new PlanPoint(0, 80)
            },
            Array.Empty<string>(),
            Confidence.High)
        {
            Label = "Office 1",
            UseKind = RoomUseKind.Office
        };
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(rooms: new[] { room }));

        var solution = JointStructuralSolver.Solve(graph);

        Assert.Equal(
            StructuralRoomLoopContext.Indoor,
            Assert.Single(graph.RoomLoops).Context);
        Assert.Equal(4, solution.Metrics.SelectedCandidateCount);
        Assert.Equal(4, solution.WallRuns.Count);
        var closure = Assert.Single(solution.RoomClosures);
        Assert.True(closure.IsClosed);
        Assert.Equal(1, closure.BoundaryCoverage);
        Assert.Empty(closure.MissingBoundaryEdgeIds);
    }

    [Fact]
    public void JointSolver_CompactsCollinearFragmentsIntoOneCanonicalRun()
    {
        var first = Candidate(
            "first",
            new PlanLineSegment(new PlanPoint(0, 10), new PlanPoint(50, 10)),
            unaryScore: 0.60);
        var second = Candidate(
            "second",
            new PlanLineSegment(new PlanPoint(50, 10), new PlanPoint(110, 10)),
            unaryScore: 0.58);
        var relation = Relation(
            StructuralEvidenceRelationKind.Continuation,
            first,
            second,
            weight: 0.20);
        var graph = Graph(new[] { first, second }, new[] { relation });

        var solution = JointStructuralSolver.Solve(graph);

        Assert.Equal(2, solution.Metrics.SelectedCandidateCount);
        var run = Assert.Single(solution.WallRuns);
        Assert.Equal(110, run.DrawingLength, precision: 6);
        Assert.Equal(new PlanPoint(0, 10), run.CenterLine.Start);
        Assert.Equal(new PlanPoint(110, 10), run.CenterLine.End);
        Assert.Equal(2, run.CandidateIds.Count);
    }

    [Fact]
    public void CanonicalTopology_ResolvesExteriorBodyLeavesIntoOneWallAssembly()
    {
        var first = ExteriorAssemblyLeaf(
            "exterior-assembly-first",
            y: 100,
            wallType: WallType.Exterior);
        var second = ExteriorAssemblyLeaf(
            "exterior-assembly-second",
            y: 109,
            wallType: WallType.Interior);
        var graph = Graph(
            new[] { first, second },
            Array.Empty<StructuralEvidenceRelation>());

        var solution = JointStructuralSolver.Solve(graph);

        Assert.Equal(2, solution.Metrics.SelectedCandidateCount);
        var run = Assert.Single(solution.WallRuns);
        Assert.Equal(WallType.Exterior, run.WallType);
        Assert.Equal(2, run.AssemblyLeafCount);
        Assert.Equal(104.5, run.CenterLine.Start.Y, precision: 6);
        Assert.Equal(104.5, run.CenterLine.End.Y, precision: 6);
        Assert.Equal(13, run.Thickness, precision: 6);
        Assert.Equal(
            new[] { "component:exterior-assembly" },
            run.SourceWallComponentIds);
        Assert.Equal(
            new[] { first.Id, second.Id },
            run.CandidateIds);
        Assert.Contains(
            run.Evidence,
            item => item.Contains(
                "resolved exterior wall assembly",
                StringComparison.Ordinal));
    }

    [Fact]
    public void CanonicalTopology_AbsorbsContainedExteriorLeafIntoResolvedAssembly()
    {
        var first = ExteriorAssemblyLeaf(
            "exterior-assembly-base-first",
            y: 100,
            wallType: WallType.Exterior);
        var second = ExteriorAssemblyLeaf(
            "exterior-assembly-base-second",
            y: 109,
            wallType: WallType.Interior);
        var contained = ExteriorAssemblyLeaf(
            "exterior-assembly-contained",
            y: 110,
            wallType: WallType.Exterior);
        var graph = Graph(
            new[] { first, second, contained },
            Array.Empty<StructuralEvidenceRelation>());

        var solution = JointStructuralSolver.Solve(graph);

        Assert.Equal(3, solution.Metrics.SelectedCandidateCount);
        var run = Assert.Single(solution.WallRuns);
        Assert.Equal(3, run.AssemblyLeafCount);
        Assert.Equal(105, run.CenterLine.Start.Y, precision: 6);
        Assert.Equal(14, run.Thickness, precision: 6);
        Assert.Equal(
            new[] { contained.Id, first.Id, second.Id }.Order(StringComparer.Ordinal),
            run.CandidateIds);
    }

    [Fact]
    public void CanonicalTopology_DoesNotResolveParallelInteriorWallsAsAssembly()
    {
        var first = ExteriorAssemblyLeaf(
            "interior-parallel-first",
            y: 100,
            wallType: WallType.Interior,
            exteriorSemantics: false);
        var second = ExteriorAssemblyLeaf(
            "interior-parallel-second",
            y: 109,
            wallType: WallType.Interior,
            exteriorSemantics: false);
        var graph = Graph(
            new[] { first, second },
            Array.Empty<StructuralEvidenceRelation>());

        var solution = JointStructuralSolver.Solve(graph);

        Assert.Equal(2, solution.Metrics.SelectedCandidateCount);
        Assert.Equal(2, solution.WallRuns.Count);
        Assert.All(solution.WallRuns, run => Assert.Equal(1, run.AssemblyLeafCount));
    }

    [Fact]
    public void CanonicalTopology_DoesNotResolveSeparatedExteriorWallsAsAssembly()
    {
        var first = ExteriorAssemblyLeaf(
            "separated-exterior-first",
            y: 100,
            wallType: WallType.Exterior);
        var second = ExteriorAssemblyLeaf(
            "separated-exterior-second",
            y: 130,
            wallType: WallType.Exterior);
        var graph = Graph(
            new[] { first, second },
            Array.Empty<StructuralEvidenceRelation>());

        var solution = JointStructuralSolver.Solve(graph);

        Assert.Equal(2, solution.Metrics.SelectedCandidateCount);
        Assert.Equal(2, solution.WallRuns.Count);
        Assert.All(solution.WallRuns, run => Assert.Equal(1, run.AssemblyLeafCount));
    }

    [Fact]
    public void CanonicalTopology_DoesNotMergeUnresolvedContextLineIntoCleanWallBody()
    {
        var contextOnly = Candidate(
            "outdoor-context-line",
            new PlanLineSegment(new PlanPoint(0, 10), new PlanPoint(90, 10)),
            unaryScore: 0.55) with
        {
            Signals =
            [
                Signal(StructuralEvidenceSignalKind.ContextOnlyBoundary, -1.10)
            ]
        };
        var cleanWall = Candidate(
            "clean-filled-wall",
            new PlanLineSegment(new PlanPoint(100, 10), new PlanPoint(300, 10)),
            unaryScore: 0.85) with
        {
            Signals =
            [
                Signal(StructuralEvidenceSignalKind.WallBody, 0.36)
            ]
        };
        var relation = Relation(
            StructuralEvidenceRelationKind.Continuation,
            contextOnly,
            cleanWall,
            weight: 0.12);
        var graph = Graph(
            new[] { contextOnly, cleanWall },
            new[] { relation });

        var topology = CanonicalStructuralTopologyBuilder.Build(
            graph,
            new HashSet<string>(StringComparer.Ordinal)
            {
                contextOnly.Id,
                cleanWall.Id
            },
            new StructuralSolverOptions());

        Assert.Equal(2, topology.WallRuns.Count);
        Assert.All(topology.WallRuns, run => Assert.Single(run.CandidateIds));
        Assert.Contains(
            topology.WallRuns,
            run => run.SourceWallIds.Contains(contextOnly.Id, StringComparer.Ordinal)
                && run.Reliability.RequiresReview);
        Assert.Contains(
            topology.WallRuns,
            run => run.SourceWallIds.Contains(cleanWall.Id, StringComparer.Ordinal)
                && run.Reliability.ReadyForCoordinatePlacement);
    }

    [Fact]
    public void JointSolver_DoesNotMergeAxisDriftThroughTransitiveRelations()
    {
        var first = Candidate(
            "axis-first",
            new PlanLineSegment(new PlanPoint(0, 10), new PlanPoint(100, 10)),
            unaryScore: 0.75);
        var bridge = Candidate(
            "axis-bridge",
            new PlanLineSegment(new PlanPoint(0, 12.5), new PlanPoint(100, 12.5)),
            unaryScore: 0.70);
        var second = Candidate(
            "axis-second",
            new PlanLineSegment(new PlanPoint(0, 15), new PlanPoint(100, 15)),
            unaryScore: 0.72);
        var graph = Graph(
            new[] { first, bridge, second },
            new[]
            {
                Relation(
                    StructuralEvidenceRelationKind.Continuation,
                    first,
                    bridge,
                    weight: 0.20),
                Relation(
                    StructuralEvidenceRelationKind.Continuation,
                    bridge,
                    second,
                    weight: 0.20)
            });

        var solution = JointStructuralSolver.Solve(
            graph,
            new StructuralSolverOptions
            {
                AxisTolerance = 3
            });

        Assert.Equal(3, solution.Metrics.SelectedCandidateCount);
        Assert.Equal(2, solution.WallRuns.Count);
        Assert.All(
            solution.WallRuns,
            run => Assert.InRange(
                Math.Abs(run.CenterLine.Start.Y - run.CenterLine.End.Y),
                0,
                0.001));
        Assert.DoesNotContain(
            solution.WallRuns,
            run => run.CandidateIds.Count == 3);
    }

    [Fact]
    public void JointSolver_SelectsOnlyOneHardDuplicateRepresentation()
    {
        var first = Candidate(
            "first",
            new PlanLineSegment(new PlanPoint(0, 10), new PlanPoint(100, 10)),
            unaryScore: 0.70);
        var second = Candidate(
            "second",
            new PlanLineSegment(new PlanPoint(0, 10.5), new PlanPoint(100, 10.5)),
            unaryScore: 0.62);
        var relation = Relation(
            StructuralEvidenceRelationKind.Duplicate,
            first,
            second,
            weight: -1.30,
            hard: true);
        var graph = Graph(new[] { first, second }, new[] { relation });

        var solution = JointStructuralSolver.Solve(graph);

        Assert.Equal(1, solution.Metrics.SelectedCandidateCount);
        Assert.Single(solution.WallRuns);
        Assert.Single(
            solution.CandidateDecisions,
            decision => decision.Decision == StructuralWallDecisionKind.Selected);
    }

    [Fact]
    public void JointSolver_PrefersCleanLongWallBodyOverContainedFragments()
    {
        var longWall = Candidate(
            "long-wall-body",
            new PlanLineSegment(new PlanPoint(0, 10), new PlanPoint(200, 10)),
            unaryScore: 1.90) with
        {
            Signals = [Signal(StructuralEvidenceSignalKind.WallBody, 0.34)]
        };
        var firstFragment = Candidate(
            "first-fragment",
            new PlanLineSegment(new PlanPoint(0, 10), new PlanPoint(70, 10)),
            unaryScore: 2.00) with
        {
            Signals = [Signal(StructuralEvidenceSignalKind.WallBody, 0.20)]
        };
        var secondFragment = Candidate(
            "second-fragment",
            new PlanLineSegment(new PlanPoint(130, 10), new PlanPoint(200, 10)),
            unaryScore: 2.00) with
        {
            Signals = [Signal(StructuralEvidenceSignalKind.WallBody, 0.20)]
        };
        var graph = Graph(
            new[] { longWall, firstFragment, secondFragment },
            new[]
            {
                Relation(
                    StructuralEvidenceRelationKind.Duplicate,
                    longWall,
                    firstFragment,
                    weight: -1.35,
                    hard: true),
                Relation(
                    StructuralEvidenceRelationKind.Duplicate,
                    longWall,
                    secondFragment,
                    weight: -1.35,
                    hard: true)
            });

        var solution = JointStructuralSolver.Solve(graph);

        var run = Assert.Single(solution.WallRuns);
        Assert.Equal(200, run.DrawingLength, precision: 6);
        Assert.Equal(new[] { longWall.Id }, run.CandidateIds);
        Assert.All(
            solution.CandidateDecisions.Where(decision =>
                decision.CandidateId == firstFragment.Id
                || decision.CandidateId == secondFragment.Id),
            decision =>
            {
                Assert.Equal(StructuralWallDecisionKind.Rejected, decision.Decision);
                Assert.Contains(
                    decision.Reasons,
                    reason => reason.Contains(
                        "contained fragment",
                        StringComparison.OrdinalIgnoreCase));
            });
    }

    [Fact]
    public void JointSolver_DoesNotLetSemanticallySuspectLongLineDominateCleanFragments()
    {
        var suspectLongLine = Candidate(
            "dimension-crossing-wall",
            new PlanLineSegment(new PlanPoint(0, 10), new PlanPoint(200, 10)),
            unaryScore: 1.90) with
        {
            Signals =
            [
                Signal(StructuralEvidenceSignalKind.WallBody, 0.34),
                Signal(StructuralEvidenceSignalKind.DimensionOrAnnotation, -0.10)
            ]
        };
        var firstFragment = Candidate(
            "first-clean-fragment",
            new PlanLineSegment(new PlanPoint(0, 10), new PlanPoint(70, 10)),
            unaryScore: 2.00);
        var secondFragment = Candidate(
            "second-clean-fragment",
            new PlanLineSegment(new PlanPoint(130, 10), new PlanPoint(200, 10)),
            unaryScore: 2.00);
        var graph = Graph(
            new[] { suspectLongLine, firstFragment, secondFragment },
            new[]
            {
                Relation(
                    StructuralEvidenceRelationKind.Duplicate,
                    suspectLongLine,
                    firstFragment,
                    weight: -1.35,
                    hard: true),
                Relation(
                    StructuralEvidenceRelationKind.Duplicate,
                    suspectLongLine,
                    secondFragment,
                    weight: -1.35,
                    hard: true)
            });

        var solution = JointStructuralSolver.Solve(graph);

        Assert.DoesNotContain(
            solution.CandidateDecisions,
            decision =>
                decision.CandidateId == suspectLongLine.Id
                && decision.Decision == StructuralWallDecisionKind.Selected);
        Assert.Contains(
            solution.CandidateDecisions,
            decision =>
                decision.CandidateId == firstFragment.Id
                && decision.Decision == StructuralWallDecisionKind.Selected);
        Assert.Contains(
            solution.CandidateDecisions,
            decision =>
                decision.CandidateId == secondFragment.Id
                && decision.Decision == StructuralWallDecisionKind.Selected);
    }

    [Fact]
    public void EvidenceGraph_UsesShortWallAsContinuationForSourceBackedExteriorShell()
    {
        var shell = Wall(
            "page:1:wall-exterior-shell-source-backed:001",
            new PlanLineSegment(new PlanPoint(0, 10), new PlanPoint(300, 10)),
            new Confidence(0.68)) with
        {
            WallType = WallType.Exterior,
            Evidence =
            [
                "wall evidence: source-backed exterior shell closure recovered from long PDF line with shell anchors"
            ]
        };
        var fragment = Wall(
            "shell-end-fragment",
            new PlanLineSegment(new PlanPoint(250, 10.2), new PlanPoint(300, 10.2)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            WallType = WallType.Exterior
        };
        var assessments = new[]
        {
            new WallEvidenceWallAssessment(
                shell.Id,
                shell.PageNumber,
                shell.Bounds,
                WallEvidenceCategory.RecoveredWallBody,
                shell.Confidence,
                PlacementReady: true,
                RequiresReview: false,
                RejectedAsNoise: false,
                shell.SourcePrimitiveIds,
                shell.Evidence)
            {
                Decision = WallEvidenceDecision.Accept
            },
            new WallEvidenceWallAssessment(
                fragment.Id,
                fragment.PageNumber,
                fragment.Bounds,
                WallEvidenceCategory.StrongWallBody,
                fragment.Confidence,
                PlacementReady: true,
                RequiresReview: false,
                RejectedAsNoise: false,
                fragment.SourcePrimitiveIds,
                ["strong parallel-face wall pair"])
            {
                Decision = WallEvidenceDecision.Accept
            }
        };
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: [shell, fragment],
                acceptedWalls: [shell, fragment],
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    assessments,
                    SourceCandidateWallCount: 2)));

        var shellCandidate = Assert.Single(
            graph.WallCandidates,
            candidate => candidate.SourceWallIds.Contains(shell.Id, StringComparer.Ordinal));
        Assert.True(shellCandidate.Origins.HasFlag(StructuralCandidateOrigin.ExteriorShell));
        Assert.Contains(
            shellCandidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.ExteriorShell);
        var relation = Assert.Single(
            graph.Relations,
            item => item.Kind == StructuralEvidenceRelationKind.Continuation
                && (item.FirstCandidateId == shellCandidate.Id
                    || item.SecondCandidateId == shellCandidate.Id));
        Assert.Equal(StructuralEvidenceRelationKind.Continuation, relation.Kind);
        Assert.False(relation.IsHardConstraint);

        var solution = JointStructuralSolver.Solve(graph);
        var run = Assert.Single(solution.WallRuns);
        Assert.Equal(300, run.DrawingLength, precision: 6);
        Assert.Contains(shellCandidate.Id, run.CandidateIds);
    }

    [Fact]
    public void EvidenceGraph_KeepsOrdinaryContainedWallEvidenceAsDuplicate()
    {
        var longWall = Wall(
            "ordinary-long-wall",
            new PlanLineSegment(new PlanPoint(0, 10), new PlanPoint(300, 10)),
            Confidence.High);
        var shortWall = Wall(
            "ordinary-short-wall",
            new PlanLineSegment(new PlanPoint(250, 10.2), new PlanPoint(300, 10.2)),
            Confidence.High);
        var assessments = new[]
        {
            AcceptedAssessment(longWall, WallEvidenceCategory.MediumWallBody),
            AcceptedAssessment(shortWall, WallEvidenceCategory.StrongWallBody)
        };
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: [longWall, shortWall],
                acceptedWalls: [longWall, shortWall],
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    assessments,
                    SourceCandidateWallCount: 2)));

        Assert.Contains(
            graph.Relations,
            relation => relation.Kind == StructuralEvidenceRelationKind.Duplicate);
        Assert.DoesNotContain(
            graph.Relations,
            relation => relation.Kind == StructuralEvidenceRelationKind.Continuation);
    }

    [Fact]
    public void JointSolver_SuppressesExplicitDuplicateWallFaceBehindReferencedWallBody()
    {
        var pairedBody = Candidate(
            "paired-body",
            new PlanLineSegment(new PlanPoint(0, 10), new PlanPoint(200, 10)),
            unaryScore: 1.10) with
        {
            SourceWallIds = ["wall-paired"],
            Signals = [Signal(StructuralEvidenceSignalKind.WallBody, 0.34)]
        };
        var duplicateFace = Candidate(
            "duplicate-face",
            new PlanLineSegment(new PlanPoint(20, 18), new PlanPoint(180, 18)),
            unaryScore: 1.18) with
        {
            SourceWallIds = ["wall-face"],
            Evidence =
            [
                "wall evidence: duplicate wall-face line already represented by stronger paired wall body wall-paired; keep for review but block exact placement"
            ]
        };
        var graph = Graph(
            new[] { pairedBody, duplicateFace },
            Array.Empty<StructuralEvidenceRelation>());

        var solution = JointStructuralSolver.Solve(graph);

        var run = Assert.Single(solution.WallRuns);
        Assert.Equal(new[] { pairedBody.Id }, run.CandidateIds);
        var decision = Assert.Single(
            solution.CandidateDecisions,
            candidate => candidate.CandidateId == duplicateFace.Id);
        Assert.Equal(StructuralWallDecisionKind.Rejected, decision.Decision);
        Assert.Contains(
            decision.Reasons,
            reason => reason.Contains(
                "duplicate wall-face",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void JointSolver_RetainsExplicitDuplicateFallbackWhenReferencedBodyIsUnavailable()
    {
        var duplicateFace = Candidate(
            "duplicate-face-fallback",
            new PlanLineSegment(new PlanPoint(20, 18), new PlanPoint(180, 18)),
            unaryScore: 1.18) with
        {
            SourceWallIds = ["wall-face"],
            Evidence =
            [
                "wall evidence: duplicate wall-face line already represented by stronger paired wall body missing-wall; keep for review but block exact placement"
            ]
        };
        var graph = Graph(
            new[] { duplicateFace },
            Array.Empty<StructuralEvidenceRelation>());

        var solution = JointStructuralSolver.Solve(graph);

        var run = Assert.Single(solution.WallRuns);
        Assert.Equal(new[] { duplicateFace.Id }, run.CandidateIds);
    }

    [Fact]
    public void JointSolver_SuppressesShortWallFaceWithSharedSourceProvenance()
    {
        var sharedPrimitiveIds = Enumerable.Range(1, 8)
            .Select(index => $"primitive:shared:{index}")
            .ToArray();
        var pairedBody = Candidate(
            "shared-paired-body",
            new PlanLineSegment(new PlanPoint(0, 10), new PlanPoint(220, 10)),
            unaryScore: 1.35) with
        {
            Thickness = 6,
            SourcePrimitiveIds = sharedPrimitiveIds
                .Append("primitive:body-only")
                .ToArray(),
            SourceOpeningIds = ["opening:1"],
            Signals = [Signal(StructuralEvidenceSignalKind.WallBody, 0.34)]
        };
        var duplicateFace = Candidate(
            "shared-short-face",
            new PlanLineSegment(new PlanPoint(40, 14), new PlanPoint(100, 14)),
            unaryScore: 1.40) with
        {
            SourcePrimitiveIds = sharedPrimitiveIds
                .Take(5)
                .Append("primitive:face-only")
                .ToArray(),
            SourceOpeningIds = ["opening:1"],
            Signals = [Signal(StructuralEvidenceSignalKind.WallBody, 0.20)]
        };
        var graph = Graph(
            new[] { pairedBody, duplicateFace },
            Array.Empty<StructuralEvidenceRelation>());

        var solution = JointStructuralSolver.Solve(graph);

        var run = Assert.Single(solution.WallRuns);
        Assert.Equal(new[] { pairedBody.Id }, run.CandidateIds);
        Assert.Contains(
            solution.CandidateDecisions,
            decision =>
                decision.CandidateId == duplicateFace.Id
                && decision.Decision == StructuralWallDecisionKind.Rejected);
    }

    [Fact]
    public void JointSolver_PreservesNearbyWallWhenSharedPrimitiveEvidenceIsWeak()
    {
        var pairedBody = Candidate(
            "nearby-paired-body",
            new PlanLineSegment(new PlanPoint(0, 10), new PlanPoint(220, 10)),
            unaryScore: 1.35) with
        {
            Thickness = 6,
            SourcePrimitiveIds =
            [
                "primitive:shared",
                "primitive:body:1",
                "primitive:body:2"
            ],
            Signals = [Signal(StructuralEvidenceSignalKind.WallBody, 0.34)]
        };
        var nearbyWall = Candidate(
            "nearby-real-wall",
            new PlanLineSegment(new PlanPoint(40, 14), new PlanPoint(100, 14)),
            unaryScore: 1.40) with
        {
            SourcePrimitiveIds =
            [
                "primitive:shared",
                "primitive:nearby:1",
                "primitive:nearby:2",
                "primitive:nearby:3",
                "primitive:nearby:4"
            ],
            Signals = [Signal(StructuralEvidenceSignalKind.WallBody, 0.20)]
        };
        var graph = Graph(
            new[] { pairedBody, nearbyWall },
            Array.Empty<StructuralEvidenceRelation>());

        var solution = JointStructuralSolver.Solve(graph);

        Assert.Equal(2, solution.Metrics.SelectedCandidateCount);
        Assert.Equal(2, solution.WallRuns.Count);
    }

    [Fact]
    public void EvidenceGraph_AddsOppositeSideSupportForSharedRoomWall()
    {
        var wall = Wall(
            "shared-partition",
            new PlanLineSegment(new PlanPoint(50, 0), new PlanPoint(50, 100)),
            Confidence.High);
        var rooms = new[]
        {
            Room(
                "left-room",
                new PlanRect(0, 0, 50, 100),
                new[]
                {
                    new PlanPoint(0, 0),
                    new PlanPoint(50, 0),
                    new PlanPoint(50, 100),
                    new PlanPoint(0, 100)
                }),
            Room(
                "right-room",
                new PlanRect(50, 0, 50, 100),
                new[]
                {
                    new PlanPoint(50, 0),
                    new PlanPoint(100, 0),
                    new PlanPoint(100, 100),
                    new PlanPoint(50, 100)
                })
        };

        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: new[] { wall },
                acceptedWalls: new[] { wall },
                rooms: rooms));

        var candidate = Assert.Single(
            graph.WallCandidates,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Equal(2, candidate.SourceRoomIds.Count);
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.OppositeRoomBoundary
                && signal.Weight > 0);
    }

    [Fact]
    public void EvidenceGraph_DoesNotAddOppositeSideSupportForSameSideRoomCycles()
    {
        var wall = Wall(
            "same-side-line",
            new PlanLineSegment(new PlanPoint(50, 0), new PlanPoint(50, 100)),
            Confidence.High);
        var rooms = new[]
        {
            Room(
                "upper-left-room",
                new PlanRect(0, 0, 50, 45),
                new[]
                {
                    new PlanPoint(0, 0),
                    new PlanPoint(50, 0),
                    new PlanPoint(50, 45),
                    new PlanPoint(0, 45)
                }),
            Room(
                "lower-left-room",
                new PlanRect(0, 55, 50, 45),
                new[]
                {
                    new PlanPoint(0, 55),
                    new PlanPoint(50, 55),
                    new PlanPoint(50, 100),
                    new PlanPoint(0, 100)
                })
        };

        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: new[] { wall },
                acceptedWalls: new[] { wall },
                rooms: rooms));

        var candidate = Assert.Single(
            graph.WallCandidates,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Equal(2, candidate.SourceRoomIds.Count);
        Assert.DoesNotContain(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.OppositeRoomBoundary);
    }

    [Fact]
    public void EvidenceGraph_DemotesExcludedIsolatedWallIsland()
    {
        var wall = Wall(
            "isolated-detail-line",
            new PlanLineSegment(new PlanPoint(200, 20), new PlanPoint(200, 180)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            Evidence = new[]
            {
                "wall belongs to non-structural or isolated graph component"
            }
        };
        var wallGraph = WallGraphFor(
            new[] { wall },
            WallGraphComponentKind.IsolatedFragment,
            excludedFromStructuralTopology: true);
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: new[] { wall },
                acceptedWalls: new[] { wall },
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    new[] { AcceptedAssessment(wall, WallEvidenceCategory.StrongWallBody) },
                    SourceCandidateWallCount: 1),
                wallGraph: wallGraph));

        var candidate = Assert.Single(
            graph.WallCandidates,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.True(candidate.HasStrongNegativeEvidence);
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.IsolatedStructuralIsland
                && signal.Weight <= -1);
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.ExistingGraph
                && signal.Weight == 0);
        Assert.All(
            candidate.Signals.Where(signal => signal.Kind == StructuralEvidenceSignalKind.Junction),
            signal => Assert.Equal(0, signal.Weight));

        var solution = JointStructuralSolver.Solve(graph);
        Assert.DoesNotContain(
            solution.CandidateDecisions,
            decision =>
                decision.CandidateId == candidate.Id
                && decision.Decision == StructuralWallDecisionKind.Selected);
        Assert.DoesNotContain(
            solution.WallRuns,
            run => run.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_PreservesPromotedLongExteriorShellInIsolatedComponent()
    {
        var wall = Wall(
            "promoted-long-exterior-shell",
            new PlanLineSegment(new PlanPoint(200, 20), new PlanPoint(200, 260)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            WallType = WallType.Exterior,
            Evidence =
            [
                "parallel wall-face pair",
                "filled wall-solid primitive",
                "wall evidence: filled closed vector wall body",
                "wall type refined exterior: trusted long isolated exterior shell wall body",
                "wall evidence: trusted long isolated exterior shell promoted to placement-ready after shell-continuity review"
            ]
        };
        var wallGraph = WallGraphFor(
            [wall],
            WallGraphComponentKind.IsolatedFragment,
            excludedFromStructuralTopology: true);
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: [wall],
                acceptedWalls: [wall],
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    [AcceptedAssessment(wall, WallEvidenceCategory.MediumWallBody)],
                    SourceCandidateWallCount: 1),
                wallGraph: wallGraph));

        var candidate = Assert.Single(
            graph.WallCandidates,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Equal(
            new[] { "component:test" },
            candidate.SourceWallComponentIds);
        Assert.True(candidate.Origins.HasFlag(StructuralCandidateOrigin.ExteriorShell));
        Assert.DoesNotContain(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.IsolatedStructuralIsland);
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.StructuralTerritory
                && signal.Weight > 0);

        var solution = JointStructuralSolver.Solve(graph);
        Assert.Contains(
            solution.CandidateDecisions,
            decision =>
                decision.CandidateId == candidate.Id
                && decision.Decision == StructuralWallDecisionKind.Selected);
        Assert.Contains(
            solution.WallRuns,
            run => run.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_PreservesPromotedRoomConfirmedBodyInIsolatedComponent()
    {
        var wall = PairedWall(
            "promoted-room-confirmed-wall",
            y: 100,
            thickness: 5,
            length: 180) with
        {
            SourcePrimitiveIds =
            [
                "pdf:p1:path:42:subpath:1:line:4",
                "pdf:p1:path:100:subpath:1:line:1",
                "pdf:p1:path:101:subpath:1:line:1",
                "pdf:p1:path:102:subpath:1:line:1",
                "pdf:p1:path:103:subpath:1:line:1",
                "pdf:p1:path:104:subpath:1:line:1",
                "pdf:p1:path:105:subpath:1:line:1",
                "pdf:p1:path:106:subpath:1:line:1",
                "pdf:p1:path:107:subpath:1:line:1",
                "pdf:p1:path:108:subpath:1:line:1"
            ],
            Evidence =
            [
                "parallel wall-face pair",
                "layer (unlayered) classified Dimension (0.24)",
                "layer evidence: contains dimension-like text",
                "wall evidence: room-confirmed wall body promoted to placement-ready after room adjacency refinement",
                "wall evidence: room-confirmed isolated wall graph fragment kept placement-ready because room boundary evidence overrode early isolated graph classification"
            ]
        };
        var dimension = new DimensionAnnotation(
            "dimension-crossing-room-confirmed-wall",
            1,
            DimensionKind.Linear,
            DimensionOrientation.Horizontal,
            "1 300",
            "1300",
            new PlanRect(80, 100, 220, 20),
            PlanMeasurementUnit.Millimeter,
            1300,
            new PlanLineSegment(new PlanPoint(80, 100), new PlanPoint(300, 100)),
            220,
            5.91,
            Confidence.High,
            null,
            ["pdf:p1:word:1", "pdf:p1:path:42:subpath:1:line:2"],
            ["dimension crosses one source family"]);
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: [wall],
                acceptedWalls: [wall],
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    [AcceptedAssessment(wall, WallEvidenceCategory.MediumWallBody)],
                    SourceCandidateWallCount: 1),
                wallGraph: WallGraphFor(
                    [wall],
                    WallGraphComponentKind.IsolatedFragment,
                    excludedFromStructuralTopology: true),
                dimensions: [dimension]));

        var candidate = Assert.Single(
            graph.WallCandidates,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.True(candidate.Origins.HasFlag(StructuralCandidateOrigin.RoomBoundary));
        Assert.DoesNotContain(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.IsolatedStructuralIsland);
        var dimensionSignal = Assert.Single(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.DimensionOrAnnotation);
        Assert.InRange(dimensionSignal.Weight, -0.12, -0.08);
        Assert.False(candidate.HasAbsoluteBlockingEvidence);

        var solution = JointStructuralSolver.Solve(graph);
        Assert.Contains(
            solution.CandidateDecisions,
            decision =>
                decision.CandidateId == candidate.Id
                && decision.Decision == StructuralWallDecisionKind.Selected);
        Assert.Contains(
            solution.WallRuns,
            run => run.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_DoesNotLetRoomlessOpeningAnchorExcludedWallIsland()
    {
        var wall = Wall(
            "isolated-window-like-detail",
            new PlanLineSegment(new PlanPoint(240, 180), new PlanPoint(340, 180)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair
        };
        var opening = new OpeningCandidate(
            "roomless-window-candidate",
            1,
            OpeningType.Window,
            new PlanRect(270, 174, 40, 12),
            new Confidence(0.58))
        {
            WallId = wall.Id,
            HostWallIds = new[] { wall.Id },
            AdjacentWallIds = new[] { wall.Id },
            CenterLine = new PlanLineSegment(
                new PlanPoint(270, 180),
                new PlanPoint(310, 180))
        };
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: new[] { wall },
                acceptedWalls: new[] { wall },
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    new[] { AcceptedAssessment(wall, WallEvidenceCategory.StrongWallBody) },
                    SourceCandidateWallCount: 1),
                wallGraph: WallGraphFor(
                    new[] { wall },
                    WallGraphComponentKind.IsolatedFragment,
                    excludedFromStructuralTopology: false),
                openings: new[] { opening }));

        var candidate = Assert.Single(
            graph.WallCandidates,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Contains(opening.Id, candidate.SourceOpeningIds);
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.IsolatedStructuralIsland
                && signal.Weight <= -1);
        Assert.DoesNotContain(
            JointStructuralSolver.Solve(graph).WallRuns,
            run => run.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_DoesNotLetWeakRoomTouchAnchorExcludedWallIsland()
    {
        var wall = Wall(
            "isolated-room-touch-detail",
            new PlanLineSegment(new PlanPoint(240, 180), new PlanPoint(340, 180)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair
        };
        var room = Room(
            "trusted-opening-room",
            new PlanRect(0, 0, 120, 80),
            new[]
            {
                new PlanPoint(0, 0),
                new PlanPoint(120, 0),
                new PlanPoint(120, 80),
                new PlanPoint(0, 80)
            });
        var opening = new OpeningCandidate(
            "weak-room-touch-opening",
            1,
            OpeningType.Window,
            new PlanRect(270, 174, 40, 12),
            new Confidence(0.58))
        {
            WallId = wall.Id,
            HostWallIds = new[] { wall.Id },
            AdjacentWallIds = new[] { wall.Id },
            ConnectedRoomIds = new[] { room.Id },
            ConnectedRoomLinks = new[]
            {
                new OpeningRoomConnection(
                    room.Id,
                    "Room",
                    RoomUseKind.Office,
                    Array.Empty<string>(),
                    OpeningRoomSide.PositiveNormalSide,
                    new PlanPoint(120, 80),
                    new PlanPoint(120, 80),
                    SignedDistanceFromOpening: 12,
                    DistanceToOpening: 18,
                    SharesHostWall: false,
                    new Confidence(0.50),
                    new[] { "opening only touches room proximity envelope" })
            },
            CenterLine = new PlanLineSegment(
                new PlanPoint(270, 180),
                new PlanPoint(310, 180))
        };
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: new[] { wall },
                acceptedWalls: new[] { wall },
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    new[] { AcceptedAssessment(wall, WallEvidenceCategory.StrongWallBody) },
                    SourceCandidateWallCount: 1),
                wallGraph: WallGraphFor(
                    new[] { wall },
                    WallGraphComponentKind.IsolatedFragment,
                    excludedFromStructuralTopology: false),
                rooms: new[] { room },
                openings: new[] { opening }));

        var candidate = Assert.Single(
            graph.WallCandidates,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Contains(opening.Id, candidate.SourceOpeningIds);
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.IsolatedStructuralIsland
                && signal.Weight <= -1);
        Assert.DoesNotContain(
            JointStructuralSolver.Solve(graph).WallRuns,
            run => run.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_DoesNotPropagateTrustIntoMediumIsolatedDetail()
    {
        var host = Wall(
            "trusted-room-host",
            new PlanLineSegment(new PlanPoint(0, 100), new PlanPoint(200, 100)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair
        };
        var detail = Wall(
            "nearby-medium-detail",
            new PlanLineSegment(new PlanPoint(100, 105), new PlanPoint(100, 160)),
            new Confidence(0.72)) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            PairEvidence = new WallPairEvidence(
                new PlanLineSegment(
                    new PlanPoint(98, 105),
                    new PlanPoint(98, 160)),
                new PlanLineSegment(
                    new PlanPoint(102, 105),
                    new PlanPoint(102, 160)),
                FaceSeparation: 4,
                OverlapRatio: 1,
                Score: 0.96,
                FirstFaceFragmentCount: 14,
                SecondFaceFragmentCount: 14,
                FirstFaceSourcePrimitiveIds: new[] { "primitive:detail:first" },
                SecondFaceSourcePrimitiveIds: new[] { "primitive:detail:second" })
        };
        var room = Room(
            "trusted-room",
            new PlanRect(0, 0, 200, 100),
            new[]
            {
                new PlanPoint(0, 0),
                new PlanPoint(200, 0),
                new PlanPoint(200, 100),
                new PlanPoint(0, 100)
            });
        var hostGraph = WallGraphFor(
            new[] { host },
            WallGraphComponentKind.MainStructural,
            excludedFromStructuralTopology: false,
            componentId: "component:trusted-host");
        var detailGraph = WallGraphFor(
            new[] { detail },
            WallGraphComponentKind.IsolatedFragment,
            excludedFromStructuralTopology: false,
            componentId: "component:medium-detail");
        var wallGraph = new WallGraph(
            hostGraph.Nodes.Concat(detailGraph.Nodes).ToArray(),
            hostGraph.Edges.Concat(detailGraph.Edges).ToArray(),
            hostGraph.Components.Concat(detailGraph.Components).ToArray());
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: new[] { host, detail },
                acceptedWalls: new[] { host, detail },
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    new WallEvidenceWallAssessment[]
                    {
                        AcceptedAssessment(host, WallEvidenceCategory.StrongWallBody),
                        ReviewAssessment(detail, WallEvidenceCategory.MediumWallBody)
                    },
                    SourceCandidateWallCount: 2),
                wallGraph: wallGraph,
                rooms: new[] { room }));

        var detailCandidate = Assert.Single(
            graph.WallCandidates,
            candidate => candidate.SourceWallIds.Contains(
                detail.Id,
                StringComparer.Ordinal));
        Assert.Contains(
            detailCandidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.IsolatedStructuralIsland
                && signal.Weight <= -1);
        Assert.Contains(
            detailCandidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.WallBody
                && signal.Weight >= 0.30);
        Assert.DoesNotContain(
            JointStructuralSolver.Solve(graph).WallRuns,
            run => run.SourceWallIds.Contains(detail.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_PreservesRoomAnchoredWallFromExcludedComponent()
    {
        var wall = Wall(
            "room-anchored-island",
            new PlanLineSegment(new PlanPoint(0, 0), new PlanPoint(120, 0)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair
        };
        var room = Room(
            "trusted-room",
            new PlanRect(0, 0, 120, 80),
            new[]
            {
                new PlanPoint(0, 0),
                new PlanPoint(120, 0),
                new PlanPoint(120, 80),
                new PlanPoint(0, 80)
            });
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: new[] { wall },
                acceptedWalls: new[] { wall },
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    new[] { AcceptedAssessment(wall, WallEvidenceCategory.StrongWallBody) },
                    SourceCandidateWallCount: 1),
                wallGraph: WallGraphFor(
                    new[] { wall },
                    WallGraphComponentKind.IsolatedFragment,
                    excludedFromStructuralTopology: true),
                rooms: new[] { room }));

        var candidate = Assert.Single(
            graph.WallCandidates,
            value => value.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Contains(room.Id, candidate.SourceRoomIds);
        Assert.DoesNotContain(
            candidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.IsolatedStructuralIsland);
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.StructuralTerritory
                && signal.Weight > 0);

        var solution = JointStructuralSolver.Solve(graph);
        Assert.Contains(
            solution.CandidateDecisions,
            decision =>
                decision.CandidateId == candidate.Id
                && decision.Decision == StructuralWallDecisionKind.Selected);
    }

    [Fact]
    public void EvidenceGraph_PreservesCoherentDetachedSecondaryComponent()
    {
        var horizontal = Wall(
            "detached-wing-horizontal",
            new PlanLineSegment(new PlanPoint(300, 40), new PlanPoint(440, 40)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair
        };
        var vertical = Wall(
            "detached-wing-vertical",
            new PlanLineSegment(new PlanPoint(440, 40), new PlanPoint(440, 160)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair
        };
        var walls = new[] { horizontal, vertical };
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: walls,
                acceptedWalls: walls,
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    walls
                        .Select(wall => AcceptedAssessment(wall, WallEvidenceCategory.StrongWallBody))
                        .ToArray(),
                    SourceCandidateWallCount: walls.Length),
                wallGraph: WallGraphFor(
                    walls,
                    WallGraphComponentKind.SecondaryStructural,
                    excludedFromStructuralTopology: false)));

        var candidates = graph.WallCandidates
            .Where(candidate => candidate.SourceWallIds.Any(wallId =>
                walls.Any(wall => string.Equals(wall.Id, wallId, StringComparison.Ordinal))))
            .ToArray();
        Assert.Equal(2, candidates.Length);
        Assert.All(
            candidates,
            candidate =>
            {
                Assert.DoesNotContain(
                    candidate.Signals,
                    signal => signal.Kind == StructuralEvidenceSignalKind.IsolatedStructuralIsland);
                Assert.Contains(
                    candidate.Signals,
                    signal =>
                        signal.Kind == StructuralEvidenceSignalKind.StructuralTerritory
                        && signal.Weight > 0);
            });

        var solution = JointStructuralSolver.Solve(graph);
        Assert.All(
            candidates,
            candidate => Assert.Contains(
                solution.CandidateDecisions,
                decision =>
                    decision.CandidateId == candidate.Id
                    && decision.Decision == StructuralWallDecisionKind.Selected));
    }

    [Fact]
    public void EvidenceGraph_PreservesExcludedBranchAtStructuralTJunction()
    {
        var host = Wall(
            "room-anchored-host",
            new PlanLineSegment(new PlanPoint(0, 100), new PlanPoint(200, 100)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair
        };
        var branch = Wall(
            "isolated-t-junction-branch",
            new PlanLineSegment(new PlanPoint(100, 100), new PlanPoint(100, 180)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair
        };
        var room = Room(
            "host-room",
            new PlanRect(0, 0, 200, 100),
            new[]
            {
                new PlanPoint(0, 0),
                new PlanPoint(200, 0),
                new PlanPoint(200, 100),
                new PlanPoint(0, 100)
            });
        var hostGraph = WallGraphFor(
            new[] { host },
            WallGraphComponentKind.MainStructural,
            excludedFromStructuralTopology: false,
            componentId: "component:host");
        var branchGraph = WallGraphFor(
            new[] { branch },
            WallGraphComponentKind.IsolatedFragment,
            excludedFromStructuralTopology: true,
            componentId: "component:branch");
        var wallGraph = new WallGraph(
            hostGraph.Nodes.Concat(branchGraph.Nodes).ToArray(),
            hostGraph.Edges.Concat(branchGraph.Edges).ToArray(),
            hostGraph.Components.Concat(branchGraph.Components).ToArray());
        var walls = new[] { host, branch };
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: walls,
                acceptedWalls: walls,
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    walls
                        .Select(wall => AcceptedAssessment(
                            wall,
                            WallEvidenceCategory.StrongWallBody))
                        .ToArray(),
                    SourceCandidateWallCount: walls.Length),
                wallGraph: wallGraph,
                rooms: new[] { room }));

        var branchCandidate = Assert.Single(
            graph.WallCandidates,
            candidate => candidate.SourceWallIds.Contains(
                branch.Id,
                StringComparer.Ordinal));
        Assert.DoesNotContain(
            branchCandidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.IsolatedStructuralIsland);
        Assert.Contains(
            branchCandidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.StructuralTerritory
                && signal.Weight > 0);
        Assert.Contains(
            graph.Relations,
            relation =>
                relation.Kind == StructuralEvidenceRelationKind.Junction
                && relation.Evidence.Any(item =>
                    item.Contains(
                        "endpoint-to-wall T-junction",
                        StringComparison.Ordinal)));

        var solution = JointStructuralSolver.Solve(graph);
        Assert.Contains(
            solution.WallRuns,
            run => run.SourceWallIds.Contains(branch.Id, StringComparer.Ordinal));
        Assert.Contains(
            solution.Junctions,
            junction =>
                junction.Kind == StructuralJunctionKind.Tee
                && junction.Position.DistanceTo(new PlanPoint(100, 100)) <= 0.001);
    }

    [Fact]
    public void EvidenceGraph_DoesNotPromoteReviewDetailAtStructuralTJunction()
    {
        var host = Wall(
            "trusted-detail-host",
            new PlanLineSegment(new PlanPoint(0, 100), new PlanPoint(200, 100)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair
        };
        var detail = Wall(
            "review-detail-branch",
            new PlanLineSegment(new PlanPoint(100, 100), new PlanPoint(100, 160)),
            new Confidence(0.72)) with
        {
            DetectionKind = WallDetectionKind.FragmentMerged
        };
        var room = Room(
            "detail-host-room",
            new PlanRect(0, 0, 200, 100),
            new[]
            {
                new PlanPoint(0, 0),
                new PlanPoint(200, 0),
                new PlanPoint(200, 100),
                new PlanPoint(0, 100)
            });
        var hostGraph = WallGraphFor(
            new[] { host },
            WallGraphComponentKind.MainStructural,
            excludedFromStructuralTopology: false,
            componentId: "component:detail-host");
        var detailGraph = WallGraphFor(
            new[] { detail },
            WallGraphComponentKind.IsolatedFragment,
            excludedFromStructuralTopology: true,
            componentId: "component:detail");
        var wallGraph = new WallGraph(
            hostGraph.Nodes.Concat(detailGraph.Nodes).ToArray(),
            hostGraph.Edges.Concat(detailGraph.Edges).ToArray(),
            hostGraph.Components.Concat(detailGraph.Components).ToArray());
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: new[] { host, detail },
                acceptedWalls: new[] { host },
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    new WallEvidenceWallAssessment[]
                    {
                        AcceptedAssessment(host, WallEvidenceCategory.StrongWallBody),
                        ReviewAssessment(
                            detail,
                            WallEvidenceCategory.ObjectOrFixtureDetail)
                    },
                    SourceCandidateWallCount: 2),
                wallGraph: wallGraph,
                rooms: new[] { room }));

        var detailCandidate = Assert.Single(
            graph.WallCandidates,
            candidate => candidate.SourceWallIds.Contains(
                detail.Id,
                StringComparer.Ordinal));
        Assert.True(detailCandidate.HasBlockingSemanticEvidence);
        Assert.Contains(
            detailCandidate.Signals,
            signal => signal.Kind == StructuralEvidenceSignalKind.IsolatedStructuralIsland);
        Assert.DoesNotContain(
            graph.Relations,
            relation =>
                (relation.FirstCandidateId == detailCandidate.Id
                    || relation.SecondCandidateId == detailCandidate.Id)
                && relation.Evidence.Any(item =>
                    item.Contains(
                        "endpoint-to-wall T-junction",
                        StringComparison.Ordinal)));

        var solution = JointStructuralSolver.Solve(graph);
        Assert.DoesNotContain(
            solution.WallRuns,
            run => run.SourceWallIds.Contains(detail.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_BlocksDenseRepeatedPairedDetailFamily()
    {
        var details = Enumerable.Range(0, 7)
            .Select(index => Wall(
                $"paired-tread-{index}",
                new PlanLineSegment(
                    new PlanPoint(20, 20 + (index * 8)),
                    new PlanPoint(80, 20 + (index * 8))),
                Confidence.High) with
            {
                DetectionKind = WallDetectionKind.ParallelLinePair
            })
            .ToArray();
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: details,
                acceptedWalls: details,
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    details
                        .Select(wall => AcceptedAssessment(
                            wall,
                            WallEvidenceCategory.StrongWallBody))
                        .ToArray(),
                    SourceCandidateWallCount: details.Length)));

        var detailCandidates = graph.WallCandidates
            .Where(candidate => candidate.SourceWallIds.Any(wallId =>
                details.Any(detail => string.Equals(
                    detail.Id,
                    wallId,
                    StringComparison.Ordinal))))
            .ToArray();
        Assert.Equal(details.Length, detailCandidates.Length);
        Assert.All(
            detailCandidates,
            candidate => Assert.Contains(
                candidate.Signals,
                signal =>
                    signal.Kind == StructuralEvidenceSignalKind.RepeatedDetailPattern
                    && signal.Weight <= -0.45));

        var solution = JointStructuralSolver.Solve(graph);
        Assert.DoesNotContain(
            solution.WallRuns,
            run => run.SourceWallIds.Any(wallId =>
                details.Any(detail => string.Equals(
                    detail.Id,
                    wallId,
                    StringComparison.Ordinal))));
    }

    [Fact]
    public void EvidenceGraph_UsesDeclaredSurfacePatternAgainstPairedDetail()
    {
        var detail = Wall(
            "paired-grid-detail",
            new PlanLineSegment(new PlanPoint(20, 40), new PlanPoint(80, 40)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair
        };
        var pattern = new SurfacePatternCandidate(
            "surface-pattern:test-grid",
            1,
            SurfacePatternKind.DenseOrthogonalGrid,
            SurfacePatternOrientation.Orthogonal,
            new PlanRect(10, 10, 80, 80),
            SourceRegionId: null,
            LineCount: 16,
            HorizontalLineCount: 8,
            VerticalLineCount: 8,
            IntersectionCount: 64,
            HorizontalMedianSpacing: 8,
            VerticalMedianSpacing: 8,
            MedianSpacing: null,
            ExcludedFromWallDetection: true,
            ExcludedFromStructuralTopology: true,
            detail.SourcePrimitiveIds,
            Confidence.High,
            RequiresReview: false,
            Evidence: new[] { "test dense grid" });
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: new[] { detail },
                acceptedWalls: new[] { detail },
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    new[]
                    {
                        AcceptedAssessment(
                            detail,
                            WallEvidenceCategory.StrongWallBody)
                    },
                    SourceCandidateWallCount: 1),
                surfacePatterns: new[] { pattern }));

        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(detail.Id, StringComparer.Ordinal));
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.RepeatedDetailPattern
                && signal.SourceId == pattern.Id
                && signal.Weight <= -1);
    }

    [Fact]
    public void EvidenceGraph_UsesUpstreamStairDetailReviewAgainstPairedWallBody()
    {
        var detail = Wall(
            "reviewed-stair-detail",
            new PlanLineSegment(new PlanPoint(20, 40), new PlanPoint(80, 40)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            Evidence =
            [
                "wall evidence: demoted from placement-ready because short unlayered wall candidate sits inside dense local detail/stair-like linework"
            ]
        };
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: new[] { detail },
                acceptedWalls: new[] { detail },
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    new[]
                    {
                        AcceptedAssessment(
                            detail,
                            WallEvidenceCategory.StrongWallBody)
                    },
                    SourceCandidateWallCount: 1)));

        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(detail.Id, StringComparer.Ordinal));
        Assert.Contains(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.RepeatedDetailPattern
                && signal.Weight <= -1);
        Assert.Empty(JointStructuralSolver.Solve(graph).WallRuns);
    }

    [Fact]
    public void EvidenceGraph_PreservesReviewedDetailHintWhenRoomLoopConfirmsWall()
    {
        var wall = Wall(
            "room-confirmed-detail-hint",
            new PlanLineSegment(new PlanPoint(0, 80), new PlanPoint(100, 80)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            Evidence =
            [
                "wall evidence: demoted from placement-ready because short unlayered wall candidate sits inside dense local detail/stair-like linework"
            ]
        };
        var room = Room(
            "confirming-room",
            new PlanRect(0, 0, 100, 80),
            new[]
            {
                new PlanPoint(0, 0),
                new PlanPoint(100, 0),
                new PlanPoint(100, 80),
                new PlanPoint(0, 80)
            });
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: new[] { wall },
                acceptedWalls: new[] { wall },
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    new[]
                    {
                        AcceptedAssessment(
                            wall,
                            WallEvidenceCategory.StrongWallBody)
                    },
                    SourceCandidateWallCount: 1),
                rooms: new[] { room }));

        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.Contains(room.Id, candidate.SourceRoomIds);
        Assert.DoesNotContain(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.RepeatedDetailPattern);
        Assert.Contains(
            JointStructuralSolver.Solve(graph).WallRuns,
            run => run.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_PreservesReviewedDetailHintWhenFilledWallBodyConfirmsWall()
    {
        var wall = Wall(
            "filled-wall-body-detail-hint",
            new PlanLineSegment(new PlanPoint(40, 20), new PlanPoint(40, 100)),
            Confidence.High) with
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            Evidence =
            [
                "filled wall-solid primitive",
                "wall evidence: filled closed vector wall body",
                "wall evidence assessment: StrongWallBody / placement-ready / confidence 0.86",
                "wall evidence: demoted from placement-ready because short unlayered wall candidate sits inside dense local detail/stair-like linework"
            ]
        };
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: new[] { wall },
                acceptedWalls: new[] { wall },
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    new[]
                    {
                        AcceptedAssessment(
                            wall,
                            WallEvidenceCategory.MediumWallBody)
                    },
                    SourceCandidateWallCount: 1)));

        var candidate = Assert.Single(
            graph.WallCandidates,
            item => item.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        Assert.DoesNotContain(
            candidate.Signals,
            signal =>
                signal.Kind == StructuralEvidenceSignalKind.RepeatedDetailPattern);
        Assert.Contains(
            JointStructuralSolver.Solve(graph).WallRuns,
            run => run.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void EvidenceGraph_PreservesDenseRepeatedPartitionsBackedByRoomLoops()
    {
        var partitions = Enumerable.Range(0, 6)
            .Select(index => Wall(
                $"room-partition-{index}",
                new PlanLineSegment(
                    new PlanPoint(index * 8, 0),
                    new PlanPoint(index * 8, 80)),
                Confidence.High) with
            {
                DetectionKind = WallDetectionKind.ParallelLinePair
            })
            .ToArray();
        var rooms = Enumerable.Range(0, partitions.Length - 1)
            .Select(index => Room(
                $"narrow-room-{index}",
                new PlanRect(index * 8, 0, 8, 80),
                new[]
                {
                    new PlanPoint(index * 8, 0),
                    new PlanPoint((index + 1) * 8, 0),
                    new PlanPoint((index + 1) * 8, 80),
                    new PlanPoint(index * 8, 80)
                }))
            .ToArray();
        var graph = StructuralEvidenceGraphBuilder.Build(
            Source(
                wallCandidates: partitions,
                acceptedWalls: partitions,
                evidence: new WallEvidenceMap(
                    Array.Empty<WallEvidenceSegment>(),
                    Array.Empty<WallEvidenceBand>(),
                    partitions
                        .Select(wall => AcceptedAssessment(
                            wall,
                            WallEvidenceCategory.StrongWallBody))
                        .ToArray(),
                    SourceCandidateWallCount: partitions.Length),
                rooms: rooms));

        var partitionCandidates = graph.WallCandidates
            .Where(candidate => candidate.SourceWallIds.Any(wallId =>
                partitions.Any(partition => string.Equals(
                    partition.Id,
                    wallId,
                    StringComparison.Ordinal))))
            .ToArray();
        Assert.Equal(partitions.Length, partitionCandidates.Length);
        Assert.All(
            partitionCandidates,
            candidate =>
            {
                Assert.NotEmpty(candidate.SourceRoomIds);
                Assert.DoesNotContain(
                    candidate.Signals,
                    signal =>
                        signal.Kind == StructuralEvidenceSignalKind.RepeatedDetailPattern);
            });
    }

    [Fact]
    public void JointSolver_RepresentsTJunctionWithoutSplittingLongHostRun()
    {
        var horizontal = Candidate(
            "horizontal",
            new PlanLineSegment(new PlanPoint(0, 50), new PlanPoint(100, 50)),
            unaryScore: 0.75);
        var vertical = Candidate(
            "vertical",
            new PlanLineSegment(new PlanPoint(50, 0), new PlanPoint(50, 50)),
            unaryScore: 0.70);
        var relation = Relation(
            StructuralEvidenceRelationKind.Junction,
            horizontal,
            vertical,
            weight: 0.18);
        var graph = Graph(new[] { horizontal, vertical }, new[] { relation });

        var solution = JointStructuralSolver.Solve(graph);

        Assert.Equal(2, solution.WallRuns.Count);
        Assert.Contains(solution.WallRuns, run => run.DrawingLength == 100);
        var tee = Assert.Single(solution.Junctions, junction => junction.Kind == StructuralJunctionKind.Tee);
        Assert.Equal(new PlanPoint(50, 50), tee.Position);
        Assert.Equal(2, tee.IncidentWallRunIds.Count);
    }

    [Fact]
    public void JointSolver_IsDeterministic()
    {
        var room = new RoomRegion(
            "deterministic-room",
            1,
            new PlanRect(0, 0, 60, 40),
            new[]
            {
                new PlanPoint(0, 0),
                new PlanPoint(60, 0),
                new PlanPoint(60, 40),
                new PlanPoint(0, 40)
            },
            Array.Empty<string>(),
            Confidence.High);
        var graph = StructuralEvidenceGraphBuilder.Build(Source(rooms: new[] { room }));

        var first = JointStructuralSolver.Solve(graph);
        var second = JointStructuralSolver.Solve(graph);

        Assert.Equal(first.ObjectiveScore, second.ObjectiveScore);
        Assert.Equal(first.Metrics, second.Metrics);
        Assert.Equal(
            first.CandidateDecisions.Select(decision => (decision.CandidateId, decision.Decision, decision.ObjectiveContribution)),
            second.CandidateDecisions.Select(decision => (decision.CandidateId, decision.Decision, decision.ObjectiveContribution)));
        Assert.Equal(
            first.WallRuns.Select(run => (run.Id, run.CenterLine, run.Thickness, run.WallType)),
            second.WallRuns.Select(run => (run.Id, run.CenterLine, run.Thickness, run.WallType)));
        Assert.Equal(
            first.Junctions.Select(junction => (junction.Id, junction.Position, junction.Kind)),
            second.Junctions.Select(junction => (junction.Id, junction.Position, junction.Kind)));
    }

    private static StructuralEvidenceSource Source(
        IReadOnlyList<WallSegment>? wallCandidates = null,
        IReadOnlyList<WallSegment>? acceptedWalls = null,
        WallEvidenceMap? evidence = null,
        WallGraph? wallGraph = null,
        IReadOnlyList<RoomRegion>? rooms = null,
        IReadOnlyList<OpeningCandidate>? openings = null,
        IReadOnlyList<SurfacePatternCandidate>? surfacePatterns = null,
        IReadOnlyList<DimensionAnnotation>? dimensions = null,
        IReadOnlyList<SheetRegion>? sheetRegions = null) =>
        new(
            wallCandidates ?? Array.Empty<WallSegment>(),
            acceptedWalls ?? Array.Empty<WallSegment>(),
            evidence ?? WallEvidenceMap.Empty,
            wallGraph ?? WallGraph.Empty,
            rooms ?? Array.Empty<RoomRegion>(),
            openings ?? Array.Empty<OpeningCandidate>(),
            sheetRegions ?? Array.Empty<SheetRegion>(),
            DefaultWallThickness: 4)
        {
            SurfacePatterns =
                surfacePatterns ?? Array.Empty<SurfacePatternCandidate>(),
            Dimensions =
                dimensions ?? Array.Empty<DimensionAnnotation>()
        };

    private static StructuralEvidenceGraph Graph(
        IReadOnlyList<StructuralWallCandidate> candidates,
        IReadOnlyList<StructuralEvidenceRelation> relations) =>
        new(
            StructuralEvidenceGraph.CurrentContractVersion,
            candidates,
            relations,
            Array.Empty<StructuralJunctionCandidate>(),
            Array.Empty<StructuralRoomLoopCandidate>(),
            Array.Empty<StructuralOpeningConstraint>(),
            new[] { "test" },
            Array.Empty<string>());

    private static StructuralWallCandidate Candidate(
        string id,
        PlanLineSegment line,
        double unaryScore) =>
        new(
            id,
            1,
            line,
            4,
            WallType.Interior,
            Confidence.High,
            StructuralCandidateOrigin.DetectedWall | StructuralCandidateOrigin.AcceptedWall,
            IsEligible: true,
            WasAcceptedByPreliminaryPipeline: true,
            unaryScore,
            new[] { id },
            Array.Empty<string>(),
            new[] { $"primitive:{id}" },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<StructuralEvidenceSignal>(),
            Array.Empty<string>());

    private static StructuralWallCandidate ExteriorAssemblyLeaf(
        string id,
        double y,
        WallType wallType,
        bool exteriorSemantics = true) =>
        Candidate(
            id,
            new PlanLineSegment(
                new PlanPoint(0, y),
                new PlanPoint(120, y)),
            unaryScore: 0.90) with
        {
            WallType = wallType,
            SourceWallComponentIds = ["component:exterior-assembly"],
            Signals =
            [
                Signal(StructuralEvidenceSignalKind.WallBody, 0.36)
            ],
            Evidence = exteriorSemantics
                ? ["parallel wall-face pair", "wall type exterior: source-backed shell"]
                : ["parallel wall-face pair"]
        };

    private static StructuralEvidenceRelation Relation(
        StructuralEvidenceRelationKind kind,
        StructuralWallCandidate first,
        StructuralWallCandidate second,
        double weight,
        bool hard = false) =>
        new(
            $"relation:{kind}:{first.Id}:{second.Id}",
            kind,
            first.Id,
            second.Id,
            weight,
            hard,
            Array.Empty<string>());

    private static StructuralEvidenceSignal Signal(
        StructuralEvidenceSignalKind kind,
        double weight) =>
        new(
            $"signal:{kind}",
            kind,
            weight,
            "test",
            kind.ToString(),
            Array.Empty<string>());

    private static WallSegment Wall(
        string id,
        PlanLineSegment line,
        Confidence confidence) =>
        new(id, 1, line, 4, confidence)
        {
            SourcePrimitiveIds = new[] { $"primitive:{id}" }
        };

    private static WallSegment FragmentedReviewWall(
        string id,
        PlanLineSegment line) =>
        Wall(id, line, new Confidence(0.81)) with
        {
            DetectionKind = WallDetectionKind.FragmentMerged,
            WallType = WallType.Interior,
            Evidence =
            [
                "wall evidence: geometric room boundary support from reliable room-boundary alignment",
                "fragment merge retained continuous structural axis for review"
            ],
            FragmentEvidence = new WallFragmentEvidence(
                FragmentCount: 147,
                TotalHealedGap: 8.5,
                MaxHealedGap: 3.9,
                DuplicatePrimitiveCount: 1,
                GapRatio: 0.052,
                RequiresGeometryReview: true,
                Evidence: ["fragment merge geometry requires review"])
        };

    private static WallSegment PairedWall(
        string id,
        double y,
        double thickness,
        bool filled = false,
        double length = 120)
    {
        var centerLine = new PlanLineSegment(
            new PlanPoint(0, y),
            new PlanPoint(length, y));
        var evidence = new List<string>
        {
            "parallel wall-face pair",
            $"face separation {thickness:0.###} drawing units",
            "pair score 0.9",
            "overlap ratio 1"
        };
        if (filled)
        {
            evidence.Add("filled wall-solid primitive");
            evidence.Add("wall evidence: filled closed vector wall body");
        }

        return new WallSegment(
            id,
            1,
            centerLine,
            thickness,
            Confidence.High)
        {
            DetectionKind = WallDetectionKind.ParallelLinePair,
            WallType = WallType.Interior,
            SourcePrimitiveIds = [$"primitive:{id}:first", $"primitive:{id}:second"],
            Evidence = evidence,
            PairEvidence = new WallPairEvidence(
                new PlanLineSegment(
                    new PlanPoint(0, y - (thickness / 2.0)),
                    new PlanPoint(length, y - (thickness / 2.0))),
                new PlanLineSegment(
                    new PlanPoint(0, y + (thickness / 2.0)),
                    new PlanPoint(length, y + (thickness / 2.0))),
                thickness,
                OverlapRatio: 1,
                Score: 0.9,
                FirstFaceFragmentCount: 1,
                SecondFaceFragmentCount: 1,
                FirstFaceSourcePrimitiveIds: [$"primitive:{id}:first"],
                SecondFaceSourcePrimitiveIds: [$"primitive:{id}:second"])
        };
    }

    private static WallEvidenceWallAssessment AcceptedAssessment(
        WallSegment wall,
        WallEvidenceCategory category) =>
        new(
            wall.Id,
            wall.PageNumber,
            wall.Bounds,
            category,
            wall.Confidence,
            PlacementReady: true,
            RequiresReview: false,
            RejectedAsNoise: false,
            wall.SourcePrimitiveIds,
            wall.Evidence)
        {
            Decision = WallEvidenceDecision.Accept
        };

    private static WallEvidenceWallAssessment ReviewAssessment(
        WallSegment wall,
        WallEvidenceCategory category) =>
        new(
            wall.Id,
            wall.PageNumber,
            wall.Bounds,
            category,
            wall.Confidence,
            PlacementReady: false,
            RequiresReview: true,
            RejectedAsNoise: false,
            wall.SourcePrimitiveIds,
            wall.Evidence)
        {
            Decision = WallEvidenceDecision.Review
        };

    private static WallGraph WallGraphFor(
        IReadOnlyList<WallSegment> walls,
        WallGraphComponentKind componentKind,
        bool excludedFromStructuralTopology,
        string componentId = "component:test")
    {
        var nodes = new List<WallNode>();
        var edges = new List<WallEdge>();
        foreach (var wall in walls)
        {
            var fromId = $"node:{wall.Id}:from";
            var toId = $"node:{wall.Id}:to";
            nodes.Add(
                new WallNode(
                    fromId,
                    wall.PageNumber,
                    wall.CenterLine.Start,
                    WallNodeKind.Endpoint,
                    Degree: 1,
                    Array.Empty<string>(),
                    wall.Confidence,
                    Array.Empty<string>()));
            nodes.Add(
                new WallNode(
                    toId,
                    wall.PageNumber,
                    wall.CenterLine.End,
                    WallNodeKind.Endpoint,
                    Degree: 1,
                    Array.Empty<string>(),
                    wall.Confidence,
                    Array.Empty<string>()));
            edges.Add(
                new WallEdge(
                    $"edge:{wall.Id}",
                    wall.PageNumber,
                    fromId,
                    toId,
                    wall.Id,
                    wall.Confidence));
        }

        return new WallGraph(
            nodes,
            edges,
            new[]
            {
                new WallGraphComponent(
                    componentId,
                    walls[0].PageNumber,
                    componentKind,
                    walls
                        .Select(wall => wall.Bounds)
                        .Aggregate(PlanRect.Union),
                    walls.Select(wall => wall.Id).ToArray(),
                    nodes.Select(node => node.Id).ToArray(),
                    edges.Select(edge => edge.Id).ToArray(),
                    walls.SelectMany(wall => wall.SourcePrimitiveIds).ToArray(),
                    walls.Sum(wall => wall.DrawingLength),
                    Confidence.High,
                    new[] { "test wall graph component" },
                    excludedFromStructuralTopology)
            });
    }

    private static RoomRegion Room(
        string id,
        PlanRect bounds,
        IReadOnlyList<PlanPoint> boundary) =>
        new(
            id,
            1,
            bounds,
            boundary,
            Array.Empty<string>(),
            Confidence.High)
        {
            UseKind = RoomUseKind.Office
        };
}
