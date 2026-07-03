namespace OpenPlanTrace;

public static class WallPlacementContextGuards
{
    private const double MinTrustedSecondaryExteriorShellLengthDrawingUnits = 72.0;
    private const double MinTrustedSecondaryExteriorShellPairScore = 0.78;
    private const double MinTrustedSecondaryExteriorShellOverlapRatio = 0.90;
    private const double MinTrustedSecondaryExteriorShellFaceSeparationDrawingUnits = 2.0;
    private const double MaxTrustedSecondaryExteriorShellFaceSeparationDrawingUnits = 18.0;
    private const int MaxTrustedSecondaryExteriorShellFaceFragments = 220;
    private const double MinTrustedSecondaryInteriorWallLengthDrawingUnits = 120.0;
    private const double MinTrustedSecondaryInteriorWallPairScore = 0.88;
    private const double MinTrustedSecondaryInteriorWallOverlapRatio = 0.95;
    private const double MinTrustedSecondaryInteriorWallFaceSeparationDrawingUnits = 2.0;
    private const double MaxTrustedSecondaryInteriorWallFaceSeparationDrawingUnits = 18.0;
    private const int MaxTrustedSecondaryInteriorWallFaceFragments = 80;
    private const double MinTrustedAnchoredSingleSecondaryWallPairScore = 0.86;
    private const double MinTrustedAnchoredSingleSecondaryWallOverlapRatio = 0.90;
    private const double MinTrustedAnchoredSingleSecondaryWallFaceSeparationDrawingUnits = 2.0;
    private const double MaxTrustedAnchoredSingleSecondaryWallFaceSeparationDrawingUnits = 18.0;
    private const int MaxTrustedAnchoredSingleSecondaryWallFaceFragments = 80;
    private const double MinTrustedMainStructuralInteriorLengthDrawingUnits = 80.0;
    private const double MinTrustedMainStructuralInteriorPairScore = 0.82;
    private const double MinTrustedMainStructuralInteriorOverlapRatio = 0.95;
    private const double MinTrustedMainStructuralInteriorFaceSeparationDrawingUnits = 2.0;
    private const double MaxTrustedMainStructuralInteriorFaceSeparationDrawingUnits = 18.0;
    private const int MaxTrustedMainStructuralInteriorFaceFragments = 64;
    private const double MinTrustedNoisyMainStructuralInteriorPairScore = 0.76;
    private const double MinTrustedNoisyMainStructuralInteriorOverlapRatio = 0.85;
    private const int MaxTrustedNoisyMainStructuralInteriorFaceFragments = 360;
    private const double MinTrustedOneEndpointNoisyMainStructuralInteriorLengthDrawingUnits = 108.0;
    private const double MinTrustedOneEndpointNoisyMainStructuralInteriorPairScore = 0.78;
    private const double MinTrustedOneEndpointNoisyMainStructuralInteriorOverlapRatio = 0.97;
    private const double MinTrustedOneEndpointNoisyMainStructuralInteriorFaceSeparationDrawingUnits = 2.0;
    private const double MaxTrustedOneEndpointNoisyMainStructuralInteriorFaceSeparationDrawingUnits = 18.0;
    private const int MaxTrustedOneEndpointNoisyMainStructuralInteriorFaceFragments = 360;
    private const double MinTrustedLongOneEndpointFragmentMergedInteriorLengthDrawingUnits = 90.0;
    private const double MinTrustedLongOneEndpointFragmentMergedInteriorConfidence = 0.84;
    private const double MinTrustedLongOneEndpointFragmentMergedInteriorAssessmentConfidence = 0.82;
    private const int MaxTrustedLongOneEndpointFragmentMergedInteriorFragments = 12;
    private const int MaxTrustedLongOneEndpointFragmentMergedInteriorDuplicatePrimitives = 8;
    private const double MinTrustedOpeningLinkedFragmentMergedInteriorLengthDrawingUnits = 48.0;
    private const double MinTrustedOpeningLinkedFragmentMergedInteriorConfidence = 0.76;
    private const double MinTrustedOpeningLinkedFragmentMergedInteriorAssessmentConfidence = 0.76;
    private const int MaxTrustedOpeningLinkedCompactFragmentMergedInteriorFragments = 16;
    private const int MaxTrustedOpeningLinkedDenseFragmentMergedInteriorFragments = 120;
    private const int MaxTrustedOpeningLinkedFragmentMergedInteriorDuplicatePrimitives = 16;
    private const double MaxTrustedOpeningLinkedFragmentMergedInteriorGapRatio = 0.02;
    private const double MaxTrustedOpeningLinkedFragmentMergedInteriorTotalHealedGapRatio = 0.04;
    private const int MinTrustedDenseLongOneEndpointFragmentMergedInteriorFragments = 24;
    private const int MaxTrustedDenseLongOneEndpointFragmentMergedInteriorFragments = 180;
    private const int MaxTrustedDenseLongOneEndpointFragmentMergedInteriorDuplicatePrimitives = 8;
    private const double MinTrustedDenseTwoSidedRoomFragmentMergedInteriorLengthDrawingUnits = 96.0;
    private const double MinTrustedDenseTwoSidedRoomFragmentMergedInteriorConfidence = 0.82;
    private const int MaxTrustedDenseTwoSidedRoomFragmentMergedInteriorFragments = 80;
    private const int MaxTrustedDenseTwoSidedRoomFragmentMergedInteriorDuplicatePrimitives = 16;
    private const double MaxTrustedDenseTwoSidedRoomFragmentMergedInteriorGapRatio = 0.02;
    private const double MaxTrustedDenseTwoSidedRoomFragmentMergedInteriorTotalHealedGapRatio = 0.025;
    private const double MinTrustedRecoveredMainStructuralInteriorLengthDrawingUnits = 100.0;
    private const double MinTrustedRecoveredMainStructuralInteriorPairScore = 0.80;
    private const double MinTrustedRecoveredMainStructuralInteriorOverlapRatio = 0.95;
    private const double MinTrustedRecoveredMainStructuralInteriorFaceSeparationDrawingUnits = 2.0;
    private const double MaxTrustedRecoveredMainStructuralInteriorFaceSeparationDrawingUnits = 18.0;
    private const int MaxTrustedRecoveredMainStructuralInteriorFaceFragments = 72;
    private const double MinTrustedShortRecoveredRoomBoundaryLengthDrawingUnits = 12.0;
    private const double MaxTrustedShortRecoveredRoomBoundaryLengthDrawingUnits = 36.0;
    private const double MinTrustedShortRecoveredRoomBoundaryConfidence = 0.76;
    private const double MinTrustedCleanIsolatedRoomBoundaryFragmentLengthDrawingUnits = 60.0;
    private const double MinTrustedCleanIsolatedRoomBoundaryFragmentConfidence = 0.80;
    private const int MaxTrustedCleanIsolatedRoomBoundaryFragmentCount = 8;
    private const int MaxTrustedCleanIsolatedRoomBoundaryDuplicatePrimitives = 3;
    private const double MinTrustedIsolatedTwoSidedInteriorPairLengthDrawingUnits = 36.0;
    private const double MaxTrustedIsolatedTwoSidedInteriorPairLengthDrawingUnits = 80.0;
    private const double MinTrustedIsolatedTwoSidedInteriorPairConfidence = 0.72;
    private const double MinTrustedIsolatedTwoSidedInteriorPairScore = 0.78;
    private const double MinTrustedIsolatedTwoSidedInteriorPairOverlapRatio = 0.95;
    private const double MinTrustedIsolatedTwoSidedInteriorPairFaceSeparationDrawingUnits = 2.0;
    private const double MaxTrustedIsolatedTwoSidedInteriorPairFaceSeparationDrawingUnits = 18.0;
    private const int MaxTrustedIsolatedTwoSidedInteriorPairFaceFragments = 16;
    private const double MinTrustedRejectedObjectLikeBoundaryRecallLengthDrawingUnits = 48.0;
    private const double MinTrustedRejectedObjectLikeBoundaryRecallConfidence = 0.72;
    private const double MinTrustedRejectedObjectLikeBoundaryRecallPairScore = 0.74;
    private const double MinTrustedRejectedObjectLikeBoundaryRecallOverlapRatio = 0.90;
    private const double MinTrustedRejectedObjectLikeBoundaryRecallFaceSeparationDrawingUnits = 1.0;
    private const double MaxTrustedRejectedObjectLikeBoundaryRecallFaceSeparationDrawingUnits = 24.0;
    private const int MaxTrustedRejectedObjectLikeBoundaryRecallFaceFragments = 180;
    private const int MaxTrustedRejectedObjectLikeBoundaryRecallFragmentCount = 48;
    private const int MaxTrustedRejectedObjectLikeBoundaryRecallDuplicatePrimitives = 48;
    private const double MaxTrustedRejectedObjectLikeBoundaryRecallGapRatio = 0.025;

    public const string SecondaryStructuralWithoutRoomBoundarySupportReason =
        "secondary structural wall component lacks room-boundary support";

    public const string SecondaryStructuralObjectLineworkWithoutRoomBoundarySupportReason =
        "secondary structural wall overlaps detected stair/object linework without room-boundary support";

    public const string SecondaryStructuralOverSourcedDetailLineworkReason =
        "secondary structural wall has excessive detail/source linework contamination despite room-boundary support";

    public const string FragmentMergedInteriorWithoutRoomBoundarySupportReason =
        "fragment-merged interior wall has suspicious linework and lacks room-boundary support";

    public const string MainStructuralInteriorWithoutSemanticSupportReason =
        "main structural interior wall has risky linework and lacks semantic room-boundary support";

    public const string TrustedRecoveredMainStructuralInteriorEvidence =
        "trusted recovered main structural interior wall body";

    public const string TrustedOneEndpointNoisyMainStructuralInteriorEvidence =
        "trusted one-end noisy main structural interior wall body";

    public const string TrustedLongOneEndpointFragmentMergedInteriorEvidence =
        "trusted long one-end fragment-merged interior wall body";

    public const string TrustedObjectLikeLongCleanFragmentInteriorEvidence =
        "protected long clean object-like fragment wall kept as structural interior candidate";

    public const string TrustedObjectLikeExteriorShellPairEvidence =
        "strong exterior paired wall body is supported by shell evidence";

    public const string TrustedDenseTwoSidedRoomFragmentMergedInteriorEvidence =
        "trusted dense two-sided room fragment-merged interior wall body";

    public const string TrustedRejectedStrongBoundaryWallBodyEvidence =
        "rejected object-like wall body restored because strong wall-body and room/exterior boundary evidence agree";

    public const string TrustedRejectedMediumBoundaryFragmentWallBodyEvidence =
        "rejected object-like fragment wall restored because clean fragment geometry and two-end boundary evidence agree";

    public const string TrustedRejectedObjectLikeBoundaryRecallEvidence =
        "rejected object-like wall restored because wall-type and placement-ready evidence outweigh compact component classification";

    public const string TrustedShortRecoveredRoomBoundaryEvidence =
        "short recovered wall has two-ended structural support and room evidence on both sides";

    public const string TrustedCleanIsolatedRoomBoundaryFragmentEvidence =
        "clean isolated fragment wall has structural endpoint and interior boundary support";

