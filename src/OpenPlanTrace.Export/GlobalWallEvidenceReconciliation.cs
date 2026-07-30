namespace OpenPlanTrace.Export;

public static partial class GlobalWallSolutionBuilder
{
    public const string ReconcilerVersion = "openplantrace.wall-evidence-reconciler.v9";

    private const double MinimumReconciliationMovement = 0.05;
    private const double MaximumReconciliationAxisShift = 8.0;
    private const double MaximumReconciliationEndpointAdjustment = 24.0;

    private static IReadOnlyList<CompactedWallRun> ReconcileWallEvidence(
        IReadOnlyList<CompactedWallRun> runs,
        IReadOnlyList<GlobalWallCandidate> allCandidates,
        IReadOnlyList<PlacementRoomExport> rooms,
        IReadOnlyList<PlacementOpeningExport> openings)
    {
        if (runs.Count == 0)
        {
            return runs;
        }

        var axisAligned = runs
            .Select(run => ReconcileRunAxisAndExtent(
                run,
                runs,
                allCandidates,
                rooms,
                openings))
            .ToArray();
        var junctionCompleted = CompleteSupportedJunctions(
            axisAligned,
            openings);
        var cornerNormalized = TrimSupportedExteriorCornerOverruns(junctionCompleted);

        return cornerNormalized
            .Select((run, index) =>
            {
                var beforeJunction = axisAligned[index];
                var state = beforeJunction.Reconciliation
                    ?? WallReconciliationState.Unchanged(beforeJunction.CenterLine);
                var junctionSnapCount = Math.Max(
                    0,
                    run.CompletedJunctionCount - beforeJunction.CompletedJunctionCount);
                var bodyContactJunctionCount = Math.Max(
                    0,
                    run.BodyContactJunctionCount
                        - beforeJunction.BodyContactJunctionCount);
                if (junctionSnapCount == 0)
                {
                    return run;
                }

                return run with
                {
                    Reconciliation = state with
                    {
                        JunctionSnapCount = state.JunctionSnapCount + junctionSnapCount,
                        Evidence = state.Evidence
                            .Append($"reconciler snapped {junctionSnapCount} endpoint(s) to supported perpendicular junctions")
                            .Concat(bodyContactJunctionCount > 0
                                ? new[]
                                {
                                    $"reconciler normalized {bodyContactJunctionCount} source-backed wall-body contact endpoint(s) within a shared main-structural component"
                                }
                                : Array.Empty<string>())
                            .Distinct(StringComparer.Ordinal)
                            .ToArray()
                    }
                };
            })
            .ToArray();
    }

    private static CompactedWallRun ReconcileRunAxisAndExtent(
        CompactedWallRun run,
        IReadOnlyList<CompactedWallRun> runs,
        IReadOnlyList<GlobalWallCandidate> allCandidates,
        IReadOnlyList<PlacementRoomExport> rooms,
        IReadOnlyList<PlacementOpeningExport> openings)
    {
        var orientation = Orientation(run.CenterLine);
        if (orientation == WallOrientation.Diagonal)
        {
            return run with
            {
                Reconciliation = WallReconciliationState.Unchanged(
                    run.CenterLine,
                    "diagonal wall retained because the first reconciler is limited to orthogonal plans")
            };
        }

        var votes = CollectReconciliationVotes(
            run,
            runs,
            allCandidates,
            rooms,
            openings);
        var currentAxis = AxisCoordinate(run.CenterLine);
        var allowedAxisShift = Math.Max(
            1.5,
            Math.Min(
                MaximumReconciliationAxisShift,
                run.ThicknessDrawingUnits * 0.65));
        var consensus = BuildAxisConsensus(
            votes,
            currentAxis,
            run.ThicknessDrawingUnits,
            allowedAxisShift);
        var reconciledAxis = consensus.Accepted
            ? consensus.Axis
            : currentAxis;
        var line = WithAxis(run.CenterLine, orientation, reconciledAxis);
        var extent = ReconcileSupportedExtent(
            run,
            runs,
            line,
            orientation,
            votes,
            reconciledAxis,
            run.ThicknessDrawingUnits);
        line = WithInterval(line, orientation, extent.Start, extent.End);

        var candidateVotes = votes.Count(vote => vote.Kind == ReconciliationVoteKind.Candidate);
        var roomVotes = votes.Count(vote => vote.Kind == ReconciliationVoteKind.RoomBoundary);
        var openingVotes = votes.Count(vote => vote.Kind == ReconciliationVoteKind.Opening);
        var neighborVotes = votes.Count(vote => vote.Kind == ReconciliationVoteKind.Neighbor);
        var evidence = new List<string>
        {
            $"reconciler evaluated {votes.Count} source-aware geometry vote(s)",
            $"candidate votes {candidateVotes}, room-boundary votes {roomVotes}, opening votes {openingVotes}, neighbor votes {neighborVotes}",
            consensus.Evidence,
            extent.Evidence
        };
        if (IsResolvedPhysicalWallAssembly(run))
        {
            evidence.Add(
                "resolved physical wall assembly axis retained while source-linked face evidence remained available for extent reconciliation");
        }
        var confidence = Math.Clamp(
            run.Confidence * 0.35
            + consensus.Confidence * 0.45
            + (roomVotes > 0 ? 0.08 : 0)
            + (openingVotes > 0 ? 0.05 : 0)
            + (neighborVotes > 0 ? 0.07 : 0),
            0,
            1);

        return run with
        {
            CenterLine = line,
            Reconciliation = new WallReconciliationState(
                run.CenterLine,
                candidateVotes,
                roomVotes,
                openingVotes,
                neighborVotes,
                0,
                0,
                confidence,
                evidence
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray())
        };
    }

