namespace OpenPlanTrace;

public static class StructuralEvidenceGraphBuilder
{
    private static readonly IReadOnlyList<IStructuralEvidenceProducer> Producers =
        new IStructuralEvidenceProducer[]
        {
            new WallStructuralEvidenceProducer(),
            new DimensionStructuralEvidenceProducer(),
            new RoomBoundaryStructuralEvidenceProducer(),
            new WallBodyPlausibilityStructuralEvidenceProducer(),
            new OccupiedTerritoryStructuralEvidenceProducer(),
            new RepeatedPatternStructuralEvidenceProducer(),
            new TopologyStructuralEvidenceProducer(),
            new OpeningStructuralEvidenceProducer()
        };

    public static StructuralEvidenceGraph Build(
        StructuralEvidenceSource source,
        StructuralSolverOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        options ??= new StructuralSolverOptions();
        var context = new StructuralEvidenceBuildContext(source, options);
        foreach (var producer in Producers)
        {
            producer.Produce(context);
            context.Producers.Add(producer.Name);
        }

        var candidates = context.Candidates.Build();
        var relations = StructuralRelationBuilder.Build(candidates, options);
        context.Relations.AddRange(relations);

        return new StructuralEvidenceGraph(
            StructuralEvidenceGraph.CurrentContractVersion,
            candidates,
            context.Relations
                .GroupBy(relation => relation.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(relation => relation.Kind)
                .ThenBy(relation => relation.FirstCandidateId, StringComparer.Ordinal)
                .ThenBy(relation => relation.SecondCandidateId, StringComparer.Ordinal)
                .ToArray(),
            context.Junctions
                .OrderBy(junction => junction.PageNumber)
                .ThenBy(junction => junction.Position.Y)
                .ThenBy(junction => junction.Position.X)
                .ThenBy(junction => junction.Id, StringComparer.Ordinal)
                .ToArray(),
            context.RoomLoops
                .OrderBy(loop => loop.PageNumber)
                .ThenBy(loop => loop.SourceRoomId, StringComparer.Ordinal)
                .ToArray(),
            context.OpeningConstraints
                .OrderBy(constraint => constraint.PageNumber)
                .ThenBy(constraint => constraint.SourceOpeningId, StringComparer.Ordinal)
                .ToArray(),
            context.Producers.ToArray(),
            new[]
            {
                $"retained {candidates.Count} structural wall candidate(s) until joint solving",
                $"built {relations.Count} pairwise structural relation(s)",
                $"built {context.RoomLoops.Count} room loop candidate(s)",
                $"built {context.Junctions.Count} junction candidate(s)",
                $"built {context.OpeningConstraints.Count} opening constraint(s)"
            });
    }
}

internal interface IStructuralEvidenceProducer
{
    string Name { get; }

    void Produce(StructuralEvidenceBuildContext context);
}

internal sealed class StructuralEvidenceBuildContext
{
    public StructuralEvidenceBuildContext(
        StructuralEvidenceSource source,
        StructuralSolverOptions options)
    {
        Source = source;
        Options = options;
    }

    public StructuralEvidenceSource Source { get; }

    public StructuralSolverOptions Options { get; }

    public StructuralCandidateRegistry Candidates { get; } = new();

    public List<StructuralEvidenceRelation> Relations { get; } = new();

    public List<StructuralJunctionCandidate> Junctions { get; } = new();

    public List<StructuralRoomLoopCandidate> RoomLoops { get; } = new();

    public List<StructuralOpeningConstraint> OpeningConstraints { get; } = new();

    public List<string> Producers { get; } = new();

    public string CandidateId(string wallId) => $"structural:wall:{wallId}";
}
