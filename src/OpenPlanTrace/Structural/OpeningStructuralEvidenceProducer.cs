namespace OpenPlanTrace;

internal sealed class OpeningStructuralEvidenceProducer : IStructuralEvidenceProducer
{
    public string Name => "opening-host-evidence";

    public void Produce(StructuralEvidenceBuildContext context)
    {
        var roomContexts =
            RoomBoundaryStructuralEvidenceProducer.ClassifyRoomContexts(
                context.Source.Rooms);
        var trustedRoomIds = roomContexts
            .Where(item => item.Value == StructuralRoomLoopContext.Indoor)
            .Select(item => item.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var opening in context.Source.Openings
                     .OrderBy(opening => opening.PageNumber)
                     .ThenBy(opening => opening.Id, StringComparer.Ordinal))
        {
            var support = StructuralOpeningSupport.Assess(
                opening,
                trustedRoomIds);
            var hostWallIds = opening.HostWallIds
                .Concat(opening.AdjacentWallIds)
                .Append(opening.WallId ?? string.Empty)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);
            var hostCandidates = context.Candidates.Drafts
                .Where(candidate => candidate.PageNumber == opening.PageNumber)
                .Where(candidate =>
                    candidate.SourceWallIds.Any(hostWallIds.Contains)
                    || candidate.CenterLine.Bounds
                        .Inflate(Math.Max(context.Options.AxisTolerance * 2, candidate.Thickness))
                        .Intersects(opening.Bounds))
                .OrderByDescending(candidate => candidate.SourceWallIds.Any(hostWallIds.Contains))
                .ThenBy(candidate => candidate.CenterLine.DistanceToPoint(opening.Bounds.Center))
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                .Take(8)
                .ToArray();

            foreach (var candidate in hostCandidates)
            {
                candidate.SourceOpeningIds.Add(opening.Id);
                var explicitHost = candidate.SourceWallIds.Any(hostWallIds.Contains);
                var supportsStructure =
                    !support.HasAmbiguousRoomTopology
                    && !candidate.HasRejectedWallEvidence;
                if (explicitHost && supportsStructure)
                {
                    candidate.AddOrigin(StructuralCandidateOrigin.OpeningHost);
                }

                var weight = !supportsStructure
                    ? 0
                    : explicitHost
                        ? 0.10
                        : 0.025;
                candidate.AddSignal(
                    new StructuralEvidenceSignal(
                        $"signal:{candidate.Id}:opening:{opening.Id}",
                        StructuralEvidenceSignalKind.OpeningHost,
                        weight,
                        opening.Id,
                        candidate.HasRejectedWallEvidence
                            ? $"opening {opening.Id} proximity retained as provenance but not support after final wall rejection"
                            : support.HasAmbiguousRoomTopology
                                ? $"{support.Evidence}; opening proximity retained as provenance without structural support"
                            : explicitHost
                                ? $"candidate explicitly hosts opening {opening.Id}"
                                : $"candidate is geometrically near opening {opening.Id}",
                        opening.SourcePrimitiveIds));
            }

            context.OpeningConstraints.Add(
                new StructuralOpeningConstraint(
                    $"structural-opening:{opening.Id}",
                    opening.Id,
                    opening.PageNumber,
                    OpeningLine(opening),
                    hostCandidates.Select(candidate => candidate.Id).ToArray(),
                    opening.Confidence,
                    opening.SourcePrimitiveIds,
                    opening.Evidence
                        .Append(support.Evidence)
                        .Append($"matched {hostCandidates.Length} possible structural host candidate(s)")
                        .ToArray()));
        }
    }

    private static PlanLineSegment OpeningLine(OpeningCandidate opening)
    {
        if (opening.CenterLine.Length > 0)
        {
            return StructuralGeometry.Canonicalize(opening.CenterLine);
        }

        return opening.Bounds.Width >= opening.Bounds.Height
            ? new PlanLineSegment(
                new PlanPoint(opening.Bounds.Left, opening.Bounds.Center.Y),
                new PlanPoint(opening.Bounds.Right, opening.Bounds.Center.Y))
            : new PlanLineSegment(
                new PlanPoint(opening.Bounds.Center.X, opening.Bounds.Top),
                new PlanPoint(opening.Bounds.Center.X, opening.Bounds.Bottom));
    }
}
