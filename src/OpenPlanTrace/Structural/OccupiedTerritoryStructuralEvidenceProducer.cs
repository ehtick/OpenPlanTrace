namespace OpenPlanTrace;

internal sealed class OccupiedTerritoryStructuralEvidenceProducer : IStructuralEvidenceProducer
{
    private static readonly double[] SideSampleParameters = [0.15, 0.35, 0.50, 0.65, 0.85];

    public string Name => "occupied-territory-evidence";

    public void Produce(StructuralEvidenceBuildContext context)
    {
        var roomContexts =
            RoomBoundaryStructuralEvidenceProducer.ClassifyRoomContexts(context.Source.Rooms);
        var trustedRoomsByPage = context.Source.Rooms
            .Where(room =>
                roomContexts.GetValueOrDefault(
                    room.Id,
                    StructuralRoomLoopContext.Unknown)
                == StructuralRoomLoopContext.Indoor)
            .GroupBy(room => room.PageNumber)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var mainRegionsByPage = context.Source.SheetRegions
            .Where(region => region.Kind == RegionKind.MainFloorPlan)
            .GroupBy(region => region.PageNumber)
            .ToDictionary(
                group => group.Key,
                group => PlanRect.Union(group.Select(region => region.Bounds)));
        var trustedShellAnchorsByPage = context.Candidates.Drafts
            .Where(candidate =>
                trustedRoomsByPage.TryGetValue(candidate.PageNumber, out var rooms)
                && candidate.WallType == WallType.Exterior
                && !candidate.HasBlockingSemanticEvidence
                && (HasFilledOrSourceBackedWallBody(candidate)
                    || CountOccupiedSideSamples(
                        candidate,
                        rooms,
                        context.Options) >= 3))
            .GroupBy(candidate => candidate.PageNumber)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var candidate in context.Candidates.Drafts)
        {
            if (!trustedRoomsByPage.TryGetValue(candidate.PageNumber, out var rooms)
                || rooms.Length == 0
                || candidate.SourceOpeningIds.Count > 0
                || candidate.Origins.HasFlag(StructuralCandidateOrigin.OpeningHost)
                || HasFilledOrSourceBackedWallBody(candidate)
                || !LooksLikeExteriorShellExtension(candidate))
            {
                continue;
            }

            var maximumWhiskerLength = mainRegionsByPage.TryGetValue(
                candidate.PageNumber,
                out var mainRegion)
                ? Math.Max(
                    context.Options.MinimumCandidateLength * 4,
                    Math.Min(mainRegion.Width, mainRegion.Height) * 0.22)
                : Math.Max(context.Options.MinimumCandidateLength * 4, 160);
            if (candidate.DrawingLength > maximumWhiskerLength)
            {
                continue;
            }

            var anchorTolerance = Math.Max(
                context.Options.EndpointTolerance * 2.5,
                Math.Max(candidate.Thickness * 2.0, context.Options.AxisTolerance * 3.0));
            var startDistance = DistanceToOccupiedTerritory(candidate.CenterLine.Start, rooms);
            var endDistance = DistanceToOccupiedTerritory(candidate.CenterLine.End, rooms);
            var occupiedTerritoryNearDistance = Math.Min(startDistance, endDistance);
            var shellAnchorDistance = trustedShellAnchorsByPage.TryGetValue(
                candidate.PageNumber,
                out var anchors)
                ? DistanceToShellAnchor(candidate, anchors)
                : double.PositiveInfinity;
            var nearDistance = Math.Min(
                occupiedTerritoryNearDistance,
                shellAnchorDistance);
            var farDistance = Math.Max(startDistance, endDistance);
            var minimumFarDistance = Math.Max(
                anchorTolerance * 2.0,
                candidate.DrawingLength * 0.35);
            if (nearDistance > anchorTolerance || farDistance < minimumFarDistance)
            {
                continue;
            }

            var occupiedSideSampleCount = CountOccupiedSideSamples(
                candidate,
                rooms,
                context.Options);
            if (occupiedSideSampleCount > 1)
            {
                continue;
            }

            candidate.AddSignal(
                new StructuralEvidenceSignal(
                    $"signal:{candidate.Id}:outward-shell-whisker",
                    StructuralEvidenceSignalKind.UnoccupiedShellExtension,
                    -1.35,
                    string.Join(",", rooms.Select(room => room.Id).Order(StringComparer.Ordinal)),
                    $"one-ended exterior-shell extension leaves trusted occupied territory; room-side support {occupiedSideSampleCount}/{SideSampleParameters.Length}, occupied-territory distance {occupiedTerritoryNearDistance:0.###}, shell-anchor distance {shellAnchorDistance:0.###}, far endpoint distance {farDistance:0.###}",
                    candidate.SourcePrimitiveIds.Order(StringComparer.Ordinal).ToArray()));
        }
    }

    private static double DistanceToShellAnchor(
        StructuralCandidateRegistry.CandidateDraft candidate,
        IReadOnlyList<StructuralCandidateRegistry.CandidateDraft> anchors)
    {
        var distance = double.PositiveInfinity;
        foreach (var anchor in anchors.Where(anchor =>
                     !string.Equals(anchor.Id, candidate.Id, StringComparison.Ordinal)))
        {
            distance = Math.Min(
                distance,
                anchor.CenterLine.DistanceToPoint(candidate.CenterLine.Start));
            distance = Math.Min(
                distance,
                anchor.CenterLine.DistanceToPoint(candidate.CenterLine.End));
        }

        return distance;
    }

    private static bool HasFilledOrSourceBackedWallBody(
        StructuralCandidateRegistry.CandidateDraft candidate) =>
        candidate.Evidence.Any(item =>
            item.Contains(
                "filled closed vector wall body",
                StringComparison.OrdinalIgnoreCase)
            || item.Contains(
                "filled wall-solid primitive",
                StringComparison.OrdinalIgnoreCase)
            || item.Contains(
                "source-backed exterior shell",
                StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeExteriorShellExtension(
        StructuralCandidateRegistry.CandidateDraft candidate) =>
        candidate.WallType == WallType.Exterior
        || candidate.Origins.HasFlag(StructuralCandidateOrigin.RecoveredWall)
        || candidate.Evidence.Any(item =>
            item.Contains(
                "exterior shell repair",
                StringComparison.OrdinalIgnoreCase)
            || item.Contains(
                "near detected floorplan/wall envelope",
                StringComparison.OrdinalIgnoreCase)
            || item.Contains(
                "near detected floorplan envelope",
                StringComparison.OrdinalIgnoreCase));

    private static int CountOccupiedSideSamples(
        StructuralCandidateRegistry.CandidateDraft candidate,
        IReadOnlyList<RoomRegion> rooms,
        StructuralSolverOptions options)
    {
        var normal = StructuralGeometry.UnitNormal(candidate.CenterLine);
        var sampleOffset = Math.Max(
            candidate.Thickness * 1.5,
            Math.Max(options.AxisTolerance * 2.0, options.EndpointTolerance));
        var count = 0;
        foreach (var parameter in SideSampleParameters)
        {
            var point = candidate.CenterLine.PointAt(parameter);
            if (IsInsideOccupiedTerritory(point + (normal * sampleOffset), rooms)
                || IsInsideOccupiedTerritory(point + (normal * -sampleOffset), rooms))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsInsideOccupiedTerritory(
        PlanPoint point,
        IReadOnlyList<RoomRegion> rooms) =>
        rooms.Any(room =>
            room.Bounds.Contains(point, 1e-6)
            && (room.Boundary.Count < 3 || IsPointInPolygon(point, room.Boundary)));

    private static double DistanceToOccupiedTerritory(
        PlanPoint point,
        IReadOnlyList<RoomRegion> rooms) =>
        rooms.Min(room => DistanceToRoom(point, room));

    private static double DistanceToRoom(PlanPoint point, RoomRegion room)
    {
        if (room.Bounds.Contains(point, 1e-6)
            && (room.Boundary.Count < 3 || IsPointInPolygon(point, room.Boundary)))
        {
            return 0;
        }

        if (room.Boundary.Count < 2)
        {
            var dx = Math.Max(0, Math.Max(room.Bounds.Left - point.X, point.X - room.Bounds.Right));
            var dy = Math.Max(0, Math.Max(room.Bounds.Top - point.Y, point.Y - room.Bounds.Bottom));
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        var distance = double.PositiveInfinity;
        for (var index = 0; index < room.Boundary.Count; index++)
        {
            var edge = new PlanLineSegment(
                room.Boundary[index],
                room.Boundary[(index + 1) % room.Boundary.Count]);
            distance = Math.Min(distance, edge.DistanceToPoint(point));
        }

        return distance;
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
            var currentPoint = polygon[index];
            var previousPoint = polygon[previous];
            var crossesY =
                currentPoint.Y > point.Y != previousPoint.Y > point.Y;
            if (!crossesY)
            {
                continue;
            }

            var intersectionX =
                ((previousPoint.X - currentPoint.X) * (point.Y - currentPoint.Y)
                    / (previousPoint.Y - currentPoint.Y))
                + currentPoint.X;
            if (point.X < intersectionX)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
