using OpenPlanTrace.Export;

namespace OpenPlanTrace.Tests;

public sealed class GlobalWallSolutionTests
{
    [Fact]
    public async Task Solver_UsesJointStructuralCoreWithThreeAuditableFallbackHypotheses()
    {
        var result = await CreateScanResultAsync();

        var first = PlanPlacementExport.From(result).WallSolutions;
        var second = GlobalWallSolutionBuilder.From(
            PlanPlacementExport.From(result).Pages,
            PlanPlacementExport.From(result).Walls,
            PlanPlacementExport.From(result).Rooms,
            PlanPlacementExport.From(result).Openings,
            PlanPlacementExport.From(result).WallGraph,
            result.StructuralPlanSolution);

        Assert.Equal(GlobalWallSolutionBuilder.SolverVersion, first.SolverVersion);
        Assert.Equal(4, first.Hypotheses.Count);
        Assert.Equal("joint-structural", first.SelectedProfile);
        Assert.Contains(
            first.Hypotheses,
            hypothesis => hypothesis.Id == "hypothesis:joint-structural-core" && hypothesis.Selected);
        Assert.Single(first.Hypotheses.Where(hypothesis => hypothesis.Selected));
        Assert.Equal(first.SelectedHypothesisId, first.Hypotheses.Single(hypothesis => hypothesis.Selected).Id);
        Assert.Equal(first.SelectedWallRunCount, first.SelectedWallRuns.Count);
        Assert.Equal(first.CandidateCount, first.CandidateDecisions.Count);
        Assert.Equal(GlobalWallSolutionBuilder.ReconcilerVersion, first.Reconciliation.ReconcilerVersion);
        Assert.Equal(first.SelectedWallRunCount, first.Reconciliation.EvaluatedWallRunCount);
        Assert.NotEmpty(first.SelectedWallRuns);
        Assert.All(first.Hypotheses, hypothesis =>
            Assert.InRange(hypothesis.Metrics.DuplicateLengthRatio, 0, 0.01));
        Assert.All(first.Hypotheses, hypothesis =>
            Assert.Equal(
                hypothesis.InitialCandidateCount
                - hypothesis.RemovedCandidateCount
                + hypothesis.RecoveredCandidateCount,
                hypothesis.SelectedCandidateCount));
        Assert.Equal(first.SelectedHypothesisId, second.SelectedHypothesisId);
        Assert.Equal(first.SelectedProfile, second.SelectedProfile);
        Assert.Equal(first.SelectedScore, second.SelectedScore);
        Assert.Equal(
            first.SelectedWallRuns.Select(run => run.CenterLine),
            second.SelectedWallRuns.Select(run => run.CenterLine));
    }

