namespace OpenPlanTrace;

internal static class StructuralCandidateDominance
{
    private static readonly string[] RepresentationReferenceMarkers =
    [
        "duplicate wall-face line already represented by stronger paired wall body ",
        "recovered duplicate wall body already represented by stronger nearby paired wall body ",
        "wall already represented by clean topology span from wall "
    ];

    private static readonly IReadOnlySet<StructuralEvidenceSignalKind> BlockingSignalKinds =
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

    public static IReadOnlyDictionary<string, string> FindDominatedCandidates(
        StructuralEvidenceGraph graph,
        StructuralSolverOptions options)
    {
        var candidatesById = graph.WallCandidates
            .ToDictionary(candidate => candidate.Id, StringComparer.Ordinal);
        var proposals = graph.Relations
            .Where(relation => relation.Kind == StructuralEvidenceRelationKind.Duplicate)
            .SelectMany(relation =>
            {
                if (!candidatesById.TryGetValue(relation.FirstCandidateId, out var first)
                    || !candidatesById.TryGetValue(relation.SecondCandidateId, out var second))
                {
                    return Array.Empty<DominanceProposal>();
                }

                return new[]
                {
                    TryCreateProposal(first, second, options),
                    TryCreateProposal(second, first, options)
                }
                .Where(proposal => proposal is not null)
                .Select(proposal => proposal!);
            })
            .Concat(FindExplicitRepresentationProposals(
                graph.WallCandidates,
                options))
            .Concat(FindSharedPrimitiveRepresentationProposals(
                graph.WallCandidates,
                options))
            .OrderByDescending(proposal => proposal.Dominator.DrawingLength)
            .ThenByDescending(proposal => DominanceQuality(proposal.Dominator))
            .ThenBy(proposal => proposal.Dominator.Id, StringComparer.Ordinal)
            .ThenBy(proposal => proposal.Dominated.Id, StringComparer.Ordinal)
            .ToArray();

        var dominatedBy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var proposal in proposals)
        {
            if (dominatedBy.TryGetValue(proposal.Dominated.Id, out var existingId)
                && candidatesById.TryGetValue(existingId, out var existing)
                && CompareDominators(existing, proposal.Dominator) >= 0)
            {
                continue;
            }

            dominatedBy[proposal.Dominated.Id] = proposal.Dominator.Id;
        }

        foreach (var candidateId in dominatedBy.Keys.ToArray())
        {
            var root = candidateId;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (dominatedBy.TryGetValue(root, out var next) && visited.Add(root))
            {
                root = next;
            }

            if (!string.Equals(root, candidateId, StringComparison.Ordinal))
            {
                dominatedBy[candidateId] = root;
            }
        }

