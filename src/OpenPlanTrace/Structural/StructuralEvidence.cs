namespace OpenPlanTrace;

[Flags]
public enum StructuralCandidateOrigin
{
    None = 0,
    DetectedWall = 1 << 0,
    AcceptedWall = 1 << 1,
    RecoveredWall = 1 << 2,
    WallGraph = 1 << 3,
    RoomBoundary = 1 << 4,
    ExteriorShell = 1 << 5,
    OpeningHost = 1 << 6
}

public enum StructuralEvidenceSignalKind
{
    Unknown = 0,
    SourceConfidence,
    AcceptedWall,
    ReviewWall,
    RejectedWall,
    WallBody,
    RecoveredWallBody,
    ExistingGraph,
    LongRun,
    Junction,
    RoomBoundary,
    ExteriorShell,
    OpeningHost,
    DoorOrOpeningSymbol,
    SurfacePattern,
    RepeatedDetailPattern,
    DimensionOrAnnotation,
    ObjectOrFixture,
    ContextOnlyBoundary,
    UnsupportedOblique,
    LayerSupport,
    OppositeRoomBoundary,
    StructuralTerritory,
    IsolatedStructuralIsland,
    UnoccupiedShellExtension,
    WallBodyThicknessOutlier,
    FragmentAxisContinuity
}

public enum StructuralEvidenceRelationKind
{
    Unknown = 0,
    Duplicate,
    Continuation,
    Junction,
    Conflict,
    SharedRoomBoundary,
    OpeningHost
}

public sealed record StructuralEvidenceSignal(
    string Id,
    StructuralEvidenceSignalKind Kind,
    double Weight,
    string SourceId,
    string Description,
    IReadOnlyList<string> SourcePrimitiveIds)
{
    public bool IsPositive => Weight > 0;

    public bool IsNegative => Weight < 0;

    public bool IsBlockingSemanticNegative =>
        Weight < 0
        && Kind is
            StructuralEvidenceSignalKind.DoorOrOpeningSymbol
            or StructuralEvidenceSignalKind.SurfacePattern
            or StructuralEvidenceSignalKind.RepeatedDetailPattern
            or StructuralEvidenceSignalKind.DimensionOrAnnotation
            or StructuralEvidenceSignalKind.ObjectOrFixture
            or StructuralEvidenceSignalKind.ContextOnlyBoundary
            or StructuralEvidenceSignalKind.UnsupportedOblique
            or StructuralEvidenceSignalKind.IsolatedStructuralIsland
            or StructuralEvidenceSignalKind.UnoccupiedShellExtension
            or StructuralEvidenceSignalKind.WallBodyThicknessOutlier;

    public bool IsStrongBlockingSemanticNegative =>
        Weight <= -0.45
        && IsBlockingSemanticNegative;
}

public sealed record StructuralEvidenceRelation(
    string Id,
    StructuralEvidenceRelationKind Kind,
    string FirstCandidateId,
    string SecondCandidateId,
    double Weight,
    bool IsHardConstraint,
    IReadOnlyList<string> Evidence);

