namespace OpenPlanTrace;

internal sealed record StructuralOpeningSupportAssessment(
    bool HasTrustedRoomContext,
    bool HasAmbiguousRoomTopology,
    int ReportedRoomTerritoryCount,
    string Evidence);

internal static class StructuralOpeningSupport
{
    private const int MaximumPhysicalRoomTerritories = 2;

    public static StructuralOpeningSupportAssessment Assess(
        OpeningCandidate opening,
        IReadOnlySet<string> trustedRoomIds)
    {
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(trustedRoomIds);

        var trustedLinks = opening.ConnectedRoomLinks
            .Where(link => trustedRoomIds.Contains(link.RoomId))
            .ToArray();
        var reportedTerritoryIds = opening.ConnectedRoomIds
            .Concat(opening.ConnectedRoomLinks.Select(link => link.RoomId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (reportedTerritoryIds.Length > MaximumPhysicalRoomTerritories)
        {
            return new StructuralOpeningSupportAssessment(
                HasTrustedRoomContext: false,
                HasAmbiguousRoomTopology: true,
                reportedTerritoryIds.Length,
                $"opening {opening.Id} reports {reportedTerritoryIds.Length} room territories; a physical opening can support at most two opposing territories");
        }

        var hasTrustedRoomContext = trustedLinks.Length > 0
            ? trustedLinks.Any(link =>
                link.SharesHostWall
                || link.Confidence.Value >= 0.65)
            : opening.ConnectedRoomIds.Any(trustedRoomIds.Contains)
                && (opening.RoomAdjacencyIds.Count > 0
                    || opening.Confidence.Value >= 0.70);

        return new StructuralOpeningSupportAssessment(
            hasTrustedRoomContext,
            HasAmbiguousRoomTopology: false,
            reportedTerritoryIds.Length,
            hasTrustedRoomContext
                ? $"opening {opening.Id} has structurally plausible room context across {reportedTerritoryIds.Length} reported room territory or territories"
                : $"opening {opening.Id} has no trusted room context for structural anchoring");
    }
}
