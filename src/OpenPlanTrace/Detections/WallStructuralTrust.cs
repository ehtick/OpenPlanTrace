namespace OpenPlanTrace;

public static class WallStructuralTrust
{
    public static bool IsRejectedNonStructural(WallEvidenceWallAssessment? evidenceAssessment) =>
        evidenceAssessment?.RejectedAsNoise == true
        || evidenceAssessment?.Decision == WallEvidenceDecision.Reject;

    public static bool IsExcludedFromStructuralTopology(
        WallGraphComponent? component,
        WallEvidenceWallAssessment? evidenceAssessment) =>
        component?.ExcludedFromStructuralTopology == true
            && !HasProtectedObjectLikeExteriorShellPairEvidence(evidenceAssessment)
        || IsRejectedNonStructural(evidenceAssessment);

    private static bool HasProtectedObjectLikeExteriorShellPairEvidence(
        WallEvidenceWallAssessment? evidenceAssessment) =>
        evidenceAssessment?.RejectedAsNoise != true
        && evidenceAssessment?.Decision != WallEvidenceDecision.Reject
        && evidenceAssessment?.Evidence.Any(item =>
            item.Contains(
                WallPlacementContextGuards.TrustedObjectLikeExteriorShellPairEvidence,
                StringComparison.OrdinalIgnoreCase)) == true;
}
