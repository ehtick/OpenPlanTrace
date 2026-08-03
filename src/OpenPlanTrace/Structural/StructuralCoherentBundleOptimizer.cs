namespace OpenPlanTrace;

internal enum StructuralBundleKind
{
    RoomClosure,
    Continuation
}

internal sealed record StructuralBundleOptimizationResult(
    int CompletedPassCount,
    int AcceptedBundleCount,
    int AddedCandidateCount,
    int AcceptedRoomClosureBundleCount,
    int AcceptedContinuationBundleCount)
{
    public static StructuralBundleOptimizationResult Empty { get; } =
        new(0, 0, 0, 0, 0);
}

internal static class StructuralCoherentBundleOptimizer
{
    public static StructuralBundleOptimizationResult Optimize(
        StructuralEvidenceGraph graph,
        IReadOnlyList<StructuralWallCandidate> considered,
        HashSet<string> selected,
        StructuralObjective objective,
        StructuralSolverOptions options)
    {
        if (!options.EnableCoherentBundleOptimization
            || options.MaximumCoherentBundlePasses <= 0
            || considered.Count < 2)
        {
            return StructuralBundleOptimizationResult.Empty;
        }

        var candidatesById = considered.ToDictionary(
            candidate => candidate.Id,
            StringComparer.Ordinal);
        var hardConflicts = BuildHardConflictLookup(graph.Relations);
        var acceptedBundleCount = 0;
        var addedCandidateCount = 0;
        var acceptedRoomBundleCount = 0;
        var acceptedContinuationBundleCount = 0;
        var completedPasses = 0;

        for (var pass = 0; pass < options.MaximumCoherentBundlePasses; pass++)
        {
            var proposals = BuildRoomClosureBundles(
                    graph,
                    candidatesById,
                    selected,
                    objective,
                    options,
                    hardConflicts)
                .Concat(BuildContinuationBundles(
                    graph,
                    candidatesById,
                    selected,
                    options,
                    hardConflicts))
                .GroupBy(proposal => proposal.StableKey, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderByDescending(proposal => proposal.StructuralPriority)
                .ThenBy(proposal => proposal.Kind)
                .ThenBy(proposal => proposal.StableKey, StringComparer.Ordinal)
                .ToArray();

            var changed = false;
            foreach (var proposal in proposals)
            {
                var additions = proposal.CandidateIds
                    .Where(candidateId => !selected.Contains(candidateId))
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                if (additions.Length < 2
                    || additions.Length > options.MaximumCoherentBundleSize
                    || HasHardConflict(additions, selected, hardConflicts))
                {
                    continue;
                }

                var before = objective.Evaluate(selected);
                var tentative = new HashSet<string>(selected, StringComparer.Ordinal);
                tentative.UnionWith(additions);
                var after = objective.Evaluate(tentative);
                if (after - before <= options.ObjectiveImprovementTolerance)
                {
                    continue;
                }

                selected.UnionWith(additions);
                changed = true;
                acceptedBundleCount++;
                addedCandidateCount += additions.Length;
                if (proposal.Kind == StructuralBundleKind.RoomClosure)
                {
                    acceptedRoomBundleCount++;
                }
                else
                {
                    acceptedContinuationBundleCount++;
                }
            }

            completedPasses++;
            if (!changed)
            {
                break;
            }
        }

        return new StructuralBundleOptimizationResult(
            completedPasses,
            acceptedBundleCount,
            addedCandidateCount,
            acceptedRoomBundleCount,
            acceptedContinuationBundleCount);
    }

    private static IReadOnlyList<StructuralBundleProposal> BuildRoomClosureBundles(
        StructuralEvidenceGraph graph,
        IReadOnlyDictionary<string, StructuralWallCandidate> candidatesById,
        IReadOnlySet<string> selected,
        StructuralObjective objective,
        StructuralSolverOptions options,
        IReadOnlyDictionary<string, IReadOnlySet<string>> hardConflicts)
    {
        var proposals = new List<StructuralBundleProposal>();
        foreach (var loop in graph.RoomLoops
                     .Where(loop =>
                         loop.Context == StructuralRoomLoopContext.Indoor
                         && loop.Weight > 0
                         && objective.RoomBoundaryCoverage(loop, selected) < 0.90)
                     .OrderByDescending(loop => loop.Weight)
                     .ThenByDescending(loop => loop.Confidence.Value)
                     .ThenBy(loop => loop.Id, StringComparer.Ordinal))
        {
            var missingEdges = loop.BoundaryEdges
                .Where(edge => !edge.CandidateIds.Any(selected.Contains))
                .OrderBy(edge => edge.CandidateIds.Count)
                .ThenByDescending(edge => edge.DrawingLength)
                .ThenBy(edge => edge.Id, StringComparer.Ordinal)
                .ToArray();
            if (missingEdges.Length < 2
                || missingEdges.Length > options.MaximumCoherentBundleSize)
            {
                continue;
            }

            var tentative = new HashSet<string>(selected, StringComparer.Ordinal);
            var additions = new HashSet<string>(StringComparer.Ordinal);
            var complete = true;
            foreach (var edge in missingEdges)
            {
                var match = edge.CandidateIds
                    .Where(candidatesById.ContainsKey)
                    .Select(candidateId => candidatesById[candidateId])
                    .Where(candidate =>
                        IsSafeBundleCandidate(candidate, options)
                        && StructuralPlacementAuthorityEvaluator.CanParticipateInRecoveryBundle(candidate)
                        && !HasHardConflict(
                            new[] { candidate.Id },
                            tentative,
                            hardConflicts))
                    .OrderByDescending(candidate =>
                        objective.ToggleDelta(candidate.Id, tentative, add: true))
                    .ThenByDescending(BundleCandidatePriority)
                    .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (match is null)
                {
                    complete = false;
                    break;
                }

                additions.Add(match.Id);
                tentative.Add(match.Id);
            }

            if (!complete
                || additions.Count < 2
                || additions.Count > options.MaximumCoherentBundleSize
                || objective.RoomBoundaryCoverage(loop, tentative) < 0.90)
            {
                continue;
            }

            proposals.Add(
                new StructuralBundleProposal(
                    StructuralBundleKind.RoomClosure,
                    additions.Order(StringComparer.Ordinal).ToArray(),
                    StructuralPriority:
                        (loop.Weight * 10)
                        + missingEdges.Sum(edge => edge.DrawingLength)));
        }

        return proposals;
    }

    private static IReadOnlyList<StructuralBundleProposal> BuildContinuationBundles(
        StructuralEvidenceGraph graph,
        IReadOnlyDictionary<string, StructuralWallCandidate> candidatesById,
        IReadOnlySet<string> selected,
        StructuralSolverOptions options,
        IReadOnlyDictionary<string, IReadOnlySet<string>> hardConflicts)
    {
        var adjacency = new Dictionary<string, List<ContinuationNeighbor>>(StringComparer.Ordinal);
        foreach (var relation in graph.Relations
                     .Where(relation =>
                         relation.Kind == StructuralEvidenceRelationKind.Continuation
                         && relation.Weight > 0)
                     .OrderByDescending(relation => relation.Weight)
                     .ThenBy(relation => relation.Id, StringComparer.Ordinal))
        {
            AddContinuationNeighbor(
                adjacency,
                relation.FirstCandidateId,
                relation.SecondCandidateId,
                relation.Weight);
            AddContinuationNeighbor(
                adjacency,
                relation.SecondCandidateId,
                relation.FirstCandidateId,
                relation.Weight);
        }

        var seedIds = adjacency
            .Where(item =>
                !selected.Contains(item.Key)
                && candidatesById.TryGetValue(item.Key, out var candidate)
                && IsSafeBundleCandidate(candidate, options)
                && StructuralPlacementAuthorityEvaluator.CanParticipateInRecoveryBundle(candidate)
                && item.Value.Any(neighbor => selected.Contains(neighbor.CandidateId)))
            .Select(item => item.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var proposals = new List<StructuralBundleProposal>();
        foreach (var seedId in seedIds)
        {
            var additions = GrowContinuationBundle(
                seedId,
                adjacency,
                candidatesById,
                selected,
                options,
                hardConflicts);
            if (additions.Count < 2)
            {
                continue;
            }

            proposals.Add(
                new StructuralBundleProposal(
                    StructuralBundleKind.Continuation,
                    additions,
                    StructuralPriority: additions
                        .Select(candidateId => candidatesById[candidateId])
                        .Sum(candidate => candidate.DrawingLength)));
        }

        return proposals;
    }

    private static IReadOnlyList<string> GrowContinuationBundle(
        string seedId,
        IReadOnlyDictionary<string, List<ContinuationNeighbor>> adjacency,
        IReadOnlyDictionary<string, StructuralWallCandidate> candidatesById,
        IReadOnlySet<string> selected,
        StructuralSolverOptions options,
        IReadOnlyDictionary<string, IReadOnlySet<string>> hardConflicts)
    {
        var additions = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(seedId);
        while (queue.Count > 0
               && additions.Count < options.MaximumCoherentBundleSize)
        {
            var candidateId = queue.Dequeue();
            if (selected.Contains(candidateId)
                || additions.Contains(candidateId)
                || !candidatesById.TryGetValue(candidateId, out var candidate)
                || !IsSafeBundleCandidate(candidate, options)
                || !StructuralPlacementAuthorityEvaluator.CanParticipateInRecoveryBundle(candidate)
                || HasHardConflict(
                    new[] { candidateId },
                    new CombinedSelection(selected, additions),
                    hardConflicts))
            {
                continue;
            }

            additions.Add(candidateId);
            if (!adjacency.TryGetValue(candidateId, out var neighbors))
            {
                continue;
            }

            foreach (var neighbor in neighbors
                         .Where(neighbor => !selected.Contains(neighbor.CandidateId))
                         .OrderByDescending(neighbor => neighbor.Weight)
                         .ThenBy(neighbor => neighbor.CandidateId, StringComparer.Ordinal))
            {
                queue.Enqueue(neighbor.CandidateId);
            }
        }

        return additions.Order(StringComparer.Ordinal).ToArray();
    }

    private static bool IsSafeBundleCandidate(
        StructuralWallCandidate candidate,
        StructuralSolverOptions options) =>
        candidate.IsEligible
        && candidate.UnaryScore >= options.MinimumCoherentBundleCandidateScore
        && !candidate.HasStrongNegativeEvidence
        && !candidate.HasStrongRepeatedDetailEvidence
        && !candidate.HasAbsoluteBlockingEvidence;

    private static double BundleCandidatePriority(
        StructuralWallCandidate candidate)
    {
        var priority = candidate.WasAcceptedByPreliminaryPipeline ? 2.0 : 0;
        priority += candidate.HasIndependentWallBodyEvidence ? 1.5 : 0;
        priority += candidate.HasCrossDomainWallBodyEvidence ? 1.0 : 0;
        priority += candidate.Origins.HasFlag(StructuralCandidateOrigin.ExteriorShell) ? 0.75 : 0;
        priority += candidate.Origins.HasFlag(StructuralCandidateOrigin.WallGraph) ? 0.5 : 0;
        return priority + candidate.UnaryScore;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildHardConflictLookup(
        IReadOnlyList<StructuralEvidenceRelation> relations)
    {
        var lookup = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var relation in relations.Where(relation =>
                     relation.IsHardConstraint
                     && relation.Kind is
                         StructuralEvidenceRelationKind.Duplicate
                         or StructuralEvidenceRelationKind.Conflict))
        {
            AddHardConflict(lookup, relation.FirstCandidateId, relation.SecondCandidateId);
            AddHardConflict(lookup, relation.SecondCandidateId, relation.FirstCandidateId);
        }

        return lookup.ToDictionary(
            item => item.Key,
            item => (IReadOnlySet<string>)item.Value,
            StringComparer.Ordinal);
    }

    private static bool HasHardConflict(
        IReadOnlyList<string> additions,
        IReadOnlySet<string> selected,
        IReadOnlyDictionary<string, IReadOnlySet<string>> hardConflicts)
    {
        var tentative = new HashSet<string>(selected, StringComparer.Ordinal);
        foreach (var candidateId in additions)
        {
            if (hardConflicts.TryGetValue(candidateId, out var conflicts)
                && conflicts.Any(tentative.Contains))
            {
                return true;
            }

            tentative.Add(candidateId);
        }

        return false;
    }

    private static void AddContinuationNeighbor(
        IDictionary<string, List<ContinuationNeighbor>> adjacency,
        string candidateId,
        string neighborId,
        double weight)
    {
        if (!adjacency.TryGetValue(candidateId, out var neighbors))
        {
            neighbors = new List<ContinuationNeighbor>();
            adjacency.Add(candidateId, neighbors);
        }

        neighbors.Add(new ContinuationNeighbor(neighborId, weight));
    }

    private static void AddHardConflict(
        IDictionary<string, HashSet<string>> lookup,
        string candidateId,
        string conflictingCandidateId)
    {
        if (!lookup.TryGetValue(candidateId, out var conflicts))
        {
            conflicts = new HashSet<string>(StringComparer.Ordinal);
            lookup.Add(candidateId, conflicts);
        }

        conflicts.Add(conflictingCandidateId);
    }

    private sealed record StructuralBundleProposal(
        StructuralBundleKind Kind,
        IReadOnlyList<string> CandidateIds,
        double StructuralPriority)
    {
        public string StableKey =>
            $"{Kind}:{string.Join("|", CandidateIds.Order(StringComparer.Ordinal))}";
    }

    private sealed record ContinuationNeighbor(
        string CandidateId,
        double Weight);

    private sealed class CombinedSelection : IReadOnlySet<string>
    {
        private readonly IReadOnlySet<string> _first;
        private readonly IReadOnlySet<string> _second;

        public CombinedSelection(
            IReadOnlySet<string> first,
            IReadOnlySet<string> second)
        {
            _first = first;
            _second = second;
        }

        public int Count => _first.Union(_second, StringComparer.Ordinal).Count();

        public bool Contains(string item) =>
            _first.Contains(item) || _second.Contains(item);

        public IEnumerator<string> GetEnumerator() =>
            _first.Union(_second, StringComparer.Ordinal).GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();

        public bool IsProperSubsetOf(IEnumerable<string> other) =>
            this.ToHashSet(StringComparer.Ordinal).IsProperSubsetOf(other);

        public bool IsProperSupersetOf(IEnumerable<string> other) =>
            this.ToHashSet(StringComparer.Ordinal).IsProperSupersetOf(other);

        public bool IsSubsetOf(IEnumerable<string> other) =>
            this.ToHashSet(StringComparer.Ordinal).IsSubsetOf(other);

        public bool IsSupersetOf(IEnumerable<string> other) =>
            this.ToHashSet(StringComparer.Ordinal).IsSupersetOf(other);

        public bool Overlaps(IEnumerable<string> other) =>
            this.ToHashSet(StringComparer.Ordinal).Overlaps(other);

        public bool SetEquals(IEnumerable<string> other) =>
            this.ToHashSet(StringComparer.Ordinal).SetEquals(other);
    }
}
