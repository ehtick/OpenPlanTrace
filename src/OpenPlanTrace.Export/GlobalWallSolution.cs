namespace OpenPlanTrace.Export;

public sealed record PlacementWallSolutionSetExport(
    string SolverVersion,
    string SelectedHypothesisId,
    string SelectedProfile,
    double SelectedScore,
    int CandidateCount,
    int SelectedCandidateCount,
    int SelectedWallRunCount,
    int IterationCount,
    IReadOnlyList<PlacementWallHypothesisExport> Hypotheses,
    IReadOnlyList<PlacementSolvedWallRunExport> SelectedWallRuns,
    IReadOnlyList<PlacementWallCandidateDecisionExport> CandidateDecisions,
    PlacementWallReconciliationSummaryExport Reconciliation,
    PlacementWallTopologyOptimizationSummaryExport Topology,
    IReadOnlyList<string> Evidence);

public sealed record PlacementWallReconciliationSummaryExport(
    string ReconcilerVersion,
    int EvaluatedWallRunCount,
    int AdjustedWallRunCount,
    int AxisAlignedWallRunCount,
    int ExtendedEndpointCount,
    int TrimmedEndpointCount,
    int JunctionSnappedEndpointCount,
    int CollapsedDuplicateWallRunCount,
    int CandidateSupportedWallRunCount,
    int RoomBoundarySupportedWallRunCount,
    int OpeningSupportedWallRunCount,
    int NeighborSupportedWallRunCount,
    int PreservedForReviewWallRunCount,
    double TotalAxisShiftDrawingUnits,
    double MaximumAxisShiftDrawingUnits,
    IReadOnlyList<string> Evidence);

public sealed record PlacementWallHypothesisExport(
    string Id,
    string Profile,
    double Score,
    bool Selected,
    int IterationCount,
    int InitialCandidateCount,
    int SelectedCandidateCount,
    int RecoveredCandidateCount,
    int RemovedCandidateCount,
    PlacementWallHypothesisMetricsExport Metrics,
    IReadOnlyList<string> SelectedCandidateIds,
    IReadOnlyList<string> Evidence);

public sealed record PlacementWallHypothesisMetricsExport(
    double MajorWallCoverageRatio,
    double LongWallCoverageRatio,
    double EndpointConnectivityRatio,
    double RoomBoundaryClosureRatio,
    double ExteriorContinuityRatio,
    double DuplicateLengthRatio,
    double ReviewLengthRatio,
    double NoiseLengthRatio,
    double AverageConfidence,
    double SelectedDrawingLength,
    int UnsupportedEndpointCount,
    int ClosedRoomCount,
    int EvaluatedRoomCount);

public sealed record PlacementWallCandidateDecisionExport(
    string CandidateId,
    int PageNumber,
    string Origin,
    string WallType,
    double DrawingLength,
    double LocalScore,
    bool MajorWallCandidate,
    bool StrongNegativeEvidence,
    int SupportedEndpointCount,
    int RoomBoundarySupportCount,
    int OpeningSupportCount,
    IReadOnlyList<string> SourceWallIds,
    IReadOnlyList<string> SelectedByHypothesisIds,
    string Decision,
    IReadOnlyList<string> Evidence);

public sealed record PlacementWallTopologyOptimizationSummaryExport(
    string OptimizerVersion,
    string Method,
    int EvaluatedWallPairCount,
    int JunctionNodeCount,
    int InlineJunctionReferenceCount,
    int TJunctionNodeCount,
    int CrossingNodeCount,
    int EndpointAnchoredNodeCount,
    int ObservationCount,
    int LineConstraintCount,
    int MaximumIterationCount,
    double RootMeanSquareResidualDrawingUnits,
    double MaximumResidualDrawingUnits,
    double RobustObjective,
    IReadOnlyList<string> Evidence);

public sealed record PlacementWallJunctionOptimizationExport(
    string OptimizerVersion,
    string Method,
    bool EndpointAnchored,
    int IterationCount,
    int ObservationCount,
    int LineConstraintCount,
    double RootMeanSquareResidualDrawingUnits,
    double MaximumResidualDrawingUnits,
    double RobustObjective,
    bool Converged,
    IReadOnlyList<string> Evidence);

public sealed record PlacementSolvedWallInlineJunctionExport(
    string Id,
    string WallRunId,
    string NodeId,
    int PageNumber,
    string Kind,
    PointExport NodePosition,
    PointExport? NodePositionMillimeters,
    PointExport WallPosition,
    PointExport? WallPositionMillimeters,
    double Parameter,
    double OffsetDrawingUnits,
    double? OffsetMillimeters,
    double ProjectionResidualDrawingUnits,
    double? ProjectionResidualMillimeters,
    IReadOnlyList<string> IncidentWallRunIds,
    double Confidence,
    bool RequiresReview,
    PlacementWallJunctionOptimizationExport Optimization,
    IReadOnlyList<string> Evidence);

public sealed record PlacementSolvedWallRunExport(
    string Id,
    int PageNumber,
    string FromNodeId,
    string ToNodeId,
    string WallType,
    LineExport CenterLine,
    LineExport? CenterLineMillimeters,
    RectExport Bounds,
    RectExport? BoundsMillimeters,
    double DrawingLength,
    double? LengthMeters,
    double ThicknessDrawingUnits,
    double? ThicknessMillimeters,
    double? MillimetersPerDrawingUnit,
    double SolidDrawingLength,
    double? SolidLengthMeters,
    double OpeningDrawingLength,
    int ReconstructedOpeningGapCount,
    double Confidence,
    PlacementReliabilityExport Reliability,
    IReadOnlyList<PlacementSolvedWallOpeningIntervalExport> OpeningIntervals,
    IReadOnlyList<PlacementSolvedWallSolidIntervalExport> SolidIntervals,
    IReadOnlyList<string> CandidateIds,
    IReadOnlyList<string> CandidateOrigins,
    IReadOnlyList<string> SourceWallIds,
    IReadOnlyList<string> SourceWallGraphEdgeIds,
    IReadOnlyList<string> SourcePrimitiveIds,
    IReadOnlyList<string> SourceLayers,
    IReadOnlyList<PlacementSolvedWallInlineJunctionExport> InlineJunctions,
    PlacementSolvedWallReconciliationExport Reconciliation,
    IReadOnlyList<string> Evidence)
{
    internal bool HasCoherentRoomBoundarySupport { get; init; }
}

public sealed record PlacementSolvedWallReconciliationExport(
    string Status,
    LineExport OriginalCenterLine,
    LineExport ReconciledCenterLine,
    double AxisShiftDrawingUnits,
    double StartEndpointDeltaDrawingUnits,
    double EndEndpointDeltaDrawingUnits,
    int CandidateVoteCount,
    int RoomBoundaryVoteCount,
    int OpeningVoteCount,
    int NeighborVoteCount,
    int JunctionSnapCount,
    int CollapsedDuplicateRunCount,
    double Confidence,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Evidence);

public sealed record PlacementSolvedWallOpeningIntervalExport(
    string Id,
    string WallRunId,
    string OpeningId,
    int PageNumber,
    string Type,
    string Operation,
    string AttachmentKind,
    LineExport CenterLine,
    LineExport? CenterLineMillimeters,
    PointExport StartPoint,
    PointExport? StartPointMillimeters,
    PointExport EndPoint,
    PointExport? EndPointMillimeters,
    double StartParameter,
    double EndParameter,
    double CenterParameter,
    double StartOffsetDrawingUnits,
    double EndOffsetDrawingUnits,
    double CenterOffsetDrawingUnits,
    double LengthDrawingUnits,
    double? StartOffsetMillimeters,
    double? EndOffsetMillimeters,
    double? CenterOffsetMillimeters,
    double? LengthMillimeters,
    double SourceOpeningWidthDrawingUnits,
    double? SourceOpeningWidthMillimeters,
    double CrossWallOffsetDrawingUnits,
    double? CrossWallOffsetMillimeters,
    bool ReadyForCoordinatePlacement,
    bool RequiresReview,
    double Confidence,
    IReadOnlyList<string> SourceHostWallIds,
    IReadOnlyList<string> SourcePrimitiveIds,
    IReadOnlyList<string> SourceLayers,
    IReadOnlyList<string> Evidence);

public sealed record PlacementSolvedWallSolidIntervalExport(
    string Id,
    string WallRunId,
    int PageNumber,
    int Sequence,
    LineExport CenterLine,
    LineExport? CenterLineMillimeters,
    IReadOnlyList<PointExport> BodyPolygon,
    IReadOnlyList<PointExport>? BodyPolygonMillimeters,
    RectExport BodyBounds,
    RectExport? BodyBoundsMillimeters,
    VectorExport AlongVector,
    VectorExport NormalVector,
    double ThicknessDrawingUnits,
    double? ThicknessMillimeters,
    double StartParameter,
    double EndParameter,
    double CenterParameter,
    double StartOffsetDrawingUnits,
    double EndOffsetDrawingUnits,
    double CenterOffsetDrawingUnits,
    double DrawingLength,
    double? LengthMeters,
    bool ReadyForCoordinatePlacement,
    bool ReadyForMetricPlacement,
    bool RequiresReview,
    IReadOnlyList<string> ReviewReasons,
    IReadOnlyList<string> AdjacentOpeningIds,
    IReadOnlyList<string> Evidence);

public static partial class GlobalWallSolutionBuilder
{
    public const string SolverVersion = "openplantrace.global-wall-solver.v20";

    private const double EndpointSnapDistance = 2.0;
    private const double EndpointAxisEqualityTolerance = 0.000001;
    private const double EndpointSupportDistance = 4.0;
    private const double AxisGroupingDistance = 3.0;
    private const double DuplicateAxisDistance = 1.0;
    private const double CompetingSourceRunDistance = 12.0;
    private const double IntervalMergeGap = 3.0;
    private const double JunctionCompletionDistance = 5.0;
    private const double JunctionProjectionTolerance = 2.0;
    private const double MaximumBodyContactJunctionDistance = 12.0;
    private const double OpeningJambJunctionTolerance = 2.5;
    private const double MajorWallLength = 80.0;
    private const double LongWallLength = 150.0;
    private const double MinimumCoherentBoundaryScore = 0.50;
    private const double MinimumCoherentBoundaryLength = 80.0;
    private const double MaximumConsensusRecallCoverage = 0.85;
    private const double MinimumConsensusRecallScore = 0.75;
    private const double MinimumConsensusSourceBackedRecallScore = 0.90;
    private const double MinimumConsensusRecallUncoveredLength = 24.0;
    private const double MinimumConsensusRecallUncoveredThicknessMultiple = 4.0;
    private const double MinimumStructuralBridgeRecallScore = 0.44;
    private const int MaximumStructuralBridgeRecallPassCount = 2;
    private const double MinimumMajorRecallGain = 0.010;
    private const double MinimumStructuralOverrideScoreGain = 0.006;
    private const double MinimumStructuralOverrideNoiseReduction = 0.010;
    private const double MinimumStructuralOverrideReviewReduction = 0.015;
    private const double MinimumDecisiveRecallScoreGain = 0.040;
    private const double MinimumDecisiveMajorRecallGain = 0.080;
    private const double MinimumDecisiveLongRecallGain = 0.080;
    private const double MinimumDecisiveAbsoluteRecall = 0.900;
    private const double MaximumDecisiveEndpointConnectivityLoss = 0.030;
    private const double MaximumDecisiveRoomClosureLoss = 0.300;
    private const double MaximumDecisiveDuplicateRatio = 0.040;
    private const double MaximumDecisiveNoiseRatio = 0.030;
    private const double MaximumDecisiveReviewRatio = 0.150;
    private const double MaximumRecallScoreRegression = 0.025;
    private const double MaximumStructuralOverrideMajorCoverageLoss = 0.015;
    private const double MaximumStructuralOverrideLongCoverageLoss = 0.006;
    private const double MaximumStructuralOverrideRoomClosureLoss = 0.150;
    private const double MaximumStructuralOverrideDuplicateRatio = 0.080;
    private const double MaximumStructuralOverrideNoiseRatio = 0.080;
    private const double MaximumStructuralOverrideReviewRatio = 0.080;
    private const double MaximumStructuralOverrideReviewRatioGrowth = 0.020;
    private const double MaximumStructuralOverrideLengthGrowth = 1.35;
    private const double MinimumCredibleUnknownRoomAreaSquareMeters = 1.5;
    private const double MinimumSemanticRoomAreaMatchRatio = 0.50;
    private const double MaximumSemanticRoomAreaMatchRatio = 1.45;

    private static readonly WallSolverProfile[] Profiles =
    [
        new(
            "conservative",
            InitialGraphThreshold: 0.60,
            InitialRecoveredThreshold: 0.75,
            MinimumConsiderScore: 0.68,
            AllowedObjectiveRegressionForMajorRecall: 0.002),
        new(
            "balanced",
            InitialGraphThreshold: 0.48,
            InitialRecoveredThreshold: 0.62,
            MinimumConsiderScore: 0.55,
            AllowedObjectiveRegressionForMajorRecall: 0.010),
        new(
            "recall-first",
            InitialGraphThreshold: 0.36,
            InitialRecoveredThreshold: 0.50,
            MinimumConsiderScore: 0.44,
            AllowedObjectiveRegressionForMajorRecall: 0.025)
    ];

    private static readonly string[] StrongNegativeTerms =
    [
        "wall evidence rejected as non-wall/noise",
        "reclassified as object/fixture detail",
        "overlaps non-structural surface/detail pattern",
        "door swing linework",
        "door leaf linework",
        "door arc linework",
        "follows door swing symbol",
        "witness/extension line",
        "repeated short detail linework",
        "inferred repeated detail family",
        "declared dense repeated",
        "declared dense orthogonal",
        "railing detail",
        "stair tread linework",
        "unsupported geometry or context-only negative evidence",
        "unsupported oblique single-line geometry",
        "excluded, unanchored wall-graph island",
        "belongs only to excluded",
        "outdoor or conflicted room context cannot promote",
        "objectorfixturedetail",
        "opening-clearance rectangle",
        "parallel offset detail shadow",
        "shadowed by stronger parallel wall",
        "rejected as noise"
    ];

    private static readonly string[] WeakNegativeTerms =
    [
        "dimension-like",
        "witness/extension",
        "surface/detail pattern",
        "surface pattern",
        "repeated short detail",
        "fixture detail",
        "object/fixture",
        "railing",
        "stair tread",
        "dense detail"
    ];

    private static readonly string[] StructuralPositiveTerms =
    [
        "parallel wall-face pair",
        "strong parallel-face wall pair",
        "filled wall-solid primitive",
        "filled closed vector wall body",
        "room boundary",
        "exterior shell",
        "main structural",
        "structural context",
        "opening linked",
        "global envelope",
        "global-room-envelope-edge"
    ];

    private static readonly IReadOnlySet<StructuralEvidenceSignalKind>
        StructuralRejectionSignalKinds =
            new HashSet<StructuralEvidenceSignalKind>
            {
                StructuralEvidenceSignalKind.DoorOrOpeningSymbol,
                StructuralEvidenceSignalKind.SurfacePattern,
                StructuralEvidenceSignalKind.RepeatedDetailPattern,
                StructuralEvidenceSignalKind.DimensionOrAnnotation,
                StructuralEvidenceSignalKind.ObjectOrFixture,
                StructuralEvidenceSignalKind.ContextOnlyBoundary,
                StructuralEvidenceSignalKind.UnsupportedOblique,
                StructuralEvidenceSignalKind.IsolatedStructuralIsland,
                StructuralEvidenceSignalKind.UnoccupiedShellExtension,
                StructuralEvidenceSignalKind.WallBodyThicknessOutlier
            };

    public static PlacementWallSolutionSetExport From(
        IReadOnlyList<PlacementPageExport> pages,
        IReadOnlyList<PlacementWallExport> walls,
        IReadOnlyList<PlacementRoomExport> rooms,
        IReadOnlyList<PlacementOpeningExport> openings,
        PlacementWallGraphExport wallGraph) =>
        From(
            pages,
            walls,
            rooms,
            openings,
            wallGraph,
            structuralSolution: null);

    public static PlacementWallSolutionSetExport From(
        IReadOnlyList<PlacementPageExport> pages,
        IReadOnlyList<PlacementWallExport> walls,
        IReadOnlyList<PlacementRoomExport> rooms,
        IReadOnlyList<PlacementOpeningExport> openings,
        PlacementWallGraphExport wallGraph,
        StructuralPlanSolution? structuralSolution)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(walls);
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(openings);
        ArgumentNullException.ThrowIfNull(wallGraph);

