namespace OpenPlanTrace;

internal sealed class WallStructuralEvidenceProducer : IStructuralEvidenceProducer
{
    public string Name => "preliminary-wall-evidence";

    public void Produce(StructuralEvidenceBuildContext context)
    {
        var acceptedById = context.Source.AcceptedWalls
            .GroupBy(wall => wall.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var assessmentsById = context.Source.WallEvidence.WallAssessments
            .Where(assessment => !string.IsNullOrWhiteSpace(assessment.WallId))
            .GroupBy(assessment => assessment.WallId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        var componentIdsByWallId = context.Source.WallGraph.Components
            .SelectMany(component => component.WallIds.Select(wallId => (
                WallId: wallId,
                ComponentId: component.Id)))
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.WallId)
                && !string.IsNullOrWhiteSpace(item.ComponentId))
            .GroupBy(item => item.WallId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.ComponentId)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        var allWalls = context.Source.WallCandidates
            .Concat(context.Source.AcceptedWalls)
            .GroupBy(wall => wall.Id, StringComparer.Ordinal)
            .Select(group => acceptedById.TryGetValue(group.Key, out var accepted)
                ? accepted
                : group.OrderByDescending(wall => wall.Confidence.Value).First())
            .OrderBy(wall => wall.PageNumber)
            .ThenBy(wall => wall.Id, StringComparer.Ordinal)
            .ToArray();

        foreach (var wall in allWalls)
        {
            assessmentsById.TryGetValue(wall.Id, out var assessment);
            var accepted = acceptedById.ContainsKey(wall.Id)
                && (assessment is null
                    || (assessment.Decision == WallEvidenceDecision.Accept
                        && assessment.PlacementReady
                        && !assessment.RequiresReview
                        && !assessment.RejectedAsNoise));
            var recovered = assessment?.Category == WallEvidenceCategory.RecoveredWallBody;
            var trustedExteriorShell = IsTrustedExteriorShell(wall, assessment);
            var trustedRoomConfirmedWall =
                accepted && IsTrustedRoomConfirmedWall(wall, assessment);
            var openingClearanceReviewGeometry =
                IsOpeningClearanceReviewGeometry(wall, assessment);
            var origins = StructuralCandidateOrigin.DetectedWall;
            if (accepted)
            {
                origins |= StructuralCandidateOrigin.AcceptedWall;
            }

            if (recovered)
            {
                origins |= StructuralCandidateOrigin.RecoveredWall;
            }

            if (trustedExteriorShell)
            {
                origins |= StructuralCandidateOrigin.ExteriorShell;
            }

            if (trustedRoomConfirmedWall)
            {
                origins |= StructuralCandidateOrigin.RoomBoundary;
            }

            var candidate = context.Candidates.Add(
                context.CandidateId(wall.Id),
                wall.PageNumber,
                wall.CenterLine,
                Math.Max(wall.Thickness, 0.5),
                wall.WallType,
                wall.Confidence,
                origins,
                wall.CenterLine.Length >= context.Options.MinimumCandidateLength,
                accepted,
                sourceWallIds: new[] { wall.Id },
                sourceWallComponentIds:
                    componentIdsByWallId.GetValueOrDefault(wall.Id)
                    ?? Array.Empty<string>(),
                sourcePrimitiveIds: wall.SourcePrimitiveIds,
                evidence: wall.Evidence);

            candidate.AddSignal(Signal(
                candidate,
                StructuralEvidenceSignalKind.SourceConfidence,
                (wall.Confidence.Value - 0.45) * 0.80,
                wall.Id,
                $"source confidence {wall.Confidence.Value:0.###}",
                wall.SourcePrimitiveIds));
            candidate.AddSignal(Signal(
                candidate,
                StructuralEvidenceSignalKind.LongRun,
                LengthWeight(wall.CenterLine.Length, context.Options.MinimumCandidateLength),
                wall.Id,
                $"wall candidate length {wall.CenterLine.Length:0.###}",
                wall.SourcePrimitiveIds));

            if (accepted)
            {
                candidate.AddSignal(Signal(
                    candidate,
                    StructuralEvidenceSignalKind.AcceptedWall,
                    0.30,
                    wall.Id,
                    "accepted by preliminary wall pipeline",
                    wall.SourcePrimitiveIds));
            }

            if (trustedExteriorShell)
            {
                candidate.AddSignal(Signal(
                    candidate,
                    StructuralEvidenceSignalKind.ExteriorShell,
                    0.16,
                    wall.Id,
                    IsSourceBackedExteriorShell(wall)
                        ? "source-backed exterior shell span anchored to detected structural endpoints"
                        : "trusted long exterior shell wall body accepted by preliminary shell-continuity review",
                    wall.SourcePrimitiveIds));
            }

            if (trustedRoomConfirmedWall)
            {
                candidate.AddSignal(Signal(
                    candidate,
                    StructuralEvidenceSignalKind.RoomBoundary,
                    0.18,
                    wall.Id,
                    "accepted room-confirmed wall body carries preliminary adjacency review into structural solving",
                    wall.SourcePrimitiveIds));
            }

            if (assessment is not null)
            {
                AddAssessmentSignals(candidate, assessment);
            }

            AddGeometrySignals(candidate, wall, assessment, context.Options);
            AddContextSignals(candidate, wall, assessment);

            if (openingClearanceReviewGeometry)
            {
                candidate.AddSignal(Signal(
                    candidate,
                    StructuralEvidenceSignalKind.DoorOrOpeningSymbol,
                    -1.10,
                    wall.Id,
                    "opening-clearance rectangle is review geometry, not a canonical wall",
                    wall.SourcePrimitiveIds));
            }
        }
    }

