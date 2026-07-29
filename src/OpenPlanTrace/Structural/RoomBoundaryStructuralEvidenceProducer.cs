namespace OpenPlanTrace;

internal sealed class RoomBoundaryStructuralEvidenceProducer : IStructuralEvidenceProducer
{
    private const double MinimumCredibleUnknownRoomAreaSquareMeters = 1.5;
    private const double MinimumSemanticAreaMatchRatio = 0.50;
    private const double MaximumSemanticAreaMatchRatio = 1.45;

    public string Name => "room-loop-evidence";

    public void Produce(StructuralEvidenceBuildContext context)
    {
        var roomContexts = ClassifyRoomContexts(context.Source.Rooms);
        foreach (var room in context.Source.Rooms
                     .Where(room => room.Boundary.Count >= 3)
                     .OrderBy(room => room.PageNumber)
                     .ThenBy(room => room.Id, StringComparer.Ordinal))
        {
            var roomContext = roomContexts.GetValueOrDefault(
                room.Id,
                StructuralRoomLoopContext.Unknown);
            var boundary = NormalizeBoundary(room.Boundary);
            if (boundary.Count < 3)
            {
                continue;
            }

            var boundaryEdges = new List<StructuralRoomBoundaryEdge>();
            for (var index = 0; index < boundary.Count; index++)
            {
                var line = StructuralGeometry.Canonicalize(
                    new PlanLineSegment(boundary[index], boundary[(index + 1) % boundary.Count]));
                if (line.Length < context.Options.MinimumCandidateLength)
                {
                    continue;
                }

                var compatible = context.Candidates.FindCompatible(
                    room.PageNumber,
                    line,
                    context.Options,
                    minimumOverlapRatio: 0.40);
                var cleanMatches = compatible
                    .Where(candidate => !candidate.HasStrongNegativeEvidence)
                    .Take(context.Options.MaximumRoomBoundaryAlternativesPerEdge)
                    .ToArray();
                var matches = cleanMatches.Length > 0
                    ? cleanMatches.ToList()
                    : compatible
                        .Take(1)
                        .ToList();
                if (cleanMatches.Length == 0)
                {
                    var id = $"structural:room:{room.Id}:edge:{index}";
                    var confidence = new Confidence(room.Confidence.Value * 0.82);
                    var candidate = context.Candidates.Add(
                        id,
                        room.PageNumber,
                        line,
                        Math.Max(context.Source.DefaultWallThickness, 0.5),
                        WallType.Unknown,
                        confidence,
                        StructuralCandidateOrigin.RoomBoundary,
                        isEligible: true,
                        wasAccepted: false,
                        sourceRoomIds: new[] { room.Id },
                        evidence: new[]
                        {
                            $"generated from room {room.Id} boundary edge {index}",
                            "retained as a room-boundary hypothesis until joint solving"
                        });
                    AddRoomBoundarySignal(
                        candidate,
                        room,
                        index,
                        confidence.Value * 0.32,
                        roomContext);
                    matches.Add(candidate);
                }

                foreach (var match in matches)
                {
                    match.SourceRoomIds.Add(room.Id);
                    match.AddOrigin(StructuralCandidateOrigin.RoomBoundary);
                    AddRoomBoundarySignal(
                        match,
                        room,
                        index,
                        room.Confidence.Value * 0.30,
                        roomContext);
                }

                boundaryEdges.Add(
                    new StructuralRoomBoundaryEdge(
                        $"structural-room-edge:{room.Id}:{index}",
                        line,
                        matches.Select(match => match.Id).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                        new[]
                        {
                            $"room {room.Id} boundary edge {index}",
                            $"matched {matches.Count} structural candidate(s)"
                        }));
            }

            if (boundaryEdges.Count < 3)
            {
                continue;
            }

            context.RoomLoops.Add(
                new StructuralRoomLoopCandidate(
                    $"structural-room-loop:{room.Id}",
                    room.Id,
                    room.PageNumber,
                    boundary,
                    boundaryEdges,
                    room.Confidence,
                    RoomLoopWeight(room, roomContext),
                    room.Evidence
                        .Append($"room loop contains {boundaryEdges.Count} boundary edge(s)")
                        .Append($"room loop context {roomContext}")
                        .ToArray())
                {
                    Context = roomContext
                });
        }

        AddSharedBoundaryAndExteriorSignals(context, roomContexts);
    }

