namespace OpenPlanTrace;

public sealed record PipelineStageContract(
    bool WritesOnlyDeclaredArtifacts,
    IReadOnlyList<PlanArtifactKind> DeclaredWrites,
    IReadOnlyList<PlanArtifactKind> ChangedArtifacts,
    IReadOnlyList<PlanArtifactKind> UndeclaredChangedArtifacts,
    IReadOnlyList<PlanArtifactKind> DeclaredUnchangedArtifacts)
{
    public static PipelineStageContract Empty { get; } = new(
        true,
        Array.Empty<PlanArtifactKind>(),
        Array.Empty<PlanArtifactKind>(),
        Array.Empty<PlanArtifactKind>(),
        Array.Empty<PlanArtifactKind>());

    public static PipelineStageContract From(
        IEnumerable<PlanArtifactKind> declaredWrites,
        IEnumerable<PlanArtifactKind> changedArtifacts)
    {
        ArgumentNullException.ThrowIfNull(declaredWrites);
        ArgumentNullException.ThrowIfNull(changedArtifacts);

        var declared = Normalize(declaredWrites);
        var changed = Normalize(changedArtifacts);
        var declaredSet = declared.ToHashSet();
        var changedSet = changed.ToHashSet();
        var undeclared = changed.Where(artifact => !declaredSet.Contains(artifact)).ToArray();
        var unchanged = declared.Where(artifact => !changedSet.Contains(artifact)).ToArray();

        return new PipelineStageContract(
            undeclared.Length == 0,
            declared,
            changed,
            undeclared,
            unchanged);
    }

    private static PlanArtifactKind[] Normalize(IEnumerable<PlanArtifactKind> artifacts) =>
        artifacts
            .Where(artifact => artifact != PlanArtifactKind.Unknown)
            .Distinct()
            .OrderBy(artifact => artifact.ToString(), StringComparer.Ordinal)
            .ToArray();
}
