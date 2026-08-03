namespace OpenPlanTrace;

internal sealed class StructuralObjective
{
    private readonly StructuralEvidenceGraph _graph;
    private readonly StructuralSolverOptions _options;
    private readonly IReadOnlyDictionary<string, StructuralWallCandidate> _candidates;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<StructuralEvidenceRelation>> _relationsByCandidate;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<StructuralRoomLoopCandidate>> _loopsByCandidate;

    public StructuralObjective(
        StructuralEvidenceGraph graph,
        StructuralSolverOptions options)
    {
        _graph = graph;
        _options = options;
        _candidates = graph.WallCandidates.ToDictionary(candidate => candidate.Id, StringComparer.Ordinal);
        _relationsByCandidate = BuildRelationsByCandidate(graph.Relations);
        _loopsByCandidate = BuildLoopsByCandidate(graph.RoomLoops);
    }

    public double Evaluate(IReadOnlySet<string> selected)
    {
        var objective = selected
            .Where(_candidates.ContainsKey)
            .Sum(candidateId => UnaryContribution(_candidates[candidateId]));

        objective += _graph.Relations
            .Where(relation =>
                selected.Contains(relation.FirstCandidateId)
                && selected.Contains(relation.SecondCandidateId))
            .Sum(RelationContribution);
        objective += _graph.RoomLoops.Sum(loop => RoomLoopContribution(loop, selected));
        objective += ExteriorContinuityContribution(selected);
        return objective;
    }

    public double ToggleDelta(
        string candidateId,
        IReadOnlySet<string> selected,
        bool add)
    {
        if (!_candidates.TryGetValue(candidateId, out var candidate)
            || add == selected.Contains(candidateId))
        {
            return 0;
        }

        var direction = add ? 1.0 : -1.0;
        var delta = UnaryContribution(candidate) * direction;

        if (_relationsByCandidate.TryGetValue(candidateId, out var relations))
        {
            foreach (var relation in relations)
            {
                var otherId = string.Equals(relation.FirstCandidateId, candidateId, StringComparison.Ordinal)
                    ? relation.SecondCandidateId
                    : relation.FirstCandidateId;
                if (selected.Contains(otherId))
                {
                    delta += RelationContribution(relation) * direction;
                }
            }
        }

        if (_loopsByCandidate.TryGetValue(candidateId, out var loops))
        {
            foreach (var loop in loops)
            {
                var before = RoomLoopContribution(loop, selected);
                var afterSelected = new ToggledSelection(selected, candidateId, add);
                var after = RoomLoopContribution(loop, afterSelected);
                delta += after - before;
            }
        }

        delta += ExteriorContinuityToggleDelta(candidate, selected, add);
        return delta;
    }

    public double CandidateContribution(
        string candidateId,
        IReadOnlySet<string> selected)
    {
        if (!_candidates.TryGetValue(candidateId, out var candidate)
            || !selected.Contains(candidateId))
        {
            return 0;
        }

        var contribution = UnaryContribution(candidate);
        if (_relationsByCandidate.TryGetValue(candidateId, out var relations))
        {
            contribution += relations
                .Where(relation =>
                    selected.Contains(relation.FirstCandidateId)
                    && selected.Contains(relation.SecondCandidateId))
                .Sum(relation => RelationContribution(relation) / 2.0);
        }

        if (_loopsByCandidate.TryGetValue(candidateId, out var loops))
        {
            contribution += loops.Sum(loop =>
                CandidateRoomShare(candidateId, loop, selected));
        }

        return contribution;
    }

    public double RoomBoundaryCoverage(
        StructuralRoomLoopCandidate loop,
        IReadOnlySet<string> selected)
    {
        var perimeter = loop.BoundaryEdges.Sum(edge => edge.DrawingLength);
        if (perimeter <= 0)
        {
            return 0;
        }

        var covered = loop.BoundaryEdges
            .Where(edge => edge.CandidateIds.Any(selected.Contains))
            .Sum(edge => edge.DrawingLength);
        return Math.Clamp(covered / perimeter, 0, 1);
    }

    private double UnaryContribution(StructuralWallCandidate candidate)
    {
        if (!candidate.IsEligible)
        {
            return -_options.HardConflictPenalty;
        }

        if (!StructuralPlacementAuthorityEvaluator.Evaluate(candidate).CanSelect)
        {
            return -_options.HardConflictPenalty;
        }

        var score = candidate.UnaryScore;
        if (candidate.Origins.HasFlag(StructuralCandidateOrigin.ExteriorShell))
        {
            score += 0.04;
        }

        return score;
    }

    private double RelationContribution(StructuralEvidenceRelation relation)
    {
        if (relation.IsHardConstraint
            && relation.Kind is StructuralEvidenceRelationKind.Duplicate or StructuralEvidenceRelationKind.Conflict)
        {
            return -_options.HardConflictPenalty;
        }

        return relation.Weight;
    }

    private double RoomLoopContribution(
        StructuralRoomLoopCandidate loop,
        IReadOnlySet<string> selected)
    {
        var coverage = RoomBoundaryCoverage(loop, selected);
        var contribution = loop.Weight * coverage * coverage * 0.55;
        if (coverage >= 0.90)
        {
            contribution += loop.Weight * _options.RoomClosureBonus;
        }

        return contribution;
    }

