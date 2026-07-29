namespace OpenPlanTrace;

internal sealed class WallBodyPlausibilityStructuralEvidenceProducer : IStructuralEvidenceProducer
{
    private const int MinimumProfileSampleCount = 3;

    public string Name => "wall-body-plausibility";

    public void Produce(StructuralEvidenceBuildContext context)
    {
        foreach (var pageCandidates in context.Candidates.Drafts
                     .GroupBy(candidate => candidate.PageNumber)
                     .OrderBy(group => group.Key))
        {
            var profile = BuildProfile(
                pageCandidates.ToArray(),
                context.Source.DefaultWallThickness);
            foreach (var candidate in pageCandidates)
            {
                if (!candidate.HasParallelFaceEvidence
                    || candidate.HasExplicitFilledWallBodyEvidence
                    || candidate.Thickness <= profile.ModerateOutlierThreshold)
                {
                    continue;
                }

                var severeInteriorOutlier =
                    candidate.WallType != WallType.Exterior
                    && candidate.Thickness >= profile.SevereOutlierThreshold;
                var shortReviewDimensionPair =
                    candidate.HasReviewWallEvidence
                    && candidate.HasDimensionOrAnnotationEvidence
                    && candidate.SourceRoomIds.Count == 0
                    && candidate.DrawingLength <= profile.MaximumGuardedDetailLength;
                if (!severeInteriorOutlier && !shortReviewDimensionPair)
                {
                    continue;
                }

                var weight = severeInteriorOutlier ? -1.35 : -1.20;
                candidate.AddSignal(
                    new StructuralEvidenceSignal(
                        $"signal:{candidate.Id}:wall-body-thickness-outlier",
                        StructuralEvidenceSignalKind.WallBodyThicknessOutlier,
                        weight,
                        $"page:{candidate.PageNumber}:wall-thickness-profile",
                        severeInteriorOutlier
                            ? $"unfilled parallel-face body thickness {candidate.Thickness:0.###} is an implausible interior outlier against page median {profile.DominantThickness:0.###}; severe threshold {profile.SevereOutlierThreshold:0.###} from {profile.SampleCount} trusted wall bodies"
                            : $"short review-only dimension-owned parallel pair thickness {candidate.Thickness:0.###} is inconsistent with page median {profile.DominantThickness:0.###}; guarded threshold {profile.ModerateOutlierThreshold:0.###} from {profile.SampleCount} trusted wall bodies",
                        candidate.SourcePrimitiveIds
                            .Order(StringComparer.Ordinal)
                            .ToArray()));
            }
        }
    }

    private static WallThicknessProfile BuildProfile(
        IReadOnlyList<StructuralCandidateRegistry.CandidateDraft> candidates,
        double defaultWallThickness)
    {
        var safeDefault = Math.Max(0.5, defaultWallThickness);
        var profileSampleLimit = Math.Max(12.0, safeDefault * 3.0);
        var samples = candidates
            .Where(candidate =>
                candidate.WasAcceptedByPreliminaryPipeline
                && candidate.HasIndependentWallBodyEvidence
                && !candidate.HasBlockingSemanticEvidence
                && !candidate.HasReviewWallEvidence
                && candidate.Thickness is > 0
                && candidate.Thickness <= profileSampleLimit)
            .Select(candidate => candidate.Thickness)
            .Order()
            .ToArray();

        var dominant = samples.Length >= MinimumProfileSampleCount
            ? Median(samples)
            : safeDefault;
        var deviations = samples
            .Select(sample => Math.Abs(sample - dominant))
            .Order()
            .ToArray();
        var medianAbsoluteDeviation = deviations.Length >= MinimumProfileSampleCount
            ? Median(deviations)
            : 0;
        var moderateThreshold = Math.Max(
            dominant * 1.20,
            Math.Max(
                dominant + Math.Max(1.25, medianAbsoluteDeviation * 1.50),
                safeDefault * 1.30));
        var severeThreshold = Math.Max(
            dominant * 2.20,
            Math.Max(
                dominant + Math.Max(6.0, medianAbsoluteDeviation * 3.0),
                safeDefault * 3.0));
        var maximumGuardedDetailLength = Math.Max(
            90.0,
            Math.Max(dominant * 18.0, safeDefault * 20.0));

        return new WallThicknessProfile(
            samples.Length,
            dominant,
            moderateThreshold,
            severeThreshold,
            maximumGuardedDetailLength);
    }

    private static double Median(IReadOnlyList<double> sortedValues)
    {
        var middle = sortedValues.Count / 2;
        return sortedValues.Count % 2 == 0
            ? (sortedValues[middle - 1] + sortedValues[middle]) / 2.0
            : sortedValues[middle];
    }

    private sealed record WallThicknessProfile(
        int SampleCount,
        double DominantThickness,
        double ModerateOutlierThreshold,
        double SevereOutlierThreshold,
        double MaximumGuardedDetailLength);
}