    private static bool IsOpeningClearanceReviewGeometry(
        WallSegment wall,
        WallEvidenceWallAssessment? assessment) =>
        wall.Evidence
            .Concat(assessment?.Evidence ?? Array.Empty<string>())
            .Any(item =>
                item.Contains(
                    "opening-clearance rectangle",
                    StringComparison.OrdinalIgnoreCase)
                && (item.Contains(
                        "retained as review geometry",
                        StringComparison.OrdinalIgnoreCase)
                    || item.Contains(
                        "demoted from placement-ready",
                        StringComparison.OrdinalIgnoreCase)));

    private static bool IsSourceBackedExteriorShell(WallSegment wall) =>
        wall.WallType == WallType.Exterior
        && (wall.Id.Contains(
                "wall-exterior-shell-source-backed:",
                StringComparison.Ordinal)
            || wall.Evidence.Any(item =>
                item.Contains(
                    "source-backed exterior shell closure",
                    StringComparison.OrdinalIgnoreCase)));

    private static bool IsTrustedExteriorShell(
        WallSegment wall,
        WallEvidenceWallAssessment? assessment) =>
        IsSourceBackedExteriorShell(wall)
        || (wall.WallType == WallType.Exterior
            && wall.Evidence
                .Concat(assessment?.Evidence ?? Array.Empty<string>())
                .Any(item =>
                    item.Contains(
                        "trusted long isolated exterior shell promoted",
                        StringComparison.OrdinalIgnoreCase)
                    || item.Contains(
                        "trusted long main exterior shell promoted",
                        StringComparison.OrdinalIgnoreCase)));

    private static bool IsTrustedRoomConfirmedWall(
        WallSegment wall,
        WallEvidenceWallAssessment? assessment) =>
        wall.Evidence
            .Concat(assessment?.Evidence ?? Array.Empty<string>())
            .Any(item =>
                item.Contains(
                    "room-confirmed wall body promoted to placement-ready",
                    StringComparison.OrdinalIgnoreCase)
                || item.Contains(
                    "room-confirmed isolated wall graph fragment kept placement-ready",
                    StringComparison.OrdinalIgnoreCase));

    private static void AddGeometrySignals(
        StructuralCandidateRegistry.CandidateDraft candidate,
        WallSegment wall,
        WallEvidenceWallAssessment? assessment,
        StructuralSolverOptions options)
    {
        AddFragmentAxisContinuitySignal(candidate, wall, assessment, options);

        var explicitFilledWallBody = wall.Evidence
            .Concat(assessment?.Evidence ?? Array.Empty<string>())
            .Any(item =>
                item.Contains("filled closed vector wall body", StringComparison.OrdinalIgnoreCase)
                || item.Contains("filled wall-solid primitive", StringComparison.OrdinalIgnoreCase));
        var reviewGatedVeryShortPair =
            IsReviewGatedVeryShortParallelPair(wall, assessment, options);
        var strongParallelWallBody = wall.PairEvidence is
        {
            Score: >= 0.60,
            OverlapRatio: >= 0.60
        }
            && !reviewGatedVeryShortPair;
        if ((explicitFilledWallBody || strongParallelWallBody)
            && !candidate.HasIndependentWallBodyEvidence)
        {
            candidate.AddSignal(Signal(
                candidate,
                StructuralEvidenceSignalKind.WallBody,
                0.36,
                $"{wall.Id}:independent-body",
                explicitFilledWallBody
                    ? "explicit filled wall-body geometry independently supports structural placement"
                    : "strong parallel-face geometry independently supports structural placement",
                wall.SourcePrimitiveIds));
        }
        else if (reviewGatedVeryShortPair)
        {
            candidate.AddSignal(Signal(
                candidate,
                StructuralEvidenceSignalKind.ReviewWall,
                -0.30,
                $"{wall.Id}:very-short-pair-review-gate",
                "very short low-score parallel-face pair remains review-only without independent room, opening, or filled wall-body evidence",
                wall.SourcePrimitiveIds));
        }

        var hasIndependentWallBody =
            assessment?.Category == WallEvidenceCategory.StrongWallBody
            || strongParallelWallBody
            || explicitFilledWallBody;
        if (hasIndependentWallBody || IsAxisAligned(wall.CenterLine, options.AngleToleranceDegrees))
        {
            return;
        }

        candidate.AddSignal(Signal(
            candidate,
            StructuralEvidenceSignalKind.UnsupportedOblique,
            -1.25,
            wall.Id,
            "unsupported oblique single-line geometry is retained for review, not structural wall placement",
            wall.SourcePrimitiveIds));
    }