    [Fact]
    public async Task Solver_AllowsGuardedLegacyOverrideOfIncompleteStructuralCore()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var trustedWalls = placement.Walls
            .Select(wall => wall with
            {
                Reliability = wall.Reliability with
                {
                    ReadyForCoordinatePlacement = true,
                    RequiresReview = false,
                    CoordinatePlacementBlocked = false,
                    Reasons = Array.Empty<string>()
                }
            })
            .ToArray();
        var incompleteStructural = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            trustedWalls
                .Take(1)
                .ToArray());

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            trustedWalls,
            placement.Rooms,
            placement.Openings,
            placement.WallGraph,
            incompleteStructural);

        var structural = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Id == "hypothesis:joint-structural-core"));
        var selected = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Selected));

        Assert.True(
            !string.Equals(structural.Id, selected.Id, StringComparison.Ordinal),
            string.Join(
                Environment.NewLine,
                solutions.Hypotheses.Select(hypothesis =>
                    $"{hypothesis.Profile}: score={hypothesis.Score:0.000000}, major={hypothesis.Metrics.MajorWallCoverageRatio:0.000000}, long={hypothesis.Metrics.LongWallCoverageRatio:0.000000}, room={hypothesis.Metrics.RoomBoundaryClosureRatio:0.000000}, noise={hypothesis.Metrics.NoiseLengthRatio:0.000000}")));
        Assert.True(
            selected.Score >= structural.Score + 0.006
            || selected.Metrics.MajorWallCoverageRatio
                >= structural.Metrics.MajorWallCoverageRatio + 0.010);
        Assert.Contains(
            solutions.Evidence,
            evidence => evidence.Contains(
                "guarded arbitration selected",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Solver_AllowsDecisiveRecallRescueWithModerateReviewDebt()
    {
        var structural = HypothesisMetrics(
            major: 0.84,
            longCoverage: 0.76,
            endpoint: 0.59,
            roomClosure: 1.0,
            duplicate: 0,
            review: 0,
            noise: 0,
            selectedLength: 4_670);
        var alternative = HypothesisMetrics(
            major: 0.985,
            longCoverage: 0.996,
            endpoint: 0.76,
            roomClosure: 0.75,
            duplicate: 0,
            review: 0.13,
            noise: 0,
            selectedLength: 5_610);

        Assert.True(GlobalWallSolutionBuilder.IsDecisiveRecallRescue(
            alternativeScore: 0.879,
            alternative,
            structuralScore: 0.793,
            structural));
        Assert.False(GlobalWallSolutionBuilder.IsDecisiveRecallRescue(
            alternativeScore: 0.879,
            alternative with { ReviewLengthRatio = 0.18 },
            structuralScore: 0.793,
            structural));
        Assert.False(GlobalWallSolutionBuilder.IsDecisiveRecallRescue(
            alternativeScore: 0.879,
            alternative with { LongWallCoverageRatio = 0.82 },
            structuralScore: 0.793,
            structural));
    }

    [Fact]
    public async Task StructuralCore_WithholdsNoisyReviewDetailLoopBeforeArbitration()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var trustedWalls = placement.Walls
            .Select(wall => wall with
            {
                Reliability = wall.Reliability with
                {
                    ReadyForCoordinatePlacement = true,
                    RequiresReview = false,
                    CoordinatePlacementBlocked = false,
                    Reasons = Array.Empty<string>()
                }
            })
            .ToArray();
        var template = trustedWalls[0];
        var detailLoop = new[]
        {
            DetailLoopWall(template, "detail-loop-top", 200, 200, 207, 200),
            DetailLoopWall(template, "detail-loop-right", 207, 200, 207, 207),
            DetailLoopWall(template, "detail-loop-bottom", 207, 207, 200, 207),
            DetailLoopWall(template, "detail-loop-left", 200, 207, 200, 200)
        };
        var allWalls = trustedWalls.Concat(detailLoop).ToArray();
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            allWalls);
        structuralSolution = structuralSolution with
        {
            WallRuns = structuralSolution.WallRuns
                .Select(run => run.SourceWallIds.Any(id =>
                    id.StartsWith("detail-loop-", StringComparison.Ordinal))
                    ? run with
                    {
                        Evidence =
                        [
                            "reclassified as object/fixture detail",
                            "test structural proposal retained the closed detail loop"
                        ],
                        Reliability = new StructuralWallRunReliability(
                            ReadyForCoordinatePlacement: false,
                            RequiresReview: true,
                            Confidence: 0.95,
                            Reasons: ["closed detail loop requires review"])
                    }
                    : run)
                .ToArray()
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            allWalls,
            placement.Rooms,
            placement.Openings,
            placement.WallGraph,
            structuralSolution);

        var structural = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Id == "hypothesis:joint-structural-core"));
        var selected = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Selected));

        Assert.Equal(structural.Id, selected.Id);
        Assert.Equal(0, selected.Metrics.NoiseLengthRatio);
        Assert.Equal(0, selected.Metrics.ReviewLengthRatio);
        Assert.True(structural.RemovedCandidateCount >= detailLoop.Length);
        Assert.DoesNotContain(
            solutions.SelectedWallRuns,
            run => run.SourceWallIds.Any(id =>
                id.StartsWith("detail-loop-", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Solver_KeepsLegacyHypothesesIndependentOfStructuralPromotions()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var trustedWalls = placement.Walls
            .Select(wall => wall with
            {
                Reliability = wall.Reliability with
                {
                    ReadyForCoordinatePlacement = true,
                    RequiresReview = false,
                    CoordinatePlacementBlocked = false,
                    Reasons = Array.Empty<string>()
                }
            })
            .ToArray();
        var reviewOnly = HostWallFragment(
            trustedWalls[0],
            "structural-promotion-review-only",
            new LineExport(
                new PointExport(200, 210),
                new PointExport(264, 210))) with
        {
            Confidence = 0.65,
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: false,
                ReadyForMetricPlacement: false,
                RequiresReview: true,
                Confidence: 0.65,
                Reasons: ["opening-clearance geometry requires review"]),
            SourceLayers = ["(unlayered)"],
            Evidence =
            [
                "single wall-length vector run",
                "unfilled exterior opening-clearance rectangle retained as review geometry"
            ]
        };
        var allWalls = trustedWalls.Append(reviewOnly).ToArray();
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            allWalls);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            allWalls,
            placement.Rooms,
            placement.Openings,
            placement.WallGraph,
            structuralSolution);

        var sourceCandidateId = $"candidate:wall:{reviewOnly.Id}";
        var structural = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Id == "hypothesis:joint-structural-core"));
        var legacy = solutions.Hypotheses
            .Where(hypothesis => hypothesis.Id != structural.Id)
            .ToArray();

        Assert.All(
            legacy,
            hypothesis => Assert.DoesNotContain(
                sourceCandidateId,
                hypothesis.SelectedCandidateIds));
        Assert.Contains(
            structural.SelectedCandidateIds,
            candidateId => solutions.CandidateDecisions.Any(decision =>
                decision.CandidateId == candidateId
                && decision.SourceWallIds.Contains(reviewOnly.Id, StringComparer.Ordinal)));
        Assert.NotEqual(structural.Id, solutions.SelectedHypothesisId);
        Assert.DoesNotContain(
            solutions.SelectedWallRuns,
            run => run.SourceWallIds.Contains(reviewOnly.Id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task StructuralCore_PreservesBlockedCoordinateReadiness()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var blockedStructure = result.StructuralPlanSolution with
        {
            WallRuns = result.StructuralPlanSolution.WallRuns
                .Select(run => run with
                {
                    Reliability = new StructuralWallRunReliability(
                        ReadyForCoordinatePlacement: false,
                        RequiresReview: true,
                        Confidence: 0.45,
                        Reasons: new[] { "structural evidence requires review" })
                })
                .ToArray()
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            placement.Walls,
            placement.Rooms,
            placement.Openings,
            placement.WallGraph,
            blockedStructure);

        Assert.NotEmpty(solutions.SelectedWallRuns);
        Assert.All(solutions.SelectedWallRuns, run =>
        {
            Assert.False(run.Reliability.ReadyForCoordinatePlacement);
            Assert.True(run.Reliability.RequiresReview);
        });
    }

    [Fact]
    public async Task StructuralCore_WithholdsReviewOnlyRunsWhenReadyRunsExist()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var reviewTarget = result.StructuralPlanSolution.WallRuns.First();
        var mixedStructure = result.StructuralPlanSolution with
        {
            WallRuns = result.StructuralPlanSolution.WallRuns
                .Select(run => run.Id == reviewTarget.Id
                    ? run with
                    {
                        Reliability = new StructuralWallRunReliability(
                            ReadyForCoordinatePlacement: false,
                            RequiresReview: true,
                            Confidence: 0.55,
                            Reasons: ["test structural proposal requires review"])
                    }
                    : run)
                .ToArray()
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            placement.Walls,
            placement.Rooms,
            placement.Openings,
            placement.WallGraph,
            mixedStructure);

        var structural = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Id == "hypothesis:joint-structural-core"));
        var reviewCandidateId = $"candidate:structural:{reviewTarget.Id}";

        Assert.DoesNotContain(reviewCandidateId, structural.SelectedCandidateIds);
        Assert.True(structural.RemovedCandidateCount >= 1);
        var reviewDecision = Assert.Single(solutions.CandidateDecisions.Where(decision =>
            decision.CandidateId == reviewCandidateId));
        Assert.DoesNotContain(
            structural.Id,
            reviewDecision.SelectedByHypothesisIds);
    }

    [Fact]
    public async Task StructuralCore_DoesNotAllowLegacyReadinessToPromoteBlockedRuns()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var legacyReadyWalls = placement.Walls
            .Select(wall => wall with
            {
                Reliability = wall.Reliability with
                {
                    ReadyForCoordinatePlacement = true,
                    ReadyForMetricPlacement = true,
                    RequiresReview = false,
                    CoordinatePlacementBlocked = false,
                    Reasons = new[] { "legacy detector marked wall ready" }
                }
            })
            .ToArray();
        var blockedStructure = result.StructuralPlanSolution with
        {
            WallRuns = result.StructuralPlanSolution.WallRuns
                .Select(run => run with
                {
                    Reliability = new StructuralWallRunReliability(
                        ReadyForCoordinatePlacement: false,
                        RequiresReview: true,
                        Confidence: 0.45,
                        Reasons: new[] { "structural evidence requires review" })
                })
                .ToArray()
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            legacyReadyWalls,
            placement.Rooms,
            placement.Openings,
            placement.WallGraph,
            blockedStructure);

        Assert.NotEmpty(solutions.SelectedWallRuns);
        Assert.All(solutions.SelectedWallRuns, run =>
        {
            Assert.False(run.Reliability.ReadyForCoordinatePlacement);
            Assert.True(run.Reliability.RequiresReview);
        });
    }

    [Theory]
    [InlineData(StructuralEvidenceSignalKind.RepeatedDetailPattern)]
    [InlineData(StructuralEvidenceSignalKind.ContextOnlyBoundary)]
    [InlineData(StructuralEvidenceSignalKind.UnsupportedOblique)]
    [InlineData(StructuralEvidenceSignalKind.IsolatedStructuralIsland)]
    [InlineData(StructuralEvidenceSignalKind.UnoccupiedShellExtension)]
    [InlineData(StructuralEvidenceSignalKind.WallBodyThicknessOutlier)]
    public async Task StructuralCore_RetainedReviewSignalDoesNotBecomeHardRejection(
        StructuralEvidenceSignalKind blockingKind)
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var target = placement.Walls.First();
        var structuralSolution = result.StructuralPlanSolution with
        {
            CandidateDecisions =
            [
                new StructuralWallDecision(
                    $"structural:wall:{target.Id}",
                    StructuralWallDecisionKind.RetainedForReview,
                    UnaryScore: 0.2,
                    ObjectiveContribution: 0,
                    Reasons:
                    [
                        "contains strong negative structural evidence",
                        "rejected as a repeated detail family"
                    ])
                {
                    SourceWallIds = [target.Id],
                    BlockingSignalKinds =
                    [
                        blockingKind
                    ]
                }
            ],
            WallRuns = Array.Empty<StructuralWallRun>(),
            Metrics = result.StructuralPlanSolution.Metrics with
            {
                SelectedCandidateCount = 0,
                CanonicalWallRunCount = 0
            }
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [target],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        var targetDecisions = solutions.CandidateDecisions
            .Where(decision => decision.SourceWallIds.Contains(
                target.Id,
                StringComparer.Ordinal))
            .ToArray();
        Assert.NotEmpty(targetDecisions);
        Assert.DoesNotContain(
            targetDecisions,
            decision => decision.Evidence.Any(item => item.Contains(
                "joint structural evidence rejected every contributing source wall",
                StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(
            targetDecisions,
            decision => decision.Decision != "Rejected"
                || decision.SelectedByHypothesisIds.Count > 0);
    }

    [Fact]
    public async Task StructuralCore_DoesNotLetAuditSelectionOverrideAbsoluteBlockingEvidence()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var target = placement.Walls.First();
        var structuralSolution = result.StructuralPlanSolution with
        {
            CandidateDecisions =
            [
                new StructuralWallDecision(
                    $"structural:wall:{target.Id}",
                    StructuralWallDecisionKind.Selected,
                    UnaryScore: 0.7,
                    ObjectiveContribution: 0.2,
                    Reasons:
                    [
                        "selected for structural audit continuity",
                        "unsupported oblique single-line geometry remains an absolute placement blocker"
                    ])
                {
                    SourceWallIds = [target.Id],
                    AbsolutePlacementBlock = true,
                    BlockingSignalKinds =
                    [
                        StructuralEvidenceSignalKind.UnsupportedOblique
                    ]
                }
            ],
            WallRuns = Array.Empty<StructuralWallRun>(),
            Metrics = result.StructuralPlanSolution.Metrics with
            {
                SelectedCandidateCount = 1,
                CanonicalWallRunCount = 0
            }
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [target],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        var targetDecisions = solutions.CandidateDecisions
            .Where(decision => decision.SourceWallIds.Contains(
                target.Id,
                StringComparer.Ordinal))
            .ToArray();
        Assert.NotEmpty(targetDecisions);
        Assert.All(
            targetDecisions,
            decision =>
            {
                Assert.True(decision.StrongNegativeEvidence);
                Assert.Equal("Rejected", decision.Decision);
                Assert.Empty(decision.SelectedByHypothesisIds);
            });
    }

    [Fact]
    public async Task StructuralCore_SelectedCandidateOutweighsNonAbsoluteBlockingSignal()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var target = placement.Walls.First();
        var structuralSolution = result.StructuralPlanSolution with
        {
            CandidateDecisions =
            [
                new StructuralWallDecision(
                    $"structural:wall:{target.Id}",
                    StructuralWallDecisionKind.Selected,
                    UnaryScore: 0.7,
                    ObjectiveContribution: 0.2,
                    Reasons:
                    [
                        "selected by the joint structural objective",
                        "context-only evidence remains as a review signal"
                    ])
                {
                    SourceWallIds = [target.Id],
                    BlockingSignalKinds =
                    [
                        StructuralEvidenceSignalKind.ContextOnlyBoundary
                    ]
                }
            ],
            WallRuns = Array.Empty<StructuralWallRun>(),
            Metrics = result.StructuralPlanSolution.Metrics with
            {
                SelectedCandidateCount = 1,
                CanonicalWallRunCount = 0
            }
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [target],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        var targetDecisions = solutions.CandidateDecisions
            .Where(decision => decision.SourceWallIds.Contains(
                target.Id,
                StringComparer.Ordinal))
            .ToArray();
        Assert.NotEmpty(targetDecisions);
        Assert.DoesNotContain(
            targetDecisions,
            decision => decision.Evidence.Any(item => item.Contains(
                "joint structural evidence rejected every contributing source wall",
                StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(targetDecisions, decision => decision.Decision == "Selected");
    }

    [Fact]
    public async Task StructuralCore_CoordinateReadyRunOutweighsLocalBlockingSignal()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var sourceRun = result.StructuralPlanSolution.WallRuns.First(run =>
            run.SourceWallIds.Count > 0
            && run.SourceWallIds.Any(id =>
                placement.Walls.Any(wall =>
                    string.Equals(wall.Id, id, StringComparison.Ordinal))));
        var target = placement.Walls.First(wall =>
            sourceRun.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        var structuralSolution = result.StructuralPlanSolution with
        {
            CandidateDecisions =
            [
                new StructuralWallDecision(
                    $"structural:wall:{target.Id}",
                    StructuralWallDecisionKind.Selected,
                    UnaryScore: 0.8,
                    ObjectiveContribution: 0.3,
                    Reasons:
                    [
                        "selected with independent filled wall-body evidence",
                        "overlapping dimension evidence remains as local provenance"
                    ])
                {
                    SourceWallIds = [target.Id],
                    BlockingSignalKinds =
                    [
                        StructuralEvidenceSignalKind.DimensionOrAnnotation
                    ]
                }
            ],
            WallRuns =
            [
                sourceRun with
                {
                    SourceWallIds = [target.Id],
                    Reliability = new StructuralWallRunReliability(
                        ReadyForCoordinatePlacement: true,
                        RequiresReview: false,
                        Confidence: 0.9,
                        Reasons:
                        [
                            "independent strong wall-body evidence supports coordinate placement"
                        ])
                }
            ],
            Metrics = result.StructuralPlanSolution.Metrics with
            {
                SelectedCandidateCount = 1,
                CanonicalWallRunCount = 1
            }
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [target],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        var targetDecisions = solutions.CandidateDecisions
            .Where(decision => decision.SourceWallIds.Contains(
                target.Id,
                StringComparer.Ordinal))
            .ToArray();
        Assert.NotEmpty(targetDecisions);
        Assert.DoesNotContain(
            targetDecisions,
            decision => decision.Evidence.Any(item => item.Contains(
                "joint structural evidence rejected every contributing source wall",
                StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(targetDecisions, decision => decision.Decision == "Selected");
    }

    [Fact]
    public async Task StructuralCore_CoordinateReadyRunDoesNotOverrideAbsoluteCandidateBlock()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var sourceRun = result.StructuralPlanSolution.WallRuns.First(run =>
            run.SourceWallIds.Count > 0
            && run.SourceWallIds.Any(id =>
                placement.Walls.Any(wall =>
                    string.Equals(wall.Id, id, StringComparison.Ordinal))));
        var target = placement.Walls.First(wall =>
            sourceRun.SourceWallIds.Contains(wall.Id, StringComparer.Ordinal));
        var structuralSolution = result.StructuralPlanSolution with
        {
            CandidateDecisions =
            [
                new StructuralWallDecision(
                    $"structural:wall:{target.Id}",
                    StructuralWallDecisionKind.RetainedForReview,
                    UnaryScore: -0.4,
                    ObjectiveContribution: 0,
                    Reasons:
                    [
                        "one-sided outdoor context is an absolute placement block"
                    ])
                {
                    SourceWallIds = [target.Id],
                    AbsolutePlacementBlock = true,
                    BlockingSignalKinds =
                    [
                        StructuralEvidenceSignalKind.ContextOnlyBoundary
                    ]
                }
            ],
            WallRuns =
            [
                sourceRun with
                {
                    SourceWallIds = [target.Id],
                    Reliability = new StructuralWallRunReliability(
                        ReadyForCoordinatePlacement: true,
                        RequiresReview: false,
                        Confidence: 0.9,
                        Reasons:
                        [
                            "other contributors support the merged structural run"
                        ])
                }
            ]
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [target],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        var targetDecisions = solutions.CandidateDecisions
            .Where(decision => decision.SourceWallIds.Contains(
                target.Id,
                StringComparer.Ordinal))
            .ToArray();
        Assert.NotEmpty(targetDecisions);
        Assert.All(targetDecisions, decision =>
        {
            Assert.True(decision.StrongNegativeEvidence);
            Assert.Equal("Rejected", decision.Decision);
            Assert.Empty(decision.SelectedByHypothesisIds);
        });
    }

    [Fact]
    public async Task StructuralCore_DoesNotAllowLegacySourceBlockToHideReadyCanonicalRun()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var readyStructuralRun = result.StructuralPlanSolution.WallRuns
            .First(run =>
                run.Reliability.ReadyForCoordinatePlacement
                && !run.Reliability.RequiresReview
                && run.SourceWallIds.Count > 0);
        var structuralSourceWallId = readyStructuralRun.SourceWallIds[0];
        var mixedWalls = placement.Walls
            .Select(wall => string.Equals(wall.Id, structuralSourceWallId, StringComparison.Ordinal)
                ? wall with
                {
                    Reliability = wall.Reliability with
                    {
                        ReadyForCoordinatePlacement = false,
                        ReadyForMetricPlacement = false,
                        RequiresReview = true,
                        CoordinatePlacementBlocked = true,
                        Reasons = wall.Reliability.Reasons
                            .Append("test source wall requires structural review")
                            .ToArray()
                    }
                }
                : wall)
            .ToArray();

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            mixedWalls,
            placement.Rooms,
            placement.Openings,
            placement.WallGraph,
            result.StructuralPlanSolution);

        var affectedRuns = solutions.SelectedWallRuns
            .Where(run => run.SourceWallIds.Contains(structuralSourceWallId, StringComparer.Ordinal))
            .ToArray();
        Assert.NotEmpty(affectedRuns);
        Assert.All(affectedRuns, run =>
        {
            Assert.True(run.Reliability.ReadyForCoordinatePlacement);
            Assert.False(run.Reliability.RequiresReview);
        });
    }

    [Fact]
    public async Task StructuralCore_PreservesDistinctParallelRunsWithSharedSourceProvenance()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var source = HostWallFragment(
            template,
            "shared-structural-source",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(400, 100))) with
        {
            ThicknessDrawingUnits = 20
        };
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [source]);
        var runTemplate = structuralSolution.WallRuns[0];
        var first = runTemplate with
        {
            Id = "test-structural-shared-axis-1",
            CenterLine = new PlanLineSegment(
                new PlanPoint(100, 100),
                new PlanPoint(400, 100)),
            CandidateIds = ["test-structural-shared-candidate-1"]
        };
        var second = runTemplate with
        {
            Id = "test-structural-shared-axis-2",
            CenterLine = new PlanLineSegment(
                new PlanPoint(100, 108.5),
                new PlanPoint(400, 108.5)),
            CandidateIds = ["test-structural-shared-candidate-2"]
        };
        structuralSolution = structuralSolution with
        {
            WallRuns = [first, second],
            Metrics = structuralSolution.Metrics with
            {
                SelectedCandidateCount = 2,
                CanonicalWallRunCount = 2
            }
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [source],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        Assert.Equal(2, solutions.SelectedWallRuns.Count);
        var axes = solutions.SelectedWallRuns
            .Select(run => run.CenterLine.Start.Y)
            .Order()
            .ToArray();
        Assert.Equal(100, axes[0], 6);
        Assert.Equal(108.5, axes[1], 6);
        Assert.All(
            solutions.SelectedWallRuns,
            run => Assert.Contains("StructuralCore", run.CandidateOrigins));
    }

    [Fact]
    public async Task StructuralCore_PreservesResolvedPhysicalAssemblyAxisAgainstSourceFaceVotes()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var firstFace = HostWallFragment(
            template,
            "assembly-face-first",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(400, 100)));
        var secondFace = HostWallFragment(
            template,
            "assembly-face-second",
            new LineExport(
                new PointExport(100, 110),
                new PointExport(400, 110)));
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [firstFace]);
        var runTemplate = structuralSolution.WallRuns[0];
        var assembly = runTemplate with
        {
            Id = "test-structural-physical-assembly",
            CenterLine = new PlanLineSegment(
                new PlanPoint(100, 105),
                new PlanPoint(400, 105)),
            Thickness = 14,
            CandidateIds =
            [
                "test-structural-assembly-candidate-first",
                "test-structural-assembly-candidate-second"
            ],
            SourceWallIds = [firstFace.Id, secondFace.Id],
            Evidence =
            [
                "resolved shared-source physical wall assembly from 2 structural leaves",
                "assembly leaves share source primitives from the same physical wall body"
            ],
            AssemblyLeafCount = 2
        };
        structuralSolution = structuralSolution with
        {
            WallRuns = [assembly],
            Metrics = structuralSolution.Metrics with
            {
                SelectedCandidateCount = 2,
                CanonicalWallRunCount = 1
            }
        };
        var opening = ShiftOpeningToAxis(
            AnchoredOpening(
                "assembly-face-opening",
                secondFace.Id,
                secondFace.Id),
            axis: 110);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [firstFace, secondFace],
            Array.Empty<PlacementRoomExport>(),
            [opening],
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        var run = Assert.Single(solutions.SelectedWallRuns);
        Assert.Equal(105, run.CenterLine.Start.Y, 6);
        Assert.Equal(105, run.CenterLine.End.Y, 6);
        Assert.Equal(0, run.Reconciliation.AxisShiftDrawingUnits, 6);
        Assert.DoesNotContain("AxisAligned", run.Reconciliation.Actions);
        Assert.Contains(
            run.Reconciliation.Evidence,
            item => item.Contains(
                "resolved physical wall assembly axis retained",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task StructuralCore_RecoversUnanimousIndoorBoundaryWallAndPrefersSourceGeometry()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var target = HostWallFragment(
            template,
            "consensus-target",
            new LineExport(
                new PointExport(100, 200),
                new PointExport(400, 200)));
        var supports = new[]
        {
            HostWallFragment(
                template,
                "consensus-support-outer-left",
                new LineExport(new PointExport(60, 80), new PointExport(60, 320))),
            HostWallFragment(
                template,
                "consensus-support-left",
                new LineExport(new PointExport(100, 80), new PointExport(100, 320))),
            HostWallFragment(
                template,
                "consensus-support-right",
                new LineExport(new PointExport(400, 80), new PointExport(400, 320))),
            HostWallFragment(
                template,
                "consensus-support-outer-right",
                new LineExport(new PointExport(440, 80), new PointExport(440, 320)))
        };
        var bridgedLine = new LineExport(
            new PointExport(60, 200),
            new PointExport(440, 200));
        var bridgedEdge = new PlacementWallGraphEdgeExport(
            Id: "consensus-target-bridged",
            PageNumber: 1,
            FromNodeId: "consensus-target-bridged-from",
            ToNodeId: "consensus-target-bridged-to",
            WallId: target.Id,
            WallComponentId: null,
            WallComponentKind: null,
            ExcludedFromStructuralTopology: false,
            CenterLine: bridgedLine,
            CenterLineMillimeters: null,
            Bounds: new RectExport(55, 195, 390, 10),
            BoundsMillimeters: null,
            DrawingLength: 380,
            LengthMeters: null,
            ThicknessDrawingUnits: 10,
            ThicknessMillimeters: null,
            MillimetersPerDrawingUnit: null,
            Confidence: 0.98,
            SourcePrimitiveIds: target.SourcePrimitiveIds,
            SourceLayers: target.SourceLayers,
            SourceWallIds: [target.Id],
            SourceWallGraphEdgeIds: ["consensus-target-bridged"],
            Evidence: target.Evidence);
        var graph = EmptyGraph(placement.WallGraph) with
        {
            Edges = [bridgedEdge]
        };
        var rooms = new[]
        {
            RoomBoundaryWithHorizontalSpan(
                placement.Rooms.First(),
                "consensus-room-above",
                [target.Id],
                startX: 60,
                endX: 440,
                axis: 200) with
            {
                UseKind = "Living"
            },
            RoomBoundaryWithHorizontalSpan(
                placement.Rooms.First(),
                "consensus-room-below",
                [target.Id],
                startX: 60,
                endX: 440,
                axis: 200) with
            {
                UseKind = "Bedroom"
            }
        };
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            supports);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            supports.Append(target).ToArray(),
            rooms,
            Array.Empty<PlacementOpeningExport>(),
            graph,
            structuralSolution);

        var selectedHypothesis = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Selected));
        Assert.Equal(1, selectedHypothesis.RecoveredCandidateCount);
        var recovered = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            run.SourceWallIds.Contains(target.Id, StringComparer.Ordinal)));
        Assert.Equal(300, recovered.DrawingLength, 6);
        Assert.Equal(100, Math.Min(recovered.CenterLine.Start.X, recovered.CenterLine.End.X), 6);
        Assert.Equal(400, Math.Max(recovered.CenterLine.Start.X, recovered.CenterLine.End.X), 6);

        var sourceDecision = Assert.Single(solutions.CandidateDecisions.Where(decision =>
            decision.CandidateId == $"candidate:wall:{target.Id}"));
        Assert.Equal("Selected", sourceDecision.Decision);
        Assert.Contains(
            "hypothesis:joint-structural-core",
            sourceDecision.SelectedByHypothesisIds);
        Assert.Contains(
            sourceDecision.Evidence,
            evidence => evidence.Contains(
                "selected by hypothesis:joint-structural-core",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            solutions.CandidateDecisions,
            decision => decision.CandidateId == $"candidate:graph:{bridgedEdge.Id}");
    }

    [Fact]
    public async Task StructuralCore_RecoversUnanimousSourceBackedGraphWallWithOpeningSupport()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var target = HostWallFragment(
            template,
            "source-backed-consensus-target",
            new LineExport(
                new PointExport(100, 200),
                new PointExport(400, 200)));
        var structural = HostWallFragment(
            template,
            "source-backed-consensus-structural",
            new LineExport(
                new PointExport(600, 100),
                new PointExport(600, 400)));
        var edge = CleanGraphEdge(target, "source-backed-consensus-edge");
        var opening = AnchoredOpening(
            "source-backed-consensus-opening",
            target.Id,
            structural.Id);
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [structural]);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [structural, target],
            Array.Empty<PlacementRoomExport>(),
            [opening],
            EmptyGraph(placement.WallGraph) with
            {
                Edges = [edge]
            },
            structuralSolution);

        var selectedHypothesis = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Selected));
        Assert.Equal("hypothesis:joint-structural-core", selectedHypothesis.Id);
        Assert.Equal(1, selectedHypothesis.RecoveredCandidateCount);
        Assert.Contains(
            solutions.SelectedWallRuns,
            run => run.SourceWallIds.Contains(target.Id, StringComparer.Ordinal));
        var decision = Assert.Single(solutions.CandidateDecisions.Where(candidate =>
            candidate.CandidateId == $"candidate:graph:{edge.Id}"));
        Assert.Equal("Selected", decision.Decision);
        Assert.Equal(3, decision.SelectedByHypothesisIds.Count(id =>
            !string.Equals(id, selectedHypothesis.Id, StringComparison.Ordinal)));
        Assert.Contains(selectedHypothesis.Id, decision.SelectedByHypothesisIds);
    }

    [Fact]
    public async Task StructuralCore_RecoversUnanimousMajorWallWithTwoAnchoredOpenings()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var target = HostWallFragment(
            template,
            "two-opening-consensus-target",
            new LineExport(
                new PointExport(100, 200),
                new PointExport(400, 200))) with
        {
            Evidence =
            [
                "parallel wall-face pair",
                "dimension-like source context"
            ]
        };
        var structural = HostWallFragment(
            template,
            "two-opening-consensus-structural",
            new LineExport(
                new PointExport(600, 100),
                new PointExport(600, 400)));
        var endpointSupports = new[]
        {
            HostWallFragment(
                template,
                "two-opening-consensus-left-support",
                new LineExport(
                    new PointExport(100, 80),
                    new PointExport(100, 320))),
            HostWallFragment(
                template,
                "two-opening-consensus-right-support",
                new LineExport(
                    new PointExport(400, 80),
                    new PointExport(400, 320)))
        };
        var edge = CleanGraphEdge(target, "two-opening-consensus-edge");
        var openings = new[]
        {
            AnchoredOpening(
                "two-opening-consensus-first",
                target.Id,
                structural.Id),
            AnchoredOpening(
                "two-opening-consensus-second",
                target.Id,
                structural.Id)
        };
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [structural]);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            endpointSupports
                .Append(structural)
                .Append(target)
                .ToArray(),
            Array.Empty<PlacementRoomExport>(),
            openings,
            EmptyGraph(placement.WallGraph) with
            {
                Edges = [edge]
            },
            structuralSolution);

        var selectedHypothesis = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Selected));
        Assert.Equal("hypothesis:joint-structural-core", selectedHypothesis.Id);
        var decision = Assert.Single(solutions.CandidateDecisions.Where(candidate =>
            candidate.CandidateId == $"candidate:graph:{edge.Id}"));
        Assert.Equal(2, decision.OpeningSupportCount);
        Assert.True(decision.MajorWallCandidate);
        Assert.True(decision.LocalScore >= 0.90);
        Assert.Equal(2, decision.SupportedEndpointCount);
        Assert.Equal(3, decision.SelectedByHypothesisIds.Count(id =>
            !string.Equals(id, selectedHypothesis.Id, StringComparison.Ordinal)));
        Assert.Contains("weak non-wall evidence 1", decision.Evidence);
        Assert.Contains("structural evidence 1", decision.Evidence);
        Assert.Equal(1, selectedHypothesis.RecoveredCandidateCount);
        Assert.Equal("Selected", decision.Decision);
        Assert.Contains(selectedHypothesis.Id, decision.SelectedByHypothesisIds);
        Assert.Contains(
            solutions.SelectedWallRuns,
            run => run.SourceWallIds.Contains(target.Id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task StructuralCore_RecoversReviewedMainStructuralBridgeBetweenSelectedRuns()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var target = ReviewedMainStructuralBridge(
            template,
            "reviewed-two-ended-bridge",
            new LineExport(
                new PointExport(100, 200),
                new PointExport(400, 200)));
        var supports = new[]
        {
            HostWallFragment(
                template,
                "reviewed-two-ended-left",
                new LineExport(new PointExport(100, 80), new PointExport(100, 320))),
            HostWallFragment(
                template,
                "reviewed-two-ended-right",
                new LineExport(new PointExport(400, 80), new PointExport(400, 320)))
        };
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            supports);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            supports.Append(target).ToArray(),
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        var selected = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Selected));
        Assert.Equal("hypothesis:joint-structural-core", selected.Id);
        Assert.Equal(1, selected.RecoveredCandidateCount);
        var recoveredRun = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            run.SourceWallIds.Contains(target.Id, StringComparer.Ordinal)));
        Assert.True(recoveredRun.Reliability.ReadyForCoordinatePlacement);
        Assert.False(recoveredRun.Reliability.RequiresReview);
        Assert.Contains(
            recoveredRun.Reliability.Reasons,
            reason => reason.Contains(
                "global topology promoted",
                StringComparison.Ordinal));
        var decision = Assert.Single(solutions.CandidateDecisions.Where(candidate =>
            candidate.CandidateId == $"candidate:wall:{target.Id}"));
        Assert.Equal("Selected", decision.Decision);
        Assert.Contains(selected.Id, decision.SelectedByHypothesisIds);
    }

    [Fact]
    public async Task StructuralCore_RecoversReviewedOpeningLinkedBridgeWithOneSelectedEndpoint()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var target = ReviewedMainStructuralBridge(
            template,
            "reviewed-one-ended-opening-bridge",
            new LineExport(
                new PointExport(100, 200),
                new PointExport(400, 200))) with
        {
            Evidence =
            [
                "merged collinear wall fragments",
                "layer evidence: contains dimension-like text",
                "wall evidence: geometric room boundary support from reliable room-boundary alignment"
            ]
        };
        var support = HostWallFragment(
            template,
            "reviewed-one-ended-support",
            new LineExport(new PointExport(100, 80), new PointExport(100, 320)));
        var opening = AnchoredOpening(
            "reviewed-one-ended-opening",
            target.Id,
            support.Id);
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [support]);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [support, target],
            Array.Empty<PlacementRoomExport>(),
            [opening],
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        var selected = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Selected));
        Assert.Equal("hypothesis:joint-structural-core", selected.Id);
        Assert.Equal(1, selected.RecoveredCandidateCount);
        var recoveredRun = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            run.SourceWallIds.Contains(target.Id, StringComparer.Ordinal)));
        Assert.True(recoveredRun.Reliability.ReadyForCoordinatePlacement);
        Assert.False(recoveredRun.Reliability.RequiresReview);
    }

    [Fact]
    public void StructuralCore_DoesNotClearReviewForUntrustedMergedContributor()
    {
        var trusted = new HashSet<string>(StringComparer.Ordinal)
        {
            "candidate:wall:trusted-bridge"
        };

        Assert.True(GlobalWallSolutionBuilder.CanPromoteRecoveredRun(
            ["candidate:wall:trusted-bridge"],
            ["candidate:wall:trusted-bridge"],
            trusted));
        Assert.False(GlobalWallSolutionBuilder.CanPromoteRecoveredRun(
            [
                "candidate:wall:trusted-bridge",
                "candidate:wall:untrusted-overlap"
            ],
            [
                "candidate:wall:trusted-bridge",
                "candidate:wall:untrusted-overlap"
            ],
            trusted));
        Assert.False(GlobalWallSolutionBuilder.CanPromoteRecoveredRun(
            ["candidate:wall:untrusted-overlap"],
            ["candidate:wall:untrusted-overlap"],
            trusted));
    }

    [Fact]
    public async Task StructuralCore_DoesNotRecoverReviewedFixtureDetailBetweenSelectedRuns()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var target = ReviewedMainStructuralBridge(
            template,
            "reviewed-fixture-detail",
            new LineExport(
                new PointExport(100, 200),
                new PointExport(400, 200))) with
        {
            Evidence =
            [
                "merged collinear wall fragments",
                "wall evidence assessment: ObjectOrFixtureDetail / review / confidence 0.71"
            ]
        };
        var supports = new[]
        {
            HostWallFragment(
                template,
                "reviewed-fixture-left",
                new LineExport(new PointExport(100, 80), new PointExport(100, 320))),
            HostWallFragment(
                template,
                "reviewed-fixture-right",
                new LineExport(new PointExport(400, 80), new PointExport(400, 320)))
        };
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            supports);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            supports.Append(target).ToArray(),
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        var selected = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Selected));
        Assert.Equal(0, selected.RecoveredCandidateCount);
        Assert.DoesNotContain(
            solutions.SelectedWallRuns,
            run => run.SourceWallIds.Contains(target.Id, StringComparer.Ordinal));
        var decision = Assert.Single(solutions.CandidateDecisions.Where(candidate =>
            candidate.CandidateId == $"candidate:wall:{target.Id}"));
        Assert.True(decision.StrongNegativeEvidence);
        Assert.Equal("Rejected", decision.Decision);
    }

    [Theory]
    [InlineData(320.97, 0.888, 4.0, true)]
    [InlineData(320.97, 0.950, 4.0, false)]
    [InlineData(100.0, 0.700, 10.0, false)]
    public void StructuralCore_RequiresMeaningfulAbsoluteGapForMostlyCoveredConsensusWall(
        double drawingLength,
        double coverageRatio,
        double thickness,
        bool expected)
    {
        Assert.Equal(
            expected,
            GlobalWallSolutionBuilder.HasSufficientUncoveredConsensusRecallLength(
                drawingLength,
                coverageRatio,
                thickness));
    }

    [Fact]
    public async Task StructuralCore_DoesNotRecoverUnsupportedShortSourceBackedGraphDetail()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var target = HostWallFragment(
            template,
            "unsupported-short-source-backed-target",
            new LineExport(
                new PointExport(100, 200),
                new PointExport(160, 200)));
        var structural = HostWallFragment(
            template,
            "unsupported-short-source-backed-structural",
            new LineExport(
                new PointExport(600, 100),
                new PointExport(600, 400)));
        var edge = CleanGraphEdge(target, "unsupported-short-source-backed-edge");
        var opening = AnchoredOpening(
            "unsupported-short-source-backed-opening",
            target.Id,
            structural.Id);
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [structural]);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [structural, target],
            Array.Empty<PlacementRoomExport>(),
            [opening],
            EmptyGraph(placement.WallGraph) with
            {
                Edges = [edge]
            },
            structuralSolution);

        var selectedHypothesis = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Selected));
        Assert.Equal(0, selectedHypothesis.RecoveredCandidateCount);
        Assert.DoesNotContain(
            solutions.SelectedWallRuns,
            run => run.SourceWallIds.Contains(target.Id, StringComparer.Ordinal));
        var decision = Assert.Single(solutions.CandidateDecisions.Where(candidate =>
            candidate.CandidateId == $"candidate:graph:{edge.Id}"));
        Assert.Equal("AlternativeHypothesisOnly", decision.Decision);
        Assert.Equal(3, decision.SelectedByHypothesisIds.Count);
    }

    [Fact]
    public async Task StructuralCore_DoesNotRecoverSourceBackedConsensusWithMultipleWeakNegatives()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var target = HostWallFragment(
            template,
            "weak-negative-source-backed-target",
            new LineExport(
                new PointExport(100, 200),
                new PointExport(400, 200))) with
        {
            Evidence =
            [
                "parallel wall-face pair",
                "room boundary",
                "main structural wall body",
                "dimension-like source context",
                "surface pattern proximity"
            ]
        };
        var structural = HostWallFragment(
            template,
            "weak-negative-source-backed-structural",
            new LineExport(
                new PointExport(600, 100),
                new PointExport(600, 400)));
        var edge = CleanGraphEdge(target, "weak-negative-source-backed-edge");
        var opening = AnchoredOpening(
            "weak-negative-source-backed-opening",
            target.Id,
            structural.Id);
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [structural]);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [structural, target],
            Array.Empty<PlacementRoomExport>(),
            [opening],
            EmptyGraph(placement.WallGraph) with
            {
                Edges = [edge]
            },
            structuralSolution);

        var selectedHypothesis = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Selected));
        Assert.Equal(0, selectedHypothesis.RecoveredCandidateCount);
        Assert.DoesNotContain(
            solutions.SelectedWallRuns,
            run => run.SourceWallIds.Contains(target.Id, StringComparer.Ordinal));
        var decision = Assert.Single(solutions.CandidateDecisions.Where(candidate =>
            candidate.CandidateId == $"candidate:graph:{edge.Id}"));
        Assert.False(decision.StrongNegativeEvidence);
        Assert.Equal("AlternativeHypothesisOnly", decision.Decision);
        Assert.Equal(3, decision.SelectedByHypothesisIds.Count);
    }

    [Fact]
    public async Task StructuralCore_DoesNotRecoverConsensusWallWithoutTwoIndoorRoomSupports()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var target = HostWallFragment(
            template,
            "single-room-consensus-target",
            new LineExport(
                new PointExport(100, 200),
                new PointExport(400, 200)));
        var supports = new[]
        {
            HostWallFragment(
                template,
                "single-room-support-left",
                new LineExport(new PointExport(100, 80), new PointExport(100, 320))),
            HostWallFragment(
                template,
                "single-room-support-right",
                new LineExport(new PointExport(400, 80), new PointExport(400, 320)))
        };
        var room = RoomBoundaryWithHorizontalSpan(
            placement.Rooms.First(),
            "single-consensus-room",
            [target.Id],
            startX: 100,
            endX: 400,
            axis: 200) with
        {
            UseKind = "Living"
        };
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            supports);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            supports.Append(target).ToArray(),
            [room],
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        var selectedHypothesis = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Selected));
        Assert.Equal(0, selectedHypothesis.RecoveredCandidateCount);
        Assert.DoesNotContain(
            solutions.SelectedWallRuns,
            run => run.SourceWallIds.Contains(target.Id, StringComparer.Ordinal));
        var targetDecision = Assert.Single(solutions.CandidateDecisions.Where(decision =>
            decision.CandidateId == $"candidate:wall:{target.Id}"));
        Assert.Equal("AlternativeHypothesisOnly", targetDecision.Decision);
        Assert.Equal(3, targetDecision.SelectedByHypothesisIds.Count);
    }

    [Fact]
    public async Task StructuralCore_RecoversUnknownInteriorBoundaryWithOppositeRoomCycles()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var target = HostWallFragment(
            template,
            "unknown-room-cycle-target",
            new LineExport(
                new PointExport(100, 200),
                new PointExport(400, 200)));
        var supports = new[]
        {
            HostWallFragment(
                template,
                "unknown-room-cycle-support-left",
                new LineExport(new PointExport(100, 80), new PointExport(100, 320))),
            HostWallFragment(
                template,
                "unknown-room-cycle-support-right",
                new LineExport(new PointExport(400, 80), new PointExport(400, 320)))
        };
        var rooms = new[]
        {
            RoomBoundaryWithHorizontalSpanBetween(
                placement.Rooms.First(),
                "unknown-room-cycle-above",
                [target.Id],
                startX: 100,
                endX: 400,
                top: 80,
                bottom: 200),
            RoomBoundaryWithHorizontalSpanBetween(
                placement.Rooms.First(),
                "unknown-room-cycle-below",
                [target.Id],
                startX: 100,
                endX: 400,
                top: 200,
                bottom: 320)
        };
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            supports);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            supports.Append(target).ToArray(),
            rooms,
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        var selectedHypothesis = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Selected));
        Assert.Equal(1, selectedHypothesis.RecoveredCandidateCount);
        Assert.Contains(
            solutions.SelectedWallRuns,
            run => run.SourceWallIds.Contains(target.Id, StringComparer.Ordinal));
        var decision = Assert.Single(solutions.CandidateDecisions.Where(candidate =>
            candidate.CandidateId == $"candidate:wall:{target.Id}"));
        Assert.Equal("Selected", decision.Decision);
        Assert.Contains(
            "two-sided source-linked room boundary support",
            decision.Evidence);
        Assert.Contains(
            "indoor room boundary support 0",
            decision.Evidence);
    }

    [Fact]
    public async Task StructuralCore_DoesNotRecoverUnknownInteriorBoundaryWithSameSideRoomCycles()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var target = HostWallFragment(
            template,
            "same-side-room-cycle-target",
            new LineExport(
                new PointExport(100, 200),
                new PointExport(400, 200)));
        var supports = new[]
        {
            HostWallFragment(
                template,
                "same-side-room-cycle-support-left",
                new LineExport(new PointExport(100, 80), new PointExport(100, 360))),
            HostWallFragment(
                template,
                "same-side-room-cycle-support-right",
                new LineExport(new PointExport(400, 80), new PointExport(400, 360)))
        };
        var rooms = new[]
        {
            RoomBoundaryWithHorizontalSpanBetween(
                placement.Rooms.First(),
                "same-side-room-cycle-near",
                [target.Id],
                startX: 100,
                endX: 400,
                top: 200,
                bottom: 300),
            RoomBoundaryWithHorizontalSpanBetween(
                placement.Rooms.First(),
                "same-side-room-cycle-far",
                [target.Id],
                startX: 100,
                endX: 400,
                top: 240,
                bottom: 360)
        };
        var structuralSolution = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            supports);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            supports.Append(target).ToArray(),
            rooms,
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        var selectedHypothesis = Assert.Single(solutions.Hypotheses.Where(hypothesis =>
            hypothesis.Selected));
        Assert.Equal(0, selectedHypothesis.RecoveredCandidateCount);
        Assert.DoesNotContain(
            solutions.SelectedWallRuns,
            run => run.SourceWallIds.Contains(target.Id, StringComparer.Ordinal));
        var decision = Assert.Single(solutions.CandidateDecisions.Where(candidate =>
            candidate.CandidateId == $"candidate:wall:{target.Id}"));
        Assert.Equal("AlternativeHypothesisOnly", decision.Decision);
        Assert.Contains(
            "no two-sided source-linked room boundary support",
            decision.Evidence);
    }

    [Fact]
    public async Task Solver_CompactsSelectedCandidatesAndPreservesProvenance()
    {
        var solutions = PlanPlacementExport.From(await CreateScanResultAsync()).WallSolutions;
        var nodeIds = solutions.SelectedWallRuns
            .SelectMany(run => new[] { run.FromNodeId, run.ToNodeId })
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(solutions.SelectedWallRunCount <= solutions.SelectedCandidateCount);
        Assert.All(solutions.SelectedWallRuns, run =>
        {
            Assert.True(run.DrawingLength > 0);
            Assert.NotEqual(run.FromNodeId, run.ToNodeId);
            Assert.Contains(run.FromNodeId, nodeIds);
            Assert.Contains(run.ToNodeId, nodeIds);
            Assert.NotEmpty(run.CandidateIds);
            Assert.NotEmpty(run.CandidateOrigins);
            Assert.NotEmpty(run.SourceWallIds);
            Assert.NotEmpty(run.SourcePrimitiveIds);
            Assert.Contains(
                run.Evidence,
                evidence => evidence.Contains("global wall solver compacted", StringComparison.Ordinal));
        });
        Assert.DoesNotContain(
            EquivalentRunPairs(solutions.SelectedWallRuns),
            pair => CollinearOverlapRatio(pair.First.CenterLine, pair.Second.CenterLine) > 0.95);
        Assert.DoesNotContain(
            CompetingSourceRunPairs(solutions.SelectedWallRuns),
            pair => AxisDistance(pair.First.CenterLine, pair.Second.CenterLine) <= 12
                && CollinearOverlapRatio(pair.First.CenterLine, pair.Second.CenterLine) >= 0.65);
    }

    [Fact]
    public async Task Solver_PreservesPlacementReadyExteriorShellTypeAcrossMixedCompaction()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var line = new LineExport(
            new PointExport(100, 200),
            new PointExport(400, 200));
        var interior = HostWallFragment(template, "mixed-shell-interior", line);
        var exteriorShell = HostWallFragment(
            template,
            "page:1:wall-exterior-shell-inferred:001",
            line) with
        {
            WallType = "Exterior",
            Confidence = 0.75,
            Evidence =
            [
                "wall evidence: inferred exterior shell wall from indoor room boundary with outside on opposite side",
                "wall evidence: exterior-shell inference source-line support coverage 1 from 8 primitive(s)"
            ]
        };
        var edge = CleanGraphEdge(interior, "mixed-shell-edge") with
        {
            SourceWallIds = [interior.Id, exteriorShell.Id],
            SourcePrimitiveIds = interior.SourcePrimitiveIds
                .Concat(exteriorShell.SourcePrimitiveIds)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Evidence = interior.Evidence
                .Concat(exteriorShell.Evidence)
                .ToArray()
        };
        var graph = EmptyGraph(placement.WallGraph) with
        {
            Edges = [edge]
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [interior, exteriorShell],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            graph);

        var run = Assert.Single(solutions.SelectedWallRuns);
        Assert.Equal("Exterior", run.WallType);
        Assert.Contains(interior.Id, run.SourceWallIds);
        Assert.Contains(exteriorShell.Id, run.SourceWallIds);
        Assert.Contains(
            run.Evidence,
            evidence => evidence.Contains(
                "resolved mixed wall type as Exterior",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Solver_DoesNotPromoteGenericMixedCompactionToExterior()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var line = new LineExport(
            new PointExport(100, 200),
            new PointExport(400, 200));
        var interior = HostWallFragment(template, "generic-mixed-interior", line);
        var exterior = HostWallFragment(template, "generic-mixed-exterior", line) with
        {
            WallType = "Exterior"
        };
        var edge = CleanGraphEdge(interior, "generic-mixed-edge") with
        {
            SourceWallIds = [interior.Id, exterior.Id],
            SourcePrimitiveIds = interior.SourcePrimitiveIds
                .Concat(exterior.SourcePrimitiveIds)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Evidence = interior.Evidence
                .Concat(exterior.Evidence)
                .ToArray()
        };
        var graph = EmptyGraph(placement.WallGraph) with
        {
            Edges = [edge]
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [interior, exterior],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            graph);

        var run = Assert.Single(solutions.SelectedWallRuns);
        Assert.Equal("Mixed", run.WallType);
        Assert.DoesNotContain(
            run.Evidence,
            evidence => evidence.Contains(
                "resolved mixed wall type as Exterior",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Solver_DoesNotOverrideTwoSidedInteriorAuthorityWithExteriorShellProvenance()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var line = new LineExport(
            new PointExport(100, 200),
            new PointExport(400, 200));
        var interior = HostWallFragment(template, "two-sided-mixed-interior", line);
        var exteriorShell = HostWallFragment(
            template,
            "page:1:wall-exterior-shell-inferred:002",
            line) with
        {
            WallType = "Exterior",
            Confidence = 0.75,
            Evidence =
            [
                "wall evidence: inferred exterior shell wall from indoor room boundary with outside on opposite side",
                "wall evidence: exterior-shell inference source-line support coverage 1 from 8 primitive(s)"
            ]
        };
        var edge = CleanGraphEdge(interior, "two-sided-mixed-edge") with
        {
            SourceWallIds = [interior.Id, exteriorShell.Id],
            SourcePrimitiveIds = interior.SourcePrimitiveIds
                .Concat(exteriorShell.SourcePrimitiveIds)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Evidence = interior.Evidence
                .Concat(exteriorShell.Evidence)
                .ToArray()
        };
        var graph = EmptyGraph(placement.WallGraph) with
        {
            Edges = [edge]
        };
        var rooms = new[]
        {
            RoomBoundaryWithHorizontalSpanBetween(
                placement.Rooms.First(),
                "two-sided-mixed-above",
                [interior.Id, exteriorShell.Id],
                startX: 100,
                endX: 400,
                top: 80,
                bottom: 200) with
            {
                UseKind = "Living"
            },
            RoomBoundaryWithHorizontalSpanBetween(
                placement.Rooms.First(),
                "two-sided-mixed-below",
                [interior.Id, exteriorShell.Id],
                startX: 100,
                endX: 400,
                top: 200,
                bottom: 320) with
            {
                UseKind = "Living"
            }
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [interior, exteriorShell],
            rooms,
            Array.Empty<PlacementOpeningExport>(),
            graph);

        var run = Assert.Single(solutions.SelectedWallRuns);
        Assert.Equal("Mixed", run.WallType);
        Assert.DoesNotContain(
            run.Evidence,
            evidence => evidence.Contains(
                "resolved mixed wall type as Exterior",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Solver_DistinguishesContextualLayerRiskFromExplicitNonWallEvidence()
    {
        Assert.False(GlobalWallSolutionBuilder.HasStrongNegativeEvidence(
            ["layer evidence: contains dimension-like text"]));
        Assert.Equal(
            1,
            GlobalWallSolutionBuilder.CountWeakNegativeEvidence(
                ["layer evidence: contains dimension-like text"]));

        Assert.True(GlobalWallSolutionBuilder.HasStrongNegativeEvidence(
            ["wall evidence: reclassified as object/fixture detail because the component is object-like"]));
        Assert.True(GlobalWallSolutionBuilder.HasStrongNegativeEvidence(
            ["wall evidence assessment: ObjectOrFixtureDetail / review / confidence 0.71"]));
        Assert.True(GlobalWallSolutionBuilder.HasStrongNegativeEvidence(
            ["parallel offset detail shadow retained as review evidence"]));
        Assert.True(GlobalWallSolutionBuilder.HasStrongNegativeEvidence(
            ["unfilled exterior opening-clearance rectangle is not a wall body"]));
        Assert.True(GlobalWallSolutionBuilder.HasStrongNegativeEvidence(
            ["test unlayered short line follows door swing symbol"]));
        Assert.False(GlobalWallSolutionBuilder.HasStrongNegativeEvidence(
            ["opening support includes door swing evidence near a real host wall"]));
    }

    [Fact]
    public void Reconciler_UsesProvenanceBeforeSemanticTypeForGeometryVotes()
    {
        Assert.True(GlobalWallSolutionBuilder.ReconciliationWallTypesCompatible(
            "Interior",
            "Exterior",
            authoritativeSourceLinked: true));
        Assert.False(GlobalWallSolutionBuilder.ReconciliationWallTypesCompatible(
            "Interior",
            "Exterior",
            authoritativeSourceLinked: false));
        Assert.True(GlobalWallSolutionBuilder.ReconciliationWallTypesCompatible(
            "Mixed",
            "Exterior",
            authoritativeSourceLinked: false));
    }

    [Fact]
    public async Task Solver_DoesNotRewardExcludedStructuralIslandAsMajorRecall()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var isolated = HostWallFragment(
            templateWall,
            "excluded-long-detail-island",
            new LineExport(
                new PointExport(600, 100),
                new PointExport(600, 400))) with
        {
            WallType = "Exterior",
            ExcludedFromStructuralTopology = true,
            Evidence =
            [
                "parallel wall-face pair",
                "candidate belongs only to excluded IsolatedFragment component without a trusted structural anchor"
            ]
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [isolated],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph));

        var decision = Assert.Single(solutions.CandidateDecisions);
        Assert.False(decision.MajorWallCandidate);
        Assert.True(decision.StrongNegativeEvidence);
        Assert.Equal("Rejected", decision.Decision);
        Assert.Empty(solutions.SelectedWallRuns);
    }

    [Fact]
    public async Task Solver_RecoversCoherentUnknownBoundaryWithoutPromotingIsolatedDetail()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var left = HostWallFragment(
            templateWall,
            "coherent-boundary-left",
            new LineExport(
                new PointExport(100, 80),
                new PointExport(100, 200)));
        var right = HostWallFragment(
            templateWall,
            "coherent-boundary-right",
            new LineExport(
                new PointExport(300, 80),
                new PointExport(300, 200)));
        var branch = HostWallFragment(
            templateWall,
            "coherent-boundary-branch",
            new LineExport(
                new PointExport(200, 100),
                new PointExport(200, 200)));
        var boundary = UnknownReviewBoundary(
            templateWall,
            "coherent-unknown-boundary",
            new LineExport(
                new PointExport(100, 200),
                new PointExport(300, 200)));
        var isolatedDetail = UnknownReviewBoundary(
            templateWall,
            "isolated-unknown-detail",
            new LineExport(
                new PointExport(100, 500),
                new PointExport(300, 500)));
        var roomTemplate = placement.Rooms.First();
        var upperRoom = RoomBoundaryAtAxis(
            roomTemplate,
            boundary.Id,
            axis: 80,
            sourceLinked: true) with
        {
            Id = "coherent-boundary-upper-room",
            UseKind = "Bedroom"
        };
        var lowerRoom = RoomBoundaryAtAxis(
            roomTemplate,
            boundary.Id,
            axis: 200,
            sourceLinked: true) with
        {
            Id = "coherent-boundary-lower-room",
            UseKind = "Living"
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [left, right, branch, boundary, isolatedDetail],
            [upperRoom, lowerRoom],
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph));

        var boundaryDecision = Assert.Single(solutions.CandidateDecisions.Where(decision =>
            decision.SourceWallIds.Contains(boundary.Id, StringComparer.Ordinal)));
        Assert.Equal("Selected", boundaryDecision.Decision);
        Assert.True(boundaryDecision.MajorWallCandidate);
        Assert.Equal("Interior", boundaryDecision.WallType);
        Assert.Contains(
            boundaryDecision.Evidence,
            evidence => string.Equals(
                evidence,
                "coherent room-boundary candidate",
                StringComparison.Ordinal));

        var boundaryRun = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            run.SourceWallIds.Contains(boundary.Id, StringComparer.Ordinal)));
        Assert.Equal("Interior", boundaryRun.WallType);
        Assert.Contains(
            boundaryRun.Evidence,
            evidence => evidence.Contains(
                "global solver coherent room boundary support",
                StringComparison.Ordinal));
        var inlineJunction = Assert.Single(boundaryRun.InlineJunctions);
        Assert.Equal("TJunction", inlineJunction.Kind);
        Assert.True(inlineJunction.RequiresReview);
        Assert.Equal(
            GlobalWallSolutionBuilder.TopologyOptimizerVersion,
            inlineJunction.Optimization.OptimizerVersion);

        var structure = PlanStructureExport.From(placement with
        {
            Walls = [left, right, branch, boundary, isolatedDetail],
            Rooms = [upperRoom, lowerRoom],
            Openings = Array.Empty<PlacementOpeningExport>(),
            WallGraph = EmptyGraph(placement.WallGraph),
            WallSolutions = solutions
        });
        var junctionNode = Assert.Single(structure.Nodes.Where(node =>
            string.Equals(node.Id, inlineJunction.NodeId, StringComparison.Ordinal)));
        Assert.Equal("TJunction", junctionNode.TopologyKind);
        Assert.Equal(3, junctionNode.Degree);
        Assert.Single(junctionNode.EndpointWallRunIds);
        Assert.Single(junctionNode.InlineWallRunIds);

        var detailDecision = Assert.Single(solutions.CandidateDecisions.Where(decision =>
            decision.SourceWallIds.Contains(isolatedDetail.Id, StringComparer.Ordinal)));
        Assert.Equal("Rejected", detailDecision.Decision);
        Assert.False(detailDecision.MajorWallCandidate);
        Assert.DoesNotContain(
            solutions.SelectedWallRuns,
            run => run.SourceWallIds.Contains(isolatedDetail.Id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Topology_DoesNotAttachReadyEndpointToGenericReviewWall()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var branch = HostWallFragment(
            templateWall,
            "generic-review-branch",
            new LineExport(
                new PointExport(200, 100),
                new PointExport(200, 200)));
        var reviewHost = HostWallFragment(
            templateWall,
            "generic-review-host",
            new LineExport(
                new PointExport(100, 200),
                new PointExport(300, 200))) with
        {
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: false,
                ReadyForMetricPlacement: false,
                RequiresReview: true,
                Confidence: 0.85,
                Reasons: ["generic review wall lacks coherent-boundary authorization"])
        };
        var roomTemplate = placement.Rooms.First();
        var upperRoom = RoomBoundaryAtAxis(
            roomTemplate,
            reviewHost.Id,
            axis: 80,
            sourceLinked: true);
        var lowerRoom = RoomBoundaryAtAxis(
            roomTemplate,
            reviewHost.Id,
            axis: 200,
            sourceLinked: true);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [branch, reviewHost],
            [upperRoom, lowerRoom],
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph));

        var hostRun = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            run.SourceWallIds.Contains(reviewHost.Id, StringComparer.Ordinal)));
        Assert.True(hostRun.Reliability.RequiresReview);
        Assert.Empty(hostRun.InlineJunctions);
        Assert.Equal(0, solutions.Topology.JunctionNodeCount);
        Assert.Equal(0, solutions.Topology.InlineJunctionReferenceCount);
    }

    [Fact]
    public async Task Solver_EmitsConnectedTJunctionsForSmallSourceGaps()
    {
        var solutions = PlanPlacementExport.From(
            await CreateScanResultAsync(partitionInset: 5)).WallSolutions;
        var partition = solutions.SelectedWallRuns
            .Where(run => IsVertical(run.CenterLine))
            .OrderBy(run => Math.Abs(((run.CenterLine.Start.X + run.CenterLine.End.X) / 2.0) - 300))
            .First();
        var minimumY = Math.Min(partition.CenterLine.Start.Y, partition.CenterLine.End.Y);
        var maximumY = Math.Max(partition.CenterLine.Start.Y, partition.CenterLine.End.Y);

        Assert.InRange(minimumY, 79, 81);
        Assert.InRange(maximumY, 359, 361);
    }

    [Fact]
    public async Task EndpointNodeSolver_PreservesOrthogonalityAtOffsetCorner()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var horizontal = HostWallFragment(
            template,
            "offset-corner-horizontal",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(200, 100)));
        var vertical = HostWallFragment(
            template,
            "offset-corner-vertical",
            new LineExport(
                new PointExport(201, 99),
                new PointExport(201, 220)));
        var graph = EmptyGraph(placement.WallGraph);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [horizontal, vertical],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            graph);

        var horizontalRun = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            run.SourceWallIds.Contains(horizontal.Id, StringComparer.Ordinal)));
        var verticalRun = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            run.SourceWallIds.Contains(vertical.Id, StringComparer.Ordinal)));
        Assert.Equal(horizontalRun.CenterLine.Start.Y, horizontalRun.CenterLine.End.Y, 9);
        Assert.Equal(verticalRun.CenterLine.Start.X, verticalRun.CenterLine.End.X, 9);
        Assert.Equal(horizontalRun.ToNodeId, verticalRun.FromNodeId);
        Assert.Equal(horizontalRun.CenterLine.End.X, verticalRun.CenterLine.Start.X, 9);
        Assert.Equal(horizontalRun.CenterLine.End.Y, verticalRun.CenterLine.Start.Y, 9);
        Assert.Equal(201, horizontalRun.CenterLine.End.X, 9);
        Assert.Equal(100, horizontalRun.CenterLine.End.Y, 9);

        var structure = PlanStructureExport.From(placement with
        {
            Walls = [horizontal, vertical],
            Rooms = Array.Empty<PlacementRoomExport>(),
            Openings = Array.Empty<PlacementOpeningExport>(),
            WallGraph = graph,
            WallSolutions = solutions
        });
        Assert.DoesNotContain(
            PlanStructureValidator.Validate(structure),
            message => string.Equals(message.Severity, "Error", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EndpointNodeSolver_DoesNotMergeOffsetParallelEndpoints()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var first = HostWallFragment(
            template,
            "parallel-endpoint-first",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(200, 100))) with
        {
            WallType = "Exterior"
        };
        var second = HostWallFragment(
            template,
            "parallel-endpoint-second",
            new LineExport(
                new PointExport(200.5, 101),
                new PointExport(300, 101))) with
        {
            WallType = "Interior"
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [first, second],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph));

        var firstRun = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            run.SourceWallIds.Contains(first.Id, StringComparer.Ordinal)));
        var secondRun = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            run.SourceWallIds.Contains(second.Id, StringComparer.Ordinal)));
        Assert.NotEqual(firstRun.ToNodeId, secondRun.FromNodeId);
        Assert.Equal(100, firstRun.CenterLine.Start.Y, 9);
        Assert.Equal(100, firstRun.CenterLine.End.Y, 9);
        Assert.Equal(101, secondRun.CenterLine.Start.Y, 9);
        Assert.Equal(101, secondRun.CenterLine.End.Y, 9);
    }

    [Fact]
    public async Task Solver_ReconstructsOneLogicalHostWallAndPreservesOpeningGap()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var leftLine = new LineExport(
            new PointExport(100, 120),
            new PointExport(230, 120));
        var rightLine = new LineExport(
            new PointExport(270, 120),
            new PointExport(400, 120));
        var left = HostWallFragment(template, "host-left", leftLine);
        var right = HostWallFragment(template, "host-right", rightLine);
        var opening = AnchoredOpening("door-gap", left.Id, right.Id);
        var graph = placement.WallGraph with
        {
            Nodes = Array.Empty<PlacementWallGraphNodeExport>(),
            Edges = Array.Empty<PlacementWallGraphEdgeExport>(),
            Components = Array.Empty<PlacementWallGraphComponentExport>(),
            RepairCandidateIds = Array.Empty<string>(),
            ResidualEndpointOnHostCandidates =
                Array.Empty<PlacementWallGraphResidualEndpointOnHostCandidateExport>()
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [left, right],
            Array.Empty<PlacementRoomExport>(),
            [opening],
            graph);

        var host = Assert.Single(solutions.SelectedWallRuns);
        var interval = Assert.Single(host.OpeningIntervals);
        Assert.Equal(2, host.SolidIntervals.Count);
        Assert.Equal(1, host.ReconstructedOpeningGapCount);
        Assert.Equal("ReconstructedGap", interval.AttachmentKind);
        Assert.Equal(opening.Id, interval.OpeningId);
        Assert.Equal(300, host.DrawingLength, 6);
        Assert.Equal(40, host.OpeningDrawingLength, 6);
        Assert.Equal(260, host.SolidDrawingLength, 6);
        Assert.Equal(host.DrawingLength, host.SolidDrawingLength + host.OpeningDrawingLength, 6);
        Assert.Equal(new[] { left.Id, right.Id }, host.SourceWallIds.Order(StringComparer.Ordinal));
        Assert.Equal(230, interval.CenterLine.Start.X, 6);
        Assert.Equal(270, interval.CenterLine.End.X, 6);
        Assert.Equal(interval.OpeningId, host.SolidIntervals[0].AdjacentOpeningIds.Single());
        Assert.Equal(interval.OpeningId, host.SolidIntervals[1].AdjacentOpeningIds.Single());
        Assert.True(host.SolidIntervals[0].CenterLine.End.X <= interval.CenterLine.Start.X);
        Assert.True(host.SolidIntervals[1].CenterLine.Start.X >= interval.CenterLine.End.X);
        Assert.Equal(GlobalWallSolutionBuilder.ReconcilerVersion, solutions.Reconciliation.ReconcilerVersion);
        Assert.Equal(host.CenterLine, host.Reconciliation.ReconciledCenterLine);

        var structure = PlanStructureExport.From(placement with
        {
            Walls = [left, right],
            Rooms = Array.Empty<PlacementRoomExport>(),
            Openings = [opening],
            WallGraph = graph,
            WallSolutions = solutions
        });
        var structureHost = Assert.Single(structure.WallRuns);
        var structureOpening = Assert.Single(structure.Openings);
        Assert.Equal(host.OpeningIntervals, structureHost.OpeningIntervals);
        Assert.Equal(host.SolidIntervals, structureHost.SolidIntervals);
        Assert.Equal([structureHost.Id], structureOpening.HostWallRunIds);
        Assert.Equal(
            [structureHost.OpeningIntervals.Single().Id],
            structureOpening.HostWallOpeningIntervalIds);
        Assert.DoesNotContain(
            PlanStructureValidator.Validate(structure),
            message => string.Equals(message.Severity, "Error", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Topology_DoesNotAttachInlineJunctionInsideOpeningGap()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var left = HostWallFragment(
            template,
            "opening-host-left",
            new LineExport(
                new PointExport(100, 120),
                new PointExport(230, 120)));
        var right = HostWallFragment(
            template,
            "opening-host-right",
            new LineExport(
                new PointExport(270, 120),
                new PointExport(400, 120)));
        var branch = HostWallFragment(
            template,
            "opening-branch",
            new LineExport(
                new PointExport(250, 120),
                new PointExport(250, 300)));
        var opening = AnchoredOpening("topology-opening-gap", left.Id, right.Id);
        var graph = EmptyGraph(placement.WallGraph);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [left, right, branch],
            Array.Empty<PlacementRoomExport>(),
            [opening],
            graph);

        var host = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            IsHorizontal(run.CenterLine)));
        Assert.Single(host.OpeningIntervals);
        Assert.Empty(host.InlineJunctions);
        Assert.Equal(0, solutions.Topology.InlineJunctionReferenceCount);
        Assert.Equal(0, solutions.Topology.JunctionNodeCount);
    }

    [Fact]
    public async Task Topology_RepresentsCrossingWithoutSplittingCanonicalWalls()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var template = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var horizontal = HostWallFragment(
            template,
            "crossing-horizontal",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(400, 100)));
        var vertical = HostWallFragment(
            template,
            "crossing-vertical",
            new LineExport(
                new PointExport(250, 0),
                new PointExport(250, 300)));
        var graph = EmptyGraph(placement.WallGraph);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [horizontal, vertical],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            graph);

        Assert.Equal(2, solutions.SelectedWallRuns.Count);
        Assert.All(
            solutions.SelectedWallRuns,
            run => Assert.Single(run.InlineJunctions));
        var references = solutions.SelectedWallRuns
            .Select(run => run.InlineJunctions.Single())
            .ToArray();
        Assert.Single(references.Select(reference => reference.NodeId).Distinct(StringComparer.Ordinal));
        Assert.All(references, reference =>
        {
            Assert.Equal("Crossing", reference.Kind);
            Assert.Equal(0, reference.ProjectionResidualDrawingUnits, 6);
            Assert.False(reference.Optimization.EndpointAnchored);
            Assert.True(reference.Optimization.Converged);
        });
        Assert.Equal(1, solutions.Topology.JunctionNodeCount);
        Assert.Equal(2, solutions.Topology.InlineJunctionReferenceCount);
        Assert.Equal(1, solutions.Topology.CrossingNodeCount);

        var structure = PlanStructureExport.From(placement with
        {
            Walls = [horizontal, vertical],
            Rooms = Array.Empty<PlacementRoomExport>(),
            Openings = Array.Empty<PlacementOpeningExport>(),
            WallGraph = graph,
            WallSolutions = solutions
        });
        var crossing = Assert.Single(structure.Nodes.Where(node =>
            string.Equals(node.TopologyKind, "Crossing", StringComparison.Ordinal)));
        Assert.Equal(4, crossing.Degree);
        Assert.Empty(crossing.EndpointWallRunIds);
        Assert.Equal(2, crossing.InlineWallRunIds.Count);
        Assert.Equal(1, structure.Summary.ConnectedComponentCount);
        Assert.DoesNotContain(
            PlanStructureValidator.Validate(structure),
            message => string.Equals(message.Severity, "Error", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reconciler_AlignsCanonicalAxisWhenRoomAndOpeningEvidenceAgree()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var sourceLine = new LineExport(
            new PointExport(100, 104),
            new PointExport(400, 104));
        var wall = HostWallFragment(templateWall, "axis-source", sourceLine);
        var room = RoomBoundaryAtAxis(
            placement.Rooms.First(),
            wall.Id,
            axis: 100,
            sourceLinked: true);
        var opening = ShiftOpeningToAxis(
            AnchoredOpening("axis-opening", wall.Id, wall.Id),
            axis: 100);
        var graph = EmptyGraph(placement.WallGraph);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [wall],
            [room],
            [opening],
            graph);

        var reconciled = Assert.Single(solutions.SelectedWallRuns);
        Assert.Equal(100, reconciled.CenterLine.Start.Y, 3);
        Assert.Equal(100, reconciled.CenterLine.End.Y, 3);
        Assert.Equal(-4, reconciled.Reconciliation.AxisShiftDrawingUnits, 3);
        Assert.True(reconciled.Reconciliation.RoomBoundaryVoteCount > 0);
        Assert.True(reconciled.Reconciliation.OpeningVoteCount > 0);
        Assert.Contains("AxisAligned", reconciled.Reconciliation.Actions);
        Assert.Equal("Adjusted", reconciled.Reconciliation.Status);
        Assert.Equal(1, solutions.Reconciliation.AxisAlignedWallRunCount);
        Assert.Equal(1, solutions.Reconciliation.AdjustedWallRunCount);
        Assert.Equal(4, solutions.Reconciliation.MaximumAxisShiftDrawingUnits, 3);
    }

    [Fact]
    public async Task Reconciler_DoesNotMoveWallFromOneUnlinkedRoomVote()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var sourceLine = new LineExport(
            new PointExport(100, 104),
            new PointExport(400, 104));
        var wall = HostWallFragment(templateWall, "axis-guard", sourceLine);
        var room = RoomBoundaryAtAxis(
            placement.Rooms.First(),
            wall.Id,
            axis: 100,
            sourceLinked: false);
        var graph = EmptyGraph(placement.WallGraph);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [wall],
            [room],
            Array.Empty<PlacementOpeningExport>(),
            graph);

        var reconciled = Assert.Single(solutions.SelectedWallRuns);
        Assert.Equal(104, reconciled.CenterLine.Start.Y, 6);
        Assert.Equal(0, reconciled.Reconciliation.AxisShiftDrawingUnits, 6);
        Assert.DoesNotContain("AxisAligned", reconciled.Reconciliation.Actions);
        Assert.Equal(0, solutions.Reconciliation.AxisAlignedWallRunCount);
    }

    [Fact]
    public async Task Reconciler_DoesNotTreatCandidateAndNeighborVariantsAsIndependentLargeShift()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var source = HostWallFragment(
            templateWall,
            "source-axis-wall",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(220, 100))) with
        {
            ThicknessDrawingUnits = 16.8
        };
        var competing = HostWallFragment(
            templateWall,
            "context-axis-wall",
            new LineExport(
                new PointExport(100, 107.5),
                new PointExport(220, 107.5))) with
        {
            ThicknessDrawingUnits = 16.8
        };
        var contextOnlyRoom = RoomBoundaryAtAxis(
            placement.Rooms.First(),
            competing.Id,
            axis: 107.5,
            sourceLinked: true) with
        {
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: false,
                ReadyForMetricPlacement: false,
                RequiresReview: true,
                Confidence: 0.35,
                Reasons: ["room is context-only and cannot authorize geometry movement"])
        };
        var graph = EmptyGraph(placement.WallGraph);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [source, competing],
            [contextOnlyRoom],
            Array.Empty<PlacementOpeningExport>(),
            graph);

        Assert.Equal(2, solutions.SelectedWallRuns.Count);
        var sourceRun = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            run.SourceWallIds.Contains(source.Id, StringComparer.Ordinal)));
        Assert.Equal(100, sourceRun.CenterLine.Start.Y, 6);
        Assert.Equal(100, sourceRun.CenterLine.End.Y, 6);
        Assert.Equal(0, sourceRun.Reconciliation.AxisShiftDrawingUnits, 6);
        Assert.DoesNotContain("AxisAligned", sourceRun.Reconciliation.Actions);
        Assert.Contains(
            sourceRun.Reconciliation.Evidence,
            evidence => evidence.Contains(
                "fewer than two independent geometry sources",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reconciler_CollapsesAlignedDuplicateRunsAndPreservesProvenance()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var canonical = HostWallFragment(
            templateWall,
            "canonical-axis-wall",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(400, 100)));
        var duplicate = HostWallFragment(
            templateWall,
            "duplicate-axis-wall",
            new LineExport(
                new PointExport(100, 104),
                new PointExport(400, 104)));
        var room = RoomBoundaryAtAxis(
            placement.Rooms.First(),
            duplicate.Id,
            axis: 100,
            sourceLinked: true);
        var opening = ShiftOpeningToAxis(
            AnchoredOpening("duplicate-axis-opening", duplicate.Id, duplicate.Id),
            axis: 100);
        var graph = EmptyGraph(placement.WallGraph);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [canonical, duplicate],
            [room],
            [opening],
            graph);

        var run = Assert.Single(solutions.SelectedWallRuns);
        Assert.Equal(
            new[] { canonical.Id, duplicate.Id },
            run.SourceWallIds.Order(StringComparer.Ordinal));
        Assert.Equal(1, run.Reconciliation.CollapsedDuplicateRunCount);
        Assert.Equal(1, solutions.Reconciliation.CollapsedDuplicateWallRunCount);
        Assert.Contains(
            run.Reconciliation.Evidence,
            evidence => evidence.Contains(
                "collapsed one near-coincident duplicate run",
                StringComparison.Ordinal));
        Assert.Contains(
            run.OpeningIntervals,
            interval => interval.OpeningId == opening.Id);
    }

    [Fact]
    public void Reconciler_ExpandsAxisToleranceOnlyForSharedGraphStructuralRepresentations()
    {
        var sharedCrossRepresentationTolerance =
            GlobalWallSolutionBuilder.ReconciledDuplicateAxisTolerance(
                firstThickness: 6.64,
                secondThickness: 3.81,
                sharesSourceWall: true,
                firstHasStructuralCore: true,
                secondHasStructuralCore: true,
                firstHasCleanGraph: false,
                secondHasCleanGraph: true);
        var unrelatedStructuralTolerance =
            GlobalWallSolutionBuilder.ReconciledDuplicateAxisTolerance(
                firstThickness: 6.64,
                secondThickness: 3.81,
                sharesSourceWall: true,
                firstHasStructuralCore: true,
                secondHasStructuralCore: true,
                firstHasCleanGraph: false,
                secondHasCleanGraph: false);

        Assert.True(sharedCrossRepresentationTolerance >= 3.35);
        Assert.True(unrelatedStructuralTolerance < 3.35);
        Assert.InRange(sharedCrossRepresentationTolerance, 0.75, 8.0);
    }

    [Fact]
    public async Task CandidatePool_ConsolidatesExplicitCleanTopologyRepresentation()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var host = HostWallFragment(
            templateWall,
            "page:1:wall-exterior-shell-source-backed:002",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(500, 100)));
        var represented = HostWallFragment(
            templateWall,
            "page:1:wall:52",
            new LineExport(
                new PointExport(180, 103.2),
                new PointExport(440, 103.2))) with
        {
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: false,
                ReadyForMetricPlacement: false,
                RequiresReview: true,
                Confidence: 0.91,
                Reasons: ["wall omitted from clean placement topology"]),
            PlacementOmission = new PlacementWallOmissionExport(
                Code: "duplicate_clean_topology_span",
                Category: "DuplicateCleanTopology",
                Message: "Wall is already represented by clean topology.",
                RecommendedAction: "Use the linked clean wall.",
                LinkedWallIds: [host.Id],
                RepairCandidateIds: Array.Empty<string>(),
                Evidence:
                [
                    $"wall already represented by clean topology span from wall {host.Id}; overlap 1; axis distance 3.2 drawing units"
                ])
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [host, represented],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph));

        var run = Assert.Single(solutions.SelectedWallRuns);
        Assert.Equal(100, run.CenterLine.Start.Y, 6);
        Assert.Equal(100, run.CenterLine.End.Y, 6);
        Assert.Equal(
            new[] { host.Id, represented.Id },
            run.SourceWallIds.Order(StringComparer.Ordinal));
        Assert.Contains(represented.Id, run.SourcePrimitiveIds);
        Assert.Contains(
            run.Evidence,
            evidence => evidence.Contains(
                "explicitly represented by clean topology",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            solutions.CandidateDecisions,
            decision => string.Equals(
                decision.CandidateId,
                $"candidate:wall:{represented.Id}",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task CandidatePool_PreservesNearbyParallelWallWithoutExplicitRepresentation()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var first = HostWallFragment(
            templateWall,
            "parallel-room-wall-a",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(400, 100)));
        var second = HostWallFragment(
            templateWall,
            "parallel-room-wall-b",
            new LineExport(
                new PointExport(100, 114),
                new PointExport(400, 114)));

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [first, second],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph));

        Assert.Equal(2, solutions.SelectedWallRuns.Count);
        Assert.Contains(
            solutions.SelectedWallRuns,
            run => run.SourceWallIds.Contains(first.Id, StringComparer.Ordinal));
        Assert.Contains(
            solutions.SelectedWallRuns,
            run => run.SourceWallIds.Contains(second.Id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task CandidatePool_RepresentationProvenanceCannotOverrideHostStructuralBlock()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var host = HostWallFragment(
            templateWall,
            "outdoor-fill-boundary",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(500, 100)));
        var represented = HostWallFragment(
            templateWall,
            "dimension-pair-provenance",
            new LineExport(
                new PointExport(180, 103.2),
                new PointExport(440, 103.2))) with
        {
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: false,
                ReadyForMetricPlacement: false,
                RequiresReview: true,
                Confidence: 0.8,
                Reasons: ["represented by clean topology"]),
            PlacementOmission = new PlacementWallOmissionExport(
                Code: "duplicate_clean_topology_span",
                Category: "DuplicateCleanTopology",
                Message: "Wall is already represented by clean topology.",
                RecommendedAction: "Use the linked clean wall.",
                LinkedWallIds: [host.Id],
                RepairCandidateIds: Array.Empty<string>(),
                Evidence:
                [
                    $"wall already represented by clean topology span from wall {host.Id}; overlap 1; axis distance 3.2 drawing units"
                ])
        };
        var structuralSolution = result.StructuralPlanSolution with
        {
            CandidateDecisions =
            [
                new StructuralWallDecision(
                    $"structural:wall:{host.Id}",
                    StructuralWallDecisionKind.RetainedForReview,
                    UnaryScore: -0.4,
                    ObjectiveContribution: 0,
                    Reasons: ["outdoor fill boundary is not structural geometry"])
                {
                    SourceWallIds = [host.Id],
                    AbsolutePlacementBlock = true,
                    BlockingSignalKinds =
                    [
                        StructuralEvidenceSignalKind.ContextOnlyBoundary
                    ]
                },
                new StructuralWallDecision(
                    $"structural:wall:{represented.Id}",
                    StructuralWallDecisionKind.Selected,
                    UnaryScore: 0.5,
                    ObjectiveContribution: 0.2,
                    Reasons: ["retained only as represented source provenance"])
                {
                    SourceWallIds = [represented.Id]
                }
            ],
            WallRuns = Array.Empty<StructuralWallRun>()
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [host, represented],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structuralSolution);

        var hostDecision = Assert.Single(
            solutions.CandidateDecisions,
            decision => decision.SourceWallIds.Contains(host.Id, StringComparer.Ordinal));
        Assert.Contains(represented.Id, hostDecision.SourceWallIds);
        Assert.True(hostDecision.StrongNegativeEvidence);
        Assert.Equal("Rejected", hostDecision.Decision);
        Assert.Empty(solutions.SelectedWallRuns);
    }

    [Fact]
    public async Task Compactor_DoesNotLetWeakReviewEvidenceExtendTrustedWall()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var trusted = HostWallFragment(
            templateWall,
            "trusted-wall-extent",
            new LineExport(
                new PointExport(250, 100),
                new PointExport(250, 300)));
        var reviewTail = HostWallFragment(
            templateWall,
            "dimension-like-review-tail",
            new LineExport(
                new PointExport(250, 299),
                new PointExport(250, 500))) with
        {
            Confidence = 0.99,
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: true,
                ReadyForMetricPlacement: false,
                RequiresReview: true,
                Confidence: 0.99,
                Reasons: ["dimension-like source requires review"]),
            Evidence =
            [
                "parallel wall-face pair",
                "main structural wall body",
                "layer evidence: contains dimension-like text"
            ]
        };
        var graph = EmptyGraph(placement.WallGraph);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [trusted, reviewTail],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            graph);

        var run = Assert.Single(solutions.SelectedWallRuns);
        Assert.Equal(100, Math.Min(run.CenterLine.Start.Y, run.CenterLine.End.Y), 6);
        Assert.Equal(300, Math.Max(run.CenterLine.Start.Y, run.CenterLine.End.Y), 6);
        Assert.Equal(200, run.DrawingLength, 6);
        Assert.Equal(2, run.CandidateIds.Count);
        Assert.Contains(
            run.Evidence,
            evidence => evidence.Contains(
                "weak review-only contributors could corroborate overlap but could not extend the wall",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task StructuralCore_DoesNotLetReviewOnlySourceExtendTrustedWall()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var trusted = HostWallFragment(
            templateWall,
            "structural-trusted-wall-extent",
            new LineExport(
                new PointExport(250, 100),
                new PointExport(250, 300)));
        var reviewTail = HostWallFragment(
            templateWall,
            "structural-review-only-tail",
            new LineExport(
                new PointExport(250, 299),
                new PointExport(250, 700))) with
        {
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: false,
                ReadyForMetricPlacement: false,
                RequiresReview: true,
                Confidence: 0.92,
                Reasons: ["review-only source cannot define canonical extent"])
        };
        var structural = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [trusted]);
        var runTemplate = structural.WallRuns[0];
        structural = structural with
        {
            WallRuns =
            [
                runTemplate with
                {
                    CenterLine = new PlanLineSegment(
                        new PlanPoint(250, 100),
                        new PlanPoint(250, 700)),
                    SourceWallIds = [trusted.Id, reviewTail.Id],
                    SourcePrimitiveIds = trusted.SourcePrimitiveIds
                        .Concat(reviewTail.SourcePrimitiveIds)
                        .ToArray()
                }
            ]
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [trusted, reviewTail],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structural);

        var run = Assert.Single(solutions.SelectedWallRuns);
        Assert.Equal(100, Math.Min(run.CenterLine.Start.Y, run.CenterLine.End.Y), 6);
        Assert.Equal(300, Math.Max(run.CenterLine.Start.Y, run.CenterLine.End.Y), 6);
        Assert.Equal(200, run.DrawingLength, 6);
        Assert.Contains(
            run.Evidence,
            evidence => evidence.Contains(
                "extent clipped to coordinate-ready source-wall support",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task StructuralCore_RetainsTrustedRecoveredPairedContinuationExtent()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var trusted = HostWallFragment(
            templateWall,
            "trusted-lower-wall-body",
            new LineExport(
                new PointExport(250, 155.7),
                new PointExport(250, 395.9))) with
        {
            WallComponentId = "main-structural-component",
            WallComponentKind = "MainStructural"
        };
        var recovered = HostWallFragment(
            templateWall,
            "recovered-upper-wall-body",
            new LineExport(
                new PointExport(250, 103.1),
                new PointExport(250, 153.5))) with
        {
            DetectionKind = "ParallelLinePair",
            WallComponentId = trusted.WallComponentId,
            WallComponentKind = trusted.WallComponentKind,
            Confidence = 0.876,
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: false,
                ReadyForMetricPlacement: false,
                RequiresReview: true,
                Confidence: 0.876,
                Reasons:
                [
                    "wall evidence requires review",
                    "wall omitted from clean placement topology (duplicate_wall_face)"
                ]),
            PlacementOmission = new PlacementWallOmissionExport(
                Code: "duplicate_wall_face",
                Category: "DuplicateWallFace",
                Message: "Earlier geometry classified this wall as a duplicate.",
                RecommendedAction: "Use synchronized geometry to verify the relationship.",
                LinkedWallIds: [trusted.Id],
                RepairCandidateIds: Array.Empty<string>(),
                Evidence:
                [
                    $"recovered duplicate wall body already represented by {trusted.Id}"
                ]),
            EvidenceAssessment = new WallEvidenceAssessmentExport(
                Category: "MediumWallBody",
                Confidence: 0.876,
                PlacementReady: false,
                RequiresReview: true,
                RejectedAsNoise: false,
                ScoreBreakdown: new WallEvidenceScoreBreakdownExport(
                    PositiveScore: 0.9,
                    NegativeScore: 0,
                    DecisionScore: 0.9,
                    PairSupportScore: 0.5,
                    LayerSupportScore: 0,
                    StructuralSupportScore: 0.2,
                    RecoverySupportScore: 0.2,
                    NoisePenalty: 0,
                    FragmentReviewPenalty: 0,
                    PositiveEvidence:
                    [
                        "strong parallel-face wall pair",
                        "both endpoints supported by structural context",
                        "missing-wall recovery evidence"
                    ],
                    NegativeEvidence:
                    [
                        "not placement-ready without review"
                    ]),
                SourcePrimitiveIds: ["recovered-upper-wall-body"],
                Evidence:
                [
                    "recovered by wall evidence map from unclaimed parallel wall-face evidence"
                ]),
            Evidence = Enumerable.Range(0, 64)
                .Select(index => $"recovered wall source provenance {index:00}")
                .Concat(
                [
                    "recovered by wall evidence map from unclaimed parallel wall-face evidence",
                    "parallel wall-face pair",
                    "room boundary",
                    "main structural wall body"
                ])
                .ToArray()
        };
        var structural = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [trusted]);
        var runTemplate = structural.WallRuns[0];
        structural = structural with
        {
            WallRuns =
            [
                runTemplate with
                {
                    CenterLine = new PlanLineSegment(
                        new PlanPoint(250, 103.1),
                        new PlanPoint(250, 395.9)),
                    SourceWallIds = [trusted.Id, recovered.Id],
                    SourcePrimitiveIds = trusted.SourcePrimitiveIds
                        .Concat(recovered.SourcePrimitiveIds)
                        .ToArray()
                }
            ]
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [trusted, recovered],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structural);

        var run = Assert.Single(solutions.SelectedWallRuns);
        Assert.Equal(103.1, Math.Min(run.CenterLine.Start.Y, run.CenterLine.End.Y), 6);
        Assert.Equal(395.9, Math.Max(run.CenterLine.Start.Y, run.CenterLine.End.Y), 6);
        Assert.Equal(292.8, run.DrawingLength, 6);
        Assert.Contains(
            run.Evidence,
            evidence => evidence.Contains(
                "retained an adjacent source-backed recovered wall-body continuation",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task StructuralCore_ClipsDominantRoomHypothesisTailBeyondCleanSource()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var trusted = HostWallFragment(
            templateWall,
            "structural-clean-source-extent",
            new LineExport(
                new PointExport(250, 100),
                new PointExport(250, 140)));
        var structural = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [trusted]);
        var runTemplate = structural.WallRuns[0];
        structural = structural with
        {
            WallRuns =
            [
                runTemplate with
                {
                    CenterLine = new PlanLineSegment(
                        new PlanPoint(250, 100),
                        new PlanPoint(250, 240)),
                    SourceWallIds = [trusted.Id],
                    SourceRoomIds = ["room:hypothesis-tail"],
                    SourcePrimitiveIds = trusted.SourcePrimitiveIds
                }
            ]
        };

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [trusted],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structural);

        var run = Assert.Single(solutions.SelectedWallRuns);
        Assert.Equal(100, Math.Min(run.CenterLine.Start.Y, run.CenterLine.End.Y), 6);
        Assert.Equal(140, Math.Max(run.CenterLine.Start.Y, run.CenterLine.End.Y), 6);
        Assert.Equal(40, run.DrawingLength, 6);
        Assert.Contains(
            run.Evidence,
            evidence => evidence.Contains(
                "unsupported structural-hypothesis tails remain provenance",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reconciler_ExtendsBranchToSupportedPerpendicularJunction()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var host = HostWallFragment(
            templateWall,
            "junction-host",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(400, 100)));
        var branch = HostWallFragment(
            templateWall,
            "junction-branch",
            new LineExport(
                new PointExport(250, 105),
                new PointExport(250, 300)));
        var room = RoomBoundaryAtAxis(
            placement.Rooms.First(),
            host.Id,
            axis: 100,
            sourceLinked: true) with
        {
            WallIds = [host.Id, branch.Id]
        };
        var graph = EmptyGraph(placement.WallGraph);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [host, branch],
            [room],
            Array.Empty<PlacementOpeningExport>(),
            graph);

        var verticalRuns = solutions.SelectedWallRuns
            .Where(run => IsVertical(run.CenterLine))
            .ToArray();
        Assert.True(
            verticalRuns.Length == 1,
            $"Expected one vertical run, found {verticalRuns.Length}: "
            + string.Join(
                " | ",
                solutions.SelectedWallRuns.Select(run =>
                    $"{run.CenterLine.Start.X:0.###},{run.CenterLine.Start.Y:0.###}"
                    + $"->{run.CenterLine.End.X:0.###},{run.CenterLine.End.Y:0.###}")));
        var reconciledBranch = verticalRuns[0];
        Assert.Equal(100, Math.Min(
            reconciledBranch.CenterLine.Start.Y,
            reconciledBranch.CenterLine.End.Y), 6);
        Assert.Contains("ExtendedStart", reconciledBranch.Reconciliation.Actions);
        Assert.Contains("JunctionSnapped", reconciledBranch.Reconciliation.Actions);
        Assert.Equal(1, reconciledBranch.Reconciliation.JunctionSnapCount);
        Assert.True(solutions.Reconciliation.ExtendedEndpointCount > 0);
        Assert.True(solutions.Reconciliation.JunctionSnappedEndpointCount > 0);

        var reconciledHost = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            IsHorizontal(run.CenterLine)));
        var junction = Assert.Single(reconciledHost.InlineJunctions);
        var branchJunctionNodeId =
            Math.Abs(reconciledBranch.CenterLine.Start.Y - 100)
            <= Math.Abs(reconciledBranch.CenterLine.End.Y - 100)
                ? reconciledBranch.FromNodeId
                : reconciledBranch.ToNodeId;
        Assert.Equal(2, solutions.SelectedWallRuns.Count);
        Assert.Equal(300, reconciledHost.DrawingLength, 6);
        Assert.Equal(branchJunctionNodeId, junction.NodeId);
        Assert.Equal("TJunction", junction.Kind);
        Assert.Equal(0.5, junction.Parameter, 6);
        Assert.Equal(150, junction.OffsetDrawingUnits, 6);
        Assert.Equal(0, junction.ProjectionResidualDrawingUnits, 6);
        Assert.Equal(
            new[] { reconciledBranch.Id, reconciledHost.Id }.Order(StringComparer.Ordinal),
            junction.IncidentWallRunIds.Order(StringComparer.Ordinal));
        Assert.True(junction.Optimization.EndpointAnchored);
        Assert.True(junction.Optimization.Converged);
        Assert.Equal(
            GlobalWallSolutionBuilder.TopologyOptimizerVersion,
            junction.Optimization.OptimizerVersion);
        Assert.Equal(1, solutions.Topology.JunctionNodeCount);
        Assert.Equal(1, solutions.Topology.InlineJunctionReferenceCount);
        Assert.Equal(1, solutions.Topology.TJunctionNodeCount);
        Assert.Equal(0, solutions.Topology.CrossingNodeCount);

        var structure = PlanStructureExport.From(placement with
        {
            Walls = [host, branch],
            Rooms = [room],
            Openings = Array.Empty<PlacementOpeningExport>(),
            WallGraph = graph,
            WallSolutions = solutions
        });
        var structureJunction = Assert.Single(structure.Nodes.Where(node =>
            string.Equals(node.Id, junction.NodeId, StringComparison.Ordinal)));
        Assert.Equal("Junction", structureJunction.Kind);
        Assert.Equal("TJunction", structureJunction.TopologyKind);
        Assert.Equal(3, structureJunction.Degree);
        Assert.Equal([reconciledBranch.Id], structureJunction.EndpointWallRunIds);
        Assert.Equal([reconciledHost.Id], structureJunction.InlineWallRunIds);
        Assert.Equal(1, structure.Summary.ConnectedComponentCount);
        Assert.Equal(1, structure.Summary.InlineJunctionReferenceCount);
        Assert.DoesNotContain(
            PlanStructureValidator.Validate(structure),
            message => string.Equals(message.Severity, "Error", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reconciler_ExtendsStructuralCoreBranchWithoutSemanticFallback()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var host = HostWallFragment(
            templateWall,
            "structural-junction-host",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(400, 100)));
        var branch = HostWallFragment(
            templateWall,
            "structural-junction-branch",
            new LineExport(
                new PointExport(250, 105),
                new PointExport(250, 300)));
        var structural = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [host, branch]);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [host, branch],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structural);

        var reconciledBranch = Assert.Single(
            solutions.SelectedWallRuns.Where(run =>
                IsVertical(run.CenterLine)));
        Assert.Equal(
            100,
            Math.Min(
                reconciledBranch.CenterLine.Start.Y,
                reconciledBranch.CenterLine.End.Y),
            6);
        Assert.Contains(
            "ExtendedStart",
            reconciledBranch.Reconciliation.Actions);
        Assert.Contains(
            "JunctionSnapped",
            reconciledBranch.Reconciliation.Actions);
        Assert.Equal(
            1,
            reconciledBranch.Reconciliation.JunctionSnapCount);
        Assert.Contains(
            "StructuralCore",
            reconciledBranch.CandidateOrigins);
        Assert.Equal(1, solutions.Topology.TJunctionNodeCount);
    }

    [Fact]
    public async Task Reconciler_NormalizesSharedMainStructuralWallBodyContact()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var host = HostWallFragment(
            templateWall,
            "body-contact-host",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(400, 100))) with
        {
            ThicknessDrawingUnits = 16,
            WallComponentId = "component:main",
            WallComponentKind = "MainStructural"
        };
        var branch = HostWallFragment(
            templateWall,
            "body-contact-branch",
            new LineExport(
                new PointExport(250, 109),
                new PointExport(250, 300))) with
        {
            ThicknessDrawingUnits = 2,
            WallComponentId = "component:main",
            WallComponentKind = "MainStructural"
        };
        var structural = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [host, branch]);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [host, branch],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structural);

        var reconciledBranch = Assert.Single(
            solutions.SelectedWallRuns.Where(run =>
                run.SourceWallIds.Contains(branch.Id, StringComparer.Ordinal)));
        Assert.Equal(
            100,
            Math.Min(
                reconciledBranch.CenterLine.Start.Y,
                reconciledBranch.CenterLine.End.Y),
            6);
        Assert.Contains(
            reconciledBranch.Reconciliation.Evidence,
            evidence => evidence.Contains(
                "source-backed wall-body contact",
                StringComparison.Ordinal));
        Assert.Equal(1, reconciledBranch.Reconciliation.JunctionSnapCount);
        Assert.Equal(1, solutions.Topology.TJunctionNodeCount);
    }

    [Fact]
    public async Task Reconciler_DoesNotNormalizeWallBodyContactAcrossComponents()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var host = HostWallFragment(
            templateWall,
            "cross-component-host",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(400, 100))) with
        {
            ThicknessDrawingUnits = 16,
            WallComponentId = "component:host",
            WallComponentKind = "MainStructural"
        };
        var branch = HostWallFragment(
            templateWall,
            "cross-component-branch",
            new LineExport(
                new PointExport(250, 109),
                new PointExport(250, 300))) with
        {
            ThicknessDrawingUnits = 2,
            WallComponentId = "component:branch",
            WallComponentKind = "MainStructural"
        };
        var structural = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [host, branch]);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [host, branch],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structural);

        var retainedBranch = Assert.Single(
            solutions.SelectedWallRuns.Where(run =>
                run.SourceWallIds.Contains(branch.Id, StringComparer.Ordinal)));
        Assert.Equal(
            109,
            Math.Min(
                retainedBranch.CenterLine.Start.Y,
                retainedBranch.CenterLine.End.Y),
            6);
        Assert.Equal(0, retainedBranch.Reconciliation.JunctionSnapCount);
    }

    [Fact]
    public async Task Reconciler_DoesNotExtendAlreadySupportedEndpointToFartherWallAxis()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var thickHost = HostWallFragment(
            templateWall,
            "supported-endpoint-thick-host",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(400, 100))) with
        {
            ThicknessDrawingUnits = 16,
            WallComponentId = "component:main",
            WallComponentKind = "MainStructural"
        };
        var endpointHost = HostWallFragment(
            templateWall,
            "supported-endpoint-host",
            new LineExport(
                new PointExport(100, 109),
                new PointExport(250, 109))) with
        {
            ThicknessDrawingUnits = 2,
            WallComponentId = "component:main",
            WallComponentKind = "MainStructural"
        };
        var branch = HostWallFragment(
            templateWall,
            "supported-endpoint-branch",
            new LineExport(
                new PointExport(250, 109),
                new PointExport(250, 300))) with
        {
            ThicknessDrawingUnits = 2,
            WallComponentId = "component:main",
            WallComponentKind = "MainStructural"
        };
        var structural = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [thickHost, endpointHost, branch]);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [thickHost, endpointHost, branch],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            EmptyGraph(placement.WallGraph),
            structural);

        var retainedBranch = Assert.Single(
            solutions.SelectedWallRuns.Where(run =>
                run.SourceWallIds.Contains(branch.Id, StringComparer.Ordinal)));
        Assert.Equal(
            109,
            Math.Min(
                retainedBranch.CenterLine.Start.Y,
                retainedBranch.CenterLine.End.Y),
            6);
        Assert.DoesNotContain(
            retainedBranch.Reconciliation.Evidence,
            evidence => evidence.Contains(
                "source-backed wall-body contact",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(250, false)]
    [InlineData(268.5, true)]
    public async Task Reconciler_OnlyNormalizesOpeningContactAtJambEdge(
        double branchX,
        bool expectedJambConnection)
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var host = HostWallFragment(
            templateWall,
            "opening-contact-host",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(400, 100))) with
        {
            ThicknessDrawingUnits = 16,
            WallComponentId = "component:main",
            WallComponentKind = "MainStructural"
        };
        var branch = HostWallFragment(
            templateWall,
            "opening-contact-branch",
            new LineExport(
                new PointExport(branchX, 109),
                new PointExport(branchX, 300))) with
        {
            ThicknessDrawingUnits = 2,
            WallComponentId = "component:main",
            WallComponentKind = "MainStructural"
        };
        var opening = AnchoredOpening(
            "body-contact-opening",
            host.Id,
            host.Id);
        var structural = StructuralSolutionForWalls(
            result.StructuralPlanSolution,
            [host, branch]);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [host, branch],
            Array.Empty<PlacementRoomExport>(),
            [opening],
            EmptyGraph(placement.WallGraph),
            structural);

        var reconciledBranch = Assert.Single(
            solutions.SelectedWallRuns.Where(run =>
                run.SourceWallIds.Contains(branch.Id, StringComparer.Ordinal)));
        Assert.Equal(
            expectedJambConnection ? 100 : 109,
            Math.Min(
                reconciledBranch.CenterLine.Start.Y,
                reconciledBranch.CenterLine.End.Y),
            6);
        Assert.Equal(
            expectedJambConnection ? 1 : 0,
            reconciledBranch.Reconciliation.JunctionSnapCount);
    }

    [Fact]
    public async Task Reconciler_TrimsShortExteriorOverrunToSupportedCorner()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var vertical = HostWallFragment(
            templateWall,
            "corner-overrun-vertical",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(100, 314))) with
        {
            WallType = "Exterior"
        };
        var horizontal = HostWallFragment(
            templateWall,
            "corner-host-horizontal",
            new LineExport(
                new PointExport(100, 300),
                new PointExport(400, 300))) with
        {
            WallType = "Exterior"
        };
        var graph = EmptyGraph(placement.WallGraph);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [vertical, horizontal],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            graph);

        var reconciledVertical = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            IsVertical(run.CenterLine)));
        Assert.Equal(300, Math.Max(
            reconciledVertical.CenterLine.Start.Y,
            reconciledVertical.CenterLine.End.Y), 6);
        Assert.Contains("TrimmedEnd", reconciledVertical.Reconciliation.Actions);
        Assert.Contains("JunctionSnapped", reconciledVertical.Reconciliation.Actions);
        Assert.Equal(1, reconciledVertical.Reconciliation.JunctionSnapCount);
        Assert.Equal(1, solutions.Reconciliation.TrimmedEndpointCount);
        Assert.Equal(1, solutions.Reconciliation.JunctionSnappedEndpointCount);
        Assert.Equal(0, solutions.Topology.InlineJunctionReferenceCount);

        var structure = PlanStructureExport.From(placement with
        {
            Walls = [vertical, horizontal],
            Rooms = Array.Empty<PlacementRoomExport>(),
            Openings = Array.Empty<PlacementOpeningExport>(),
            WallGraph = graph,
            WallSolutions = solutions
        });
        var corner = Assert.Single(structure.Nodes.Where(node =>
            string.Equals(node.TopologyKind, "Corner", StringComparison.Ordinal)));
        Assert.Equal(2, corner.Degree);
        Assert.Equal(1, structure.Summary.ConnectedComponentCount);
    }

    [Fact]
    public async Task Reconciler_TrimsInteriorOverrunWhenBodyRoomAndJunctionAgree()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var overextended = HostWallFragment(
            templateWall,
            "interior-overrun",
            new LineExport(
                new PointExport(70, 100),
                new PointExport(300, 100)));
        var corroboratingBody = HostWallFragment(
            templateWall,
            "interior-supported-body",
            new LineExport(
                new PointExport(100, 101.5),
                new PointExport(300, 101.5))) with
        {
            Confidence = 0.88,
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: false,
                ReadyForMetricPlacement: false,
                RequiresReview: true,
                Confidence: 0.88,
                Reasons: ["duplicate fragment retained as endpoint evidence"]),
            SourceLayers = ["(unlayered)"],
            Evidence =
            [
                "merged collinear wall fragments",
                "duplicate wall-face line",
                "room boundary"
            ]
        };
        var perpendicularHost = HostWallFragment(
            templateWall,
            "interior-perpendicular-host",
            new LineExport(
                new PointExport(100, 40),
                new PointExport(100, 220)));
        var room = RoomBoundaryWithHorizontalSpan(
            placement.Rooms.First(),
            "interior-overrun-room",
            [overextended.Id, corroboratingBody.Id, perpendicularHost.Id],
            startX: 100,
            endX: 300,
            axis: 100);
        var graph = EmptyGraph(placement.WallGraph);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [overextended, corroboratingBody, perpendicularHost],
            [room],
            Array.Empty<PlacementOpeningExport>(),
            graph);

        var reconciled = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            IsHorizontal(run.CenterLine)));
        var minimumX = Math.Min(
            reconciled.CenterLine.Start.X,
            reconciled.CenterLine.End.X);
        Assert.True(
            Math.Abs(minimumX - 100) <= 0.000001,
            $"Expected supported start 100, found {minimumX:0.###}. "
            + $"Actions: {string.Join(",", reconciled.Reconciliation.Actions)}. "
            + $"Evidence: {string.Join(" | ", reconciled.Reconciliation.Evidence)}");
        Assert.Equal(300, Math.Max(
            reconciled.CenterLine.Start.X,
            reconciled.CenterLine.End.X), 6);
        Assert.Contains("TrimmedStart", reconciled.Reconciliation.Actions);
        Assert.Contains(
            reconciled.Reconciliation.Evidence,
            evidence => evidence.Contains(
                "unsupported interior overrun",
                StringComparison.Ordinal));
        Assert.Equal(1, solutions.Reconciliation.TrimmedEndpointCount);
    }

    [Fact]
    public async Task Reconciler_PreservesInteriorContinuationWithSemanticEndpointSupport()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var continuation = HostWallFragment(
            templateWall,
            "interior-supported-continuation",
            new LineExport(
                new PointExport(70, 100),
                new PointExport(300, 100)));
        var perpendicularBranch = HostWallFragment(
            templateWall,
            "interior-t-branch",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(100, 220)));
        var room = RoomBoundaryWithHorizontalSpan(
            placement.Rooms.First(),
            "interior-continuation-room",
            [continuation.Id, perpendicularBranch.Id],
            startX: 70,
            endX: 300,
            axis: 100);
        var graph = EmptyGraph(placement.WallGraph);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [continuation, perpendicularBranch],
            [room],
            Array.Empty<PlacementOpeningExport>(),
            graph);

        var reconciled = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            IsHorizontal(run.CenterLine)));
        Assert.Equal(70, Math.Min(
            reconciled.CenterLine.Start.X,
            reconciled.CenterLine.End.X), 6);
        Assert.DoesNotContain(
            "TrimmedStart",
            reconciled.Reconciliation.Actions);
        var inlineJunction = Assert.Single(reconciled.InlineJunctions);
        Assert.Equal("TJunction", inlineJunction.Kind);
        Assert.Equal(100, inlineJunction.NodePosition.X, 6);
    }

    [Fact]
    public async Task Reconciler_PreservesLongExteriorContinuationPastTJunction()
    {
        var placement = PlanPlacementExport.From(await CreateScanResultAsync());
        var templateWall = placement.Walls.First(wall =>
            wall.Reliability.ReadyForCoordinatePlacement);
        var vertical = HostWallFragment(
            templateWall,
            "long-exterior-continuation",
            new LineExport(
                new PointExport(100, 100),
                new PointExport(100, 360))) with
        {
            WallType = "Exterior"
        };
        var horizontal = HostWallFragment(
            templateWall,
            "exterior-t-branch",
            new LineExport(
                new PointExport(100, 300),
                new PointExport(400, 300))) with
        {
            WallType = "Exterior"
        };
        var graph = EmptyGraph(placement.WallGraph);

        var solutions = GlobalWallSolutionBuilder.From(
            placement.Pages,
            [vertical, horizontal],
            Array.Empty<PlacementRoomExport>(),
            Array.Empty<PlacementOpeningExport>(),
            graph);

        var reconciledVertical = Assert.Single(solutions.SelectedWallRuns.Where(run =>
            IsVertical(run.CenterLine)));
        Assert.Equal(360, Math.Max(
            reconciledVertical.CenterLine.Start.Y,
            reconciledVertical.CenterLine.End.Y), 6);
        Assert.DoesNotContain("TrimmedEnd", reconciledVertical.Reconciliation.Actions);
        var inlineJunction = Assert.Single(reconciledVertical.InlineJunctions);
        Assert.Equal("TJunction", inlineJunction.Kind);
        Assert.Equal(300, inlineJunction.NodePosition.Y, 6);
    }

    [Fact]
    public async Task Structure_UsesSelectedSolverRunsAsCanonicalWalls()
    {
        var result = await CreateScanResultAsync();
        var placement = PlanPlacementExport.From(result);
        var structure = PlanStructureExport.From(result);

        Assert.Equal(placement.WallSolutions.SelectedWallRunCount, structure.WallRuns.Count);
        Assert.Equal(placement.WallSolutions.SelectedHypothesisId, structure.WallSolver.SelectedHypothesisId);
        Assert.Equal(placement.WallSolutions.SelectedProfile, structure.WallSolver.SelectedProfile);
        Assert.Equal(placement.WallSolutions.SelectedScore, structure.WallSolver.SelectedScore);
        Assert.Equal(
            placement.WallSolutions.SelectedWallRuns.Select(run => run.CenterLine),
            structure.WallRuns.Select(run => run.CenterLine));
    }

    private static IEnumerable<(PlacementSolvedWallRunExport First, PlacementSolvedWallRunExport Second)>
        EquivalentRunPairs(IReadOnlyList<PlacementSolvedWallRunExport> runs)
    {
        for (var first = 0; first < runs.Count; first++)
        {
            for (var second = first + 1; second < runs.Count; second++)
            {
                if (runs[first].PageNumber == runs[second].PageNumber
                    && SameOrientation(runs[first].CenterLine, runs[second].CenterLine)
                    && AxisDistance(runs[first].CenterLine, runs[second].CenterLine) <= 1.0)
                {
                    yield return (runs[first], runs[second]);
                }
            }
        }
    }

    private static IEnumerable<(PlacementSolvedWallRunExport First, PlacementSolvedWallRunExport Second)>
        CompetingSourceRunPairs(IReadOnlyList<PlacementSolvedWallRunExport> runs)
    {
        for (var first = 0; first < runs.Count; first++)
        {
            for (var second = first + 1; second < runs.Count; second++)
            {
                if (runs[first].PageNumber != runs[second].PageNumber
                    || !SameOrientation(runs[first].CenterLine, runs[second].CenterLine)
                    || !runs[first].SourceWallIds.Intersect(
                        runs[second].SourceWallIds,
                        StringComparer.Ordinal).Any())
                {
                    continue;
                }

                yield return (runs[first], runs[second]);
            }
        }
    }

    private static double CollinearOverlapRatio(LineExport first, LineExport second)
    {
        var horizontal = Math.Abs(first.End.X - first.Start.X) >= Math.Abs(first.End.Y - first.Start.Y);
        var firstStart = horizontal ? Math.Min(first.Start.X, first.End.X) : Math.Min(first.Start.Y, first.End.Y);
        var firstEnd = horizontal ? Math.Max(first.Start.X, first.End.X) : Math.Max(first.Start.Y, first.End.Y);
        var secondStart = horizontal ? Math.Min(second.Start.X, second.End.X) : Math.Min(second.Start.Y, second.End.Y);
        var secondEnd = horizontal ? Math.Max(second.Start.X, second.End.X) : Math.Max(second.Start.Y, second.End.Y);
        var overlap = Math.Max(0, Math.Min(firstEnd, secondEnd) - Math.Max(firstStart, secondStart));
        return overlap / Math.Max(0.001, Math.Min(firstEnd - firstStart, secondEnd - secondStart));
    }

    private static bool SameOrientation(LineExport first, LineExport second) =>
        IsHorizontal(first) == IsHorizontal(second);

    private static double AxisDistance(LineExport first, LineExport second) =>
        IsHorizontal(first)
            ? Math.Abs(((first.Start.Y + first.End.Y) / 2.0) - ((second.Start.Y + second.End.Y) / 2.0))
            : Math.Abs(((first.Start.X + first.End.X) / 2.0) - ((second.Start.X + second.End.X) / 2.0));

    private static bool IsHorizontal(LineExport line) =>
        Math.Abs(line.End.X - line.Start.X) >= Math.Abs(line.End.Y - line.Start.Y);

    private static bool IsVertical(LineExport line) =>
        Math.Abs(line.End.Y - line.Start.Y) > Math.Abs(line.End.X - line.Start.X);

    private static PlacementWallExport HostWallFragment(
        PlacementWallExport template,
        string id,
        LineExport centerLine) =>
        template with
        {
            Id = id,
            PageNumber = 1,
            CenterLine = centerLine,
            CenterLineMillimeters = null,
            TopologySpans = Array.Empty<PlacementWallTopologySpanExport>(),
            OpeningCutouts = Array.Empty<PlacementWallOpeningCutoutExport>(),
            SolidSpans = Array.Empty<PlacementWallSolidSpanExport>(),
            Bounds = new RectExport(
                Math.Min(centerLine.Start.X, centerLine.End.X) - 5,
                Math.Min(centerLine.Start.Y, centerLine.End.Y) - 5,
                Math.Abs(centerLine.End.X - centerLine.Start.X) + 10,
                Math.Abs(centerLine.End.Y - centerLine.Start.Y) + 10),
            BoundsMillimeters = null,
            DrawingLength = Math.Sqrt(
                Math.Pow(centerLine.End.X - centerLine.Start.X, 2)
                + Math.Pow(centerLine.End.Y - centerLine.Start.Y, 2)),
            LengthMeters = null,
            ThicknessDrawingUnits = 10,
            ThicknessMillimeters = null,
            WallType = "Interior",
            WallComponentId = null,
            WallComponentKind = null,
            ExcludedFromStructuralTopology = false,
            MeasurementScaleGroupId = null,
            MillimetersPerDrawingUnit = null,
            Confidence = 0.95,
            FragmentEvidence = null,
            EvidenceAssessment = null,
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: true,
                ReadyForMetricPlacement: false,
                RequiresReview: false,
                Confidence: 0.95,
                Reasons: Array.Empty<string>()),
            PlacementOmission = null,
            WallGraphRepairCandidateIds = Array.Empty<string>(),
            SourcePrimitiveIds = [id],
            SourceLayers = ["Wall"],
            Evidence =
            [
                "parallel wall-face pair",
                "room boundary",
                "main structural wall body"
            ]
        };

    private static PlacementWallExport ReviewedMainStructuralBridge(
        PlacementWallExport template,
        string id,
        LineExport centerLine) =>
        HostWallFragment(template, id, centerLine) with
        {
            WallComponentId = $"{id}:component",
            WallComponentKind = "MainStructural",
            Confidence = 0.85,
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: false,
                ReadyForMetricPlacement: false,
                RequiresReview: true,
                Confidence: 0.85,
                Reasons: ["dense local detail requires review"]),
            SourceLayers = ["(unlayered)"],
            Evidence =
            [
                "parallel wall-face pair",
                "layer evidence: contains dimension-like text"
            ]
        };

    private static PlacementWallGraphEdgeExport CleanGraphEdge(
        PlacementWallExport wall,
        string id) =>
        new(
            Id: id,
            PageNumber: wall.PageNumber,
            FromNodeId: $"{id}:from",
            ToNodeId: $"{id}:to",
            WallId: wall.Id,
            WallComponentId: wall.WallComponentId,
            WallComponentKind: wall.WallComponentKind,
            ExcludedFromStructuralTopology: wall.ExcludedFromStructuralTopology,
            CenterLine: wall.CenterLine,
            CenterLineMillimeters: wall.CenterLineMillimeters,
            Bounds: wall.Bounds,
            BoundsMillimeters: wall.BoundsMillimeters,
            DrawingLength: wall.DrawingLength,
            LengthMeters: wall.LengthMeters,
            ThicknessDrawingUnits: wall.ThicknessDrawingUnits,
            ThicknessMillimeters: wall.ThicknessMillimeters,
            MillimetersPerDrawingUnit: wall.MillimetersPerDrawingUnit,
            Confidence: 0.98,
            SourcePrimitiveIds: wall.SourcePrimitiveIds,
            SourceLayers: wall.SourceLayers,
            SourceWallIds: [wall.Id],
            SourceWallGraphEdgeIds: [id],
            Evidence: wall.Evidence);

    private static PlacementWallExport DetailLoopWall(
        PlacementWallExport template,
        string id,
        double startX,
        double startY,
        double endX,
        double endY) =>
        HostWallFragment(
            template,
            id,
            new LineExport(
                new PointExport(startX, startY),
                new PointExport(endX, endY))) with
        {
            Confidence = 0.95,
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: false,
                ReadyForMetricPlacement: false,
                RequiresReview: true,
                Confidence: 0.95,
                Reasons: ["closed detail loop requires review"]),
            SourceLayers = ["Detail"],
            Evidence =
            [
                "reclassified as object/fixture detail",
                "closed detail loop is not a wall body"
            ]
        };

    private static PlacementWallExport UnknownReviewBoundary(
        PlacementWallExport template,
        string id,
        LineExport centerLine) =>
        HostWallFragment(template, id, centerLine) with
        {
            WallType = "Unknown",
            Confidence = 0.85,
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: false,
                ReadyForMetricPlacement: false,
                RequiresReview: true,
                Confidence: 0.85,
                Reasons: ["dimension-like unknown source requires contextual review"]),
            SourceLayers = ["(unlayered)"],
            Evidence =
            [
                "parallel wall-face pair",
                "room boundary",
                "layer evidence: contains dimension-like text"
            ]
        };

    private static PlacementOpeningExport AnchoredOpening(
        string id,
        string leftWallId,
        string rightWallId)
    {
        var referenceLine = new LineExport(
            new PointExport(100, 120),
            new PointExport(400, 120));
        var startPoint = new PointExport(230, 120);
        var endPoint = new PointExport(270, 120);
        var placement = new OpeningPlacementExport(
            HostWallId: leftWallId,
            AnchorWallIds: [leftWallId, rightWallId],
            ReferenceLine: referenceLine,
            ReferenceLineMillimeters: null,
            StartPoint: startPoint,
            StartPointMillimeters: null,
            EndPoint: endPoint,
            EndPointMillimeters: null,
            StartOffsetDrawingUnits: 130,
            EndOffsetDrawingUnits: 170,
            CenterOffsetDrawingUnits: 150,
            LengthDrawingUnits: 40,
            FootprintBounds: new RectExport(230, 115, 40, 10),
            FootprintBoundsMillimeters: null,
            FootprintCorners:
            [
                new PointExport(230, 115),
                new PointExport(270, 115),
                new PointExport(270, 125),
                new PointExport(230, 125)
            ],
            FootprintCornersMillimeters: null,
            StartJambLine: new LineExport(new PointExport(230, 115), new PointExport(230, 125)),
            StartJambLineMillimeters: null,
            EndJambLine: new LineExport(new PointExport(270, 115), new PointExport(270, 125)),
            EndJambLineMillimeters: null,
            DepthDrawingUnits: 10,
            DepthMillimeters: null,
            StartOffsetMillimeters: null,
            EndOffsetMillimeters: null,
            CenterOffsetMillimeters: null,
            LengthMillimeters: null,
            HostWallStartParameter: 130.0 / 300.0,
            HostWallEndParameter: 170.0 / 300.0,
            HostWallCenterParameter: 0.5,
            AlongVector: new VectorExport(1, 0),
            NormalVector: new VectorExport(0, 1),
            CrossWallOffsetDrawingUnits: 0,
            CrossWallOffsetMillimeters: null,
            Confidence: 0.95,
            Evidence: ["anchored opening spans two host wall fragments"]);
        return new PlacementOpeningExport(
            Id: id,
            PageNumber: 1,
            Type: "Door",
            Operation: "Hinged",
            Orientation: "Horizontal",
            CenterLine: new LineExport(startPoint, endPoint),
            CenterLineMillimeters: null,
            Bounds: new RectExport(230, 115, 40, 10),
            BoundsMillimeters: null,
            DrawingWidth: 40,
            WidthMillimeters: null,
            MeasurementScaleGroupId: null,
            MillimetersPerDrawingUnit: null,
            PlacementStatus: "Anchored",
            Placement: placement,
            HingeSide: "Left",
            SwingSide: "Unknown",
            SwingDirection: "Unknown",
            HingePoint: startPoint,
            HingePointMillimeters: null,
            HostWallIds: [leftWallId, rightWallId],
            ConnectedRoomIds: Array.Empty<string>(),
            ConnectedRoomLabels: Array.Empty<string>(),
            ConnectedRoomLinks: Array.Empty<OpeningRoomConnectionExport>(),
            RoomAdjacencyIds: Array.Empty<string>(),
            Confidence: 0.95,
            Reliability: new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: true,
                ReadyForMetricPlacement: false,
                RequiresReview: false,
                Confidence: 0.95,
                Reasons: Array.Empty<string>()),
            SourcePrimitiveIds: [id],
            SourceLayers: ["Door"],
            Evidence: ["door opening explains the wall gap"]);
    }

    private static PlacementWallGraphExport EmptyGraph(PlacementWallGraphExport graph) =>
        graph with
        {
            Nodes = Array.Empty<PlacementWallGraphNodeExport>(),
            Edges = Array.Empty<PlacementWallGraphEdgeExport>(),
            Components = Array.Empty<PlacementWallGraphComponentExport>(),
            RepairCandidateIds = Array.Empty<string>(),
            ResidualEndpointOnHostCandidates =
                Array.Empty<PlacementWallGraphResidualEndpointOnHostCandidateExport>()
        };

    private static PlacementWallHypothesisMetricsExport HypothesisMetrics(
        double major,
        double longCoverage,
        double endpoint,
        double roomClosure,
        double duplicate,
        double review,
        double noise,
        double selectedLength) =>
        new(
            major,
            longCoverage,
            endpoint,
            roomClosure,
            ExteriorContinuityRatio: 0.80,
            duplicate,
            review,
            noise,
            AverageConfidence: 0.85,
            selectedLength,
            UnsupportedEndpointCount: 0,
            ClosedRoomCount: roomClosure >= 0.90 ? 1 : 0,
            EvaluatedRoomCount: 1);

    private static StructuralPlanSolution StructuralSolutionForWalls(
        StructuralPlanSolution template,
        IReadOnlyList<PlacementWallExport> walls)
    {
        var runTemplate = template.WallRuns.First();
        var runs = walls
            .Select((wall, index) => runTemplate with
            {
                Id = $"test-structural-run-{index + 1}",
                PageNumber = wall.PageNumber,
                CenterLine = new PlanLineSegment(
                    new PlanPoint(wall.CenterLine.Start.X, wall.CenterLine.Start.Y),
                    new PlanPoint(wall.CenterLine.End.X, wall.CenterLine.End.Y)),
                Thickness = wall.ThicknessDrawingUnits,
                WallType = WallType.Interior,
                CandidateIds = [$"test-structural-candidate-{index + 1}"],
                SourceWallIds = [wall.Id],
                SourceWallGraphEdgeIds = Array.Empty<string>(),
                SourcePrimitiveIds = wall.SourcePrimitiveIds,
                SourceRoomIds = Array.Empty<string>(),
                SourceOpeningIds = Array.Empty<string>(),
                Evidence = wall.Evidence
                    .Append("test canonical structural wall")
                    .ToArray(),
                Reliability = new StructuralWallRunReliability(
                    ReadyForCoordinatePlacement: true,
                    RequiresReview: false,
                    Confidence: 0.95,
                    Reasons: ["test canonical structural wall is coordinate ready"])
            })
            .ToArray();
        return template with
        {
            WallRuns = runs,
            Metrics = template.Metrics with
            {
                SelectedCandidateCount = runs.Length,
                CanonicalWallRunCount = runs.Length
            }
        };
    }

    private static PlacementRoomExport RoomBoundaryAtAxis(
        PlacementRoomExport template,
        string wallId,
        double axis,
        bool sourceLinked) =>
        RoomBoundaryWithHorizontalSpan(
            template,
            $"room-axis-{axis:0.###}",
            sourceLinked ? [wallId] : ["unrelated-wall"],
            startX: 100,
            endX: 400,
            axis: axis);

    private static PlacementRoomExport RoomBoundaryWithHorizontalSpan(
        PlacementRoomExport template,
        string id,
        IReadOnlyList<string> wallIds,
        double startX,
        double endX,
        double axis) =>
        RoomBoundaryWithHorizontalSpanBetween(
            template,
            id,
            wallIds,
            startX,
            endX,
            top: axis,
            bottom: axis + 120);

    private static PlacementRoomExport RoomBoundaryWithHorizontalSpanBetween(
        PlacementRoomExport template,
        string id,
        IReadOnlyList<string> wallIds,
        double startX,
        double endX,
        double top,
        double bottom) =>
        template with
        {
            Id = id,
            PageNumber = 1,
            Bounds = new RectExport(startX, top, endX - startX, bottom - top),
            BoundsMillimeters = null,
            Center = new PointExport((startX + endX) / 2.0, (top + bottom) / 2.0),
            CenterMillimeters = null,
            Boundary =
            [
                new PointExport(startX, top),
                new PointExport(endX, top),
                new PointExport(endX, bottom),
                new PointExport(startX, bottom)
            ],
            BoundaryMillimeters = null,
            WallIds = wallIds,
            DrawingArea = (endX - startX) * (bottom - top),
            AreaSquareMeters = null,
            MeasurementScaleGroupId = null,
            MillimetersPerDrawingUnit = null,
            Confidence = 0.95,
            Reliability = new PlacementReliabilityExport(
                ReadyForCoordinatePlacement: true,
                ReadyForMetricPlacement: false,
                RequiresReview: false,
                Confidence: 0.95,
                Reasons: Array.Empty<string>()),
            Evidence = ["reviewed room boundary alignment evidence"]
        };

    private static PlacementOpeningExport ShiftOpeningToAxis(
        PlacementOpeningExport opening,
        double axis)
    {
        var placement = opening.Placement!;
        var start = new PointExport(placement.StartPoint.X, axis);
        var end = new PointExport(placement.EndPoint.X, axis);
        var footprintTop = axis - placement.DepthDrawingUnits / 2.0;
        var footprintBottom = axis + placement.DepthDrawingUnits / 2.0;
        var shiftedPlacement = placement with
        {
            ReferenceLine = new LineExport(
                new PointExport(placement.ReferenceLine.Start.X, axis),
                new PointExport(placement.ReferenceLine.End.X, axis)),
            StartPoint = start,
            EndPoint = end,
            FootprintBounds = new RectExport(
                Math.Min(start.X, end.X),
                footprintTop,
                Math.Abs(end.X - start.X),
                placement.DepthDrawingUnits),
            FootprintCorners =
            [
                new PointExport(start.X, footprintTop),
                new PointExport(end.X, footprintTop),
                new PointExport(end.X, footprintBottom),
                new PointExport(start.X, footprintBottom)
            ],
            StartJambLine = new LineExport(
                new PointExport(start.X, footprintTop),
                new PointExport(start.X, footprintBottom)),
            EndJambLine = new LineExport(
                new PointExport(end.X, footprintTop),
                new PointExport(end.X, footprintBottom)),
            CrossWallOffsetDrawingUnits = 0
        };

        return opening with
        {
            CenterLine = new LineExport(start, end),
            Bounds = shiftedPlacement.FootprintBounds,
            Placement = shiftedPlacement,
            HingePoint = start
        };
    }

    private static async Task<PlanScanResult> CreateScanResultAsync(double partitionInset = 0)
    {
        var document = new PlanDocument(
            "global-wall-solver-test",
            new[]
            {
                new PlanPage(
                    1,
                    new PlanSize(600, 450),
                    new PlanPrimitive[]
                    {
                        WallLine("top-a", new PlanPoint(80, 80), new PlanPoint(260, 80)),
                        WallLine("top-b", new PlanPoint(260, 80), new PlanPoint(500, 80)),
                        WallLine("right", new PlanPoint(500, 80), new PlanPoint(500, 360)),
                        WallLine("bottom-a", new PlanPoint(500, 360), new PlanPoint(300, 360)),
                        WallLine("bottom-b", new PlanPoint(300, 360), new PlanPoint(80, 360)),
                        WallLine("left", new PlanPoint(80, 360), new PlanPoint(80, 80)),
                        WallLine(
                            "partition",
                            new PlanPoint(300, 80 + partitionInset),
                            new PlanPoint(300, 360 - partitionInset)),
                        new TextPrimitive("ROOM A", new PlanRect(155, 180, 60, 18)),
                        new TextPrimitive("ROOM B", new PlanRect(385, 180, 60, 18))
                    })
            })
        {
            Metadata = new PlanMetadata
            {
                SourceName = "global-wall-solver-test.pdf",
                Properties = new Dictionary<string, string>
                {
                    ["format"] = "pdf",
                    ["loader"] = "PDF/PdfPig"
                }
            }
        };

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
