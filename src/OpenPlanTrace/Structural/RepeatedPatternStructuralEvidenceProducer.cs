namespace OpenPlanTrace;

internal sealed class RepeatedPatternStructuralEvidenceProducer : IStructuralEvidenceProducer
{
    private const int MinimumRepeatedAxisCount = 4;
    private const int StrongRepeatedAxisCount = 6;

    public string Name => "repeated-detail-pattern-evidence";

    public void Produce(StructuralEvidenceBuildContext context)
    {
        var protectedRoomIds = RoomBoundaryStructuralEvidenceProducer
            .ClassifyRoomContexts(context.Source.Rooms)
            .Where(item => item.Value == StructuralRoomLoopContext.Indoor)
            .Select(item => item.Key)
            .ToHashSet(StringComparer.Ordinal);

        AddUpstreamDetailSignals(context, protectedRoomIds);
        AddDeclaredPatternSignals(context, protectedRoomIds);
        AddInferredPatternSignals(context, protectedRoomIds);
    }

    private static void AddUpstreamDetailSignals(
        StructuralEvidenceBuildContext context,
        IReadOnlySet<string> protectedRoomIds)
    {
        foreach (var candidate in context.Candidates.Drafts
                     .Where(candidate =>
                         candidate.Origins.HasFlag(StructuralCandidateOrigin.DetectedWall)
                         && HasUpstreamRepeatedDetailEvidence(candidate))
                     .OrderBy(candidate => candidate.PageNumber)
                     .ThenBy(candidate => candidate.Id, StringComparer.Ordinal))
        {
            if (candidate.HasStrongNegativeEvidence
                || HasProtectedStructuralSupport(candidate, protectedRoomIds)
                || HasVerifiedFilledWallBodySupport(candidate))
            {
                continue;
            }

            candidate.AddSignal(
                new StructuralEvidenceSignal(
                    $"signal:{candidate.Id}:upstream-repeated-detail-review",
                    StructuralEvidenceSignalKind.RepeatedDetailPattern,
                    -1.10,
                    candidate.SourceWallIds.Order(StringComparer.Ordinal).FirstOrDefault()
                        ?? candidate.Id,
                    "upstream wall review identified dense local stair, comb, grid, or repeated detail linework without independent room, opening, or exterior-shell support",
                    candidate.SourcePrimitiveIds
                        .Order(StringComparer.Ordinal)
                        .ToArray()));
        }
    }

    private static void AddDeclaredPatternSignals(
        StructuralEvidenceBuildContext context,
        IReadOnlySet<string> protectedRoomIds)
    {
        foreach (var pattern in context.Source.SurfacePatterns
                     .Where(pattern =>
                         pattern.ExcludedFromWallDetection
                         || pattern.ExcludedFromStructuralTopology)
                     .OrderBy(pattern => pattern.PageNumber)
                     .ThenBy(pattern => pattern.Id, StringComparer.Ordinal))
        {
            foreach (var candidate in context.Candidates.Drafts
                         .Where(candidate =>
                             candidate.PageNumber == pattern.PageNumber
                             && candidate.Origins.HasFlag(StructuralCandidateOrigin.DetectedWall))
                         .OrderBy(candidate => candidate.Id, StringComparer.Ordinal))
            {
                if (candidate.HasStrongNegativeEvidence
                    || HasProtectedStructuralSupport(candidate, protectedRoomIds)
                    || !MatchesDeclaredPattern(candidate, pattern, context))
                {
                    continue;
                }

                var weight = pattern.ExcludedFromStructuralTopology ? -1.10 : -0.78;
                candidate.AddSignal(
                    new StructuralEvidenceSignal(
                        $"signal:{candidate.Id}:declared-repeated-pattern:{pattern.Id}",
                        StructuralEvidenceSignalKind.RepeatedDetailPattern,
                        weight,
                        pattern.Id,
                        $"candidate belongs to declared {PatternDescription(pattern)} and lacks independent room, opening, or exterior-shell support",
                        candidate.SourcePrimitiveIds
                            .Concat(pattern.SourcePrimitiveIds)
                            .Distinct(StringComparer.Ordinal)
                            .Order(StringComparer.Ordinal)
                            .ToArray()));
            }
        }
    }

