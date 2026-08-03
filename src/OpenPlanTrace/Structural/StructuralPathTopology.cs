namespace OpenPlanTrace;

public enum StructuralPathKind
{
    Unknown = 0,
    Line,
    CircularArc
}

public enum StructuralPathEndpointKind
{
    Unknown = 0,
    Start,
    End
}

public enum StructuralPathJunctionKind
{
    Unknown = 0,
    Tangent,
    Corner
}

public abstract record StructuralPathGeometry
{
    public abstract StructuralPathKind Kind { get; }

    public abstract PlanPoint StartPoint { get; }

    public abstract PlanPoint EndPoint { get; }

    public abstract PlanRect Bounds { get; }

    public abstract double DrawingLength { get; }
}

public sealed record StructuralLinePathGeometry(PlanLineSegment CenterLine) : StructuralPathGeometry
{
    public override StructuralPathKind Kind => StructuralPathKind.Line;

    public override PlanPoint StartPoint => CenterLine.Start;

    public override PlanPoint EndPoint => CenterLine.End;

    public override PlanRect Bounds => CenterLine.Bounds;

    public override double DrawingLength => CenterLine.Length;
}

public sealed record StructuralCircularArcPathGeometry(
    PlanPoint Center,
    double Radius,
    double StartAngleRadians,
    double SweepAngleRadians) : StructuralPathGeometry
{
    private const double FullCircleRadians = Math.PI * 2.0;

    public override StructuralPathKind Kind => StructuralPathKind.CircularArc;

    public override PlanPoint StartPoint => PointAt(StartAngleRadians);

    public override PlanPoint EndPoint => PointAt(StartAngleRadians + SweepAngleRadians);

    public override PlanRect Bounds => ComputeBounds();

    public override double DrawingLength => Math.Abs(SweepAngleRadians) * Math.Max(0, Radius);

    public PlanPoint PointAt(double angle) =>
        new(
            Center.X + (Math.Cos(angle) * Radius),
            Center.Y + (Math.Sin(angle) * Radius));

    private PlanRect ComputeBounds()
    {
        if (!(Radius > 0) || !double.IsFinite(Radius))
        {
            return new PlanRect(Center.X, Center.Y, 0, 0);
        }

        var points = new List<PlanPoint>
        {
            StartPoint,
            EndPoint
        };
        foreach (var angle in new[] { 0.0, Math.PI / 2.0, Math.PI, Math.PI * 1.5 })
        {
            if (ContainsAngle(angle))
            {
                points.Add(PointAt(angle));
            }
        }

        return PlanRect.FromEdges(
            points.Min(point => point.X),
            points.Min(point => point.Y),
            points.Max(point => point.X),
            points.Max(point => point.Y));
    }

    private bool ContainsAngle(double angle)
    {
        var sweep = Math.Abs(SweepAngleRadians);
        if (sweep >= FullCircleRadians - 1e-9)
        {
            return true;
        }

        var directedDelta = SweepAngleRadians >= 0
            ? NormalizeAngle(angle - StartAngleRadians)
            : NormalizeAngle(StartAngleRadians - angle);
        return directedDelta <= sweep + 1e-9;
    }

    private static double NormalizeAngle(double angle)
    {
        var normalized = angle % FullCircleRadians;
        return normalized < 0 ? normalized + FullCircleRadians : normalized;
    }
}

public sealed record StructuralPath(
    string Id,
    int PageNumber,
    StructuralPathGeometry Geometry,
    double Thickness,
    double? ThicknessMillimeters,
    double? LengthMeters,
    string? MeasurementScaleGroupId,
    WallType WallType,
    Confidence Confidence,
    bool ReadyForCoordinatePlacement,
    bool RequiresReview,
    IReadOnlyList<string> SourceStructuralWallRunIds,
    IReadOnlyList<string> SourceCurvedWallCandidateIds,
    IReadOnlyList<string> SourceWallIds,
    IReadOnlyList<string> SourceWallGraphEdgeIds,
    IReadOnlyList<string> SourcePrimitiveIds,
    IReadOnlyList<string> Evidence)
{
    public string? SourceRegionId { get; init; }

    public IReadOnlyList<string> ConnectedPathIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ConnectedStraightPathIds { get; init; } = Array.Empty<string>();

    public StructuralPathKind Kind => Geometry.Kind;

    public PlanPoint StartPoint => Geometry.StartPoint;

    public PlanPoint EndPoint => Geometry.EndPoint;

    public PlanRect Bounds => Geometry.Bounds.Inflate(Math.Max(Thickness / 2.0, 0.5));

    public double DrawingLength => Geometry.DrawingLength;

    public int ConnectedStraightPathSupportCount => ConnectedStraightPathIds.Count;
}

public sealed record StructuralPathEndpointReference(
    string PathId,
    StructuralPathEndpointKind Endpoint,
    PlanPoint Position,
    PlanVector DirectionIntoPath);

public sealed record StructuralPathJunction(
    string Id,
    int PageNumber,
    StructuralPathJunctionKind Kind,
    StructuralPathEndpointReference FirstEndpoint,
    StructuralPathEndpointReference SecondEndpoint,
    PlanPoint ProposedPosition,
    double EndpointDistance,
    double MatchTolerance,
    double DirectionAngleDegrees,
    double TangentDeviationDegrees,
    Confidence Confidence,
    bool RequiresReview,
    IReadOnlyList<string> SourcePrimitiveIds,
    IReadOnlyList<string> Evidence);

public sealed record StructuralPathTopologyMetrics(
    int LinePathCount,
    int CircularArcPathCount,
    int JunctionCount,
    int TangentJunctionCount,
    int CornerJunctionCount,
    int ConnectedCurvedPathCount,
    int UnconnectedCurvedPathCount,
    int RejectedCurvedCandidateCount,
    int PlacementReadyPathCount,
    int ReviewPathCount);

public sealed record StructuralPathTopology(
    string ContractVersion,
    IReadOnlyList<StructuralPath> Paths,
    IReadOnlyList<StructuralPathJunction> Junctions,
    StructuralPathTopologyMetrics Metrics,
    IReadOnlyList<string> Evidence)
{
    public const string CurrentContractVersion = "openplantrace.structural-path-topology.v1";

    public static StructuralPathTopology Empty { get; } =
        new(
            CurrentContractVersion,
            Array.Empty<StructuralPath>(),
            Array.Empty<StructuralPathJunction>(),
            new StructuralPathTopologyMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
            Array.Empty<string>());

    public int ArtifactCount => Paths.Count + Junctions.Count;
}