        return dominatedBy;
    }

    private static IEnumerable<DominanceProposal> FindSharedPrimitiveRepresentationProposals(
        IReadOnlyList<StructuralWallCandidate> candidates,
        StructuralSolverOptions options)
    {
        var dominators = candidates
            .Where(candidate =>
                candidate.IsEligible
                && candidate.WasAcceptedByPreliminaryPipeline
                && candidate.HasIndependentWallBodyEvidence
                && !HasBlockingSemanticEvidence(candidate))
            .OrderByDescending(candidate => candidate.DrawingLength)
            .ThenByDescending(DominanceQuality)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();

        foreach (var represented in candidates
            .Where(candidate =>
                candidate.IsEligible
                && !candidate.HasIndependentWallBodyEvidence
                && candidate.DrawingLength >= Math.Max(
                    options.MinimumCandidateLength * 4.0,
                    options.MaximumContinuationGap * 3.0)
                && candidate.SourcePrimitiveIds.Count >= 2)
                     .OrderBy(candidate => candidate.Id, StringComparer.Ordinal))
        {
            var representedPrimitiveIds = represented.SourcePrimitiveIds
                .ToHashSet(StringComparer.Ordinal);
            var proposal = dominators
                .Where(dominator =>
                    !string.Equals(
                        dominator.Id,
                        represented.Id,
                        StringComparison.Ordinal))
                .Where(dominator =>
                {
                    var minimumSharedPrimitiveCount = Math.Max(
                        2,
                        (int)Math.Ceiling(representedPrimitiveIds.Count * 0.35));
                    return dominator.SourcePrimitiveIds.Count(sourceId =>
                            representedPrimitiveIds.Contains(sourceId))
                        >= minimumSharedPrimitiveCount;
                })
                .Where(dominator =>
                    represented.SourceRoomIds.All(roomId =>
                        dominator.SourceRoomIds.Contains(roomId, StringComparer.Ordinal))
                    && represented.SourceOpeningIds.All(openingId =>
                        dominator.SourceOpeningIds.Contains(openingId, StringComparer.Ordinal)))
                .Select(dominator => TryCreateSharedPrimitiveRepresentationProposal(
                    dominator,
                    represented,
                    options))
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!)
                .OrderByDescending(candidate => DominanceQuality(candidate.Dominator))
                .ThenByDescending(candidate => candidate.Dominator.DrawingLength)
                .ThenBy(candidate => candidate.Dominator.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (proposal is not null)
            {
                yield return proposal;
            }
        }
    }

    private static DominanceProposal? TryCreateSharedPrimitiveRepresentationProposal(
        StructuralWallCandidate dominator,
        StructuralWallCandidate represented,
        StructuralSolverOptions options)
    {
        if (dominator.PageNumber != represented.PageNumber
            || !CompatibleWallTypes(dominator.WallType, represented.WallType)
            || dominator.DrawingLength
            < represented.DrawingLength * options.MinimumDominantWallLengthRatio
            || dominator.UnaryScore
            < represented.UnaryScore - options.MaximumDominantWallScoreDeficit
            || dominator.Confidence.Value
            < represented.Confidence.Value - options.MaximumDominantWallConfidenceDeficit)
        {
            return null;
        }

        var angleTolerance = options.AngleToleranceDegrees * Math.PI / 180.0;
        if (!StructuralGeometry.AreParallel(
                dominator.CenterLine,
                represented.CenterLine,
                angleTolerance))
        {
            return null;
        }

        var maximumAxisDistance = Math.Max(
            options.AxisTolerance,
            Math.Min(8.0, dominator.Thickness * 0.85));
        if (StructuralGeometry.PerpendicularDistance(
                dominator.CenterLine,
                represented.CenterLine) > maximumAxisDistance)
        {
            return null;
        }

        var direction = StructuralGeometry.UnitDirection(dominator.CenterLine);
        var dominantRange = StructuralGeometry.ProjectionRange(
            dominator.CenterLine,
            direction);
        var representedRange = StructuralGeometry.ProjectionRange(
            represented.CenterLine,
            direction);
        var endpointTolerance = Math.Max(
            options.EndpointTolerance,
            options.MaximumContinuationGap);
        var contained =
            representedRange.Start >= dominantRange.Start - endpointTolerance
            && representedRange.End <= dominantRange.End + endpointTolerance;
        var overlapRatio = StructuralGeometry.OverlapLength(
            dominator.CenterLine,
            represented.CenterLine) / Math.Max(1e-9, represented.DrawingLength);
        return contained && overlapRatio >= options.DuplicateOverlapRatio
            ? new DominanceProposal(dominator, represented)
            : null;
    }

    private static IEnumerable<DominanceProposal> FindExplicitRepresentationProposals(
        IReadOnlyList<StructuralWallCandidate> candidates,
        StructuralSolverOptions options)
    {
        var candidatesBySourceWallId = candidates
            .SelectMany(candidate => candidate.SourceWallIds.Select(sourceWallId =>
                (SourceWallId: sourceWallId, Candidate: candidate)))
            .GroupBy(item => item.SourceWallId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.Candidate)
                    .DistinctBy(candidate => candidate.Id, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        foreach (var represented in candidates)
        {
            foreach (var sourceWallId in ReferencedRepresentationWallIds(represented.Evidence))
            {
                if (!candidatesBySourceWallId.TryGetValue(sourceWallId, out var dominators))
                {
                    continue;
                }

                var proposal = dominators
                    .Where(dominator =>
                        !string.Equals(
                            dominator.Id,
                            represented.Id,
                            StringComparison.Ordinal))
                    .Select(dominator => TryCreateExplicitRepresentationProposal(
                        dominator,
                        represented,
                        options))
                    .Where(candidate => candidate is not null)
                    .Select(candidate => candidate!)
                    .OrderByDescending(candidate => DominanceQuality(candidate.Dominator))
                    .ThenByDescending(candidate => candidate.Dominator.DrawingLength)
                    .ThenBy(candidate => candidate.Dominator.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (proposal is not null)
                {
                    yield return proposal;
                }
            }
        }
    }

    private static DominanceProposal? TryCreateExplicitRepresentationProposal(
        StructuralWallCandidate dominator,
        StructuralWallCandidate represented,
        StructuralSolverOptions options)
    {
        if (!dominator.IsEligible
            || !represented.IsEligible
            || dominator.PageNumber != represented.PageNumber
            || !dominator.WasAcceptedByPreliminaryPipeline
            || !dominator.HasIndependentWallBodyEvidence
            || HasBlockingSemanticEvidence(dominator)
            || !CompatibleWallTypes(dominator.WallType, represented.WallType)
            || dominator.UnaryScore
            < represented.UnaryScore - options.MaximumDominantWallScoreDeficit
            || dominator.Confidence.Value
            < represented.Confidence.Value - 0.20)
        {
            return null;
        }

        var angleTolerance = options.AngleToleranceDegrees * Math.PI / 180.0;
        if (!StructuralGeometry.AreParallel(
                dominator.CenterLine,
                represented.CenterLine,
                angleTolerance))
        {
            return null;
        }

        var axisDistance = StructuralGeometry.PerpendicularDistance(
            dominator.CenterLine,
            represented.CenterLine);
        var maximumAxisDistance = Math.Min(
            24.0,
            Math.Max(
                options.AxisTolerance * 4.0,
                (dominator.Thickness + represented.Thickness) * 1.50));
        var overlapRatio = StructuralGeometry.OverlapLength(
            dominator.CenterLine,
            represented.CenterLine) / Math.Max(1e-9, represented.DrawingLength);
        return axisDistance <= maximumAxisDistance
            && overlapRatio >= 0.75
                ? new DominanceProposal(dominator, represented)
                : null;
    }

    private static IEnumerable<string> ReferencedRepresentationWallIds(
        IReadOnlyList<string> evidence)
    {
        foreach (var item in evidence)
        {
            foreach (var marker in RepresentationReferenceMarkers)
            {
                var markerIndex = item.IndexOf(
                    marker,
                    StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0)
                {
                    continue;
                }

                var valueStart = markerIndex + marker.Length;
                var valueEnd = item.IndexOfAny([';', ' ', '\t', '\r', '\n'], valueStart);
                var sourceWallId = valueEnd < 0
                    ? item[valueStart..]
                    : item[valueStart..valueEnd];
                if (!string.IsNullOrWhiteSpace(sourceWallId))
                {
                    yield return sourceWallId.Trim();
                }
            }
        }
    }

    private static bool CompatibleWallTypes(
        WallType first,
        WallType second) =>
        first == second
        || first == WallType.Unknown
        || second == WallType.Unknown;

    private static DominanceProposal? TryCreateProposal(
        StructuralWallCandidate dominator,
        StructuralWallCandidate dominated,
        StructuralSolverOptions options)
    {
        if (!dominator.IsEligible
            || !dominated.IsEligible
            || dominator.PageNumber != dominated.PageNumber
            || !dominator.WasAcceptedByPreliminaryPipeline
            || !dominator.HasIndependentWallBodyEvidence
            || HasBlockingSemanticEvidence(dominator)
            || dominator.DrawingLength
            < dominated.DrawingLength * options.MinimumDominantWallLengthRatio
            || dominator.UnaryScore
            < dominated.UnaryScore - options.MaximumDominantWallScoreDeficit
            || dominator.Confidence.Value
            < dominated.Confidence.Value - options.MaximumDominantWallConfidenceDeficit)
        {
            return null;
        }

        var angleTolerance = options.AngleToleranceDegrees * Math.PI / 180.0;
        if (!StructuralGeometry.AreParallel(
                dominator.CenterLine,
                dominated.CenterLine,
                angleTolerance)
            || StructuralGeometry.PerpendicularDistance(
                dominator.CenterLine,
                dominated.CenterLine) > options.AxisTolerance)
        {
            return null;
        }

        var direction = StructuralGeometry.UnitDirection(dominator.CenterLine);
        var dominantRange = StructuralGeometry.ProjectionRange(dominator.CenterLine, direction);
        var coveredRange = StructuralGeometry.ProjectionRange(dominated.CenterLine, direction);
        var endpointTolerance = Math.Max(
            options.EndpointTolerance,
            options.MaximumContinuationGap);
        var contained =
            coveredRange.Start >= dominantRange.Start - endpointTolerance
            && coveredRange.End <= dominantRange.End + endpointTolerance;
        var overlapRatio = StructuralGeometry.OverlapLength(
            dominator.CenterLine,
            dominated.CenterLine) / Math.Max(1e-9, dominated.DrawingLength);
        return contained && overlapRatio >= options.DuplicateOverlapRatio
            ? new DominanceProposal(dominator, dominated)
            : null;
    }

    private static bool HasBlockingSemanticEvidence(StructuralWallCandidate candidate) =>
        candidate.Signals.Any(signal =>
            signal.Weight < 0
            && BlockingSignalKinds.Contains(signal.Kind));

    private static int CompareDominators(
        StructuralWallCandidate first,
        StructuralWallCandidate second)
    {
        var quality = DominanceQuality(first).CompareTo(DominanceQuality(second));
        if (quality != 0)
        {
            return quality;
        }

        var length = first.DrawingLength.CompareTo(second.DrawingLength);
        if (length != 0)
        {
            return length;
        }

        return -string.CompareOrdinal(first.Id, second.Id);
    }

    private static double DominanceQuality(StructuralWallCandidate candidate)
    {
        var wallBody = candidate.Signals
            .Where(signal => signal.Kind == StructuralEvidenceSignalKind.WallBody)
            .Select(signal => signal.Weight)
            .DefaultIfEmpty(0)
            .Max();
        var oppositeRoomSupport = candidate.Signals.Any(signal =>
            signal.Kind == StructuralEvidenceSignalKind.OppositeRoomBoundary
            && signal.Weight > 0);
        return (wallBody * 3.0)
            + candidate.UnaryScore
            + candidate.Confidence.Value
            + (oppositeRoomSupport ? 0.20 : 0);
    }

    private sealed record DominanceProposal(
        StructuralWallCandidate Dominator,
        StructuralWallCandidate Dominated);
}
