namespace OpenPlanTrace;

internal sealed class WallTypeRefinementStage : IPipelineStage
{
    private const string StageName = "wall-type-refinement";

    public string Name => StageName;

    public ValueTask ExecuteAsync(ScanContext context, CancellationToken cancellationToken)
    {
        if (context.Walls.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        var roomIdsByWallId = BuildRoomIdsByWallId(context.Rooms);
        var sharedWallIds = BuildSharedWallIds(context.RoomAdjacencyGraph);
        var componentsByWallId = BuildComponentsByWallId(context.WallGraph);
        var evidenceByWallId = BuildEvidenceByWallId(context.WallEvidenceMap);
        var rejectedEvidenceByWallId = BuildRejectedEvidenceByWallId(context.WallEvidenceMap);
        var roomsById = context.Rooms
            .Where(room => !string.IsNullOrWhiteSpace(room.Id))
            .GroupBy(room => room.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var roomsByPage = context.Rooms
            .GroupBy(room => room.PageNumber)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var changed = 0;
        var evidenceUpdated = 0;
        var roomReferenced = 0;
        var twoSidedRoomEvidence = 0;
        var oneSidedRoomEvidence = 0;
        var rejectedEvidenceProtected = 0;
        var roomConfirmedPlacementPromoted = 0;
        var promotedAssessmentsByWallId = new Dictionary<string, WallEvidenceWallAssessment>(StringComparer.Ordinal);

        for (var index = 0; index < context.Walls.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var wall = context.Walls[index];
            var wallRoomIds = roomIdsByWallId.TryGetValue(wall.Id, out var ids)
                ? ids
                : Array.Empty<string>();
            if (wallRoomIds.Length > 0)
            {
                roomReferenced++;
            }

            var sideEvidence = roomsByPage.TryGetValue(wall.PageNumber, out var pageRooms)
                ? AnalyzeRoomSides(wall, pageRooms, context.Options)
                : RoomSideEvidence.Empty;
            if (sideEvidence.PositiveRoomHits > 0 && sideEvidence.NegativeRoomHits > 0)
            {
                twoSidedRoomEvidence++;
            }
            else if (sideEvidence.PositiveRoomHits > 0 || sideEvidence.NegativeRoomHits > 0)
            {
                oneSidedRoomEvidence++;
            }

            var component = componentsByWallId.TryGetValue(wall.Id, out var foundComponent)
                ? foundComponent
                : null;
            var rejectedEvidence = rejectedEvidenceByWallId.TryGetValue(wall.Id, out var foundEvidence)
                ? foundEvidence
                : null;
            if (rejectedEvidence is not null)
            {
                rejectedEvidenceProtected++;
            }

            var hasOutdoorRoomReference = wallRoomIds
                .Select(id => roomsById.TryGetValue(id, out var room) ? room : null)
                .OfType<RoomRegion>()
                .Any(room => room.UseKind == RoomUseKind.Outdoor);
            var refined = RefineWallType(
                wall,
                wallRoomIds.Length,
                sharedWallIds.Contains(wall.Id),
                hasOutdoorRoomReference,
                sideEvidence,
                component,
                rejectedEvidence);

            var evidence = IsActionableEvidence(refined.Evidence)
                ? AppendEvidence(wall.Evidence, refined.Evidence)
                : wall.Evidence;
            var evidenceChanged = evidence.Count != wall.Evidence.Count;
            var updatedWall = wall;

            if (refined.WallType != wall.WallType || evidenceChanged)
            {
                updatedWall = wall with
                {
                    WallType = refined.WallType,
                    Evidence = evidence
                };

                if (refined.WallType != wall.WallType)
                {
                    changed++;
                }

                if (evidenceChanged)
                {
                    evidenceUpdated++;
                }
            }

            if (evidenceByWallId.TryGetValue(wall.Id, out var assessment)
                && TryPromoteRoomConfirmedWallEvidence(
                    updatedWall,
                    assessment,
                    component,
                    wallRoomIds.Length,
                    sharedWallIds.Contains(wall.Id),
                    hasOutdoorRoomReference,
                    sideEvidence,
                    out var promotedAssessment,
                    out var promotionEvidence))
            {
                promotedAssessmentsByWallId[wall.Id] = promotedAssessment;
                updatedWall = updatedWall with
                {
                    Evidence = AppendEvidence(updatedWall.Evidence, promotionEvidence)
                };
                roomConfirmedPlacementPromoted++;
                evidenceUpdated++;
            }

            if (!ReferenceEquals(updatedWall, wall))
            {
                context.Walls[index] = updatedWall;
            }
        }

        if (promotedAssessmentsByWallId.Count > 0)
        {
            context.WallEvidenceMap = context.WallEvidenceMap with
            {
                WallAssessments = context.WallEvidenceMap.WallAssessments
                    .Select(assessment => promotedAssessmentsByWallId.TryGetValue(assessment.WallId, out var promoted)
                        ? promoted
                        : assessment)
                    .ToArray()
            };
        }

        AddDiagnostics(
            context,
            changed,
            evidenceUpdated,
            roomReferenced,
            twoSidedRoomEvidence,
            oneSidedRoomEvidence,
            rejectedEvidenceProtected,
            roomConfirmedPlacementPromoted);
        return ValueTask.CompletedTask;
    }

    private static Dictionary<string, string[]> BuildRoomIdsByWallId(IReadOnlyList<RoomRegion> rooms)
    {
        var builder = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var room in rooms)
        {
            foreach (var wallId in room.WallIds)
            {
                if (!builder.TryGetValue(wallId, out var roomIds))
                {
                    roomIds = new HashSet<string>(StringComparer.Ordinal);
                    builder[wallId] = roomIds;
                }

                roomIds.Add(room.Id);
            }
        }

        return builder.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Order(StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
    }

    private static HashSet<string> BuildSharedWallIds(RoomAdjacencyGraph graph) =>
        graph.Edges
            .SelectMany(edge => edge.SharedWallIds)
            .ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, WallGraphComponent> BuildComponentsByWallId(WallGraph graph)
    {
        var result = new Dictionary<string, WallGraphComponent>(StringComparer.Ordinal);
        foreach (var component in graph.Components)
        {
            foreach (var wallId in component.WallIds)
            {
                result[wallId] = component;
            }
        }

        return result;
    }

    private static Dictionary<string, WallEvidenceWallAssessment> BuildRejectedEvidenceByWallId(WallEvidenceMap evidenceMap) =>
        evidenceMap.WallAssessments
            .Where(assessment => assessment.RejectedAsNoise || assessment.Decision == WallEvidenceDecision.Reject)
            .Where(assessment => !string.IsNullOrWhiteSpace(assessment.WallId))
            .GroupBy(assessment => assessment.WallId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    private static Dictionary<string, WallEvidenceWallAssessment> BuildEvidenceByWallId(WallEvidenceMap evidenceMap) =>
        evidenceMap.WallAssessments
            .Where(assessment => !string.IsNullOrWhiteSpace(assessment.WallId))
            .GroupBy(assessment => assessment.WallId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    private static WallTypeRefinement RefineWallType(
        WallSegment wall,
        int roomReferenceCount,
        bool isSharedByRoomAdjacency,
        bool hasOutdoorRoomReference,
        RoomSideEvidence sideEvidence,
        WallGraphComponent? component,
        WallEvidenceWallAssessment? rejectedEvidence)
    {
        if (rejectedEvidence is not null && IsNonStructuralWallComponent(component))
        {
            return new WallTypeRefinement(
                WallType.Unknown,
                $"wall type refined unknown: wall belongs to non-structural or isolated graph component; Wall Evidence V2 rejected candidate as {rejectedEvidence.Category}");
        }

        if (rejectedEvidence is not null)
        {
            return new WallTypeRefinement(
                WallType.Unknown,
                $"wall type refined unknown: Wall Evidence V2 rejected candidate as {rejectedEvidence.Category}");
        }

        if (IsNonStructuralWallComponent(component))
        {
            return new WallTypeRefinement(
                WallType.Unknown,
                "wall type refined unknown: wall belongs to non-structural or isolated graph component");
        }

        if (wall.FragmentEvidence?.RequiresGeometryReview == true)
        {
            return new WallTypeRefinement(
                WallType.Unknown,
                "wall type refined unknown: fragment-merged wall geometry requires review before exact placement");
        }

        if (isSharedByRoomAdjacency)
        {
            if (hasOutdoorRoomReference)
            {
                return new WallTypeRefinement(
                    WallType.Exterior,
                    "wall type refined exterior: shared by room adjacency that includes outdoor/terrace room evidence");
            }

            if (wall.WallType == WallType.Exterior)
            {
                return new WallTypeRefinement(
                    WallType.Interior,
                    "wall type refined interior: shared by room adjacency boundary overrides exterior envelope/local-boundary guess");
            }

            return new WallTypeRefinement(
                WallType.Interior,
                "wall type refined interior: shared by room adjacency boundary");
        }

        if (sideEvidence.HasRoomsOnBothSides)
        {
            if (sideEvidence.HasOutdoorRoomSide)
            {
                return new WallTypeRefinement(
                    WallType.Exterior,
                    "wall type refined exterior: room evidence on both sides includes outdoor/terrace space");
            }

            if (wall.WallType == WallType.Exterior)
            {
                return new WallTypeRefinement(
                    WallType.Interior,
                    "wall type refined interior: detected room evidence on both sides overrides exterior envelope/local-boundary guess");
            }

            return new WallTypeRefinement(
                WallType.Interior,
                "wall type refined interior: detected room evidence on both sides");
        }

        if (sideEvidence.HasRoomsOnExactlyOneSide
            && IsStructuralWallComponent(component)
            && wall.Confidence.Value >= 0.45)
        {
            if (wall.WallType == WallType.Interior)
            {
                return new WallTypeRefinement(
                    WallType.Interior,
                    "wall type preserved interior: one-sided room evidence did not override interior wall-envelope evidence");
            }

            return new WallTypeRefinement(
                WallType.Exterior,
                "wall type refined exterior: detected room evidence on one side only");
        }

        if (wall.WallType == WallType.Unknown
            && roomReferenceCount == 1
            && IsStructuralWallComponent(component)
            && wall.Confidence.Value >= 0.6)
        {
            return new WallTypeRefinement(
                WallType.Exterior,
                "wall type refined exterior: structural room boundary with no shared room side");
        }

        return new WallTypeRefinement(wall.WallType, "wall type unchanged: room-side evidence was inconclusive");
    }

    private static bool IsStructuralWallComponent(WallGraphComponent? component) =>
        component is null
        || (!component.ExcludedFromStructuralTopology
            && component.Kind is WallGraphComponentKind.MainStructural or WallGraphComponentKind.SecondaryStructural);

    private static bool IsNonStructuralWallComponent(WallGraphComponent? component) =>
        component is not null
        && (component.ExcludedFromStructuralTopology
            || component.Kind is WallGraphComponentKind.ObjectLikeIsland);

    private static bool TryPromoteRoomConfirmedWallEvidence(
        WallSegment wall,
        WallEvidenceWallAssessment assessment,
        WallGraphComponent? component,
        int roomReferenceCount,
        bool isSharedByRoomAdjacency,
        bool hasOutdoorRoomReference,
        RoomSideEvidence sideEvidence,
        out WallEvidenceWallAssessment promotedAssessment,
        out IReadOnlyList<string> promotionEvidence)
    {
        promotedAssessment = assessment;
        promotionEvidence = Array.Empty<string>();

        if (assessment.PlacementReady
            || assessment.RejectedAsNoise
            || assessment.Decision == WallEvidenceDecision.Reject
            || !assessment.RequiresReview)
        {
            return false;
        }

        if (assessment.Category is not (WallEvidenceCategory.MediumWallBody or WallEvidenceCategory.RecoveredWallBody))
        {
            return false;
        }

        if (!IsStructuralWallComponent(component)
            || wall.WallType == WallType.Unknown
            || wall.FragmentEvidence?.RequiresGeometryReview == true)
        {
            return false;
        }

        if (hasOutdoorRoomReference || sideEvidence.HasOutdoorRoomSide)
        {
            return false;
        }

        var hasStrongRoomConfirmation =
            isSharedByRoomAdjacency
            || roomReferenceCount >= 2
            || sideEvidence.HasRoomsOnBothSides;
        if (!hasStrongRoomConfirmation)
        {
            return false;
        }

        if (!HasWallBodyEvidence(wall, assessment))
        {
            return false;
        }

        promotionEvidence = new[]
        {
            "wall evidence: room-confirmed wall body promoted to placement-ready after room adjacency refinement",
            $"wall evidence: room references {roomReferenceCount.ToString(System.Globalization.CultureInfo.InvariantCulture)}, shared adjacency {isSharedByRoomAdjacency.ToString(System.Globalization.CultureInfo.InvariantCulture)}, two-sided room evidence {sideEvidence.HasRoomsOnBothSides.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
        };
        promotedAssessment = assessment with
        {
            PlacementReady = true,
            RequiresReview = false,
            Decision = WallEvidenceDecision.Accept,
            Evidence = AppendEvidence(assessment.Evidence, promotionEvidence)
        };
        return true;
    }

    private static bool HasWallBodyEvidence(WallSegment wall, WallEvidenceWallAssessment assessment)
    {
        if (wall.DetectionKind == WallDetectionKind.ParallelLinePair
            || assessment.Category == WallEvidenceCategory.RecoveredWallBody)
        {
            return true;
        }

        return assessment.Evidence
            .Concat(wall.Evidence)
            .Any(item => item.Contains("parallel wall-face pair", StringComparison.OrdinalIgnoreCase)
                || item.Contains("recovered by wall evidence map", StringComparison.OrdinalIgnoreCase));
    }

    private static RoomSideEvidence AnalyzeRoomSides(
        WallSegment wall,
        IReadOnlyList<RoomRegion> pageRooms,
        ScannerOptions options)
    {
        if (pageRooms.Count == 0 || wall.CenterLine.Length <= double.Epsilon)
        {
            return RoomSideEvidence.Empty;
        }

        var along = wall.CenterLine.Vector.Normalize();
        if (along.Length <= double.Epsilon)
        {
            return RoomSideEvidence.Empty;
        }

        var normal = new PlanVector(-along.Y, along.X).Normalize();
        var sampleOffset = Math.Max(
            wall.Thickness > 0 ? wall.Thickness * 1.5 : options.DefaultWallThickness * 2.5,
            Math.Max(options.WallSnapTolerance * 3.0, options.DefaultWallThickness * 2.5));
        var positiveHits = 0;
        var negativeHits = 0;
        var positiveOutdoorHits = 0;
        var negativeOutdoorHits = 0;

        foreach (var t in new[] { 0.25, 0.5, 0.75 })
        {
            var point = wall.CenterLine.PointAt(t);
            var positiveRooms = RoomsContaining(point + (normal * sampleOffset), pageRooms, options.WallSnapTolerance);
            if (positiveRooms.Count > 0)
            {
                positiveHits++;
                if (positiveRooms.Any(room => room.UseKind == RoomUseKind.Outdoor))
                {
                    positiveOutdoorHits++;
                }
            }

            var negativeRooms = RoomsContaining(point + (normal * -sampleOffset), pageRooms, options.WallSnapTolerance);
            if (negativeRooms.Count > 0)
            {
                negativeHits++;
                if (negativeRooms.Any(room => room.UseKind == RoomUseKind.Outdoor))
                {
                    negativeOutdoorHits++;
                }
            }
        }

        return new RoomSideEvidence(positiveHits, negativeHits, positiveOutdoorHits, negativeOutdoorHits);
    }

    private static IReadOnlyList<RoomRegion> RoomsContaining(
        PlanPoint point,
        IReadOnlyList<RoomRegion> rooms,
        double tolerance) =>
        rooms
            .Where(room => IsInsideRoom(point, room, tolerance))
            .ToArray();

    private static bool IsInsideRoom(PlanPoint point, RoomRegion room, double tolerance)
    {
        if (!room.Bounds.Contains(point, tolerance))
        {
            return false;
        }

        return room.Boundary.Count < 3 || IsPointInPolygon(point, room.Boundary);
    }

    private static bool IsPointInPolygon(PlanPoint point, IReadOnlyList<PlanPoint> polygon)
    {
        var inside = false;
        for (int index = 0, previous = polygon.Count - 1; index < polygon.Count; previous = index++)
        {
            var currentPoint = polygon[index];
            var previousPoint = polygon[previous];
            var crossesY = currentPoint.Y > point.Y != previousPoint.Y > point.Y;
            if (!crossesY)
            {
                continue;
            }

            var intersectionX = ((previousPoint.X - currentPoint.X) * (point.Y - currentPoint.Y)
                / (previousPoint.Y - currentPoint.Y))
                + currentPoint.X;
            if (point.X < intersectionX)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static IReadOnlyList<string> AppendEvidence(
        IReadOnlyList<string> evidence,
        string refinementEvidence) =>
        evidence
            .Append(refinementEvidence)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> AppendEvidence(
        IReadOnlyList<string> evidence,
        IEnumerable<string> refinementEvidence) =>
        evidence
            .Concat(refinementEvidence)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static bool IsActionableEvidence(string evidence) =>
        !evidence.Contains("unchanged", StringComparison.OrdinalIgnoreCase)
        && !evidence.Contains("inconclusive", StringComparison.OrdinalIgnoreCase);

    private static void AddDiagnostics(
        ScanContext context,
        int changed,
        int evidenceUpdated,
        int roomReferenced,
        int twoSidedRoomEvidence,
        int oneSidedRoomEvidence,
        int rejectedEvidenceProtected,
        int roomConfirmedPlacementPromoted)
    {
        var exterior = context.Walls.Count(wall => wall.WallType == WallType.Exterior);
        var interior = context.Walls.Count(wall => wall.WallType == WallType.Interior);
        var unknown = context.Walls.Count(wall => wall.WallType == WallType.Unknown);
        context.AddDiagnostic(
            "walls.architectural_type_refined",
            DiagnosticSeverity.Info,
            StageName,
            $"Refined wall type classifications for {changed} wall(s).",
            confidence: Confidence.Medium,
            scope: DiagnosticScope.Detection,
            properties: new Dictionary<string, string>
            {
                ["wallCount"] = context.Walls.Count.ToString(),
                ["changedWallTypeCount"] = changed.ToString(),
                ["evidenceUpdatedWallCount"] = evidenceUpdated.ToString(),
                ["roomReferencedWallCount"] = roomReferenced.ToString(),
                ["twoSidedRoomEvidenceWallCount"] = twoSidedRoomEvidence.ToString(),
                ["oneSidedRoomEvidenceWallCount"] = oneSidedRoomEvidence.ToString(),
                ["rejectedEvidenceProtectedWallCount"] = rejectedEvidenceProtected.ToString(),
                ["roomConfirmedPlacementPromotedWallCount"] = roomConfirmedPlacementPromoted.ToString(),
                ["exteriorWallCount"] = exterior.ToString(),
                ["interiorWallCount"] = interior.ToString(),
                ["unknownWallCount"] = unknown.ToString()
            });
    }

    private readonly record struct RoomSideEvidence(
        int PositiveRoomHits,
        int NegativeRoomHits,
        int PositiveOutdoorRoomHits,
        int NegativeOutdoorRoomHits)
    {
        public static RoomSideEvidence Empty { get; } = new(0, 0, 0, 0);

        public bool HasRoomsOnBothSides => PositiveRoomHits > 0 && NegativeRoomHits > 0;

        public bool HasRoomsOnExactlyOneSide => PositiveRoomHits > 0 != NegativeRoomHits > 0;

        public bool HasOutdoorRoomSide => PositiveOutdoorRoomHits > 0 || NegativeOutdoorRoomHits > 0;
    }

    private sealed record WallTypeRefinement(WallType WallType, string Evidence);
}