    private static bool IsReviewGatedVeryShortParallelPair(
        WallSegment wall,
        WallEvidenceWallAssessment? assessment,
        StructuralSolverOptions options)
    {
        if (wall.DetectionKind != WallDetectionKind.ParallelLinePair
            || wall.PairEvidence is null
            || assessment is not
            {
                Decision: WallEvidenceDecision.Review,
                PlacementReady: false,
                RequiresReview: true,
                RejectedAsNoise: false
            }
            || wall.DrawingLength
                > Math.Max(48.0, options.MinimumCandidateLength * 2.0))
        {
            return false;
        }

        return wall.Evidence
            .Concat(assessment.Evidence)
            .Any(item =>
                item.Contains(
                    "very short unlayered parallel-face candidate has low pair score",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static void AddFragmentAxisContinuitySignal(
        StructuralCandidateRegistry.CandidateDraft candidate,
        WallSegment wall,
        WallEvidenceWallAssessment? assessment,
        StructuralSolverOptions options)
    {
        if (wall.DetectionKind != WallDetectionKind.FragmentMerged
            || wall.FragmentEvidence is not
            {
                RequiresGeometryReview: true,
                FragmentCount: >= 40
            } fragmentEvidence
            || assessment is not
            {
                Category: WallEvidenceCategory.MediumWallBody,
                Decision: WallEvidenceDecision.Review,
                RequiresReview: true,
                RejectedAsNoise: false
            }
            || !IsAxisAligned(wall.CenterLine, options.AngleToleranceDegrees)
            || wall.CenterLine.Length
                < Math.Max(60, options.MaximumContinuationGap * 5.0)
            || fragmentEvidence.GapRatio > 0.06
            || fragmentEvidence.TotalHealedGap
                > Math.Max(12, wall.CenterLine.Length * 0.06)
            || fragmentEvidence.MaxHealedGap
                > Math.Max(
                    options.AxisTolerance * 1.5,
                    Math.Max(wall.Thickness, 0.5) * 1.5)
            || fragmentEvidence.DuplicatePrimitiveCount
                > Math.Max(4, fragmentEvidence.FragmentCount * 0.04)
            || !wall.Evidence
                .Concat(assessment.Evidence)
                .Any(item =>
                    item.Contains(
                        "geometric room boundary support from reliable room-boundary alignment",
                        StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        candidate.AddSignal(Signal(
            candidate,
            StructuralEvidenceSignalKind.FragmentAxisContinuity,
            0.20,
            $"{wall.Id}:fragment-axis-continuity",
            $"continuous axis assembled from {fragmentEvidence.FragmentCount} fragments with {fragmentEvidence.GapRatio:P1} healed-gap ratio and reliable room-boundary alignment",
            wall.SourcePrimitiveIds));
    }

    private static void AddContextSignals(
        StructuralCandidateRegistry.CandidateDraft candidate,
        WallSegment wall,
        WallEvidenceWallAssessment? assessment)
    {
        if (candidate.HasIndependentWallBodyEvidence)
        {
            return;
        }

        var evidence = wall.Evidence
            .Concat(assessment?.Evidence ?? Array.Empty<string>())
            .ToArray();
        if (!evidence.Any(item =>
                item.Contains(
                    "detected room evidence on one side is outdoor/terrace space",
                    StringComparison.OrdinalIgnoreCase))
            || evidence.Any(item =>
                item.Contains("shared by room adjacency boundary", StringComparison.OrdinalIgnoreCase)
                || item.Contains("geometric room boundary support", StringComparison.OrdinalIgnoreCase)
                || item.Contains("opening-host evidence confirms", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        candidate.AddSignal(Signal(
            candidate,
            StructuralEvidenceSignalKind.ContextOnlyBoundary,
            -1.10,
            $"{wall.Id}:outdoor-context",
            "one-sided outdoor room context cannot promote a line into a structural wall without independent wall-body evidence",
            wall.SourcePrimitiveIds));
    }

    private static bool IsAxisAligned(
        PlanLineSegment line,
        double angleToleranceDegrees)
    {
        var angle = StructuralGeometry.NormalizeAngle(line.AngleRadians);
        var tolerance = Math.Max(0.25, angleToleranceDegrees) * Math.PI / 180.0;
        var horizontalDeviation = Math.Min(angle, Math.PI - angle);
        var verticalDeviation = Math.Abs(angle - (Math.PI / 2.0));
        return Math.Min(horizontalDeviation, verticalDeviation) <= tolerance;
    }

    private static void AddAssessmentSignals(
        StructuralCandidateRegistry.CandidateDraft candidate,
        WallEvidenceWallAssessment assessment)
    {
        var sourceIds = assessment.SourcePrimitiveIds;
        switch (assessment.Decision)
        {
            case WallEvidenceDecision.Accept:
                candidate.AddSignal(Signal(
                    candidate,
                    StructuralEvidenceSignalKind.AcceptedWall,
                    0.20,
                    assessment.WallId,
                    "wall evidence decision accepted",
                    sourceIds));
                break;
            case WallEvidenceDecision.Review:
                candidate.AddSignal(Signal(
                    candidate,
                    StructuralEvidenceSignalKind.ReviewWall,
                    -0.08,
                    assessment.WallId,
                    "wall evidence retained for review",
                    sourceIds));
                break;
            case WallEvidenceDecision.Reject:
                candidate.AddSignal(Signal(
                    candidate,
                    StructuralEvidenceSignalKind.RejectedWall,
                    -0.14,
                    assessment.WallId,
                    "preliminary wall evidence rejected candidate without deleting it",
                    sourceIds));
                break;
        }

        var (kind, baseWeight, description) = assessment.Category switch
        {
            WallEvidenceCategory.StrongWallBody =>
                (StructuralEvidenceSignalKind.WallBody, 0.34, "strong wall-body evidence"),
            WallEvidenceCategory.MediumWallBody =>
                (StructuralEvidenceSignalKind.WallBody, 0.20, "medium wall-body evidence"),
            WallEvidenceCategory.WeakSingleLine =>
                (StructuralEvidenceSignalKind.WallBody, -0.02, "weak single-line wall evidence"),
            WallEvidenceCategory.RecoveredWallBody =>
                (StructuralEvidenceSignalKind.RecoveredWallBody, 0.16, "recovered wall-body evidence"),
            WallEvidenceCategory.DoorOrOpeningSymbol =>
                (StructuralEvidenceSignalKind.DoorOrOpeningSymbol, -0.70, "door or opening symbol evidence"),
            WallEvidenceCategory.SurfacePatternDetail =>
                (StructuralEvidenceSignalKind.SurfacePattern, -0.70, "surface-pattern evidence"),
            WallEvidenceCategory.DimensionOrAnnotation =>
                (StructuralEvidenceSignalKind.DimensionOrAnnotation, -0.72, "dimension or annotation evidence"),
            WallEvidenceCategory.ObjectOrFixtureDetail =>
                (StructuralEvidenceSignalKind.ObjectOrFixture, -0.55, "object or fixture evidence"),
            _ =>
                (StructuralEvidenceSignalKind.Unknown, 0.0, "unclassified wall evidence")
        };
        var negativeScale = assessment.Decision == WallEvidenceDecision.Reject || assessment.RejectedAsNoise
            ? 1.0
            : assessment.Decision == WallEvidenceDecision.Review
                ? 0.35
                : 0.15;
        var positiveScale = assessment.Decision == WallEvidenceDecision.Accept
            && assessment.PlacementReady
            && !assessment.RequiresReview
            && !assessment.RejectedAsNoise
                ? 1.0
                : assessment.Decision == WallEvidenceDecision.Review
                    ? 0.35
                    : 0.10;
        var weight = baseWeight < 0
            ? baseWeight * negativeScale
            : baseWeight * positiveScale;
        candidate.AddSignal(Signal(
            candidate,
            kind,
            weight,
            assessment.WallId,
            description,
            sourceIds));
    }

    private static double LengthWeight(double length, double minimumLength)
    {
        var normalized = Math.Max(0, length) / Math.Max(1, minimumLength);
        return Math.Min(0.22, Math.Log2(1 + normalized) * 0.045);
    }

    private static StructuralEvidenceSignal Signal(
        StructuralCandidateRegistry.CandidateDraft candidate,
        StructuralEvidenceSignalKind kind,
        double weight,
        string sourceId,
        string description,
        IReadOnlyList<string> sourcePrimitiveIds) =>
        new(
            $"signal:{candidate.Id}:{kind}:{sourceId}",
            kind,
            weight,
            sourceId,
            description,
            sourcePrimitiveIds);
}