        var cleanTopologyRepresentations = BuildCleanTopologyRepresentations(walls);
        var legacyRawCandidates = DeduplicateCandidates(
            ConsolidateCleanTopologyRepresentations(
                ApplyStructuralRejectionEvidence(
                    DeduplicateCandidates(BuildCandidatePool(walls, wallGraph)),
                    structuralSolution),
                cleanTopologyRepresentations));
        var structuralRawCandidates = DeduplicateCandidates(
            ConsolidateCleanTopologyRepresentations(
                ApplyStructuralRejectionEvidence(
                    DeduplicateCandidates(BuildStructuralSolutionCandidates(
                        structuralSolution,
                        walls)),
                    structuralSolution),
                cleanTopologyRepresentations));
        var rawCandidates = legacyRawCandidates
            .Concat(structuralRawCandidates)
            .ToArray();
        var candidates = rawCandidates
            .Select(candidate => AddContext(candidate, rawCandidates, rooms, openings))
            .OrderBy(candidate => candidate.PageNumber)
            .ThenBy(candidate => candidate.Bounds.Y)
            .ThenBy(candidate => candidate.Bounds.X)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
        var legacyCandidates = legacyRawCandidates
            .Select(candidate => AddContext(
                candidate,
                legacyRawCandidates,
                rooms,
                openings))
            .OrderBy(candidate => candidate.PageNumber)
            .ThenBy(candidate => candidate.Bounds.Y)
            .ThenBy(candidate => candidate.Bounds.X)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
        var legacySolved = Profiles
            .Select(profile => Solve(profile, legacyCandidates, rooms, openings))
            .ToArray();
        var structuralHypothesis = BuildStructuralCoreHypothesis(
            structuralSolution,
            candidates,
            rooms,
            openings,
            legacySolved);
        var solved = structuralHypothesis is null
            ? legacySolved
            : new[] { structuralHypothesis }.Concat(legacySolved).ToArray();
        var structuralPlacementIsGloballyBlocked =
            structuralSolution is not null
            && structuralSolution.WallRuns.Count > 0
            && structuralSolution.WallRuns.All(run =>
                !run.Reliability.ReadyForCoordinatePlacement
                || run.Reliability.RequiresReview);
        var selected = SelectHypothesis(
            solved,
            structuralHypothesis,
            structuralPlacementIsGloballyBlocked);
        var solvedRuns = BuildSolvedRuns(
            selected.SelectedCandidateIds
                .Select(id => candidates.First(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal)))
                .ToArray(),
            candidates,
            rooms,
            openings,
            selected.TrustedCoordinateRecoveredCandidateIds);
        var selectedRuns = solvedRuns.Runs;
        var hypothesisExports = solved
            .Select(hypothesis => hypothesis.Export(
                selected: string.Equals(hypothesis.Id, selected.Id, StringComparison.Ordinal)))
            .ToArray();
        var selectedByCandidateId = solved
            .SelectMany(hypothesis => hypothesis.SelectedCandidateIds.Select(id => (CandidateId: id, HypothesisId: hypothesis.Id)))
            .GroupBy(item => item.CandidateId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(item => item.HypothesisId).Order(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var decisions = candidates
            .Select(candidate =>
            {
                var selectedBy = selectedByCandidateId.TryGetValue(candidate.Id, out var ids)
                    ? ids
                    : Array.Empty<string>();
                return new PlacementWallCandidateDecisionExport(
                    candidate.Id,
                    candidate.PageNumber,
                    candidate.PrimaryOrigin,
                    candidate.WallType,
                    candidate.DrawingLength,
                    Round(candidate.LocalScore),
                    candidate.MajorWallCandidate,
                    candidate.StrongNegativeEvidence,
                    candidate.SupportedEndpointCount,
                    candidate.RoomBoundarySupportCount,
                    candidate.OpeningSupportCount,
                    candidate.SourceWallIds,
                    selectedBy,
                    selected.SelectedCandidateIds.Contains(candidate.Id, StringComparer.Ordinal)
                        ? "Selected"
                        : selectedBy.Count > 0
                            ? "AlternativeHypothesisOnly"
                            : "Rejected",
                    CandidateDecisionEvidence(candidate, selectedBy))
                    ;
            })
            .ToArray();

        return new PlacementWallSolutionSetExport(
            SolverVersion,
            selected.Id,
            selected.Profile.Name,
            Round(selected.Score),
            candidates.Length,
            selected.SelectedCandidateIds.Count,
            selectedRuns.Count,
            selected.IterationCount,
            hypothesisExports,
            selectedRuns,
            decisions,
            solvedRuns.Reconciliation,
            solvedRuns.Topology,
            new[]
            {
                $"global solver evaluated {candidates.Length} deduplicated wall candidate(s)",
                $"global solver compared {solved.Length} deterministic wall graph hypotheses",
                structuralHypothesis is null
                    ? "legacy wall hypotheses selected the canonical candidate set"
                    : $"joint structural core proposed {structuralHypothesis.InitialCandidateCount} canonical candidate(s) and recovered {structuralHypothesis.RecoveredCandidateCount} guarded recall candidate(s)",
                structuralHypothesis is null
                    ? "no joint structural hypothesis was available for guarded arbitration"
                    : string.Equals(selected.Id, structuralHypothesis.Id, StringComparison.Ordinal)
                        ? "guarded arbitration retained the joint structural core"
                        : $"guarded arbitration selected {selected.Profile.Name} over the joint structural core after objective, recall, closure, duplicate, review, and noise checks",
                $"selected {selected.Profile.Name} hypothesis at objective {selected.Score:0.###}",
                $"selected graph compacted {selected.SelectedCandidateIds.Count} candidate(s) into {selectedRuns.Count} canonical wall run(s)",
                $"canonical wall runs contain {selectedRuns.Sum(run => run.OpeningIntervals.Count)} opening interval(s) and {selectedRuns.Sum(run => run.SolidIntervals.Count)} solid interval(s)",
                $"opening-aware reconstruction joined {selectedRuns.Sum(run => run.ReconstructedOpeningGapCount)} source gap(s)",
                $"evidence reconciliation adjusted {solvedRuns.Reconciliation.AdjustedWallRunCount} canonical wall run(s)",
                $"canonical topology retained {solvedRuns.Topology.InlineJunctionReferenceCount} inline junction reference(s) without splitting long wall runs",
                "raw wall detections remain unchanged; solver decisions are an auditable downstream interpretation"
            });
    }

    private static IReadOnlyList<GlobalWallCandidate> ApplyStructuralRejectionEvidence(
        IReadOnlyList<GlobalWallCandidate> candidates,
        StructuralPlanSolution? structuralSolution)
    {
        if (structuralSolution is null)
        {
            return candidates;
        }

        var absoluteBlockedStructuralWallIds = structuralSolution.CandidateDecisions
            .Where(decision => decision.AbsolutePlacementBlock)
            .SelectMany(decision => decision.SourceWallIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        var coordinateReadyStructuralWallIds = structuralSolution.WallRuns
            .Where(run =>
                run.Reliability.ReadyForCoordinatePlacement
                && !run.Reliability.RequiresReview)
            .SelectMany(run => run.SourceWallIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Where(id => !absoluteBlockedStructuralWallIds.Contains(id))
            .ToHashSet(StringComparer.Ordinal);
        var structurallyAcceptedWallIds = structuralSolution.CandidateDecisions
            .Where(decision =>
                decision.Decision == StructuralWallDecisionKind.Selected
                && !decision.AbsolutePlacementBlock)
            .SelectMany(decision => decision.SourceWallIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Concat(coordinateReadyStructuralWallIds)
            .Where(id => !absoluteBlockedStructuralWallIds.Contains(id))
            .ToHashSet(StringComparer.Ordinal);
        var structurallyRejectedWallIds = structuralSolution.CandidateDecisions
            .Where(decision =>
                decision.AbsolutePlacementBlock
                || (decision.Decision is StructuralWallDecisionKind.Rejected
                        or StructuralWallDecisionKind.Invalid
                    && decision.BlockingSignalKinds.Any(
                        StructuralRejectionSignalKinds.Contains)))
            .SelectMany(decision => decision.SourceWallIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Where(id => !structurallyAcceptedWallIds.Contains(id))
            .ToHashSet(StringComparer.Ordinal);
        if (structurallyRejectedWallIds.Count == 0)
        {
            return candidates;
        }

        return candidates
            .Select(candidate =>
            {
                var sourceWallIds = candidate.SourceWallIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToArray();
                if (sourceWallIds.Length == 0
                    || !sourceWallIds.All(structurallyRejectedWallIds.Contains))
                {
                    return candidate;
                }

                return candidate with
                {
                    ExcludedFromStructuralTopology = true,
                    ReadyForCoordinatePlacement = false,
                    ReadyForMetricPlacement = false,
                    RequiresReview = true,
                    StrongNegativeEvidenceVotes = Math.Max(
                        candidate.StrongNegativeEvidenceVotes,
                        Math.Max(1, candidate.EvidenceContributorCount)),
                    StrongNegativeEvidence = true,
                    Evidence = candidate.Evidence
                        .Append(
                            "joint structural evidence rejected every contributing source wall through auditable non-wall or unsupported-context evidence")
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                };
            })
            .ToArray();
    }

    private static IReadOnlyList<GlobalWallCandidate> BuildStructuralSolutionCandidates(
        StructuralPlanSolution? structuralSolution,
        IReadOnlyList<PlacementWallExport> walls)
    {
        if (structuralSolution is null || structuralSolution.WallRuns.Count == 0)
        {
            return Array.Empty<GlobalWallCandidate>();
        }

        var wallsById = walls
            .GroupBy(wall => wall.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return structuralSolution.WallRuns
            .Where(run => run.DrawingLength > 0)
            .Select(run =>
            {
                var sourceWalls = run.SourceWallIds
                    .Where(wallsById.ContainsKey)
                    .Select(id => wallsById[id])
                    .ToArray();
                var scale = sourceWalls
                    .Select(wall => wall.MillimetersPerDrawingUnit)
                    .FirstOrDefault(value => value is > 0);
                var sourceAwareGeometry = ResolveStructuralSourceAwareGeometry(
                    LineExport.From(run.CenterLine),
                    run.Thickness,
                    sourceWalls);
                var centerLine = sourceAwareGeometry.CenterLine;
                var bounds = sourceAwareGeometry.Trimmed
                    ? RectExport.From(BoundsFor(centerLine, run.Thickness))
                    : RectExport.From(run.Bounds);
                var sourceLayers = sourceWalls
                    .SelectMany(wall => wall.SourceLayers)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var sourceEvidence = sourceWalls
                    .SelectMany(wall => wall.Evidence)
                    .Concat(run.Evidence)
                    .Concat(run.Reliability.Reasons)
                    .Concat(sourceWalls.Any(wall => wall.Reliability.CoordinatePlacementBlocked)
                        ? new[]
                        {
                            "legacy source-wall placement blocks retained as provenance; canonical structural reliability is authoritative"
                        }
                        : Array.Empty<string>())
                    .Concat(sourceAwareGeometry.Trimmed
                        ? new[]
                        {
                            "canonical structural run extent clipped to coordinate-ready source-wall support; unsupported structural-hypothesis tails remain provenance"
                        }
                        : Array.Empty<string>())
                    .Concat(sourceAwareGeometry.RetainedRecoveredContinuation
                        ? new[]
                        {
                            "canonical structural run extent retained an adjacent source-backed recovered wall-body continuation after synchronized geometry disproved the earlier duplicate classification"
                        }
                        : Array.Empty<string>())
                    .Append($"selected by {structuralSolution.SolverVersion}")
                    .Append("canonical structural run supplied by core interpretation")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var readyForCoordinates =
                    run.Reliability.ReadyForCoordinatePlacement
                    && !run.Reliability.RequiresReview
                    && run.Confidence.Value >= 0.5;
                var requiresReview = !readyForCoordinates;
                var structuralNegative = run.Reliability.RequiresReview
                    && run.Reliability.Reasons.Any(reason =>
                        reason.Contains(
                            "unsupported geometry or context-only negative evidence",
                            StringComparison.OrdinalIgnoreCase)
                        || reason.Contains(
                            "excluded, unanchored wall-graph island",
                            StringComparison.OrdinalIgnoreCase));

                return CreateCandidate(
                    $"candidate:structural:{run.Id}",
                    "StructuralCore",
                    run.PageNumber,
                    centerLine,
                    scale is > 0 ? ScaleLine(centerLine, scale.Value) : null,
                    bounds,
                    ScaleRect(bounds, scale),
                    LineLength(centerLine),
                    scale is > 0 ? LineLength(centerLine) * scale.Value / 1000.0 : null,
                    run.Thickness,
                    scale is > 0 ? run.Thickness * scale.Value : null,
                    scale,
                    run.WallType.ToString(),
                    Math.Min(run.Confidence.Value, run.Reliability.Confidence),
                    excluded: false,
                    readyForCoordinates,
                    requiresReview,
                    run.SourceWallIds,
                    run.SourceWallGraphEdgeIds,
                    run.SourcePrimitiveIds,
                    sourceLayers,
                    BuildSourceWallComponentReferences(sourceWalls),
                    strongNegativeEvidenceVotes: structuralNegative
                        ? Math.Max(1, run.CandidateIds.Count)
                        : 0,
                    evidenceContributorCount: Math.Max(1, run.CandidateIds.Count),
                    sourceEvidence);
            })
            .ToArray();
    }

    private static (
        LineExport CenterLine,
        bool Trimmed,
        bool RetainedRecoveredContinuation)
        ResolveStructuralSourceAwareGeometry(
        LineExport centerLine,
        double thickness,
        IReadOnlyList<PlacementWallExport> sourceWalls)
    {
        if (Orientation(centerLine) == WallOrientation.Diagonal)
        {
            return (centerLine, false, false);
        }

        var cleanSources = sourceWalls
            .Where(wall =>
                wall.Reliability.ReadyForCoordinatePlacement
                && !wall.Reliability.CoordinatePlacementBlocked
                && !WallRequiresSolverReview(wall)
                && SameOrientation(wall.CenterLine, centerLine)
                && LineDistance(wall.CenterLine, centerLine)
                    <= Math.Max(3.0, thickness))
            .ToArray();
        if (cleanSources.Length == 0)
        {
            return (centerLine, false, false);
        }

        var recoveredContinuationSources = sourceWalls
            .Where(wall => IsTrustedRecoveredExtentContinuation(
                wall,
                centerLine,
                thickness,
                cleanSources))
            .ToArray();
        var recoveredContinuationIds = recoveredContinuationSources
            .Select(wall => wall.Id)
            .ToHashSet(StringComparer.Ordinal);
        var extentSources = cleanSources
            .Concat(recoveredContinuationSources)
            .DistinctBy(wall => wall.Id, StringComparer.Ordinal)
            .ToArray();
        var hasReviewOnlySource = sourceWalls.Any(wall =>
            (!wall.Reliability.ReadyForCoordinatePlacement
                || wall.Reliability.CoordinatePlacementBlocked
                || WallRequiresSolverReview(wall))
            && !recoveredContinuationIds.Contains(wall.Id));
        var originalStart = IntervalStart(centerLine);
        var originalEnd = IntervalEnd(centerLine);
        var supportedStart = Math.Max(
            originalStart,
            extentSources.Min(wall => IntervalStart(wall.CenterLine)));
        var supportedEnd = Math.Min(
            originalEnd,
            extentSources.Max(wall => IntervalEnd(wall.CenterLine)));
        var originalLength = originalEnd - originalStart;
        var supportedLength = supportedEnd - supportedStart;
        var hasDominantUnbackedTail =
            supportedLength > 0
            && originalLength >= supportedLength * 1.75
            && originalLength - supportedLength
                >= Math.Max(12.0, Math.Max(1.0, thickness) * 4.0);
        if (!hasReviewOnlySource && !hasDominantUnbackedTail)
        {
            return (
                centerLine,
                false,
                recoveredContinuationSources.Length > 0);
        }

        if (supportedLength <= 0
            || supportedLength < originalLength * 0.20
            || (supportedStart - originalStart < 1.0
                && originalEnd - supportedEnd < 1.0))
        {
            return (
                centerLine,
                false,
                recoveredContinuationSources.Length > 0);
        }

        var axis = AxisCoordinate(centerLine);
        var trimmed = Orientation(centerLine) == WallOrientation.Horizontal
            ? new LineExport(
                new PointExport(supportedStart, axis),
                new PointExport(supportedEnd, axis))
            : new LineExport(
                new PointExport(axis, supportedStart),
                new PointExport(axis, supportedEnd));
        return (
            trimmed,
            true,
            recoveredContinuationSources.Length > 0);
    }

    private static bool IsTrustedRecoveredExtentContinuation(
        PlacementWallExport wall,
        LineExport structuralCenterLine,
        double structuralThickness,
        IReadOnlyList<PlacementWallExport> cleanSources)
    {
        var assessment = wall.EvidenceAssessment;
        var omission = wall.PlacementOmission;
        if (wall.Reliability.ReadyForCoordinatePlacement
            && !wall.Reliability.CoordinatePlacementBlocked
            && !WallRequiresSolverReview(wall))
        {
            return false;
        }

        if (wall.ExcludedFromStructuralTopology
            || wall.Reliability.CoordinatePlacementBlocked
            || !string.Equals(
                wall.DetectionKind,
                "ParallelLinePair",
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                wall.WallComponentKind,
                "MainStructural",
                StringComparison.OrdinalIgnoreCase)
            || wall.Confidence < 0.80
            || assessment is null
            || assessment.Confidence < 0.80
            || assessment.RejectedAsNoise
            || assessment.ScoreBreakdown.PairSupportScore < 0.45
            || assessment.ScoreBreakdown.StructuralSupportScore < 0.15
            || assessment.ScoreBreakdown.RecoverySupportScore < 0.15
            || assessment.ScoreBreakdown.NegativeScore > 0.01
            || assessment.ScoreBreakdown.NoisePenalty > 0.01
            || !assessment.ScoreBreakdown.PositiveEvidence.Any(item =>
                item.Contains(
                    "both endpoints supported by structural context",
                    StringComparison.OrdinalIgnoreCase))
            || omission is null
            || !string.Equals(
                omission.Code,
                "duplicate_wall_face",
                StringComparison.OrdinalIgnoreCase)
            || Orientation(wall.CenterLine) == WallOrientation.Diagonal
            || !SameOrientation(wall.CenterLine, structuralCenterLine)
            || LineDistance(wall.CenterLine, structuralCenterLine)
                > Math.Max(3.0, structuralThickness)
            || HasStrongNegativeEvidence(
                wall.Evidence
                    .Concat(assessment.Evidence)
                    .Concat(wall.Reliability.Reasons)
                    .ToArray()))
        {
            return false;
        }

        var linkedCleanSource = cleanSources
            .Where(source => omission.LinkedWallIds.Contains(
                source.Id,
                StringComparer.Ordinal))
            .Where(source => SameOrientation(source.CenterLine, wall.CenterLine))
            .Where(source => CompatibleWallTypes(source.WallType, wall.WallType))
            .Where(source =>
                !string.IsNullOrWhiteSpace(wall.WallComponentId)
                && string.Equals(
                    source.WallComponentId,
                    wall.WallComponentId,
                    StringComparison.Ordinal))
            .OrderBy(source => ProjectedIntervalGap(
                source.CenterLine,
                wall.CenterLine))
            .FirstOrDefault();
        if (linkedCleanSource is null)
        {
            return false;
        }

        var overlap = ProjectedOverlapLength(
            linkedCleanSource.CenterLine,
            wall.CenterLine);
        var minimumLength = Math.Min(
            linkedCleanSource.DrawingLength,
            wall.DrawingLength);
        if (minimumLength <= 0
            || overlap / minimumLength > 0.20)
        {
            return false;
        }

        var maximumContinuationGap = Math.Max(
            IntervalMergeGap,
            Math.Min(
                linkedCleanSource.ThicknessDrawingUnits,
                wall.ThicknessDrawingUnits));
        return ProjectedIntervalGap(
                linkedCleanSource.CenterLine,
                wall.CenterLine)
            <= maximumContinuationGap;
    }

    private static double ProjectedIntervalGap(
        LineExport first,
        LineExport second)
    {
        var firstStart = IntervalStart(first);
        var firstEnd = IntervalEnd(first);
        var secondStart = IntervalStart(second);
        var secondEnd = IntervalEnd(second);
        return secondStart > firstEnd
            ? secondStart - firstEnd
            : firstStart > secondEnd
                ? firstStart - secondEnd
                : 0;
    }

    private static SolvedHypothesis? BuildStructuralCoreHypothesis(
        StructuralPlanSolution? structuralSolution,
        IReadOnlyList<GlobalWallCandidate> candidates,
        IReadOnlyList<PlacementRoomExport> rooms,
        IReadOnlyList<PlacementOpeningExport> openings,
        IReadOnlyList<SolvedHypothesis> legacyHypotheses)
    {
        if (structuralSolution is null || structuralSolution.WallRuns.Count == 0)
        {
            return null;
        }

        var structuralCandidates = candidates
            .Where(candidate => string.Equals(candidate.PrimaryOrigin, "StructuralCore", StringComparison.Ordinal))
            .ToArray();
        if (structuralCandidates.Length == 0)
        {
            return null;
        }

        var structuralIds = structuralCandidates
            .Select(candidate => candidate.Id)
            .ToHashSet(StringComparer.Ordinal);
        var readyStructuralIds = structuralCandidates
            .Where(candidate =>
                candidate.ReadyForCoordinatePlacement
                && !candidate.RequiresReview
                && !candidate.StrongNegativeEvidence)
            .Select(candidate => candidate.Id)
            .ToHashSet(StringComparer.Ordinal);
        var reviewFallbackStructuralIds = structuralCandidates
            .Where(candidate =>
                !candidate.StrongNegativeEvidence
                && !candidate.ExcludedFromStructuralTopology)
            .Select(candidate => candidate.Id)
            .ToHashSet(StringComparer.Ordinal);
        var selectedIds = readyStructuralIds.Count > 0
            ? readyStructuralIds
            : reviewFallbackStructuralIds;
        if (selectedIds.Count == 0)
        {
            return null;
        }

        var withheldReviewCount = structuralIds.Count - selectedIds.Count;
        var consensusRecovered = SelectConsensusRecallCandidates(
            legacyHypotheses,
            candidates,
            selectedIds,
            rooms,
            openings);
        selectedIds.UnionWith(consensusRecovered.Select(candidate => candidate.Id));
        var bridgeRecovered = SelectStructuralBridgeRecallCandidates(
            candidates,
            selectedIds,
            rooms,
            openings);
        selectedIds.UnionWith(bridgeRecovered.Select(candidate => candidate.Id));
        var recoveredCount = consensusRecovered
            .Concat(bridgeRecovered)
            .Select(candidate => candidate.Id)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var metrics = Evaluate(selectedIds, candidates, rooms, openings);
        var profile = new WallSolverProfile(
            "joint-structural",
            InitialGraphThreshold: 0,
            InitialRecoveredThreshold: 0,
            MinimumConsiderScore: 0,
            AllowedObjectiveRegressionForMajorRecall: 0);
        return new SolvedHypothesis(
            "hypothesis:joint-structural-core",
            profile,
            metrics.ObjectiveScore,
            structuralSolution.Metrics.OptimizationPassCount,
            structuralIds.Count,
            selectedIds.Order(StringComparer.Ordinal).ToArray(),
            RecoveredCandidateCount: recoveredCount,
            RemovedCandidateCount: withheldReviewCount,
            TrustedCoordinateRecoveredCandidateIds: bridgeRecovered
                .Select(candidate => candidate.Id)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            metrics);
    }

    private static IReadOnlyList<GlobalWallCandidate> SelectConsensusRecallCandidates(
        IReadOnlyList<SolvedHypothesis> legacyHypotheses,
        IReadOnlyList<GlobalWallCandidate> candidates,
        IReadOnlySet<string> selectedStructuralIds,
        IReadOnlyList<PlacementRoomExport> rooms,
        IReadOnlyList<PlacementOpeningExport> openings)
    {
        var profileHypotheses = Profiles
            .Select(profile => legacyHypotheses.SingleOrDefault(hypothesis =>
                string.Equals(hypothesis.Profile.Name, profile.Name, StringComparison.Ordinal)))
            .Where(hypothesis => hypothesis is not null)
            .Cast<SolvedHypothesis>()
            .ToArray();
        if (profileHypotheses.Length != Profiles.Length)
        {
            return Array.Empty<GlobalWallCandidate>();
        }

        var consensusIds = profileHypotheses[0].SelectedCandidateIds
            .Where(candidateId => profileHypotheses
                .Skip(1)
                .All(hypothesis => hypothesis.SelectedCandidateIds.Contains(
                    candidateId,
                    StringComparer.Ordinal)))
            .ToHashSet(StringComparer.Ordinal);
        if (consensusIds.Count == 0)
        {
            return Array.Empty<GlobalWallCandidate>();
        }

        var structuralRuns = PrepareSelectedRuns(
            candidates.Where(candidate => selectedStructuralIds.Contains(candidate.Id)).ToArray(),
            candidates,
            rooms,
            openings);
        return candidates
            .Where(candidate => consensusIds.Contains(candidate.Id))
            .Where(candidate => !string.Equals(
                candidate.PrimaryOrigin,
                "StructuralCore",
                StringComparison.Ordinal))
            .Where(candidate =>
                !candidate.ExcludedFromStructuralTopology
                && !candidate.StrongNegativeEvidence
                && HasConsensusRecallSupport(candidate)
                && Orientation(candidate.CenterLine) != WallOrientation.Diagonal
                && HasRecoverableConsensusRecallCoverage(candidate, structuralRuns))
            .GroupBy(ConsensusRecallSourceSignature, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(candidate => ConsensusRecallOriginPreference(candidate.PrimaryOrigin))
                .ThenBy(candidate => candidate.DrawingLength)
                .ThenByDescending(candidate => candidate.LocalScore)
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                .First())
            .OrderBy(candidate => candidate.PageNumber)
            .ThenBy(candidate => candidate.Bounds.Y)
            .ThenBy(candidate => candidate.Bounds.X)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasConsensusRecallSupport(GlobalWallCandidate candidate) =>
        (candidate.LocalScore >= MinimumConsensusRecallScore
            && candidate.MajorWallCandidate
            && candidate.SupportedEndpointCount >= 2
            && HasConsensusRecallRoomSupport(candidate))
        || (candidate.LocalScore >= MinimumConsensusSourceBackedRecallScore
            && HasConsensusSourceBackedRecallSupport(candidate));

    private static bool HasConsensusRecallRoomSupport(GlobalWallCandidate candidate) =>
        candidate.IndoorRoomBoundarySupportCount >= 2
        || (string.Equals(candidate.WallType, "Interior", StringComparison.OrdinalIgnoreCase)
            && candidate.RoomBoundarySupportCount >= 2
            && candidate.OutdoorRoomBoundarySupportCount == 0
            && candidate.TwoSidedSourceLinkedRoomBoundarySupport
            && candidate.ReadyForCoordinatePlacement
            && !candidate.RequiresReview
            && candidate.StructuralEvidenceCount >= 2
            && candidate.WeakNegativeEvidenceCount == 0);

    private static bool HasConsensusSourceBackedRecallSupport(GlobalWallCandidate candidate) =>
        string.Equals(candidate.PrimaryOrigin, "CleanGraph", StringComparison.Ordinal)
        && candidate.ReadyForCoordinatePlacement
        && candidate.WeakNegativeEvidenceCount <= 1
        && (candidate.MajorWallCandidate
            ? (candidate.StructuralEvidenceCount >= 2
                    && (candidate.SupportedEndpointCount >= 2
                        || candidate.OpeningSupportCount >= 1))
                || (candidate.StructuralEvidenceCount >= 1
                    && candidate.SupportedEndpointCount >= 2
                    && candidate.OpeningSupportCount >= 2)
            : candidate.StructuralEvidenceCount >= 1
                && candidate.SupportedEndpointCount >= 2
                && (candidate.OpeningSupportCount >= 1
                    || candidate.RoomBoundarySupportCount >= 1));

    private static bool HasRecoverableConsensusRecallCoverage(
        GlobalWallCandidate candidate,
        IReadOnlyList<CompactedWallRun> structuralRuns)
    {
        var coverage = CoverageRatio(candidate, structuralRuns);
        if (coverage < MaximumConsensusRecallCoverage)
        {
            return true;
        }

        return candidate.MajorWallCandidate
            && HasConsensusSourceBackedRecallSupport(candidate)
            && candidate.SupportedEndpointCount >= 2
            && candidate.StructuralEvidenceCount >= 3
            && HasSufficientUncoveredConsensusRecallLength(
                candidate.DrawingLength,
                coverage,
                candidate.ThicknessDrawingUnits);
    }

    internal static bool HasSufficientUncoveredConsensusRecallLength(
        double drawingLength,
        double coverageRatio,
        double thicknessDrawingUnits)
    {
        var uncoveredLength = Math.Max(0, drawingLength)
            * (1.0 - Math.Clamp(coverageRatio, 0, 1));
        return uncoveredLength >= Math.Max(
            MinimumConsensusRecallUncoveredLength,
            Math.Max(0, thicknessDrawingUnits)
                * MinimumConsensusRecallUncoveredThicknessMultiple);
    }

    private static string ConsensusRecallSourceSignature(GlobalWallCandidate candidate) =>
        candidate.SourceWallIds.Count == 0
            ? candidate.Id
            : string.Join(
                "\u001f",
                candidate.SourceWallIds.Order(StringComparer.Ordinal));

    private static int ConsensusRecallOriginPreference(string origin) =>
        origin switch
        {
            "SourceWall" => 3,
            "CleanGraph" => 2,
            "TopologySpan" => 1,
            _ => 0
        };

    private static IReadOnlyList<GlobalWallCandidate> SelectStructuralBridgeRecallCandidates(
        IReadOnlyList<GlobalWallCandidate> candidates,
        IReadOnlySet<string> selectedStructuralIds,
        IReadOnlyList<PlacementRoomExport> rooms,
        IReadOnlyList<PlacementOpeningExport> openings)
    {
        var selectedIds = selectedStructuralIds.ToHashSet(StringComparer.Ordinal);
        var recovered = new List<GlobalWallCandidate>();
        for (var pass = 0; pass < MaximumStructuralBridgeRecallPassCount; pass++)
        {
            var selectedRuns = PrepareSelectedRuns(
                candidates.Where(candidate => selectedIds.Contains(candidate.Id)).ToArray(),
                candidates,
                rooms,
                openings);
            var passRecovered = 0;
            foreach (var candidate in candidates
                         .Where(candidate => !selectedIds.Contains(candidate.Id))
                         .OrderByDescending(candidate => candidate.LocalScore)
                         .ThenByDescending(candidate => candidate.StructuralEvidenceCount)
                         .ThenByDescending(candidate => candidate.DrawingLength)
                         .ThenBy(candidate => candidate.Id, StringComparer.Ordinal))
            {
                if (!HasStructuralBridgeRecallSupport(candidate, selectedRuns))
                {
                    continue;
                }

                selectedIds.Add(candidate.Id);
                recovered.Add(candidate);
                passRecovered++;
                selectedRuns = PrepareSelectedRuns(
                    candidates.Where(item => selectedIds.Contains(item.Id)).ToArray(),
                    candidates,
                    rooms,
                    openings);
            }

            if (passRecovered == 0)
            {
                break;
            }
        }

        return recovered
            .OrderBy(candidate => candidate.PageNumber)
            .ThenBy(candidate => candidate.Bounds.Y)
            .ThenBy(candidate => candidate.Bounds.X)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasStructuralBridgeRecallSupport(
        GlobalWallCandidate candidate,
        IReadOnlyList<CompactedWallRun> selectedRuns)
    {
        if (!string.Equals(candidate.PrimaryOrigin, "SourceWall", StringComparison.Ordinal)
            || candidate.ExcludedFromStructuralTopology
            || candidate.StrongNegativeEvidence
            || !candidate.RequiresReview
            || candidate.LocalScore < MinimumStructuralBridgeRecallScore
            || candidate.StructuralEvidenceCount < 1
            || candidate.WeakNegativeEvidenceCount > 1
            || Orientation(candidate.CenterLine) == WallOrientation.Diagonal
            || !HasMainStructuralSourceComponent(candidate)
            || !HasExplicitVectorWallBodyEvidence(candidate))
        {
            return false;
        }

        var coverage = CoverageRatio(candidate, selectedRuns);
        if (coverage >= MaximumConsensusRecallCoverage
            || !HasSufficientUncoveredConsensusRecallLength(
                candidate.DrawingLength,
                coverage,
                candidate.ThicknessDrawingUnits))
        {
            return false;
        }

        var selectedEndpointSupport = CountSelectedRunEndpointSupport(
            candidate,
            selectedRuns);
        return selectedEndpointSupport >= 2
            || (selectedEndpointSupport >= 1
                && candidate.OpeningSupportCount >= 1
                && HasGeometricRoomBoundaryEvidence(candidate));
    }

    private static bool HasMainStructuralSourceComponent(GlobalWallCandidate candidate) =>
        candidate.SourceWallComponents.Any(component =>
            string.Equals(
                component.Kind,
                "MainStructural",
                StringComparison.OrdinalIgnoreCase));

    private static bool HasExplicitVectorWallBodyEvidence(GlobalWallCandidate candidate)
    {
        var text = string.Join(" | ", candidate.Evidence).ToLowerInvariant();
        return text.Contains("parallel wall-face pair")
            || text.Contains("strong parallel-face wall pair")
            || text.Contains("filled wall-solid primitive")
            || text.Contains("filled closed vector wall body")
            || text.Contains("merged collinear wall fragments")
            || text.Contains("unclaimed parallel wall-face evidence");
    }

    private static bool HasGeometricRoomBoundaryEvidence(GlobalWallCandidate candidate) =>
        candidate.Evidence.Any(evidence => evidence.Contains(
            "geometric room boundary",
            StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<GlobalWallCandidate> BuildCandidatePool(
        IReadOnlyList<PlacementWallExport> walls,
        PlacementWallGraphExport graph)
    {
        var wallsById = walls
            .GroupBy(wall => wall.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var candidates = new List<GlobalWallCandidate>();

        foreach (var edge in graph.Edges.Where(HasUsableGeometry))
        {
            var sourceWallIds = edge.SourceWallIds
                .Append(edge.WallId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var sourceWalls = sourceWallIds
                .Where(wallsById.ContainsKey)
                .Select(id => wallsById[id])
                .ToArray();
            var edgeEvidence = edge.Evidence
                .Concat(sourceWalls.SelectMany(wall => wall.Evidence))
                .ToArray();
            var strongNegativeVotes = sourceWalls.Count(wall =>
                HasStrongNegativeEvidence(wall.Evidence));
            strongNegativeVotes += HasStrongNegativeEvidence(edge.Evidence) ? 1 : 0;
            candidates.Add(CreateCandidate(
                $"candidate:graph:{edge.Id}",
                "CleanGraph",
                edge.PageNumber,
                edge.CenterLine!,
                edge.CenterLineMillimeters,
                edge.Bounds!,
                edge.BoundsMillimeters,
                edge.DrawingLength,
                edge.LengthMeters,
                edge.ThicknessDrawingUnits,
                edge.ThicknessMillimeters,
                edge.MillimetersPerDrawingUnit,
                ResolveWallType(sourceWalls),
                edge.Confidence,
                edge.ExcludedFromStructuralTopology,
                sourceWalls.Any(wall => wall.Reliability.ReadyForCoordinatePlacement),
                sourceWalls.Any(WallRequiresSolverReview),
                sourceWallIds,
                edge.SourceWallGraphEdgeIds,
                edge.SourcePrimitiveIds,
                edge.SourceLayers,
                BuildSourceWallComponentReferences(sourceWalls),
                strongNegativeVotes,
                sourceWalls.Length + 1,
                edgeEvidence));
        }

        foreach (var wall in walls)
        {
            foreach (var span in wall.TopologySpans.Where(HasUsableGeometry))
            {
                var spanEvidence = span.Evidence.Concat(wall.Evidence).ToArray();
                candidates.Add(CreateCandidate(
                    $"candidate:span:{span.Id}",
                    "TopologySpan",
                    span.PageNumber,
                    span.CenterLine,
                    span.CenterLineMillimeters,
                    span.Bounds,
                    span.BoundsMillimeters,
                    span.DrawingLength,
                    span.LengthMeters,
                    span.ThicknessDrawingUnits,
                    span.ThicknessMillimeters,
                    wall.MillimetersPerDrawingUnit,
                    wall.WallType,
                    Math.Min(wall.Confidence, span.Confidence),
                    wall.ExcludedFromStructuralTopology,
                    wall.Reliability.ReadyForCoordinatePlacement,
                    WallRequiresSolverReview(wall),
                    new[] { wall.Id },
                    span.SourceWallGraphEdgeIds,
                    span.SourcePrimitiveIds,
                    span.SourceLayers,
                    BuildSourceWallComponentReferences([wall]),
                    (HasStrongNegativeEvidence(span.Evidence) ? 1 : 0)
                        + (HasStrongNegativeEvidence(wall.Evidence) ? 1 : 0),
                    2,
                    spanEvidence));
            }

            if (wall.DrawingLength <= 0 || !HasUsableGeometry(wall.CenterLine))
            {
                continue;
            }

            var wallEvidence = wall.Evidence
                .Concat(wall.Reliability.Reasons)
                .Concat(wall.PlacementOmission?.Evidence ?? Array.Empty<string>())
                .ToArray();
            candidates.Add(CreateCandidate(
                $"candidate:wall:{wall.Id}",
                "SourceWall",
                wall.PageNumber,
                wall.CenterLine,
                wall.CenterLineMillimeters,
                wall.Bounds,
                wall.BoundsMillimeters,
                wall.DrawingLength,
                wall.LengthMeters,
                wall.ThicknessDrawingUnits,
                wall.ThicknessMillimeters,
                wall.MillimetersPerDrawingUnit,
                wall.WallType,
                wall.Confidence,
                wall.ExcludedFromStructuralTopology,
                wall.Reliability.ReadyForCoordinatePlacement,
                WallRequiresSolverReview(wall),
                new[] { wall.Id },
                wall.TopologySpans.SelectMany(span => span.SourceWallGraphEdgeIds).Distinct(StringComparer.Ordinal).ToArray(),
                wall.SourcePrimitiveIds,
                wall.SourceLayers,
                BuildSourceWallComponentReferences([wall]),
                HasStrongNegativeEvidence(wallEvidence) ? 1 : 0,
                1,
                wallEvidence));
        }

        return DeduplicateCandidates(candidates);
    }

    private static bool WallRequiresSolverReview(PlacementWallExport wall) =>
        wall.Reliability.RequiresReview
        || wall.EvidenceAssessment?.RequiresReview == true;

    private static IReadOnlyDictionary<string, CleanTopologyRepresentation> BuildCleanTopologyRepresentations(
        IReadOnlyList<PlacementWallExport> walls)
    {
        var wallsById = walls
            .GroupBy(wall => wall.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var representations = new Dictionary<string, CleanTopologyRepresentation>(StringComparer.Ordinal);

        foreach (var wall in walls.Where(wall =>
                     string.Equals(
                         wall.PlacementOmission?.Code,
                         "duplicate_clean_topology_span",
                         StringComparison.Ordinal)))
        {
            var hostWallIds = wall.PlacementOmission!.LinkedWallIds
                .Where(id => !string.Equals(id, wall.Id, StringComparison.Ordinal))
                .Where(wallsById.ContainsKey)
                .Where(id => IsCleanTopologyRepresentationGeometry(wall, wallsById[id]))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (hostWallIds.Length == 0)
            {
                continue;
            }

            representations[wall.Id] = new CleanTopologyRepresentation(
                wall,
                hostWallIds);
        }

        return representations;
    }

    private static IReadOnlyList<GlobalWallCandidate> ConsolidateCleanTopologyRepresentations(
        IReadOnlyList<GlobalWallCandidate> candidates,
        IReadOnlyDictionary<string, CleanTopologyRepresentation> representations)
    {
        if (representations.Count == 0)
        {
            return candidates;
        }

        var retained = candidates
            .Where(candidate =>
            {
                var sourceWallIds = candidate.SourceWallIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                return sourceWallIds.Length == 0
                    || sourceWallIds.Any(id => !representations.ContainsKey(id));
            })
            .ToArray();

        return retained
            .Select(candidate =>
            {
                var representedWalls = representations.Values
                    .Where(representation =>
                        representation.HostWallIds.Any(candidate.SourceWallIds.Contains))
                    .Select(representation => representation.RepresentedWall)
                    .DistinctBy(wall => wall.Id, StringComparer.Ordinal)
                    .ToArray();
                if (representedWalls.Length == 0)
                {
                    return candidate;
                }

                return candidate with
                {
                    SourceWallIds = candidate.SourceWallIds
                        .Concat(representedWalls.Select(wall => wall.Id))
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray(),
                    SourcePrimitiveIds = candidate.SourcePrimitiveIds
                        .Concat(representedWalls.SelectMany(wall => wall.SourcePrimitiveIds))
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray(),
                    SourceLayers = candidate.SourceLayers
                        .Concat(representedWalls.SelectMany(wall => wall.SourceLayers))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    EvidenceContributorCount = candidate.EvidenceContributorCount + representedWalls.Length,
                    Evidence = candidate.Evidence
                        .Concat(representedWalls.SelectMany(wall =>
                            wall.PlacementOmission?.Evidence ?? Array.Empty<string>()))
                        .Append(
                            $"consolidated {representedWalls.Length} wall source(s) explicitly represented by clean topology")
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                };
            })
            .ToArray();
    }

    private static bool IsCleanTopologyRepresentationGeometry(
        PlacementWallExport representedWall,
        PlacementWallExport hostWall)
    {
        if (representedWall.PageNumber != hostWall.PageNumber
            || !HasUsableGeometry(representedWall.CenterLine)
            || !HasUsableGeometry(hostWall.CenterLine)
            || !SameOrientation(representedWall.CenterLine, hostWall.CenterLine))
        {
            return false;
        }

        var overlap = ProjectedOverlapLength(representedWall.CenterLine, hostWall.CenterLine);
        var representedLength = LineLength(representedWall.CenterLine);
        var hostLength = LineLength(hostWall.CenterLine);
        return representedLength > 0.001
            && hostLength > 0.001
            && overlap / Math.Min(representedLength, hostLength) >= 0.88;
    }

    private static IReadOnlyList<GlobalWallCandidate> DeduplicateCandidates(
        IReadOnlyList<GlobalWallCandidate> candidates)
    {
        var retained = new List<GlobalWallCandidate>();
        foreach (var candidate in candidates
                     .OrderByDescending(candidate => OriginPriority(candidate.PrimaryOrigin))
                     .ThenByDescending(candidate => candidate.ReadyForCoordinatePlacement)
                     .ThenByDescending(candidate => candidate.Confidence)
                     .ThenByDescending(candidate => candidate.DrawingLength)
                     .ThenBy(candidate => candidate.Id, StringComparer.Ordinal))
        {
            var duplicateIndex = retained.FindIndex(existing => AreEquivalent(existing, candidate));
            if (duplicateIndex < 0)
            {
                retained.Add(candidate);
                continue;
            }

            var existing = retained[duplicateIndex];
            retained[duplicateIndex] = ShouldPreferPreciseSourceGeometry(candidate, existing)
                ? MergeEquivalent(candidate, existing)
                : MergeEquivalent(existing, candidate);
        }

        return retained;
    }

    private static bool ShouldPreferPreciseSourceGeometry(
        GlobalWallCandidate candidate,
        GlobalWallCandidate existing) =>
        string.Equals(candidate.PrimaryOrigin, "SourceWall", StringComparison.Ordinal)
        && !string.Equals(existing.PrimaryOrigin, "StructuralCore", StringComparison.Ordinal)
        && !string.Equals(existing.PrimaryOrigin, "SourceWall", StringComparison.Ordinal)
        && candidate.ReadyForCoordinatePlacement
        && !candidate.RequiresReview
        && candidate.SourceWallIds.Count > 0
        && candidate.SourceWallIds.ToHashSet(StringComparer.Ordinal)
            .SetEquals(existing.SourceWallIds)
        && existing.DrawingLength >= candidate.DrawingLength * 1.12
        && existing.DrawingLength - candidate.DrawingLength
            >= Math.Max(12.0, candidate.ThicknessDrawingUnits)
        && ProjectedOverlapLength(candidate.CenterLine, existing.CenterLine)
            >= candidate.DrawingLength * 0.95;

    private static GlobalWallCandidate AddContext(
        GlobalWallCandidate candidate,
        IReadOnlyList<GlobalWallCandidate> allCandidates,
        IReadOnlyList<PlacementRoomExport> rooms,
        IReadOnlyList<PlacementOpeningExport> openings)
    {
        var supportedEndpoints = CountSupportedEndpoints(candidate, allCandidates);
        var supportingRooms = rooms.Where(room =>
            room.PageNumber == candidate.PageNumber
            && RoomSupportsCandidate(room, candidate))
            .ToArray();
        var roomSupport = supportingRooms.Length;
        var indoorRoomSupport = supportingRooms.Count(room =>
            IsIndoorRoomUse(room.UseKind));
        var outdoorRoomSupport = supportingRooms.Count(room =>
            IsOutdoorRoomUse(room.UseKind));
        var twoSidedSourceLinkedRoomSupport =
            HasTwoSidedSourceLinkedRoomBoundarySupport(candidate, supportingRooms);
        var openingSupport = openings.Count(opening =>
            opening.PageNumber == candidate.PageNumber
            && opening.HostWallIds.Any(candidate.SourceWallIds.Contains));
        var evidenceText = string.Join(" | ", candidate.Evidence).ToLowerInvariant();
        var weakNegativeCount = CountWeakNegativeEvidence(candidate.Evidence);
        var structuralTermCount = StructuralPositiveTerms.Count(evidenceText.Contains);
        var corroboratedStructuralBody = !candidate.ExcludedFromStructuralTopology
            && candidate.ReadyForCoordinatePlacement
            && structuralTermCount >= 2
            && (roomSupport > 0 || openingSupport > 0);
        var strongNegative = candidate.StrongNegativeEvidenceVotes > 0
            && candidate.StrongNegativeEvidenceVotes * 2 >= candidate.EvidenceContributorCount
            && !corroboratedStructuralBody;
        var localScore = candidate.Confidence * 0.45;
        localScore += OriginPriority(candidate.PrimaryOrigin) switch
        {
            3 => 0.27,
            2 => 0.18,
            _ => 0.04
        };
        localScore += candidate.ReadyForCoordinatePlacement ? 0.17 : 0;
        localScore -= candidate.RequiresReview ? 0.07 : 0;
        localScore -= candidate.ExcludedFromStructuralTopology ? 0.24 : 0;
        localScore += Math.Min(0.10, candidate.DrawingLength / 1500.0);
        localScore += Math.Min(0.12, supportedEndpoints * 0.055);
        localScore += Math.Min(0.12, roomSupport * 0.06);
        localScore += Math.Min(0.06, openingSupport * 0.03);
        localScore += Math.Min(0.12, structuralTermCount * 0.025);
        localScore -= Math.Min(0.12, weakNegativeCount * 0.035);
        localScore -= strongNegative ? 0.34 : 0;
        localScore = Math.Clamp(localScore, 0, 1);
        var unresolvedReviewSource = OriginPriority(candidate.PrimaryOrigin) == 1
            && !candidate.ReadyForCoordinatePlacement
            && candidate.RequiresReview
            && string.Equals(candidate.WallType, "Unknown", StringComparison.OrdinalIgnoreCase);
        var coherentRoomBoundary = IsCoherentUnknownRoomBoundaryCandidate(
            candidate,
            localScore,
            strongNegative,
            weakNegativeCount,
            structuralTermCount,
            supportedEndpoints,
            indoorRoomSupport);
        var major = candidate.DrawingLength >= MajorWallLength
            && localScore >= 0.44
            && !candidate.ExcludedFromStructuralTopology
            && (!unresolvedReviewSource || coherentRoomBoundary)
            && (!strongNegative
                || (candidate.ReadyForCoordinatePlacement
                    && roomSupport >= 2
                    && structuralTermCount >= 2));
        var contextualWallType = coherentRoomBoundary
            ? outdoorRoomSupport > 0
                ? "Exterior"
                : "Interior"
            : candidate.WallType;
        var contextualEvidence = coherentRoomBoundary
            ? candidate.Evidence
                .Append(
                    $"global solver coherent room boundary support: {indoorRoomSupport} indoor and {outdoorRoomSupport} outdoor room(s), {supportedEndpoints}/2 supported endpoint(s)")
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : candidate.Evidence;

        return candidate with
        {
            WallType = contextualWallType,
            LocalScore = localScore,
            StrongNegativeEvidence = strongNegative,
            WeakNegativeEvidenceCount = weakNegativeCount,
            StructuralEvidenceCount = structuralTermCount,
            SupportedEndpointCount = supportedEndpoints,
            RoomBoundarySupportCount = roomSupport,
            IndoorRoomBoundarySupportCount = indoorRoomSupport,
            OutdoorRoomBoundarySupportCount = outdoorRoomSupport,
            TwoSidedSourceLinkedRoomBoundarySupport = twoSidedSourceLinkedRoomSupport,
            OpeningSupportCount = openingSupport,
            MajorWallCandidate = major,
            CoherentRoomBoundaryCandidate = coherentRoomBoundary,
            Evidence = contextualEvidence
        };
    }

    private static bool IsCoherentUnknownRoomBoundaryCandidate(
        GlobalWallCandidate candidate,
        double localScore,
        bool strongNegative,
        int weakNegativeCount,
        int structuralTermCount,
        int supportedEndpointCount,
        int indoorRoomSupportCount) =>
        string.Equals(candidate.WallType, "Unknown", StringComparison.OrdinalIgnoreCase)
        && candidate.RequiresReview
        && !candidate.ExcludedFromStructuralTopology
        && !strongNegative
        && candidate.DrawingLength >= MinimumCoherentBoundaryLength
        && localScore >= MinimumCoherentBoundaryScore
        && structuralTermCount >= 2
        && weakNegativeCount <= 2
        && supportedEndpointCount >= 1
        && indoorRoomSupportCount >= 2
        && Orientation(candidate.CenterLine) != WallOrientation.Diagonal;

    private static bool IsIndoorRoomUse(string useKind) =>
        !string.IsNullOrWhiteSpace(useKind)
        && !string.Equals(useKind, "Unknown", StringComparison.OrdinalIgnoreCase)
        && !IsOutdoorRoomUse(useKind);

    private static bool IsOutdoorRoomUse(string useKind) =>
        string.Equals(useKind, "Outdoor", StringComparison.OrdinalIgnoreCase)
        || string.Equals(useKind, "Terrace", StringComparison.OrdinalIgnoreCase);

    internal static bool HasStrongNegativeEvidence(IReadOnlyList<string> evidence)
    {
        var text = string.Join(" | ", evidence).ToLowerInvariant();
        return StrongNegativeTerms.Any(text.Contains);
    }

    internal static int CountWeakNegativeEvidence(IReadOnlyList<string> evidence)
    {
        var text = string.Join(" | ", evidence).ToLowerInvariant();
        return WeakNegativeTerms.Count(text.Contains);
    }

    private static SolvedHypothesis Solve(
        WallSolverProfile profile,
        IReadOnlyList<GlobalWallCandidate> candidates,
        IReadOnlyList<PlacementRoomExport> rooms,
        IReadOnlyList<PlacementOpeningExport> openings)
    {
        var selected = candidates
            .Where(candidate => InitiallySelected(profile, candidate))
            .Select(candidate => candidate.Id)
            .ToHashSet(StringComparer.Ordinal);
        var initiallySelected = selected.ToHashSet(StringComparer.Ordinal);
        var initialCount = selected.Count;
        var iterationCount = 0;

        for (var pass = 0; pass < 2; pass++)
        {
            var changed = false;
            foreach (var candidate in candidates
                         .Where(candidate => !selected.Contains(candidate.Id))
                         .Where(IsSolverEligible)
                         .Where(candidate => candidate.LocalScore >= profile.MinimumConsiderScore)
                         .OrderByDescending(candidate => candidate.MajorWallCandidate)
                         .ThenByDescending(candidate => candidate.DrawingLength * candidate.LocalScore)
                         .ThenBy(candidate => candidate.Id, StringComparer.Ordinal))
            {
                var current = Evaluate(selected, candidates, rooms, openings);
                selected.Add(candidate.Id);
                var trial = Evaluate(selected, candidates, rooms, openings);
                var coverageGain = trial.MajorWallCoverageRatio - current.MajorWallCoverageRatio;
                var accept = trial.ObjectiveScore >= current.ObjectiveScore + 0.003
                    || (candidate.MajorWallCandidate
                        && coverageGain >= 0.01
                        && trial.ObjectiveScore + profile.AllowedObjectiveRegressionForMajorRecall >= current.ObjectiveScore);
                if (!accept)
                {
                    selected.Remove(candidate.Id);
                    continue;
                }

                changed = true;
            }

            foreach (var candidate in candidates
                         .Where(candidate => selected.Contains(candidate.Id))
                         .Where(candidate => !candidate.MajorWallCandidate)
                         .OrderBy(candidate => candidate.LocalScore)
                         .ThenBy(candidate => candidate.DrawingLength)
                         .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                         .ToArray())
            {
                var current = Evaluate(selected, candidates, rooms, openings);
                selected.Remove(candidate.Id);
                var trial = Evaluate(selected, candidates, rooms, openings);
                if (trial.ObjectiveScore <= current.ObjectiveScore + 0.004)
                {
                    selected.Add(candidate.Id);
                    continue;
                }

                changed = true;
            }

            iterationCount++;
            if (!changed)
            {
                break;
            }
        }

        var metrics = Evaluate(selected, candidates, rooms, openings);
        var recoveredCount = selected.Count(candidateId => !initiallySelected.Contains(candidateId));
        var removedCount = initiallySelected.Count(candidateId => !selected.Contains(candidateId));
        return new SolvedHypothesis(
            $"wall-solution:{profile.Name}",
            profile,
            metrics.ObjectiveScore,
            iterationCount,
            initialCount,
            selected.Order(StringComparer.Ordinal).ToArray(),
            recoveredCount,
            removedCount,
            Array.Empty<string>(),
            metrics);
    }

    private static bool InitiallySelected(WallSolverProfile profile, GlobalWallCandidate candidate)
    {
        if (!IsSolverEligible(candidate))
        {
            return false;
        }

        if (candidate.CoherentRoomBoundaryCandidate)
        {
            return true;
        }

        var graphBacked = OriginPriority(candidate.PrimaryOrigin) == 3;
        var threshold = graphBacked
            ? profile.InitialGraphThreshold
            : profile.InitialRecoveredThreshold;
        if (candidate.LocalScore >= threshold
            && (!candidate.StrongNegativeEvidence
                || (candidate.ReadyForCoordinatePlacement
                    && candidate.RoomBoundarySupportCount >= 2
                    && candidate.StructuralEvidenceCount >= 2
                    && candidate.LocalScore >= threshold + 0.08)))
        {
            return true;
        }

        return candidate.MajorWallCandidate
            && !candidate.StrongNegativeEvidence
            && candidate.SupportedEndpointCount > 0
            && candidate.LocalScore + profile.AllowedObjectiveRegressionForMajorRecall >= threshold - 0.10;
    }

    private static bool IsSolverEligible(GlobalWallCandidate candidate)
    {
        if (candidate.CoherentRoomBoundaryCandidate)
        {
            return true;
        }

        if (OriginPriority(candidate.PrimaryOrigin) > 1
            || candidate.ReadyForCoordinatePlacement
            || !candidate.RequiresReview)
        {
            return true;
        }

        return !string.Equals(candidate.WallType, "Unknown", StringComparison.OrdinalIgnoreCase)
            && !candidate.StrongNegativeEvidence
            && candidate.StructuralEvidenceCount >= 2
            && candidate.RoomBoundarySupportCount > 0;
    }

    private static GlobalWallMetrics Evaluate(
        IReadOnlySet<string> selectedIds,
        IReadOnlyList<GlobalWallCandidate> candidates,
        IReadOnlyList<PlacementRoomExport> rooms,
        IReadOnlyList<PlacementOpeningExport> openings)
    {
        var selected = candidates.Where(candidate => selectedIds.Contains(candidate.Id)).ToArray();
        if (selected.Length == 0)
        {
            return GlobalWallMetrics.Empty;
        }

        var selectedRuns = PrepareSelectedRuns(selected, candidates, rooms, openings);
        var selectedLength = selectedRuns.Sum(RunLength);
        var majorCandidates = candidates
            .Where(candidate =>
                candidate.MajorWallCandidate
                && !candidate.StrongNegativeEvidence)
            .ToArray();
        var longCandidates = majorCandidates.Where(candidate => candidate.DrawingLength >= LongWallLength).ToArray();
        var majorCoverage = WeightedCoverage(majorCandidates, selectedRuns);
        var longCoverage = WeightedCoverage(longCandidates, selectedRuns);
        var supportedEndpointCount = selectedRuns.Sum(run => CountSupportedEndpoints(run, selectedRuns));
        var endpointCount = selectedRuns.Count * 2;
        var endpointConnectivity = endpointCount == 0 ? 0 : (double)supportedEndpointCount / endpointCount;
        var reliableRooms = rooms
            .Where(room =>
                room.Boundary.Count >= 4
                && room.Confidence >= 0.45
                && room.Reliability.ReadyForCoordinatePlacement
                && IsTrustedStructuralRoom(room))
            .ToArray();
        var roomClosureValues = reliableRooms
            .Select(room => RoomClosureRatio(room, selectedRuns))
            .ToArray();
        var roomClosure = roomClosureValues.Length == 0 ? 1 : roomClosureValues.Average();
        var exterior = selectedRuns
            .Where(run => string.Equals(run.WallType, "Exterior", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var exteriorEndpointCount = exterior.Length * 2;
        var exteriorSupportedEndpointCount = exterior.Sum(run => CountSupportedEndpoints(run, selectedRuns));
        var exteriorContinuity = exteriorEndpointCount == 0
            ? 1
            : (double)exteriorSupportedEndpointCount / exteriorEndpointCount;
        var duplicateLength = DuplicateLength(selectedRuns);
        var duplicateRatio = selectedLength <= 0 ? 0 : Math.Clamp(duplicateLength / selectedLength, 0, 1);
        var reviewLength = selectedRuns.Where(RunRequiresReview).Sum(RunLength);
        var reviewRatio = selectedLength <= 0 ? 0 : reviewLength / selectedLength;
        var noiseLength = selectedRuns.Where(RunIsNoise).Sum(RunLength);
        var noiseRatio = selectedLength <= 0 ? 0 : noiseLength / selectedLength;
        var averageConfidence = selectedRuns.Sum(run => run.Confidence * RunLength(run)) / selectedLength;
        var objective =
            majorCoverage * 0.34
            + longCoverage * 0.14
            + endpointConnectivity * 0.16
            + roomClosure * 0.14
            + exteriorContinuity * 0.10
            + averageConfidence * 0.12
            - duplicateRatio * 0.22
            - reviewRatio * 0.10
            - noiseRatio * 0.18;

        return new GlobalWallMetrics(
            Math.Clamp(objective, 0, 1),
            majorCoverage,
            longCoverage,
            endpointConnectivity,
            roomClosure,
            exteriorContinuity,
            duplicateRatio,
            reviewRatio,
            noiseRatio,
            averageConfidence,
            selectedLength,
            Math.Max(0, endpointCount - supportedEndpointCount),
            roomClosureValues.Count(value => value >= 0.90),
            reliableRooms.Length);
    }

    private static SolvedHypothesis SelectHypothesis(
        IReadOnlyList<SolvedHypothesis> hypotheses,
        SolvedHypothesis? structuralHypothesis,
        bool structuralPlacementIsGloballyBlocked)
    {
        if (structuralHypothesis is not null
            && structuralPlacementIsGloballyBlocked)
        {
            return structuralHypothesis;
        }

        var best = hypotheses
            .OrderByDescending(hypothesis => hypothesis.Score)
            .ThenBy(hypothesis => ProfilePreference(hypothesis.Profile.Name))
            .First();
        var recall = hypotheses
            .Where(hypothesis => hypothesis.Metrics.DuplicateLengthRatio <= 0.20)
            .Where(hypothesis => hypothesis.Metrics.NoiseLengthRatio <= 0.15)
            .OrderByDescending(hypothesis => hypothesis.Metrics.MajorWallCoverageRatio)
            .ThenByDescending(hypothesis => hypothesis.Metrics.LongWallCoverageRatio)
            .ThenByDescending(hypothesis => hypothesis.Score)
            .FirstOrDefault();
        if (recall is not null
            && recall.Metrics.MajorWallCoverageRatio >= best.Metrics.MajorWallCoverageRatio + MinimumMajorRecallGain
            && recall.Metrics.LongWallCoverageRatio + 0.005 >= best.Metrics.LongWallCoverageRatio
            && recall.Score + MaximumRecallScoreRegression >= best.Score)
        {
            best = recall;
        }

        if (structuralHypothesis is null
            || string.Equals(best.Id, structuralHypothesis.Id, StringComparison.Ordinal)
            || CanOverrideStructuralHypothesis(best, structuralHypothesis))
        {
            return best;
        }

        return structuralHypothesis;
    }

    private static bool CanOverrideStructuralHypothesis(
        SolvedHypothesis alternative,
        SolvedHypothesis structural)
    {
        var alternativeMetrics = alternative.Metrics;
        var structuralMetrics = structural.Metrics;
        var objectiveImproved =
            alternative.Score >= structural.Score + MinimumStructuralOverrideScoreGain;
        var majorRecallImproved =
            alternativeMetrics.MajorWallCoverageRatio
            >= structuralMetrics.MajorWallCoverageRatio + MinimumMajorRecallGain;
        var materiallyCleaner =
            alternative.Score >= structural.Score
            && alternativeMetrics.NoiseLengthRatio
                + MinimumStructuralOverrideNoiseReduction
                <= structuralMetrics.NoiseLengthRatio
            && alternativeMetrics.ReviewLengthRatio
                + MinimumStructuralOverrideReviewReduction
                <= structuralMetrics.ReviewLengthRatio;
        var structuralCoreIsSubstantiallyIncomplete =
            alternativeMetrics.MajorWallCoverageRatio
            >= structuralMetrics.MajorWallCoverageRatio + 0.10;
        var acceptableLengthGrowth = structuralMetrics.SelectedDrawingLength <= 0
            || alternativeMetrics.SelectedDrawingLength
            <= structuralMetrics.SelectedDrawingLength * MaximumStructuralOverrideLengthGrowth;
        if (IsDecisiveRecallRescue(
                alternative.Score,
                alternativeMetrics.Export(),
                structural.Score,
                structuralMetrics.Export()))
        {
            return true;
        }

        return (objectiveImproved || majorRecallImproved || materiallyCleaner)
            && alternative.Score + MaximumRecallScoreRegression >= structural.Score
            && alternativeMetrics.MajorWallCoverageRatio
                + MaximumStructuralOverrideMajorCoverageLoss
                >= structuralMetrics.MajorWallCoverageRatio
            && alternativeMetrics.LongWallCoverageRatio
                + MaximumStructuralOverrideLongCoverageLoss
                >= structuralMetrics.LongWallCoverageRatio
            && alternativeMetrics.RoomBoundaryClosureRatio
                + MaximumStructuralOverrideRoomClosureLoss
                >= structuralMetrics.RoomBoundaryClosureRatio
            && alternativeMetrics.DuplicateLengthRatio <= MaximumStructuralOverrideDuplicateRatio
            && alternativeMetrics.NoiseLengthRatio <= MaximumStructuralOverrideNoiseRatio
            && alternativeMetrics.ReviewLengthRatio
                <= Math.Max(
                    MaximumStructuralOverrideReviewRatio,
                    structuralMetrics.ReviewLengthRatio
                        + MaximumStructuralOverrideReviewRatioGrowth)
            && (acceptableLengthGrowth || structuralCoreIsSubstantiallyIncomplete);
    }

    internal static bool IsDecisiveRecallRescue(
        double alternativeScore,
        PlacementWallHypothesisMetricsExport alternative,
        double structuralScore,
        PlacementWallHypothesisMetricsExport structural)
    {
        var acceptableLengthGrowth = structural.SelectedDrawingLength <= 0
            || alternative.SelectedDrawingLength
            <= structural.SelectedDrawingLength * MaximumStructuralOverrideLengthGrowth;
        return alternativeScore
                >= structuralScore + MinimumDecisiveRecallScoreGain
            && alternative.MajorWallCoverageRatio
                >= MinimumDecisiveAbsoluteRecall
            && alternative.LongWallCoverageRatio
                >= MinimumDecisiveAbsoluteRecall
            && alternative.MajorWallCoverageRatio
                >= structural.MajorWallCoverageRatio + MinimumDecisiveMajorRecallGain
            && alternative.LongWallCoverageRatio
                >= structural.LongWallCoverageRatio + MinimumDecisiveLongRecallGain
            && alternative.EndpointConnectivityRatio
                + MaximumDecisiveEndpointConnectivityLoss
                >= structural.EndpointConnectivityRatio
            && alternative.RoomBoundaryClosureRatio
                + MaximumDecisiveRoomClosureLoss
                >= structural.RoomBoundaryClosureRatio
            && alternative.DuplicateLengthRatio <= MaximumDecisiveDuplicateRatio
            && alternative.NoiseLengthRatio <= MaximumDecisiveNoiseRatio
            && alternative.ReviewLengthRatio <= MaximumDecisiveReviewRatio
            && acceptableLengthGrowth;
    }

    private static SolvedRunsBuildResult BuildSolvedRuns(
        IReadOnlyList<GlobalWallCandidate> selected,
        IReadOnlyList<GlobalWallCandidate> allCandidates,
        IReadOnlyList<PlacementRoomExport> rooms,
        IReadOnlyList<PlacementOpeningExport> openings,
        IReadOnlyList<string> trustedCoordinateRecoveredCandidateIds)
    {
        var trustedCoordinateRecoveredIds = trustedCoordinateRecoveredCandidateIds
            .ToHashSet(StringComparer.Ordinal);
        var compacted = PrepareSelectedRuns(selected, allCandidates, rooms, openings);
        var endpointClusters = BuildEndpointClusters(compacted);
        var runs = new List<PlacementSolvedWallRunExport>();

        for (var index = 0; index < compacted.Count; index++)
        {
            var run = compacted[index];
            var runId = $"wall-solution:run:{index + 1}";
            var startCluster = FindEndpointCluster(endpointClusters, index, isStart: true);
            var endCluster = FindEndpointCluster(endpointClusters, index, isStart: false);
            var adjustedLine = new LineExport(
                PointExport.From(startCluster.Position),
                PointExport.From(endCluster.Position));
            var reconciliation = BuildReconciliationExport(run, adjustedLine);
            var scale = run.MillimetersPerDrawingUnit;
            var adjustedMetricLine = scale is > 0
                ? ScaleLine(adjustedLine, scale.Value)
                : null;
            var drawingLength = LineLength(adjustedLine);
            var bounds = BoundsFor(adjustedLine, run.ThicknessDrawingUnits);
            var trustedBridgeRecovery = CanPromoteRecoveredRun(
                run.Contributors.Select(candidate => candidate.Id),
                run.Contributors
                    .Where(candidate => candidate.RequiresReview)
                    .Select(candidate => candidate.Id),
                trustedCoordinateRecoveredIds);
            var ready = trustedBridgeRecovery
                || run.Contributors.Any(candidate => candidate.ReadyForCoordinatePlacement);
            var requiresReview = RunRequiresReview(run) && !trustedBridgeRecovery;
            var reliabilityReasons = run.Contributors
                .Where(candidate =>
                    candidate.RequiresReview
                    && !trustedCoordinateRecoveredIds.Contains(candidate.Id))
                .Select(candidate => $"candidate {candidate.Id} requires review")
                .Concat(!trustedBridgeRecovery
                    && run.Contributors.All(candidate => OriginPriority(candidate.PrimaryOrigin) < 3)
                    ? new[] { "run was recovered without a clean placement graph contributor" }
                    : Array.Empty<string>())
                .Concat(trustedBridgeRecovery
                    ? new[]
                    {
                        "global topology promoted a source-backed MainStructural bridge with selected-run endpoint support"
                    }
                    : Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var candidateIds = run.Contributors.Select(candidate => candidate.Id).Order(StringComparer.Ordinal).ToArray();
            var origins = run.Contributors.SelectMany(candidate => candidate.Origins).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var sourceWallIds = run.Contributors.SelectMany(candidate => candidate.SourceWallIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var sourceGraphEdgeIds = run.Contributors.SelectMany(candidate => candidate.SourceWallGraphEdgeIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var sourcePrimitiveIds = run.Contributors.SelectMany(candidate => candidate.SourcePrimitiveIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var sourceLayers = run.Contributors.SelectMany(candidate => candidate.SourceLayers).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
            var reliability = new PlacementReliabilityExport(
                ready,
                ready
                    && scale is > 0
                    && (trustedBridgeRecovery
                        || run.Contributors.Any(candidate => candidate.ReadyForMetricPlacement)),
                requiresReview,
                run.Confidence,
                reliabilityReasons);
            var openingIntervals = BuildOpeningIntervals(
                runId,
                run,
                adjustedLine,
                scale,
                openings);
            var solidIntervals = BuildSolidIntervals(
                runId,
                run,
                adjustedLine,
                scale,
                reliability,
                openingIntervals);
            var solidDrawingLength = solidIntervals.Sum(interval => interval.DrawingLength);
            var openingDrawingLength = Math.Max(0, drawingLength - solidDrawingLength);
            var canonicalExtentEvidence = run.Contributors
                .SelectMany(candidate => candidate.Evidence)
                .Where(item => item.StartsWith(
                    "canonical structural run extent",
                    StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var wallTypeResolutionEvidence =
                string.Equals(run.WallType, "Exterior", StringComparison.OrdinalIgnoreCase)
                && UsesAuthoritativeExteriorShellTypeResolution(run.Contributors)
                    ? new[]
                    {
                        "global wall solver resolved mixed wall type as Exterior from placement-ready exterior-shell provenance without conflicting two-sided room-boundary evidence"
                    }
                    : Array.Empty<string>();
            var evidence = new[]
                {
                    $"global wall solver compacted {candidateIds.Length} selected candidate(s)",
                    $"global wall solver candidate origins: {string.Join(",", origins)}",
                    $"global wall solver local score range {run.Contributors.Min(candidate => candidate.LocalScore):0.###}-{run.Contributors.Max(candidate => candidate.LocalScore):0.###}",
                    run.CompletedJunctionCount > 0
                        ? $"global wall solver completed {run.CompletedJunctionCount} source-backed junction(s)"
                        : "global wall solver retained source wall endpoints",
                    run.BodyContactJunctionCount > 0
                        ? $"global wall solver normalized {run.BodyContactJunctionCount} wall-body contact endpoint(s) to shared main-structural centerline junctions"
                        : "global wall solver required no extended wall-body contact normalization",
                    openingIntervals.Count > 0
                        ? $"canonical host wall carries {openingIntervals.Count} anchored opening interval(s)"
                        : "canonical wall has no anchored opening intervals",
                    run.BridgedOpeningIds.Count > 0
                        ? $"opening-aware reconstruction joined {run.BridgedOpeningIds.Count} source gap(s)"
                        : "canonical wall did not require opening-gap reconstruction",
                    $"wall evidence reconciliation status {reconciliation.Status}",
                    $"wall evidence reconciliation confidence {reconciliation.Confidence:0.###}"
                }
                .Concat(wallTypeResolutionEvidence)
                .Concat(canonicalExtentEvidence)
                .Concat(run.Contributors.SelectMany(candidate => candidate.Evidence))
                .Concat(reconciliation.Evidence)
                .Distinct(StringComparer.Ordinal)
                .Take(48)
                .ToArray();

            runs.Add(new PlacementSolvedWallRunExport(
                runId,
                run.PageNumber,
                startCluster.Id,
                endCluster.Id,
                run.WallType,
                adjustedLine,
                adjustedMetricLine,
                RectExport.From(bounds),
                scale is > 0 ? ScaleRect(bounds, scale.Value) : null,
                drawingLength,
                scale is > 0 ? drawingLength * scale.Value / 1000.0 : null,
                run.ThicknessDrawingUnits,
                scale is > 0 ? run.ThicknessDrawingUnits * scale.Value : null,
                scale,
                solidDrawingLength,
                scale is > 0 ? solidDrawingLength * scale.Value / 1000.0 : null,
                openingDrawingLength,
                run.BridgedOpeningIds.Count,
                run.Confidence,
                reliability,
                openingIntervals,
                solidIntervals,
                candidateIds,
                origins,
                sourceWallIds,
                sourceGraphEdgeIds,
                sourcePrimitiveIds,
                sourceLayers,
                Array.Empty<PlacementSolvedWallInlineJunctionExport>(),
                reconciliation,
                evidence)
            {
                HasCoherentRoomBoundarySupport =
                    run.Contributors.Any(candidate => candidate.CoherentRoomBoundaryCandidate)
            });
        }

        var topology = BuildInlineJunctionTopology(runs);
        return new SolvedRunsBuildResult(
            topology.Runs,
            BuildReconciliationSummary(topology.Runs),
            topology.Summary);
    }

    internal static bool CanPromoteRecoveredRun(
        IEnumerable<string> contributorIds,
        IEnumerable<string> reviewContributorIds,
        IReadOnlySet<string> trustedRecoveredCandidateIds)
    {
        return contributorIds.Any(trustedRecoveredCandidateIds.Contains)
            && reviewContributorIds.All(trustedRecoveredCandidateIds.Contains);
    }

    private static IReadOnlyList<CompactedWallRun> PrepareSelectedRuns(
        IReadOnlyList<GlobalWallCandidate> selected,
        IReadOnlyList<GlobalWallCandidate> allCandidates,
        IReadOnlyList<PlacementRoomExport> rooms,
        IReadOnlyList<PlacementOpeningExport> openings) =>
        CollapseReconciledDuplicateRuns(
            ReconcileWallEvidence(
            ReconstructOpeningAwareHostRuns(
                CollapseCompetingSourceRuns(
                    CompactSelectedCandidates(selected)),
                openings),
            allCandidates,
            rooms,
            openings));

    private static IReadOnlyList<CompactedWallRun> CollapseReconciledDuplicateRuns(
        IReadOnlyList<CompactedWallRun> runs)
    {
        var retained = new List<CompactedWallRun>();
        foreach (var run in runs
                     .OrderByDescending(RunSelectionPriority)
                     .ThenByDescending(RunLength)
                     .ThenBy(item => item.PageNumber)
                     .ThenBy(item => AxisCoordinate(item.CenterLine))
                     .ThenBy(item => IntervalStart(item.CenterLine)))
        {
            var duplicateIndex = retained.FindIndex(existing =>
                AreReconciledDuplicateRuns(existing, run));
            if (duplicateIndex < 0)
            {
                retained.Add(run);
                continue;
            }

            retained[duplicateIndex] = MergeReconciledDuplicateRuns(
                retained[duplicateIndex],
                run);
        }

        return retained
            .OrderBy(run => run.PageNumber)
            .ThenBy(run => Math.Min(run.CenterLine.Start.Y, run.CenterLine.End.Y))
            .ThenBy(run => Math.Min(run.CenterLine.Start.X, run.CenterLine.End.X))
            .ThenBy(run => run.WallType, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool AreReconciledDuplicateRuns(
        CompactedWallRun first,
        CompactedWallRun second)
    {
        if (first.PageNumber != second.PageNumber
            || Orientation(first.CenterLine) == WallOrientation.Diagonal
            || Orientation(first.CenterLine) != Orientation(second.CenterLine))
        {
            return false;
        }

        var axisTolerance = ReconciledDuplicateAxisTolerance(
            first.ThicknessDrawingUnits,
            second.ThicknessDrawingUnits,
            SharesSourceWall(first, second),
            HasStructuralCoreContributor(first),
            HasStructuralCoreContributor(second),
            HasCleanGraphContributor(first),
            HasCleanGraphContributor(second));
        if (Math.Abs(AxisCoordinate(first.CenterLine) - AxisCoordinate(second.CenterLine))
            > axisTolerance)
        {
            return false;
        }

        var minimumLength = Math.Min(RunLength(first), RunLength(second));
        if (minimumLength <= 0)
        {
            return false;
        }

        var overlapRatio = ProjectedOverlapLength(
            first.CenterLine,
            second.CenterLine) / minimumLength;
        if (overlapRatio < 0.80)
        {
            return false;
        }

        if (CompatibleWallTypes(first.WallType, second.WallType))
        {
            return true;
        }

        var maximumLength = Math.Max(RunLength(first), RunLength(second));
        return overlapRatio >= 0.95
            && maximumLength >= minimumLength * 1.5;
    }

    internal static double ReconciledDuplicateAxisTolerance(
        double firstThickness,
        double secondThickness,
        bool sharesSourceWall,
        bool firstHasStructuralCore,
        bool secondHasStructuralCore,
        bool firstHasCleanGraph,
        bool secondHasCleanGraph)
    {
        var baseTolerance = Math.Clamp(
            Math.Min(firstThickness, secondThickness) * 0.25,
            0.75,
            1.5);
        var sharedSourceAcrossGraphAndStructure =
            sharesSourceWall
            && (firstHasStructuralCore || secondHasStructuralCore)
            && firstHasCleanGraph != secondHasCleanGraph;
        return sharedSourceAcrossGraphAndStructure
            ? Math.Min(
                8.0,
                Math.Max(
                    baseTolerance,
                    ((firstThickness + secondThickness) / 2.0) + 0.75))
            : baseTolerance;
    }

    private static bool SharesSourceWall(
        CompactedWallRun first,
        CompactedWallRun second)
    {
        var firstSourceWallIds = first.Contributors
            .SelectMany(candidate => candidate.SourceWallIds)
            .ToHashSet(StringComparer.Ordinal);
        return second.Contributors
            .SelectMany(candidate => candidate.SourceWallIds)
            .Any(firstSourceWallIds.Contains);
    }

    private static bool HasCleanGraphContributor(CompactedWallRun run) =>
        run.Contributors.Any(candidate =>
            string.Equals(
                candidate.PrimaryOrigin,
                "CleanGraph",
                StringComparison.Ordinal));

    private static CompactedWallRun MergeReconciledDuplicateRuns(
        CompactedWallRun preferred,
        CompactedWallRun duplicate)
    {
        var contributors = preferred.Contributors
            .Concat(duplicate.Contributors)
            .GroupBy(candidate => candidate.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var preferredState = preferred.Reconciliation
            ?? WallReconciliationState.Unchanged(preferred.CenterLine);
        var duplicateState = duplicate.Reconciliation
            ?? WallReconciliationState.Unchanged(duplicate.CenterLine);
        var duplicateSourceIds = duplicate.Contributors
            .SelectMany(candidate => candidate.SourceWallIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return preferred with
        {
            WallType = string.Equals(preferred.WallType, "Unknown", StringComparison.OrdinalIgnoreCase)
                ? duplicate.WallType
                : preferred.WallType,
            MillimetersPerDrawingUnit = preferred.MillimetersPerDrawingUnit
                ?? duplicate.MillimetersPerDrawingUnit,
            Confidence = Math.Max(preferred.Confidence, duplicate.Confidence),
            Contributors = contributors,
            CompletedJunctionCount = Math.Max(
                preferred.CompletedJunctionCount,
                duplicate.CompletedJunctionCount),
            BodyContactJunctionCount = Math.Max(
                preferred.BodyContactJunctionCount,
                duplicate.BodyContactJunctionCount),
            BridgedOpeningIds = preferred.BridgedOpeningIds
                .Concat(duplicate.BridgedOpeningIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            Reconciliation = preferredState with
            {
                CandidateVoteCount = Math.Max(
                    preferredState.CandidateVoteCount,
                    duplicateState.CandidateVoteCount),
                RoomBoundaryVoteCount = Math.Max(
                    preferredState.RoomBoundaryVoteCount,
                    duplicateState.RoomBoundaryVoteCount),
                OpeningVoteCount = Math.Max(
                    preferredState.OpeningVoteCount,
                    duplicateState.OpeningVoteCount),
                NeighborVoteCount = Math.Max(
                    preferredState.NeighborVoteCount,
                    duplicateState.NeighborVoteCount),
                JunctionSnapCount = Math.Max(
                    preferredState.JunctionSnapCount,
                    duplicateState.JunctionSnapCount),
                CollapsedDuplicateRunCount =
                    preferredState.CollapsedDuplicateRunCount
                    + duplicateState.CollapsedDuplicateRunCount
                    + 1,
                Confidence = Math.Max(
                    preferredState.Confidence,
                    duplicateState.Confidence),
                Evidence = preferredState.Evidence
                    .Concat(duplicateState.Evidence)
                    .Append(
                        $"reconciler collapsed one near-coincident duplicate run and preserved source wall provenance {string.Join(",", duplicateSourceIds)}")
                    .Distinct(StringComparer.Ordinal)
                    .Take(24)
                    .ToArray()
            }
        };
    }

    private static IReadOnlyList<CompactedWallRun> CompactSelectedCandidates(
        IReadOnlyList<GlobalWallCandidate> selected)
    {
        var groups = new List<List<GlobalWallCandidate>>();
        foreach (var candidate in selected
                     .OrderBy(candidate => candidate.PageNumber)
                     .ThenBy(candidate => Orientation(candidate.CenterLine))
                     .ThenBy(candidate => AxisCoordinate(candidate.CenterLine))
                     .ThenBy(candidate => IntervalStart(candidate.CenterLine))
                     .ThenBy(candidate => candidate.Id, StringComparer.Ordinal))
        {
            var group = groups.FirstOrDefault(existing => CanCompact(existing, candidate));
            if (group is null)
            {
                groups.Add([candidate]);
            }
            else
            {
                group.Add(candidate);
            }
        }

        return groups
            .Select(CompactGroup)
            .OrderBy(run => run.PageNumber)
            .ThenBy(run => Math.Min(run.CenterLine.Start.Y, run.CenterLine.End.Y))
            .ThenBy(run => Math.Min(run.CenterLine.Start.X, run.CenterLine.End.X))
            .ThenBy(run => run.WallType, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool CanCompact(IReadOnlyList<GlobalWallCandidate> group, GlobalWallCandidate candidate)
    {
        var first = group[0];
        var typesCompatible = CompatibleWallTypes(first.WallType, candidate.WallType)
            || ConflictingTypeEquivalent(first, candidate);
        if (first.PageNumber != candidate.PageNumber
            || Orientation(first.CenterLine) != Orientation(candidate.CenterLine)
            || !typesCompatible
            || Math.Abs(AxisCoordinate(first.CenterLine) - AxisCoordinate(candidate.CenterLine)) > AxisGroupingDistance)
        {
            return false;
        }

        var groupStart = group.Min(item => IntervalStart(item.CenterLine));
        var groupEnd = group.Max(item => IntervalEnd(item.CenterLine));
        var candidateStart = IntervalStart(candidate.CenterLine);
        var candidateEnd = IntervalEnd(candidate.CenterLine);
        var gap = candidateStart > groupEnd
            ? candidateStart - groupEnd
            : groupStart > candidateEnd
                ? groupStart - candidateEnd
                : 0;
        return gap <= Math.Max(IntervalMergeGap, Math.Min(first.ThicknessDrawingUnits, candidate.ThicknessDrawingUnits) * 0.35);
    }

    private static bool ConflictingTypeEquivalent(
        GlobalWallCandidate first,
        GlobalWallCandidate second)
    {
        if (Orientation(first.CenterLine) == WallOrientation.Diagonal
            || LineDistance(first.CenterLine, second.CenterLine) > DuplicateAxisDistance)
        {
            return false;
        }

        var minimumLength = Math.Min(first.DrawingLength, second.DrawingLength);
        return minimumLength > 0
            && ProjectedOverlapLength(first.CenterLine, second.CenterLine) / minimumLength >= 0.90;
    }

    private static CompactedWallRun CompactGroup(IReadOnlyList<GlobalWallCandidate> group)
    {
        var orientation = Orientation(group[0].CenterLine);
        if (orientation == WallOrientation.Diagonal)
        {
            var candidate = group.OrderByDescending(item => item.LocalScore).First();
            return new CompactedWallRun(
                candidate.PageNumber,
                candidate.WallType,
                candidate.CenterLine,
                candidate.ThicknessDrawingUnits,
                candidate.MillimetersPerDrawingUnit,
                candidate.Confidence,
                group.ToArray(),
                0,
                Array.Empty<string>());
        }

        var weights = group.Select(candidate => Math.Max(0.1, candidate.LocalScore) * candidate.DrawingLength).ToArray();
        var totalWeight = weights.Sum();
        var axis = group.Select((candidate, index) => AxisCoordinate(candidate.CenterLine) * weights[index]).Sum() / totalWeight;
        var fullStart = group.Min(candidate => IntervalStart(candidate.CenterLine));
        var fullEnd = group.Max(candidate => IntervalEnd(candidate.CenterLine));
        var extentContributors = group
            .Where(CandidateCanExtendCanonicalExtent)
            .ToArray();
        var start = extentContributors.Length > 0
            ? extentContributors.Min(candidate => IntervalStart(candidate.CenterLine))
            : fullStart;
        var end = extentContributors.Length > 0
            ? extentContributors.Max(candidate => IntervalEnd(candidate.CenterLine))
            : fullEnd;
        var constrainedStart = start > fullStart + 0.01;
        var constrainedEnd = end < fullEnd - 0.01;
        var contributors = constrainedStart || constrainedEnd
            ? group.Select(candidate => candidate with
            {
                Evidence = candidate.Evidence
                        .Append(
                            $"canonical extent constrained to trusted support {start:0.###}-{end:0.###}; weak review-only contributors could corroborate overlap but could not extend the wall")
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
            })
                .ToArray()
            : group.ToArray();
        var line = orientation == WallOrientation.Horizontal
            ? new LineExport(new PointExport(start, axis), new PointExport(end, axis))
            : new LineExport(new PointExport(axis, start), new PointExport(axis, end));
        var thickness = WeightedAverage(group, candidate => Math.Max(0.5, candidate.ThicknessDrawingUnits));
        var confidence = WeightedAverage(group, candidate => candidate.Confidence);
        var scale = group
            .Where(candidate => candidate.MillimetersPerDrawingUnit is > 0)
            .OrderByDescending(candidate => candidate.LocalScore)
            .Select(candidate => candidate.MillimetersPerDrawingUnit)
            .FirstOrDefault();

        return new CompactedWallRun(
            group[0].PageNumber,
            ResolveWallType(contributors),
            line,
            thickness,
            scale,
            confidence,
            contributors,
            0,
            Array.Empty<string>());
    }

    private static bool CandidateCanExtendCanonicalExtent(GlobalWallCandidate candidate)
    {
        if (!candidate.RequiresReview)
        {
            return true;
        }

        if (candidate.StrongNegativeEvidence)
        {
            return false;
        }

        if (candidate.SupportedEndpointCount >= 2
            || candidate.RoomBoundarySupportCount > 0
            || candidate.OpeningSupportCount > 0)
        {
            return true;
        }

        return candidate.WeakNegativeEvidenceCount == 0
            && candidate.StructuralEvidenceCount >= 2
            && candidate.LocalScore >= 0.85;
    }

    private static IReadOnlyList<CompactedWallRun> CollapseCompetingSourceRuns(
        IReadOnlyList<CompactedWallRun> runs)
    {
        var retained = new List<CompactedWallRun>();
        foreach (var run in runs
                     .OrderByDescending(RunSelectionPriority)
                     .ThenBy(run => run.PageNumber)
                     .ThenBy(run => Math.Min(run.CenterLine.Start.Y, run.CenterLine.End.Y))
                     .ThenBy(run => Math.Min(run.CenterLine.Start.X, run.CenterLine.End.X)))
        {
            var competingIndex = retained.FindIndex(existing =>
                AreCompetingSourceRuns(existing, run));
            if (competingIndex < 0)
            {
                retained.Add(run);
                continue;
            }

            retained[competingIndex] = MergeCompetingSourceRuns(
                retained[competingIndex],
                run);
        }

        return retained
            .OrderBy(run => run.PageNumber)
            .ThenBy(run => Math.Min(run.CenterLine.Start.Y, run.CenterLine.End.Y))
            .ThenBy(run => Math.Min(run.CenterLine.Start.X, run.CenterLine.End.X))
            .ThenBy(run => run.WallType, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool AreCompetingSourceRuns(
        CompactedWallRun first,
        CompactedWallRun second)
    {
        if (first.PageNumber != second.PageNumber
            || Orientation(first.CenterLine) == WallOrientation.Diagonal
            || Orientation(first.CenterLine) != Orientation(second.CenterLine))
        {
            return false;
        }

        if (HasStructuralCoreContributor(first)
            && HasStructuralCoreContributor(second))
        {
            return false;
        }

        var firstSourceWallIds = first.Contributors
            .SelectMany(candidate => candidate.SourceWallIds)
            .ToHashSet(StringComparer.Ordinal);
        if (!second.Contributors
            .SelectMany(candidate => candidate.SourceWallIds)
            .Any(firstSourceWallIds.Contains))
        {
            return false;
        }

        var allowedDistance = Math.Max(
            CompetingSourceRunDistance,
            Math.Min(
                24.0,
                (first.ThicknessDrawingUnits + second.ThicknessDrawingUnits) * 1.25));
        var minimumLength = Math.Min(RunLength(first), RunLength(second));
        return minimumLength > 0
            && LineDistance(first.CenterLine, second.CenterLine) <= allowedDistance
            && ProjectedOverlapLength(first.CenterLine, second.CenterLine) / minimumLength >= 0.65;
    }

    private static bool HasStructuralCoreContributor(CompactedWallRun run) =>
        run.Contributors.Any(candidate =>
            string.Equals(
                candidate.PrimaryOrigin,
                "StructuralCore",
                StringComparison.Ordinal));

    private static CompactedWallRun MergeCompetingSourceRuns(
        CompactedWallRun preferred,
        CompactedWallRun alternative)
    {
        var contributors = preferred.Contributors
            .Concat(alternative.Contributors)
            .GroupBy(candidate => candidate.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return preferred with
        {
            WallType = ResolveWallType(contributors),
            MillimetersPerDrawingUnit = preferred.MillimetersPerDrawingUnit
                ?? alternative.MillimetersPerDrawingUnit,
            Confidence = Math.Max(preferred.Confidence, alternative.Confidence),
            Contributors = contributors,
            CompletedJunctionCount = preferred.CompletedJunctionCount
                + alternative.CompletedJunctionCount
        };
    }

    private static double RunSelectionPriority(CompactedWallRun run)
    {
        var maximumOrigin = run.Contributors.Max(candidate => OriginPriority(candidate.PrimaryOrigin));
        var cleanReady = run.Contributors.Any(candidate =>
            OriginPriority(candidate.PrimaryOrigin) == 3
            && candidate.ReadyForCoordinatePlacement);
        var structural = run.Contributors.Max(candidate => candidate.StructuralEvidenceCount);
        var maximumLocalScore = run.Contributors.Max(candidate => candidate.LocalScore);
        return maximumOrigin * 10.0
            + (cleanReady ? 4.0 : 0)
            + Math.Min(3.0, structural * 0.5)
            + maximumLocalScore
            + run.Confidence * 0.5;
    }

    private static IReadOnlyList<CompactedWallRun> ReconstructOpeningAwareHostRuns(
        IReadOnlyList<CompactedWallRun> runs,
        IReadOnlyList<PlacementOpeningExport> openings)
    {
        var current = runs.ToList();
        var eligibleOpenings = openings
            .Where(OpeningCanAnchorCanonicalWall)
            .OrderBy(opening => opening.PageNumber)
            .ThenBy(opening => opening.Bounds.Y)
            .ThenBy(opening => opening.Bounds.X)
            .ThenBy(opening => opening.Id, StringComparer.Ordinal)
            .ToArray();
        if (current.Count < 2 || eligibleOpenings.Length == 0)
        {
            return current;
        }

        var maximumPasses = Math.Max(1, Math.Min(64, current.Count + eligibleOpenings.Length));
        for (var pass = 0; pass < maximumPasses; pass++)
        {
            OpeningBridgeCandidate? best = null;
            for (var firstIndex = 0; firstIndex < current.Count; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < current.Count; secondIndex++)
                {
                    foreach (var opening in eligibleOpenings)
                    {
                        if (!TryBuildOpeningBridge(
                                current[firstIndex],
                                current[secondIndex],
                                opening,
                                out var bridge))
                        {
                            continue;
                        }

                        var candidate = new OpeningBridgeCandidate(
                            firstIndex,
                            secondIndex,
                            opening.Id,
                            bridge,
                            GapBetween(current[firstIndex].CenterLine, current[secondIndex].CenterLine));
                        if (best is null
                            || candidate.GapDrawingUnits < best.GapDrawingUnits - 0.001
                            || (Math.Abs(candidate.GapDrawingUnits - best.GapDrawingUnits) <= 0.001
                                && string.CompareOrdinal(candidate.OpeningId, best.OpeningId) < 0))
                        {
                            best = candidate;
                        }
                    }
                }
            }

            if (best is null)
            {
                break;
            }

            current[best.FirstIndex] = best.MergedRun;
            current.RemoveAt(best.SecondIndex);
        }

        return current
            .OrderBy(run => run.PageNumber)
            .ThenBy(run => Math.Min(run.CenterLine.Start.Y, run.CenterLine.End.Y))
            .ThenBy(run => Math.Min(run.CenterLine.Start.X, run.CenterLine.End.X))
            .ThenBy(run => run.WallType, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryBuildOpeningBridge(
        CompactedWallRun first,
        CompactedWallRun second,
        PlacementOpeningExport opening,
        out CompactedWallRun merged)
    {
        merged = first;
        var firstOrientation = Orientation(first.CenterLine);
        var secondOrientation = Orientation(second.CenterLine);
        if (first.PageNumber != second.PageNumber
            || first.PageNumber != opening.PageNumber
            || firstOrientation == WallOrientation.Diagonal
            || firstOrientation != secondOrientation
            || !CompatibleWallTypes(first.WallType, second.WallType)
            || opening.Placement is null
            || Orientation(opening.Placement.ReferenceLine) != firstOrientation)
        {
            return false;
        }

        var gap = GapBetween(first.CenterLine, second.CenterLine);
        if (gap <= IntervalMergeGap)
        {
            return false;
        }

        var placement = opening.Placement;
        var thickness = Math.Max(first.ThicknessDrawingUnits, second.ThicknessDrawingUnits);
        var axisTolerance = Math.Max(
            AxisGroupingDistance,
            Math.Max(thickness * 1.25, placement.DepthDrawingUnits * 0.75));
        var weightedAxis = (
            AxisCoordinate(first.CenterLine) * Math.Max(1, RunLength(first))
            + AxisCoordinate(second.CenterLine) * Math.Max(1, RunLength(second)))
            / (Math.Max(1, RunLength(first)) + Math.Max(1, RunLength(second)));
        if (Math.Abs(AxisCoordinate(first.CenterLine) - AxisCoordinate(second.CenterLine)) > axisTolerance
            || Math.Abs(AxisCoordinate(placement.ReferenceLine) - weightedAxis) > axisTolerance)
        {
            return false;
        }

        var firstSourceIds = SourceWallIds(first);
        var secondSourceIds = SourceWallIds(second);
        var openingSourceIds = OpeningSourceHostWallIds(opening);
        var firstSourceLinked = openingSourceIds.Any(firstSourceIds.Contains);
        var secondSourceLinked = openingSourceIds.Any(secondSourceIds.Contains);
        var firstStructurallyReady = RunSupportsOpeningBridge(first);
        var secondStructurallyReady = RunSupportsOpeningBridge(second);
        if ((!firstSourceLinked || !secondSourceLinked)
            && (!(firstSourceLinked || secondSourceLinked)
                || opening.Confidence < 0.65
                || !firstStructurallyReady
                || !secondStructurallyReady))
        {
            return false;
        }

        var start = Math.Min(IntervalStart(first.CenterLine), IntervalStart(second.CenterLine));
        var end = Math.Max(IntervalEnd(first.CenterLine), IntervalEnd(second.CenterLine));
        var logicalLine = firstOrientation == WallOrientation.Horizontal
            ? new LineExport(new PointExport(start, weightedAxis), new PointExport(end, weightedAxis))
            : new LineExport(new PointExport(weightedAxis, start), new PointExport(weightedAxis, end));
        var openingStart = Math.Min(
            ProjectParameter(logicalLine, placement.StartPoint),
            ProjectParameter(logicalLine, placement.EndPoint));
        var openingEnd = Math.Max(
            ProjectParameter(logicalLine, placement.StartPoint),
            ProjectParameter(logicalLine, placement.EndPoint));
        var firstEnd = Math.Min(IntervalEnd(first.CenterLine), IntervalEnd(second.CenterLine));
        var secondStart = Math.Max(IntervalStart(first.CenterLine), IntervalStart(second.CenterLine));
        var logicalLength = LineLength(logicalLine);
        var openingStartOffset = openingStart * logicalLength;
        var openingEndOffset = openingEnd * logicalLength;
        var firstEndOffset = firstEnd - start;
        var secondStartOffset = secondStart - start;
        var gapTolerance = Math.Max(2.0, Math.Max(thickness, placement.DepthDrawingUnits * 0.5));
        var maximumGap = Math.Max(
            8.0,
            Math.Min(
                160.0,
                Math.Max(opening.DrawingWidth, placement.LengthDrawingUnits) * 1.5 + gapTolerance * 2.0));
        if (gap > maximumGap
            || openingStartOffset > firstEndOffset + gapTolerance
            || openingEndOffset < secondStartOffset - gapTolerance)
        {
            return false;
        }

        var contributors = first.Contributors
            .Concat(second.Contributors)
            .GroupBy(candidate => candidate.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var scale = first.MillimetersPerDrawingUnit
            ?? second.MillimetersPerDrawingUnit;
        merged = new CompactedWallRun(
            first.PageNumber,
            ResolveWallType(contributors),
            logicalLine,
            WeightedRunValue(first, second, run => run.ThicknessDrawingUnits),
            scale,
            WeightedRunValue(first, second, run => run.Confidence),
            contributors,
            first.CompletedJunctionCount + second.CompletedJunctionCount,
            first.BridgedOpeningIds
                .Concat(second.BridgedOpeningIds)
                .Append(opening.Id)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());
        return true;
    }

    private static bool OpeningCanAnchorCanonicalWall(PlacementOpeningExport opening) =>
        opening.Placement is not null
        && string.Equals(opening.PlacementStatus, "Anchored", StringComparison.Ordinal)
        && opening.Reliability.ReadyForCoordinatePlacement
        && opening.DrawingWidth > 0.5
        && opening.Confidence >= 0.45;

    private static bool RunSupportsOpeningBridge(CompactedWallRun run) =>
        !run.Contributors.All(candidate => candidate.StrongNegativeEvidence)
        && run.Contributors.Any(candidate =>
            candidate.ReadyForCoordinatePlacement
            && !candidate.ExcludedFromStructuralTopology);

    private static HashSet<string> SourceWallIds(CompactedWallRun run) =>
        run.Contributors
            .SelectMany(candidate => candidate.SourceWallIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> OpeningSourceHostWallIds(PlacementOpeningExport opening) =>
        opening.HostWallIds
            .Concat(opening.Placement?.AnchorWallIds ?? Array.Empty<string>())
            .Concat(opening.Placement?.HostWallId is { Length: > 0 } hostWallId
                ? new[] { hostWallId }
                : Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

    private static double WeightedRunValue(
        CompactedWallRun first,
        CompactedWallRun second,
        Func<CompactedWallRun, double> selector)
    {
        var firstWeight = Math.Max(1, RunLength(first));
        var secondWeight = Math.Max(1, RunLength(second));
        return (selector(first) * firstWeight + selector(second) * secondWeight)
            / (firstWeight + secondWeight);
    }

    private static double GapBetween(LineExport first, LineExport second)
    {
        var firstStart = IntervalStart(first);
        var firstEnd = IntervalEnd(first);
        var secondStart = IntervalStart(second);
        var secondEnd = IntervalEnd(second);
        if (firstEnd < secondStart)
        {
            return secondStart - firstEnd;
        }

        return secondEnd < firstStart ? firstStart - secondEnd : 0;
    }

    private static IReadOnlyList<CompactedWallRun> CompleteSupportedJunctions(
        IReadOnlyList<CompactedWallRun> runs,
        IReadOnlyList<PlacementOpeningExport> openings)
    {
        var current = runs.ToArray();
        for (var pass = 0; pass < 2; pass++)
        {
            var changed = false;
            var next = current.ToArray();
            for (var index = 0; index < current.Length; index++)
            {
                var run = current[index];
                if (!RunSupportsJunctionCompletion(run)
                    || Orientation(run.CenterLine) == WallOrientation.Diagonal)
                {
                    continue;
                }

                var start = CompleteJunctionEndpoint(
                    run,
                    run.CenterLine.Start,
                    current,
                    index,
                    openings);
                var end = CompleteJunctionEndpoint(
                    run,
                    run.CenterLine.End,
                    current,
                    index,
                    openings);
                var completed = 0;
                if (Distance(ToPlanPoint(start.Position), ToPlanPoint(run.CenterLine.Start)) > 0.01)
                {
                    completed++;
                }

                if (Distance(ToPlanPoint(end.Position), ToPlanPoint(run.CenterLine.End)) > 0.01)
                {
                    completed++;
                }

                var adjusted = new LineExport(start.Position, end.Position);
                if (completed == 0 || LineLength(adjusted) <= 0.5)
                {
                    continue;
                }

                next[index] = run with
                {
                    CenterLine = adjusted,
                    CompletedJunctionCount = run.CompletedJunctionCount + completed,
                    BodyContactJunctionCount = run.BodyContactJunctionCount
                        + (start.SourceBackedBodyContact ? 1 : 0)
                        + (end.SourceBackedBodyContact ? 1 : 0)
                };
                changed = true;
            }

            current = next;
            if (!changed)
            {
                break;
            }
        }

        return current;
    }

    private static IReadOnlyList<CompactedWallRun> TrimSupportedExteriorCornerOverruns(
        IReadOnlyList<CompactedWallRun> runs)
    {
        var current = runs.ToArray();
        var normalized = current.ToArray();
        for (var index = 0; index < current.Length; index++)
        {
            var run = current[index];
            if (!IsExteriorCornerWallType(run.WallType)
                || Orientation(run.CenterLine) == WallOrientation.Diagonal
                || !RunSupportsExteriorCornerNormalization(run)
                || run.Contributors.Any(candidate => candidate.OpeningSupportCount > 0))
            {
                continue;
            }

            var start = TrimExteriorCornerEndpoint(
                run,
                run.CenterLine.Start,
                current,
                index);
            var end = TrimExteriorCornerEndpoint(
                run,
                run.CenterLine.End,
                current,
                index);
            var trimmed = 0;
            if (Distance(ToPlanPoint(start), ToPlanPoint(run.CenterLine.Start)) > 0.01)
            {
                trimmed++;
            }

            if (Distance(ToPlanPoint(end), ToPlanPoint(run.CenterLine.End)) > 0.01)
            {
                trimmed++;
            }

            var adjusted = new LineExport(start, end);
            if (trimmed == 0 || LineLength(adjusted) <= 0.5)
            {
                continue;
            }

            normalized[index] = run with
            {
                CenterLine = adjusted,
                CompletedJunctionCount = run.CompletedJunctionCount + trimmed
            };
        }

        return normalized;
    }

    private static PointExport TrimExteriorCornerEndpoint(
        CompactedWallRun source,
        PointExport endpoint,
        IReadOnlyList<CompactedWallRun> runs,
        int sourceIndex)
    {
        if (EndpointHasIndependentStructuralSupport(
                source,
                endpoint,
                runs,
                sourceIndex))
        {
            return endpoint;
        }

        var sourceOrientation = Orientation(source.CenterLine);
        var endpointCoordinate = sourceOrientation == WallOrientation.Horizontal
            ? endpoint.X
            : endpoint.Y;
        var oppositeEndpoint = OppositeEndpoint(source.CenterLine, endpoint);
        var oppositeCoordinate = sourceOrientation == WallOrientation.Horizontal
            ? oppositeEndpoint.X
            : oppositeEndpoint.Y;
        var endpointIsMinimum = endpointCoordinate < oppositeCoordinate;
        var best = endpoint;
        var bestOverrun = double.PositiveInfinity;

        for (var index = 0; index < runs.Count; index++)
        {
            if (index == sourceIndex)
            {
                continue;
            }

            var target = runs[index];
            var targetOrientation = Orientation(target.CenterLine);
            if (target.PageNumber != source.PageNumber
                || targetOrientation == WallOrientation.Diagonal
                || targetOrientation == sourceOrientation
                || !IsExteriorCornerWallType(target.WallType)
                || !RunSupportsExteriorCornerNormalization(target))
            {
                continue;
            }

            var intersection = sourceOrientation == WallOrientation.Horizontal
                ? new PointExport(AxisCoordinate(target.CenterLine), endpoint.Y)
                : new PointExport(endpoint.X, AxisCoordinate(target.CenterLine));
            var intersectionCoordinate = sourceOrientation == WallOrientation.Horizontal
                ? intersection.X
                : intersection.Y;
            var overrun = endpointIsMinimum
                ? intersectionCoordinate - endpointCoordinate
                : endpointCoordinate - intersectionCoordinate;
            if (overrun <= MinimumReconciliationMovement)
            {
                continue;
            }

            var allowedOverrun = Math.Max(
                6.0,
                Math.Min(
                    MaximumReconciliationEndpointAdjustment,
                    source.ThicknessDrawingUnits * 1.75
                    + target.ThicknessDrawingUnits * 0.50));
            if (overrun > allowedOverrun
                || Distance(
                    ToPlanPoint(intersection),
                    ToPlanPoint(oppositeEndpoint)) <= 0.5)
            {
                continue;
            }

            var targetProjection = targetOrientation == WallOrientation.Horizontal
                ? intersection.X
                : intersection.Y;
            var targetEndpointDistance = Math.Min(
                Math.Abs(targetProjection - IntervalStart(target.CenterLine)),
                Math.Abs(targetProjection - IntervalEnd(target.CenterLine)));
            var targetEndpointTolerance = Math.Max(
                JunctionProjectionTolerance,
                Math.Min(6.0, target.ThicknessDrawingUnits * 0.75));
            if (targetEndpointDistance > targetEndpointTolerance
                || overrun >= bestOverrun)
            {
                continue;
            }

            best = intersection;
            bestOverrun = overrun;
        }

        return best;
    }

    private static bool EndpointHasIndependentStructuralSupport(
        CompactedWallRun source,
        PointExport endpoint,
        IReadOnlyList<CompactedWallRun> runs,
        int sourceIndex)
    {
        var point = ToPlanPoint(endpoint);
        for (var index = 0; index < runs.Count; index++)
        {
            if (index == sourceIndex)
            {
                continue;
            }

            var other = runs[index];
            if (other.PageNumber != source.PageNumber
                || !RunSupportsExteriorCornerNormalization(other))
            {
                continue;
            }

            if (Distance(point, ToPlanPoint(other.CenterLine.Start)) <= EndpointSupportDistance
                || Distance(point, ToPlanPoint(other.CenterLine.End)) <= EndpointSupportDistance
                || PointToSegmentDistance(point, other.CenterLine) <= EndpointSupportDistance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExteriorCornerWallType(string wallType) =>
        string.Equals(wallType, "Exterior", StringComparison.OrdinalIgnoreCase)
        || string.Equals(wallType, "Mixed", StringComparison.OrdinalIgnoreCase);

    private static bool RunSupportsExteriorCornerNormalization(CompactedWallRun run) =>
        !run.Contributors.All(candidate => candidate.StrongNegativeEvidence)
        && run.Contributors.Any(candidate =>
            candidate.ReadyForCoordinatePlacement
            && !candidate.RequiresReview
            && candidate.LocalScore >= 0.75
            && candidate.StructuralEvidenceCount > 0);

    private static JunctionEndpointCompletion CompleteJunctionEndpoint(
        CompactedWallRun source,
        PointExport endpoint,
        IReadOnlyList<CompactedWallRun> runs,
        int sourceIndex,
        IReadOnlyList<PlacementOpeningExport> openings)
    {
        var sourceOrientation = Orientation(source.CenterLine);
        var best = new JunctionEndpointCompletion(
            endpoint,
            SourceBackedBodyContact: false);
        var bestDistance = double.PositiveInfinity;
        for (var index = 0; index < runs.Count; index++)
        {
            if (index == sourceIndex)
            {
                continue;
            }

            var target = runs[index];
            var targetOrientation = Orientation(target.CenterLine);
            if (target.PageNumber != source.PageNumber
                || targetOrientation == WallOrientation.Diagonal
                || targetOrientation == sourceOrientation
                || !RunSupportsJunctionCompletion(target))
            {
                continue;
            }

            var intersection = sourceOrientation == WallOrientation.Horizontal
                ? new PointExport(AxisCoordinate(target.CenterLine), endpoint.Y)
                : new PointExport(endpoint.X, AxisCoordinate(target.CenterLine));
            var projected = sourceOrientation == WallOrientation.Horizontal
                ? intersection.Y
                : intersection.X;
            if (projected < IntervalStart(target.CenterLine) - JunctionProjectionTolerance
                || projected > IntervalEnd(target.CenterLine) + JunctionProjectionTolerance)
            {
                continue;
            }

            var distance = Distance(ToPlanPoint(endpoint), ToPlanPoint(intersection));
            var ordinaryAllowedDistance = Math.Max(
                JunctionCompletionDistance,
                Math.Min(
                    8.0,
                    (source.ThicknessDrawingUnits + target.ThicknessDrawingUnits) * 0.5));
            var sourceBackedBodyContact =
                distance > ordinaryAllowedDistance + MinimumReconciliationMovement;
            var maximumDistance = sourceBackedBodyContact
                ? Math.Max(
                    ordinaryAllowedDistance,
                    Math.Min(
                        MaximumBodyContactJunctionDistance,
                        source.ThicknessDrawingUnits
                            + target.ThicknessDrawingUnits))
                : ordinaryAllowedDistance;
            if (distance > maximumDistance
                || (sourceBackedBodyContact
                    && !CanCompleteSourceBackedBodyContactJunction(
                        source,
                        endpoint,
                        target,
                        intersection,
                        runs,
                        openings))
                || distance >= bestDistance
                || Distance(ToPlanPoint(intersection), ToPlanPoint(OppositeEndpoint(source.CenterLine, endpoint)))
                    <= 0.5)
            {
                continue;
            }

            best = new JunctionEndpointCompletion(
                intersection,
                sourceBackedBodyContact);
            bestDistance = distance;
        }

        return best;
    }

    private static bool CanCompleteSourceBackedBodyContactJunction(
        CompactedWallRun source,
        PointExport endpoint,
        CompactedWallRun target,
        PointExport intersection,
        IReadOnlyList<CompactedWallRun> runs,
        IReadOnlyList<PlacementOpeningExport> openings)
    {
        if (!EndpointExtensionIsOutward(
                source.CenterLine,
                endpoint,
                intersection)
            || !CompatibleWallTypes(source.WallType, target.WallType)
            || !RunSupportsSourceBackedBodyContact(source)
            || !RunSupportsSourceBackedBodyContact(target)
            || source.Contributors.Any(candidate =>
                candidate.OpeningSupportCount > 0)
            || !SharesMainStructuralComponent(source, target)
            || EndpointSupported(source, endpoint, runs))
        {
            return false;
        }

        var targetOpenings = BuildOpeningIntervals(
            "wall-solution:body-contact-probe",
            target,
            target.CenterLine,
            target.MillimetersPerDrawingUnit,
            openings);
        if (targetOpenings.Count == 0)
        {
            return true;
        }

        var targetLength = Math.Max(0.001, RunLength(target));
        var intersectionOffset =
            Math.Clamp(ProjectParameter(target.CenterLine, intersection), 0, 1)
            * targetLength;
        foreach (var opening in targetOpenings)
        {
            if (intersectionOffset < opening.StartOffsetDrawingUnits
                    - MinimumReconciliationMovement
                || intersectionOffset > opening.EndOffsetDrawingUnits
                    + MinimumReconciliationMovement)
            {
                continue;
            }

            var jambDistance = Math.Min(
                Math.Abs(
                    intersectionOffset
                    - opening.StartOffsetDrawingUnits),
                Math.Abs(
                    intersectionOffset
                    - opening.EndOffsetDrawingUnits));
            if (jambDistance > OpeningJambJunctionTolerance)
            {
                return false;
            }
        }

        return true;
    }

    private static bool EndpointExtensionIsOutward(
        LineExport line,
        PointExport endpoint,
        PointExport intersection)
    {
        var orientation = Orientation(line);
        var opposite = OppositeEndpoint(line, endpoint);
        var endpointCoordinate = orientation == WallOrientation.Horizontal
            ? endpoint.X
            : endpoint.Y;
        var oppositeCoordinate = orientation == WallOrientation.Horizontal
            ? opposite.X
            : opposite.Y;
        var intersectionCoordinate = orientation == WallOrientation.Horizontal
            ? intersection.X
            : intersection.Y;
        return endpointCoordinate < oppositeCoordinate
            ? intersectionCoordinate
                < endpointCoordinate - MinimumReconciliationMovement
            : intersectionCoordinate
                > endpointCoordinate + MinimumReconciliationMovement;
    }

    private static bool RunSupportsSourceBackedBodyContact(
        CompactedWallRun run) =>
        !run.Contributors.All(candidate => candidate.StrongNegativeEvidence)
        && run.Contributors.Any(candidate =>
            candidate.ReadyForCoordinatePlacement
            && !candidate.RequiresReview
            && candidate.LocalScore >= 0.75
            && candidate.StructuralEvidenceCount > 0
            && candidate.SourceWallComponents.Any(component =>
                string.Equals(
                    component.Kind,
                    "MainStructural",
                    StringComparison.OrdinalIgnoreCase)));

    private static bool SharesMainStructuralComponent(
        CompactedWallRun source,
        CompactedWallRun target)
    {
        var sourceComponentIds = source.Contributors
            .SelectMany(candidate => candidate.SourceWallComponents)
            .Where(component => string.Equals(
                component.Kind,
                "MainStructural",
                StringComparison.OrdinalIgnoreCase))
            .Select(component => component.Id)
            .ToHashSet(StringComparer.Ordinal);
        return target.Contributors
            .SelectMany(candidate => candidate.SourceWallComponents)
            .Where(component => string.Equals(
                component.Kind,
                "MainStructural",
                StringComparison.OrdinalIgnoreCase))
            .Any(component => sourceComponentIds.Contains(component.Id));
    }

    private static PointExport OppositeEndpoint(LineExport line, PointExport endpoint) =>
        Distance(ToPlanPoint(endpoint), ToPlanPoint(line.Start))
            <= Distance(ToPlanPoint(endpoint), ToPlanPoint(line.End))
            ? line.End
            : line.Start;

    private static bool RunSupportsJunctionCompletion(CompactedWallRun run) =>
        !run.Contributors.All(candidate => candidate.StrongNegativeEvidence)
        && run.Contributors.Any(candidate =>
            (OriginPriority(candidate.PrimaryOrigin)
                    >= OriginPriority("CleanGraph")
                && candidate.ReadyForCoordinatePlacement)
            || candidate.RoomBoundarySupportCount > 0
            || candidate.OpeningSupportCount > 0);

    private static IReadOnlyList<PlacementSolvedWallOpeningIntervalExport> BuildOpeningIntervals(
        string runId,
        CompactedWallRun run,
        LineExport centerLine,
        double? millimetersPerDrawingUnit,
        IReadOnlyList<PlacementOpeningExport> openings)
    {
        var intervals = new List<PlacementSolvedWallOpeningIntervalExport>();
        foreach (var opening in openings
                     .Where(opening => opening.PageNumber == run.PageNumber)
                     .Where(OpeningCanAnchorCanonicalWall)
                     .OrderBy(opening => opening.Bounds.Y)
                     .ThenBy(opening => opening.Bounds.X)
                     .ThenBy(opening => opening.Id, StringComparer.Ordinal))
        {
            if (TryBuildOpeningInterval(
                    runId,
                    run,
                    centerLine,
                    millimetersPerDrawingUnit,
                    opening,
                    out var interval))
            {
                intervals.Add(interval);
            }
        }

        return intervals
            .GroupBy(interval => interval.OpeningId, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(interval => interval.AttachmentKind == "ReconstructedGap")
                .ThenByDescending(interval => interval.AttachmentKind == "SourceLinked")
                .First())
            .OrderBy(interval => interval.StartParameter)
            .ThenBy(interval => interval.EndParameter)
            .ThenBy(interval => interval.OpeningId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryBuildOpeningInterval(
        string runId,
        CompactedWallRun run,
        LineExport centerLine,
        double? millimetersPerDrawingUnit,
        PlacementOpeningExport opening,
        out PlacementSolvedWallOpeningIntervalExport interval)
    {
        interval = null!;
        var placement = opening.Placement;
        if (placement is null
            || !DirectionsAreParallel(centerLine, placement.ReferenceLine))
        {
            return false;
        }

        var runLength = LineLength(centerLine);
        if (runLength <= 0.001)
        {
            return false;
        }

        var sourceHostWallIds = OpeningSourceHostWallIds(opening)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var runSourceWallIds = SourceWallIds(run);
        var sourceLinked = sourceHostWallIds.Any(runSourceWallIds.Contains);
        var startDistance = PointToInfiniteLineDistance(placement.StartPoint, centerLine);
        var endDistance = PointToInfiniteLineDistance(placement.EndPoint, centerLine);
        var crossWallOffset = (startDistance + endDistance) / 2.0;
        var distanceTolerance = Math.Max(
            2.0,
            Math.Max(run.ThicknessDrawingUnits * 1.5, placement.DepthDrawingUnits * 1.25));
        if (crossWallOffset > distanceTolerance
            || (!sourceLinked && opening.Confidence < 0.70))
        {
            return false;
        }

        var rawStart = ProjectParameter(centerLine, placement.StartPoint);
        var rawEnd = ProjectParameter(centerLine, placement.EndPoint);
        var unclampedStart = Math.Min(rawStart, rawEnd);
        var unclampedEnd = Math.Max(rawStart, rawEnd);
        var parameterTolerance = Math.Min(0.25, distanceTolerance / runLength);
        if (unclampedEnd < -parameterTolerance
            || unclampedStart > 1.0 + parameterTolerance)
        {
            return false;
        }

        var startParameter = Math.Clamp(unclampedStart, 0, 1);
        var endParameter = Math.Clamp(unclampedEnd, 0, 1);
        if ((endParameter - startParameter) * runLength <= 0.5)
        {
            return false;
        }

        var startPoint = PointAt(centerLine, startParameter);
        var endPoint = PointAt(centerLine, endParameter);
        var intervalLine = new LineExport(startPoint, endPoint);
        var startOffset = startParameter * runLength;
        var endOffset = endParameter * runLength;
        var centerOffset = (startOffset + endOffset) / 2.0;
        var length = endOffset - startOffset;
        var attachmentKind = run.BridgedOpeningIds.Contains(opening.Id, StringComparer.Ordinal)
            ? "ReconstructedGap"
            : sourceLinked
                ? "SourceLinked"
                : "GeometryAligned";
        var evidence = opening.Evidence
            .Concat(placement.Evidence)
            .Concat(new[]
            {
                $"opening attached to canonical wall run {runId} as {attachmentKind}",
                $"opening interval {startParameter:0.######}-{endParameter:0.######} along logical host wall",
                $"cross-wall alignment offset {crossWallOffset:0.###} drawing units",
                "canonical topology retains one host wall identity while solid intervals preserve the physical opening gap"
            })
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .Take(40)
            .ToArray();

        interval = new PlacementSolvedWallOpeningIntervalExport(
            $"{runId}:opening:{opening.Id}",
            runId,
            opening.Id,
            opening.PageNumber,
            opening.Type,
            opening.Operation,
            attachmentKind,
            intervalLine,
            millimetersPerDrawingUnit is > 0
                ? ScaleLine(intervalLine, millimetersPerDrawingUnit.Value)
                : null,
            startPoint,
            ScalePoint(startPoint, millimetersPerDrawingUnit),
            endPoint,
            ScalePoint(endPoint, millimetersPerDrawingUnit),
            startParameter,
            endParameter,
            (startParameter + endParameter) / 2.0,
            startOffset,
            endOffset,
            centerOffset,
            length,
            ScaleValue(startOffset, millimetersPerDrawingUnit),
            ScaleValue(endOffset, millimetersPerDrawingUnit),
            ScaleValue(centerOffset, millimetersPerDrawingUnit),
            ScaleValue(length, millimetersPerDrawingUnit),
            opening.DrawingWidth,
            opening.WidthMillimeters
                ?? ScaleValue(opening.DrawingWidth, millimetersPerDrawingUnit),
            crossWallOffset,
            ScaleValue(crossWallOffset, millimetersPerDrawingUnit),
            opening.Reliability.ReadyForCoordinatePlacement,
            opening.Reliability.RequiresReview,
            Math.Min(run.Confidence, opening.Confidence),
            sourceHostWallIds,
            opening.SourcePrimitiveIds,
            opening.SourceLayers,
            evidence);
        return true;
    }

    private static IReadOnlyList<PlacementSolvedWallSolidIntervalExport> BuildSolidIntervals(
        string runId,
        CompactedWallRun run,
        LineExport centerLine,
        double? millimetersPerDrawingUnit,
        PlacementReliabilityExport reliability,
        IReadOnlyList<PlacementSolvedWallOpeningIntervalExport> openingIntervals)
    {
        var runLength = LineLength(centerLine);
        if (runLength <= 0.001)
        {
            return Array.Empty<PlacementSolvedWallSolidIntervalExport>();
        }

        var openings = UnionOpeningIntervals(openingIntervals);
        var spans = new List<PlacementSolvedWallSolidIntervalExport>();
        var cursor = 0.0;
        OpeningUnionInterval? previous = null;
        var sequence = 1;
        foreach (var opening in openings)
        {
            if (opening.StartParameter > cursor + 0.000001)
            {
                spans.Add(CreateSolidInterval(
                    runId,
                    run,
                    centerLine,
                    millimetersPerDrawingUnit,
                    reliability,
                    sequence++,
                    cursor,
                    opening.StartParameter,
                    previous?.OpeningIds ?? Array.Empty<string>(),
                    opening.OpeningIds));
            }

            cursor = Math.Max(cursor, opening.EndParameter);
            previous = opening;
        }

        if (cursor < 1.0 - 0.000001)
        {
            spans.Add(CreateSolidInterval(
                runId,
                run,
                centerLine,
                millimetersPerDrawingUnit,
                reliability,
                sequence,
                cursor,
                1.0,
                previous?.OpeningIds ?? Array.Empty<string>(),
                Array.Empty<string>()));
        }

        return spans;
    }

    private static PlacementSolvedWallSolidIntervalExport CreateSolidInterval(
        string runId,
        CompactedWallRun run,
        LineExport hostLine,
        double? millimetersPerDrawingUnit,
        PlacementReliabilityExport reliability,
        int sequence,
        double startParameter,
        double endParameter,
        IReadOnlyList<string> previousOpeningIds,
        IReadOnlyList<string> nextOpeningIds)
    {
        var runLength = LineLength(hostLine);
        var startPoint = PointAt(hostLine, startParameter);
        var endPoint = PointAt(hostLine, endParameter);
        var line = new LineExport(startPoint, endPoint);
        var length = LineLength(line);
        var startOffset = startParameter * runLength;
        var endOffset = endParameter * runLength;
        var centerOffset = (startOffset + endOffset) / 2.0;
        var alongX = (endPoint.X - startPoint.X) / Math.Max(length, 0.001);
        var alongY = (endPoint.Y - startPoint.Y) / Math.Max(length, 0.001);
        var along = new VectorExport(alongX, alongY);
        var normal = new VectorExport(-alongY, alongX);
        var bodyPolygon = BuildBodyPolygon(line, run.ThicknessDrawingUnits);
        var bodyBounds = BoundsForPoints(bodyPolygon);
        var adjacentOpeningIds = previousOpeningIds
            .Concat(nextOpeningIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new PlacementSolvedWallSolidIntervalExport(
            $"{runId}:solid:{sequence}",
            runId,
            run.PageNumber,
            sequence,
            line,
            millimetersPerDrawingUnit is > 0
                ? ScaleLine(line, millimetersPerDrawingUnit.Value)
                : null,
            bodyPolygon,
            ScalePoints(bodyPolygon, millimetersPerDrawingUnit),
            bodyBounds,
            ScaleRect(bodyBounds, millimetersPerDrawingUnit),
            along,
            normal,
            run.ThicknessDrawingUnits,
            ScaleValue(run.ThicknessDrawingUnits, millimetersPerDrawingUnit),
            startParameter,
            endParameter,
            (startParameter + endParameter) / 2.0,
            startOffset,
            endOffset,
            centerOffset,
            length,
            millimetersPerDrawingUnit is > 0
                ? length * millimetersPerDrawingUnit.Value / 1000.0
                : null,
            reliability.ReadyForCoordinatePlacement,
            reliability.ReadyForMetricPlacement,
            reliability.RequiresReview,
            reliability.Reasons,
            adjacentOpeningIds,
            adjacentOpeningIds.Length > 0
                ? [
                    "solid interval is bounded by one or more canonical opening intervals",
                    "body polygon preserves collision-ready wall thickness without filling the opening gap"
                ]
                : [
                    "solid interval covers the logical wall where no anchored opening interval is present",
                    "body polygon preserves collision-ready wall thickness"
                ]);
    }

    private static IReadOnlyList<OpeningUnionInterval> UnionOpeningIntervals(
        IReadOnlyList<PlacementSolvedWallOpeningIntervalExport> intervals)
    {
        var merged = new List<OpeningUnionInterval>();
        foreach (var interval in intervals
                     .OrderBy(interval => interval.StartParameter)
                     .ThenBy(interval => interval.EndParameter)
                     .ThenBy(interval => interval.OpeningId, StringComparer.Ordinal))
        {
            if (merged.Count == 0
                || interval.StartParameter > merged[^1].EndParameter + 0.000001)
            {
                merged.Add(new OpeningUnionInterval(
                    interval.StartParameter,
                    interval.EndParameter,
                    new[] { interval.OpeningId }));
                continue;
            }

            var previous = merged[^1];
            merged[^1] = previous with
            {
                EndParameter = Math.Max(previous.EndParameter, interval.EndParameter),
                OpeningIds = previous.OpeningIds
                    .Append(interval.OpeningId)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()
            };
        }

        return merged;
    }

    private static IReadOnlyList<PointExport> BuildBodyPolygon(
        LineExport line,
        double thickness)
    {
        var length = LineLength(line);
        var alongX = (line.End.X - line.Start.X) / Math.Max(length, 0.001);
        var alongY = (line.End.Y - line.Start.Y) / Math.Max(length, 0.001);
        var normalX = -alongY;
        var normalY = alongX;
        var half = Math.Max(0.25, thickness / 2.0);
        var startLeft = new PointExport(
            line.Start.X + normalX * half,
            line.Start.Y + normalY * half);
        var endLeft = new PointExport(
            line.End.X + normalX * half,
            line.End.Y + normalY * half);
        var endRight = new PointExport(
            line.End.X - normalX * half,
            line.End.Y - normalY * half);
        var startRight = new PointExport(
            line.Start.X - normalX * half,
            line.Start.Y - normalY * half);
        return [startLeft, endLeft, endRight, startRight, startLeft];
    }

    private static RectExport BoundsForPoints(IReadOnlyList<PointExport> points)
    {
        var left = points.Min(point => point.X);
        var top = points.Min(point => point.Y);
        var right = points.Max(point => point.X);
        var bottom = points.Max(point => point.Y);
        return new RectExport(left, top, right - left, bottom - top);
    }

    private static IReadOnlyList<EndpointCluster> BuildEndpointClusters(
        IReadOnlyList<CompactedWallRun> runs)
    {
        var endpoints = runs
            .SelectMany((run, runIndex) => new[]
            {
                new EndpointObservation(
                    runIndex,
                    IsStart: true,
                    run.PageNumber,
                    ToPlanPoint(run.CenterLine.Start),
                    Orientation(run.CenterLine)),
                new EndpointObservation(
                    runIndex,
                    IsStart: false,
                    run.PageNumber,
                    ToPlanPoint(run.CenterLine.End),
                    Orientation(run.CenterLine))
            })
            .OrderBy(endpoint => endpoint.PageNumber)
            .ThenBy(endpoint => endpoint.Position.Y)
            .ThenBy(endpoint => endpoint.Position.X)
            .ThenBy(endpoint => endpoint.RunIndex)
            .ThenBy(endpoint => endpoint.IsStart ? 0 : 1)
            .ToArray();
        var clusters = new List<List<EndpointObservation>>();
        foreach (var endpoint in endpoints)
        {
            var cluster = clusters
                .Where(items => items.All(item =>
                    EndpointObservationsCanShareNode(item, endpoint)))
                .OrderBy(items => items.Max(item =>
                    Distance(item.Position, endpoint.Position)))
                .ThenBy(items => items[0].RunIndex)
                .ThenBy(items => items[0].IsStart ? 0 : 1)
                .FirstOrDefault();
            if (cluster is null)
            {
                clusters.Add([endpoint]);
            }
            else
            {
                cluster.Add(endpoint);
            }
        }

        return clusters
            .Select((cluster, index) => new EndpointCluster(
                $"wall-solution:page:{cluster[0].PageNumber}:node:{index + 1}",
                cluster[0].PageNumber,
                ResolveEndpointClusterPosition(cluster),
                cluster.ToArray()))
            .ToArray();
    }

    private static bool EndpointObservationsCanShareNode(
        EndpointObservation first,
        EndpointObservation second)
    {
        if (first.PageNumber != second.PageNumber
            || Distance(first.Position, second.Position) > EndpointSnapDistance)
        {
            return false;
        }

        if (first.Orientation == WallOrientation.Diagonal
            || second.Orientation == WallOrientation.Diagonal)
        {
            return Distance(first.Position, second.Position)
                <= EndpointAxisEqualityTolerance;
        }

        if (first.Orientation == second.Orientation)
        {
            return Math.Abs(
                EndpointAxisCoordinate(first)
                - EndpointAxisCoordinate(second)) <= EndpointAxisEqualityTolerance;
        }

        var horizontal = first.Orientation == WallOrientation.Horizontal
            ? first
            : second;
        var vertical = first.Orientation == WallOrientation.Vertical
            ? first
            : second;
        var intersection = new PlanPoint(
            vertical.Position.X,
            horizontal.Position.Y);
        return Distance(horizontal.Position, intersection) <= EndpointSnapDistance
            && Distance(vertical.Position, intersection) <= EndpointSnapDistance;
    }

    private static PlanPoint ResolveEndpointClusterPosition(
        IReadOnlyList<EndpointObservation> cluster)
    {
        var horizontal = cluster
            .Where(item => item.Orientation == WallOrientation.Horizontal)
            .ToArray();
        var vertical = cluster
            .Where(item => item.Orientation == WallOrientation.Vertical)
            .ToArray();
        if (horizontal.Length > 0 && vertical.Length > 0)
        {
            return new PlanPoint(
                vertical.Average(item => item.Position.X),
                horizontal.Average(item => item.Position.Y));
        }

        if (horizontal.Length > 0)
        {
            return new PlanPoint(
                horizontal.Average(item => item.Position.X),
                horizontal[0].Position.Y);
        }

        if (vertical.Length > 0)
        {
            return new PlanPoint(
                vertical[0].Position.X,
                vertical.Average(item => item.Position.Y));
        }

        return new PlanPoint(
            cluster.Average(item => item.Position.X),
            cluster.Average(item => item.Position.Y));
    }

    private static double EndpointAxisCoordinate(EndpointObservation endpoint) =>
        endpoint.Orientation == WallOrientation.Horizontal
            ? endpoint.Position.Y
            : endpoint.Position.X;

    private static EndpointCluster FindEndpointCluster(
        IReadOnlyList<EndpointCluster> clusters,
        int runIndex,
        bool isStart) =>
        clusters.Single(cluster => cluster.Endpoints.Any(endpoint =>
            endpoint.RunIndex == runIndex
            && endpoint.IsStart == isStart));

    private static double WeightedCoverage(
        IReadOnlyList<GlobalWallCandidate> truthCandidates,
        IReadOnlyList<CompactedWallRun> selected)
    {
        var total = truthCandidates.Sum(candidate => candidate.DrawingLength);
        if (total <= 0)
        {
            return 1;
        }

        var covered = truthCandidates.Sum(candidate =>
            candidate.DrawingLength * CoverageRatio(candidate, selected));
        return Math.Clamp(covered / total, 0, 1);
    }

    private static double CoverageRatio(
        GlobalWallCandidate candidate,
        IReadOnlyList<CompactedWallRun> selected)
    {
        var intervals = selected
            .Where(item =>
                item.PageNumber == candidate.PageNumber
                && SameOrientation(item.CenterLine, candidate.CenterLine)
                && (LineDistance(item.CenterLine, candidate.CenterLine)
                        <= Math.Max(AxisGroupingDistance, candidate.ThicknessDrawingUnits * 0.45)
                    || RepresentsCompetingSourceCandidate(candidate, item)))
            .Select(item => ProjectedOverlapInterval(candidate.CenterLine, item.CenterLine))
            .Where(interval => interval.End > interval.Start)
            .OrderBy(interval => interval.Start)
            .ToArray();
        if (intervals.Length == 0)
        {
            return 0;
        }

        var mergedLength = 0.0;
        var start = intervals[0].Start;
        var end = intervals[0].End;
        for (var index = 1; index < intervals.Length; index++)
        {
            var interval = intervals[index];
            if (interval.Start <= end + 0.5)
            {
                end = Math.Max(end, interval.End);
                continue;
            }

            mergedLength += end - start;
            start = interval.Start;
            end = interval.End;
        }

        mergedLength += end - start;
        return Math.Clamp(mergedLength / candidate.DrawingLength, 0, 1);
    }

    private static bool RepresentsCompetingSourceCandidate(
        GlobalWallCandidate candidate,
        CompactedWallRun run)
    {
        var candidateSourceWallIds = candidate.SourceWallIds.ToHashSet(StringComparer.Ordinal);
        return run.Contributors
                .SelectMany(contributor => contributor.SourceWallIds)
                .Any(candidateSourceWallIds.Contains)
            && LineDistance(candidate.CenterLine, run.CenterLine) <= CompetingSourceRunDistance
            && LengthOverlapRatio(candidate.CenterLine, run.CenterLine) >= 0.65;
    }

    private static double RoomClosureRatio(
        PlacementRoomExport room,
        IReadOnlyList<CompactedWallRun> selected)
    {
        var bounds = ToPlanRect(room.Bounds);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return 0;
        }

        var sides = new[]
        {
            new PlanLineSegment(new PlanPoint(bounds.Left, bounds.Top), new PlanPoint(bounds.Right, bounds.Top)),
            new PlanLineSegment(new PlanPoint(bounds.Left, bounds.Bottom), new PlanPoint(bounds.Right, bounds.Bottom)),
            new PlanLineSegment(new PlanPoint(bounds.Left, bounds.Top), new PlanPoint(bounds.Left, bounds.Bottom)),
            new PlanLineSegment(new PlanPoint(bounds.Right, bounds.Top), new PlanPoint(bounds.Right, bounds.Bottom))
        };
        return sides.Average(side => selected.Any(run =>
            run.PageNumber == room.PageNumber
            && SameOrientation(run.CenterLine, LineExport.From(side))
            && LineDistance(run.CenterLine, LineExport.From(side))
                <= Math.Max(5.0, run.ThicknessDrawingUnits)
            && LengthOverlapRatio(run.CenterLine, LineExport.From(side)) >= 0.35)
                ? 1.0
                : 0.0);
    }

    private static double DuplicateLength(IReadOnlyList<CompactedWallRun> selected)
    {
        var duplicate = 0.0;
        for (var firstIndex = 0; firstIndex < selected.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < selected.Count; secondIndex++)
            {
                var first = selected[firstIndex];
                var second = selected[secondIndex];
                if (first.PageNumber != second.PageNumber
                    || !SameOrientation(first.CenterLine, second.CenterLine)
                    || LineDistance(first.CenterLine, second.CenterLine) > DuplicateAxisDistance)
                {
                    continue;
                }

                duplicate += ProjectedOverlapLength(first.CenterLine, second.CenterLine);
            }
        }

        return duplicate;
    }

    private static double RunLength(CompactedWallRun run) => LineLength(run.CenterLine);

    private static bool RunRequiresReview(CompactedWallRun run) =>
        run.Contributors.All(candidate => candidate.RequiresReview)
        || run.Contributors.All(candidate => OriginPriority(candidate.PrimaryOrigin) < 3);

    private static bool RunIsNoise(CompactedWallRun run)
    {
        if (run.Contributors.All(candidate => candidate.StrongNegativeEvidence))
        {
            return true;
        }

        var weightedScore = WeightedAverage(run.Contributors, candidate => candidate.LocalScore);
        return weightedScore < 0.45
            && run.Contributors.All(candidate => candidate.RoomBoundarySupportCount == 0)
            && run.Contributors.All(candidate => candidate.OpeningSupportCount == 0);
    }

    private static int CountSupportedEndpoints(
        CompactedWallRun run,
        IReadOnlyList<CompactedWallRun> runs)
    {
        var count = 0;
        if (EndpointSupported(run, run.CenterLine.Start, runs))
        {
            count++;
        }

        if (EndpointSupported(run, run.CenterLine.End, runs))
        {
            count++;
        }

        if (count < 2
            && run.Contributors.Any(candidate => candidate.OpeningSupportCount > 0))
        {
            count++;
        }

        return count;
    }

    private static int CountSelectedRunEndpointSupport(
        GlobalWallCandidate candidate,
        IReadOnlyList<CompactedWallRun> runs)
    {
        var count = 0;
        if (EndpointSupportedBySelectedRun(candidate, candidate.CenterLine.Start, runs))
        {
            count++;
        }

        if (EndpointSupportedBySelectedRun(candidate, candidate.CenterLine.End, runs))
        {
            count++;
        }

        return count;
    }

    private static bool EndpointSupportedBySelectedRun(
        GlobalWallCandidate candidate,
        PointExport endpoint,
        IReadOnlyList<CompactedWallRun> runs)
    {
        var point = ToPlanPoint(endpoint);
        foreach (var run in runs)
        {
            if (run.PageNumber != candidate.PageNumber)
            {
                continue;
            }

            if (Distance(point, ToPlanPoint(run.CenterLine.Start)) <= JunctionCompletionDistance
                || Distance(point, ToPlanPoint(run.CenterLine.End)) <= JunctionCompletionDistance)
            {
                return true;
            }

            if (!SameOrientation(candidate.CenterLine, run.CenterLine)
                && PointToSegmentDistance(point, run.CenterLine) <= JunctionCompletionDistance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool EndpointSupported(
        CompactedWallRun run,
        PointExport endpoint,
        IReadOnlyList<CompactedWallRun> runs)
    {
        var point = ToPlanPoint(endpoint);
        foreach (var other in runs)
        {
            if (ReferenceEquals(other, run)
                || other.PageNumber != run.PageNumber)
            {
                continue;
            }

            if (Distance(point, ToPlanPoint(other.CenterLine.Start)) <= EndpointSupportDistance
                || Distance(point, ToPlanPoint(other.CenterLine.End)) <= EndpointSupportDistance)
            {
                return true;
            }

            if (!SameOrientation(run.CenterLine, other.CenterLine)
                && PointToSegmentDistance(point, other.CenterLine) <= EndpointSupportDistance)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountSupportedEndpoints(
        GlobalWallCandidate candidate,
        IReadOnlyList<GlobalWallCandidate> candidates)
    {
        var count = 0;
        if (EndpointSupported(candidate, candidate.CenterLine.Start, candidates))
        {
            count++;
        }

        if (EndpointSupported(candidate, candidate.CenterLine.End, candidates))
        {
            count++;
        }

        return count;
    }

    private static bool EndpointSupported(
        GlobalWallCandidate candidate,
        PointExport endpoint,
        IReadOnlyList<GlobalWallCandidate> candidates)
    {
        var point = ToPlanPoint(endpoint);
        foreach (var other in candidates)
        {
            if (ReferenceEquals(other, candidate)
                || string.Equals(other.Id, candidate.Id, StringComparison.Ordinal)
                || other.PageNumber != candidate.PageNumber)
            {
                continue;
            }

            if (Distance(point, ToPlanPoint(other.CenterLine.Start)) <= EndpointSupportDistance
                || Distance(point, ToPlanPoint(other.CenterLine.End)) <= EndpointSupportDistance)
            {
                return true;
            }

            if (!SameOrientation(candidate.CenterLine, other.CenterLine)
                && PointToSegmentDistance(point, other.CenterLine) <= EndpointSupportDistance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool RoomSupportsCandidate(PlacementRoomExport room, GlobalWallCandidate candidate)
    {
        if (!IsTrustedStructuralRoom(room))
        {
            return false;
        }

        if (room.WallIds.Any(candidate.SourceWallIds.Contains))
        {
            return true;
        }

        if (!room.Reliability.ReadyForCoordinatePlacement
            || room.Reliability.RequiresReview)
        {
            return false;
        }

        var bounds = ToPlanRect(room.Bounds);
        var sides = new[]
        {
            new LineExport(new PointExport(bounds.Left, bounds.Top), new PointExport(bounds.Right, bounds.Top)),
            new LineExport(new PointExport(bounds.Left, bounds.Bottom), new PointExport(bounds.Right, bounds.Bottom)),
            new LineExport(new PointExport(bounds.Left, bounds.Top), new PointExport(bounds.Left, bounds.Bottom)),
            new LineExport(new PointExport(bounds.Right, bounds.Top), new PointExport(bounds.Right, bounds.Bottom))
        };
        return sides.Any(side =>
            SameOrientation(side, candidate.CenterLine)
            && LineDistance(side, candidate.CenterLine) <= Math.Max(5.0, candidate.ThicknessDrawingUnits)
            && LengthOverlapRatio(side, candidate.CenterLine) >= 0.35);
    }

    private static bool IsTrustedStructuralRoom(PlacementRoomExport room)
    {
        if (HasConflictedStructuralRoomEvidence(room))
        {
            return false;
        }

        if (!string.Equals(
                room.UseKind,
                "Unknown",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (room.Evidence.Any(item =>
                item.Contains(
                    "semantic room seed from label",
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(room.Label)
            || room.AreaSquareMeters is > 0
                and < MinimumCredibleUnknownRoomAreaSquareMeters)
        {
            return false;
        }

        var letterCount = room.Label.Count(char.IsLetter);
        return letterCount >= 2
            || (letterCount == 1 && room.Label.Any(char.IsDigit));
    }

    private static bool HasConflictedStructuralRoomEvidence(
        PlacementRoomExport room)
    {
        if (room.Evidence.Any(item =>
                item.Contains(
                    "semantic room boundary could not be closed",
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var areaMatchRatio = ReadStructuralRoomEvidenceNumber(
            room.Evidence,
            "semantic room boundary area match ratio ");
        if (areaMatchRatio is < MinimumSemanticRoomAreaMatchRatio
            or > MaximumSemanticRoomAreaMatchRatio)
        {
            return true;
        }

        var trustedWallSupport = ReadStructuralRoomEvidenceNumber(
            room.Evidence,
            "semantic room boundary trusted wall support ");
        return trustedWallSupport is <= 0.01
            && room.Evidence.Any(item =>
                item.Contains(
                    "review-supported semantic room boundary",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static double? ReadStructuralRoomEvidenceNumber(
        IReadOnlyList<string> evidence,
        string prefix)
    {
        var item = evidence.FirstOrDefault(value =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return null;
        }

        var value = item[prefix.Length..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return double.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    private static bool HasTwoSidedSourceLinkedRoomBoundarySupport(
        GlobalWallCandidate candidate,
        IReadOnlyList<PlacementRoomExport> supportingRooms)
    {
        var length = LineLength(candidate.CenterLine);
        if (length <= 0.001)
        {
            return false;
        }

        var minimumCenterDistance = Math.Max(
            1.0,
            Math.Min(10.0, candidate.ThicknessDrawingUnits * 0.5));
        var hasPositiveSide = false;
        var hasNegativeSide = false;
        var deltaX = candidate.CenterLine.End.X - candidate.CenterLine.Start.X;
        var deltaY = candidate.CenterLine.End.Y - candidate.CenterLine.Start.Y;

        foreach (var room in supportingRooms.Where(room =>
                     room.WallIds.Any(candidate.SourceWallIds.Contains)))
        {
            var signedDistance = (
                deltaX * (room.Center.Y - candidate.CenterLine.Start.Y)
                - deltaY * (room.Center.X - candidate.CenterLine.Start.X)) / length;
            hasPositiveSide |= signedDistance >= minimumCenterDistance;
            hasNegativeSide |= signedDistance <= -minimumCenterDistance;
            if (hasPositiveSide && hasNegativeSide)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<SourceWallComponentReference> BuildSourceWallComponentReferences(
        IEnumerable<PlacementWallExport> walls) =>
        walls
            .Where(wall => !string.IsNullOrWhiteSpace(wall.WallComponentId))
            .GroupBy(wall => wall.WallComponentId!, StringComparer.Ordinal)
            .Select(group => new SourceWallComponentReference(
                group.Key,
                group
                    .Select(wall => wall.WallComponentKind)
                    .FirstOrDefault(kind => !string.IsNullOrWhiteSpace(kind))
                    ?? "Unknown"))
            .OrderBy(component => component.Id, StringComparer.Ordinal)
            .ThenBy(component => component.Kind, StringComparer.Ordinal)
            .ToArray();

    private static GlobalWallCandidate CreateCandidate(
        string id,
        string origin,
        int pageNumber,
        LineExport centerLine,
        LineExport? centerLineMillimeters,
        RectExport bounds,
        RectExport? boundsMillimeters,
        double drawingLength,
        double? lengthMeters,
        double thicknessDrawingUnits,
        double? thicknessMillimeters,
        double? millimetersPerDrawingUnit,
        string wallType,
        double confidence,
        bool excluded,
        bool readyForCoordinates,
        bool requiresReview,
        IReadOnlyList<string> sourceWallIds,
        IReadOnlyList<string> sourceWallGraphEdgeIds,
        IReadOnlyList<string> sourcePrimitiveIds,
        IReadOnlyList<string> sourceLayers,
        IReadOnlyList<SourceWallComponentReference> sourceWallComponents,
        int strongNegativeEvidenceVotes,
        int evidenceContributorCount,
        IReadOnlyList<string> evidence) =>
        new(
            id,
            pageNumber,
            origin,
            new[] { origin },
            wallType,
            centerLine,
            centerLineMillimeters,
            bounds,
            boundsMillimeters,
            drawingLength > 0 ? drawingLength : LineLength(centerLine),
            lengthMeters,
            Math.Max(0.5, thicknessDrawingUnits),
            thicknessMillimeters,
            millimetersPerDrawingUnit,
            Math.Clamp(confidence, 0, 1),
            excluded,
            readyForCoordinates,
            readyForCoordinates && centerLineMillimeters is not null,
            requiresReview,
            Math.Max(0, strongNegativeEvidenceVotes),
            Math.Max(1, evidenceContributorCount),
            0,
            false,
            false,
            0,
            0,
            0,
            0,
            0,
            0,
            false,
            0,
            false,
            sourceWallComponents
                .Distinct()
                .OrderBy(component => component.Id, StringComparer.Ordinal)
                .ThenBy(component => component.Kind, StringComparer.Ordinal)
                .ToArray(),
            sourceWallIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            sourceWallGraphEdgeIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            sourcePrimitiveIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            sourceLayers.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            evidence.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray());

    private static bool HasUsableGeometry(PlacementWallGraphEdgeExport edge) =>
        edge.CenterLine is not null
        && edge.Bounds is not null
        && edge.DrawingLength > 0
        && HasUsableGeometry(edge.CenterLine);

    private static bool HasUsableGeometry(PlacementWallTopologySpanExport span) =>
        span.DrawingLength > 0 && HasUsableGeometry(span.CenterLine);

    private static bool HasUsableGeometry(LineExport line) =>
        double.IsFinite(line.Start.X)
        && double.IsFinite(line.Start.Y)
        && double.IsFinite(line.End.X)
        && double.IsFinite(line.End.Y)
        && LineLength(line) > 0.001;

    private static bool AreEquivalent(GlobalWallCandidate first, GlobalWallCandidate second)
    {
        if (first.PageNumber != second.PageNumber
            || !SameOrientation(first.CenterLine, second.CenterLine)
            || LineDistance(first.CenterLine, second.CenterLine)
                > Math.Max(2.0, Math.Min(first.ThicknessDrawingUnits, second.ThicknessDrawingUnits) * 0.35))
        {
            return false;
        }

        var overlap = ProjectedOverlapLength(first.CenterLine, second.CenterLine);
        var minLength = Math.Min(first.DrawingLength, second.DrawingLength);
        var maxLength = Math.Max(first.DrawingLength, second.DrawingLength);
        return minLength > 0
            && overlap / minLength >= 0.88
            && overlap / maxLength >= 0.72;
    }

    private static GlobalWallCandidate MergeEquivalent(
        GlobalWallCandidate preferred,
        GlobalWallCandidate duplicate)
    {
        var structuralAuthority = string.Equals(
            preferred.PrimaryOrigin,
            "StructuralCore",
            StringComparison.Ordinal);
        var readyForCoordinatePlacement = structuralAuthority
            ? preferred.ReadyForCoordinatePlacement
            : preferred.ReadyForCoordinatePlacement || duplicate.ReadyForCoordinatePlacement;
        var readyForMetricPlacement = structuralAuthority
            ? preferred.ReadyForMetricPlacement
            : preferred.ReadyForMetricPlacement || duplicate.ReadyForMetricPlacement;
        var requiresReview = structuralAuthority
            ? preferred.RequiresReview
            : preferred.RequiresReview && duplicate.RequiresReview;

        return preferred with
        {
            Origins = preferred.Origins.Concat(duplicate.Origins).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            SourceWallComponents = preferred.SourceWallComponents
                .Concat(duplicate.SourceWallComponents)
                .Distinct()
                .OrderBy(component => component.Id, StringComparer.Ordinal)
                .ThenBy(component => component.Kind, StringComparer.Ordinal)
                .ToArray(),
            SourceWallIds = preferred.SourceWallIds.Concat(duplicate.SourceWallIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            SourceWallGraphEdgeIds = preferred.SourceWallGraphEdgeIds.Concat(duplicate.SourceWallGraphEdgeIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            SourcePrimitiveIds = preferred.SourcePrimitiveIds.Concat(duplicate.SourcePrimitiveIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            SourceLayers = preferred.SourceLayers.Concat(duplicate.SourceLayers).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            Evidence = preferred.Evidence
                .Concat(duplicate.Evidence)
                .Append($"candidate deduplicated equivalent source {duplicate.Id}")
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            StrongNegativeEvidenceVotes = preferred.StrongNegativeEvidenceVotes
                + duplicate.StrongNegativeEvidenceVotes,
            EvidenceContributorCount = preferred.EvidenceContributorCount
                + duplicate.EvidenceContributorCount,
            Confidence = structuralAuthority
                ? preferred.Confidence
                : Math.Max(preferred.Confidence, duplicate.Confidence),
            ReadyForCoordinatePlacement = readyForCoordinatePlacement,
            ReadyForMetricPlacement = readyForMetricPlacement,
            RequiresReview = requiresReview
        };
    }

    private static IReadOnlyList<string> CandidateDecisionEvidence(
        GlobalWallCandidate candidate,
        IReadOnlyList<string> selectedBy)
    {
        var summary = new[]
        {
            $"local structural score {candidate.LocalScore:0.###}",
            $"endpoint support {candidate.SupportedEndpointCount}/2",
            $"room boundary support {candidate.RoomBoundarySupportCount}",
            $"indoor room boundary support {candidate.IndoorRoomBoundarySupportCount}",
            $"outdoor room boundary support {candidate.OutdoorRoomBoundarySupportCount}",
            candidate.TwoSidedSourceLinkedRoomBoundarySupport
                ? "two-sided source-linked room boundary support"
                : "no two-sided source-linked room boundary support",
            $"opening support {candidate.OpeningSupportCount}",
            candidate.MajorWallCandidate ? "major-wall recall protected" : "not a major-wall recall candidate",
            candidate.CoherentRoomBoundaryCandidate
                ? "coherent room-boundary candidate"
                : "not a coherent room-boundary candidate",
            candidate.StrongNegativeEvidence ? "strong non-wall evidence present" : "no strong non-wall evidence",
            $"weak non-wall evidence {candidate.WeakNegativeEvidenceCount}",
            $"structural evidence {candidate.StructuralEvidenceCount}",
            $"strong non-wall evidence votes {candidate.StrongNegativeEvidenceVotes}/{candidate.EvidenceContributorCount}",
            selectedBy.Count == 0
                ? "rejected by all global hypotheses"
                : $"selected by {string.Join(",", selectedBy)}"
        };
        return summary
            .Concat(candidate.Evidence
                .Where(item => item.Contains(
                    "joint structural evidence rejected",
                    StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static int OriginPriority(string origin) =>
        origin switch
        {
            "StructuralCore" => 4,
            "CleanGraph" => 3,
            "TopologySpan" => 2,
            _ => 1
        };

    private static int ProfilePreference(string profile) =>
        profile switch
        {
            "balanced" => 0,
            "recall-first" => 1,
            _ => 2
        };

    private static string ResolveWallType(IReadOnlyList<PlacementWallExport> walls) =>
        ResolveWallType(walls.Select(wall => wall.WallType));

    private static string ResolveWallType(IReadOnlyList<GlobalWallCandidate> candidates)
    {
        var resolved = ResolveWallType(candidates.Select(candidate => candidate.WallType));
        return UsesAuthoritativeExteriorShellTypeResolution(candidates)
                ? "Exterior"
                : resolved;
    }

    private static bool UsesAuthoritativeExteriorShellTypeResolution(
        IReadOnlyList<GlobalWallCandidate> candidates) =>
        !string.Equals(
            ResolveWallType(candidates.Select(candidate => candidate.WallType)),
            "Exterior",
            StringComparison.OrdinalIgnoreCase)
        && HasAuthoritativeExteriorShellProvenance(candidates)
        && !candidates.Any(candidate =>
            candidate.TwoSidedSourceLinkedRoomBoundarySupport
            && candidate.OutdoorRoomBoundarySupportCount == 0);

    private static bool HasAuthoritativeExteriorShellProvenance(
        IReadOnlyList<GlobalWallCandidate> candidates) =>
        candidates.Any(candidate =>
            candidate.ReadyForCoordinatePlacement
            && !candidate.RequiresReview
            && !candidate.StrongNegativeEvidence
            && (string.Equals(candidate.WallType, "Exterior", StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.WallType, "Mixed", StringComparison.OrdinalIgnoreCase))
            && candidate.SourceWallIds.Any(IsAuthoritativeExteriorShellSourceId));

    private static bool IsAuthoritativeExteriorShellSourceId(string sourceWallId) =>
        sourceWallId.Contains(
            "wall-exterior-shell-inferred:",
            StringComparison.Ordinal)
        || sourceWallId.Contains(
            "wall-exterior-shell-source-backed:",
            StringComparison.Ordinal);

    private static string ResolveWallType(IEnumerable<string> types)
    {
        var values = types
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var exterior = values.Any(type => string.Equals(type, "Exterior", StringComparison.OrdinalIgnoreCase));
        var interior = values.Any(type => string.Equals(type, "Interior", StringComparison.OrdinalIgnoreCase));
        if (exterior && interior)
        {
            return "Mixed";
        }

        if (exterior)
        {
            return "Exterior";
        }

        if (interior)
        {
            return "Interior";
        }

        return values.Length == 1 ? values[0] : "Unknown";
    }

    private static bool CompatibleWallTypes(string first, string second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase)
        || string.Equals(first, "Unknown", StringComparison.OrdinalIgnoreCase)
        || string.Equals(second, "Unknown", StringComparison.OrdinalIgnoreCase)
        || string.Equals(first, "Mixed", StringComparison.OrdinalIgnoreCase)
        || string.Equals(second, "Mixed", StringComparison.OrdinalIgnoreCase);

    private static double WeightedAverage(
        IReadOnlyList<GlobalWallCandidate> candidates,
        Func<GlobalWallCandidate, double> selector)
    {
        var totalWeight = candidates.Sum(candidate => Math.Max(0.1, candidate.LocalScore) * candidate.DrawingLength);
        return totalWeight <= 0
            ? candidates.Average(selector)
            : candidates.Sum(candidate => selector(candidate) * Math.Max(0.1, candidate.LocalScore) * candidate.DrawingLength) / totalWeight;
    }

    private static bool SameOrientation(LineExport first, LineExport second) =>
        Orientation(first) == Orientation(second);

    private static bool DirectionsAreParallel(LineExport first, LineExport second)
    {
        var firstLength = LineLength(first);
        var secondLength = LineLength(second);
        if (firstLength <= 0.001 || secondLength <= 0.001)
        {
            return false;
        }

        var firstX = (first.End.X - first.Start.X) / firstLength;
        var firstY = (first.End.Y - first.Start.Y) / firstLength;
        var secondX = (second.End.X - second.Start.X) / secondLength;
        var secondY = (second.End.Y - second.Start.Y) / secondLength;
        return Math.Abs(firstX * secondX + firstY * secondY) >= 0.985;
    }

    private static WallOrientation Orientation(LineExport line)
    {
        var dx = Math.Abs(line.End.X - line.Start.X);
        var dy = Math.Abs(line.End.Y - line.Start.Y);
        if (dy <= Math.Max(1.0, dx * 0.04))
        {
            return WallOrientation.Horizontal;
        }

        if (dx <= Math.Max(1.0, dy * 0.04))
        {
            return WallOrientation.Vertical;
        }

        return WallOrientation.Diagonal;
    }

    private static double AxisCoordinate(LineExport line) =>
        Orientation(line) == WallOrientation.Horizontal
            ? (line.Start.Y + line.End.Y) / 2.0
            : (line.Start.X + line.End.X) / 2.0;

    private static double IntervalStart(LineExport line) =>
        Orientation(line) == WallOrientation.Horizontal
            ? Math.Min(line.Start.X, line.End.X)
            : Math.Min(line.Start.Y, line.End.Y);

    private static double IntervalEnd(LineExport line) =>
        Orientation(line) == WallOrientation.Horizontal
            ? Math.Max(line.Start.X, line.End.X)
            : Math.Max(line.Start.Y, line.End.Y);

    private static double LineDistance(LineExport first, LineExport second)
    {
        if (SameOrientation(first, second)
            && Orientation(first) != WallOrientation.Diagonal)
        {
            return Math.Abs(AxisCoordinate(first) - AxisCoordinate(second));
        }

        return Math.Min(
            PointToSegmentDistance(ToPlanPoint(first.Start), second),
            PointToSegmentDistance(ToPlanPoint(first.End), second));
    }

    private static double LengthOverlapRatio(LineExport first, LineExport second)
    {
        var overlap = ProjectedOverlapLength(first, second);
        var minimum = Math.Min(LineLength(first), LineLength(second));
        return minimum <= 0 ? 0 : Math.Clamp(overlap / minimum, 0, 1);
    }

    private static double ProjectedOverlapLength(LineExport first, LineExport second)
    {
        var interval = ProjectedOverlapInterval(first, second);
        return Math.Max(0, interval.End - interval.Start);
    }

    private static (double Start, double End) ProjectedOverlapInterval(LineExport basis, LineExport other)
    {
        var start = Math.Max(IntervalStart(basis), IntervalStart(other));
        var end = Math.Min(IntervalEnd(basis), IntervalEnd(other));
        return (start, Math.Max(start, end));
    }

    private static double PointToSegmentDistance(PlanPoint point, LineExport line)
    {
        var start = ToPlanPoint(line.Start);
        var end = ToPlanPoint(line.End);
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 0)
        {
            return Distance(point, start);
        }

        var parameter = Math.Clamp(
            ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared,
            0,
            1);
        return Distance(
            point,
            new PlanPoint(start.X + parameter * dx, start.Y + parameter * dy));
    }

    private static double PointToInfiniteLineDistance(
        PointExport point,
        LineExport line)
    {
        var dx = line.End.X - line.Start.X;
        var dy = line.End.Y - line.Start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 0.001)
        {
            return Distance(ToPlanPoint(point), ToPlanPoint(line.Start));
        }

        return Math.Abs(
            dy * point.X
            - dx * point.Y
            + line.End.X * line.Start.Y
            - line.End.Y * line.Start.X) / length;
    }

    private static double ProjectParameter(
        LineExport line,
        PointExport point)
    {
        var dx = line.End.X - line.Start.X;
        var dy = line.End.Y - line.Start.Y;
        var lengthSquared = dx * dx + dy * dy;
        return lengthSquared <= 0.000001
            ? 0
            : ((point.X - line.Start.X) * dx + (point.Y - line.Start.Y) * dy)
                / lengthSquared;
    }

    private static PointExport PointAt(LineExport line, double parameter) =>
        new(
            line.Start.X + (line.End.X - line.Start.X) * parameter,
            line.Start.Y + (line.End.Y - line.Start.Y) * parameter);

    private static double Distance(PlanPoint first, PlanPoint second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double LineLength(LineExport line) =>
        Distance(ToPlanPoint(line.Start), ToPlanPoint(line.End));

    private static PlanPoint ToPlanPoint(PointExport point) => new(point.X, point.Y);

    private static PlanRect ToPlanRect(RectExport rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    private static PlanRect BoundsFor(LineExport line, double thickness)
    {
        var half = Math.Max(0.5, thickness / 2.0);
        var left = Math.Min(line.Start.X, line.End.X) - half;
        var top = Math.Min(line.Start.Y, line.End.Y) - half;
        var right = Math.Max(line.Start.X, line.End.X) + half;
        var bottom = Math.Max(line.Start.Y, line.End.Y) + half;
        return new PlanRect(left, top, right - left, bottom - top);
    }

    private static LineExport ScaleLine(LineExport line, double scale) =>
        new(
            new PointExport(line.Start.X * scale, line.Start.Y * scale),
            new PointExport(line.End.X * scale, line.End.Y * scale));

    private static PointExport? ScalePoint(
        PointExport point,
        double? scale) =>
        scale is > 0
            ? new PointExport(point.X * scale.Value, point.Y * scale.Value)
            : null;

    private static IReadOnlyList<PointExport>? ScalePoints(
        IReadOnlyList<PointExport> points,
        double? scale) =>
        scale is > 0
            ? points.Select(point => ScalePoint(point, scale)!).ToArray()
            : null;

    private static RectExport ScaleRect(PlanRect rect, double scale) =>
        new(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale);

    private static RectExport? ScaleRect(
        RectExport rect,
        double? scale) =>
        scale is > 0
            ? new RectExport(
                rect.X * scale.Value,
                rect.Y * scale.Value,
                rect.Width * scale.Value,
                rect.Height * scale.Value)
            : null;

    private static double? ScaleValue(
        double value,
        double? scale) =>
        scale is > 0 ? value * scale.Value : null;

    private static double Round(double value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private enum WallOrientation
    {
        Horizontal = 0,
        Vertical,
        Diagonal
    }

    private sealed record WallSolverProfile(
        string Name,
        double InitialGraphThreshold,
        double InitialRecoveredThreshold,
        double MinimumConsiderScore,
        double AllowedObjectiveRegressionForMajorRecall);

    private sealed record GlobalWallCandidate(
        string Id,
        int PageNumber,
        string PrimaryOrigin,
        IReadOnlyList<string> Origins,
        string WallType,
        LineExport CenterLine,
        LineExport? CenterLineMillimeters,
        RectExport Bounds,
        RectExport? BoundsMillimeters,
        double DrawingLength,
        double? LengthMeters,
        double ThicknessDrawingUnits,
        double? ThicknessMillimeters,
        double? MillimetersPerDrawingUnit,
        double Confidence,
        bool ExcludedFromStructuralTopology,
        bool ReadyForCoordinatePlacement,
        bool ReadyForMetricPlacement,
        bool RequiresReview,
        int StrongNegativeEvidenceVotes,
        int EvidenceContributorCount,
        double LocalScore,
        bool MajorWallCandidate,
        bool StrongNegativeEvidence,
        int WeakNegativeEvidenceCount,
        int StructuralEvidenceCount,
        int SupportedEndpointCount,
        int RoomBoundarySupportCount,
        int IndoorRoomBoundarySupportCount,
        int OutdoorRoomBoundarySupportCount,
        bool TwoSidedSourceLinkedRoomBoundarySupport,
        int OpeningSupportCount,
        bool CoherentRoomBoundaryCandidate,
        IReadOnlyList<SourceWallComponentReference> SourceWallComponents,
        IReadOnlyList<string> SourceWallIds,
        IReadOnlyList<string> SourceWallGraphEdgeIds,
        IReadOnlyList<string> SourcePrimitiveIds,
        IReadOnlyList<string> SourceLayers,
        IReadOnlyList<string> Evidence);

    private sealed record SourceWallComponentReference(
        string Id,
        string Kind);

    private sealed record CleanTopologyRepresentation(
        PlacementWallExport RepresentedWall,
        IReadOnlyList<string> HostWallIds);

    private sealed record CompactedWallRun(
        int PageNumber,
        string WallType,
        LineExport CenterLine,
        double ThicknessDrawingUnits,
        double? MillimetersPerDrawingUnit,
        double Confidence,
        IReadOnlyList<GlobalWallCandidate> Contributors,
        int CompletedJunctionCount,
        IReadOnlyList<string> BridgedOpeningIds,
        int BodyContactJunctionCount = 0,
        WallReconciliationState? Reconciliation = null);

    private sealed record SolvedRunsBuildResult(
        IReadOnlyList<PlacementSolvedWallRunExport> Runs,
        PlacementWallReconciliationSummaryExport Reconciliation,
        PlacementWallTopologyOptimizationSummaryExport Topology);

    private sealed record OpeningBridgeCandidate(
        int FirstIndex,
        int SecondIndex,
        string OpeningId,
        CompactedWallRun MergedRun,
        double GapDrawingUnits);

    private sealed record OpeningUnionInterval(
        double StartParameter,
        double EndParameter,
        IReadOnlyList<string> OpeningIds);

    private sealed record EndpointObservation(
        int RunIndex,
        bool IsStart,
        int PageNumber,
        PlanPoint Position,
        WallOrientation Orientation);

    private sealed record JunctionEndpointCompletion(
        PointExport Position,
        bool SourceBackedBodyContact);

    private sealed record EndpointCluster(
        string Id,
        int PageNumber,
        PlanPoint Position,
        IReadOnlyList<EndpointObservation> Endpoints);

    private sealed record GlobalWallMetrics(
        double ObjectiveScore,
        double MajorWallCoverageRatio,
        double LongWallCoverageRatio,
        double EndpointConnectivityRatio,
        double RoomBoundaryClosureRatio,
        double ExteriorContinuityRatio,
        double DuplicateLengthRatio,
        double ReviewLengthRatio,
        double NoiseLengthRatio,
        double AverageConfidence,
        double SelectedDrawingLength,
        int UnsupportedEndpointCount,
        int ClosedRoomCount,
        int EvaluatedRoomCount)
    {
        public static GlobalWallMetrics Empty { get; } = new(
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);

        public PlacementWallHypothesisMetricsExport Export() =>
            new(
                Round(MajorWallCoverageRatio),
                Round(LongWallCoverageRatio),
                Round(EndpointConnectivityRatio),
                Round(RoomBoundaryClosureRatio),
                Round(ExteriorContinuityRatio),
                Round(DuplicateLengthRatio),
                Round(ReviewLengthRatio),
                Round(NoiseLengthRatio),
                Round(AverageConfidence),
                Round(SelectedDrawingLength),
                UnsupportedEndpointCount,
                ClosedRoomCount,
                EvaluatedRoomCount);
    }

    private sealed record SolvedHypothesis(
        string Id,
        WallSolverProfile Profile,
        double Score,
        int IterationCount,
        int InitialCandidateCount,
        IReadOnlyList<string> SelectedCandidateIds,
        int RecoveredCandidateCount,
        int RemovedCandidateCount,
        IReadOnlyList<string> TrustedCoordinateRecoveredCandidateIds,
        GlobalWallMetrics Metrics)
    {
        public PlacementWallHypothesisExport Export(bool selected) =>
            new(
                Id,
                Profile.Name,
                Round(Score),
                selected,
                IterationCount,
                InitialCandidateCount,
                SelectedCandidateIds.Count,
                RecoveredCandidateCount,
                RemovedCandidateCount,
                Metrics.Export(),
                SelectedCandidateIds,
                new[]
                {
                    $"{Profile.Name} hypothesis completed {IterationCount} global add/remove pass(es)",
                    $"major wall coverage {Metrics.MajorWallCoverageRatio:0.###}",
                    $"long wall coverage {Metrics.LongWallCoverageRatio:0.###}",
                    $"endpoint connectivity {Metrics.EndpointConnectivityRatio:0.###}",
                    $"room boundary closure {Metrics.RoomBoundaryClosureRatio:0.###}",
                    $"duplicate length ratio {Metrics.DuplicateLengthRatio:0.###}",
                    $"review length ratio {Metrics.ReviewLengthRatio:0.###}",
                    $"noise length ratio {Metrics.NoiseLengthRatio:0.###}"
                });
    }
}
