namespace OpenPlanTrace;

public enum CurvedWallSourceKind
{
    NativeArcPair,
    PolylineArcPair,
    MixedArcPair
}

public sealed record CurvedWallCandidate(
    string Id,
    int PageNumber,
    PlanPoint Center,
    double CenterlineRadius,
    double StartAngleRadians,
    double SweepAngleRadians,
    double Thickness,
    PlanRect Bounds,
    string? SourceRegionId,
    CurvedWallSourceKind SourceKind,
    double AngularOverlapRatio,
    double RadialFitError,
    bool ReadyForCoordinatePlacement,
    bool ExcludedFromLinearTopology,
    Confidence Confidence,
    bool RequiresReview,
    IReadOnlyList<string> SourcePrimitiveIds,
    IReadOnlyList<string> Evidence)
{
    public double InnerRadius => Math.Max(0, CenterlineRadius - (Thickness / 2.0));

    public double OuterRadius => CenterlineRadius + (Thickness / 2.0);

    public double ArcLength => Math.Abs(SweepAngleRadians) * CenterlineRadius;

    public PlanPoint StartPoint => PointAt(StartAngleRadians);

    public PlanPoint EndPoint => PointAt(StartAngleRadians + SweepAngleRadians);

    public double? RadiusMillimeters { get; init; }

    public double? ThicknessMillimeters { get; init; }

    public double? ArcLengthMeters { get; init; }

    public string? MeasurementScaleGroupId { get; init; }

    private PlanPoint PointAt(double angle) =>
        new(
            Center.X + (Math.Cos(angle) * CenterlineRadius),
            Center.Y + (Math.Sin(angle) * CenterlineRadius));
}