    private static IReadOnlyList<WallReconciliationVote> CollectReconciliationVotes(
        CompactedWallRun run,
        IReadOnlyList<CompactedWallRun> runs,
        IReadOnlyList<GlobalWallCandidate> allCandidates,
        IReadOnlyList<PlacementRoomExport> rooms,
        IReadOnlyList<PlacementOpeningExport> openings)
    {
        var votes = new List<WallReconciliationVote>();
        var orientation = Orientation(run.CenterLine);
        var runLength = Math.Max(1, RunLength(run));
        var sourceWallIds = SourceWallIds(run);
        var contributorIds = run.Contributors
            .Select(candidate => candidate.Id)
            .ToHashSet(StringComparer.Ordinal);
        var resolvedPhysicalAssembly = IsResolvedPhysicalWallAssembly(run);
        var resolvedAssemblyAxis = AxisCoordinate(run.CenterLine);

        foreach (var contributor in run.Contributors)
        {
            var weight = Math.Max(0.25, contributor.LocalScore)
                * Math.Max(1, OriginPriority(contributor.PrimaryOrigin))
                * (contributor.ReadyForCoordinatePlacement ? 1.35 : 1.0)
                * Math.Max(0.35, Math.Min(1.0, contributor.DrawingLength / runLength));
            votes.Add(new WallReconciliationVote(
                contributor.CenterLine,
                weight,
                ReconciliationVoteKind.Candidate,
                contributor.ReadyForCoordinatePlacement,
                CandidateSourceKeys(contributor),
                $"selected candidate {contributor.Id} ({contributor.PrimaryOrigin})"));
        }

        var contextualAxisTolerance = Math.Max(
            AxisGroupingDistance,
            Math.Min(
                MaximumReconciliationAxisShift,
                run.ThicknessDrawingUnits * 1.25));
        foreach (var candidate in allCandidates)
        {
            if (contributorIds.Contains(candidate.Id)
                || candidate.PageNumber != run.PageNumber
                || Orientation(candidate.CenterLine) != orientation
                || candidate.StrongNegativeEvidence
                || !CompatibleWallTypes(candidate.WallType, run.WallType)
                || LineDistance(candidate.CenterLine, run.CenterLine) > contextualAxisTolerance)
            {
                continue;
            }

            var overlapRatio = LengthOverlapRatio(candidate.CenterLine, run.CenterLine);
            var sourceLinked = candidate.SourceWallIds.Any(sourceWallIds.Contains);
            var independentlyStructural = candidate.ReadyForCoordinatePlacement
                && !candidate.ExcludedFromStructuralTopology
                && candidate.StructuralEvidenceCount >= 2
                && candidate.RoomBoundarySupportCount > 0;
            if (overlapRatio < 0.35
                || (!sourceLinked && !independentlyStructural))
            {
                continue;
            }

            var weight = Math.Max(0.20, candidate.LocalScore)
                * (sourceLinked ? 1.8 : 1.0)
                * (OriginPriority(candidate.PrimaryOrigin) >= 2 ? 1.35 : 0.85)
                * Math.Max(0.35, overlapRatio);
            votes.Add(new WallReconciliationVote(
                NormalizeSourceLinkedAssemblyVote(
                    candidate.CenterLine,
                    orientation,
                    sourceLinked,
                    resolvedPhysicalAssembly,
                    resolvedAssemblyAxis),
                weight,
                ReconciliationVoteKind.Candidate,
                independentlyStructural,
                CandidateSourceKeys(candidate),
                $"context candidate {candidate.Id} ({candidate.PrimaryOrigin})"));
        }

        foreach (var room in rooms)
        {
            if (room.PageNumber != run.PageNumber
                || room.Boundary.Count < 2
                || !room.Reliability.ReadyForCoordinatePlacement
                || room.Confidence < 0.40)
            {
                continue;
            }

            var sourceLinked = room.WallIds.Any(sourceWallIds.Contains);
            foreach (var boundary in RoomBoundaryLines(room))
            {
                if (Orientation(boundary) != orientation
                    || LineDistance(boundary, run.CenterLine) > contextualAxisTolerance)
                {
                    continue;
                }

                var overlapRatio = LengthOverlapRatio(boundary, run.CenterLine);
                if (overlapRatio < (sourceLinked ? 0.20 : 0.45))
                {
                    continue;
                }

                votes.Add(new WallReconciliationVote(
                    NormalizeSourceLinkedAssemblyVote(
                        boundary,
                        orientation,
                        sourceLinked,
                        resolvedPhysicalAssembly,
                        resolvedAssemblyAxis),
                    (sourceLinked ? 3.25 : 1.60)
                        * Math.Max(0.40, room.Confidence)
                        * Math.Max(0.30, overlapRatio),
                    ReconciliationVoteKind.RoomBoundary,
                    true,
                    [$"room:{room.Id}"],
                    $"room boundary {room.Id}")
                {
                    SourceLinked = sourceLinked
                });
            }
        }

        foreach (var opening in openings)
        {
            if (opening.PageNumber != run.PageNumber
                || opening.Placement is null
                || !opening.Reliability.ReadyForCoordinatePlacement
                || Orientation(opening.Placement.ReferenceLine) != orientation)
            {
                continue;
            }

            var sourceLinked = OpeningSourceHostWallIds(opening).Any(sourceWallIds.Contains)
                || run.BridgedOpeningIds.Contains(opening.Id, StringComparer.Ordinal);
            var axisTolerance = Math.Max(
                contextualAxisTolerance,
                opening.Placement.DepthDrawingUnits);
            var geometricallyAligned = LineDistance(
                opening.Placement.ReferenceLine,
                run.CenterLine) <= axisTolerance
                && IntervalsOverlapWithTolerance(
                    opening.Placement.ReferenceLine,
                    run.CenterLine,
                    Math.Max(2, opening.DrawingWidth));
            if (!sourceLinked && !geometricallyAligned)
            {
                continue;
            }

            votes.Add(new WallReconciliationVote(
                NormalizeSourceLinkedAssemblyVote(
                    opening.Placement.ReferenceLine,
                    orientation,
                    sourceLinked,
                    resolvedPhysicalAssembly,
                    resolvedAssemblyAxis),
                (sourceLinked ? 2.75 : 1.25) * Math.Max(0.45, opening.Confidence),
                ReconciliationVoteKind.Opening,
                opening.Confidence >= 0.65,
                [$"opening:{opening.Id}"],
                $"anchored opening {opening.Id}"));
        }

        foreach (var neighbor in runs)
        {
            if (ReferenceEquals(neighbor, run)
                || neighbor.PageNumber != run.PageNumber
                || Orientation(neighbor.CenterLine) != orientation
                || !CompatibleWallTypes(neighbor.WallType, run.WallType))
            {
                continue;
            }

            var allowedDistance = Math.Max(
                AxisGroupingDistance,
                Math.Min(
                    MaximumReconciliationAxisShift,
                    (run.ThicknessDrawingUnits + neighbor.ThicknessDrawingUnits) * 0.55));
            var maximumGap = Math.Max(
                12.0,
                Math.Min(
                    40.0,
                    (run.ThicknessDrawingUnits + neighbor.ThicknessDrawingUnits) * 2.0));
            if (LineDistance(neighbor.CenterLine, run.CenterLine) > allowedDistance
                || GapBetween(neighbor.CenterLine, run.CenterLine) > maximumGap)
            {
                continue;
            }

            var neighborSources = SourceWallIds(neighbor);
            var sourceLinked = neighborSources.Any(sourceWallIds.Contains);
            var structurallyReady = RunSupportsJunctionCompletion(neighbor);
            if (!sourceLinked && !structurallyReady)
            {
                continue;
            }

            votes.Add(new WallReconciliationVote(
                NormalizeSourceLinkedAssemblyVote(
                    neighbor.CenterLine,
                    orientation,
                    sourceLinked,
                    resolvedPhysicalAssembly,
                    resolvedAssemblyAxis),
                (sourceLinked ? 2.50 : 1.40) * Math.Max(0.40, neighbor.Confidence),
                ReconciliationVoteKind.Neighbor,
                structurallyReady,
                RunSourceKeys(neighbor),
                $"neighboring canonical run at axis {AxisCoordinate(neighbor.CenterLine):0.###}"));
        }

        return votes;
    }

