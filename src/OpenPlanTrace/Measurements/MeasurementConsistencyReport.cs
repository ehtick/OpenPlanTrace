namespace OpenPlanTrace;

public sealed record MeasurementConsistencyReport(
    bool HasReliableCalibration,
    double? SelectedMillimetersPerDrawingUnit,
    double? MedianDimensionMillimetersPerDrawingUnit,
    double? DimensionScaleSpreadRatio,
    Confidence Confidence,
    IReadOnlyList<MeasurementConsistencyCheck> Checks)
{
    public static MeasurementConsistencyReport Empty { get; } =
        new(
            false,
            null,
            null,
            null,
            Confidence.None,
            Array.Empty<MeasurementConsistencyCheck>());

    public const int NonBlockingOutlierCountMaximum = 2;

    public const double NonBlockingOutlierRatioMaximum = 0.25;

    public const double BlockingScaleSpreadRatioThreshold = 1.5;

    public const int DominantConsistentClusterMinimumCount = 6;

    public const double DominantConsistentClusterMinimumShare = 0.70;

    public const double DominantConsistentClusterMaximumSpreadRatio = 1.05;

    public int CheckedCount => Checks.Count(check => check.Status is not MeasurementConsistencyStatus.Unchecked);

    public int ConsistentCount => Checks.Count(check => check.Status == MeasurementConsistencyStatus.Consistent);

    public int OutlierCount => Checks.Count(check => check.Status == MeasurementConsistencyStatus.Outlier);

    public bool HasOutliers => OutlierCount > 0;

    public double ConsistentRatio =>
        CheckedCount == 0 ? 0 : Math.Round(ConsistentCount / (double)CheckedCount, 6);

    public double OutlierRatio =>
        CheckedCount == 0 ? 0 : Math.Round(OutlierCount / (double)CheckedCount, 6);

    public double? ConsistentScaleSpreadRatio => CalculateConsistentScaleSpreadRatio();

    public bool HasDominantConsistentCluster =>
        HasReliableCalibration
        && ConsistentCount >= DominantConsistentClusterMinimumCount
        && ConsistentRatio >= DominantConsistentClusterMinimumShare
        && ConsistentScaleSpreadRatio is <= DominantConsistentClusterMaximumSpreadRatio;

    public bool HasBlockingOutliers =>
        HasOutliers
        && !HasDominantConsistentCluster
        && (OutlierCount > NonBlockingOutlierCountMaximum
            || OutlierRatio > NonBlockingOutlierRatioMaximum
            || DimensionScaleSpreadRatio is >= BlockingScaleSpreadRatioThreshold);

    public bool HasTolerableOutliers => HasOutliers && !HasBlockingOutliers;

    private double? CalculateConsistentScaleSpreadRatio()
    {
        var values = Checks
            .Where(check => check.Status == MeasurementConsistencyStatus.Consistent)
            .Select(check => check.ImpliedMillimetersPerDrawingUnit)
            .Where(value => value > 0)
            .Order()
            .ToArray();
        if (values.Length < 2)
        {
            return null;
        }

        return values[0] <= 0 ? null : Math.Round(values[^1] / values[0], 6);
    }
}
