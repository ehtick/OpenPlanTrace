namespace OpenPlanTrace;

public sealed record StructuralEvidenceSource(
    IReadOnlyList<WallSegment> WallCandidates,
    IReadOnlyList<WallSegment> AcceptedWalls,
    WallEvidenceMap WallEvidence,
    WallGraph WallGraph,
    IReadOnlyList<RoomRegion> Rooms,
    IReadOnlyList<OpeningCandidate> Openings,
    IReadOnlyList<SheetRegion> SheetRegions,
    double DefaultWallThickness)
{
    public IReadOnlyList<SurfacePatternCandidate> SurfacePatterns { get; init; } =
        Array.Empty<SurfacePatternCandidate>();

    public IReadOnlyList<DimensionAnnotation> Dimensions { get; init; } =
        Array.Empty<DimensionAnnotation>();

    public IReadOnlyList<PlanAnnotationBlock> Annotations { get; init; } =
        Array.Empty<PlanAnnotationBlock>();

    public static StructuralEvidenceSource Empty { get; } =
        new(
            Array.Empty<WallSegment>(),
            Array.Empty<WallSegment>(),
            WallEvidenceMap.Empty,
            WallGraph.Empty,
            Array.Empty<RoomRegion>(),
            Array.Empty<OpeningCandidate>(),
            Array.Empty<SheetRegion>(),
            4);
}
