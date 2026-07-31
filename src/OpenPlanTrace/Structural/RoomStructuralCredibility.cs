namespace OpenPlanTrace;

internal static class RoomStructuralCredibility
{
    internal const double MaximumNestedFixtureAreaSquareMeters = 1.5;

    private const double MinimumParentDrawingAreaRatio = 4.0;
    private const double MinimumEmbeddedParentDrawingAreaRatio = 3.0;
    private const double MinimumBoundsContainmentRatio = 0.92;
    private const double MinimumEmbeddedSharedWallRatio = 0.75;

    public static IReadOnlySet<string> FindNestedFixtureLikeRoomIds(
        IReadOnlyList<RoomRegion> rooms)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var room in rooms)
        {
            if (IsNestedFixtureLikeRoom(room, rooms))
            {
                result.Add(room.Id);
            }
        }

        foreach (var roomId in FindEmbeddedCutoutLikeRoomIds(rooms))
        {
            result.Add(roomId);
        }

        return result;
    }

    public static IReadOnlySet<string> FindEmbeddedCutoutLikeRoomIds(
        IReadOnlyList<RoomRegion> rooms)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var room in rooms)
        {
            if (IsEmbeddedCutoutLikeRoom(room, rooms))
            {
                result.Add(room.Id);
            }
        }

        return result;
    }

    public static bool IsNestedFixtureLikeRoom(
        RoomRegion room,
        IReadOnlyList<RoomRegion> rooms)
    {
        if (room.UseKind != RoomUseKind.Unknown
            || room.Boundary.Count < 4
            || room.Bounds.IsEmpty
            || room.DrawingArea <= 0
            || HasSemanticIdentity(room))
        {
            return false;
        }

        foreach (var parent in rooms)
        {
            if (string.Equals(parent.Id, room.Id, StringComparison.Ordinal)
                || parent.PageNumber != room.PageNumber
                || parent.UseKind == RoomUseKind.Outdoor
                || parent.Boundary.Count < 3
                || parent.Bounds.IsEmpty
                || parent.DrawingArea < room.DrawingArea * MinimumParentDrawingAreaRatio
                || !ContainsRoom(parent, room))
            {
                continue;
            }

            var childAreaSquareMeters = EstimateChildAreaSquareMeters(room, parent);
            if (childAreaSquareMeters is not > 0
                || childAreaSquareMeters > MaximumNestedFixtureAreaSquareMeters)
            {
                continue;
            }

            var parentAreaSquareMeters = parent.AreaSquareMeters;
            if (parentAreaSquareMeters is > 0
                && parentAreaSquareMeters.Value
                    < childAreaSquareMeters.Value * MinimumParentDrawingAreaRatio)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsEmbeddedCutoutLikeRoom(
        RoomRegion room,
        IReadOnlyList<RoomRegion> rooms)
    {
        if (room.UseKind != RoomUseKind.Unknown
            || room.Boundary.Count < 4
            || room.WallIds.Count < 2
            || room.Bounds.IsEmpty
            || room.DrawingArea <= 0
            || HasSemanticIdentity(room))
        {
            return false;
        }

        foreach (var parent in rooms)
        {
            if (string.Equals(parent.Id, room.Id, StringComparison.Ordinal)
                || parent.PageNumber != room.PageNumber
                || parent.UseKind == RoomUseKind.Outdoor
                || parent.Boundary.Count < 3
                || parent.Bounds.IsEmpty
                || parent.DrawingArea < room.DrawingArea * MinimumEmbeddedParentDrawingAreaRatio
                || ContainsRoom(parent, room))
            {
                continue;
            }

            var boundsContainment = parent.Bounds.OverlapArea(room.Bounds)
                / Math.Max(room.Bounds.Area, 0.001);
            if (boundsContainment < MinimumBoundsContainmentRatio)
            {
                continue;
            }

            var sharedWallCount = room.WallIds.Count(id =>
                parent.WallIds.Contains(id, StringComparer.Ordinal));
            var sharedWallRatio = sharedWallCount
                / (double)Math.Max(1, room.WallIds.Count);
            if (sharedWallCount < 2
                || sharedWallRatio < MinimumEmbeddedSharedWallRatio)
            {
                continue;
            }

            var childAreaSquareMeters = EstimateChildAreaSquareMeters(
                room,
                parent);
            if (childAreaSquareMeters is not > 0
                || childAreaSquareMeters > MaximumNestedFixtureAreaSquareMeters)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    public static bool HasSemanticIdentity(RoomRegion room)
    {
        if (room.UseKind != RoomUseKind.Unknown)
        {
            return true;
        }

        if (room.Evidence.Any(item =>
                item.Contains(
                    "semantic room seed from label",
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(room.Label)
            && room.Label.Count(char.IsLetter) >= 2;
    }

    private static double? EstimateChildAreaSquareMeters(
        RoomRegion room,
        RoomRegion parent)
    {
        if (room.AreaSquareMeters is > 0)
        {
            return room.AreaSquareMeters;
        }

        if (parent.AreaSquareMeters is not > 0 || parent.DrawingArea <= 0)
        {
            return null;
        }

        return parent.AreaSquareMeters.Value
            * room.DrawingArea
            / parent.DrawingArea;
    }

    private static bool ContainsRoom(RoomRegion parent, RoomRegion child)
    {
        var containment = parent.Bounds.OverlapArea(child.Bounds)
            / Math.Max(child.Bounds.Area, 0.001);
        if (containment < MinimumBoundsContainmentRatio)
        {
            return false;
        }

        return parent.Boundary.Count < 3
            || IsPointInPolygon(child.Bounds.Center, parent.Boundary);
    }

    private static bool IsPointInPolygon(
        PlanPoint point,
        IReadOnlyList<PlanPoint> polygon)
    {
        var inside = false;
        for (int index = 0, previous = polygon.Count - 1;
             index < polygon.Count;
             previous = index++)
        {
            var current = polygon[index];
            var prior = polygon[previous];
            if ((current.Y > point.Y) == (prior.Y > point.Y))
            {
                continue;
            }

            var intersectionX =
                ((prior.X - current.X) * (point.Y - current.Y)
                    / (prior.Y - current.Y))
                + current.X;
            if (point.X < intersectionX)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
