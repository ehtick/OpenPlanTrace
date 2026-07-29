namespace OpenPlanTrace;

internal sealed class DimensionStructuralEvidenceProducer : IStructuralEvidenceProducer
{
    public string Name => "dimension-provenance";

    public void Produce(StructuralEvidenceBuildContext context)
    {
        var dimensionFamiliesByPage = context.Source.Dimensions
            .SelectMany(dimension => dimension.SourcePrimitiveIds
                .Select(PrimitiveFamilyId)
                .Where(id => id is not null)
                .Select(id => (dimension.PageNumber, FamilyId: id!, dimension.Id)))
            .GroupBy(item => item.PageNumber)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(item => item.FamilyId, StringComparer.Ordinal)
                    .ToDictionary(
                        family => family.Key,
                        family => family
                            .Select(item => item.Id)
                            .Distinct(StringComparer.Ordinal)
                            .Order(StringComparer.Ordinal)
                            .ToArray(),
                        StringComparer.Ordinal));

        foreach (var candidate in context.Candidates.Drafts)
        {
            if (!dimensionFamiliesByPage.TryGetValue(
                    candidate.PageNumber,
                    out var dimensionFamilies))
            {
                continue;
            }

            var candidateFamilyIds = candidate.SourcePrimitiveIds
                .Select(PrimitiveFamilyId)
                .Where(id => id is not null)
                .Select(id => id!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var matchingFamilyIds = candidateFamilyIds
                .Where(dimensionFamilies.ContainsKey)
                .ToHashSet(StringComparer.Ordinal);
            var matchingDimensionIds = matchingFamilyIds
                .SelectMany(id => dimensionFamilies[id])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (matchingDimensionIds.Length == 0)
            {
                continue;
            }

            var familyCoverage = matchingFamilyIds.Count
                / (double)Math.Max(1, candidateFamilyIds.Length);
            var hasDimensionLayerSemantics =
                candidate.Evidence.Any(item =>
                    item.Contains(
                        "classified Dimension",
                        StringComparison.OrdinalIgnoreCase))
                && candidate.Evidence.Any(item =>
                    item.Contains(
                        "dimension-like text",
                        StringComparison.OrdinalIgnoreCase));
            var dimensionLayerOwnedDetail =
                hasDimensionLayerSemantics
                && !candidate.HasExplicitFilledWallBodyEvidence
                && !candidate.HasTrustedExteriorShellEvidence
                && !candidate.HasTrustedRoomConfirmedWallEvidence;
            var weight = dimensionLayerOwnedDetail
                ? -1.10
                : -Math.Clamp(1.10 * familyCoverage, 0.08, 1.10);
            var ownershipDescription = dimensionLayerOwnedDetail
                ? "source-family overlap is corroborated by dimension-layer semantics"
                : familyCoverage >= 0.50
                    ? "dimension-owned primitive families dominate the candidate"
                    : "dimension provenance is a partial source-family overlap, not dominant ownership";
            candidate.AddSignal(new StructuralEvidenceSignal(
                $"signal:{candidate.Id}:dimension-provenance",
                StructuralEvidenceSignalKind.DimensionOrAnnotation,
                weight,
                string.Join(",", matchingDimensionIds),
                $"{ownershipDescription}: {matchingFamilyIds.Count}/{candidateFamilyIds.Length} family(s), coverage {familyCoverage:P0}, detected dimension annotation(s) {string.Join(",", matchingDimensionIds)}",
                candidate.SourcePrimitiveIds
                    .Where(id => PrimitiveFamilyId(id) is { } family
                        && matchingFamilyIds.Contains(family))
                    .Order(StringComparer.Ordinal)
                    .ToArray()));
        }
    }

    private static string? PrimitiveFamilyId(string sourcePrimitiveId)
    {
        var marker = sourcePrimitiveId.IndexOf(":subpath:", StringComparison.Ordinal);
        if (marker > 0)
        {
            return sourcePrimitiveId[..marker];
        }

        return sourcePrimitiveId.Contains(":path:", StringComparison.Ordinal)
            ? sourcePrimitiveId
            : null;
    }
}