    private static void AddInferredPatternSignals(
        StructuralEvidenceBuildContext context,
        IReadOnlySet<string> protectedRoomIds)
    {
        var candidates = context.Candidates.Drafts
            .Where(candidate =>
                candidate.IsEligible
                && candidate.Origins.HasFlag(StructuralCandidateOrigin.DetectedWall)
                && candidate.CenterLine.Length >= context.Options.MinimumCandidateLength)
            .OrderBy(candidate => candidate.PageNumber)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
        var angleTolerance = Math.Max(
            context.Options.AngleToleranceDegrees * 1.5,
            4.0) * Math.PI / 180.0;
        var familyNumber = 0;

        foreach (var orientationGroup in candidates
                     .GroupBy(candidate => (
                         candidate.PageNumber,
                         Orientation: StructuralGeometry.OrientationBucket(
                             candidate.CenterLine,
                             angleTolerance)))
                     .OrderBy(group => group.Key.PageNumber)
                     .ThenBy(group => group.Key.Orientation))
        {
            var maximumRepeatedLength = MaximumRepeatedLength(
                context,
                orientationGroup.Key.PageNumber,
                orientationGroup);
            var eligible = orientationGroup
                .Where(candidate => candidate.DrawingLength <= Math.Max(
                    context.Options.MinimumCandidateLength * 5.0,
                    maximumRepeatedLength))
                .ToArray();

            foreach (var component in FindRepeatComponents(
                         eligible,
                         context,
                         angleTolerance))
            {
                var family = TryDescribeFamily(component, context, angleTolerance);
                if (family is null)
                {
                    continue;
                }

                familyNumber++;
                var familyId =
                    $"inferred-repeat:p{orientationGroup.Key.PageNumber}:{familyNumber:000}";
                foreach (var candidate in family.Candidates
                             .OrderBy(candidate => candidate.Id, StringComparer.Ordinal))
                {
                    if (candidate.HasStrongNegativeEvidence
                        || HasProtectedStructuralSupport(candidate, protectedRoomIds))
                    {
                        continue;
                    }

                    var weight = InferredPatternWeight(candidate, family);
                    if (weight >= 0)
                    {
                        continue;
                    }

                    candidate.AddSignal(
                        new StructuralEvidenceSignal(
                            $"signal:{candidate.Id}:inferred-repeated-pattern:{familyId}",
                            StructuralEvidenceSignalKind.RepeatedDetailPattern,
                            weight,
                            familyId,
                            $"candidate belongs to inferred repeated detail family with {family.AxisCount} parallel axes, {family.MedianSpacing:0.###} median spacing, {family.SpacingRegularity:P0} spacing regularity, and {family.AlignmentRatio:P0} span alignment",
                            candidate.SourcePrimitiveIds
                                .Order(StringComparer.Ordinal)
                                .ToArray()));
                }
            }
        }
    }

    private static IReadOnlyList<IReadOnlyList<StructuralCandidateRegistry.CandidateDraft>>
        FindRepeatComponents(
            IReadOnlyList<StructuralCandidateRegistry.CandidateDraft> candidates,
            StructuralEvidenceBuildContext context,
            double angleTolerance)
    {
        var visited = new bool[candidates.Count];
        var components = new List<IReadOnlyList<StructuralCandidateRegistry.CandidateDraft>>();
        for (var index = 0; index < candidates.Count; index++)
        {
            if (visited[index])
            {
                continue;
            }

            var queue = new Queue<int>();
            var component = new List<StructuralCandidateRegistry.CandidateDraft>();
            visited[index] = true;
            queue.Enqueue(index);
            while (queue.Count > 0)
            {
                var currentIndex = queue.Dequeue();
                var current = candidates[currentIndex];
                component.Add(current);
                for (var otherIndex = 0; otherIndex < candidates.Count; otherIndex++)
                {
                    if (visited[otherIndex]
                        || !LooksLikeRepeatedNeighbor(
                            current,
                            candidates[otherIndex],
                            context,
                            angleTolerance))
                    {
                        continue;
                    }

                    visited[otherIndex] = true;
                    queue.Enqueue(otherIndex);
                }
            }

            if (component.Count >= MinimumRepeatedAxisCount)
            {
                components.Add(component);
            }
        }

        return components;
    }