    private static LineExport NormalizeSourceLinkedAssemblyVote(
        LineExport line,
        WallOrientation orientation,
        bool sourceLinked,
        bool resolvedPhysicalAssembly,
        double resolvedAssemblyAxis) =>
        sourceLinked && resolvedPhysicalAssembly
            ? WithAxis(line, orientation, resolvedAssemblyAxis)
            : line;

    private static bool IsResolvedPhysicalWallAssembly(
        CompactedWallRun run) =>
        run.Contributors.Any(candidate =>
            candidate.Evidence.Any(item =>
                item.Contains(
                    "resolved exterior wall assembly",
                    StringComparison.OrdinalIgnoreCase)
                || item.Contains(
                    "resolved shared-source physical wall assembly",
                    StringComparison.OrdinalIgnoreCase)));

    private static AxisConsensus BuildAxisConsensus(
        IReadOnlyList<WallReconciliationVote> votes,
        double currentAxis,
        double thickness,
        double allowedAxisShift)
    {
        if (votes.Count == 0)
        {
            return AxisConsensus.Rejected(
                currentAxis,
                "reconciler retained source axis because no independent geometry votes were available");
        }

        var weightedMedian = WeightedMedian(votes);
        var inlierTolerance = Math.Max(0.75, Math.Min(3.0, thickness * 0.25));
        var inliers = votes
            .Where(vote => Math.Abs(AxisCoordinate(vote.Line) - weightedMedian) <= inlierTolerance)
            .ToArray();
        var totalWeight = votes.Sum(vote => vote.Weight);
        var inlierWeight = inliers.Sum(vote => vote.Weight);
        var inlierRatio = totalWeight <= 0 ? 0 : inlierWeight / totalWeight;
        var target = inlierWeight <= 0
            ? currentAxis
            : inliers.Sum(vote => AxisCoordinate(vote.Line) * vote.Weight) / inlierWeight;
        var shift = target - currentAxis;
        var independentKinds = inliers
            .Select(vote => vote.Kind)
            .Distinct()
            .Count();
        var independentSupport = EvaluateIndependentSupport(
            inliers,
            shift,
            thickness);
        var accepted = independentSupport.Accepted
            && inlierRatio >= 0.55
            && Math.Abs(shift) <= allowedAxisShift
            && double.IsFinite(target);
        var confidence = Math.Clamp(
            inlierRatio * 0.55
            + Math.Min(3, independentKinds) * 0.10
            + Math.Min(3, independentSupport.DistinctSourceCount) * 0.04
            + (independentSupport.HasSemanticGeometry ? 0.08 : 0),
            0,
            1);

        if (!accepted)
        {
            return new AxisConsensus(
                currentAxis,
                false,
                confidence,
                $"reconciler retained source axis; consensus ratio {inlierRatio:0.###}, independent kinds {independentKinds}, independent sources {independentSupport.DistinctSourceCount}, proposed shift {shift:0.###}; {independentSupport.Evidence}");
        }

        return new AxisConsensus(
            target,
            true,
            confidence,
            Math.Abs(shift) <= MinimumReconciliationMovement
                ? $"reconciler confirmed source axis with {inlierRatio:0.###} consensus from {independentSupport.DistinctSourceCount} independent source(s)"
                : $"reconciler aligned axis by {shift:0.###} drawing units with {inlierRatio:0.###} consensus from {independentSupport.DistinctSourceCount} independent source(s)");
    }