    private double CandidateRoomShare(
        string candidateId,
        StructuralRoomLoopCandidate loop,
        IReadOnlySet<string> selected)
    {
        var selectedEdges = loop.BoundaryEdges
            .Where(edge => edge.CandidateIds.Any(selected.Contains))
            .ToArray();
        if (selectedEdges.Length == 0)
        {
            return 0;
        }

        var candidateLength = selectedEdges
            .Where(edge => edge.CandidateIds.Contains(candidateId, StringComparer.Ordinal))
            .Sum(edge => edge.DrawingLength);
        var selectedLength = selectedEdges.Sum(edge => edge.DrawingLength);
        if (candidateLength <= 0 || selectedLength <= 0)
        {
            return 0;
        }

        return RoomLoopContribution(loop, selected) * candidateLength / selectedLength;
    }

    private double ExteriorContinuityContribution(IReadOnlySet<string> selected)
    {
        var exteriorSelected = selected
            .Where(_candidates.ContainsKey)
            .Where(candidateId =>
                _candidates[candidateId].Origins.HasFlag(StructuralCandidateOrigin.ExteriorShell)
                || _candidates[candidateId].WallType == WallType.Exterior)
            .ToArray();
        if (exteriorSelected.Length == 0)
        {
            return 0;
        }

        var supported = exteriorSelected.Count(candidateId =>
            HasSelectedContinuity(candidateId, selected));
        return _options.ExteriorContinuityBonus * supported / exteriorSelected.Length;
    }

    private double ExteriorContinuityToggleDelta(
        StructuralWallCandidate candidate,
        IReadOnlySet<string> selected,
        bool add)
    {
        if (!candidate.Origins.HasFlag(StructuralCandidateOrigin.ExteriorShell)
            && candidate.WallType != WallType.Exterior)
        {
            return 0;
        }

        var before = ExteriorContinuityContribution(selected);
        var after = ExteriorContinuityContribution(new ToggledSelection(selected, candidate.Id, add));
        return after - before;
    }

    private bool HasSelectedContinuity(
        string candidateId,
        IReadOnlySet<string> selected)
    {
        return _relationsByCandidate.TryGetValue(candidateId, out var relations)
            && relations.Any(relation =>
                relation.Kind is StructuralEvidenceRelationKind.Continuation
                    or StructuralEvidenceRelationKind.Junction
                && selected.Contains(
                    string.Equals(relation.FirstCandidateId, candidateId, StringComparison.Ordinal)
                        ? relation.SecondCandidateId
                        : relation.FirstCandidateId));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<StructuralEvidenceRelation>> BuildRelationsByCandidate(
        IReadOnlyList<StructuralEvidenceRelation> relations)
    {
        var result = new Dictionary<string, List<StructuralEvidenceRelation>>(StringComparer.Ordinal);
        foreach (var relation in relations)
        {
            Add(result, relation.FirstCandidateId, relation);
            Add(result, relation.SecondCandidateId, relation);
        }

        return result.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<StructuralEvidenceRelation>)item.Value,
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<StructuralRoomLoopCandidate>> BuildLoopsByCandidate(
        IReadOnlyList<StructuralRoomLoopCandidate> loops)
    {
        var result = new Dictionary<string, List<StructuralRoomLoopCandidate>>(StringComparer.Ordinal);
        foreach (var loop in loops)
        {
            foreach (var candidateId in loop.BoundaryEdges
                         .SelectMany(edge => edge.CandidateIds)
                         .Distinct(StringComparer.Ordinal))
            {
                if (!result.TryGetValue(candidateId, out var values))
                {
                    values = new List<StructuralRoomLoopCandidate>();
                    result.Add(candidateId, values);
                }

                values.Add(loop);
            }
        }

        return result.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<StructuralRoomLoopCandidate>)item.Value,
            StringComparer.Ordinal);
    }

    private static void Add(
        IDictionary<string, List<StructuralEvidenceRelation>> result,
        string candidateId,
        StructuralEvidenceRelation relation)
    {
        if (!result.TryGetValue(candidateId, out var values))
        {
            values = new List<StructuralEvidenceRelation>();
            result.Add(candidateId, values);
        }

        values.Add(relation);
    }

    private sealed class ToggledSelection : IReadOnlySet<string>
    {
        private readonly IReadOnlySet<string> _source;
        private readonly string _candidateId;
        private readonly bool _add;

        public ToggledSelection(
            IReadOnlySet<string> source,
            string candidateId,
            bool add)
        {
            _source = source;
            _candidateId = candidateId;
            _add = add;
        }

        public int Count => _source.Count + (_add ? 1 : -1);

        public bool Contains(string item) =>
            string.Equals(item, _candidateId, StringComparison.Ordinal)
                ? _add
                : _source.Contains(item);

        public IEnumerator<string> GetEnumerator()
        {
            foreach (var value in _source)
            {
                if (!string.Equals(value, _candidateId, StringComparison.Ordinal))
                {
                    yield return value;
                }
            }

            if (_add)
            {
                yield return _candidateId;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public bool IsProperSubsetOf(IEnumerable<string> other) => this.ToHashSet().IsProperSubsetOf(other);

        public bool IsProperSupersetOf(IEnumerable<string> other) => this.ToHashSet().IsProperSupersetOf(other);

        public bool IsSubsetOf(IEnumerable<string> other) => this.ToHashSet().IsSubsetOf(other);

        public bool IsSupersetOf(IEnumerable<string> other) => this.ToHashSet().IsSupersetOf(other);

        public bool Overlaps(IEnumerable<string> other) => this.ToHashSet().Overlaps(other);

        public bool SetEquals(IEnumerable<string> other) => this.ToHashSet().SetEquals(other);
    }
}
