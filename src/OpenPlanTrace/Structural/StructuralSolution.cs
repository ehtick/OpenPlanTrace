namespace OpenPlanTrace;

public enum StructuralWallDecisionKind
{
    Unknown = 0,
    Selected,
    Rejected,
    RetainedForReview,
    Invalid
}

public enum StructuralJunctionKind
{
    Unknown = 0,
    Endpoint,
    Corner,
    Tee,
    Cross,
    Inline
}

public sealed record StructuralWallDecision(
    string CandidateId,
    StructuralWallDecisionKind Decision,
    double UnaryScore,
    double ObjectiveContribution,
    IReadOnlyList<string> Reasons)
{
    public IReadOnlyList<string> SourceWallIds { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<StructuralEvidenceSignalKind> BlockingSignalKinds { get; init; } =
        Array.Empty<StructuralEvidenceSignalKind>();

    public bool AbsolutePlacementBlock { get; init; }
}

public sealed record StructuralWallRunReliability(
    bool ReadyForCoordinatePlacement,
    bool RequiresReview,
    double Confidence,
    IReadOnlyList<string> Reasons)
{
    public static StructuralWallRunReliability Unassessed { get; } =
        new(
            ReadyForCoordinatePlacement: false,
            RequiresReview: true,
            Confidence: 0,
            Reasons: new[] { "structural wall run has not been assessed for coordinate placement" });
}

public sealed record StructuralWallRun(
    string Id,
    int PageNumber,
    PlanLineSegment CenterLine,
    double Thickness,
    WallType WallType,
    Confidence Confidence,
    IReadOnlyList<string> CandidateIds,
    IReadOnlyList<string> SourceWallIds,
    IReadOnlyList<string> SourceWallGraphEdgeIds,
    IReadOnlyList<string> SourcePrimitiveIds,
    IReadOnlyList<string> SourceRoomIds,
    IReadOnlyList<string> SourceOpeningIds,
    IReadOnlyList<string> Evidence)
{
    public int AssemblyLeafCount { get; init; } = 1;

    public IReadOnlyList<string> SourceWallComponentIds { get; init; } =
        Array.Empty<string>();

    public StructuralWallRunReliability Reliability { get; init; } =
        StructuralWallRunReliability.Unassessed;

    public double DrawingLength => CenterLine.Length;

    public PlanRect Bounds => CenterLine.Bounds.Inflate(Math.Max(Thickness / 2.0, 0.5));
}

public sealed record StructuralJunction(
    string Id,
    int PageNumber,
    PlanPoint Position,
    StructuralJunctionKind Kind,
    IReadOnlyList<string> IncidentWallRunIds,
    IReadOnlyList<string> SourceJunctionCandidateIds,
    Confidence Confidence,
    bool RequiresReview,
    IReadOnlyList<string> Evidence);

public sealed record StructuralRoomClosure(
    string RoomLoopId,
    string SourceRoomId,
    double BoundaryCoverage,
    bool IsClosed,
    IReadOnlyList<string> SelectedWallRunIds,
    IReadOnlyList<string> MissingBoundaryEdgeIds);

public sealed record StructuralSolutionMetrics(
    int CandidateCount,
    int EligibleCandidateCount,
    int SelectedCandidateCount,
    int RejectedCandidateCount,
    int ReviewCandidateCount,
    int CanonicalWallRunCount,
    int JunctionCount,
    int EvaluatedRoomLoopCount,
    int ClosedRoomLoopCount,
    double MeanRoomBoundaryCoverage,
    int PreliminaryAcceptedCandidateCount,
    int RecoveredSelectedCandidateCount,
    int StrongNegativeSelectedCandidateCount,
    int OptimizationPassCount);

public sealed record StructuralPlanSolution(
    string SolverVersion,
    string EvidenceContractVersion,
    double ObjectiveScore,
    IReadOnlyList<StructuralWallDecision> CandidateDecisions,
    IReadOnlyList<StructuralWallRun> WallRuns,
    IReadOnlyList<StructuralJunction> Junctions,
    IReadOnlyList<StructuralRoomClosure> RoomClosures,
    StructuralSolutionMetrics Metrics,
    IReadOnlyList<string> Evidence)
{
    public const string CurrentSolverVersion = "openplantrace.joint-structural-solver.v10";

    public static StructuralPlanSolution Empty { get; } =
        new(
            CurrentSolverVersion,
            StructuralEvidenceGraph.CurrentContractVersion,
            0,
            Array.Empty<StructuralWallDecision>(),
            Array.Empty<StructuralWallRun>(),
            Array.Empty<StructuralJunction>(),
            Array.Empty<StructuralRoomClosure>(),
            new StructuralSolutionMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            Array.Empty<string>());

    public int ArtifactCount =>
        CandidateDecisions.Count
        + WallRuns.Count
        + Junctions.Count
        + RoomClosures.Count;
}