    private static ExtentConsensus ReconcileSupportedExtent(
        CompactedWallRun run,
        IReadOnlyList<CompactedWallRun> runs,
        LineExport line,
        WallOrientation orientation,
        IReadOnlyList<WallReconciliationVote> votes,
        double reconciledAxis,
        double thickness)
    {
        var start = IntervalStart(line);
        var end = IntervalEnd(line);
        var maximumAdjustment = Math.Max(
            4.0,
            Math.Min(
                MaximumReconciliationEndpointAdjustment,
                thickness * 1.75));
        var maximumTrimAdjustment = Math.Max(
            4.0,
            Math.Min(
                32.0,
                (end - start) * 0.25));
        var axisTolerance = Math.Max(1.0, Math.Min(3.5, thickness * 0.30));
        var extentVotes = votes
            .Where(vote =>
                (vote.Kind is ReconciliationVoteKind.RoomBoundary
                    or ReconciliationVoteKind.Neighbor
                    or ReconciliationVoteKind.Candidate)
                && vote.IndependentSupport
                && Math.Abs(AxisCoordinate(vote.Line) - reconciledAxis) <= axisTolerance)
            .ToArray();
        var startTarget = SupportedExtensionTarget(
            extentVotes,
            start,
            maximumAdjustment,
            extendTowardMinimum: true,
            thickness);
        var endTarget = SupportedExtensionTarget(
            extentVotes,
            end,
            maximumAdjustment,
            extendTowardMinimum: false,
            thickness);
        var startTrimTarget = startTarget is null
            ? SupportedInwardTrimTarget(
                run,
                runs,
                line,
                orientation,
                votes,
                start,
                end,
                maximumTrimAdjustment,
                trimMinimumEndpoint: true,
                thickness)
            : null;
        var endTrimTarget = endTarget is null
            ? SupportedInwardTrimTarget(
                run,
                runs,
                line,
                orientation,
                votes,
                end,
                start,
                maximumTrimAdjustment,
                trimMinimumEndpoint: false,
                thickness)
            : null;
        var reconciledStart = startTarget ?? startTrimTarget ?? start;
        var reconciledEnd = endTarget ?? endTrimTarget ?? end;
        if (reconciledEnd - reconciledStart <= 0.5)
        {
            return new ExtentConsensus(
                start,
                end,
                "reconciler retained source extent because proposed support collapsed the run");
        }

        var extensions = 0;
        if (reconciledStart < start - MinimumReconciliationMovement)
        {
            extensions++;
        }

        if (reconciledEnd > end + MinimumReconciliationMovement)
        {
            extensions++;
        }

        var trims = 0;
        if (reconciledStart > start + MinimumReconciliationMovement)
        {
            trims++;
        }

        if (reconciledEnd < end - MinimumReconciliationMovement)
        {
            trims++;
        }

        var evidence = new List<string>();
        if (extensions > 0)
        {
            evidence.Add(
                $"reconciler recovered {extensions} endpoint extent(s) from corroborated geometry");
        }

        if (trims > 0)
        {
            evidence.Add(
                $"reconciler clipped {trims} unsupported interior overrun(s) to source-backed perpendicular junctions");
        }

        return new ExtentConsensus(
            reconciledStart,
            reconciledEnd,
            evidence.Count == 0
                ? "reconciler retained source extent pending junction evidence"
                : string.Join("; ", evidence));
    }