    private static bool LooksLikeRepeatedNeighbor(
        StructuralCandidateRegistry.CandidateDraft first,
        StructuralCandidateRegistry.CandidateDraft second,
        StructuralEvidenceBuildContext context,
        double angleTolerance)
    {
        if (!StructuralGeometry.AreParallel(
                first.CenterLine,
                second.CenterLine,
                angleTolerance))
        {
            return false;
        }

        var shorterLength = Math.Min(first.DrawingLength, second.DrawingLength);
        var longerLength = Math.Max(first.DrawingLength, second.DrawingLength);
        if (shorterLength / Math.Max(1e-9, longerLength) < 0.55
            || StructuralGeometry.OverlapRatio(
                first.CenterLine,
                second.CenterLine) < 0.58)
        {
            return false;
        }

        var spacing = StructuralGeometry.PerpendicularDistance(
            first.CenterLine,
            second.CenterLine);
        var duplicateAxisTolerance = DuplicateAxisTolerance(context);
        var maximumSpacing = Math.Min(
            shorterLength * 0.38,
            Math.Max(
                context.Options.AxisTolerance * 8.0,
                context.Source.DefaultWallThickness * 5.5));
        return spacing > duplicateAxisTolerance
            && spacing <= maximumSpacing;
    }

    private static RepeatedFamily? TryDescribeFamily(
        IReadOnlyList<StructuralCandidateRegistry.CandidateDraft> component,
        StructuralEvidenceBuildContext context,
        double angleTolerance)
    {
        var reference = component
            .OrderByDescending(candidate => candidate.DrawingLength)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .First();
        var normal = StructuralGeometry.UnitNormal(reference.CenterLine);
        var ordered = component
            .Where(candidate => StructuralGeometry.AreParallel(
                reference.CenterLine,
                candidate.CenterLine,
                angleTolerance))
            .Select(candidate => (
                Candidate: candidate,
                Axis: StructuralGeometry.Dot(candidate.CenterLine.Midpoint, normal)))
            .OrderBy(item => item.Axis)
            .ThenBy(item => item.Candidate.Id, StringComparer.Ordinal)
            .ToArray();
        var axisBands = BuildAxisBands(ordered, DuplicateAxisTolerance(context));
        if (axisBands.Count < MinimumRepeatedAxisCount)
        {
            return null;
        }

        var spacings = axisBands
            .Zip(axisBands.Skip(1), (first, second) => second.Axis - first.Axis)
            .Where(spacing => spacing > 1e-9)
            .Order()
            .ToArray();
        if (spacings.Length < MinimumRepeatedAxisCount - 1)
        {
            return null;
        }

        var medianSpacing = Median(spacings);
        var medianLength = Median(
            axisBands
                .SelectMany(band => band.Candidates)
                .Select(candidate => candidate.DrawingLength)
                .Order()
                .ToArray());
        if (medianSpacing <= 0
            || medianLength <= 0
            || medianSpacing / medianLength > 0.34)
        {
            return null;
        }

        var regularityTolerance = Math.Max(
            context.Options.AxisTolerance * 0.75,
            medianSpacing * 0.35);
        var regularity = spacings.Count(spacing =>
            Math.Abs(spacing - medianSpacing) <= regularityTolerance)
            / (double)spacings.Length;
        if (regularity < 0.60)
        {
            return null;
        }

        var representatives = axisBands
            .Select(band => band.Candidates
                .OrderByDescending(candidate => candidate.DrawingLength)
                .ThenByDescending(candidate => candidate.CurrentUnaryScore)
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                .First())
            .ToArray();
        var alignment = representatives
            .Select(candidate => StructuralGeometry.OverlapRatio(
                reference.CenterLine,
                candidate.CenterLine))
            .Average();
        if (alignment < 0.66)
        {
            return null;
        }

        var depth = axisBands[^1].Axis - axisBands[0].Axis;
        if (depth < medianSpacing * (MinimumRepeatedAxisCount - 1) * 0.85)
        {
            return null;
        }

        var highConfidence =
            axisBands.Count >= StrongRepeatedAxisCount
            && regularity >= 0.66
            && medianSpacing / medianLength <= 0.30
            && alignment >= 0.70;
        return new RepeatedFamily(
            axisBands.SelectMany(band => band.Candidates).Distinct().ToArray(),
            axisBands.Count,
            medianSpacing,
            regularity,
            alignment,
            highConfidence);
    }