public sealed record StructuralWallCandidate(
    string Id,
    int PageNumber,
    PlanLineSegment CenterLine,
    double Thickness,
    WallType WallType,
    Confidence Confidence,
    StructuralCandidateOrigin Origins,
    bool IsEligible,
    bool WasAcceptedByPreliminaryPipeline,
    double UnaryScore,
    IReadOnlyList<string> SourceWallIds,
    IReadOnlyList<string> SourceWallGraphEdgeIds,
    IReadOnlyList<string> SourcePrimitiveIds,
    IReadOnlyList<string> SourceRoomIds,
    IReadOnlyList<string> SourceOpeningIds,
    IReadOnlyList<StructuralEvidenceSignal> Signals,
    IReadOnlyList<string> Evidence)
{
    public IReadOnlyList<string> SourceWallComponentIds { get; init; } =
        Array.Empty<string>();

    public double DrawingLength => CenterLine.Length;

    public PlanRect Bounds => CenterLine.Bounds.Inflate(Math.Max(Thickness / 2.0, 0.5));

    public bool HasStrongNegativeEvidence =>
        Signals.Any(signal => signal.IsStrongBlockingSemanticNegative);

    public bool HasBlockingSemanticEvidence =>
        Signals.Any(signal => signal.IsBlockingSemanticNegative);

    public bool HasIndependentWallBodyEvidence =>
        Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.WallBody
            && signal.Weight >= 0.30);

    public bool HasAcceptedPlacementReadyWallBodyEvidence =>
        WasAcceptedByPreliminaryPipeline
        && !Signals.Any(signal =>
            signal.Kind is StructuralEvidenceSignalKind.ReviewWall
                or StructuralEvidenceSignalKind.RejectedWall
            && signal.Weight < 0)
        && Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.AcceptedWall
            && signal.Weight > 0)
        && Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.WallBody
            && signal.Weight >= 0.16);

    public bool HasCorroboratedFragmentAxisEvidence =>
        Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.FragmentAxisContinuity
            && signal.Weight >= 0.16)
        && HasSharedCrossDomainSupport
        && !HasStrongCrossDomainBlocker;

    public bool HasCrossDomainWallBodyEvidence =>
        (HasAcceptedPlacementReadyWallBodyEvidence
            || HasCorroboratedFragmentAxisEvidence)
        && HasSharedCrossDomainSupport
        && !HasStrongCrossDomainBlocker;

    public bool HasOpeningHostedReviewWallBodyCorroboration =>
        Origins.HasFlag(StructuralCandidateOrigin.WallGraph)
        && Origins.HasFlag(StructuralCandidateOrigin.RoomBoundary)
        && Origins.HasFlag(StructuralCandidateOrigin.OpeningHost)
        && SourceRoomIds.Distinct(StringComparer.Ordinal).Count() >= 2
        && SourceOpeningIds.Count > 0
        && Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.WallBody
            && signal.Weight >= 0.06)
        && Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.ReviewWall
            && signal.Weight < 0)
        && Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.ExistingGraph
            && signal.Weight > 0)
        && Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.OpeningHost
            && signal.Weight >= 0.10)
        && !Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.ContextOnlyBoundary
            && signal.Weight <= -1.0)
        && !HasStrongCrossDomainBlocker;

    private bool HasSharedCrossDomainSupport =>
        Origins.HasFlag(StructuralCandidateOrigin.WallGraph)
        && Origins.HasFlag(StructuralCandidateOrigin.RoomBoundary)
        && Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.ExistingGraph
            && signal.Weight > 0)
        && Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.StructuralTerritory
            && signal.Weight > 0)
        && Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.OppositeRoomBoundary
            && signal.Weight >= 0.08);

    private bool HasStrongCrossDomainBlocker =>
        Signals.Any(signal =>
            signal.Weight <= -0.45
            && signal.Kind is
                StructuralEvidenceSignalKind.DoorOrOpeningSymbol
                    or StructuralEvidenceSignalKind.SurfacePattern
                    or StructuralEvidenceSignalKind.RepeatedDetailPattern
                    or StructuralEvidenceSignalKind.DimensionOrAnnotation
                    or StructuralEvidenceSignalKind.ObjectOrFixture
                    or StructuralEvidenceSignalKind.UnsupportedOblique
                    or StructuralEvidenceSignalKind.IsolatedStructuralIsland
                    or StructuralEvidenceSignalKind.UnoccupiedShellExtension
                    or StructuralEvidenceSignalKind.WallBodyThicknessOutlier);

    public bool HasStrongRepeatedDetailEvidence =>
        Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.RepeatedDetailPattern
            && signal.Weight <= -0.45);

    public bool HasAbsoluteBlockingEvidence =>
        Signals.Any(signal =>
            signal.Kind is
                StructuralEvidenceSignalKind.IsolatedStructuralIsland
                or StructuralEvidenceSignalKind.UnoccupiedShellExtension
                or StructuralEvidenceSignalKind.WallBodyThicknessOutlier
            && signal.Weight <= -0.45)
        || (!HasIndependentWallBodyEvidence
            && !HasCrossDomainWallBodyEvidence
            && !Origins.HasFlag(StructuralCandidateOrigin.OpeningHost)
            && Signals.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.ContextOnlyBoundary
                && signal.Weight <= -0.45));
}

public enum StructuralRoomLoopContext
{
    Unknown = 0,
    Indoor,
    Outdoor,
    Conflicted,
    FixtureLike
}

public sealed record StructuralRoomBoundaryEdge(
    string Id,
    PlanLineSegment Line,
    IReadOnlyList<string> CandidateIds,
    IReadOnlyList<string> Evidence)
{
    public double DrawingLength => Line.Length;
}

public sealed record StructuralRoomLoopCandidate(
    string Id,
    string SourceRoomId,
    int PageNumber,
    IReadOnlyList<PlanPoint> Boundary,
    IReadOnlyList<StructuralRoomBoundaryEdge> BoundaryEdges,
    Confidence Confidence,
    double Weight,
    IReadOnlyList<string> Evidence)
{
    public StructuralRoomLoopContext Context { get; init; } = StructuralRoomLoopContext.Unknown;

    public double DrawingPerimeter => BoundaryEdges.Sum(edge => edge.DrawingLength);
}

public sealed record StructuralJunctionCandidate(
    string Id,
    int PageNumber,
    PlanPoint Position,
    WallNodeKind Kind,
    IReadOnlyList<string> CandidateIds,
    IReadOnlyList<string> SourceWallGraphNodeIds,
    Confidence Confidence,
    IReadOnlyList<string> Evidence);

public sealed record StructuralOpeningConstraint(
    string Id,
    string SourceOpeningId,
    int PageNumber,
    PlanLineSegment CenterLine,
    IReadOnlyList<string> HostCandidateIds,
    Confidence Confidence,
    IReadOnlyList<string> SourcePrimitiveIds,
    IReadOnlyList<string> Evidence);

public sealed record StructuralEvidenceGraph(
    string ContractVersion,
    IReadOnlyList<StructuralWallCandidate> WallCandidates,
    IReadOnlyList<StructuralEvidenceRelation> Relations,
    IReadOnlyList<StructuralJunctionCandidate> Junctions,
    IReadOnlyList<StructuralRoomLoopCandidate> RoomLoops,
    IReadOnlyList<StructuralOpeningConstraint> OpeningConstraints,
    IReadOnlyList<string> Producers,
    IReadOnlyList<string> Evidence)
{
    public const string CurrentContractVersion = "openplantrace.structural-evidence.v10";

    public static StructuralEvidenceGraph Empty { get; } =
        new(
            CurrentContractVersion,
            Array.Empty<StructuralWallCandidate>(),
            Array.Empty<StructuralEvidenceRelation>(),
            Array.Empty<StructuralJunctionCandidate>(),
            Array.Empty<StructuralRoomLoopCandidate>(),
            Array.Empty<StructuralOpeningConstraint>(),
            Array.Empty<string>(),
            Array.Empty<string>());

    public int ArtifactCount =>
        WallCandidates.Count
        + Relations.Count
        + Junctions.Count
        + RoomLoops.Count
        + OpeningConstraints.Count;
}