    private static double? SupportedExtensionTarget(
        IReadOnlyList<WallReconciliationVote> votes,
        double current,
        double maximumAdjustment,
        bool extendTowardMinimum,
        double thickness)
    {
        var candidates = votes
            .Select(vote => new EndpointVote(
                extendTowardMinimum ? IntervalStart(vote.Line) : IntervalEnd(vote.Line),
                vote))
            .Where(vote =>
                extendTowardMinimum
                    ? vote.Coordinate < current - MinimumReconciliationMovement
                        && current - vote.Coordinate <= maximumAdjustment
                    : vote.Coordinate > current + MinimumReconciliationMovement
                        && vote.Coordinate - current <= maximumAdjustment)
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var clusterTolerance = Math.Max(1.0, Math.Min(3.0, thickness * 0.20));
        EndpointVoteCluster? best = null;
        foreach (var seed in candidates)
        {
            var cluster = candidates
                .Where(vote => Math.Abs(vote.Coordinate - seed.Coordinate) <= clusterTolerance)
                .ToArray();
            var sourceVotes = cluster
                .Select(vote => vote.Source)
                .ToArray();
            var support = EvaluateIndependentSupport(
                sourceVotes,
                seed.Coordinate - current,
                thickness);
            var weight = cluster.Sum(vote => vote.Source.Weight);
            if (!support.Accepted || weight <= 0)
            {
                continue;
            }

            var coordinate = cluster.Sum(vote => vote.Coordinate * vote.Source.Weight) / weight;
            var candidate = new EndpointVoteCluster(coordinate, weight);
            if (best is null
                || candidate.Weight > best.Weight + 0.001
                || (Math.Abs(candidate.Weight - best.Weight) <= 0.001
                    && (extendTowardMinimum
                        ? candidate.Coordinate < best.Coordinate
                        : candidate.Coordinate > best.Coordinate)))
            {
                best = candidate;
            }
        }

        return best?.Coordinate;
    }