    private static IReadOnlyList<AxisBand> BuildAxisBands(
        IReadOnlyList<(
            StructuralCandidateRegistry.CandidateDraft Candidate,
            double Axis)> ordered,
        double tolerance)
    {
        var bands = new List<AxisBand>();
        foreach (var item in ordered)
        {
            if (bands.Count == 0
                || Math.Abs(item.Axis - bands[^1].Axis) > tolerance)
            {
                bands.Add(new AxisBand(
                    item.Axis,
                    new List<StructuralCandidateRegistry.CandidateDraft>
                    {
                        item.Candidate
                    }));
                continue;
            }

            var band = bands[^1];
            band.Candidates.Add(item.Candidate);
            band.Axis = band.Candidates.Average(candidate =>
                StructuralGeometry.Dot(
                    candidate.CenterLine.Midpoint,
                    StructuralGeometry.UnitNormal(candidate.CenterLine)));
        }

        return bands;
    }

    private static double InferredPatternWeight(
        StructuralCandidateRegistry.CandidateDraft candidate,
        RepeatedFamily family)
    {
        if (!candidate.HasIndependentWallBodyEvidence)
        {
            return family.HighConfidence ? -0.86 : -0.68;
        }

        return family.HighConfidence ? -0.58 : 0;
    }

    private static bool HasProtectedStructuralSupport(
        StructuralCandidateRegistry.CandidateDraft candidate,
        IReadOnlySet<string> protectedRoomIds) =>
        candidate.SourceRoomIds.Any(protectedRoomIds.Contains)
        || candidate.SourceOpeningIds.Count > 0
        || candidate.Origins.HasFlag(StructuralCandidateOrigin.OpeningHost)
        || candidate.Origins.HasFlag(StructuralCandidateOrigin.ExteriorShell);

