namespace OpenPlanTrace;

internal static class ExteriorWallSupportEvidence
{
    public static bool HasTrustedSupport(
        WallSegment wall,
        WallEvidenceWallAssessment? assessment,
        IEnumerable<string> evidence)
    {
        if (wall.Id.Contains(
                "wall-exterior-shell-source-backed:",
                StringComparison.Ordinal)
            || assessment?.ScoreBreakdown.LayerSupportScore >= 0.20)
        {
            return true;
        }

        return HasExplicitTrustedSupport(evidence);
    }

    public static bool HasExplicitTrustedSupport(IEnumerable<string> evidence) =>
        evidence.Any(IsExplicitTrustedSupport);

    public static bool IsExplicitTrustedSupport(string evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence)
            || evidence.Contains("not trusted", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("without shell support", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("alone is not", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return evidence.Contains("source-backed exterior shell", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("exterior shell continuity", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("trusted exterior shell", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("trusted long exterior shell", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("trusted long isolated exterior shell", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("trusted long main exterior shell", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("global exterior-shell repair matched a trusted shell", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("recovered wall body aligned to main floorplan perimeter shell", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("wall-like layer exterior shell support", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("wall or structural source layer", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("filled wall-solid primitive", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("filled closed vector wall body", StringComparison.OrdinalIgnoreCase)
            || evidence.Contains("trusted benchmark", StringComparison.OrdinalIgnoreCase);
    }
}