    private static void AddSharedBoundaryAndExteriorSignals(
        StructuralEvidenceBuildContext context,
        IReadOnlyDictionary<string, StructuralRoomLoopContext> roomContexts)
    {
        var roomsById = context.Source.Rooms
            .GroupBy(room => room.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var candidate in context.Candidates.Drafts)
        {
            var trustedRoomIds = candidate.SourceRoomIds
                .Where(roomId =>
                    roomContexts.GetValueOrDefault(roomId, StructuralRoomLoopContext.Unknown)
                    == StructuralRoomLoopContext.Indoor)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var contextOnlyRoomIds = candidate.SourceRoomIds
                .Except(trustedRoomIds, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (trustedRoomIds.Length >= 2)
            {
                candidate.AddSignal(
                    new StructuralEvidenceSignal(
                        $"signal:{candidate.Id}:shared-room-boundary",
                        StructuralEvidenceSignalKind.RoomBoundary,
                        0.08,
                        string.Join(",", trustedRoomIds),
                        "referenced as a boundary by multiple trusted room loops",
                        candidate.SourcePrimitiveIds.Order(StringComparer.Ordinal).ToArray()));

                if (TryFindOppositeRoomPair(
                        candidate,
                        trustedRoomIds,
                        roomsById,
                        context.Options,
                        out var firstRoomId,
                        out var secondRoomId))
                {
                    candidate.AddSignal(
                        new StructuralEvidenceSignal(
                            $"signal:{candidate.Id}:opposite-room-boundary",
                            StructuralEvidenceSignalKind.OppositeRoomBoundary,
                            0.18,
                            $"{firstRoomId},{secondRoomId}",
                            "source-linked room interiors lie on opposite sides of the wall axis",
                            candidate.SourcePrimitiveIds.Order(StringComparer.Ordinal).ToArray()));
                }
            }

            if (contextOnlyRoomIds.Length > 0
                && !candidate.HasIndependentWallBodyEvidence)
            {
                var hasOutdoorOrConflictedContext = contextOnlyRoomIds.Any(roomId =>
                    roomContexts.GetValueOrDefault(
                        roomId,
                        StructuralRoomLoopContext.Unknown)
                    is StructuralRoomLoopContext.Outdoor
                        or StructuralRoomLoopContext.Conflicted);
                var weight = trustedRoomIds.Length == 0
                    ? hasOutdoorOrConflictedContext
                        ? -1.25
                        : -0.60
                    : hasOutdoorOrConflictedContext
                        ? -0.35
                        : -0.18;
                candidate.AddSignal(
                    new StructuralEvidenceSignal(
                        $"signal:{candidate.Id}:context-only-boundary",
                        StructuralEvidenceSignalKind.ContextOnlyBoundary,
                        weight,
                        string.Join(",", contextOnlyRoomIds),
                        trustedRoomIds.Length == 0
                            ? hasOutdoorOrConflictedContext
                                ? "outdoor or conflicted room context cannot promote a line into a structural wall without independent wall-body evidence"
                                : "provisional geometry-only room loops cannot promote their own edges into structural walls without independent wall-body evidence"
                            : hasOutdoorOrConflictedContext
                                ? "mixed indoor/outdoor boundary lacks independent wall-body evidence and remains review-only"
                                : "mixed trusted/provisional room support is not independent structural evidence",
                        candidate.SourcePrimitiveIds.Order(StringComparer.Ordinal).ToArray()));
            }

            if (trustedRoomIds.Length != 1)
            {
                continue;
            }

            var mainRegion = context.Source.SheetRegions.FirstOrDefault(region =>
                region.PageNumber == candidate.PageNumber
                && region.Kind == RegionKind.MainFloorPlan);
            var nearEnvelope = mainRegion is not null
                && IsNearEnvelope(candidate.CenterLine, mainRegion.Bounds, context.Options.AxisTolerance * 3);
            if ((candidate.WallType == WallType.Exterior || nearEnvelope)
                && (contextOnlyRoomIds.Length == 0 || candidate.HasIndependentWallBodyEvidence))
            {
                candidate.AddOrigin(StructuralCandidateOrigin.ExteriorShell);
                candidate.AddSignal(
                    new StructuralEvidenceSignal(
                        $"signal:{candidate.Id}:exterior-shell",
                        StructuralEvidenceSignalKind.ExteriorShell,
                        0.13,
                        trustedRoomIds.Single(),
                        nearEnvelope
                            ? "single-room boundary lies near the main plan envelope"
                            : "single-room boundary agrees with exterior wall typing",
                        candidate.SourcePrimitiveIds.Order(StringComparer.Ordinal).ToArray()));
            }
        }
    }

    private static bool TryFindOppositeRoomPair(
        StructuralCandidateRegistry.CandidateDraft candidate,
        IReadOnlyList<string> trustedRoomIds,
        IReadOnlyDictionary<string, RoomRegion> roomsById,
        StructuralSolverOptions options,
        out string firstRoomId,
        out string secondRoomId)
    {
        firstRoomId = string.Empty;
        secondRoomId = string.Empty;
        var normal = StructuralGeometry.UnitNormal(candidate.CenterLine);
        var axis = StructuralGeometry.Dot(candidate.CenterLine.Midpoint, normal);
        var minimumSideDistance = Math.Max(
            options.AxisTolerance * 1.5,
            candidate.Thickness / 2.0);
        var roomSides = trustedRoomIds
            .Where(roomsById.ContainsKey)
            .Select(roomId => (
                RoomId: roomId,
                SignedDistance:
                    StructuralGeometry.Dot(roomsById[roomId].Bounds.Center, normal) - axis))
            .Where(item => Math.Abs(item.SignedDistance) > minimumSideDistance)
            .OrderBy(item => item.RoomId, StringComparer.Ordinal)
            .ToArray();

        foreach (var first in roomSides)
        {
            foreach (var second in roomSides)
            {
                if (string.CompareOrdinal(first.RoomId, second.RoomId) >= 0
                    || Math.Sign(first.SignedDistance) == Math.Sign(second.SignedDistance))
                {
                    continue;
                }

                firstRoomId = first.RoomId;
                secondRoomId = second.RoomId;
                return true;
            }
        }

        return false;
    }

    private static void AddRoomBoundarySignal(
        StructuralCandidateRegistry.CandidateDraft candidate,
        RoomRegion room,
        int edgeIndex,
        double weight,
        StructuralRoomLoopContext roomContext)
    {
        var contextScale = roomContext switch
        {
            StructuralRoomLoopContext.Outdoor => 0.15,
            StructuralRoomLoopContext.Conflicted => 0.20,
            StructuralRoomLoopContext.Unknown => 0.20,
            _ => 1.0
        };
        candidate.AddSignal(
            new StructuralEvidenceSignal(
                $"signal:{candidate.Id}:room-boundary:{room.Id}:{edgeIndex}",
                StructuralEvidenceSignalKind.RoomBoundary,
                Math.Clamp(weight * contextScale, 0.02, 0.32),
                room.Id,
                $"supported by {roomContext.ToString().ToLowerInvariant()} room {room.Id} boundary edge {edgeIndex}",
                room.LabelSourcePrimitiveIds));
    }

    private static double RoomLoopWeight(
        RoomRegion room,
        StructuralRoomLoopContext roomContext)
    {
        var baseWeight = Math.Clamp(0.45 + (room.Confidence.Value * 0.55), 0, 1);
        return roomContext switch
        {
            StructuralRoomLoopContext.Outdoor => baseWeight * 0.15,
            StructuralRoomLoopContext.Conflicted => baseWeight * 0.20,
            StructuralRoomLoopContext.Unknown => baseWeight * 0.12,
            _ => baseWeight
        };
    }

    internal static IReadOnlyDictionary<string, StructuralRoomLoopContext> ClassifyRoomContexts(
        IReadOnlyList<RoomRegion> rooms)
    {
        var outdoorRooms = rooms
            .Where(room => room.UseKind == RoomUseKind.Outdoor)
            .ToArray();
        return rooms.ToDictionary(
            room => room.Id,
            room =>
            {
                if (room.UseKind == RoomUseKind.Outdoor)
                {
                    return StructuralRoomLoopContext.Outdoor;
                }

                var overlapsOutdoor = outdoorRooms.Any(outdoor =>
                    outdoor.PageNumber == room.PageNumber
                    && BoundsOverlapRatio(room.Bounds, outdoor.Bounds) >= 0.35);
                if (overlapsOutdoor)
                {
                    return StructuralRoomLoopContext.Conflicted;
                }

                if (HasConflictedBoundaryEvidence(room))
                {
                    return StructuralRoomLoopContext.Conflicted;
                }

                if (room.UseKind != RoomUseKind.Unknown
                    || HasCredibleUnknownRoomIdentity(room))
                {
                    return StructuralRoomLoopContext.Indoor;
                }

                return StructuralRoomLoopContext.Unknown;
            },
            StringComparer.Ordinal);
    }

    private static bool HasCredibleUnknownRoomIdentity(RoomRegion room)
    {
        if (room.Evidence.Any(item =>
                item.Contains(
                    "semantic room seed from label",
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(room.Label)
            || room.AreaSquareMeters is > 0
                and < MinimumCredibleUnknownRoomAreaSquareMeters)
        {
            return false;
        }

        var letterCount = room.Label.Count(char.IsLetter);
        if (letterCount >= 2)
        {
            return true;
        }

        return letterCount == 1
            && room.Label.Any(char.IsDigit);
    }

    private static bool HasConflictedBoundaryEvidence(RoomRegion room)
    {
        if (room.Evidence.Any(item =>
                item.Contains(
                    "semantic room boundary could not be closed",
                    StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var areaMatchRatio = ReadEvidenceNumber(
            room.Evidence,
            "semantic room boundary area match ratio ");
        if (areaMatchRatio is < MinimumSemanticAreaMatchRatio
            or > MaximumSemanticAreaMatchRatio)
        {
            return true;
        }

        var trustedWallSupport = ReadEvidenceNumber(
            room.Evidence,
            "semantic room boundary trusted wall support ");
        return trustedWallSupport is <= 0.01
            && room.Evidence.Any(item =>
                item.Contains(
                    "review-supported semantic room boundary",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static double? ReadEvidenceNumber(
        IReadOnlyList<string> evidence,
        string prefix)
    {
        var item = evidence.FirstOrDefault(value =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return null;
        }

        var value = item[prefix.Length..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return double.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    private static double BoundsOverlapRatio(
        PlanRect first,
        PlanRect second)
    {
        var width = Math.Max(0, Math.Min(first.Right, second.Right) - Math.Max(first.Left, second.Left));
        var height = Math.Max(0, Math.Min(first.Bottom, second.Bottom) - Math.Max(first.Top, second.Top));
        var denominator = Math.Max(1e-9, Math.Min(first.Area, second.Area));
        return Math.Clamp((width * height) / denominator, 0, 1);
    }

    private static IReadOnlyList<PlanPoint> NormalizeBoundary(IReadOnlyList<PlanPoint> boundary)
    {
        var points = boundary.ToList();
        if (points.Count > 1 && points[0].DistanceTo(points[^1]) <= 1e-6)
        {
            points.RemoveAt(points.Count - 1);
        }

        return points;
    }

    private static bool IsNearEnvelope(
        PlanLineSegment line,
        PlanRect bounds,
        double tolerance)
    {
        var horizontal = line.IsHorizontal(tolerance);
        var vertical = line.IsVertical(tolerance);
        if (horizontal)
        {
            return Math.Abs(line.Midpoint.Y - bounds.Top) <= tolerance
                || Math.Abs(line.Midpoint.Y - bounds.Bottom) <= tolerance;
        }

        if (vertical)
        {
            return Math.Abs(line.Midpoint.X - bounds.Left) <= tolerance
                || Math.Abs(line.Midpoint.X - bounds.Right) <= tolerance;
        }

        return false;
    }
}
