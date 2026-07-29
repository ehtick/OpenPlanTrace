namespace OpenPlanTrace;

internal static class StructuralWallRunReadinessEvaluator
{
    public static StructuralWallRunReliability Evaluate(
        IReadOnlyList<StructuralWallCandidate> candidates,
        StructuralSolverOptions options)
    {
        if (candidates.Count == 0)
        {
            return StructuralWallRunReliability.Unassessed;
        }

        var hasStrongWallBody = candidates.Any(candidate => candidate.HasIndependentWallBodyEvidence);
        var hasCrossDomainWallBody = candidates.Any(candidate => candidate.HasCrossDomainWallBodyEvidence);
        var hasContextOnlyNegative = candidates.Any(candidate =>
            HasStrongSignal(candidate, StructuralEvidenceSignalKind.ContextOnlyBoundary));
        var hasUnsupportedGeometryNegative = candidates.Any(candidate =>
            HasStrongSignal(candidate, StructuralEvidenceSignalKind.UnsupportedOblique));
        var hasIntrinsicNonWallNegative = candidates.Any(candidate =>
            HasStrongSignal(
                candidate,
                StructuralEvidenceSignalKind.DoorOrOpeningSymbol,
                StructuralEvidenceSignalKind.SurfacePattern,
                StructuralEvidenceSignalKind.RepeatedDetailPattern,
                StructuralEvidenceSignalKind.DimensionOrAnnotation,
                StructuralEvidenceSignalKind.ObjectOrFixture));
        var hasIsolatedTerritoryNegative = candidates.Any(candidate =>
            HasStrongSignal(candidate, StructuralEvidenceSignalKind.IsolatedStructuralIsland));
        var hasUnoccupiedShellExtension = candidates.Any(candidate =>
            HasStrongSignal(candidate, StructuralEvidenceSignalKind.UnoccupiedShellExtension));
        var hasWallBodyThicknessOutlier = candidates.Any(candidate =>
            HasStrongSignal(candidate, StructuralEvidenceSignalKind.WallBodyThicknessOutlier));
        var hasRepeatedDetailNegative = candidates.Any(candidate =>
            HasStrongSignal(candidate, StructuralEvidenceSignalKind.RepeatedDetailPattern));
        var hasStrongNegative =
            hasContextOnlyNegative
            || hasUnsupportedGeometryNegative
            || hasIntrinsicNonWallNegative
            || hasIsolatedTerritoryNegative
            || hasUnoccupiedShellExtension
            || hasWallBodyThicknessOutlier
            || hasRepeatedDetailNegative;
        var strongWallBodyCoverage = hasStrongWallBody
            ? CoverageRatio(candidates, candidate => candidate.HasIndependentWallBodyEvidence)
            : 0;
        var hasOpeningHostSupport = candidates.Any(candidate =>
            candidate.Origins.HasFlag(StructuralCandidateOrigin.OpeningHost)
            && candidate.Signals.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.OpeningHost
                && signal.Weight > 0));
        var hasAcceptedAxisAlignedMediumBody = candidates.Any(candidate =>
            candidate.WasAcceptedByPreliminaryPipeline
            && !HasStrongSignal(
                candidate,
                StructuralEvidenceSignalKind.DoorOrOpeningSymbol,
                StructuralEvidenceSignalKind.SurfacePattern,
                StructuralEvidenceSignalKind.RepeatedDetailPattern,
                StructuralEvidenceSignalKind.DimensionOrAnnotation,
                StructuralEvidenceSignalKind.ObjectOrFixture,
                StructuralEvidenceSignalKind.UnsupportedOblique)
            && IsAxisAligned(candidate.CenterLine, options.AngleToleranceDegrees)
            && candidate.Signals.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.WallBody
                && signal.Weight >= 0.16));
        var contextBoundaryCorroboratedByOpening =
            hasContextOnlyNegative
            && hasAcceptedAxisAlignedMediumBody
            && hasOpeningHostSupport;
        var blockingIntrinsicNegative =
            hasRepeatedDetailNegative
            || ((hasIntrinsicNonWallNegative || hasUnsupportedGeometryNegative)
                && (!hasStrongWallBody || strongWallBodyCoverage < 0.55));
        var blockingContextOnlyNegative =
            hasContextOnlyNegative
            && !hasStrongWallBody
            && !hasCrossDomainWallBody
            && !contextBoundaryCorroboratedByOpening;
        var blockingStrongNegative =
            blockingIntrinsicNegative
            || blockingContextOnlyNegative
            || hasIsolatedTerritoryNegative
            || hasUnoccupiedShellExtension
            || hasWallBodyThicknessOutlier;
        var hasSupportedRecovery = candidates.Any(candidate =>
            !candidate.HasStrongNegativeEvidence
            && candidate.Signals.Any(signal =>
                signal.Kind == StructuralEvidenceSignalKind.RecoveredWallBody
                && signal.Weight > 0)
            && (candidate.WasAcceptedByPreliminaryPipeline
                || candidate.Origins.HasFlag(StructuralCandidateOrigin.WallGraph)
                || candidate.Origins.HasFlag(StructuralCandidateOrigin.RoomBoundary)
                || candidate.Origins.HasFlag(StructuralCandidateOrigin.ExteriorShell)));
        var ready = !blockingStrongNegative
            && (hasStrongWallBody
                || hasCrossDomainWallBody
                || hasAcceptedAxisAlignedMediumBody
                || hasSupportedRecovery);
        var reasons = new List<string>();

        if (blockingStrongNegative)
        {
            reasons.Add(
                hasIsolatedTerritoryNegative
                    ? "structural run belongs to an excluded, unanchored wall-graph island"
                    : hasUnoccupiedShellExtension
                        ? "structural run leaves trusted occupied territory as a one-ended shell extension"
                    : hasWallBodyThicknessOutlier
                        ? "structural run is an unfilled parallel-face thickness outlier against the page wall-body profile"
                    : "structural run contains unsupported geometry or context-only negative evidence");
        }
        else if (contextBoundaryCorroboratedByOpening)
        {
            reasons.Add("opening-host evidence confirms an indoor-outdoor wall boundary");
        }
        else if (hasContextOnlyNegative && hasStrongWallBody)
        {
            reasons.Add("independent strong wall-body evidence outweighs outdoor or conflicted room context");
        }
        else if (hasContextOnlyNegative && hasCrossDomainWallBody)
        {
            reasons.Add("wall-body, coherent topology, and opposite-room evidence outweigh provisional room context");
        }
        else if (hasStrongNegative)
        {
            reasons.Add(
                $"independent wall-body evidence covers {strongWallBodyCoverage:P0} of the run and outweighs local context-only evidence");
        }

        if (hasStrongWallBody)
        {
            reasons.Add("independent strong wall-body evidence supports coordinate placement");
        }
        else if (hasCrossDomainWallBody)
        {
            reasons.Add("cross-domain wall-body evidence supports coordinate placement");
        }
        else if (hasAcceptedAxisAlignedMediumBody)
        {
            reasons.Add("accepted axis-aligned medium wall-body evidence supports coordinate placement");
        }
        else if (hasSupportedRecovery)
        {
            reasons.Add("recovered wall body is corroborated by structural topology");
        }
        else
        {
            reasons.Add("room or envelope context alone is insufficient for coordinate placement");
        }

        var confidence = candidates.Average(candidate => candidate.Confidence.Value);
        if (blockingStrongNegative)
        {
            confidence = Math.Min(confidence, 0.49);
        }
        else if (!ready)
        {
            confidence = Math.Min(confidence, 0.69);
        }

        return new StructuralWallRunReliability(
            ReadyForCoordinatePlacement: ready,
            RequiresReview: !ready,
            Confidence: Math.Round(
                Math.Clamp(confidence, 0, 1),
                6,
                MidpointRounding.AwayFromZero),
            Reasons: reasons);
    }

    private static bool HasStrongSignal(
        StructuralWallCandidate candidate,
        params StructuralEvidenceSignalKind[] kinds)
    {
        var acceptedKinds = kinds.ToHashSet();
        return candidate.Signals.Any(signal =>
            signal.Weight <= -0.45
            && acceptedKinds.Contains(signal.Kind));
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

    private static double CoverageRatio(
        IReadOnlyList<StructuralWallCandidate> candidates,
        Func<StructuralWallCandidate, bool> predicate)
    {
        var reference = candidates
            .OrderByDescending(candidate => candidate.DrawingLength)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .First();
        var direction = StructuralGeometry.UnitDirection(reference.CenterLine);
        var allRange = candidates
            .Select(candidate => StructuralGeometry.ProjectionRange(candidate.CenterLine, direction))
            .Aggregate(
                (Start: double.PositiveInfinity, End: double.NegativeInfinity),
                (current, range) => (
                    Math.Min(current.Start, range.Start),
                    Math.Max(current.End, range.End)));
        var totalLength = allRange.End - allRange.Start;
        if (totalLength <= 1e-9)
        {
            return 0;
        }

        var intervals = candidates
            .Where(predicate)
            .Select(candidate => StructuralGeometry.ProjectionRange(candidate.CenterLine, direction))
            .OrderBy(range => range.Start)
            .ThenBy(range => range.End)
            .ToArray();
        if (intervals.Length == 0)
        {
            return 0;
        }

        var covered = 0.0;
        var currentStart = intervals[0].Start;
        var currentEnd = intervals[0].End;
        foreach (var interval in intervals.Skip(1))
        {
            if (interval.Start <= currentEnd)
            {
                currentEnd = Math.Max(currentEnd, interval.End);
                continue;
            }

            covered += currentEnd - currentStart;
            currentStart = interval.Start;
            currentEnd = interval.End;
        }

        covered += currentEnd - currentStart;
        return Math.Clamp(covered / totalLength, 0, 1);
    }
}