    public const string TrustedIsolatedTwoSidedInteriorPairEvidence =
        "trusted isolated two-sided interior paired wall body";

    public static bool IsTrustedRecoveredMainStructuralInteriorWallBody(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string>? extraEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (component?.Kind != WallGraphComponentKind.MainStructural
            || component.ExcludedFromStructuralTopology
            || (wall.WallType != WallType.Unknown && wall.WallType != WallType.Interior)
            || wall.DetectionKind != WallDetectionKind.ParallelLinePair
            || wall.DrawingLength < MinTrustedRecoveredMainStructuralInteriorLengthDrawingUnits
            || wall.Confidence.Value < 0.78
            || assessment is null
            || assessment.Confidence.Value < 0.78
            || !assessment.PlacementReady
            || assessment.RequiresReview
            || assessment.RejectedAsNoise
            || assessment.Decision == WallEvidenceDecision.Reject
            || assessment.Category != WallEvidenceCategory.RecoveredWallBody
            || wall.PairEvidence is not { } pair)
        {
            return false;
        }

        if (pair.Score < MinTrustedRecoveredMainStructuralInteriorPairScore
            || pair.OverlapRatio < MinTrustedRecoveredMainStructuralInteriorOverlapRatio
            || pair.FaceSeparation < MinTrustedRecoveredMainStructuralInteriorFaceSeparationDrawingUnits
            || pair.FaceSeparation > MaxTrustedRecoveredMainStructuralInteriorFaceSeparationDrawingUnits
            || MaxFaceFragmentCount(wall, assessment) > MaxTrustedRecoveredMainStructuralInteriorFaceFragments)
        {
            return false;
        }

        var baseEvidence = WallEvidenceFor(wall, assessment)
            .Concat(component?.Evidence ?? Array.Empty<string>())
            .ToArray();
        var evidence = extraEvidence is null
            ? baseEvidence
            : baseEvidence.Concat(extraEvidence).ToArray();
        if (!EvidenceContainsAny(
                evidence,
                "recovered by wall evidence map",
                "wall evidence: recovered wall body")
            || !EvidenceContainsAny(
                evidence,
                "unclaimed parallel-face",
                "parallel wall-face pair",
                "strong double-edge wall body"))
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "layer (unlayered) classified Dimension",
            "layer evidence: contains dimension-like text",
            "dimension-like weak layer",
            "classified Dimension",
            "dimension-like text",
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "detail linework",
            "surface pattern",
            "object/fixture",
            "fixture detail",
            "stair",
            "door swing",
            "door leaf",
            "door arc",
            "tiny door-adjacent placement topology piece suppressed",
            "not trusted",
            "without shell support",
            "alone is not trusted");
    }

    public static bool IsTrustedShortRecoveredRoomBoundaryWallBody(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string>? extraEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (assessment is null
            || wall.WallType != WallType.Interior
            || wall.DetectionKind != WallDetectionKind.SingleLine
            || wall.DrawingLength < MinTrustedShortRecoveredRoomBoundaryLengthDrawingUnits
            || wall.DrawingLength > MaxTrustedShortRecoveredRoomBoundaryLengthDrawingUnits
            || wall.Confidence.Value < MinTrustedShortRecoveredRoomBoundaryConfidence
            || assessment.Confidence.Value < MinTrustedShortRecoveredRoomBoundaryConfidence
            || assessment.Category != WallEvidenceCategory.RecoveredWallBody
            || assessment.RejectedAsNoise
            || assessment.Decision == WallEvidenceDecision.Reject
            || component?.ExcludedFromStructuralTopology == true
            || component?.Kind == WallGraphComponentKind.ObjectLikeIsland
            || (!wall.CenterLine.IsHorizontal() && !wall.CenterLine.IsVertical()))
        {
            return false;
        }

        var evidence = extraEvidence is null
            ? WallEvidenceFor(wall, assessment)
            : WallEvidenceFor(wall, assessment).Concat(extraEvidence).ToArray();
        var hasTwoEndedRecovery =
            EvidenceContains(evidence, "recovered by wall evidence map as short supported wall segment")
            && EvidenceContains(evidence, "short recovery used two-ended structural support")
            && EvidenceContains(evidence, "structural endpoint support count 2");
        var hasRoomBoundarySupport =
            EvidenceContains(evidence, "room-confirmed wall body promoted to placement-ready")
            || EvidenceContains(evidence, "detected room evidence on both sides")
            || EvidenceContains(evidence, "two-sided room evidence True")
            || EvidenceContains(evidence, "geometric room boundary support")
            || EvidenceContains(evidence, "shared by room adjacency boundary");
        if (!hasTwoEndedRecovery || !hasRoomBoundarySupport)
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "layer (unlayered) classified Dimension",
            "layer evidence: contains dimension-like text",
            "dimension-like",
            "classified Dimension",
            "dimension annotation",
            "outdoor",
            "terrace",
            "covered-area",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "detail linework",
            "surface pattern",
            "object/fixture",
            "fixture detail",
            "repeated short detail",
            "door/opening",
            "door swing",
            "door leaf",
            "door arc",
            "opening detail",
            "stair",
            "not trusted",
            "without shell support",
            "alone is not trusted");
    }

    public static bool IsTrustedCleanIsolatedRoomBoundaryFragmentWallBody(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string>? extraEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (component is null
            || component.Kind != WallGraphComponentKind.IsolatedFragment
            || component.ExcludedFromStructuralTopology
            || wall.WallType != WallType.Interior
            || wall.DetectionKind != WallDetectionKind.FragmentMerged
            || wall.PairEvidence is not null
            || wall.DrawingLength < MinTrustedCleanIsolatedRoomBoundaryFragmentLengthDrawingUnits
            || wall.Confidence.Value < MinTrustedCleanIsolatedRoomBoundaryFragmentConfidence
            || wall.FragmentEvidence is not { RequiresGeometryReview: false } fragmentEvidence
            || assessment is null
            || assessment.Confidence.Value < MinTrustedCleanIsolatedRoomBoundaryFragmentConfidence
            || assessment.RejectedAsNoise
            || assessment.Decision == WallEvidenceDecision.Reject
            || assessment.Category != WallEvidenceCategory.MediumWallBody
            || (!wall.CenterLine.IsHorizontal() && !wall.CenterLine.IsVertical()))
        {
            return false;
        }

        var uniqueSourcePrimitiveCount = Math.Max(0, wall.SourcePrimitiveIds.Count - fragmentEvidence.DuplicatePrimitiveCount);
        var fragmentCount = Math.Max(fragmentEvidence.FragmentCount, uniqueSourcePrimitiveCount);
        if (fragmentCount > MaxTrustedCleanIsolatedRoomBoundaryFragmentCount
            || fragmentEvidence.DuplicatePrimitiveCount > MaxTrustedCleanIsolatedRoomBoundaryDuplicatePrimitives
            || fragmentEvidence.GapRatio > 0.001
            || fragmentEvidence.TotalHealedGap > 0.001
            || fragmentEvidence.MaxHealedGap > 0.001)
        {
            return false;
        }

        var baseEvidence = WallEvidenceFor(wall, assessment)
            .Concat(component.Evidence)
            .Concat(fragmentEvidence.Evidence)
            .ToArray();
        var evidence = extraEvidence is null
            ? baseEvidence
            : baseEvidence.Concat(extraEvidence).ToArray();
        var hasCleanFragmentEvidence =
            EvidenceContains(evidence, "merged collinear wall fragments")
            && EvidenceContains(evidence, "fragment geometry healed gap ratio 0")
            && EvidenceContains(evidence, "supported wall evidence inside exterior envelope");
        var hasBoundaryOrEndpointSupport =
            EvidenceContains(evidence, "only one trusted structural endpoint")
            || EvidenceContains(evidence, "one endpoint supported by structural context")
            || EvidenceContains(evidence, "both endpoints supported by structural context")
            || EvidenceContains(evidence, "detected room evidence on both sides")
            || EvidenceContains(evidence, "geometric room boundary support")
            || EvidenceContains(evidence, "shared by room adjacency boundary");
        if (!hasCleanFragmentEvidence || !hasBoundaryOrEndpointSupport)
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "outdoor",
            "terrace",
            "covered-area",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "detail linework",
            "surface pattern",
            "object/fixture",
            "fixture detail",
            "repeated short detail",
            "door/opening",
            "door swing",
            "door leaf",
            "door arc",
            "opening detail",
            "stair",
            "dimension annotation",
            "not trusted",
            "without shell support",
            "alone is not trusted");
    }

    public static bool IsTrustedIsolatedTwoSidedInteriorPairWallBody(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string>? extraEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (component is null
            || component.Kind != WallGraphComponentKind.IsolatedFragment
            || component.ExcludedFromStructuralTopology
            || wall.WallType != WallType.Interior
            || wall.DetectionKind != WallDetectionKind.ParallelLinePair
            || wall.DrawingLength < MinTrustedIsolatedTwoSidedInteriorPairLengthDrawingUnits
            || wall.DrawingLength > MaxTrustedIsolatedTwoSidedInteriorPairLengthDrawingUnits
            || wall.Confidence.Value < MinTrustedIsolatedTwoSidedInteriorPairConfidence
            || assessment is null
            || assessment.Confidence.Value < MinTrustedIsolatedTwoSidedInteriorPairConfidence
            || assessment.RejectedAsNoise
            || assessment.Decision == WallEvidenceDecision.Reject
            || assessment.Category != WallEvidenceCategory.MediumWallBody
            || wall.PairEvidence is not { } pair
            || (!wall.CenterLine.IsHorizontal() && !wall.CenterLine.IsVertical()))
        {
            return false;
        }

        if (pair.Score < MinTrustedIsolatedTwoSidedInteriorPairScore
            || pair.OverlapRatio < MinTrustedIsolatedTwoSidedInteriorPairOverlapRatio
            || pair.FaceSeparation < MinTrustedIsolatedTwoSidedInteriorPairFaceSeparationDrawingUnits
            || pair.FaceSeparation > MaxTrustedIsolatedTwoSidedInteriorPairFaceSeparationDrawingUnits
            || MaxFaceFragmentCount(wall, assessment) > MaxTrustedIsolatedTwoSidedInteriorPairFaceFragments)
        {
            return false;
        }

        var baseEvidence = WallEvidenceFor(wall, assessment)
            .Concat(component.Evidence)
            .ToArray();
        var evidence = extraEvidence is null
            ? baseEvidence
            : baseEvidence.Concat(extraEvidence).ToArray();
        var hasPairedWallBodyEvidence =
            EvidenceContains(evidence, "parallel wall-face pair")
            && EvidenceContains(evidence, "supported wall evidence inside exterior envelope");
        var hasTwoSidedRoomEvidence =
            EvidenceContains(evidence, "detected room evidence on both sides")
            || EvidenceContains(evidence, "two-sided room evidence True")
            || EvidenceContains(evidence, "shared by room adjacency boundary")
            || EvidenceContains(evidence, "explicit room boundary support");
        if (!hasPairedWallBodyEvidence || !hasTwoSidedRoomEvidence)
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "duplicate wall-face",
            "already represented by stronger paired wall body",
            "already represented by clean topology span",
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "detail linework",
            "surface pattern",
            "surface/detail pattern",
            "repeated short detail",
            "door/opening",
            "door swing",
            "door leaf",
            "door arc",
            "opening detail",
            "opening-linked wall fragment",
            "tiny door-adjacent",
            "opening cutouts fully consume",
            "stair",
            "not trusted",
            "without shell support",
            "alone is not trusted",
            "demoted from placement-ready");
    }

    public static bool IsTrustedOneEndpointNoisyMainStructuralInteriorWallBody(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string>? extraEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (component?.Kind != WallGraphComponentKind.MainStructural
            || component.ExcludedFromStructuralTopology
            || wall.WallType != WallType.Interior
            || wall.DetectionKind != WallDetectionKind.ParallelLinePair
            || wall.DrawingLength < MinTrustedOneEndpointNoisyMainStructuralInteriorLengthDrawingUnits
            || wall.Confidence.Value < 0.86
            || assessment is null
            || assessment.Confidence.Value < 0.86
            || !assessment.PlacementReady
            || assessment.RequiresReview
            || assessment.RejectedAsNoise
            || assessment.Decision == WallEvidenceDecision.Reject
            || assessment.Category != WallEvidenceCategory.StrongWallBody
            || wall.PairEvidence is not { } pair
            || !HasStrongPairedWallBodyEvidence(wall, assessment))
        {
            return false;
        }

        if (pair.Score < MinTrustedOneEndpointNoisyMainStructuralInteriorPairScore
            || pair.OverlapRatio < MinTrustedOneEndpointNoisyMainStructuralInteriorOverlapRatio
            || pair.FaceSeparation < MinTrustedOneEndpointNoisyMainStructuralInteriorFaceSeparationDrawingUnits
            || pair.FaceSeparation > MaxTrustedOneEndpointNoisyMainStructuralInteriorFaceSeparationDrawingUnits
            || Math.Max(pair.FirstFaceFragmentCount, pair.SecondFaceFragmentCount)
                > MaxTrustedOneEndpointNoisyMainStructuralInteriorFaceFragments)
        {
            return false;
        }

        var evidence = extraEvidence is null
            ? WallEvidenceFor(wall, assessment)
            : WallEvidenceFor(wall, assessment).Concat(extraEvidence).ToArray();
        if (!EvidenceContains(evidence, "supported wall evidence inside exterior envelope")
            || !EvidenceContains(evidence, "one endpoint supported by structural context"))
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "detail linework",
            "surface pattern",
            "object/fixture",
            "fixture detail",
            "stair",
            "door swing",
            "door leaf",
            "door arc",
            "not trusted",
            "without shell support",
            "alone is not trusted");
    }

    public static bool IsTrustedLongOneEndpointFragmentMergedInteriorWallBody(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string>? extraEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (component is null
            || component.ExcludedFromStructuralTopology
            || component.Kind is WallGraphComponentKind.ObjectLikeIsland or WallGraphComponentKind.IsolatedFragment
            || wall.WallType != WallType.Interior
            || wall.DetectionKind != WallDetectionKind.FragmentMerged
            || wall.PairEvidence is not null
            || wall.DrawingLength < MinTrustedLongOneEndpointFragmentMergedInteriorLengthDrawingUnits
            || wall.Confidence.Value < MinTrustedLongOneEndpointFragmentMergedInteriorConfidence
            || wall.FragmentEvidence is not { RequiresGeometryReview: false } fragmentEvidence
            || assessment is null
            || assessment.Confidence.Value < MinTrustedLongOneEndpointFragmentMergedInteriorAssessmentConfidence
            || assessment.RejectedAsNoise
            || assessment.Decision == WallEvidenceDecision.Reject
            || assessment.Category != WallEvidenceCategory.MediumWallBody)
        {
            return false;
        }

        var uniqueSourcePrimitiveCount = Math.Max(0, wall.SourcePrimitiveIds.Count - fragmentEvidence.DuplicatePrimitiveCount);
        var fragmentCount = Math.Max(fragmentEvidence.FragmentCount, uniqueSourcePrimitiveCount);
        var evidence = extraEvidence is null
            ? WallEvidenceFor(wall, assessment).ToArray()
            : WallEvidenceFor(wall, assessment).Concat(extraEvidence).ToArray();
        var trustedDenseGraphFragment =
            component.Kind == WallGraphComponentKind.SecondaryStructural
            && fragmentCount is >= MinTrustedDenseLongOneEndpointFragmentMergedInteriorFragments
                and <= MaxTrustedDenseLongOneEndpointFragmentMergedInteriorFragments
            && fragmentEvidence.DuplicatePrimitiveCount <= MaxTrustedDenseLongOneEndpointFragmentMergedInteriorDuplicatePrimitives
            && fragmentEvidence.GapRatio <= 0.001
            && fragmentEvidence.TotalHealedGap <= 0.001
            && fragmentEvidence.MaxHealedGap <= 0.001
            && EvidenceContains(evidence, "promoted to placement-ready by secondary structural graph component")
            && EvidenceContains(evidence, "secondary structural interior fragment continuity");
        var trustedCompactGraphFragment =
            fragmentCount is >= 2 and <= MaxTrustedLongOneEndpointFragmentMergedInteriorFragments
            && fragmentEvidence.DuplicatePrimitiveCount <= MaxTrustedLongOneEndpointFragmentMergedInteriorDuplicatePrimitives
            && fragmentEvidence.GapRatio <= 0.001
            && fragmentEvidence.TotalHealedGap <= 0.001;
        if (!trustedCompactGraphFragment && !trustedDenseGraphFragment)
        {
            return false;
        }

        var reviewOnlyMainStructuralFragment =
            component.Kind == WallGraphComponentKind.MainStructural
            && !assessment.PlacementReady
            && assessment.RequiresReview
            && assessment.Decision == WallEvidenceDecision.Review;
        var reviewOnlySecondaryDenseFragment =
            trustedDenseGraphFragment
            && !assessment.PlacementReady
            && assessment.RequiresReview
            && assessment.Decision == WallEvidenceDecision.Review;
        if ((!assessment.PlacementReady || assessment.RequiresReview)
            && !reviewOnlyMainStructuralFragment
            && !reviewOnlySecondaryDenseFragment)
        {
            return false;
        }

        if (!EvidenceContains(evidence, "supported wall evidence inside exterior envelope")
            || (!EvidenceContains(evidence, "one endpoint supported by structural context")
                && !EvidenceContains(evidence, "both endpoints supported by structural context"))
            || !EvidenceContains(evidence, "merged collinear wall fragments"))
        {
            return false;
        }

        if (reviewOnlyMainStructuralFragment
            && !EvidenceContains(evidence, "unlayered fragment-merged wall candidate has only one trusted structural endpoint"))
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "detail linework",
            "surface pattern",
            "object/fixture",
            "fixture detail",
            "stair",
            "door swing",
            "door leaf",
            "door arc",
            "opening-linked wall fragment",
            "not trusted",
            "without shell support",
            "alone is not trusted");
    }

    public static bool IsTrustedOpeningLinkedFragmentMergedInteriorWallBody(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string>? extraEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (component is null
            || component.Kind == WallGraphComponentKind.ObjectLikeIsland
            || wall.WallType != WallType.Interior
            || wall.DetectionKind != WallDetectionKind.FragmentMerged
            || wall.PairEvidence is not null
            || wall.DrawingLength < MinTrustedOpeningLinkedFragmentMergedInteriorLengthDrawingUnits
            || wall.Confidence.Value < MinTrustedOpeningLinkedFragmentMergedInteriorConfidence
            || wall.FragmentEvidence is not { RequiresGeometryReview: false } fragmentEvidence
            || assessment is null
            || assessment.Confidence.Value < MinTrustedOpeningLinkedFragmentMergedInteriorAssessmentConfidence
            || assessment.RejectedAsNoise
            || assessment.Decision == WallEvidenceDecision.Reject
            || assessment.Category is not (WallEvidenceCategory.StrongWallBody
                or WallEvidenceCategory.MediumWallBody
                or WallEvidenceCategory.RecoveredWallBody))
        {
            return false;
        }

        var uniqueSourcePrimitiveCount = Math.Max(0, wall.SourcePrimitiveIds.Count - fragmentEvidence.DuplicatePrimitiveCount);
        var fragmentCount = Math.Max(fragmentEvidence.FragmentCount, uniqueSourcePrimitiveCount);
        var compactFragmentBody = fragmentCount is >= 2 and <= MaxTrustedOpeningLinkedCompactFragmentMergedInteriorFragments;
        var denseFragmentBody = fragmentCount is > MaxTrustedOpeningLinkedCompactFragmentMergedInteriorFragments
            and <= MaxTrustedOpeningLinkedDenseFragmentMergedInteriorFragments;
        if ((!compactFragmentBody && !denseFragmentBody)
            || fragmentEvidence.DuplicatePrimitiveCount > MaxTrustedOpeningLinkedFragmentMergedInteriorDuplicatePrimitives
            || fragmentEvidence.GapRatio > MaxTrustedOpeningLinkedFragmentMergedInteriorGapRatio
            || fragmentEvidence.TotalHealedGap > Math.Max(
                1.0,
                wall.DrawingLength * MaxTrustedOpeningLinkedFragmentMergedInteriorTotalHealedGapRatio))
        {
            return false;
        }

        var evidence = extraEvidence is null
            ? WallEvidenceFor(wall, assessment).Concat(component.Evidence).ToArray()
            : WallEvidenceFor(wall, assessment).Concat(component.Evidence).Concat(extraEvidence).ToArray();
        if (!EvidenceContains(evidence, "opening-linked wall fragment")
            || !EvidenceContains(evidence, "merged collinear wall fragments")
            || !EvidenceContains(evidence, "supported wall evidence inside exterior envelope"))
        {
            return false;
        }

        var hasEndpointSupport = EvidenceContainsAny(
            evidence,
            "one endpoint supported by structural context",
            "both endpoints supported by structural context",
            "supported endpoint");
        var hasRoomBoundarySupport = HasSemanticWallPlacementSupport(evidence);
        if (!hasEndpointSupport && !hasRoomBoundarySupport)
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "surface pattern",
            "surface/detail",
            "object/fixture",
            "fixture detail",
            "stair",
            "door swing",
            "door leaf",
            "door arc",
            "tiny door-adjacent placement topology piece",
            "opening cutouts fully consume",
            "already represented",
            "recovered duplicate wall body",
            "rejected as non-wall",
            "non-wall",
            "not trusted",
            "without shell support",
            "alone is not trusted");
    }

    public static bool IsTrustedObjectLikeLongCleanFragmentInteriorWallBody(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string>? extraEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (component is null
            || component.Kind != WallGraphComponentKind.ObjectLikeIsland
            || !component.ExcludedFromStructuralTopology
            || wall.WallType != WallType.Interior
            || assessment is null
            || !assessment.PlacementReady
            || assessment.RequiresReview
            || assessment.RejectedAsNoise
            || assessment.Decision == WallEvidenceDecision.Reject
            || assessment.Category != WallEvidenceCategory.MediumWallBody)
        {
            return false;
        }

        var evidence = extraEvidence is null
            ? WallEvidenceFor(wall, assessment).ToArray()
            : WallEvidenceFor(wall, assessment).Concat(extraEvidence).ToArray();
        if (!EvidenceContains(evidence, TrustedObjectLikeLongCleanFragmentInteriorEvidence)
            || !EvidenceContains(evidence, "promoted to placement-ready"))
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "surface pattern",
            "object/fixture detail",
            "fixture detail",
            "repeated short detail",
            "door/opening",
            "door swing",
            "door leaf",
            "door arc",
            "stair",
            "railing",
            "dimension-like",
            "classified Dimension",
            "dimension/annotation",
            "already represented",
            "recovered duplicate wall body",
            "rejected as non-wall",
            "non-wall");
    }

    public static bool IsTrustedObjectLikeExteriorShellPairWallBody(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string>? extraEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (component is null
            || component.Kind != WallGraphComponentKind.ObjectLikeIsland
            || !component.ExcludedFromStructuralTopology
            || wall.DetectionKind != WallDetectionKind.ParallelLinePair
            || wall.DrawingLength < MinTrustedSecondaryExteriorShellLengthDrawingUnits
            || wall.Confidence.Value < 0.74
            || assessment is null
            || !assessment.PlacementReady
            || assessment.RequiresReview
            || assessment.RejectedAsNoise
            || assessment.Decision == WallEvidenceDecision.Reject
            || assessment.Category is not (WallEvidenceCategory.StrongWallBody
                or WallEvidenceCategory.MediumWallBody
                or WallEvidenceCategory.RecoveredWallBody)
            || wall.PairEvidence is not { } pair)
        {
            return false;
        }

        var evidence = extraEvidence is null
            ? WallEvidenceFor(wall, assessment).ToArray()
            : WallEvidenceFor(wall, assessment).Concat(extraEvidence).ToArray();
        var hasExteriorShellEvidence =
            wall.WallType == WallType.Exterior
            || EvidenceContainsAny(
                evidence,
                "wall type exterior",
                "near detected floorplan/wall envelope",
                "local outer boundary",
                "exterior shell",
                "global-room-envelope-edge",
                "global-envelope-fragment-chain");
        if (!hasExteriorShellEvidence
            || !EvidenceContains(evidence, TrustedObjectLikeExteriorShellPairEvidence)
            || pair.Score < MinTrustedSecondaryExteriorShellPairScore
            || pair.OverlapRatio < 0.92
            || pair.FaceSeparation < 4.0
            || pair.FaceSeparation > 30.0
            || MaxFaceFragmentCount(wall, assessment) > 96
            || pair.FirstFaceFragmentCount + pair.SecondFaceFragmentCount > 160)
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "surface pattern",
            "surface/detail",
            "object/fixture",
            "fixture detail",
            "repeated short detail",
            "door/opening",
            "door swing",
            "door leaf",
            "door arc",
            "opening detail",
            "stair",
            "non-wall",
            "not trusted",
            "without shell support",
            "alone is not trusted");
    }

    public static bool IsTrustedRejectedStrongBoundaryWallBody(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string>? extraEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (component?.Kind != WallGraphComponentKind.ObjectLikeIsland
            || !component.ExcludedFromStructuralTopology
            || wall.DetectionKind != WallDetectionKind.ParallelLinePair
            || wall.DrawingLength < 30.0
            || wall.Confidence.Value < 0.78
            || assessment is null
            || assessment.Category != WallEvidenceCategory.ObjectOrFixtureDetail
            || (!assessment.RejectedAsNoise && assessment.Decision != WallEvidenceDecision.Reject)
            || assessment.Confidence.Value < 0.72)
        {
            return false;
        }

        var evidence = extraEvidence is null
            ? WallEvidenceFor(wall, assessment)
            : WallEvidenceFor(wall, assessment).Concat(extraEvidence).ToArray();
        var pairScore = wall.PairEvidence?.Score
            ?? evidence.Select(TryReadPairScore).FirstOrDefault(value => value.HasValue);
        var overlapRatio = wall.PairEvidence?.OverlapRatio
            ?? evidence.Select(TryReadOverlapRatio).FirstOrDefault(value => value.HasValue);
        var faceSeparation = wall.PairEvidence?.FaceSeparation
            ?? evidence.Select(TryReadFaceSeparation).FirstOrDefault(value => value.HasValue);
        var maxFaceFragments = wall.PairEvidence is { } pair
            ? Math.Max(pair.FirstFaceFragmentCount, pair.SecondFaceFragmentCount)
            : 0;
        if (pairScore is null
            || overlapRatio is null
            || faceSeparation is null
            || pairScore.Value < 0.86
            || overlapRatio.Value < 0.90
            || faceSeparation.Value < 1.0
            || faceSeparation.Value > 18.0
            || maxFaceFragments > 160)
        {
            return false;
        }

        var hasStrongWallBodyEvidence = EvidenceContainsAny(
            evidence,
            "wall evidence assessment: StrongWallBody / placement-ready",
            "wall evidence: strong double-edge wall body",
            "wall evidence: filled closed vector wall body",
            "filled wall-solid primitive");
        var hasBoundaryEvidence = EvidenceContainsAny(
            evidence,
            "detected room evidence on both sides",
            "shared by room adjacency boundary",
            "geometric room boundary support",
            "explicit room boundary support",
            "continuity-supported short paired wall body",
            "supported wall evidence inside exterior envelope",
            "wall type exterior: near detected floorplan/wall envelope",
            "local outer boundary",
            "exterior shell");
        if (!hasStrongWallBodyEvidence || !hasBoundaryEvidence)
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "detail linework",
            "surface pattern",
            "surface/detail pattern",
            "repeated short detail",
            "door/opening",
            "door swing",
            "door leaf",
            "door arc",
            "opening detail",
            "opening-linked wall fragment",
            "stair",
            "dimension-like",
            "classified Dimension",
            "dimension/annotation",
            "already represented",
            "recovered duplicate wall body",
            "not trusted",
            "without shell support",
            "alone is not trusted");
    }

    public static bool IsTrustedRejectedMediumBoundaryFragmentWallBody(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string>? extraEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (component?.Kind != WallGraphComponentKind.ObjectLikeIsland
            || !component.ExcludedFromStructuralTopology
            || wall.DetectionKind != WallDetectionKind.FragmentMerged
            || wall.DrawingLength < 64.0
            || wall.Confidence.Value < 0.82
            || wall.FragmentEvidence is not { RequiresGeometryReview: false } fragmentEvidence
            || fragmentEvidence.FragmentCount < 8
            || fragmentEvidence.FragmentCount > 80
            || fragmentEvidence.DuplicatePrimitiveCount > 12
            || fragmentEvidence.GapRatio > 0.002
            || fragmentEvidence.TotalHealedGap > 0.25
            || fragmentEvidence.MaxHealedGap > 0.25
            || assessment is null
            || assessment.Category != WallEvidenceCategory.ObjectOrFixtureDetail
            || (!assessment.RejectedAsNoise && assessment.Decision != WallEvidenceDecision.Reject)
            || assessment.Confidence.Value < 0.80)
        {
            return false;
        }

        var evidence = extraEvidence is null
            ? WallEvidenceFor(wall, assessment)
            : WallEvidenceFor(wall, assessment).Concat(extraEvidence).ToArray();
        if (!EvidenceContains(evidence, "merged collinear wall fragments")
            || !EvidenceContains(evidence, "wall evidence assessment: MediumWallBody / placement-ready")
            || !EvidenceContains(evidence, "supported wall evidence inside exterior envelope")
            || !EvidenceContains(evidence, "both endpoints supported by structural context"))
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "duplicate wall-face",
            "already represented by stronger paired wall body",
            "already represented by clean topology span",
            "recovered duplicate wall body",
            "only one trusted structural endpoint",
            "one endpoint supported by structural context",
            "not placement-ready without review",
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "detail linework",
            "surface pattern",
            "surface/detail pattern",
            "repeated short detail",
            "door/opening",
            "door swing",
            "door leaf",
            "door arc",
            "opening detail",
            "opening-linked wall fragment",
            "stair");
    }

    public static bool IsTrustedRejectedObjectLikeBoundaryRecallWallBody(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string>? extraEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (component?.Kind != WallGraphComponentKind.ObjectLikeIsland
            || !component.ExcludedFromStructuralTopology
            || component.WallIds.Count > 3
            || wall.DrawingLength < MinTrustedRejectedObjectLikeBoundaryRecallLengthDrawingUnits
            || wall.Confidence.Value < MinTrustedRejectedObjectLikeBoundaryRecallConfidence
            || assessment is null
            || assessment.Category != WallEvidenceCategory.ObjectOrFixtureDetail
            || (!assessment.RejectedAsNoise && assessment.Decision != WallEvidenceDecision.Reject)
            || assessment.Confidence.Value < MinTrustedRejectedObjectLikeBoundaryRecallConfidence
            || (!wall.CenterLine.IsHorizontal() && !wall.CenterLine.IsVertical()))
        {
            return false;
        }

        var evidence = extraEvidence is null
            ? WallEvidenceFor(wall, assessment)
            : WallEvidenceFor(wall, assessment).Concat(extraEvidence).ToArray();
        var hasOriginalPlacementReadyWallBody =
            EvidenceContains(evidence, "wall evidence assessment: StrongWallBody / placement-ready")
            || EvidenceContains(evidence, "wall evidence assessment: MediumWallBody / placement-ready")
            || EvidenceContains(evidence, "wall evidence assessment: RecoveredWallBody / placement-ready");
        var hasInteriorBoundaryEvidence =
            EvidenceContains(evidence, "wall type interior: supported wall evidence inside exterior envelope")
            || EvidenceContains(evidence, "detected room evidence on both sides")
            || EvidenceContains(evidence, "geometric room boundary support")
            || EvidenceContains(evidence, "shared by room adjacency boundary")
            || EvidenceContains(evidence, "explicit room boundary support");
        var hasExteriorBoundaryEvidence =
            EvidenceContains(evidence, "wall type exterior: near detected floorplan/wall envelope")
            || EvidenceContains(evidence, "local outer boundary")
            || EvidenceContains(evidence, "exterior shell")
            || EvidenceContains(evidence, "global-room-envelope-edge")
            || EvidenceContains(evidence, "global-envelope-fragment-chain");
        if (!hasOriginalPlacementReadyWallBody
            || (!hasInteriorBoundaryEvidence && !hasExteriorBoundaryEvidence))
        {
            return false;
        }

        var pairedBody = IsTrustedRejectedObjectLikeRecallPair(wall, evidence);
        var singleOrFragmentBody = IsTrustedRejectedObjectLikeRecallSingleOrFragment(wall);
        if (!pairedBody && !singleOrFragmentBody)
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "duplicate wall-face",
            "already represented by stronger paired wall body",
            "already represented by clean topology span",
            "recovered duplicate wall body",
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "detail linework",
            "surface pattern",
            "surface/detail pattern",
            "repeated short detail",
            "door/opening",
            "door swing",
            "door leaf",
            "door arc",
            "opening detail",
            "opening-linked wall fragment",
            "stair",
            "not trusted",
            "without shell support",
            "alone is not trusted",
            "demoted from placement-ready");
    }

    private static bool IsTrustedRejectedObjectLikeRecallPair(
        WallSegment wall,
        IReadOnlyList<string> evidence)
    {
        var pairScore = wall.PairEvidence?.Score
            ?? evidence.Select(TryReadPairScore).Where(score => score.HasValue).Select(score => score!.Value).DefaultIfEmpty(0).Max();
        var overlapRatio = wall.PairEvidence?.OverlapRatio
            ?? evidence.Select(TryReadOverlapRatio).Where(ratio => ratio.HasValue).Select(ratio => ratio!.Value).DefaultIfEmpty(0).Max();
        var faceSeparation = wall.PairEvidence?.FaceSeparation
            ?? evidence.Select(TryReadFaceSeparation).Where(separation => separation.HasValue).Select(separation => separation!.Value).DefaultIfEmpty(0).Max();
        var maxFaceFragments = wall.PairEvidence is null
            ? evidence.SelectMany(EvidenceFragmentCounts).DefaultIfEmpty(0).Max()
            : Math.Max(wall.PairEvidence.FirstFaceFragmentCount, wall.PairEvidence.SecondFaceFragmentCount);

        return pairScore >= MinTrustedRejectedObjectLikeBoundaryRecallPairScore
            && overlapRatio >= MinTrustedRejectedObjectLikeBoundaryRecallOverlapRatio
            && faceSeparation >= MinTrustedRejectedObjectLikeBoundaryRecallFaceSeparationDrawingUnits
            && faceSeparation <= MaxTrustedRejectedObjectLikeBoundaryRecallFaceSeparationDrawingUnits
            && maxFaceFragments <= MaxTrustedRejectedObjectLikeBoundaryRecallFaceFragments;
    }

    private static bool IsTrustedRejectedObjectLikeRecallSingleOrFragment(WallSegment wall)
    {
        if (wall.DetectionKind is not (WallDetectionKind.SingleLine or WallDetectionKind.FragmentMerged))
        {
            return false;
        }

        if (wall.FragmentEvidence is not { } fragmentEvidence)
        {
            return wall.SourcePrimitiveIds.Count > 0;
        }

        var uniqueSourcePrimitiveCount = Math.Max(0, wall.SourcePrimitiveIds.Count - fragmentEvidence.DuplicatePrimitiveCount);
        var fragmentCount = Math.Max(fragmentEvidence.FragmentCount, uniqueSourcePrimitiveCount);
        return !fragmentEvidence.RequiresGeometryReview
            && fragmentCount <= MaxTrustedRejectedObjectLikeBoundaryRecallFragmentCount
            && fragmentEvidence.DuplicatePrimitiveCount <= MaxTrustedRejectedObjectLikeBoundaryRecallDuplicatePrimitives
            && fragmentEvidence.GapRatio <= MaxTrustedRejectedObjectLikeBoundaryRecallGapRatio
            && fragmentEvidence.TotalHealedGap <= Math.Max(3.5, wall.DrawingLength * 0.04)
            && fragmentEvidence.MaxHealedGap <= Math.Max(3.5, wall.Thickness);
    }

    public static bool IsTrustedDenseTwoSidedRoomFragmentMergedInteriorWallBody(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string>? extraEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(wall);

        if (component?.Kind != WallGraphComponentKind.MainStructural
            || component.ExcludedFromStructuralTopology
            || wall.WallType != WallType.Interior
            || wall.DetectionKind != WallDetectionKind.FragmentMerged
            || wall.PairEvidence is not null
            || wall.DrawingLength < MinTrustedDenseTwoSidedRoomFragmentMergedInteriorLengthDrawingUnits
            || wall.Confidence.Value < MinTrustedDenseTwoSidedRoomFragmentMergedInteriorConfidence
            || wall.FragmentEvidence is not { RequiresGeometryReview: false } fragmentEvidence
            || assessment is null
            || assessment.Confidence.Value < MinTrustedDenseTwoSidedRoomFragmentMergedInteriorConfidence
            || assessment.RejectedAsNoise
            || assessment.Decision == WallEvidenceDecision.Reject
            || assessment.Category != WallEvidenceCategory.MediumWallBody)
        {
            return false;
        }

        var uniqueSourcePrimitiveCount = Math.Max(0, wall.SourcePrimitiveIds.Count - fragmentEvidence.DuplicatePrimitiveCount);
        var fragmentCount = Math.Max(fragmentEvidence.FragmentCount, uniqueSourcePrimitiveCount);
        var maxTotalHealedGap = Math.Max(4.0, wall.DrawingLength * MaxTrustedDenseTwoSidedRoomFragmentMergedInteriorTotalHealedGapRatio);
        if (fragmentCount is < 8 or > MaxTrustedDenseTwoSidedRoomFragmentMergedInteriorFragments
            || fragmentEvidence.DuplicatePrimitiveCount > MaxTrustedDenseTwoSidedRoomFragmentMergedInteriorDuplicatePrimitives
            || fragmentEvidence.GapRatio > MaxTrustedDenseTwoSidedRoomFragmentMergedInteriorGapRatio
            || fragmentEvidence.TotalHealedGap > maxTotalHealedGap
            || fragmentEvidence.MaxHealedGap > maxTotalHealedGap)
        {
            return false;
        }

        var evidence = extraEvidence is null
            ? WallEvidenceFor(wall, assessment).Concat(component.Evidence).ToArray()
            : WallEvidenceFor(wall, assessment).Concat(component.Evidence).Concat(extraEvidence).ToArray();
        if (!EvidenceContains(evidence, "merged collinear wall fragments")
            || !EvidenceContains(evidence, "supported wall evidence inside exterior envelope")
            || !EvidenceContains(evidence, "detected room evidence on both sides")
            || (!EvidenceContains(evidence, "one endpoint supported by structural context")
                && !EvidenceContains(evidence, "only one trusted structural endpoint")
                && !EvidenceContains(evidence, "both endpoints supported by structural context")))
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "detail linework",
            "surface pattern",
            "object/fixture",
            "fixture detail",
            "stair",
            "door swing",
            "door leaf",
            "door arc",
            "opening-linked wall fragment",
            "not trusted",
            "without shell support",
            "alone is not trusted");
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildReviewReasons(PlanScanResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var roomWallReferences = RoomBoundaryWallReferenceBuilder.Build(
            result.Rooms,
            result.Walls,
            wallSnapTolerance: 2.0);
        var roomWallIds = roomWallReferences.RoomIdsByWallId.Keys.ToHashSet(StringComparer.Ordinal);
        if (result.Rooms.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }

        var componentByWallId = BuildComponentByWallId(result.WallGraph.Components);
        var wallById = result.Walls
            .Where(wall => !string.IsNullOrWhiteSpace(wall.Id))
            .ToDictionary(wall => wall.Id, StringComparer.Ordinal);
        var wallEvidenceByWallId = result.WallEvidenceMap.WallAssessments
            .Where(assessment => !string.IsNullOrWhiteSpace(assessment.WallId))
            .ToDictionary(assessment => assessment.WallId, StringComparer.Ordinal);
        var reasonsByWallId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var objectLineworkCandidatesByPage = BuildObjectLineworkCandidatesByPage(result.ObjectCandidates);
        var detailLineworkCandidatesByPage = BuildDetailLineworkCandidatesByPage(result.ObjectCandidates);

        foreach (var wall in result.Walls)
        {
            componentByWallId.TryGetValue(wall.Id, out var component);
            var wallIsRoomBoundary = roomWallIds.Contains(wall.Id);
            var hasRoomBoundarySupport = SecondaryStructuralComponentHasRoomBoundarySupport(component, roomWallIds);
            if (!wallIsRoomBoundary
                && MainStructuralInteriorWallNeedsSemanticSupportReview(
                    wall,
                    component,
                    wallEvidenceByWallId))
            {
                AddReason(
                    reasonsByWallId,
                    wall.Id,
                    MainStructuralInteriorWithoutSemanticSupportReason);
            }

            if (!wallIsRoomBoundary
                && FragmentMergedInteriorWallNeedsRoomBoundaryReview(
                    wall,
                    component,
                    wallEvidenceByWallId))
            {
                AddReason(
                    reasonsByWallId,
                    wall.Id,
                    FragmentMergedInteriorWithoutRoomBoundarySupportReason);
            }

            if (hasRoomBoundarySupport
                && SecondaryStructuralWallHasOverSourcedDetailLineworkRisk(
                    wall,
                    component,
                    wallById,
                    wallEvidenceByWallId,
                    detailLineworkCandidatesByPage))
            {
                AddReason(
                    reasonsByWallId,
                    wall.Id,
                    SecondaryStructuralOverSourcedDetailLineworkReason);
            }
            else if (!hasRoomBoundarySupport
                && !SecondaryStructuralComponentHasTrustedPairedWallBodySupport(
                    wall,
                    component,
                    wallById,
                    wallEvidenceByWallId)
                && SecondaryStructuralWallOverlapsObjectLinework(
                    wall,
                    component,
                    objectLineworkCandidatesByPage))
            {
                AddReason(
                    reasonsByWallId,
                    wall.Id,
                    SecondaryStructuralObjectLineworkWithoutRoomBoundarySupportReason);
            }
            else if (!hasRoomBoundarySupport
                && !SecondaryStructuralWallHasTrustedExteriorShellSupport(
                    wall,
                    component,
                    wallEvidenceByWallId)
                && !SecondaryStructuralWallHasTrustedTwoSidedFragmentRoomSupport(
                    wall,
                    component,
                    wallEvidenceByWallId)
                && !SecondaryStructuralWallHasTrustedLongOneEndpointFragmentSupport(
                    wall,
                    component,
                    wallEvidenceByWallId)
                && !SecondaryStructuralComponentHasTrustedPairedWallBodySupport(
                    wall,
                    component,
                    wallById,
                    wallEvidenceByWallId))
            {
                AddReason(reasonsByWallId, wall.Id, SecondaryStructuralWithoutRoomBoundarySupportReason);
            }
        }

        return reasonsByWallId.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.Distinct(StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
    }

    public static bool SecondaryStructuralComponentHasRoomBoundarySupport(
        WallGraphComponent? component,
        IReadOnlySet<string> roomWallIds)
    {
        ArgumentNullException.ThrowIfNull(roomWallIds);

        if (component?.Kind != WallGraphComponentKind.SecondaryStructural)
        {
            return true;
        }

        return component.WallIds.Any(roomWallIds.Contains);
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<ObjectCandidate>> BuildObjectLineworkCandidatesByPage(
        IReadOnlyList<ObjectCandidate> objectCandidates)
    {
        return objectCandidates
            .Where(IsWallContaminatingObjectLinework)
            .GroupBy(candidate => candidate.PageNumber)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ObjectCandidate>)group.ToArray());
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<ObjectCandidate>> BuildDetailLineworkCandidatesByPage(
        IReadOnlyList<ObjectCandidate> objectCandidates)
    {
        return objectCandidates
            .Where(IsWallContaminatingDetailLinework)
            .GroupBy(candidate => candidate.PageNumber)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ObjectCandidate>)group.ToArray());
    }

    private static bool SecondaryStructuralWallOverlapsObjectLinework(
        WallSegment wall,
        WallGraphComponent? component,
        IReadOnlyDictionary<int, IReadOnlyList<ObjectCandidate>> objectLineworkCandidatesByPage)
    {
        if (component?.Kind != WallGraphComponentKind.SecondaryStructural
            || component.ExcludedFromStructuralTopology
            || wall.WallType == WallType.Exterior
            || wall.DrawingLength < 36
            || (!wall.CenterLine.IsHorizontal() && !wall.CenterLine.IsVertical())
            || !objectLineworkCandidatesByPage.TryGetValue(wall.PageNumber, out var candidates))
        {
            return false;
        }

        var guardTolerance = Math.Max(8, wall.Thickness * 1.5);
        foreach (var candidate in candidates)
        {
            if (LineOverlapsCandidateGuardZone(
                wall.CenterLine,
                candidate.Bounds.Inflate(guardTolerance),
                minimumOverlapLength: Math.Min(42, Math.Max(24, wall.DrawingLength * 0.35)),
                minimumOverlapRatio: 0.45))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SecondaryStructuralWallHasOverSourcedDetailLineworkRisk(
        WallSegment wall,
        WallGraphComponent? component,
        IReadOnlyDictionary<string, WallSegment> wallById,
        IReadOnlyDictionary<string, WallEvidenceWallAssessment> wallEvidenceByWallId,
        IReadOnlyDictionary<int, IReadOnlyList<ObjectCandidate>> detailLineworkCandidatesByPage)
    {
        if (component?.Kind != WallGraphComponentKind.SecondaryStructural
            || component.ExcludedFromStructuralTopology
            || component.WallIds.Count is < 1 or > 3
            || wall.WallType == WallType.Exterior
            || wall.DrawingLength < 48
            || (!wall.CenterLine.IsHorizontal() && !wall.CenterLine.IsVertical())
            || HasTrustedTwoSidedRoomBoundarySupport(wall, wallEvidenceByWallId)
            || (component.WallIds.Count > 1
                && SecondaryStructuralComponentHasTrustedPairedWallBodySupport(
                    wall,
                    component,
                    wallById,
                    wallEvidenceByWallId))
            || !LooksLikeOverSourcedCompactSecondaryComponent(component, wallById, wallEvidenceByWallId)
            || !detailLineworkCandidatesByPage.TryGetValue(wall.PageNumber, out var candidates))
        {
            return false;
        }

        var guardTolerance = Math.Max(8, wall.Thickness * 2.0);
        return candidates.Any(candidate =>
            LineOverlapsCandidateGuardZone(
                wall.CenterLine,
                candidate.Bounds.Inflate(guardTolerance),
                minimumOverlapLength: Math.Min(48, Math.Max(24, wall.DrawingLength * 0.30)),
                minimumOverlapRatio: 0.35));
    }

    private static bool HasTrustedTwoSidedRoomBoundarySupport(
        WallSegment wall,
        IReadOnlyDictionary<string, WallEvidenceWallAssessment> wallEvidenceByWallId)
    {
        if (wall.WallType != WallType.Interior
            || !wallEvidenceByWallId.TryGetValue(wall.Id, out var assessment)
            || assessment.Category != WallEvidenceCategory.StrongWallBody
            || !assessment.PlacementReady
            || assessment.RequiresReview
            || assessment.RejectedAsNoise)
        {
            return false;
        }

        return wall.Evidence
            .Concat(assessment.Evidence)
            .Any(item =>
                item.Contains("detected room evidence on both sides", StringComparison.OrdinalIgnoreCase)
                || item.Contains("shared by room adjacency boundary", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MainStructuralInteriorWallNeedsSemanticSupportReview(
        WallSegment wall,
        WallGraphComponent? component,
        IReadOnlyDictionary<string, WallEvidenceWallAssessment> wallEvidenceByWallId)
    {
        if (component?.Kind != WallGraphComponentKind.MainStructural
            || component.ExcludedFromStructuralTopology
            || wall.WallType == WallType.Exterior
            || wall.DrawingLength < 72
            || (!wall.CenterLine.IsHorizontal() && !wall.CenterLine.IsVertical())
            || wall.FragmentEvidence?.RequiresGeometryReview == true
            || !wallEvidenceByWallId.TryGetValue(wall.Id, out var assessment)
            || !assessment.PlacementReady
            || assessment.RequiresReview
            || assessment.RejectedAsNoise
            || assessment.Decision == WallEvidenceDecision.Reject
            || assessment.Category is not (WallEvidenceCategory.StrongWallBody
                or WallEvidenceCategory.MediumWallBody
                or WallEvidenceCategory.RecoveredWallBody))
        {
            return false;
        }

        var evidence = WallEvidenceFor(wall, assessment);
        if (HasSemanticWallPlacementSupport(evidence)
            || HasTrustedExplicitWallLayerSupport(evidence))
        {
            return false;
        }

        if (HasTrustedLongMainStructuralInteriorWallBodySupport(wall, component, assessment, evidence))
        {
            return false;
        }

        if (HasTrustedNoisyMainStructuralInteriorWallBodySupport(wall, component, assessment, evidence))
        {
            return false;
        }

        if (IsTrustedOneEndpointNoisyMainStructuralInteriorWallBody(wall, component, assessment, evidence))
        {
            return false;
        }

        if (IsTrustedRecoveredMainStructuralInteriorWallBody(wall, component, assessment, evidence))
        {
            return false;
        }

        if (assessment.Category == WallEvidenceCategory.RecoveredWallBody)
        {
            return true;
        }

        if (EvidenceContainsAny(
            evidence,
            "layer (unlayered) classified Dimension",
            "layer evidence: contains dimension-like text",
            "dimension-like weak layer"))
        {
            return true;
        }

        var sourcePrimitiveCount = wall.SourcePrimitiveIds
            .Concat(assessment.SourcePrimitiveIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var maxFaceFragmentCount = MaxFaceFragmentCount(wall, assessment);
        var unknownLayer = EvidenceContainsAny(
            evidence,
            "layer (unlayered) classified Unknown",
            "layer evidence: no strong layer name or geometry evidence",
            "source layer category Unknown");

        return unknownLayer
            && (sourcePrimitiveCount >= 18
                || maxFaceFragmentCount >= 24
                || wall.FragmentEvidence?.FragmentCount >= 8
                || wall.FragmentEvidence?.DuplicatePrimitiveCount >= 4);
    }

    private static bool HasTrustedLongMainStructuralInteriorWallBodySupport(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment assessment,
        IReadOnlyList<string> evidence)
    {
        if (component?.Kind != WallGraphComponentKind.MainStructural
            || component.ExcludedFromStructuralTopology
            || wall.WallType != WallType.Interior
            || wall.DetectionKind != WallDetectionKind.ParallelLinePair
            || wall.DrawingLength < MinTrustedMainStructuralInteriorLengthDrawingUnits
            || wall.Confidence.Value < 0.78
            || assessment.Confidence.Value < 0.78
            || assessment.Category != WallEvidenceCategory.StrongWallBody
            || wall.PairEvidence is not { } pair
            || !HasStrongPairedWallBodyEvidence(wall, assessment))
        {
            return false;
        }

        if (pair.Score < MinTrustedMainStructuralInteriorPairScore
            || pair.OverlapRatio < MinTrustedMainStructuralInteriorOverlapRatio
            || pair.FaceSeparation < MinTrustedMainStructuralInteriorFaceSeparationDrawingUnits
            || pair.FaceSeparation > MaxTrustedMainStructuralInteriorFaceSeparationDrawingUnits
            || Math.Max(pair.FirstFaceFragmentCount, pair.SecondFaceFragmentCount)
                > MaxTrustedMainStructuralInteriorFaceFragments)
        {
            return false;
        }

        if (!EvidenceContains(evidence, "supported wall evidence inside exterior envelope"))
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "detail linework",
            "surface pattern",
            "object/fixture",
            "fixture detail",
            "stair",
            "tiny door-adjacent placement topology piece suppressed",
            "not trusted",
            "without shell support",
            "alone is not trusted");
    }

    private static bool HasTrustedNoisyMainStructuralInteriorWallBodySupport(
        WallSegment wall,
        WallGraphComponent? component,
        WallEvidenceWallAssessment assessment,
        IReadOnlyList<string> evidence)
    {
        if (component?.Kind != WallGraphComponentKind.MainStructural
            || component.ExcludedFromStructuralTopology
            || wall.WallType != WallType.Interior
            || wall.DetectionKind != WallDetectionKind.ParallelLinePair
            || wall.DrawingLength < MinTrustedMainStructuralInteriorLengthDrawingUnits
            || wall.Confidence.Value < 0.80
            || assessment.Confidence.Value < 0.80
            || assessment.Category != WallEvidenceCategory.StrongWallBody
            || wall.PairEvidence is not { } pair
            || !HasStrongPairedWallBodyEvidence(wall, assessment)
            || !EvidenceContains(evidence, "supported wall evidence inside exterior envelope")
            || !EvidenceContains(evidence, "both endpoints supported by structural context"))
        {
            return false;
        }

        if (pair.Score < MinTrustedNoisyMainStructuralInteriorPairScore
            || pair.OverlapRatio < MinTrustedNoisyMainStructuralInteriorOverlapRatio
            || pair.FaceSeparation < MinTrustedMainStructuralInteriorFaceSeparationDrawingUnits
            || pair.FaceSeparation > MaxTrustedMainStructuralInteriorFaceSeparationDrawingUnits
            || Math.Max(pair.FirstFaceFragmentCount, pair.SecondFaceFragmentCount)
                > MaxTrustedNoisyMainStructuralInteriorFaceFragments)
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "detail linework",
            "surface pattern",
            "object/fixture",
            "fixture detail",
            "stair",
            "door swing",
            "door leaf",
            "door arc",
            "not trusted",
            "without shell support",
            "alone is not trusted");
    }

    private static bool SecondaryStructuralWallHasTrustedExteriorShellSupport(
        WallSegment wall,
        WallGraphComponent? component,
        IReadOnlyDictionary<string, WallEvidenceWallAssessment> wallEvidenceByWallId)
    {
        if (component?.Kind != WallGraphComponentKind.SecondaryStructural
            || component.ExcludedFromStructuralTopology
            || wall.WallType != WallType.Exterior
            || wall.DetectionKind != WallDetectionKind.ParallelLinePair
            || wall.DrawingLength < MinTrustedSecondaryExteriorShellLengthDrawingUnits
            || wall.Confidence.Value < 0.74
            || !wallEvidenceByWallId.TryGetValue(wall.Id, out var assessment)
            || !assessment.PlacementReady
            || assessment.RequiresReview
            || assessment.RejectedAsNoise
            || assessment.Decision == WallEvidenceDecision.Reject
            || assessment.Category != WallEvidenceCategory.StrongWallBody
            || !HasStrongPairedWallBodyEvidence(wall, assessment))
        {
            return false;
        }

        var evidence = WallEvidenceFor(wall, assessment)
            .Concat(component.Evidence)
            .ToArray();
        if (!EvidenceContainsAny(
                evidence,
                "near detected floorplan/wall envelope",
                "local outer boundary",
                "trusted exterior shell",
                "exterior shell continuity"))
        {
            return false;
        }

        if (EvidenceContainsAny(
                evidence,
                "outdoor covered-area boundary",
                "unpaired outdoor covered-area boundary",
                "covered-area boundary",
                "outdoor/terrace room evidence alone",
                "terrace",
                "covered entry",
                "covered-entry",
                "overbygd",
                "canopy",
                "railing",
                "trim/detail",
                "trim linework",
                "glazing",
                "detail linework",
                "surface pattern",
                "not trusted",
                "without shell support",
                "alone is not trusted"))
        {
            return false;
        }

        var pairScore = wall.PairEvidence?.Score
            ?? evidence.Select(TryReadPairScore).Where(score => score.HasValue).Select(score => score!.Value).DefaultIfEmpty(0).Max();
        var overlapRatio = wall.PairEvidence?.OverlapRatio
            ?? evidence.Select(TryReadOverlapRatio).Where(ratio => ratio.HasValue).Select(ratio => ratio!.Value).DefaultIfEmpty(0).Max();
        var faceSeparation = wall.PairEvidence?.FaceSeparation
            ?? evidence.Select(TryReadFaceSeparation).Where(separation => separation.HasValue).Select(separation => separation!.Value).DefaultIfEmpty(0).Max();
        if (pairScore < MinTrustedSecondaryExteriorShellPairScore
            || overlapRatio < MinTrustedSecondaryExteriorShellOverlapRatio
            || faceSeparation < MinTrustedSecondaryExteriorShellFaceSeparationDrawingUnits
            || faceSeparation > MaxTrustedSecondaryExteriorShellFaceSeparationDrawingUnits
            || MaxFaceFragmentCount(wall, assessment) > MaxTrustedSecondaryExteriorShellFaceFragments)
        {
            return false;
        }

        return true;
    }

    private static bool SecondaryStructuralWallHasTrustedTwoSidedFragmentRoomSupport(
        WallSegment wall,
        WallGraphComponent? component,
        IReadOnlyDictionary<string, WallEvidenceWallAssessment> wallEvidenceByWallId) =>
        component?.Kind == WallGraphComponentKind.SecondaryStructural
        && wallEvidenceByWallId.TryGetValue(wall.Id, out var assessment)
        && WallPlacementReadinessEvaluator.IsTrustedTwoSidedFragmentMergedRoomBoundary(
            wall,
            component,
            assessment);

    private static bool SecondaryStructuralWallHasTrustedLongOneEndpointFragmentSupport(
        WallSegment wall,
        WallGraphComponent? component,
        IReadOnlyDictionary<string, WallEvidenceWallAssessment> wallEvidenceByWallId) =>
        component?.Kind == WallGraphComponentKind.SecondaryStructural
        && wallEvidenceByWallId.TryGetValue(wall.Id, out var assessment)
        && (IsTrustedLongOneEndpointFragmentMergedInteriorWallBody(
                wall,
                component,
                assessment)
            || IsTrustedOpeningLinkedFragmentMergedInteriorWallBody(
                wall,
                component,
                assessment));

    private static IReadOnlyList<string> WallEvidenceFor(
        WallSegment wall,
        WallEvidenceWallAssessment assessment) =>
        wall.Evidence
            .Concat(assessment.Evidence)
            .Concat(assessment.ScoreBreakdown.PositiveEvidence)
            .Concat(assessment.ScoreBreakdown.NegativeEvidence)
            .ToArray();

    private static bool HasSemanticWallPlacementSupport(IReadOnlyList<string> evidence) =>
        EvidenceContainsAny(
            evidence,
            "detected room evidence on both sides",
            "shared by room adjacency boundary",
            "explicit room boundary support",
            "geometric room boundary support",
            "retained by room boundary support",
            "room-confirmed wall body",
            "clean fragment-merged interior room boundary promoted",
            "room boundary evidence");

    private static bool HasTrustedExplicitWallLayerSupport(IReadOnlyList<string> evidence) =>
        EvidenceContainsAny(
            evidence,
            "wall-like layer",
            "trusted benchmark",
            "trusted exterior shell",
            "exterior shell continuity",
            "wall evidence: retained by exterior shell continuity");

    private static int MaxFaceFragmentCount(
        WallSegment wall,
        WallEvidenceWallAssessment assessment)
    {
        var pairMax = wall.PairEvidence is null
            ? 0
            : Math.Max(wall.PairEvidence.FirstFaceFragmentCount, wall.PairEvidence.SecondFaceFragmentCount);
        var evidenceMax = WallEvidenceFor(wall, assessment)
            .SelectMany(EvidenceFragmentCounts)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(pairMax, evidenceMax);
    }

    private static IEnumerable<int> EvidenceFragmentCounts(string evidence)
    {
        var markers = new[]
        {
            "first face merged ",
            "second face merged ",
            "face merged ",
            "max face fragments ",
            "total face fragments "
        };

        foreach (var marker in markers)
        {
            var count = TryReadEvidenceCount(evidence, marker);
            if (count.HasValue)
            {
                yield return count.Value;
            }
        }
    }

    private static bool LooksLikeOverSourcedCompactSecondaryComponent(
        WallGraphComponent component,
        IReadOnlyDictionary<string, WallSegment> wallById,
        IReadOnlyDictionary<string, WallEvidenceWallAssessment> wallEvidenceByWallId)
    {
        var shortSide = Math.Min(component.Bounds.Width, component.Bounds.Height);
        var longSide = Math.Max(component.Bounds.Width, component.Bounds.Height);
        if (shortSide <= 0.001
            || longSide < 72
            || shortSide > 18
            || longSide / Math.Max(shortSide, 0.001) < 8)
        {
            return false;
        }

        var distinctSourcePrimitiveCount = component.SourcePrimitiveIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (distinctSourcePrimitiveCount < Math.Max(24, component.WallIds.Count * 12))
        {
            return false;
        }

        var walls = component.WallIds
            .Select(wallId => wallById.TryGetValue(wallId, out var wall) ? wall : null)
            .OfType<WallSegment>()
            .ToArray();
        if (walls.Length != component.WallIds.Count
            || walls.Any(wall => wall.DetectionKind != WallDetectionKind.ParallelLinePair))
        {
            return false;
        }

        return walls.Any(wall =>
            wall.SourcePrimitiveIds.Count >= 18
            || (wallEvidenceByWallId.TryGetValue(wall.Id, out var assessment)
                && assessment.SourcePrimitiveIds.Count >= 18)
            || wall.Evidence.Any(IsHeavyMergedOrCollapsedFaceEvidence));
    }

    private static bool FragmentMergedInteriorWallNeedsRoomBoundaryReview(
        WallSegment wall,
        WallGraphComponent? component,
        IReadOnlyDictionary<string, WallEvidenceWallAssessment> wallEvidenceByWallId)
    {
        if (wall.WallType != WallType.Interior
            || wall.DetectionKind != WallDetectionKind.FragmentMerged
            || wall.PairEvidence is not null
            || wall.FragmentEvidence is not { RequiresGeometryReview: false } fragmentEvidence
            || wall.DrawingLength < Math.Max(48, wall.Thickness * 7.0)
            || component?.ExcludedFromStructuralTopology == true
            || component?.Kind is WallGraphComponentKind.ObjectLikeIsland or WallGraphComponentKind.IsolatedFragment)
        {
            return false;
        }

        if (!wallEvidenceByWallId.TryGetValue(wall.Id, out var assessment)
            || !assessment.PlacementReady
            || assessment.RequiresReview
            || assessment.RejectedAsNoise
            || assessment.Category != WallEvidenceCategory.MediumWallBody)
        {
            return false;
        }

        if (HasSemanticWallPlacementSupport(WallEvidenceFor(wall, assessment)))
        {
            return false;
        }

        var fragmentCount = Math.Max(fragmentEvidence.FragmentCount, wall.SourcePrimitiveIds.Count);
        return fragmentCount >= 10
            || fragmentEvidence.DuplicatePrimitiveCount >= 4
            || fragmentEvidence.GapRatio >= 0.02
            || fragmentEvidence.TotalHealedGap >= Math.Max(2.0, wall.Thickness * 0.35);
    }

    private static bool IsWallContaminatingObjectLinework(ObjectCandidate candidate) =>
        candidate.Kind == ObjectCandidateKind.Stair
        || candidate.Category == ObjectCategory.Stair
        || candidate.Evidence.Any(item =>
            item.Contains("nearby text", StringComparison.OrdinalIgnoreCase)
            && item.Contains("trapp", StringComparison.OrdinalIgnoreCase));

    private static bool IsWallContaminatingDetailLinework(ObjectCandidate candidate) =>
        IsWallContaminatingObjectLinework(candidate)
        || (candidate.SourceKind == ObjectCandidateSourceKind.WallComponentIsland
            && candidate.SourceWallComponentKind is WallGraphComponentKind.ObjectLikeIsland or WallGraphComponentKind.IsolatedFragment)
        || (candidate.SourceKind == ObjectCandidateSourceKind.CompositeLinework
            && candidate.Kind == ObjectCandidateKind.Symbol
            && candidate.Category == ObjectCategory.GenericSymbol);

    private static bool IsHeavyMergedOrCollapsedFaceEvidence(string evidence)
    {
        var value = TryReadEvidenceCount(evidence, "face merged ")
            ?? TryReadEvidenceCount(evidence, "face collapsed ");
        return value >= 18;
    }

    private static int? TryReadEvidenceCount(string evidence, string marker)
    {
        var markerIndex = evidence.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var start = markerIndex + marker.Length;
        var end = start;
        while (end < evidence.Length && char.IsDigit(evidence[end]))
        {
            end++;
        }

        return end > start && int.TryParse(evidence[start..end], out var value)
            ? value
            : null;
    }

    private static bool LineOverlapsCandidateGuardZone(
        PlanLineSegment line,
        PlanRect guardZone,
        double minimumOverlapLength,
        double minimumOverlapRatio)
    {
        if (guardZone.IsEmpty)
        {
            return false;
        }

        if (line.IsVertical())
        {
            var x = (line.Start.X + line.End.X) / 2.0;
            if (x < guardZone.Left || x > guardZone.Right)
            {
                return false;
            }

            var lineMin = Math.Min(line.Start.Y, line.End.Y);
            var lineMax = Math.Max(line.Start.Y, line.End.Y);
            return HasAxisOverlap(
                lineMin,
                lineMax,
                guardZone.Top,
                guardZone.Bottom,
                line.Length,
                minimumOverlapLength,
                minimumOverlapRatio);
        }

        if (line.IsHorizontal())
        {
            var y = (line.Start.Y + line.End.Y) / 2.0;
            if (y < guardZone.Top || y > guardZone.Bottom)
            {
                return false;
            }

            var lineMin = Math.Min(line.Start.X, line.End.X);
            var lineMax = Math.Max(line.Start.X, line.End.X);
            return HasAxisOverlap(
                lineMin,
                lineMax,
                guardZone.Left,
                guardZone.Right,
                line.Length,
                minimumOverlapLength,
                minimumOverlapRatio);
        }

        return false;
    }

    private static bool HasAxisOverlap(
        double lineMin,
        double lineMax,
        double zoneMin,
        double zoneMax,
        double lineLength,
        double minimumOverlapLength,
        double minimumOverlapRatio)
    {
        var overlap = Math.Min(lineMax, zoneMax) - Math.Max(lineMin, zoneMin);
        if (overlap <= 0)
        {
            return false;
        }

        return overlap >= minimumOverlapLength
            && overlap / Math.Max(lineLength, 0.001) >= minimumOverlapRatio;
    }

    private static bool SecondaryStructuralComponentHasTrustedPairedWallBodySupport(
        WallSegment currentWall,
        WallGraphComponent? component,
        IReadOnlyDictionary<string, WallSegment> wallById,
        IReadOnlyDictionary<string, WallEvidenceWallAssessment> wallEvidenceByWallId)
    {
        if (component?.Kind != WallGraphComponentKind.SecondaryStructural
            || component.ExcludedFromStructuralTopology
            || component.WallIds.Count < 1
            || component.WallIds.Count > 4
            || component.Confidence.Value < 0.6)
        {
            return false;
        }

        if (component.WallIds.Contains(currentWall.Id, StringComparer.Ordinal)
            && wallEvidenceByWallId.TryGetValue(currentWall.Id, out var currentAssessment)
            && currentAssessment.PlacementReady
            && !currentAssessment.RequiresReview
            && !currentAssessment.RejectedAsNoise
            && currentAssessment.Category == WallEvidenceCategory.StrongWallBody
            && HasStrongPairedWallBodyEvidence(currentWall, currentAssessment)
            && LooksLikeTrustedLongSecondaryInteriorWallBody(component, currentWall, currentAssessment))
        {
            return true;
        }

        var walls = component.WallIds
            .Select(wallId => wallById.TryGetValue(wallId, out var wall) ? wall : null)
            .OfType<WallSegment>()
            .ToArray();
        if (walls.Length != component.WallIds.Count
            || walls.Any(wall => wall.Confidence.Value < 0.74
                || wall.DetectionKind != WallDetectionKind.ParallelLinePair))
        {
            return false;
        }

        if (!walls.All(wall =>
            wallEvidenceByWallId.TryGetValue(wall.Id, out var assessment)
            && assessment.PlacementReady
            && !assessment.RequiresReview
            && !assessment.RejectedAsNoise
            && assessment.Category == WallEvidenceCategory.StrongWallBody
            && HasStrongPairedWallBodyEvidence(wall, assessment)))
        {
            return false;
        }

        var assessments = walls
            .Select(wall => wallEvidenceByWallId[wall.Id])
            .ToArray();

        if (component.WallIds.Count == 1)
        {
            return LooksLikeTrustedAnchoredSinglePairedWallBody(component, walls[0], assessments[0])
                || LooksLikeTrustedLongSecondaryInteriorWallBody(component, walls[0], assessments[0]);
        }

        return LooksLikeTrustedLongThinPairedWallBodyChain(component)
            || LooksLikeTrustedCompactPairedReturn(component, walls, assessments);
    }

    private static bool LooksLikeTrustedLongThinPairedWallBodyChain(WallGraphComponent component) =>
        component.DrawingLength >= 150
        && IsLongThinComponent(component.Bounds);

    private static bool LooksLikeTrustedAnchoredSinglePairedWallBody(
        WallGraphComponent component,
        WallSegment wall,
        WallEvidenceWallAssessment assessment) =>
        component.DrawingLength >= 72
        && wall.DrawingLength >= 72
        && wall.DetectionKind == WallDetectionKind.ParallelLinePair
        && assessment.Category == WallEvidenceCategory.StrongWallBody
        && wall.PairEvidence is { } pair
        && pair.Score >= MinTrustedAnchoredSingleSecondaryWallPairScore
        && pair.OverlapRatio >= MinTrustedAnchoredSingleSecondaryWallOverlapRatio
        && pair.FaceSeparation >= MinTrustedAnchoredSingleSecondaryWallFaceSeparationDrawingUnits
        && pair.FaceSeparation <= MaxTrustedAnchoredSingleSecondaryWallFaceSeparationDrawingUnits
        && Math.Max(pair.FirstFaceFragmentCount, pair.SecondFaceFragmentCount)
            <= MaxTrustedAnchoredSingleSecondaryWallFaceFragments
        && component.Evidence.Any(item =>
            item.Contains("anchored single paired-wall body", StringComparison.OrdinalIgnoreCase))
        && IsThinComponent(component.Bounds, minimumLongSide: 72, maxShortSide: 18, minimumAspectRatio: 3);

    private static bool LooksLikeTrustedLongSecondaryInteriorWallBody(
        WallGraphComponent component,
        WallSegment wall,
        WallEvidenceWallAssessment assessment)
    {
        if (wall.WallType != WallType.Interior
            || wall.DetectionKind != WallDetectionKind.ParallelLinePair
            || wall.DrawingLength < MinTrustedSecondaryInteriorWallLengthDrawingUnits
            || component.DrawingLength < MinTrustedSecondaryInteriorWallLengthDrawingUnits
            || wall.Confidence.Value < 0.82
            || assessment.Confidence.Value < 0.82
            || wall.PairEvidence is not { } pair
            || !IsThinComponent(wall.Bounds, minimumLongSide: 120, maxShortSide: 24, minimumAspectRatio: 5))
        {
            return false;
        }

        if (pair.Score < MinTrustedSecondaryInteriorWallPairScore
            || pair.OverlapRatio < MinTrustedSecondaryInteriorWallOverlapRatio
            || pair.FaceSeparation < MinTrustedSecondaryInteriorWallFaceSeparationDrawingUnits
            || pair.FaceSeparation > MaxTrustedSecondaryInteriorWallFaceSeparationDrawingUnits
            || MaxFaceFragmentCount(wall, assessment) > MaxTrustedSecondaryInteriorWallFaceFragments)
        {
            return false;
        }

        var evidence = WallEvidenceFor(wall, assessment)
            .Concat(component.Evidence)
            .ToArray();
        if (!EvidenceContains(evidence, "supported wall evidence inside exterior envelope"))
        {
            return false;
        }

        return !EvidenceContainsAny(
            evidence,
            "outdoor covered-area boundary",
            "unpaired outdoor covered-area boundary",
            "covered-area boundary",
            "outdoor/terrace room evidence alone",
            "terrace",
            "covered entry",
            "covered-entry",
            "overbygd",
            "canopy",
            "railing",
            "trim/detail",
            "trim linework",
            "glazing",
            "detail linework",
            "surface pattern",
            "object/fixture",
            "fixture detail",
            "stair",
            "tiny door-adjacent placement topology piece suppressed",
            "not trusted",
            "without shell support",
            "alone is not trusted");
    }

    private static bool LooksLikeTrustedCompactPairedReturn(
        WallGraphComponent component,
        IReadOnlyList<WallSegment> walls,
        IReadOnlyList<WallEvidenceWallAssessment> assessments)
    {
        if (component.WallIds.Count is < 2 or > 3
            || component.DrawingLength < 96
            || !walls.All(wall => wall.CenterLine.IsHorizontal() || wall.CenterLine.IsVertical())
            || !walls.Any(wall => wall.CenterLine.IsHorizontal())
            || !walls.Any(wall => wall.CenterLine.IsVertical())
            || !assessments.Any(HasStructuralEndpointSupportEvidence))
        {
            return false;
        }

        var pairScores = walls
            .SelectMany(wall => wall.Evidence)
            .Concat(assessments.SelectMany(assessment => assessment.Evidence))
            .Select(TryReadPairScore)
            .Where(score => score.HasValue)
            .Select(score => score!.Value)
            .ToArray();

        return pairScores.Length == 0
            || (pairScores.Any(score => score >= 0.68)
                && pairScores.All(score => score >= 0.60));
    }

    private static bool IsLongThinComponent(PlanRect bounds)
        => IsThinComponent(bounds, minimumLongSide: 120, maxShortSide: 12, minimumAspectRatio: 10);

    private static bool IsThinComponent(
        PlanRect bounds,
        double minimumLongSide,
        double maxShortSide,
        double minimumAspectRatio)
    {
        var shortSide = Math.Min(bounds.Width, bounds.Height);
        var longSide = Math.Max(bounds.Width, bounds.Height);
        return longSide >= minimumLongSide
            && shortSide <= maxShortSide
            && longSide / Math.Max(shortSide, 0.001) >= minimumAspectRatio;
    }

    private static bool HasStrongPairedWallBodyEvidence(
        WallSegment wall,
        WallEvidenceWallAssessment assessment)
    {
        var evidence = wall.Evidence.Concat(assessment.Evidence).ToArray();
        return evidence.Any(item => item.Contains("parallel wall-face pair", StringComparison.OrdinalIgnoreCase))
            && evidence.Any(item => item.Contains("strong double-edge wall body", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasStructuralEndpointSupportEvidence(WallEvidenceWallAssessment assessment) =>
        assessment.Evidence
            .Concat(assessment.ScoreBreakdown.PositiveEvidence)
            .Any(item =>
                item.Contains("endpoint supported by structural context", StringComparison.OrdinalIgnoreCase)
                || item.Contains("endpoints supported by structural context", StringComparison.OrdinalIgnoreCase)
                || item.Contains("structural graph support", StringComparison.OrdinalIgnoreCase));

    private static double? TryReadPairScore(string evidence)
    {
        const string Prefix = "pair score ";
        var index = evidence.IndexOf(Prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var start = index + Prefix.Length;
        var end = start;
        while (end < evidence.Length
            && (char.IsDigit(evidence[end])
                || evidence[end] == '.'
                || evidence[end] == ','))
        {
            end++;
        }

        var valueText = evidence[start..end].Replace(',', '.');
        return double.TryParse(
            valueText,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    private static double? TryReadOverlapRatio(string evidence)
    {
        const string Prefix = "overlap ratio ";
        var index = evidence.IndexOf(Prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        return TryReadEvidenceDouble(evidence, index + Prefix.Length);
    }

    private static double? TryReadFaceSeparation(string evidence)
    {
        const string Prefix = "face separation ";
        var index = evidence.IndexOf(Prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        return TryReadEvidenceDouble(evidence, index + Prefix.Length);
    }

    private static double? TryReadEvidenceDouble(string evidence, int start)
    {
        var end = start;
        while (end < evidence.Length
            && (char.IsDigit(evidence[end])
                || evidence[end] == '.'
                || evidence[end] == ','))
        {
            end++;
        }

        if (end <= start)
        {
            return null;
        }

        var valueText = evidence[start..end].Replace(',', '.');
        return double.TryParse(
            valueText,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    private static bool EvidenceContainsAny(
        IReadOnlyList<string> evidence,
        params string[] fragments) =>
        fragments.Any(fragment => EvidenceContains(evidence, fragment));

    private static bool EvidenceContains(
        IReadOnlyList<string> evidence,
        string fragment) =>
        evidence.Any(item => item.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, WallGraphComponent> BuildComponentByWallId(
        IReadOnlyList<WallGraphComponent> components)
    {
        var result = new Dictionary<string, WallGraphComponent>(StringComparer.Ordinal);
        foreach (var component in components)
        {
            foreach (var wallId in component.WallIds)
            {
                if (!string.IsNullOrWhiteSpace(wallId))
                {
                    result[wallId] = component;
                }
            }
        }

        return result;
    }

    private static void AddReason(
        Dictionary<string, List<string>> reasonsByWallId,
        string wallId,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(wallId))
        {
            return;
        }

        if (!reasonsByWallId.TryGetValue(wallId, out var reasons))
        {
            reasons = new List<string>();
            reasonsByWallId[wallId] = reasons;
        }

        reasons.Add(reason);
    }
}
