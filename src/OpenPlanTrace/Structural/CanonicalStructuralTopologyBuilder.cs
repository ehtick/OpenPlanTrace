using System.Security.Cryptography;
using System.Text;

namespace OpenPlanTrace;

internal static class CanonicalStructuralTopologyBuilder
{
    public static CanonicalStructuralTopology Build(
        StructuralEvidenceGraph graph,
        IReadOnlySet<string> selectedCandidateIds,
        StructuralSolverOptions options)
    {
        var selected = graph.WallCandidates
            .Where(candidate => selectedCandidateIds.Contains(candidate.Id))
            .ToArray();
        if (selected.Length == 0)
        {
            return CanonicalStructuralTopology.Empty;
        }

        var byId = selected.ToDictionary(candidate => candidate.Id, StringComparer.Ordinal);
        var sets = new AxisConstrainedDisjointSet(selected, options);
        foreach (var relation in graph.Relations
                     .Where(relation => relation.Kind is
                         StructuralEvidenceRelationKind.Duplicate
                         or StructuralEvidenceRelationKind.Continuation)
                     .OrderByDescending(relation => relation.IsHardConstraint)
                     .ThenByDescending(relation => Math.Abs(relation.Weight))
                     .ThenBy(relation => relation.Kind)
                     .ThenBy(relation => relation.Id, StringComparer.Ordinal))
        {
            if (!byId.ContainsKey(relation.FirstCandidateId)
                || !byId.ContainsKey(relation.SecondCandidateId))
            {
                continue;
            }

            if (WouldContaminateCleanRun(
                    byId[relation.FirstCandidateId],
                    byId[relation.SecondCandidateId]))
            {
                continue;
            }

            sets.TryUnion(relation.FirstCandidateId, relation.SecondCandidateId);
        }

        var leafRuns = selected
            .GroupBy(candidate => sets.Find(candidate.Id), StringComparer.Ordinal)
            .Select(group => BuildRun(group.ToArray(), options))
            .OrderBy(run => run.PageNumber)
            .ThenBy(run => run.Bounds.Y)
            .ThenBy(run => run.Bounds.X)
            .ThenBy(run => run.Id, StringComparer.Ordinal)
            .ToArray();
        var runs = ResolvePhysicalWallAssemblies(leafRuns, options);
        var junctions = BuildJunctions(graph, runs, options);
        return new CanonicalStructuralTopology(runs, junctions);
    }

    private static bool WouldContaminateCleanRun(
        StructuralWallCandidate first,
        StructuralWallCandidate second)
    {
        var firstUnresolved = HasUnresolvedBlockingEvidence(first);
        var secondUnresolved = HasUnresolvedBlockingEvidence(second);
        return firstUnresolved != secondUnresolved;
    }

    private static bool HasUnresolvedBlockingEvidence(
        StructuralWallCandidate candidate) =>
        candidate.HasAbsoluteBlockingEvidence
        || (candidate.HasStrongNegativeEvidence
            && !candidate.HasIndependentWallBodyEvidence
            && !candidate.HasCrossDomainWallBodyEvidence);

