using System.Globalization;

namespace OpenPlanTrace;

internal static class RoomBoundaryReliability
{
    private const double MinReviewSupportedTrustedWallRatio = 0.45;
    private const int MinReviewSupportedTrustedSideCount = 2;
    private const double MinExteriorShellAreaMatchRatio = 0.3;
    private const double MaxExteriorShellAreaMatchRatio = 3.0;

    public static bool HasReliableBoundaryEvidence(RoomRegion room)
    {
        if (IsWeakReviewSupportedSemanticBoundary(room))
        {
            return false;
        }

        return room.WallIds.Count >= 2
            || room.Evidence.Any(item =>
                item.Contains("closed orthogonal cycle", StringComparison.OrdinalIgnoreCase)
                || item.Contains("bounded by nearby orthogonal wall evidence", StringComparison.OrdinalIgnoreCase)
                || item.Contains("semantic room boundary inferred from nearby walls", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsWeakReviewSupportedSemanticBoundary(RoomRegion room)
    {
        if (!room.Evidence.Any(item =>
            item.Contains("review-supported semantic room boundary", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return !TryParseTrustedSupport(room.Evidence, out var trustedWallRatio, out var trustedSideCount)
            || trustedWallRatio < MinReviewSupportedTrustedWallRatio
            || trustedSideCount < MinReviewSupportedTrustedSideCount;
    }

    public static bool HasReliableExteriorShellBoundaryEvidence(RoomRegion room)
    {
        if (!HasReliableBoundaryEvidence(room))
        {
            return false;
        }

        return !TryParseAreaMatchRatio(room.Evidence, out var areaMatchRatio)
            || areaMatchRatio is >= MinExteriorShellAreaMatchRatio and <= MaxExteriorShellAreaMatchRatio;
    }

    private static bool TryParseTrustedSupport(
        IReadOnlyList<string> evidence,
        out double trustedWallRatio,
        out int trustedSideCount)
    {
        trustedWallRatio = 0;
        trustedSideCount = 0;
        foreach (var item in evidence)
        {
            const string prefix = "semantic room boundary trusted wall support ";
            const string marker = " across ";

            var prefixIndex = item.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (prefixIndex < 0)
            {
                continue;
            }

            var valueStart = prefixIndex + prefix.Length;
            var markerIndex = item.IndexOf(marker, valueStart, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                continue;
            }

            var ratioText = item[valueStart..markerIndex].Trim();
            var sideTextStart = markerIndex + marker.Length;
            var sideTextEnd = item.IndexOf(" side", sideTextStart, StringComparison.OrdinalIgnoreCase);
            if (sideTextEnd < 0)
            {
                sideTextEnd = item.Length;
            }

            var sideText = item[sideTextStart..sideTextEnd].Trim();
            if (double.TryParse(ratioText, NumberStyles.Float, CultureInfo.InvariantCulture, out trustedWallRatio)
                && int.TryParse(sideText, NumberStyles.Integer, CultureInfo.InvariantCulture, out trustedSideCount))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseAreaMatchRatio(
        IReadOnlyList<string> evidence,
        out double areaMatchRatio)
    {
        areaMatchRatio = 0;
        foreach (var item in evidence)
        {
            const string prefix = "semantic room boundary area match ratio ";
            const string suffix = " from printed room area";

            var prefixIndex = item.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (prefixIndex < 0)
            {
                continue;
            }

            var valueStart = prefixIndex + prefix.Length;
            var valueEnd = item.IndexOf(suffix, valueStart, StringComparison.OrdinalIgnoreCase);
            if (valueEnd < 0)
            {
                valueEnd = item.Length;
            }

            if (double.TryParse(
                    item[valueStart..valueEnd].Trim(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out areaMatchRatio))
            {
                return true;
            }
        }

        return false;
    }
}