    private static double? SupportedInwardTrimTarget(
        CompactedWallRun run,
        IReadOnlyList<CompactedWallRun> runs,
        LineExport line,
        WallOrientation orientation,
        IReadOnlyList<WallReconciliationVote> votes,
        double current,
        double opposite,
        double maximumAdjustment,
        bool trimMinimumEndpoint,
        double thickness)
    {
        if (!string.Equals(
                run.WallType,
                "Interior",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var endpointTolerance = Math.Max(
            1.5,
            Math.Min(4.0, thickness * 0.50 + 2.0));
        if (HasSemanticEndpointSupport(
                votes,
                current,
                trimMinimumEndpoint,
                endpointTolerance))
        {
            return null;
        }

        if (HasStructuralEndpointConnection(
                run,
                runs,
                line,
                orientation,
                current))
        {
            return null;
        }

        var candidates = votes
            .Where(vote =>
                vote.IndependentSupport
                && (vote.Kind is ReconciliationVoteKind.Candidate
                    or ReconciliationVoteKind.RoomBoundary))
            .Select(vote => new EndpointVote(
                trimMinimumEndpoint
                    ? IntervalStart(vote.Line)
                    : IntervalEnd(vote.Line),
                vote))
            .Where(vote =>
            {
                var movement = trimMinimumEndpoint
                    ? vote.Coordinate - current
                    : current - vote.Coordinate;
                var remainingLength = trimMinimumEndpoint
                    ? opposite - vote.Coordinate
                    : vote.Coordinate - opposite;
                return movement > MinimumReconciliationMovement
                    && movement <= maximumAdjustment
                    && remainingLength > 0.5;
            })
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var clusterTolerance = Math.Max(
            1.0,
            Math.Min(3.0, thickness * 0.25 + 0.75));
        var accepted = new List<EndpointVoteCluster>();
        foreach (var seed in candidates)
        {
            var cluster = candidates
                .Where(vote =>
                    Math.Abs(vote.Coordinate - seed.Coordinate)
                    <= clusterTolerance)
                .ToArray();
            var sourceVotes = cluster
                .Select(vote => vote.Source)
                .ToArray();
            var hasCandidateGeometry = sourceVotes.Any(vote =>
                vote.Kind == ReconciliationVoteKind.Candidate);
            var hasRoomGeometry = sourceVotes.Any(vote =>
                vote.Kind == ReconciliationVoteKind.RoomBoundary);
            var hasSourceLinkedRoomGeometry = sourceVotes.Any(vote =>
                vote.Kind == ReconciliationVoteKind.RoomBoundary
                && vote.SourceLinked);
            if (!hasRoomGeometry || !hasSourceLinkedRoomGeometry)
            {
                continue;
            }

            var movement = seed.Coordinate - current;
            var support = EvaluateIndependentSupport(
                sourceVotes,
                movement,
                thickness);
            var weight = cluster.Sum(vote => vote.Source.Weight);
            if ((!support.Accepted && hasCandidateGeometry)
                || !support.HasSemanticGeometry
                || weight <= 0)
            {
                continue;
            }

            var proposed = cluster.Sum(vote =>
                vote.Coordinate * vote.Source.Weight) / weight;
            var junction = ResolvePerpendicularTrimJunction(
                run,
                runs,
                line,
                orientation,
                proposed,
                clusterTolerance);
            if (junction is null)
            {
                continue;
            }

            accepted.Add(new EndpointVoteCluster(junction.Value, weight));
        }

        return accepted
            .OrderBy(candidate => Math.Abs(candidate.Coordinate - current))
            .ThenByDescending(candidate => candidate.Weight)
            .Select(candidate => (double?)candidate.Coordinate)
            .FirstOrDefault();
    }

    private static bool HasStructuralEndpointConnection(
        CompactedWallRun source,
        IReadOnlyList<CompactedWallRun> runs,
        LineExport sourceLine,
        WallOrientation sourceOrientation,
        double coordinate)
    {
        var endpoint = sourceOrientation == WallOrientation.Horizontal
            ? new PointExport(coordinate, AxisCoordinate(sourceLine))
            : new PointExport(AxisCoordinate(sourceLine), coordinate);
        var point = ToPlanPoint(endpoint);
        return runs.Any(target =>
            !ReferenceEquals(target, source)
            && target.PageNumber == source.PageNumber
            && RunSupportsExteriorCornerNormalization(target)
            && (Distance(point, ToPlanPoint(target.CenterLine.Start))
                    <= EndpointSupportDistance
                || Distance(point, ToPlanPoint(target.CenterLine.End))
                    <= EndpointSupportDistance
                || PointToSegmentDistance(point, target.CenterLine)
                    <= EndpointSupportDistance));
    }

    private static bool HasSemanticEndpointSupport(
        IReadOnlyList<WallReconciliationVote> votes,
        double current,
        bool minimumEndpoint,
        double tolerance) =>
        votes.Any(vote =>
            vote.IndependentSupport
            && (vote.Kind is ReconciliationVoteKind.RoomBoundary
                or ReconciliationVoteKind.Opening)
            && Math.Abs(
                (minimumEndpoint
                    ? IntervalStart(vote.Line)
                    : IntervalEnd(vote.Line))
                - current) <= tolerance);

    private static double? ResolvePerpendicularTrimJunction(
        CompactedWallRun source,
        IReadOnlyList<CompactedWallRun> runs,
        LineExport sourceLine,
        WallOrientation sourceOrientation,
        double proposedCoordinate,
        double tolerance)
    {
        var sourceAxis = AxisCoordinate(sourceLine);
        return runs
            .Where(target =>
                !ReferenceEquals(target, source)
                && target.PageNumber == source.PageNumber
                && Orientation(target.CenterLine) != WallOrientation.Diagonal
                && Orientation(target.CenterLine) != sourceOrientation
                && RunSupportsExteriorCornerNormalization(target)
                && Math.Abs(
                    AxisCoordinate(target.CenterLine)
                    - proposedCoordinate) <= tolerance
                && sourceAxis >= IntervalStart(target.CenterLine)
                    - JunctionProjectionTolerance
                && sourceAxis <= IntervalEnd(target.CenterLine)
                    + JunctionProjectionTolerance)
            .OrderBy(target =>
                Math.Abs(
                    AxisCoordinate(target.CenterLine)
                    - proposedCoordinate))
            .ThenByDescending(RunSelectionPriority)
            .Select(target => (double?)AxisCoordinate(target.CenterLine))
            .FirstOrDefault();
    }

    private static IndependentSupport EvaluateIndependentSupport(
        IReadOnlyList<WallReconciliationVote> votes,
        double movement,
        double thickness)
    {
        var supportedVotes = votes
            .Where(vote => vote.IndependentSupport)
            .ToArray();
        var distinctKinds = supportedVotes
            .Select(vote => vote.Kind)
            .Distinct()
            .Count();
        var distinctSources = supportedVotes
            .SelectMany(vote => vote.SourceKeys)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var candidateSources = supportedVotes
            .Where(vote => vote.Kind == ReconciliationVoteKind.Candidate)
            .SelectMany(vote => vote.SourceKeys)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var semanticSources = supportedVotes
            .Where(vote => vote.Kind is ReconciliationVoteKind.RoomBoundary
                or ReconciliationVoteKind.Opening)
            .SelectMany(vote => vote.SourceKeys)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var hasSemanticGeometry = semanticSources > 0;
        var semanticCorroboration = semanticSources >= 2
            || (hasSemanticGeometry && distinctKinds >= 2);
        var candidateCorroboration = candidateSources >= 2;
        var lowAuthorityLimit = Math.Max(
            0.75,
            Math.Min(2.5, thickness * 0.20));
        var lowAuthorityCleanup = !hasSemanticGeometry
            && distinctKinds >= 2
            && distinctSources >= 2
            && Math.Abs(movement) <= lowAuthorityLimit;
        var accepted = distinctSources >= 2
            && (semanticCorroboration
                || candidateCorroboration
                || lowAuthorityCleanup);
        var evidence = accepted
            ? hasSemanticGeometry
                ? "reliable room/opening geometry independently corroborated the movement"
                : candidateCorroboration
                    ? "distinct source-wall candidates independently corroborated the movement"
                    : $"candidate/neighbor-only cleanup stayed within the {lowAuthorityLimit:0.###}-unit low-authority limit"
            : distinctSources < 2
                ? "multiple votes resolved to fewer than two independent geometry sources"
                : $"candidate/neighbor-only movement exceeded the {lowAuthorityLimit:0.###}-unit low-authority limit";

        return new IndependentSupport(
            accepted,
            distinctKinds,
            distinctSources,
            hasSemanticGeometry,
            evidence);
    }

    private static IReadOnlyList<string> CandidateSourceKeys(
        GlobalWallCandidate candidate)
    {
        if (candidate.SourceWallIds.Count > 0)
        {
            return candidate.SourceWallIds
                .Select(id => $"wall:{id}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        if (candidate.SourcePrimitiveIds.Count > 0)
        {
            return candidate.SourcePrimitiveIds
                .Select(id => $"primitive:{id}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        if (candidate.SourceWallGraphEdgeIds.Count > 0)
        {
            return candidate.SourceWallGraphEdgeIds
                .Select(id => $"graph-edge:{id}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        return [$"candidate:{candidate.Id}"];
    }

    private static IReadOnlyList<string> RunSourceKeys(CompactedWallRun run) =>
        run.Contributors
            .SelectMany(CandidateSourceKeys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static PlacementSolvedWallReconciliationExport BuildReconciliationExport(
        CompactedWallRun run,
        LineExport finalLine)
    {
        var state = run.Reconciliation
            ?? WallReconciliationState.Unchanged(run.CenterLine);
        var original = state.OriginalCenterLine;
        var originalOrientation = Orientation(original);
        var finalOrientation = Orientation(finalLine);
        var axisShift = originalOrientation == WallOrientation.Diagonal
            || finalOrientation != originalOrientation
                ? 0
                : AxisCoordinate(finalLine) - AxisCoordinate(original);
        var startDelta = IntervalStart(finalLine) - IntervalStart(original);
        var endDelta = IntervalEnd(finalLine) - IntervalEnd(original);
        var actions = new List<string>();
        if (Math.Abs(axisShift) > MinimumReconciliationMovement)
        {
            actions.Add("AxisAligned");
        }

        if (startDelta < -MinimumReconciliationMovement)
        {
            actions.Add("ExtendedStart");
        }
        else if (startDelta > MinimumReconciliationMovement)
        {
            actions.Add("TrimmedStart");
        }

        if (endDelta > MinimumReconciliationMovement)
        {
            actions.Add("ExtendedEnd");
        }
        else if (endDelta < -MinimumReconciliationMovement)
        {
            actions.Add("TrimmedEnd");
        }

        if (state.JunctionSnapCount > 0)
        {
            actions.Add("JunctionSnapped");
        }

        var preservedForReview = RunRequiresReview(run)
            && actions.Count == 0;
        if (preservedForReview)
        {
            actions.Add("PreservedForReview");
        }

        if (actions.Count == 0)
        {
            actions.Add("Unchanged");
        }

        var status = preservedForReview
            ? "PreservedForReview"
            : actions.Count == 1 && actions[0] == "Unchanged"
                ? "Unchanged"
                : "Adjusted";
        var evidence = state.Evidence
            .Append($"final axis shift {axisShift:0.###} drawing units")
            .Append($"final endpoint deltas start={startDelta:0.###}, end={endDelta:0.###}")
            .Distinct(StringComparer.Ordinal)
            .Take(24)
            .ToArray();

        return new PlacementSolvedWallReconciliationExport(
            status,
            original,
            finalLine,
            Round(axisShift),
            Round(startDelta),
            Round(endDelta),
            state.CandidateVoteCount,
            state.RoomBoundaryVoteCount,
            state.OpeningVoteCount,
            state.NeighborVoteCount,
            state.JunctionSnapCount,
            state.CollapsedDuplicateRunCount,
            Round(state.Confidence),
            actions,
            evidence);
    }

    private static PlacementWallReconciliationSummaryExport BuildReconciliationSummary(
        IReadOnlyList<PlacementSolvedWallRunExport> runs)
    {
        var reconciliations = runs.Select(run => run.Reconciliation).ToArray();
        var axisAligned = reconciliations.Count(item =>
            item.Actions.Contains("AxisAligned", StringComparer.Ordinal));
        var extendedEndpoints = reconciliations.Sum(item =>
            item.Actions.Count(action => action is "ExtendedStart" or "ExtendedEnd"));
        var trimmedEndpoints = reconciliations.Sum(item =>
            item.Actions.Count(action => action is "TrimmedStart" or "TrimmedEnd"));
        var totalAxisShift = reconciliations.Sum(item => Math.Abs(item.AxisShiftDrawingUnits));
        var maximumAxisShift = reconciliations.Length == 0
            ? 0
            : reconciliations.Max(item => Math.Abs(item.AxisShiftDrawingUnits));

        return new PlacementWallReconciliationSummaryExport(
            ReconcilerVersion,
            reconciliations.Length,
            reconciliations.Count(item => string.Equals(item.Status, "Adjusted", StringComparison.Ordinal)),
            axisAligned,
            extendedEndpoints,
            trimmedEndpoints,
            reconciliations.Sum(item => item.JunctionSnapCount),
            reconciliations.Sum(item => item.CollapsedDuplicateRunCount),
            reconciliations.Count(item => item.CandidateVoteCount > 0),
            reconciliations.Count(item => item.RoomBoundaryVoteCount > 0),
            reconciliations.Count(item => item.OpeningVoteCount > 0),
            reconciliations.Count(item => item.NeighborVoteCount > 0),
            reconciliations.Count(item => string.Equals(item.Status, "PreservedForReview", StringComparison.Ordinal)),
            Round(totalAxisShift),
            Round(maximumAxisShift),
            new[]
            {
                $"reconciler evaluated {reconciliations.Length} canonical wall run(s)",
                $"reconciler adjusted {reconciliations.Count(item => string.Equals(item.Status, "Adjusted", StringComparison.Ordinal))} run(s)",
                $"reconciler aligned {axisAligned} axis/axes and extended {extendedEndpoints} endpoint(s)",
                $"reconciler trimmed {trimmedEndpoints} endpoint(s) only where supported junction geometry required it",
                $"reconciler collapsed {reconciliations.Sum(item => item.CollapsedDuplicateRunCount)} near-coincident duplicate run(s) after alignment",
                $"reconciler retained {reconciliations.Count(item => string.Equals(item.Status, "PreservedForReview", StringComparison.Ordinal))} unresolved run(s) for review instead of deleting recall evidence"
            });
    }

    private static IReadOnlyList<LineExport> RoomBoundaryLines(PlacementRoomExport room)
    {
        if (room.Boundary.Count < 2)
        {
            return Array.Empty<LineExport>();
        }

        var lines = new List<LineExport>();
        for (var index = 0; index < room.Boundary.Count; index++)
        {
            var next = (index + 1) % room.Boundary.Count;
            var line = new LineExport(room.Boundary[index], room.Boundary[next]);
            if (LineLength(line) > 0.5)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private static bool IntervalsOverlapWithTolerance(
        LineExport first,
        LineExport second,
        double tolerance) =>
        IntervalEnd(first) + tolerance >= IntervalStart(second)
        && IntervalEnd(second) + tolerance >= IntervalStart(first);

    private static LineExport WithAxis(
        LineExport line,
        WallOrientation orientation,
        double axis) =>
        orientation == WallOrientation.Horizontal
            ? new LineExport(
                new PointExport(line.Start.X, axis),
                new PointExport(line.End.X, axis))
            : new LineExport(
                new PointExport(axis, line.Start.Y),
                new PointExport(axis, line.End.Y));

    private static LineExport WithInterval(
        LineExport line,
        WallOrientation orientation,
        double start,
        double end) =>
        orientation == WallOrientation.Horizontal
            ? new LineExport(
                new PointExport(start, AxisCoordinate(line)),
                new PointExport(end, AxisCoordinate(line)))
            : new LineExport(
                new PointExport(AxisCoordinate(line), start),
                new PointExport(AxisCoordinate(line), end));

    private static double WeightedMedian(IReadOnlyList<WallReconciliationVote> votes)
    {
        var ordered = votes
            .OrderBy(vote => AxisCoordinate(vote.Line))
            .ToArray();
        var totalWeight = ordered.Sum(vote => Math.Max(0, vote.Weight));
        if (totalWeight <= 0)
        {
            return AxisCoordinate(ordered[ordered.Length / 2].Line);
        }

        var threshold = totalWeight / 2.0;
        var cumulative = 0.0;
        foreach (var vote in ordered)
        {
            cumulative += Math.Max(0, vote.Weight);
            if (cumulative >= threshold)
            {
                return AxisCoordinate(vote.Line);
            }
        }

        return AxisCoordinate(ordered[^1].Line);
    }

    private enum ReconciliationVoteKind
    {
        Candidate = 0,
        RoomBoundary,
        Opening,
        Neighbor
    }

    private sealed record WallReconciliationVote(
        LineExport Line,
        double Weight,
        ReconciliationVoteKind Kind,
        bool IndependentSupport,
        IReadOnlyList<string> SourceKeys,
        string Evidence)
    {
        public bool SourceLinked { get; init; }
    }

    private sealed record AxisConsensus(
        double Axis,
        bool Accepted,
        double Confidence,
        string Evidence)
    {
        public static AxisConsensus Rejected(double axis, string evidence) =>
            new(axis, false, 0, evidence);
    }

    private sealed record ExtentConsensus(
        double Start,
        double End,
        string Evidence);

    private sealed record EndpointVote(
        double Coordinate,
        WallReconciliationVote Source);

    private sealed record EndpointVoteCluster(
        double Coordinate,
        double Weight);

    private sealed record IndependentSupport(
        bool Accepted,
        int DistinctKindCount,
        int DistinctSourceCount,
        bool HasSemanticGeometry,
        string Evidence);

    private sealed record WallReconciliationState(
        LineExport OriginalCenterLine,
        int CandidateVoteCount,
        int RoomBoundaryVoteCount,
        int OpeningVoteCount,
        int NeighborVoteCount,
        int JunctionSnapCount,
        int CollapsedDuplicateRunCount,
        double Confidence,
        IReadOnlyList<string> Evidence)
    {
        public static WallReconciliationState Unchanged(
            LineExport line,
            string? evidence = null) =>
            new(
                line,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                evidence is null
                    ? Array.Empty<string>()
                    : new[] { evidence });
    }
}