    private static bool HasUpstreamRepeatedDetailEvidence(
        StructuralCandidateRegistry.CandidateDraft candidate)
    {
        var evidence = string.Join(" | ", candidate.Evidence);
        return evidence.Contains(
                "dense local detail/stair-like linework",
                StringComparison.OrdinalIgnoreCase)
            || evidence.Contains(
                "repeated short detail",
                StringComparison.OrdinalIgnoreCase)
            || evidence.Contains(
                "stair tread linework",
                StringComparison.OrdinalIgnoreCase)
            || evidence.Contains(
                "dense repeated surface/detail",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasVerifiedFilledWallBodySupport(
        StructuralCandidateRegistry.CandidateDraft candidate)
    {
        var evidence = string.Join(" | ", candidate.Evidence);
        var hasFilledBody =
            evidence.Contains(
                "filled wall-solid primitive",
                StringComparison.OrdinalIgnoreCase)
            || evidence.Contains(
                "filled closed vector wall body",
                StringComparison.OrdinalIgnoreCase);
        var wasOriginallyStrong =
            evidence.Contains(
                "wall evidence assessment: StrongWallBody",
                StringComparison.OrdinalIgnoreCase)
            || evidence.Contains(
                "wall evidence: strong double-edge wall body",
                StringComparison.OrdinalIgnoreCase);
        return hasFilledBody && wasOriginallyStrong;
    }

    private static bool MatchesDeclaredPattern(
        StructuralCandidateRegistry.CandidateDraft candidate,
        SurfacePatternCandidate pattern,
        StructuralEvidenceBuildContext context)
    {
        if (!OrientationMatches(candidate.CenterLine, pattern.Orientation, context.Options))
        {
            return false;
        }

        var tolerance = Math.Max(
            context.Options.AxisTolerance * 2.0,
            context.Source.DefaultWallThickness);
        var bounds = pattern.Bounds.Inflate(tolerance);
        var insideSamples = 0;
        const int sampleCount = 7;
        for (var index = 0; index < sampleCount; index++)
        {
            if (bounds.Contains(
                    candidate.CenterLine.PointAt(index / (double)(sampleCount - 1)),
                    tolerance))
            {
                insideSamples++;
            }
        }

        var sharesSource = candidate.SourcePrimitiveIds.Any(
            pattern.SourcePrimitiveIds.Contains);
        var longSide = Math.Max(pattern.Bounds.Width, pattern.Bounds.Height);
        var lengthFitsPattern = candidate.DrawingLength <= Math.Max(
            context.Options.MinimumCandidateLength * 2.0,
            longSide * 1.35);
        return lengthFitsPattern
            && (insideSamples >= 6
                || (sharesSource && insideSamples >= 4));
    }

    private static bool OrientationMatches(
        PlanLineSegment line,
        SurfacePatternOrientation orientation,
        StructuralSolverOptions options)
    {
        if (orientation is
            SurfacePatternOrientation.Unknown
            or SurfacePatternOrientation.Orthogonal)
        {
            return true;
        }

        var tolerance = Math.Max(
            options.AngleToleranceDegrees,
            3.0) * Math.PI / 180.0;
        var angle = StructuralGeometry.NormalizeAngle(line.AngleRadians);
        var horizontalDeviation = Math.Min(angle, Math.PI - angle);
        var verticalDeviation = Math.Abs(angle - (Math.PI / 2.0));
        return orientation == SurfacePatternOrientation.Horizontal
            ? horizontalDeviation <= tolerance
            : verticalDeviation <= tolerance;
    }

    private static double DuplicateAxisTolerance(
        StructuralEvidenceBuildContext context) =>
        Math.Max(
            context.Options.AxisTolerance,
            context.Source.DefaultWallThickness * 1.15);

    private static double MaximumRepeatedLength(
        StructuralEvidenceBuildContext context,
        int pageNumber,
        IEnumerable<StructuralCandidateRegistry.CandidateDraft> candidates)
    {
        var mainRegion = context.Source.SheetRegions
            .Where(region =>
                region.PageNumber == pageNumber
                && region.Kind == RegionKind.MainFloorPlan)
            .OrderByDescending(region => region.Bounds.Area)
            .FirstOrDefault();
        if (mainRegion is not null)
        {
            return Math.Max(mainRegion.Bounds.Width, mainRegion.Bounds.Height) * 0.35;
        }

        var bounds = candidates
            .Select(candidate => candidate.Bounds)
            .Aggregate(PlanRect.Union);
        return Math.Max(bounds.Width, bounds.Height);
    }

    private static string PatternDescription(SurfacePatternCandidate pattern) =>
        pattern.Kind switch
        {
            SurfacePatternKind.DenseOrthogonalGrid =>
                $"dense orthogonal surface/detail grid {pattern.Id}",
            SurfacePatternKind.DenseParallelBand =>
                $"dense repeated {pattern.Orientation.ToString().ToLowerInvariant()} detail band {pattern.Id}",
            _ => $"surface/detail pattern {pattern.Id}"
        };

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (values[middle - 1] + values[middle]) / 2.0
            : values[middle];
    }

    private sealed class AxisBand(
        double axis,
        List<StructuralCandidateRegistry.CandidateDraft> candidates)
    {
        public double Axis { get; set; } = axis;

        public List<StructuralCandidateRegistry.CandidateDraft> Candidates { get; } =
            candidates;
    }

    private sealed record RepeatedFamily(
        IReadOnlyList<StructuralCandidateRegistry.CandidateDraft> Candidates,
        int AxisCount,
        double MedianSpacing,
        double SpacingRegularity,
        double AlignmentRatio,
        bool HighConfidence);
}