    private static StructuralWallRun BuildRun(
        IReadOnlyList<StructuralWallCandidate> candidates,
        StructuralSolverOptions options)
    {
        var reference = candidates
            .OrderByDescending(candidate => CandidateFitWeight(candidate))
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .First();
        var direction = StructuralGeometry.UnitDirection(reference.CenterLine);
        var normal = new PlanVector(-direction.Y, direction.X);
        var weightedAxes = candidates
            .Select(candidate => (
                Value: StructuralGeometry.Dot(candidate.CenterLine.Midpoint, normal),
                Weight: CandidateFitWeight(candidate)))
            .ToArray();
        var axis = WeightedMedian(weightedAxes);
        var projections = candidates
            .SelectMany(candidate => new[]
            {
                StructuralGeometry.Dot(candidate.CenterLine.Start, direction),
                StructuralGeometry.Dot(candidate.CenterLine.End, direction)
            })
            .ToArray();
        var startProjection = projections.Min();
        var endProjection = projections.Max();
        var centerLine = StructuralGeometry.Canonicalize(
            new PlanLineSegment(
                FromBasis(startProjection, axis, direction, normal),
                FromBasis(endProjection, axis, direction, normal)));
        var thickness = WeightedMedian(candidates.Select(candidate => (
            Value: Math.Max(0.5, candidate.Thickness),
            Weight: CandidateFitWeight(candidate))).ToArray());
        var wallType = ResolveWallType(candidates);
        var totalWeight = candidates.Sum(CandidateFitWeight);
        var confidence = new Confidence(
            totalWeight <= 0
                ? candidates.Average(candidate => candidate.Confidence.Value)
                : candidates.Sum(candidate => candidate.Confidence.Value * CandidateFitWeight(candidate)) / totalWeight);
        var candidateIds = candidates.Select(candidate => candidate.Id).Order(StringComparer.Ordinal).ToArray();
        var runId = $"structural-run:{StableId(candidateIds)}";

        return new StructuralWallRun(
            runId,
            reference.PageNumber,
            centerLine,
            thickness,
            wallType,
            confidence,
            candidateIds,
            candidates.SelectMany(candidate => candidate.SourceWallIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            candidates.SelectMany(candidate => candidate.SourceWallGraphEdgeIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            candidates.SelectMany(candidate => candidate.SourcePrimitiveIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            candidates.SelectMany(candidate => candidate.SourceRoomIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            candidates.SelectMany(candidate => candidate.SourceOpeningIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            candidates.SelectMany(candidate => candidate.Evidence)
                .Distinct(StringComparer.Ordinal)
                .Append($"compacted {candidates.Count} selected structural candidate(s)")
                .Append($"robust axis consensus used {candidates.Count} weighted observation(s)")
                .Order(StringComparer.Ordinal)
                .ToArray())
        {
            SourceWallComponentIds = candidates
                .SelectMany(candidate => candidate.SourceWallComponentIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            Reliability = StructuralWallRunReadinessEvaluator.Evaluate(candidates, options)
        };
    }

    private static IReadOnlyList<StructuralWallRun> ResolvePhysicalWallAssemblies(
        IReadOnlyList<StructuralWallRun> leafRuns,
        StructuralSolverOptions options)
    {
        if (leafRuns.Count < 2)
        {
            return leafRuns;
        }

        var pairs = new List<PhysicalWallAssemblyPair>();
        for (var firstIndex = 0; firstIndex < leafRuns.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < leafRuns.Count; secondIndex++)
            {
                var pair = EvaluatePhysicalWallAssemblyPair(
                    leafRuns[firstIndex],
                    leafRuns[secondIndex],
                    options);
                if (pair is not null)
                {
                    pairs.Add(pair);
                }
            }
        }

        if (pairs.Count == 0)
        {
            return leafRuns;
        }

        var consumedRunIds = new HashSet<string>(StringComparer.Ordinal);
        var assemblies = new List<StructuralWallRun>();
        foreach (var pair in pairs
                     .OrderByDescending(item => item.OverlapRatio)
                     .ThenBy(item => item.BodyGap)
                     .ThenBy(item => item.EnvelopeDepth)
                     .ThenBy(item => item.First.Id, StringComparer.Ordinal)
                     .ThenBy(item => item.Second.Id, StringComparer.Ordinal))
        {
            if (consumedRunIds.Contains(pair.First.Id)
                || consumedRunIds.Contains(pair.Second.Id))
            {
                continue;
            }

            assemblies.Add(MergePhysicalWallAssembly(pair));
            consumedRunIds.Add(pair.First.Id);
            consumedRunIds.Add(pair.Second.Id);
        }

        foreach (var leaf in leafRuns
                     .Where(run => !consumedRunIds.Contains(run.Id))
                     .OrderBy(run => run.Id, StringComparer.Ordinal)
                     .ToArray())
        {
            var match = assemblies
                .Select((assembly, index) => (
                    Index: index,
                    Pair: EvaluatePhysicalWallAssemblyPair(
                        assembly,
                        leaf,
                        options,
                        allowContainedAssemblyLeaf: true)))
                .Where(item => item.Pair is not null)
                .OrderByDescending(item => item.Pair!.OverlapRatio)
                .ThenBy(item => item.Pair!.EnvelopeDepth)
                .ThenBy(item => item.Pair!.BodyGap)
                .ThenBy(item => item.Index)
                .FirstOrDefault();
            if (match.Pair is null)
            {
                continue;
            }

            assemblies[match.Index] = MergePhysicalWallAssembly(match.Pair);
            consumedRunIds.Add(leaf.Id);
        }

        return leafRuns
            .Where(run => !consumedRunIds.Contains(run.Id))
            .Concat(assemblies)
            .OrderBy(run => run.PageNumber)
            .ThenBy(run => run.Bounds.Y)
            .ThenBy(run => run.Bounds.X)
            .ThenBy(run => run.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static PhysicalWallAssemblyPair? EvaluatePhysicalWallAssemblyPair(
        StructuralWallRun first,
        StructuralWallRun second,
        StructuralSolverOptions options,
        bool allowContainedAssemblyLeaf = false)
    {
        var hasExteriorAssemblySemantics =
            HasExteriorSemantics(first)
            && HasExteriorSemantics(second)
            && (first.WallType == WallType.Exterior
                || second.WallType == WallType.Exterior);
        var hasSharedPhysicalSourceSupport =
            SharesPhysicalSourcePrimitives(first, second);
        if (first.PageNumber != second.PageNumber
            || !first.Reliability.ReadyForCoordinatePlacement
            || !second.Reliability.ReadyForCoordinatePlacement
            || first.Reliability.RequiresReview
            || second.Reliability.RequiresReview
            || !HasPhysicalWallBodyEvidence(first)
            || !HasPhysicalWallBodyEvidence(second)
            || (!hasExteriorAssemblySemantics
                && !hasSharedPhysicalSourceSupport)
            || !SharesWallGraphComponent(first, second)
            || !IsAxisAligned(first.CenterLine, options.AngleToleranceDegrees)
            || !IsAxisAligned(second.CenterLine, options.AngleToleranceDegrees))
        {
            return null;
        }

        var angleTolerance =
            options.AngleToleranceDegrees * Math.PI / 180.0;
        if (!StructuralGeometry.AreParallel(
                first.CenterLine,
                second.CenterLine,
                angleTolerance))
        {
            return null;
        }

        var overlapRatio = StructuralGeometry.OverlapRatio(
            first.CenterLine,
            second.CenterLine);
        if (overlapRatio < Math.Max(0.80, options.DuplicateOverlapRatio))
        {
            return null;
        }

        var direction = StructuralGeometry.UnitDirection(
            first.DrawingLength >= second.DrawingLength
                ? first.CenterLine
                : second.CenterLine);
        var normal = new PlanVector(-direction.Y, direction.X);
        var firstAxis = StructuralGeometry.Dot(first.CenterLine.Midpoint, normal);
        var secondAxis = StructuralGeometry.Dot(second.CenterLine.Midpoint, normal);
        var axisDistance = Math.Abs(firstAxis - secondAxis);
        if (axisDistance <= options.AxisTolerance
            && !allowContainedAssemblyLeaf)
        {
            return null;
        }

        var firstMinimum = firstAxis - (first.Thickness / 2.0);
        var firstMaximum = firstAxis + (first.Thickness / 2.0);
        var secondMinimum = secondAxis - (second.Thickness / 2.0);
        var secondMaximum = secondAxis + (second.Thickness / 2.0);
        var bodyGap = Math.Max(
            0,
            Math.Max(firstMinimum, secondMinimum)
            - Math.Min(firstMaximum, secondMaximum));
        var envelopeDepth =
            Math.Max(firstMaximum, secondMaximum)
            - Math.Min(firstMinimum, secondMinimum);
        var maximumBodyGap = hasSharedPhysicalSourceSupport
            ? Math.Max(1, options.AxisTolerance * 2.5)
            : Math.Max(1, options.AxisTolerance * 2.0);
        if (bodyGap > maximumBodyGap
            || envelopeDepth > Math.Max(8, options.AxisTolerance * 6.0))
        {
            return null;
        }

        if (allowContainedAssemblyLeaf)
        {
            var assembly = first.AssemblyLeafCount > 1
                ? first
                : second.AssemblyLeafCount > 1
                    ? second
                    : null;
            if (assembly is null
                || bodyGap > 0.5
                || envelopeDepth - assembly.Thickness > options.AxisTolerance)
            {
                return null;
            }
        }

        return new PhysicalWallAssemblyPair(
            first,
            second,
            direction,
            normal,
            overlapRatio,
            bodyGap,
            envelopeDepth,
            hasExteriorAssemblySemantics,
            hasSharedPhysicalSourceSupport);
    }

    private static StructuralWallRun MergePhysicalWallAssembly(
        PhysicalWallAssemblyPair pair)
    {
        var leaves = new[] { pair.First, pair.Second };
        var axes = leaves
            .Select(run => StructuralGeometry.Dot(
                run.CenterLine.Midpoint,
                pair.Normal))
            .ToArray();
        var minimumBodyAxis = leaves
            .Select((run, index) => axes[index] - (run.Thickness / 2.0))
            .Min();
        var maximumBodyAxis = leaves
            .Select((run, index) => axes[index] + (run.Thickness / 2.0))
            .Max();
        var assemblyAxis = (minimumBodyAxis + maximumBodyAxis) / 2.0;
        var projections = leaves
            .SelectMany(run => new[]
            {
                StructuralGeometry.Dot(run.CenterLine.Start, pair.Direction),
                StructuralGeometry.Dot(run.CenterLine.End, pair.Direction)
            })
            .ToArray();
        var centerLine = StructuralGeometry.Canonicalize(
            new PlanLineSegment(
                FromBasis(
                    projections.Min(),
                    assemblyAxis,
                    pair.Direction,
                    pair.Normal),
                FromBasis(
                    projections.Max(),
                    assemblyAxis,
                    pair.Direction,
                    pair.Normal)));
        var candidateIds = leaves
            .SelectMany(run => run.CandidateIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var confidenceWeight = Math.Max(1, leaves.Sum(run => run.DrawingLength));
        var confidence = new Confidence(
            leaves.Sum(run =>
                run.Confidence.Value * Math.Max(1, run.DrawingLength))
            / confidenceWeight);
        var evidence = leaves
            .SelectMany(run => run.Evidence)
            .Distinct(StringComparer.Ordinal)
            .Append(
                pair.HasExteriorSemantics
                    ? $"resolved exterior wall assembly from {leaves.Sum(run => run.AssemblyLeafCount)} structural leaves"
                    : $"resolved shared-source physical wall assembly from {leaves.Sum(run => run.AssemblyLeafCount)} structural leaves")
            .Append($"assembly overlap ratio {pair.OverlapRatio:0.###}")
            .Append($"assembly physical body gap {pair.BodyGap:0.###} drawing units")
            .Append($"assembly envelope thickness {pair.EnvelopeDepth:0.###} drawing units")
            .Concat(pair.HasSharedPhysicalSourceSupport
                ? new[] { "assembly leaves share source primitives from the same physical wall body" }
                : Array.Empty<string>())
            .Order(StringComparer.Ordinal)
            .ToArray();
        var wallType = pair.HasExteriorSemantics
            ? WallType.Exterior
            : pair.First.WallType == pair.Second.WallType
                ? pair.First.WallType
                : WallType.Unknown;

        return new StructuralWallRun(
            $"structural-run:{StableId(candidateIds)}",
            pair.First.PageNumber,
            centerLine,
            pair.EnvelopeDepth,
            wallType,
            confidence,
            candidateIds,
            Union(leaves.SelectMany(run => run.SourceWallIds)),
            Union(leaves.SelectMany(run => run.SourceWallGraphEdgeIds)),
            Union(leaves.SelectMany(run => run.SourcePrimitiveIds)),
            Union(leaves.SelectMany(run => run.SourceRoomIds)),
            Union(leaves.SelectMany(run => run.SourceOpeningIds)),
            evidence)
        {
            AssemblyLeafCount = leaves.Sum(run => run.AssemblyLeafCount),
            SourceWallComponentIds = Union(
                leaves.SelectMany(run => run.SourceWallComponentIds)),
            Reliability = new StructuralWallRunReliability(
                ReadyForCoordinatePlacement: leaves.All(
                    run => run.Reliability.ReadyForCoordinatePlacement),
                RequiresReview: leaves.Any(
                    run => run.Reliability.RequiresReview),
                Confidence: leaves.Average(
                    run => run.Reliability.Confidence),
                Reasons: Union(
                    leaves
                        .SelectMany(run => run.Reliability.Reasons)
                        .Append(
                            pair.HasExteriorSemantics
                                ? "source-backed exterior leaves resolved as one physical wall assembly"
                                : "shared-source wall-body leaves resolved as one physical wall assembly")))
        };
    }

    private static bool HasPhysicalWallBodyEvidence(
        StructuralWallRun run) =>
        run.Evidence.Any(item =>
            item.Contains(
                "parallel wall-face pair",
                StringComparison.OrdinalIgnoreCase)
            || item.Contains(
                "filled wall-solid primitive",
                StringComparison.OrdinalIgnoreCase)
            || item.Contains(
                "filled closed vector wall body",
                StringComparison.OrdinalIgnoreCase));

    private static bool HasExteriorSemantics(
        StructuralWallRun run) =>
        run.WallType == WallType.Exterior
        || run.Evidence.Any(item =>
            item.Contains(
                "wall type exterior",
                StringComparison.OrdinalIgnoreCase)
            || item.Contains(
                "exterior shell",
                StringComparison.OrdinalIgnoreCase)
            || item.Contains(
                "source-backed exterior",
                StringComparison.OrdinalIgnoreCase));

    private static bool SharesWallGraphComponent(
        StructuralWallRun first,
        StructuralWallRun second) =>
        first.SourceWallComponentIds.Count > 0
        && second.SourceWallComponentIds.Count > 0
        && first.SourceWallComponentIds.Intersect(
            second.SourceWallComponentIds,
            StringComparer.Ordinal).Any();

    private static bool SharesPhysicalSourcePrimitives(
        StructuralWallRun first,
        StructuralWallRun second)
    {
        if (first.SourcePrimitiveIds.Count < 2
            || second.SourcePrimitiveIds.Count < 2)
        {
            return false;
        }

        var firstIds = first.SourcePrimitiveIds.ToHashSet(StringComparer.Ordinal);
        return second.SourcePrimitiveIds.Count(firstIds.Contains) >= 2;
    }

    private static bool IsAxisAligned(
        PlanLineSegment line,
        double toleranceDegrees)
    {
        var angle = StructuralGeometry.NormalizeAngle(line.AngleRadians);
        var tolerance = toleranceDegrees * Math.PI / 180.0;
        return Math.Min(
            Math.Min(angle, Math.Abs(Math.PI - angle)),
            Math.Abs((Math.PI / 2.0) - angle)) <= tolerance;
    }

    private static string[] Union(IEnumerable<string> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<StructuralJunction> BuildJunctions(
        StructuralEvidenceGraph graph,
        IReadOnlyList<StructuralWallRun> runs,
        StructuralSolverOptions options)
    {
        var runIdsByCandidate = runs
            .SelectMany(run => run.CandidateIds.Select(candidateId => (CandidateId: candidateId, RunId: run.Id)))
            .GroupBy(item => item.CandidateId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.RunId).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var drafts = new List<JunctionDraft>();

        foreach (var source in graph.Junctions)
        {
            var incidentRunIds = source.CandidateIds
                .Where(runIdsByCandidate.ContainsKey)
                .SelectMany(candidateId => runIdsByCandidate[candidateId])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (incidentRunIds.Length == 0)
            {
                continue;
            }

            AddJunction(
                drafts,
                new JunctionDraft(
                    source.PageNumber,
                    source.Position,
                    MapKind(source.Kind, incidentRunIds.Length),
                    incidentRunIds,
                    new[] { source.Id },
                    source.Confidence,
                    RequiresReview: source.Confidence.Value < 0.5,
                    source.Evidence),
                options.EndpointTolerance);
        }

        foreach (var run in runs)
        {
            AddJunction(
                drafts,
                EndpointDraft(run, run.CenterLine.Start),
                options.EndpointTolerance);
            AddJunction(
                drafts,
                EndpointDraft(run, run.CenterLine.End),
                options.EndpointTolerance);
        }

        foreach (var pageRuns in runs.GroupBy(run => run.PageNumber))
        {
            var values = pageRuns.ToArray();
            for (var firstIndex = 0; firstIndex < values.Length; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < values.Length; secondIndex++)
                {
                    var first = values[firstIndex];
                    var second = values[secondIndex];
                    if (!TrySegmentIntersection(
                            first.CenterLine,
                            second.CenterLine,
                            options.EndpointTolerance,
                            out var intersection,
                            out var firstParameter,
                            out var secondParameter))
                    {
                        continue;
                    }

                    var firstInterior = firstParameter > 0.01 && firstParameter < 0.99;
                    var secondInterior = secondParameter > 0.01 && secondParameter < 0.99;
                    var kind = firstInterior && secondInterior
                        ? StructuralJunctionKind.Cross
                        : firstInterior || secondInterior
                            ? StructuralJunctionKind.Tee
                            : StructuralJunctionKind.Corner;
                    AddJunction(
                        drafts,
                        new JunctionDraft(
                            first.PageNumber,
                            intersection,
                            kind,
                            new[] { first.Id, second.Id },
                            Array.Empty<string>(),
                            new Confidence(Math.Min(first.Confidence.Value, second.Confidence.Value)),
                            RequiresReview: false,
                            new[]
                            {
                                $"geometric {kind.ToString().ToLowerInvariant()} intersection",
                                "wall runs remain unsplit; junction is represented as topology"
                            }),
                        options.EndpointTolerance);
                }
            }
        }

        return drafts
            .Select((draft, index) => draft.Build(index))
            .OrderBy(junction => junction.PageNumber)
            .ThenBy(junction => junction.Position.Y)
            .ThenBy(junction => junction.Position.X)
            .ThenBy(junction => junction.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static JunctionDraft EndpointDraft(
        StructuralWallRun run,
        PlanPoint point) =>
        new(
            run.PageNumber,
            point,
            StructuralJunctionKind.Endpoint,
            new[] { run.Id },
            Array.Empty<string>(),
            run.Confidence,
            RequiresReview: false,
            new[] { $"canonical endpoint for wall run {run.Id}" });

    private static void AddJunction(
        IList<JunctionDraft> drafts,
        JunctionDraft incoming,
        double tolerance)
    {
        var existing = drafts
            .Where(draft => draft.PageNumber == incoming.PageNumber)
            .Select((draft, index) => (Draft: draft, Index: index))
            .Where(item => item.Draft.Position.DistanceTo(incoming.Position) <= tolerance)
            .OrderBy(item => item.Draft.Position.DistanceTo(incoming.Position))
            .ThenBy(item => item.Index)
            .FirstOrDefault();
        if (existing.Draft is null)
        {
            drafts.Add(incoming);
            return;
        }

        drafts[existing.Index] = existing.Draft.Merge(incoming);
    }

    private static bool TrySegmentIntersection(
        PlanLineSegment first,
        PlanLineSegment second,
        double tolerance,
        out PlanPoint intersection,
        out double firstParameter,
        out double secondParameter)
    {
        intersection = default;
        firstParameter = 0;
        secondParameter = 0;

        var p = first.Start;
        var q = second.Start;
        var r = first.Vector;
        var s = second.Vector;
        var denominator = r.Cross(s);
        if (Math.Abs(denominator) <= 1e-8)
        {
            return false;
        }

        var qMinusP = q - p;
        firstParameter = qMinusP.Cross(s) / denominator;
        secondParameter = qMinusP.Cross(r) / denominator;
        var firstTolerance = tolerance / Math.Max(first.Length, 1);
        var secondTolerance = tolerance / Math.Max(second.Length, 1);
        if (firstParameter < -firstTolerance
            || firstParameter > 1 + firstTolerance
            || secondParameter < -secondTolerance
            || secondParameter > 1 + secondTolerance)
        {
            return false;
        }

        firstParameter = Math.Clamp(firstParameter, 0, 1);
        secondParameter = Math.Clamp(secondParameter, 0, 1);
        intersection = first.PointAt(firstParameter);
        return true;
    }

    private static StructuralJunctionKind MapKind(
        WallNodeKind kind,
        int incidentCount) =>
        kind switch
        {
            WallNodeKind.Crossing => StructuralJunctionKind.Cross,
            WallNodeKind.TJunction => StructuralJunctionKind.Tee,
            WallNodeKind.Corner => StructuralJunctionKind.Corner,
            WallNodeKind.Inline => StructuralJunctionKind.Inline,
            WallNodeKind.Endpoint => StructuralJunctionKind.Endpoint,
            _ when incidentCount >= 4 => StructuralJunctionKind.Cross,
            _ when incidentCount == 3 => StructuralJunctionKind.Tee,
            _ when incidentCount == 2 => StructuralJunctionKind.Corner,
            _ => StructuralJunctionKind.Endpoint
        };

    private static WallType ResolveWallType(
        IReadOnlyList<StructuralWallCandidate> candidates)
    {
        var exterior = candidates
            .Where(candidate => candidate.WallType == WallType.Exterior)
            .Sum(CandidateFitWeight);
        var interior = candidates
            .Where(candidate => candidate.WallType == WallType.Interior)
            .Sum(CandidateFitWeight);
        if (exterior > interior && exterior > 0)
        {
            return WallType.Exterior;
        }

        return interior > 0 ? WallType.Interior : WallType.Unknown;
    }

    private static double CandidateFitWeight(
        StructuralWallCandidate candidate) =>
        Math.Max(0.10, candidate.Confidence.Value)
        * Math.Max(1, candidate.DrawingLength)
        * Math.Max(0.20, 1 + candidate.UnaryScore);

    private static double WeightedMedian(
        IReadOnlyList<(double Value, double Weight)> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var ordered = values.OrderBy(item => item.Value).ToArray();
        var totalWeight = ordered.Sum(item => Math.Max(0, item.Weight));
        if (totalWeight <= 0)
        {
            return ordered[ordered.Length / 2].Value;
        }

        var threshold = totalWeight / 2.0;
        var cumulative = 0.0;
        foreach (var item in ordered)
        {
            cumulative += Math.Max(0, item.Weight);
            if (cumulative >= threshold)
            {
                return item.Value;
            }
        }

        return ordered[^1].Value;
    }

    private static PlanPoint FromBasis(
        double along,
        double across,
        PlanVector direction,
        PlanVector normal) =>
        new(
            (direction.X * along) + (normal.X * across),
            (direction.Y * along) + (normal.Y * across));

    private static string StableId(IEnumerable<string> values)
    {
        var content = string.Join("\n", values.Order(StringComparer.Ordinal));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private sealed record PhysicalWallAssemblyPair(
        StructuralWallRun First,
        StructuralWallRun Second,
        PlanVector Direction,
        PlanVector Normal,
        double OverlapRatio,
        double BodyGap,
        double EnvelopeDepth,
        bool HasExteriorSemantics,
        bool HasSharedPhysicalSourceSupport);

    private sealed record JunctionDraft(
        int PageNumber,
        PlanPoint Position,
        StructuralJunctionKind Kind,
        IReadOnlyList<string> IncidentWallRunIds,
        IReadOnlyList<string> SourceJunctionCandidateIds,
        Confidence Confidence,
        bool RequiresReview,
        IReadOnlyList<string> Evidence)
    {
        public JunctionDraft Merge(JunctionDraft other)
        {
            var totalConfidence = Math.Max(0.01, Confidence.Value + other.Confidence.Value);
            var position = new PlanPoint(
                ((Position.X * Confidence.Value) + (other.Position.X * other.Confidence.Value)) / totalConfidence,
                ((Position.Y * Confidence.Value) + (other.Position.Y * other.Confidence.Value)) / totalConfidence);
            return new JunctionDraft(
                PageNumber,
                position,
                StrongerKind(Kind, other.Kind),
                IncidentWallRunIds.Concat(other.IncidentWallRunIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                SourceJunctionCandidateIds.Concat(other.SourceJunctionCandidateIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                new Confidence(Math.Max(Confidence.Value, other.Confidence.Value)),
                RequiresReview || other.RequiresReview,
                Evidence.Concat(other.Evidence).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        }

        public StructuralJunction Build(int index)
        {
            var incidentCount = IncidentWallRunIds.Count;
            var resolvedKind = Kind switch
            {
                StructuralJunctionKind.Endpoint when incidentCount >= 4 => StructuralJunctionKind.Cross,
                StructuralJunctionKind.Endpoint when incidentCount == 3 => StructuralJunctionKind.Tee,
                StructuralJunctionKind.Endpoint when incidentCount == 2 => StructuralJunctionKind.Corner,
                _ => Kind
            };
            return new StructuralJunction(
                $"structural-node:p{PageNumber}:{index}",
                PageNumber,
                new PlanPoint(Round(Position.X), Round(Position.Y)),
                resolvedKind,
                IncidentWallRunIds,
                SourceJunctionCandidateIds,
                Confidence,
                RequiresReview,
                Evidence);
        }

        private static StructuralJunctionKind StrongerKind(
            StructuralJunctionKind first,
            StructuralJunctionKind second) =>
            Priority(first) >= Priority(second) ? first : second;

        private static int Priority(StructuralJunctionKind kind) =>
            kind switch
            {
                StructuralJunctionKind.Cross => 5,
                StructuralJunctionKind.Tee => 4,
                StructuralJunctionKind.Corner => 3,
                StructuralJunctionKind.Inline => 2,
                StructuralJunctionKind.Endpoint => 1,
                _ => 0
            };

        private static double Round(double value) =>
            Math.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private sealed class AxisConstrainedDisjointSet
    {
        private readonly Dictionary<string, string> _parents;
        private readonly Dictionary<string, ComponentBounds> _bounds;
        private readonly double _maximumAxisSpread;

        public AxisConstrainedDisjointSet(
            IReadOnlyList<StructuralWallCandidate> candidates,
            StructuralSolverOptions options)
        {
            var angleTolerance =
                options.AngleToleranceDegrees * Math.PI / 180.0;
            _maximumAxisSpread = Math.Max(0.25, options.AxisTolerance);
            _parents = candidates.ToDictionary(
                candidate => candidate.Id,
                candidate => candidate.Id,
                StringComparer.Ordinal);
            _bounds = candidates.ToDictionary(
                candidate => candidate.Id,
                candidate =>
                {
                    var axis = StructuralGeometry.AxisCoordinate(
                        candidate.CenterLine);
                    return new ComponentBounds(
                        candidate.PageNumber,
                        StructuralGeometry.OrientationBucket(
                            candidate.CenterLine,
                            angleTolerance),
                        axis,
                        axis);
                },
                StringComparer.Ordinal);
        }

        public string Find(string value)
        {
            var parent = _parents[value];
            if (!string.Equals(parent, value, StringComparison.Ordinal))
            {
                _parents[value] = Find(parent);
            }

            return _parents[value];
        }

        public bool TryUnion(string first, string second)
        {
            var firstRoot = Find(first);
            var secondRoot = Find(second);
            if (string.Equals(firstRoot, secondRoot, StringComparison.Ordinal))
            {
                return true;
            }

            var firstBounds = _bounds[firstRoot];
            var secondBounds = _bounds[secondRoot];
            var combinedMinimumAxis = Math.Min(
                firstBounds.MinimumAxis,
                secondBounds.MinimumAxis);
            var combinedMaximumAxis = Math.Max(
                firstBounds.MaximumAxis,
                secondBounds.MaximumAxis);
            if (firstBounds.PageNumber != secondBounds.PageNumber
                || firstBounds.OrientationBucket != secondBounds.OrientationBucket
                || combinedMaximumAxis - combinedMinimumAxis
                > _maximumAxisSpread)
            {
                return false;
            }

            var retainedRoot = string.CompareOrdinal(firstRoot, secondRoot) <= 0
                ? firstRoot
                : secondRoot;
            var mergedRoot = string.Equals(
                retainedRoot,
                firstRoot,
                StringComparison.Ordinal)
                ? secondRoot
                : firstRoot;
            _parents[mergedRoot] = retainedRoot;
            _bounds[retainedRoot] = firstBounds with
            {
                MinimumAxis = combinedMinimumAxis,
                MaximumAxis = combinedMaximumAxis
            };
            _bounds.Remove(mergedRoot);
            return true;
        }

        private sealed record ComponentBounds(
            int PageNumber,
            int OrientationBucket,
            double MinimumAxis,
            double MaximumAxis);
    }
}

internal sealed record CanonicalStructuralTopology(
    IReadOnlyList<StructuralWallRun> WallRuns,
    IReadOnlyList<StructuralJunction> Junctions)
{
    public static CanonicalStructuralTopology Empty { get; } =
        new(Array.Empty<StructuralWallRun>(), Array.Empty<StructuralJunction>());
}
